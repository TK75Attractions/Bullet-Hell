using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    [Header("Serial Input")]
    public string portName = "COM3";
    public int baudRate = 115200;

    private object serialPort;
    private PropertyInfo isOpenProperty;
    private PropertyInfo bytesToReadProperty;
    private MethodInfo readLineMethod;
    private bool serialButtonState;   // P1 の A(決定/ダッシュ)
    private bool serialBackState;     // P1 の B(戻る)。S プロトコルの bit5
    private Vector2 serialMove;
    private string latestRawLine = "";

    // --- 2P シリアル(S プロトコル)の生ビット。S 行受信で更新、UpdateInput で展開 ---
    // 各バイトのビット割り当て: bit0=上,1=下,2=左,3=右,4=A(決定/ダッシュ),5=B(戻る)。押下=1。
    private const int BitUp = 0, BitDown = 1, BitLeft = 2, BitRight = 3, BitA = 4, BitB = 5;
    private byte serialP1Bits;   // 最新の S 行の P1 バイト
    private byte serialP2Bits;   // 最新の S 行の P2 バイト
    private bool sProtocolSeen;  // "S xx yy" を一度でも受信したか(F2 表示用)
    private bool helloSeen;      // "HELLO" を一度でも受信したか(F2 表示用)

    // true = シリアル(ESP32)を開かずキーボードのみ(COM 未接続時の警告を避けたいとき)。
    // false(既定)= シリアルも開いてキーボードと併用。いずれの場合もキーボードは常に有効。
    public bool isDebugMode = false;

    // true = 2 人プレイ。P2 のキーボード代替(矢印キー+RightShift/Enter)を有効にし、
    // その分 P1 のキーボードは WASD のみへ狭める(矢印を P2 に譲る)。false(既定)では
    // 従来どおり矢印も WASD も P1 に入る=1P の入力挙動は完全に不変。タイトルの人数選択で
    // GManager が設定する。実機シリアル(S 行)の P2 ビットは本フラグに関係なく常に P2 へ入る。
    public bool twoPlayerMode = false;

    // --- P1(既存 1P プレイヤーが消費する状態)---
    public bool buttonPressed;
    public bool buttonPressedThisFrame;
    public bool backPressed;
    public bool backPressedThisFrame;
    public bool upPressed;
    public bool downPressed;
    public bool leftPressed;
    public bool rightPressed;
    public bool upPressedThisFrame;
    public bool downPressedThisFrame;
    public bool leftPressedThisFrame;
    public bool rightPressedThisFrame;

    // --- P2(入力基盤として公開。ゲームロジックの2P化は別便)---
    // シリアル(ESP32)の P2 側のみを反映する。キーボードには P2 を割り当てない。
    public bool p2ButtonPressed;
    public bool p2ButtonPressedThisFrame;
    public bool p2BackPressed;
    public bool p2BackPressedThisFrame;
    public bool p2Up;
    public bool p2Down;
    public bool p2Left;
    public bool p2Right;
    public bool p2UpThisFrame;
    public bool p2DownThisFrame;
    public bool p2LeftThisFrame;
    public bool p2RightThisFrame;

    // --- 入力の向き設定(F2 デバッグ画面で切替。P1/P2 個別)---
    // 筐体の設置(ジョイスティックの物理的な取り付け向き)は P1/P2 で別々に変わり得るため、
    // 入力方向を 90°刻みで回転(0/90/180/270 = CCW)し、必要なら軸反転もできるようにする。
    // 添字 [0]=P1, [1]=P2。PlayerPrefs で永続化。
    private readonly int[] inputRotation = new int[2];   // 0..3 = 反時計回り 90°×n
    private readonly bool[] inputFlipX = new bool[2];    // 回転後に左右反転
    private readonly bool[] inputFlipY = new bool[2];    // 回転後に上下反転

    // P1 は旧キーを据え置いて後方互換、P2 は末尾 2。
    private static readonly string[] RotPrefKey = { "inputDir.rotation", "inputDir.rotation2" };
    private static readonly string[] FlipXPrefKey = { "inputDir.flipX", "inputDir.flipX2" };
    private static readonly string[] FlipYPrefKey = { "inputDir.flipY", "inputDir.flipY2" };
    private bool orientationLoaded = false;

    // F2 デバッグ画面が参照する getter(player: 0=P1, 1=P2)。
    public int InputRotation(int player) => inputRotation[Clamp01(player)];             // 0..3
    public int InputRotationDegrees(int player) => inputRotation[Clamp01(player)] * 90; // 0/90/180/270
    public bool InputFlipX(int player) => inputFlipX[Clamp01(player)];
    public bool InputFlipY(int player) => inputFlipY[Clamp01(player)];

    public Vector2 Move => serialMove;                    // P1 の移動ベクトル(向き変換前の生値)
    public string LatestRawLine => latestRawLine;
    public bool SProtocolSeen => sProtocolSeen;
    public bool HelloSeen => helloSeen;
    public string ConfigFilePath => ConfigPath;

    // True while the ESP32 serial port is actually open. Always false in
    // keyboard debug mode (no port is opened). Consumed by InputDebugOverlay
    // to show live connection status.
    public bool IsConnected => IsSerialOpen();

    // Human-readable outcome of the last Init(), surfaced by InputDebugOverlay so
    // the reason serial is dead (esp. "SerialPort type unavailable") is visible
    // in-game without digging through the Console.
    public string InitStatus { get; private set; } = "not initialized";

    private static string ConfigPath => System.IO.Path.Combine(Application.persistentDataPath, "controller_serial.cfg");
    private static int Clamp01(int p) => p <= 0 ? 0 : 1;

    public void Init()
    {
        LoadOrientationPrefs();
        LoadSerialConfig();

        if (isDebugMode)
        {
            InitStatus = "keyboard only (serial disabled)";
            Debug.Log("InputManager: serial disabled. Keyboard input only.");
            return;
        }

        OpenSerialPort();
    }

    // ポートを開く実体。Init と F2 の再接続の両方から呼ぶ。
    private void OpenSerialPort()
    {
        try
        {
            Type serialPortType = Type.GetType("System.IO.Ports.SerialPort, System") ?? Type.GetType("System.IO.Ports.SerialPort");
            if (serialPortType == null)
            {
                InitStatus = "SerialPort type missing - set Api Compatibility Level to .NET Framework";
                Debug.LogError("SerialPort is not available in this build/runtime.");
                return;
            }

            serialPort = System.Activator.CreateInstance(serialPortType, portName, baudRate);
            serialPortType.GetProperty("ReadTimeout")?.SetValue(serialPort, 5);
            serialPortType.GetProperty("NewLine")?.SetValue(serialPort, "\n");

            isOpenProperty = serialPortType.GetProperty("IsOpen");
            bytesToReadProperty = serialPortType.GetProperty("BytesToRead");
            readLineMethod = serialPortType.GetMethod("ReadLine");

            serialPortType.GetMethod("Open")?.Invoke(serialPort, null);
            InitStatus = $"listening {portName} @ {baudRate}";
            Debug.Log($"Serial port opened: {portName} ({baudRate})");
        }
        catch (System.Exception e)
        {
            // ESP32 未接続時はここに来るが、キーボードは常時有効なので致命的ではない。
            InitStatus = "serial open failed (keyboard still works): " + e.Message;
            Debug.LogWarning("Serial port open failed (ESP32 未接続?). Keyboard input still works: " + e.Message);
            CloseSerialPort();
        }
    }

    // F2 デバッグ画面から呼ぶ。ポートを閉じて開き直す(COM 番号変更後の再接続など)。
    public void ReconnectSerial()
    {
        CloseSerialPort();
        if (isDebugMode)
        {
            InitStatus = "keyboard only (serial disabled)";
            return;
        }
        InitStatus = "reconnecting...";
        OpenSerialPort();
    }

    // F2 デバッグ画面から呼ぶ。portName 末尾の COM 番号を増減して再接続する(例 COM3→COM4)。
    public void CyclePort(int delta)
    {
        int n = ExtractComNumber(portName, 3);
        n = Mathf.Clamp(n + delta, 1, 40);
        portName = "COM" + n;
        SaveSerialConfig();
        ReconnectSerial();
    }

    private static int ExtractComNumber(string port, int fallback)
    {
        if (string.IsNullOrEmpty(port)) return fallback;
        int i = port.Length;
        while (i > 0 && char.IsDigit(port[i - 1])) i--;
        return (i < port.Length && int.TryParse(port.Substring(i), out int n)) ? n : fallback;
    }

    public void UpdateInput()
    {
        // キーボードとシリアル(ESP32)を両方受け付ける。キーボードは常時有効、シリアルは
        // ポートが開いているとき(接続時)だけマージする。どちらでも操作できる。
        Keyboard keyboard = Keyboard.current;

        bool prevButtonState = buttonPressed;
        bool prevBackState = backPressed;
        bool prevUpPressed = upPressed;
        bool prevDownPressed = downPressed;
        bool prevLeftPressed = leftPressed;
        bool prevRightPressed = rightPressed;

        bool prevP2Button = p2ButtonPressed;
        bool prevP2Back = p2BackPressed;
        bool prevP2Up = p2Up;
        bool prevP2Down = p2Down;
        bool prevP2Left = p2Left;
        bool prevP2Right = p2Right;

        // --- キーボード(常時) ---
        bool kb = keyboard != null;
        // P1 キーボード: 1P モードは従来どおり WASD と矢印の両方(1P 挙動不変)。
        // 2P モードでは矢印を P2 のテスト操作へ譲るため P1 は WASD のみへ狭める。
        bool p1Arrows = !twoPlayerMode;
        bool kbButton = kb && keyboard.spaceKey.isPressed;
        bool kbUp = kb && (keyboard.wKey.isPressed || (p1Arrows && keyboard.upArrowKey.isPressed));
        bool kbDown = kb && (keyboard.sKey.isPressed || (p1Arrows && keyboard.downArrowKey.isPressed));
        bool kbLeft = kb && (keyboard.aKey.isPressed || (p1Arrows && keyboard.leftArrowKey.isPressed));
        bool kbRight = kb && (keyboard.dKey.isPressed || (p1Arrows && keyboard.rightArrowKey.isPressed));
        bool kbBack = kb && keyboard.escapeKey.isPressed;

        // P2 キーボード(実機スティックが無い開発/テスト用)。2P モードのときだけ矢印キーを
        // P2 移動に、RightShift/Enter を P2 の決定(ダッシュ)に割り当てる。1P モードでは全て false。
        bool kbP2Up = twoPlayerMode && kb && keyboard.upArrowKey.isPressed;
        bool kbP2Down = twoPlayerMode && kb && keyboard.downArrowKey.isPressed;
        bool kbP2Left = twoPlayerMode && kb && keyboard.leftArrowKey.isPressed;
        bool kbP2Right = twoPlayerMode && kb && keyboard.rightArrowKey.isPressed;
        bool kbP2Button = twoPlayerMode && kb && (keyboard.rightShiftKey.isPressed || keyboard.enterKey.isPressed);

        // --- シリアル(接続時のみ)。1 行ずつ解析して状態を更新 ---
        if (serialPort != null && IsSerialOpen())
        {
            try
            {
                while (GetBytesToRead() > 0)
                {
                    string message = ReadSerialLine();
                    if (string.IsNullOrEmpty(message))
                    {
                        break;
                    }

                    ProcessSerialLine(message);
                }
            }
            catch (System.Reflection.TargetInvocationException tie) when (tie.InnerException is System.TimeoutException)
            {
                // Ignore partial lines and continue reading next frame.
            }
            catch (System.Exception e)
            {
                Debug.LogError("Serial read error: " + e.Message);
            }
        }

        // --- P1 シリアル方向(serialMove 由来) + P1 の向き変換 ---
        bool serUp = serialMove.y > 0.5f;
        bool serDown = serialMove.y < -0.5f;
        bool serLeft = serialMove.x < -0.5f;
        bool serRight = serialMove.x > 0.5f;

        // 筐体の設置向きに合わせた回転/反転(F2 デバッグで切替、既定は無変換)は
        // ジョイスティック(シリアル)入力にのみ適用する。キーボード WASD は開発/操作用で
        // 筐体の物理的な設置向きとは無関係なため、回転させると操作が破綻する(実機不具合)。
        ApplyInputOrientation(0, ref serUp, ref serDown, ref serLeft, ref serRight);

        // --- P2 シリアル方向(S プロトコルのビット由来) + P2 の向き変換 ---
        bool s2Up = (serialP2Bits & (1 << BitUp)) != 0;
        bool s2Down = (serialP2Bits & (1 << BitDown)) != 0;
        bool s2Left = (serialP2Bits & (1 << BitLeft)) != 0;
        bool s2Right = (serialP2Bits & (1 << BitRight)) != 0;
        ApplyInputOrientation(1, ref s2Up, ref s2Down, ref s2Left, ref s2Right);
        bool s2A = (serialP2Bits & (1 << BitA)) != 0;
        bool s2B = (serialP2Bits & (1 << BitB)) != 0;

        // --- P1: キーボード OR シリアルをマージ ---
        upPressed = kbUp || serUp;
        downPressed = kbDown || serDown;
        leftPressed = kbLeft || serLeft;
        rightPressed = kbRight || serRight;
        buttonPressed = kbButton || serialButtonState;

        // 戻る: Esc または P1 の B ボタンは全画面で有効(メニュー操作 = P1 の設計)。
        backPressed = kbBack || serialBackState;

        // --- P2: シリアル(S 行) OR キーボード(2P モード時の矢印/RightShift) ---
        p2Up = s2Up || kbP2Up;
        p2Down = s2Down || kbP2Down;
        p2Left = s2Left || kbP2Left;
        p2Right = s2Right || kbP2Right;
        p2ButtonPressed = s2A || kbP2Button;
        p2BackPressed = s2B;

        // --- this-frame エッジ(前フレームとの差分)---
        buttonPressedThisFrame = !prevButtonState && buttonPressed;
        backPressedThisFrame = !prevBackState && backPressed;
        upPressedThisFrame = !prevUpPressed && upPressed;
        downPressedThisFrame = !prevDownPressed && downPressed;
        leftPressedThisFrame = !prevLeftPressed && leftPressed;
        rightPressedThisFrame = !prevRightPressed && rightPressed;

        p2ButtonPressedThisFrame = !prevP2Button && p2ButtonPressed;
        p2BackPressedThisFrame = !prevP2Back && p2BackPressed;
        p2UpThisFrame = !prevP2Up && p2Up;
        p2DownThisFrame = !prevP2Down && p2Down;
        p2LeftThisFrame = !prevP2Left && p2Left;
        p2RightThisFrame = !prevP2Right && p2Right;
    }

    // シリアル 1 行を解析して内部状態を更新する。実シリアル読みと、テスト/デバッグの
    // 疑似注入(InjectSerialLine)の両方から使う共通経路。
    private void ProcessSerialLine(string message)
    {
        latestRawLine = message.Trim();

        // 起動時の接続確認行 "HELLO 2P v2"。状態は変えず、接続確認フラグだけ立てる。
        if (latestRawLine.StartsWith("HELLO", StringComparison.Ordinal))
        {
            helloSeen = true;
            return;
        }

        // 2P ファームの正プロトコル "S <P1hex> <P2hex>"(bit0=上,1=下,2=左,3=右,4=A,5=B)。
        if (TryParseSLine(latestRawLine, out byte p1, out byte p2))
        {
            ApplySLine(p1, p2);
            return;
        }

        // 旧プロトコル(1P): JSON Lines {"x":0,"y":-1,"dash":false}。
        if (TryParseJsonInput(latestRawLine, out Vector2 jsonMove, out bool jsonDash))
        {
            serialMove = jsonMove;
            serialButtonState = jsonDash;
            return;
        }

        // 最古のフォールバック(1P): Dir:LEFT / PRESSED / RELEASED。
        if (latestRawLine.Contains("PRESSED"))
        {
            serialButtonState = true;
        }
        else if (latestRawLine.Contains("RELEASED"))
        {
            serialButtonState = false;
        }
        ParseDirLine(latestRawLine);
    }

    // テスト/デバッグ用。実シリアルを介さず 1 行を注入し、同じ解析経路を通す。
    // 注入後に UpdateInput() を呼ぶと展開結果(upPressed/p2Up など)を検証できる。
    public void InjectSerialLine(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return;
        }
        ProcessSerialLine(line);
    }

    // "S <P1hex> <P2hex>" を解釈して P1/P2 の状態バイトを得る(純粋関数・EditMode テスト用)。
    // 例: "S 05 21" → p1=0x05, p2=0x21。hex は 1〜2 桁を許容。壊れた行は false。
    public static bool TryParseSLine(string line, out byte p1, out byte p2)
    {
        p1 = 0;
        p2 = 0;
        if (string.IsNullOrEmpty(line))
        {
            return false;
        }

        string t = line.Trim();
        if (t.Length < 2 || (t[0] != 'S' && t[0] != 's') || t[1] != ' ')
        {
            return false;
        }

        string[] parts = t.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            return false;
        }

        return TryParseHexByte(parts[1], out p1) && TryParseHexByte(parts[2], out p2);
    }

    private static bool TryParseHexByte(string s, out byte value)
    {
        return byte.TryParse(s, System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    private void ApplySLine(byte p1, byte p2)
    {
        serialP1Bits = p1;
        serialP2Bits = p2;
        sProtocolSeen = true;

        // P1 は既存の serialMove/serialButtonState 経路に流し、1P プレイヤーがそのまま消費する。
        serialMove = BitsToMove(p1);
        serialButtonState = (p1 & (1 << BitA)) != 0;
        serialBackState = (p1 & (1 << BitB)) != 0;
    }

    // 状態バイトの上下左右ビットを移動ベクトルへ(x=右+, y=上+)。
    private static Vector2 BitsToMove(byte b)
    {
        float x = ((b >> BitRight) & 1) - ((b >> BitLeft) & 1);
        float y = ((b >> BitUp) & 1) - ((b >> BitDown) & 1);
        return new Vector2(x, y);
    }

    // 上下左右の押下ブールに、指定プレイヤーの回転(反時計回り 90°×n)と軸反転を適用する。
    private void ApplyInputOrientation(int player, ref bool up, ref bool down, ref bool left, ref bool right)
    {
        int p = Clamp01(player);
        ApplyOrientation(inputRotation[p], inputFlipX[p], inputFlipY[p], ref up, ref down, ref left, ref right);
    }

    // 回転(反時計回り 90°×rotation) + 軸反転を適用する純粋関数(EditMode テスト用)。
    // 既定(回転0・反転なし)では即 return して従来挙動を完全に保つ。回転は方向の置換
    // として扱うため、同時押しや斜め入力の OR マージ結果もそのまま保存される。
    public static void ApplyOrientation(int rotation, bool flipX, bool flipY,
        ref bool up, ref bool down, ref bool left, ref bool right)
    {
        rotation = ((rotation % 4) + 4) % 4;
        if (rotation == 0 && !flipX && !flipY)
        {
            return;
        }

        bool u = up, d = down, l = left, r = right;

        // 反時計回りに 90°ずつ回す: up<-right, left<-up, down<-left, right<-down
        for (int i = 0; i < rotation; i++)
        {
            bool nu = r, nl = u, nd = l, nr = d;
            u = nu; l = nl; d = nd; r = nr;
        }

        if (flipX) { bool t = l; l = r; r = t; }
        if (flipY) { bool t = u; u = d; d = t; }

        up = u; down = d; left = l; right = r;
    }

    private void LoadOrientationPrefs()
    {
        if (orientationLoaded)
        {
            return;
        }
        for (int p = 0; p < 2; p++)
        {
            inputRotation[p] = ((PlayerPrefs.GetInt(RotPrefKey[p], 0) % 4) + 4) % 4;
            inputFlipX[p] = PlayerPrefs.GetInt(FlipXPrefKey[p], 0) != 0;
            inputFlipY[p] = PlayerPrefs.GetInt(FlipYPrefKey[p], 0) != 0;
        }
        orientationLoaded = true;
    }

    private void SaveOrientationPrefs(int player)
    {
        int p = Clamp01(player);
        PlayerPrefs.SetInt(RotPrefKey[p], inputRotation[p]);
        PlayerPrefs.SetInt(FlipXPrefKey[p], inputFlipX[p] ? 1 : 0);
        PlayerPrefs.SetInt(FlipYPrefKey[p], inputFlipY[p] ? 1 : 0);
        PlayerPrefs.Save();
    }

    // F2 デバッグ画面のボタンから呼ぶ。delta=+1 で反時計回りに 90°進める。
    public void CycleInputRotation(int player, int delta)
    {
        LoadOrientationPrefs();
        int p = Clamp01(player);
        inputRotation[p] = ((inputRotation[p] + delta) % 4 + 4) % 4;
        SaveOrientationPrefs(p);
    }

    public void ToggleInputFlipX(int player)
    {
        LoadOrientationPrefs();
        int p = Clamp01(player);
        inputFlipX[p] = !inputFlipX[p];
        SaveOrientationPrefs(p);
    }

    public void ToggleInputFlipY(int player)
    {
        LoadOrientationPrefs();
        int p = Clamp01(player);
        inputFlipY[p] = !inputFlipY[p];
        SaveOrientationPrefs(p);
    }

    public void ResetInputOrientation(int player)
    {
        LoadOrientationPrefs();
        int p = Clamp01(player);
        inputRotation[p] = 0;
        inputFlipX[p] = false;
        inputFlipY[p] = false;
        SaveOrientationPrefs(p);
    }

    // --- シリアル設定ファイル(ポート/ボーレート)。persistentDataPath に置く ---
    private void LoadSerialConfig()
    {
        try
        {
            if (!System.IO.File.Exists(ConfigPath))
            {
                // 初回はひな形を書き出し、ユーザーが手で編集できるようにする。
                SaveSerialConfig();
                return;
            }

            foreach (string raw in System.IO.File.ReadAllLines(ConfigPath))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }
                string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                string val = line.Substring(eq + 1).Trim();
                if (key == "port" && val.Length > 0)
                {
                    portName = val;
                }
                else if (key == "baud" && int.TryParse(val, out int b) && b > 0)
                {
                    baudRate = b;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("controller_serial.cfg 読み込み失敗(既定値を使用): " + e.Message);
        }
    }

    private void SaveSerialConfig()
    {
        try
        {
            string content =
                "# ESP32 2P コントローラーのシリアル設定\n" +
                "# port: 使用する COM ポート(例 COM3)\n" +
                "# baud: ボーレート(ファーム既定 115200)\n" +
                "port=" + portName + "\n" +
                "baud=" + baudRate + "\n";
            System.IO.File.WriteAllText(ConfigPath, content);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("controller_serial.cfg 書き込み失敗: " + e.Message);
        }
    }

    private void OnDestroy()
    {
        CloseSerialPort();
    }

    private void OnApplicationQuit()
    {
        CloseSerialPort();
    }

    private void CloseSerialPort()
    {
        if (serialPort == null)
        {
            return;
        }

        if (IsSerialOpen())
        {
            serialPort.GetType().GetMethod("Close")?.Invoke(serialPort, null);
        }

        serialPort.GetType().GetMethod("Dispose")?.Invoke(serialPort, null);
        serialPort = null;
        isOpenProperty = null;
        bytesToReadProperty = null;
        readLineMethod = null;
    }

    private bool IsSerialOpen()
    {
        if (serialPort == null || isOpenProperty == null)
        {
            return false;
        }

        object value = isOpenProperty.GetValue(serialPort);
        return value is bool isOpen && isOpen;
    }

    private int GetBytesToRead()
    {
        if (serialPort == null || bytesToReadProperty == null)
        {
            return 0;
        }

        object value = bytesToReadProperty.GetValue(serialPort);
        return value is int bytesToRead ? bytesToRead : 0;
    }

    private string ReadSerialLine()
    {
        if (serialPort == null || readLineMethod == null)
        {
            return null;
        }

        return readLineMethod.Invoke(serialPort, null) as string;
    }

    // esp32-s3-serial-controller の旧シリアル出力(JSON Lines)を表す。
    //   {"x":0,"y":-1,"dash":false}
    [Serializable]
    private struct SerialInputJson
    {
        public int x;
        public int y;
        public bool dash;
    }

    /// <summary>
    /// 旧 1P ファームの JSON 行を解釈して移動ベクトルとボタン(ダッシュ/決定)状態を得る。
    /// x=右+、ジョイスティックの上下左右がそのまま選択/移動に対応する。
    /// ファームは y = down - up(下が+1)で送るため、Unity の「上が+」に合わせて反転する。
    /// JSON でない/壊れた行は false を返し、旧プロトコルにフォールバックさせる。
    /// </summary>
    private bool TryParseJsonInput(string line, out Vector2 move, out bool dash)
    {
        move = Vector2.zero;
        dash = false;

        if (string.IsNullOrEmpty(line) || line.Length < 2 || line[0] != '{' || line[line.Length - 1] != '}')
        {
            return false;
        }

        try
        {
            SerialInputJson data = JsonUtility.FromJson<SerialInputJson>(line);
            float x = Mathf.Clamp(data.x, -1, 1);
            float y = Mathf.Clamp(data.y, -1, 1);
            move = new Vector2(x, -y);
            dash = data.dash;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void ParseDirLine(string line)
    {
        if (!line.StartsWith("Dir:"))
        {
            return;
        }

        float x = 0f;
        float y = 0f;

        if (line.Contains("LEFT")) x -= 1f;
        if (line.Contains("RIGHT")) x += 1f;
        if (line.Contains("UP")) y += 1f;
        if (line.Contains("DOWN")) y -= 1f;

        serialMove = new Vector2(x, y);
        if (serialMove.sqrMagnitude > 1f)
        {
            serialMove.Normalize();
        }
    }
}

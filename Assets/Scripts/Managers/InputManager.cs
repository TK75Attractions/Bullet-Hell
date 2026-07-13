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
    private bool serialButtonState;
    private Vector2 serialMove;
    private string latestRawLine = "";

    // true = シリアル(ESP32)を開かずキーボードのみ(COM4 未接続時の警告を避けたいとき)。
    // false(既定)= シリアルも開いてキーボードと併用。いずれの場合もキーボードは常に有効。
    public bool isDebugMode = false;
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

    // --- 入力の向き設定(F2 デバッグ画面で切替) ---
    // 筐体の設置(ジョイスティックの物理的な取り付け向き)は設計によって変わるため、
    // 入力方向を 90°刻みで回転(0/90/180/270 = CCW)し、必要なら軸反転もできるようにする。
    // serialMove 由来のブールにもキーボード由来のブールにも同じ変換をかけるので、実機の
    // ジョイスティックが無い環境でも WASD で切替動作を確認できる。PlayerPrefs で永続化。
    private int inputRotation = 0;   // 0..3 = 反時計回り 90°×n
    private bool inputFlipX = false; // 回転後に左右反転
    private bool inputFlipY = false; // 回転後に上下反転

    private const string RotPrefKey = "inputDir.rotation";
    private const string FlipXPrefKey = "inputDir.flipX";
    private const string FlipYPrefKey = "inputDir.flipY";
    private bool orientationLoaded = false;

    public int InputRotation => inputRotation;             // 0..3
    public int InputRotationDegrees => inputRotation * 90; // 0/90/180/270
    public bool InputFlipX => inputFlipX;
    public bool InputFlipY => inputFlipY;

    public Vector2 Move => serialMove;
    public string LatestRawLine => latestRawLine;

    // True while the ESP32 serial port is actually open. Always false in
    // keyboard debug mode (no port is opened). Consumed by InputDebugOverlay
    // to show live connection status.
    public bool IsConnected => IsSerialOpen();

    // Human-readable outcome of the last Init(), surfaced by InputDebugOverlay so
    // the reason serial is dead (esp. "SerialPort type unavailable") is visible
    // in-game without digging through the Console.
    public string InitStatus { get; private set; } = "not initialized";

    public void Init()
    {
        LoadOrientationPrefs();

        if (isDebugMode)
        {
            InitStatus = "keyboard only (serial disabled)";
            Debug.Log("InputManager: serial disabled. Keyboard input only.");
            return;
        }

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

    public void UpdateInput()
    {
        // キーボードとシリアル(ESP32)を両方受け付ける。キーボードは常時有効、シリアルは
        // ポートが開いているとき(接続時)だけマージする。どちらでも操作できる。
        Keyboard keyboard = Keyboard.current;

        bool prevButtonState = buttonPressed;
        bool prevUpPressed = upPressed;
        bool prevDownPressed = downPressed;
        bool prevLeftPressed = leftPressed;
        bool prevRightPressed = rightPressed;

        // --- キーボード(常時) ---
        bool kbButton = keyboard != null && keyboard.spaceKey.isPressed;
        bool kbUp = keyboard != null && (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed);
        bool kbDown = keyboard != null && (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed);
        bool kbLeft = keyboard != null && (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed);
        bool kbRight = keyboard != null && (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed);
        bool kbBack = keyboard != null && keyboard.escapeKey.isPressed;

        // --- シリアル(接続時のみ)。serialMove/serialButtonState を更新 ---
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

                    latestRawLine = message.Trim();

                    // 新プロトコル(esp32-s3-serial-controller): JSON Lines を約60fpsで送る。
                    //   {"x":0,"y":-1,"dash":false}
                    // JSON 行はこちらで処理し、それ以外は旧プロトコル(Dir:/PRESSED)にフォールバック。
                    if (TryParseJsonInput(latestRawLine, out Vector2 jsonMove, out bool jsonDash))
                    {
                        serialMove = jsonMove;
                        serialButtonState = jsonDash;
                    }
                    else
                    {
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

        bool serUp = serialMove.y > 0.5f;
        bool serDown = serialMove.y < -0.5f;
        bool serLeft = serialMove.x < -0.5f;
        bool serRight = serialMove.x > 0.5f;

        // 筐体の設置向きに合わせた回転/反転(F2 デバッグで切替、既定は無変換)は
        // ジョイスティック(シリアル)入力にのみ適用する。キーボード WASD は開発/操作用で
        // 筐体の物理的な設置向きとは無関係なため、回転させると操作が破綻する(実機不具合)。
        ApplyInputOrientation(ref serUp, ref serDown, ref serLeft, ref serRight);

        // --- キーボード OR シリアルをマージ ---
        upPressed = kbUp || serUp;
        downPressed = kbDown || serDown;
        leftPressed = kbLeft || serLeft;
        rightPressed = kbRight || serRight;
        buttonPressed = kbButton || serialButtonState;
        backPressed = kbBack;

        buttonPressedThisFrame = !prevButtonState && buttonPressed;
        upPressedThisFrame = !prevUpPressed && upPressed;
        downPressedThisFrame = !prevDownPressed && downPressed;
        leftPressedThisFrame = !prevLeftPressed && leftPressed;
        rightPressedThisFrame = !prevRightPressed && rightPressed;
        backPressedThisFrame = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
    }

    // 上下左右の押下ブールに、設定された回転(反時計回り 90°×n)と軸反転を適用する。
    // 既定(回転0・反転なし)では即 return して従来挙動を完全に保つ。回転は方向の置換
    // として扱うため、同時押しや斜め入力の OR マージ結果もそのまま保存される。
    private void ApplyInputOrientation(ref bool up, ref bool down, ref bool left, ref bool right)
    {
        if (inputRotation == 0 && !inputFlipX && !inputFlipY)
        {
            return;
        }

        bool u = up, d = down, l = left, r = right;

        // 反時計回りに 90°ずつ回す: up<-right, left<-up, down<-left, right<-down
        for (int i = 0; i < inputRotation; i++)
        {
            bool nu = r, nl = u, nd = l, nr = d;
            u = nu; l = nl; d = nd; r = nr;
        }

        if (inputFlipX) { bool t = l; l = r; r = t; }
        if (inputFlipY) { bool t = u; u = d; d = t; }

        up = u; down = d; left = l; right = r;
    }

    private void LoadOrientationPrefs()
    {
        if (orientationLoaded)
        {
            return;
        }
        inputRotation = ((PlayerPrefs.GetInt(RotPrefKey, 0) % 4) + 4) % 4;
        inputFlipX = PlayerPrefs.GetInt(FlipXPrefKey, 0) != 0;
        inputFlipY = PlayerPrefs.GetInt(FlipYPrefKey, 0) != 0;
        orientationLoaded = true;
    }

    private void SaveOrientationPrefs()
    {
        PlayerPrefs.SetInt(RotPrefKey, inputRotation);
        PlayerPrefs.SetInt(FlipXPrefKey, inputFlipX ? 1 : 0);
        PlayerPrefs.SetInt(FlipYPrefKey, inputFlipY ? 1 : 0);
        PlayerPrefs.Save();
    }

    // F2 デバッグ画面のボタンから呼ぶ。delta=+1 で反時計回りに 90°進める。
    public void CycleInputRotation(int delta)
    {
        LoadOrientationPrefs();
        inputRotation = ((inputRotation + delta) % 4 + 4) % 4;
        SaveOrientationPrefs();
    }

    public void ToggleInputFlipX()
    {
        LoadOrientationPrefs();
        inputFlipX = !inputFlipX;
        SaveOrientationPrefs();
    }

    public void ToggleInputFlipY()
    {
        LoadOrientationPrefs();
        inputFlipY = !inputFlipY;
        SaveOrientationPrefs();
    }

    public void ResetInputOrientation()
    {
        inputRotation = 0;
        inputFlipX = false;
        inputFlipY = false;
        orientationLoaded = true;
        SaveOrientationPrefs();
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

    // esp32-s3-serial-controller のシリアル出力(JSON Lines)を表す。
    //   {"x":0,"y":-1,"dash":false}
    [Serializable]
    private struct SerialInputJson
    {
        public int x;
        public int y;
        public bool dash;
    }

    /// <summary>
    /// ESP32 の JSON 行を解釈して移動ベクトルとボタン(ダッシュ/決定)状態を得る。
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

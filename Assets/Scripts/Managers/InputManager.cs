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

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

    public bool isDebugMode = false;
    public bool buttonPressed;
    public bool buttonPressedThisFrame;
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

    public void Init()
    {
        if (isDebugMode)
        {
            Debug.Log("InputManager is in debug mode. Using keyboard input.");
            return;
        }

        try
        {
            Type serialPortType = Type.GetType("System.IO.Ports.SerialPort, System") ?? Type.GetType("System.IO.Ports.SerialPort");
            if (serialPortType == null)
            {
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
            Debug.Log($"Serial port opened: {portName} ({baudRate})");
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to open serial port: " + e.Message);
            CloseSerialPort();
        }
    }

    public void UpdateInput()
    {
        if (isDebugMode)
        {
            buttonPressed = Keyboard.current.spaceKey.isPressed;
            buttonPressedThisFrame = Keyboard.current.spaceKey.wasPressedThisFrame;
            upPressed = Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed;
            downPressed = Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed;
            leftPressed = Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed;
            rightPressed = Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed;

            upPressedThisFrame = Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame;
            downPressedThisFrame = Keyboard.current.sKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame;
            leftPressedThisFrame = Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame;
            rightPressedThisFrame = Keyboard.current.dKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame;
            return;
        }

        bool prevButtonState = buttonPressed;
        bool prevUpPressed = upPressed;
        bool prevDownPressed = downPressed;
        bool prevLeftPressed = leftPressed;
        bool prevRightPressed = rightPressed;

        buttonPressedThisFrame = false;
        upPressed = false;
        downPressed = false;
        leftPressed = false;
        rightPressed = false;
        upPressedThisFrame = false;
        downPressedThisFrame = false;
        leftPressedThisFrame = false;
        rightPressedThisFrame = false;

        if (serialPort != null && IsSerialOpen())
        {
            try
            {
                if (GetBytesToRead() > 0)
                {
                    while (GetBytesToRead() > 0)
                    {
                        string message = ReadSerialLine();
                        if (string.IsNullOrEmpty(message))
                        {
                            break;
                        }

                        latestRawLine = message.Trim();

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

        buttonPressed = serialButtonState;
        buttonPressedThisFrame = !prevButtonState && buttonPressed;

        upPressed = serialMove.y > 0.5f;
        downPressed = serialMove.y < -0.5f;
        leftPressed = serialMove.x < -0.5f;
        rightPressed = serialMove.x > 0.5f;

        upPressedThisFrame = !prevUpPressed && upPressed;
        downPressedThisFrame = !prevDownPressed && downPressed;
        leftPressedThisFrame = !prevLeftPressed && leftPressed;
        rightPressedThisFrame = !prevRightPressed && rightPressed;
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
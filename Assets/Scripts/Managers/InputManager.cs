using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    [Header("TCP Settings")]
    [SerializeField] private string esp32Ip = "192.168.4.1";
    [SerializeField] private int port = 5000;
    [SerializeField] private int reconnectIntervalMs = 1500;

    [Header("Input Settings")]
    [SerializeField] private bool autoStartServer = true;
    [SerializeField] private bool useKeyboardInput = true;

    [Header("Events")]
    public UnityEvent OnUpSwing;
    public UnityEvent OnDownSwing;

    [Header("Current Input State (Read-Only)")]
    public bool up;
    public bool down;
    public bool left;
    public bool right;
    


    private TcpClient _client;
    private NetworkStream _stream;
    private Thread _serverThread;
    private volatile bool _running;

    private readonly ConcurrentQueue<string> _messageQueue = new();
    private bool _upPulse;
    private bool _downPulse;

    public void AwakeSetting(bool keyBoard)
    {
        useKeyboardInput = keyBoard;
    }

    private void Start()
    {
        if (!autoStartServer) return;
        Connect();
    }

    public void UpdateInput()
    {
        DrainMessageQueue();

        bool keyboardUp = false;
        bool keyboardDown = false;
        bool keyboardLeft = false;
        bool keyboardRight = false;

        if (useKeyboardInput)
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                keyboardUp = keyboard.upArrowKey.isPressed;
                keyboardDown = keyboard.downArrowKey.isPressed;
                keyboardLeft = keyboard.leftArrowKey.isPressed;
                keyboardRight = keyboard.rightArrowKey.isPressed;
            }
        }

        // Treat TCP swings as one-frame button pulses.
        bool tcpUp = _upPulse;
        bool tcpDown = _downPulse;
        _upPulse = false;
        _downPulse = false;

        up = keyboardUp || tcpUp;
        down = keyboardDown || tcpDown;
        left = keyboardLeft;
        right = keyboardRight;
    }

    public void Connect()
    {
        if (_running) return;

        _running = true;
        _serverThread = new Thread(ClientLoop)
        {
            IsBackground = true
        };
        _serverThread.Start();
    }

    public void DisConnected()
    {
        if (!_running) return;

        _running = false;

        try { _stream?.Close(); } catch { }
        try { _client?.Close(); } catch { }
        try
        {
            if (_serverThread != null && _serverThread.IsAlive)
            {
                _serverThread.Join(200);
            }
        }
        catch { }

        _client = null;
        _stream = null;
        _serverThread = null;
    }

    private void ClientLoop()
    {
        byte[] buffer = new byte[256];
        StringBuilder sb = new StringBuilder();

        try
        {
            while (_running)
            {
                if (_client == null || !_client.Connected)
                {
                    TryConnectToEsp32();
                    if (!_running) break;

                    if (_client == null || !_client.Connected)
                    {
                        Thread.Sleep(reconnectIntervalMs);
                        continue;
                    }
                }

                try
                {
                    if (_stream == null)
                    {
                        _stream = _client.GetStream();
                        _stream.ReadTimeout = 1000;
                    }

                    if (!_stream.DataAvailable)
                    {
                        Thread.Sleep(5);
                        continue;
                    }

                    int len = _stream.Read(buffer, 0, buffer.Length);
                    if (len <= 0)
                    {
                        HandleConnectionLost();
                        continue;
                    }

                    sb.Append(Encoding.UTF8.GetString(buffer, 0, len));
                    ParseBufferedLines(sb);
                }
                catch (IOException e)
                {
                    if (IsReadTimeout(e))
                    {
                        continue;
                    }
                    HandleConnectionLost();
                }
                catch (SocketException)
                {
                    HandleConnectionLost();
                }
                catch (ObjectDisposedException)
                {
                    HandleConnectionLost();
                }
                catch (Exception e)
                {
                    if (_running)
                    {
                        Debug.LogError("[TCP] Client loop error: " + e.Message);
                    }
                    HandleConnectionLost();
                }
            }
        }
        finally
        {
            try { _stream?.Close(); } catch { }
            try { _client?.Close(); } catch { }
            _stream = null;
            _client = null;
        }
    }

    private void TryConnectToEsp32()
    {
        try
        {
            _client?.Close();
            _client = new TcpClient();
            _client.NoDelay = true;
            _client.Connect(esp32Ip, port);
            _stream = _client.GetStream();
            _stream.ReadTimeout = 1000;
            Debug.Log($"[TCP] Connected to ESP32 {esp32Ip}:{port}");
        }
        catch (Exception e)
        {
            try { _stream?.Close(); } catch { }
            try { _client?.Close(); } catch { }
            _stream = null;
            _client = null;

            if (_running)
            {
                Debug.LogWarning("[TCP] Connect failed: " + e.Message);
            }
        }
    }

    private void HandleConnectionLost()
    {
        try { _stream?.Close(); } catch { }
        try { _client?.Close(); } catch { }
        _stream = null;
        _client = null;

        if (_running)
        {
            Debug.LogWarning("[TCP] ESP32 disconnected");
        }
    }

    private void ParseBufferedLines(StringBuilder sb)
    {
        while (true)
        {
            string all = sb.ToString();
            int idx = all.IndexOf('\n');
            if (idx < 0) break;

            string line = all.Substring(0, idx).Trim();
            sb.Remove(0, idx + 1);
            _messageQueue.Enqueue(line);
        }
    }

    private static bool IsReadTimeout(IOException e)
    {
        if (e.InnerException is SocketException socketEx)
        {
            return socketEx.SocketErrorCode == SocketError.TimedOut;
        }

        return e.Message.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void DrainMessageQueue()
    {
        while (_messageQueue.TryDequeue(out string msg))
        {
            HandleMessage(msg);
        }
    }

    private void HandleMessage(string line)
    {
        string msg = (line ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(msg)) return;

        if (IsUpSwingMessage(msg))
        {
            _upPulse = true;
            OnUpSwing?.Invoke();
            Debug.Log("[TCP] UP swing");
            return;
        }

        if (IsDownSwingMessage(msg))
        {
            _downPulse = true;
            OnDownSwing?.Invoke();
            Debug.Log("[TCP] DOWN swing");
            return;
        }

        if (msg == "ESP32_CONNECTED")
        {
            Debug.Log("[TCP] Handshake received");
            return;
        }

        Debug.Log("[TCP] Other: " + msg);
    }

    private static bool IsUpSwingMessage(string msg)
    {
        return msg == "UP"
            || msg == "UPSWING"
            || msg.StartsWith("UP:", StringComparison.Ordinal)
            || msg.StartsWith("UP_", StringComparison.Ordinal)
            || msg.StartsWith("UP ", StringComparison.Ordinal);
    }

    private static bool IsDownSwingMessage(string msg)
    {
        return msg == "DOWN"
            || msg == "DOWNSWING"
            || msg.StartsWith("DOWN:", StringComparison.Ordinal)
            || msg.StartsWith("DOWN_", StringComparison.Ordinal)
            || msg.StartsWith("DOWN ", StringComparison.Ordinal);
    }

    private void OnDestroy()
    {
        DisConnected();
    }

    private void OnApplicationQuit()
    {
        DisConnected();
    }
}

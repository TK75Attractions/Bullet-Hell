#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Editor-only on-screen overlay for verifying controller / joystick input at
/// runtime. Reads the live <see cref="InputManager"/> state and shows:
///   - input mode (serial ESP32-S3 controller vs keyboard debug fallback)
///   - serial connection status (port name / baud / OPEN|CLOSED) + reconnect
///   - the 2P protocol handshake state (HELLO seen / S line seen)
///   - the latest raw line received from the controller
///   - P1 and P2 direction booleans + A(dash/confirm) / B(back) buttons
///   - per-player input orientation (90 deg rotate + axis flip) toggles
///
/// Self-bootstraps on entering Play mode; no scene edits required. Gated behind
/// UNITY_EDITOR so it never ships in a festival build. Hidden by default; toggle
/// with F2 (StageTimeOverlay owns F1). ASCII-only labels to stay within the
/// built-in font's glyph set.
/// </summary>
public class InputDebugOverlay : MonoBehaviour
{
    private static InputDebugOverlay instance;
    // 既定は非表示。入力確認が必要なときだけ F2 で表示する。
    private bool visible = false;

    private const float PanelW = 360f;
    private const float PanelH = 396f;
    private const string NumFmt = "+0.00;-0.00;0.00";

    private static readonly Color OnColor = new Color(0.45f, 0.95f, 0.55f);
    private static readonly Color OffColor = new Color(0.42f, 0.48f, 0.60f);
    private static readonly Color WarnColor = new Color(0.98f, 0.68f, 0.30f);

    private GUIStyle headStyle;
    private GUIStyle rowStyle;
    private GUIStyle rawStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        GameObject go = new GameObject("~InputDebugOverlay");
        go.hideFlags = HideFlags.HideAndDontSave;
        Object.DontDestroyOnLoad(go);
        instance = go.AddComponent<InputDebugOverlay>();
    }

    private void Update()
    {
        // Input System package is the active handler; the legacy UnityEngine.Input
        // API throws every frame, so read the keyboard through Keyboard.current.
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.f2Key.wasPressedThisFrame)
        {
            visible = !visible;
        }
    }

    private void EnsureStyles()
    {
        if (headStyle != null) return;
        headStyle = new GUIStyle
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft
        };
        headStyle.normal.textColor = new Color(0.85f, 0.92f, 1f);
        rowStyle = new GUIStyle(headStyle) { fontSize = 15, fontStyle = FontStyle.Normal };
        rowStyle.normal.textColor = new Color(0.80f, 0.86f, 0.98f);
        rawStyle = new GUIStyle(rowStyle) { fontSize = 13 };
        rawStyle.normal.textColor = new Color(0.62f, 0.72f, 0.90f);
    }

    private void OnGUI()
    {
        if (!visible) return;
        EnsureStyles();

        InputManager im = GManager.Control != null ? GManager.Control.IManager : null;

        const float x = 14f;
        float top = Screen.height - PanelH - 12f;   // bottom-left; StageTimeOverlay is top-left

        GUI.color = new Color(0.03f, 0.05f, 0.11f, 0.82f);
        GUI.DrawTexture(new Rect(x - 6f, top - 6f, PanelW, PanelH), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float y = top;
        GUI.Label(new Rect(x, y, PanelW, 24f), "INPUT DEBUG (F2)  -  2P", headStyle);
        y += 26f;

        if (im == null)
        {
            GUI.Label(new Rect(x, y, PanelW, 20f), "InputManager unavailable", rowStyle);
            return;
        }

        // KEYBOARD mode ignores the ESP32 serial entirely, so flag it amber: if a
        // physical controller does nothing, this is usually why.
        bool kb = im.isDebugMode;
        GUI.color = kb ? WarnColor : OnColor;
        GUI.Label(new Rect(x, y, PanelW, 20f),
            $"mode : {(kb ? "KEYBOARD (debug) - serial ignored" : "SERIAL (ESP32-S3)")}", rowStyle);
        GUI.color = Color.white;
        y += 20f;

        if (!kb)
        {
            bool connected = im.IsConnected;
            GUI.color = connected ? OnColor : WarnColor;
            GUI.Label(new Rect(x, y, PanelW, 20f),
                $"port : {im.portName} @ {im.baudRate}  [{(connected ? "OPEN" : "CLOSED")}]", rowStyle);
            GUI.color = Color.white;
            y += 20f;

            // Handshake: firmware sends "HELLO 2P v2" on boot, then "S xx yy" on change.
            GUI.color = im.SProtocolSeen ? OnColor : (im.HelloSeen ? WarnColor : OffColor);
            GUI.Label(new Rect(x, y, PanelW, 20f),
                $"proto: HELLO {(im.HelloSeen ? "OK" : "--")}   S-line {(im.SProtocolSeen ? "OK" : "--")}", rowStyle);
            GUI.color = Color.white;
            y += 20f;

            // Port controls: cycle COMx and reopen, without leaving Play mode.
            const float bh = 20f;
            float bx = x;
            if (GUI.Button(new Rect(bx, y, 46f, bh), "COM-")) im.CyclePort(-1);
            bx += 52f;
            if (GUI.Button(new Rect(bx, y, 46f, bh), "COM+")) im.CyclePort(1);
            bx += 52f;
            if (GUI.Button(new Rect(bx, y, 88f, bh), "RECONNECT")) im.ReconnectSerial();
            y += 24f;
        }
        else
        {
            GUI.Label(new Rect(x, y, PanelW, 20f), "keys : WASD / arrows, Space=dash, Esc=back", rawStyle);
            y += 20f;
        }

        // Init outcome: the fastest way to see why serial is dead (type missing,
        // open failed, wrong COM). Amber when it clearly signals a failure.
        string init = im.InitStatus;
        bool initBad = init.Contains("missing") || init.Contains("failed");
        GUI.color = initBad ? WarnColor : OffColor;
        GUI.Label(new Rect(x, y, PanelW, 18f), $"init : {Truncate(init, 40)}", rawStyle);
        GUI.color = Color.white;
        y += 22f;

        // === P1 ===
        Vector2 mv = im.Move;
        GUI.Label(new Rect(x, y, PanelW, 20f),
            $"P1   : move x={mv.x.ToString(NumFmt)} y={mv.y.ToString(NumFmt)}", rowStyle);
        DrawStick(x + PanelW - 66f, y - 4f, 46f,
            kb ? BoolStick(im.rightPressed, im.leftPressed, im.upPressed, im.downPressed) : mv);
        y += 22f;

        DrawFlag(x, y, "U", im.upPressed);
        DrawFlag(x + 32f, y, "D", im.downPressed);
        DrawFlag(x + 64f, y, "L", im.leftPressed);
        DrawFlag(x + 96f, y, "R", im.rightPressed);
        DrawFlag(x + 132f, y, "A", im.buttonPressed);
        DrawFlag(x + 176f, y, "B", im.backPressed);
        y += 22f;
        y += DrawOrientationRow(im, 0, x, y);

        // === P2 ===
        GUI.Label(new Rect(x, y, PanelW, 20f), "P2   : (serial only)", rowStyle);
        DrawStick(x + PanelW - 66f, y - 4f, 46f,
            BoolStick(im.p2Right, im.p2Left, im.p2Up, im.p2Down));
        y += 22f;

        DrawFlag(x, y, "U", im.p2Up);
        DrawFlag(x + 32f, y, "D", im.p2Down);
        DrawFlag(x + 64f, y, "L", im.p2Left);
        DrawFlag(x + 96f, y, "R", im.p2Right);
        DrawFlag(x + 132f, y, "A", im.p2ButtonPressed);
        DrawFlag(x + 176f, y, "B", im.p2BackPressed);
        y += 22f;
        y += DrawOrientationRow(im, 1, x, y);

        string raw = string.IsNullOrEmpty(im.LatestRawLine) ? "(none)" : im.LatestRawLine;
        GUI.Label(new Rect(x, y, PanelW, 18f), $"raw  : {Truncate(raw, 40)}", rawStyle);
    }

    // Draws the "dir : ROT.. FlipX.. FlipY.." status line plus the four toggle
    // buttons for one player. Returns the vertical space consumed.
    private float DrawOrientationRow(InputManager im, int player, float x, float y)
    {
        float y0 = y;
        int rotDeg = im.InputRotationDegrees(player);
        bool fx = im.InputFlipX(player);
        bool fy = im.InputFlipY(player);
        bool oriented = rotDeg != 0 || fx || fy;

        GUI.color = oriented ? WarnColor : OffColor;
        GUI.Label(new Rect(x, y, PanelW, 20f),
            $"dir  : ROT {rotDeg}  FlipX {(fx ? "ON" : "off")}  FlipY {(fy ? "ON" : "off")}", rowStyle);
        GUI.color = Color.white;
        y += 20f;

        const float bh = 20f;
        float bx = x;
        if (GUI.Button(new Rect(bx, y, 66f, bh), "ROT +90")) im.CycleInputRotation(player, 1);
        bx += 72f;
        if (GUI.Button(new Rect(bx, y, 52f, bh), fx ? "FX:ON" : "FX:off")) im.ToggleInputFlipX(player);
        bx += 58f;
        if (GUI.Button(new Rect(bx, y, 52f, bh), fy ? "FY:ON" : "FY:off")) im.ToggleInputFlipY(player);
        bx += 58f;
        if (GUI.Button(new Rect(bx, y, 48f, bh), "RESET")) im.ResetInputOrientation(player);
        y += 24f;

        return y - y0;
    }

    private static Vector2 BoolStick(bool right, bool left, bool up, bool down)
    {
        return new Vector2((right ? 1f : 0f) - (left ? 1f : 0f),
                           (up ? 1f : 0f) - (down ? 1f : 0f));
    }

    private void DrawFlag(float x, float y, string label, bool on)
    {
        GUI.color = on ? OnColor : OffColor;
        GUI.Label(new Rect(x, y, 44f, 20f), on ? $"[{label}]" : $" {label} ", rowStyle);
        GUI.color = Color.white;
    }

    private void DrawStick(float x, float y, float size, Vector2 move)
    {
        GUI.color = new Color(0.35f, 0.42f, 0.55f, 0.9f);
        GUI.DrawTexture(new Rect(x, y, size, size), Texture2D.whiteTexture);
        GUI.color = new Color(0.06f, 0.09f, 0.16f, 1f);
        GUI.DrawTexture(new Rect(x + 1f, y + 1f, size - 2f, size - 2f), Texture2D.whiteTexture);

        // crosshair
        GUI.color = new Color(0.25f, 0.31f, 0.42f, 1f);
        GUI.DrawTexture(new Rect(x + size * 0.5f - 0.5f, y + 2f, 1f, size - 4f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x + 2f, y + size * 0.5f - 0.5f, size - 4f, 1f), Texture2D.whiteTexture);

        // dot: x right+, y up+ (screen y grows downward, so invert y)
        float cx = x + size * 0.5f;
        float cy = y + size * 0.5f;
        float r = size * 0.42f;
        float dotX = cx + Mathf.Clamp(move.x, -1f, 1f) * r;
        float dotY = cy - Mathf.Clamp(move.y, -1f, 1f) * r;
        GUI.color = OnColor;
        GUI.DrawTexture(new Rect(dotX - 4f, dotY - 4f, 8f, 8f), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    private static string Truncate(string s, int max)
    {
        return s.Length <= max ? s : s.Substring(0, max) + "..";
    }
}
#endif

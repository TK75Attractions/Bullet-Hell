#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Editor-only on-screen overlay for verifying controller / joystick input at
/// runtime. Reads the live <see cref="InputManager"/> state and shows:
///   - input mode (serial ESP32-S3 controller vs keyboard debug fallback)
///   - serial connection status (port name / baud / OPEN|CLOSED)
///   - the latest raw line received from the controller
///   - the parsed move vector, both as text and a small stick visualization
///   - the dash / back buttons and up/down/left/right direction booleans
///
/// Self-bootstraps on entering Play mode; no scene edits required. Gated behind
/// UNITY_EDITOR so it never ships in a festival build. Hidden by default; toggle
/// with F2 (StageTimeOverlay owns F1). ASCII-only labels to stay within the
/// built-in font's glyph set.
/// </summary>
public class InputDebugOverlay : MonoBehaviour
{
    private static InputDebugOverlay instance;
    // Visible by default in the editor so input can be verified the moment Play
    // starts; F2 hides it when it gets in the way.
    private bool visible = true;

    private const float PanelW = 340f;
    private const float PanelH = 198f;
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

        GUI.color = new Color(0.03f, 0.05f, 0.11f, 0.80f);
        GUI.DrawTexture(new Rect(x - 6f, top - 6f, PanelW, PanelH), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float y = top;
        GUI.Label(new Rect(x, y, PanelW, 24f), "INPUT DEBUG (F2)", headStyle);
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
        }
        else
        {
            GUI.Label(new Rect(x, y, PanelW, 20f), "keys : WASD / arrows, Space=dash, Esc=back", rawStyle);
        }
        y += 20f;

        // Init outcome: the fastest way to see why serial is dead (type missing,
        // open failed, wrong COM). Amber when it clearly signals a failure.
        string init = im.InitStatus;
        bool initBad = init.Contains("missing") || init.Contains("failed");
        GUI.color = initBad ? WarnColor : OffColor;
        GUI.Label(new Rect(x, y, 250f, 18f), $"init : {Truncate(init, 36)}", rawStyle);
        GUI.color = Color.white;
        y += 20f;

        Vector2 mv = im.Move;
        GUI.Label(new Rect(x, y, PanelW, 20f),
            $"move : x={mv.x.ToString(NumFmt)}  y={mv.y.ToString(NumFmt)}", rowStyle);
        y += 22f;

        DrawFlag(x, y, "DASH", im.buttonPressed);
        DrawFlag(x + 96f, y, "BACK", im.backPressed);
        y += 22f;

        DrawFlag(x, y, "U", im.upPressed);
        DrawFlag(x + 40f, y, "D", im.downPressed);
        DrawFlag(x + 80f, y, "L", im.leftPressed);
        DrawFlag(x + 120f, y, "R", im.rightPressed);
        y += 24f;

        string raw = string.IsNullOrEmpty(im.LatestRawLine) ? "(none)" : im.LatestRawLine;
        GUI.Label(new Rect(x, y, PanelW - 66f, 18f), $"raw  : {Truncate(raw, 34)}", rawStyle);

        // Stick visualization, docked to the panel's right edge. Keyboard mode
        // never fills Move (analog), so derive the dot from the resolved
        // direction booleans there — that is what the game actually reads.
        Vector2 stickVec = kb
            ? new Vector2((im.rightPressed ? 1f : 0f) - (im.leftPressed ? 1f : 0f),
                          (im.upPressed ? 1f : 0f) - (im.downPressed ? 1f : 0f))
            : mv;
        DrawStick(x + PanelW - 78f, top + 34f, 58f, stickVec);
    }

    private void DrawFlag(float x, float y, string label, bool on)
    {
        GUI.color = on ? OnColor : OffColor;
        GUI.Label(new Rect(x, y, 92f, 20f), on ? $"[{label}]" : $" {label} ", rowStyle);
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

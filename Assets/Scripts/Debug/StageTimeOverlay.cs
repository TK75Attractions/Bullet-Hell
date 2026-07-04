#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Editor-only on-screen overlay that displays the running stage clock so a
/// reviewer can point at a moment ("その 63 秒のカッターを…") when giving
/// feedback. Shows elapsed seconds, bar:beat (from the stage's own BPM), and —
/// for the stone stage — the current / next authored marker (M#, label, time).
///
/// Self-bootstraps on entering Play mode; no scene edits required. Gated behind
/// UNITY_EDITOR so it never ships in a festival build. Toggle with F1.
/// Marker data comes from Resources/stone_markers.txt (generated from
/// stone.chart.json), so it stays in lockstep with the chart's single source.
/// </summary>
public class StageTimeOverlay : MonoBehaviour
{
    private struct Marker
    {
        public string id;      // "M12"
        public float time;     // seconds, computed from bar:beat + BPM
        public string label;   // human-readable JP label
    }

    private static StageTimeOverlay instance;
    private bool visible = true;

    private readonly List<Marker> stoneMarkers = new List<Marker>();
    private string loadedForStage;
    private GUIStyle bigStyle;
    private GUIStyle subStyle;
    private GUIStyle markerStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        GameObject go = new GameObject("~StageTimeOverlay");
        go.hideFlags = HideFlags.HideAndDontSave;
        Object.DontDestroyOnLoad(go);
        instance = go.AddComponent<StageTimeOverlay>();
    }

    private void Update()
    {
        // Input System package is the active input handler; the legacy
        // UnityEngine.Input API throws InvalidOperationException every frame.
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.f1Key.wasPressedThisFrame)
        {
            visible = !visible;
        }
    }

    private StageReader ActiveReader()
    {
        if (GManager.Control == null) return null;
        StageReader reader = GManager.Control.SReader;
        if (reader == null || !reader.IsReady) return null;
        if (GManager.Control.state != GManager.GameState.Playing) return null;
        return reader;
    }

    private void EnsureStyles()
    {
        if (bigStyle != null) return;
        bigStyle = new GUIStyle
        {
            fontSize = 30,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft
        };
        bigStyle.normal.textColor = new Color(0.85f, 0.92f, 1f);
        subStyle = new GUIStyle(bigStyle) { fontSize = 18, fontStyle = FontStyle.Normal };
        subStyle.normal.textColor = new Color(0.72f, 0.80f, 0.95f);
        markerStyle = new GUIStyle(subStyle) { fontSize = 16 };
        markerStyle.normal.textColor = new Color(0.62f, 0.72f, 0.90f);
    }

    private void LoadStoneMarkers(StageData stage)
    {
        if (loadedForStage == stage.stageName) return;
        loadedForStage = stage.stageName;
        stoneMarkers.Clear();

        // Marker resource + bar:beat semantics are stone-specific; other stages
        // still get seconds + bar:beat, just no marker line.
        if (stage.stageName != "石工") return;

        TextAsset asset = Resources.Load<TextAsset>("stone_markers");
        if (asset == null) return;

        float beatSeconds = BeatSeconds(stage, out int measure);
        foreach (string rawLine in asset.text.Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length == 0) continue;
            string[] parts = line.Split('|');
            if (parts.Length < 2) continue;
            if (!TryParseBarBeat(parts[1], measure, beatSeconds, out float sec)) continue;
            stoneMarkers.Add(new Marker
            {
                id = parts[0],
                time = sec,
                label = parts.Length >= 3 ? parts[2] : string.Empty
            });
        }
        stoneMarkers.Sort((a, b) => a.time.CompareTo(b.time));
    }

    private static float BeatSeconds(StageData stage, out int measure)
    {
        float bpm = 120f;
        measure = 4;
        if (stage.MusicEvents != null && stage.MusicEvents.Count > 0)
        {
            StageData.MusicEvent ev = stage.MusicEvents[0];
            if (ev.BPM > 0f) bpm = ev.BPM;
            if (ev.measure > 0) measure = ev.measure;
        }
        return 60f / bpm;
    }

    private static bool TryParseBarBeat(string barBeat, int measure, float beatSeconds, out float seconds)
    {
        seconds = 0f;
        string[] bb = barBeat.Split(':');
        if (bb.Length != 2) return false;
        if (!int.TryParse(bb[0], out int bar)) return false;
        if (!float.TryParse(bb[1], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float beat)) return false;
        seconds = ((bar - 1) * measure + (beat - 1f)) * beatSeconds;
        return true;
    }

    private void OnGUI()
    {
        if (!visible) return;
        StageReader reader = ActiveReader();
        if (reader == null) return;
        StageData stage = reader.CurrentStage;
        if (stage == null) return;

        EnsureStyles();
        LoadStoneMarkers(stage);

        float t = reader.CurrentTime;
        float beatSeconds = BeatSeconds(stage, out int measure);
        float totalBeats = t / beatSeconds;
        int bar = Mathf.FloorToInt(totalBeats / measure) + 1;
        float beatInBar = (totalBeats % measure) + 1f;

        const float x = 14f;
        float y = 10f;

        // Shaded backdrop for readability over the busy playfield.
        GUI.color = new Color(0.03f, 0.05f, 0.11f, 0.72f);
        GUI.DrawTexture(new Rect(x - 6f, y - 4f, 340f, stoneMarkers.Count > 0 ? 128f : 84f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(x, y, 400f, 40f), $"t = {t,6:0.00} s", bigStyle);
        y += 38f;
        GUI.Label(new Rect(x, y, 400f, 28f), $"小節 {bar} : 拍 {beatInBar:0.0}   (BPM {60f / beatSeconds:0})", subStyle);
        y += 26f;

        if (stoneMarkers.Count > 0)
        {
            (Marker cur, Marker next) = NearestMarkers(t);
            if (cur.id != null)
            {
                GUI.Label(new Rect(x, y, 400f, 24f), $"▶ {cur.id}  {cur.label}  ({cur.time:0.0}s)", markerStyle);
                y += 22f;
            }
            if (next.id != null)
            {
                GUI.Label(new Rect(x, y, 400f, 24f), $"… {next.id}  {next.label}  ({next.time:0.0}s / +{next.time - t:0.0}s)", markerStyle);
            }
        }
    }

    private (Marker cur, Marker next) NearestMarkers(float t)
    {
        Marker cur = default;
        Marker next = default;
        for (int i = 0; i < stoneMarkers.Count; i++)
        {
            if (stoneMarkers[i].time <= t)
            {
                cur = stoneMarkers[i];
            }
            else
            {
                next = stoneMarkers[i];
                break;
            }
        }
        return (cur, next);
    }
}
#endif

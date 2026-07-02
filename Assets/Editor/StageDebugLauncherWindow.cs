using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Generic in-editor stage launcher + seek console (P3/P4 debug tooling). Lists
/// every stage in the current Play session and starts the chosen one, and once a
/// stage is running exposes a scrubber, a seconds field, chart-marker jump buttons,
/// a screenshot button and a live stage-time readout. Seeking uses
/// <see cref="StageReader.SeekTo"/> (bullet-less headstart). The legacy
/// "Start Stone Stage" menu is kept untouched.
/// </summary>
public class StageDebugLauncherWindow : EditorWindow
{
    private const string MenuPath = "Tools/Bullet Hell/Debug/Start Stage...";
    private Vector2 scroll;
    private float seekSeconds;
    private string seekSecondsField = "0";
    private bool showStageList = true;
    private bool showMarkers = true;

    private List<StageSeekSupport.StageMarker> cachedMarkers = new List<StageSeekSupport.StageMarker>();
    private string cachedMarkerStageDir;
    private string markerError;

    [MenuItem(MenuPath)]
    private static void Open()
    {
        StageDebugLauncherWindow window = GetWindow<StageDebugLauncherWindow>(true, "Stage Debug");
        window.minSize = new Vector2(320, 320);
        window.Show();
    }

    private void OnInspectorUpdate()
    {
        // Keep the live time readout ticking without user interaction.
        if (EditorApplication.isPlaying) Repaint();
    }

    private void OnGUI()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Enter Play mode first, then pick a stage. This launcher does not enter Play mode automatically.",
                MessageType.Info);
            return;
        }

        if (GManager.Control == null || !GManager.Control.ready || GManager.Control.SDB == null)
        {
            EditorGUILayout.HelpBox("GManager is not ready yet. Wait for the title/menu to load.", MessageType.Warning);
            if (GUILayout.Button("Refresh")) Repaint();
            return;
        }

        DrawStageList();
        EditorGUILayout.Space(6);
        DrawSeekControls();
    }

    private void DrawStageList()
    {
        showStageList = EditorGUILayout.Foldout(showStageList, "Start a stage", true);
        if (!showStageList) return;

        List<string> names = StoneStageDebugMenu.GetStageNames();
        if (names.Count == 0)
        {
            EditorGUILayout.HelpBox("No stages found in the stage database.", MessageType.Warning);
            return;
        }

        for (int i = 0; i < names.Count; i++)
        {
            string stageName = names[i];
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(stageName, GUILayout.Height(22)))
                {
                    StoneStageDebugMenu.StartStageByName(stageName);
                }
                if (GUILayout.Button("+Rec", GUILayout.Width(52), GUILayout.Height(22)))
                {
                    StartAndRecord(stageName);
                }
            }
        }
    }

    /// <summary>
    /// Starts the stage and, once its reader is ready, begins recording — the
    /// one-click "Start + Record" flow. Recording auto-stops on Clear / play exit.
    /// </summary>
    private void StartAndRecord(string stageName)
    {
        StoneStageDebugMenu.StartStageByName(stageName);
        double deadline = EditorApplication.timeSinceStartup + 15d;
        void WaitThenRecord()
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > deadline)
            {
                EditorApplication.update -= WaitThenRecord;
                return;
            }
            StageReader reader = StageSeekSupport.CurrentReader();
            if (reader == null || !reader.IsReady || reader.CurrentStage == null) return;
            if (GManager.Control == null || GManager.Control.state != GManager.GameState.Playing) return;
            EditorApplication.update -= WaitThenRecord;
            StageRecorderMenu.StartRecording(StageSeekSupport.CurrentStageDir());
        }
        EditorApplication.update += WaitThenRecord;
    }

    private void DrawSeekControls()
    {
        StageReader reader = StageSeekSupport.CurrentReader();
        if (reader == null || reader.CurrentStage == null || !reader.IsReady)
        {
            EditorGUILayout.HelpBox("No stage is running yet. Start a stage above to enable seeking.", MessageType.Info);
            return;
        }

        StageData stage = reader.CurrentStage;
        float duration = Mathf.Max(0.01f, reader.GetStageDuration());
        float now = reader.CurrentTime;

        EditorGUILayout.LabelField("Seek", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            $"Stage: {stage.stageName}   Time: {StageSeekSupport.FormatSeconds(now)} / {StageSeekSupport.FormatSeconds(duration)}");

        // Scrubber.
        float scrubbed = EditorGUILayout.Slider(Mathf.Clamp(seekSeconds, 0f, duration), 0f, duration);
        if (!Mathf.Approximately(scrubbed, seekSeconds))
        {
            seekSeconds = scrubbed;
            seekSecondsField = seekSeconds.ToString("0.###");
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Seconds", GUILayout.Width(56));
            seekSecondsField = EditorGUILayout.TextField(seekSecondsField);
            if (GUILayout.Button("Set", GUILayout.Width(44)))
            {
                if (float.TryParse(seekSecondsField, out float parsed)) seekSeconds = Mathf.Clamp(parsed, 0f, duration);
            }
            if (GUILayout.Button("Seek", GUILayout.Width(60)))
            {
                if (float.TryParse(seekSecondsField, out float parsed)) seekSeconds = parsed;
                reader.SeekTo(seekSeconds);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Seek to current field")) reader.SeekTo(seekSeconds);
            if (GUILayout.Button("Screenshot")) StageSeekSupport.CaptureScreenshot();
        }

        EditorGUILayout.Space(4);
        DrawMarkers(reader, stage);
    }

    private void DrawMarkers(StageReader reader, StageData stage)
    {
        showMarkers = EditorGUILayout.Foldout(showMarkers, "Chart markers", true);
        if (!showMarkers) return;

        string dir = StageSeekSupport.CurrentStageDir();
        if (dir != cachedMarkerStageDir)
        {
            cachedMarkers = StageSeekSupport.LoadMarkers(stage, out markerError);
            cachedMarkerStageDir = dir;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Reload markers", GUILayout.Width(120)))
            {
                cachedMarkers = StageSeekSupport.LoadMarkers(stage, out markerError);
                cachedMarkerStageDir = dir;
            }
        }

        if (cachedMarkers.Count == 0)
        {
            EditorGUILayout.HelpBox(markerError ?? "This stage's chart has no markers.", MessageType.None);
            return;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(120));
        for (int i = 0; i < cachedMarkers.Count; i++)
        {
            StageSeekSupport.StageMarker marker = cachedMarkers[i];
            string label = string.IsNullOrEmpty(marker.Label)
                ? $"{marker.Name}  ({marker.Seconds:0.00}s)"
                : $"{marker.Name} {marker.Seconds:0.00}s  {marker.Label}";
            if (GUILayout.Button(label, GUILayout.Height(20)))
            {
                seekSeconds = marker.Seconds;
                seekSecondsField = seekSeconds.ToString("0.###");
                reader.SeekTo(marker.Seconds);
            }
        }
        EditorGUILayout.EndScrollView();
    }
}

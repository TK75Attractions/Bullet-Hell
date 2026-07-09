using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

/// <summary>
/// P4 productized screenshot hook. Arms an <see cref="EditorApplication.update"/>
/// watcher that snapshots the Game view the moment the running stage clock reaches
/// each requested second, writing
/// <c>Assets/Screenshots/capture_&lt;stage&gt;_&lt;time&gt;.png</c>. Self-unhooks once every
/// time has fired or Play mode ends. Replaces the hand-rolled audit hooks from the
/// stone sessions.
/// </summary>
public class StageCaptureMenu : EditorWindow
{
    private const string MenuPath = "Tools/Bullet Hell/Debug/Capture At Times...";

    private string timesField = "60, 61, 62, 63.333";

    private static readonly List<float> pendingTimes = new List<float>();
    private static int nextIndex;
    private static bool armed;
    private static string armedStageDir;

    [MenuItem(MenuPath)]
    private static void Open()
    {
        StageCaptureMenu window = GetWindow<StageCaptureMenu>(true, "Capture At Times");
        window.minSize = new Vector2(360, 180);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Capture At Stage Times", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Comma-separated seconds. Each is captured the moment the running stage clock reaches it, " +
            "into Assets/Screenshots/capture_<stage>_<time>.png.",
            MessageType.Info);

        timesField = EditorGUILayout.TextField("Times (sec)", timesField);

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            {
                if (GUILayout.Button("Arm"))
                {
                    Arm(timesField);
                }
            }
            if (GUILayout.Button("Disarm"))
            {
                Disarm();
            }
        }

        if (armed)
        {
            EditorGUILayout.HelpBox(
                $"Armed for '{armedStageDir}': {nextIndex}/{pendingTimes.Count} captured. " +
                $"Next at {(nextIndex < pendingTimes.Count ? pendingTimes[nextIndex].ToString("0.###") : "-")}s.",
                MessageType.None);
        }

        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play mode and start a stage before arming.", MessageType.Warning);
        }
    }

    private static void Arm(string field)
    {
        StageReader reader = StageSeekSupport.CurrentReader();
        if (reader == null || reader.CurrentStage == null)
        {
            Debug.LogWarning("[StageCapture] No stage is running.");
            return;
        }

        pendingTimes.Clear();
        foreach (string token in field.Split(','))
        {
            string t = token.Trim();
            if (t.Length == 0) continue;
            if (float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out float sec))
            {
                pendingTimes.Add(sec);
            }
            else
            {
                Debug.LogWarning($"[StageCapture] Skipped unparseable time '{t}'.");
            }
        }

        pendingTimes.Sort();
        if (pendingTimes.Count == 0)
        {
            Debug.LogWarning("[StageCapture] No valid times to capture.");
            return;
        }

        nextIndex = 0;
        armedStageDir = StageSeekSupport.CurrentStageDir();
        armed = true;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Debug.Log($"[StageCapture] Armed for '{armedStageDir}' at {pendingTimes.Count} time(s).");
    }

    private static void Disarm()
    {
        armed = false;
        EditorApplication.update -= Tick;
        pendingTimes.Clear();
        nextIndex = 0;
    }

    private static void Tick()
    {
        if (!armed) { EditorApplication.update -= Tick; return; }

        StageReader reader = StageSeekSupport.CurrentReader();
        if (!EditorApplication.isPlaying || reader == null || reader.CurrentStage == null)
        {
            Debug.Log("[StageCapture] Disarmed (play/stage ended).");
            Disarm();
            return;
        }

        float now = reader.CurrentTime;
        while (nextIndex < pendingTimes.Count && now >= pendingTimes[nextIndex])
        {
            float target = pendingTimes[nextIndex];
            string stage = StageSeekSupport.Sanitize(armedStageDir ?? "stage");
            string file = $"capture_{stage}_{target.ToString("0.00", CultureInfo.InvariantCulture)}.png";
            StageSeekSupport.CaptureScreenshot(file);
            Debug.Log($"[StageCapture] Captured at target {target:0.###}s (clock {now:0.###}s).");
            nextIndex++;
        }

        if (nextIndex >= pendingTimes.Count)
        {
            Debug.Log("[StageCapture] All captures complete.");
            Disarm();
        }
    }
}

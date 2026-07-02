using System;
using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEngine;

/// <summary>
/// P4 Recorder integration: records the running stage to
/// <c>Recordings/&lt;stage&gt;_&lt;timestamp&gt;.mp4</c> (1280x720 / 30fps / audio) via
/// Unity Recorder's <see cref="RecorderController"/>. Exposes a toggle menu and
/// static start/stop entry points the stage launcher window reuses for its
/// "Start + Record" one-click flow. Recording auto-stops when Play mode ends or the
/// game leaves the Playing state (e.g. a stage Clear).
/// </summary>
public static class StageRecorderMenu
{
    private const string RecordMenuPath = "Tools/Bullet Hell/Debug/Record Current Stage";
    private const int OutputWidth = 1280;
    private const int OutputHeight = 720;
    private const float FrameRate = 30f;

    private static RecorderController controller;
    private static RecorderControllerSettings controllerSettings;
    private static string activeOutputFile;

    public static bool IsRecording => controller != null && controller.IsRecording();

    [MenuItem(RecordMenuPath)]
    private static void ToggleRecordCurrentStage()
    {
        if (IsRecording)
        {
            StopRecording();
            return;
        }

        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[StageRecorder] Enter Play mode and start a stage first.");
            return;
        }

        string dir = StageSeekSupport.CurrentStageDir();
        if (string.IsNullOrEmpty(dir))
        {
            Debug.LogWarning("[StageRecorder] No stage is running to record.");
            return;
        }

        StartRecording(dir);
    }

    [MenuItem(RecordMenuPath, true)]
    private static bool ToggleRecordValidate()
    {
        Menu.SetChecked(RecordMenuPath, IsRecording);
        return EditorApplication.isPlaying;
    }

    /// <summary>Starts recording the Game view. <paramref name="stageDir"/> names the output file.</summary>
    public static void StartRecording(string stageDir)
    {
        if (IsRecording)
        {
            Debug.LogWarning("[StageRecorder] Already recording.");
            return;
        }

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string recordingsDir = Path.Combine(projectRoot, "Recordings");
        if (!Directory.Exists(recordingsDir)) Directory.CreateDirectory(recordingsDir);

        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileNameNoExt = $"{StageSeekSupport.Sanitize(stageDir)}_{stamp}";
        activeOutputFile = Path.Combine(recordingsDir, fileNameNoExt);

        controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
        controllerSettings.SetRecordModeToManual();
        controllerSettings.FrameRate = FrameRate;
        controllerSettings.CapFrameRate = false;

        MovieRecorderSettings movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        movie.name = "StageMovie";
        movie.Enabled = true;
        movie.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.MP4;
        movie.VideoBitRateMode = VideoBitrateMode.High;
        movie.ImageInputSettings = new GameViewInputSettings
        {
            OutputWidth = OutputWidth,
            OutputHeight = OutputHeight
        };
        movie.AudioInputSettings.PreserveAudio = true;
        movie.OutputFile = activeOutputFile; // extension added by the recorder

        controllerSettings.AddRecorderSettings(movie);

        controller = new RecorderController(controllerSettings);
        controller.PrepareRecording();
        if (!controller.StartRecording())
        {
            Debug.LogError("[StageRecorder] Failed to start recording.");
            Cleanup();
            return;
        }

        EditorApplication.update -= WatchForAutoStop;
        EditorApplication.update += WatchForAutoStop;
        Debug.Log($"[StageRecorder] Recording started -> {activeOutputFile}.mp4");
    }

    public static void StopRecording()
    {
        if (controller == null)
        {
            Debug.Log("[StageRecorder] Not recording.");
            return;
        }

        if (controller.IsRecording()) controller.StopRecording();
        string output = activeOutputFile;
        Cleanup();
        Debug.Log($"[StageRecorder] Recording stopped -> {output}.mp4");
        AssetDatabase.Refresh();
    }

    private static void WatchForAutoStop()
    {
        if (!IsRecording)
        {
            EditorApplication.update -= WatchForAutoStop;
            return;
        }

        bool playing = EditorApplication.isPlaying
            && GManager.Control != null
            && GManager.Control.state == GManager.GameState.Playing;
        if (!playing)
        {
            StopRecording();
        }
    }

    private static void Cleanup()
    {
        EditorApplication.update -= WatchForAutoStop;
        controller = null;
        if (controllerSettings != null)
        {
            UnityEngine.Object.DestroyImmediate(controllerSettings);
            controllerSettings = null;
        }
        activeOutputFile = null;
    }
}

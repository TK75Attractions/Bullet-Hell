using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEngine;

/// <summary>
/// ステージ選択のプレビュー動画(&lt;stage&gt;/&lt;stage&gt;.mp4, 924x754)を作り直すための
/// 手動レコーダー。<see cref="StageRecorderMenu"/> は 1280x720(高さ 720)固定で
/// プレビューの 754px 高さに足りないため、ここでは 1920x1080 で GameView を録る。
/// 状態依存の自動停止は持たず、明示的な <see cref="StopManual"/> か Play mode 終了
/// でのみ止まる。録画後に ffmpeg で中央 1324x1080 をクロップ→924x754 へ縮小し、
/// 必要な 4 秒窓を切り出してプレビューへ差し替える運用を想定。
/// </summary>
public static class PreviewRecorderMenu
{
    private const int OutputWidth = 1920;
    private const int OutputHeight = 1080;
    private const float FrameRate = 30f;

    private static RecorderController controller;
    private static RecorderControllerSettings controllerSettings;
    private static string activeOutputFile;

    public static bool IsRecording => controller != null && controller.IsRecording();

    /// <summary>録画を開始する。<paramref name="fileNameNoExt"/> は Recordings/ 配下の拡張子なしファイル名。</summary>
    public static string StartManual(string fileNameNoExt)
    {
        if (IsRecording)
        {
            Debug.LogWarning("[PreviewRecorder] Already recording.");
            return activeOutputFile;
        }

        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[PreviewRecorder] Enter Play mode first.");
            return null;
        }

        // 非フォーカスでも GameView を描画し続ける(無人録画で黒フレームにしない)。
        Application.runInBackground = true;

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string recordingsDir = Path.Combine(projectRoot, "Recordings");
        if (!Directory.Exists(recordingsDir)) Directory.CreateDirectory(recordingsDir);
        activeOutputFile = Path.Combine(recordingsDir, fileNameNoExt);

        controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
        controllerSettings.SetRecordModeToManual();
        controllerSettings.FrameRate = FrameRate;
        // captureFramerate 有効下で描画が BGM クロックを追い越さないよう固定する
        // (音ハメの検証をこの録画で行う場合に効く。プレビュー自体は無音でよい)。
        controllerSettings.CapFrameRate = true;

        MovieRecorderSettings movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        movie.name = "PreviewMovie";
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
            Debug.LogError("[PreviewRecorder] Failed to start recording.");
            Cleanup();
            return null;
        }

        EditorApplication.update -= WatchForPlayModeExit;
        EditorApplication.update += WatchForPlayModeExit;
        Debug.Log($"[PreviewRecorder] Recording started -> {activeOutputFile}.mp4 ({OutputWidth}x{OutputHeight})");
        return activeOutputFile;
    }

    public static string StopManual()
    {
        if (controller == null)
        {
            Debug.Log("[PreviewRecorder] Not recording.");
            return null;
        }

        if (controller.IsRecording()) controller.StopRecording();
        string output = activeOutputFile;
        Cleanup();
        Debug.Log($"[PreviewRecorder] Recording stopped -> {output}.mp4");
        AssetDatabase.Refresh();
        return output;
    }

    private static void WatchForPlayModeExit()
    {
        if (!EditorApplication.isPlaying)
        {
            StopManual();
        }
    }

    private static void Cleanup()
    {
        EditorApplication.update -= WatchForPlayModeExit;
        controller = null;
        if (controllerSettings != null)
        {
            Object.DestroyImmediate(controllerSettings);
            controllerSettings = null;
        }
        activeOutputFile = null;
    }
}

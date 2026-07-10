using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEngine;

/// <summary>
/// リザルト画面など、GameState が Playing を離れる演出フローを録画するための
/// 手動レコーダー。<see cref="StageRecorderMenu"/> は敵弾ゼロや Playing 離脱で
/// 自動停止するため、Playing→Result→Playing→ChoosingStage のような UI 遷移を
/// 通しで録れない。ここでは状態依存の自動停止を持たず、明示的な Stop() か
/// Play mode 終了時のみ止まる。設定(1280x720 / 30fps / High / 音声つき /
/// CapFrameRate)は StageRecorderMenu と揃える。
/// </summary>
public static class ResultFlowRecorderMenu
{
    private const int OutputWidth = 1280;
    private const int OutputHeight = 720;
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
            Debug.LogWarning("[ResultFlowRecorder] Already recording.");
            return activeOutputFile;
        }

        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[ResultFlowRecorder] Enter Play mode first.");
            return null;
        }

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string recordingsDir = Path.Combine(projectRoot, "Recordings");
        if (!Directory.Exists(recordingsDir)) Directory.CreateDirectory(recordingsDir);
        activeOutputFile = Path.Combine(recordingsDir, fileNameNoExt);

        controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
        controllerSettings.SetRecordModeToManual();
        controllerSettings.FrameRate = FrameRate;
        // StageRecorderMenu と同じ理由: CapFrameRate を切ると描画がBGMクロックを
        // 追い越し、音ハメが録画上でずれる。
        controllerSettings.CapFrameRate = true;

        MovieRecorderSettings movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        movie.name = "ResultFlowMovie";
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
            Debug.LogError("[ResultFlowRecorder] Failed to start recording.");
            Cleanup();
            return null;
        }

        EditorApplication.update -= WatchForPlayModeExit;
        EditorApplication.update += WatchForPlayModeExit;
        Debug.Log($"[ResultFlowRecorder] Recording started -> {activeOutputFile}.mp4");
        return activeOutputFile;
    }

    public static string StopManual()
    {
        if (controller == null)
        {
            Debug.Log("[ResultFlowRecorder] Not recording.");
            return null;
        }

        if (controller.IsRecording()) controller.StopRecording();
        string output = activeOutputFile;
        Cleanup();
        Debug.Log($"[ResultFlowRecorder] Recording stopped -> {output}.mp4");
        AssetDatabase.Refresh();
        return output;
    }

    // 状態には依存せず、Play mode を抜けたときだけ後始末する安全網。
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
            UnityEngine.Object.DestroyImmediate(controllerSettings);
            controllerSettings = null;
        }
        activeOutputFile = null;
    }

    // ---- リザルト画面フローのデモ録画 ----
    // 実経路(StageSelectManager.StartGameTransition / GManager.ShowResult /
    // HandleResultAction)だけを叩いて、選択→プレイ→クリア→リザルト→もう一度→
    // リザルト→ステージ選択へ を通しで録る。GManager.StartCoroutine で起動する。
    // Recorder の captureFramerate 下では WaitForSeconds が動画秒と一致する。

    public static bool DemoRunning { get; private set; }
    public static string DemoOutput { get; private set; }

    public static IEnumerator ResultFlowDemoRoutine(string fileNameNoExt)
    {
        DemoRunning = true;
        DemoOutput = null;
        try
        {
            GManager g = GManager.Control;
            if (g == null) { Debug.LogError("[ResultFlowRecorder] GManager.Control null"); yield break; }

            Time.timeScale = 1f;
            AudioListener.pause = false;

            DemoOutput = StartManual(fileNameNoExt);
            if (DemoOutput == null) yield break;
            yield return new WaitForSeconds(0.6f);

            int stoneIndex = FindStageIndex(g, "石工");
            if (stoneIndex < 0) stoneIndex = 0;

            // Phase A: ステージ選択を表示(実 title→select 遷移のペイロード)。
            if (g.TManager != null) g.TManager.Dismiss();
            g.state = GManager.GameState.ChoosingStage;
            g.SSManager.ResetTimer();
            g.SSManager.PlayEntrance();
            yield return new WaitForSeconds(3.0f);

            // Phase B: 実経路で 石工 を開始(whiteout→UI退避→MosaicReveal→GoGameAsync)。
            SetSsState(g.SSManager, "InGame");
            InvokePrivate(g.SSManager, "StartGameTransition", new object[] { stoneIndex });
            yield return WaitForState(g, GManager.GameState.Playing, 12f);
            yield return new WaitForSeconds(6.0f); // 実弾+BGM のプレイを見せる

            // Phase C: クリア→リザルト(実 ShowResult。whiteout→Prepare→MosaicReveal→PlayEntrance)。
            g.ShowResult(true);
            yield return WaitForState(g, GManager.GameState.Result, 8f);
            yield return new WaitForSeconds(4.0f); // 入場アニメ後、内容を見せる

            // Phase D: 「もう一度」(既定で index0 選択済み)→リプレイ。
            InvokePrivate(g, "HandleResultAction", new object[] { ResultScreen.Action.Retry });
            yield return WaitForState(g, GManager.GameState.Playing, 12f);
            yield return new WaitForSeconds(4.0f);

            // Phase E: 再度クリア→リザルト。
            g.ShowResult(true);
            yield return WaitForState(g, GManager.GameState.Result, 8f);
            yield return new WaitForSeconds(2.5f);

            // Phase F: ハイライトを「ステージ選択へ」(index1)へ移してから確定。
            InvokePrivate(g.RManager, "Select", new object[] { 1 });
            yield return new WaitForSeconds(1.2f);
            InvokePrivate(g, "HandleResultAction", new object[] { ResultScreen.Action.StageSelect });
            yield return WaitForState(g, GManager.GameState.ChoosingStage, 8f);
            yield return new WaitForSeconds(3.0f);
        }
        finally
        {
            DemoOutput = StopManual() ?? DemoOutput;
            DemoRunning = false;
        }
    }

    private static IEnumerator WaitForState(GManager g, GManager.GameState target, float timeoutSec)
    {
        float t = timeoutSec;
        while (g.state != target && t > 0f)
        {
            t -= Time.unscaledDeltaTime;
            yield return null;
        }
        // 遷移の演出(MosaicReveal 等)が落ち着くよう軽く待つ。
        yield return new WaitForSeconds(0.3f);
    }

    private static int FindStageIndex(GManager g, string stageName)
    {
        if (g.SDB == null) return -1;
        int count = g.SDB.GetStageCount();
        for (int i = 0; i < count; i++)
        {
            StageData s = g.SDB.GetStage(i);
            if (s != null && s.stageName == stageName) return i;
        }
        return -1;
    }

    private static void SetSsState(StageSelectManager ss, string enumName)
    {
        FieldInfo f = typeof(StageSelectManager).GetField("state", BindingFlags.NonPublic | BindingFlags.Instance);
        if (f != null) f.SetValue(ss, Enum.Parse(f.FieldType, enumName));
    }

    private static void InvokePrivate(object target, string method, object[] args)
    {
        MethodInfo m = target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance);
        if (m == null) { Debug.LogError("[ResultFlowRecorder] method not found: " + method); return; }
        m.Invoke(target, args);
    }
}

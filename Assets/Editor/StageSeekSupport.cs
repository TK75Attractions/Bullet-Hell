using System;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// Shared editor helpers for stage verification tooling: locating the running
/// <see cref="StageReader"/> and taking timestamped screenshots into
/// Assets/Screenshots. Reused by the recorder / capture menus.
///
/// The marker/seek loading (bar:beat markers, <c>StageReader.SeekTo</c>) was
/// removed with the marron chart-authoring pipeline during the raymee runtime
/// integration; only the schema-agnostic helpers remain.
/// </summary>
public static class StageSeekSupport
{
    public const string ScreenshotsFolder = "Screenshots";

    /// <summary>The StageReader of the current Play session, or null.</summary>
    public static StageReader CurrentReader()
    {
        return GManager.Control != null ? GManager.Control.SReader : null;
    }

    /// <summary>Directory-name key of the running stage (used for file names).</summary>
    public static string CurrentStageDir()
    {
        StageReader reader = CurrentReader();
        StageData stage = reader != null ? reader.CurrentStage : null;
        if (stage == null) return null;
        return string.IsNullOrWhiteSpace(stage.stageDirectoryName) ? stage.stageName : stage.stageDirectoryName;
    }

    /// <summary>Absolute path of the Assets/Screenshots directory (created if missing).</summary>
    public static string EnsureScreenshotsDirectory()
    {
        string dir = Path.Combine(Application.dataPath, ScreenshotsFolder);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// Captures a screenshot to Assets/Screenshots. When <paramref name="fileName"/>
    /// is null a timestamped name is generated. Returns the absolute path (the file
    /// is written at end-of-frame by Unity).
    /// </summary>
    public static string CaptureScreenshot(string fileName = null)
    {
        string dir = EnsureScreenshotsDirectory();
        if (string.IsNullOrEmpty(fileName))
        {
            string stage = CurrentStageDir() ?? "stage";
            float t = CurrentReader() != null ? CurrentReader().CurrentTime : 0f;
            fileName = $"seek_{Sanitize(stage)}_{t:0.00}_{DateTime.Now:HHmmss}.png";
        }
        string path = Path.Combine(dir, fileName);
        ScreenCapture.CaptureScreenshot(path);
        Debug.Log($"[StageSeek] Screenshot queued: {path}");
        return path;
    }

    public static string Sanitize(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "stage";
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            raw = raw.Replace(c, '_');
        }
        return raw;
    }

    public static string FormatSeconds(float seconds)
    {
        int m = Mathf.FloorToInt(seconds / 60f);
        float s = seconds - m * 60f;
        return m > 0
            ? $"{m}:{s:00.00}"
            : s.ToString("0.00", CultureInfo.InvariantCulture) + "s";
    }
}

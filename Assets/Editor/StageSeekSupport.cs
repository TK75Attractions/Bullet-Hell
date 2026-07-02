using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Shared editor helpers for the P4 seek/verification tooling: locating the
/// running StageReader, loading a stage's StageChart markers (bar:beat -> seconds),
/// and taking timestamped screenshots into Assets/Screenshots. Reused by the stage
/// launcher window, the recorder menu and the capture-at-times hook.
/// </summary>
public static class StageSeekSupport
{
    public const string ScreenshotsFolder = "Screenshots";

    public struct StageMarker
    {
        public string Name;
        public string Label;
        public float Seconds;
    }

    /// <summary>The StageReader of the current Play session, or null.</summary>
    public static StageReader CurrentReader()
    {
        return GManager.Control != null ? GManager.Control.SReader : null;
    }

    /// <summary>Directory-name key of the running stage (used for chart lookup and file names).</summary>
    public static string CurrentStageDir()
    {
        StageReader reader = CurrentReader();
        StageData stage = reader != null ? reader.CurrentStage : null;
        if (stage == null) return null;
        return string.IsNullOrWhiteSpace(stage.stageDirectoryName) ? stage.stageName : stage.stageDirectoryName;
    }

    /// <summary>
    /// Loads and evaluates the named markers from the running stage's
    /// <c>&lt;dir&gt;/&lt;dir&gt;.chart.json</c>, returning them sorted by time.
    /// Returns an empty list (and sets <paramref name="error"/>) when no chart is found.
    /// </summary>
    public static List<StageMarker> LoadMarkers(StageData stage, out string error)
    {
        error = null;
        var markersOut = new List<StageMarker>();
        if (stage == null)
        {
            error = "No stage is running.";
            return markersOut;
        }

        string dir = string.IsNullOrWhiteSpace(stage.stageDirectoryName) ? stage.stageName : stage.stageDirectoryName;
        string path = Path.Combine(Application.dataPath, "StageData", dir, dir + ".chart.json");
        if (!File.Exists(path))
        {
            error = $"No chart for '{dir}' at {path}.";
            return markersOut;
        }

        JObject root;
        try
        {
            root = JObject.Parse(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            error = $"Chart parse failed: {ex.Message}";
            return markersOut;
        }

        JObject meta = root["meta"] as JObject;
        double bpm = meta?["bpm"]?.Value<double>() ?? 120.0;
        int measure = meta?["measure"]?.Value<int>() ?? 4;
        double beatOffset = meta?["beatOffsetSec"]?.Value<double>() ?? 0.0;

        JObject markers = root["markers"] as JObject;
        if (markers == null) return markersOut;

        JObject labels = root["_markerLabels"] as JObject;

        var markerExprs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, JToken> kv in markers)
        {
            if (kv.Key.StartsWith("_")) continue; // skip _comment etc.
            markerExprs[kv.Key] = kv.Value.ToString();
        }

        ChartTimeExpr.Context ctx;
        try
        {
            ctx = new ChartTimeExpr.Context(bpm, measure, beatOffset, markerExprs);
        }
        catch (Exception ex)
        {
            error = $"Chart context invalid: {ex.Message}";
            return markersOut;
        }

        foreach (KeyValuePair<string, string> kv in markerExprs)
        {
            try
            {
                double seconds = ChartTimeExpr.Evaluate(kv.Value, ctx);
                markersOut.Add(new StageMarker
                {
                    Name = kv.Key,
                    Label = labels?[kv.Key]?.ToString(),
                    Seconds = (float)seconds
                });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[StageSeek] marker '{kv.Key}' ({kv.Value}): {ex.Message}");
            }
        }

        markersOut.Sort((a, b) => a.Seconds.CompareTo(b.Seconds));
        return markersOut;
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

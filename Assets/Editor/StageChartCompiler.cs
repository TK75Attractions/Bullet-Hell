using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only compiler that turns a beat-domain StageChart
/// (<c>&lt;stage&gt;/&lt;stage&gt;.chart.json</c>) into the existing StageData JSON
/// (<c>&lt;stage&gt;/&lt;stage&gt;.json</c>). The runtime is untouched; the chart is
/// the source of truth and the stage.json becomes a build artifact.
///
/// Time authoring is done through <see cref="ChartTimeExpr"/> (bar:beat / marker /
/// relative-beat expressions). Times are evaluated in double precision and then
/// quantized to 6 decimal places at materialization so the emitted float value is
/// bit-identical to the hand-authored stage.json (which stored 6-decimal seconds).
/// That single output-time quantization is the only rounding applied; marker
/// arithmetic stays in double.
/// </summary>
public static class StageChartCompiler
{
    public const string CompileMenuPath = "Tools/Bullet Hell/Compile Stage Charts";
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public sealed class CompileResult
    {
        public string StageDir;
        public string StageName;
        public StageData StageDataForGolden;   // stageName + bulletSpawners only (enough for golden parity)
        public string StageJson;               // full regenerated stage.json text
        public int EventCount;
        public int SecondsFallbackCount;       // events whose 'at' was a bare seconds literal
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public bool IsGreen => Errors.Count == 0;
    }

    [MenuItem(CompileMenuPath)]
    public static void CompileAllMenu()
    {
        try
        {
            string root = Path.Combine(Application.dataPath, "StageData");
            if (!Directory.Exists(root))
            {
                Debug.LogWarning($"[Chart] StageData directory not found: {root}");
                return;
            }

            string[] chartFiles = Directory.GetFiles(root, "*.chart.json", SearchOption.AllDirectories);
            Array.Sort(chartFiles, StringComparer.Ordinal);
            if (chartFiles.Length == 0)
            {
                Debug.Log("[Chart] No *.chart.json files found under Assets/StageData.");
                return;
            }

            int ok = 0;
            foreach (string chartPath in chartFiles)
            {
                string stageDir = Path.GetFileName(Path.GetDirectoryName(chartPath));
                CompileResult result;
                try
                {
                    result = Compile(File.ReadAllText(chartPath), stageDir);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Chart] {stageDir}: compile threw: {ex.Message}");
                    continue;
                }

                ValidateClipLinks(result, stageDir);

                if (!result.IsGreen)
                {
                    Debug.LogError($"[Chart] {stageDir}: {result.Errors.Count} error(s):\n  " + string.Join("\n  ", result.Errors));
                    continue;
                }

                string outPath = Path.Combine(Path.GetDirectoryName(chartPath), stageDir + ".json");
                File.WriteAllText(outPath, result.StageJson, new UTF8Encoding(true));
                ok++;
                string warn = result.Warnings.Count > 0 ? $" ({result.Warnings.Count} warning(s))" : "";
                Debug.Log($"[Chart] {stageDir}: compiled {result.EventCount} event(s), {result.SecondsFallbackCount} seconds-fallback -> {outPath}{warn}");
            }

            AssetDatabase.Refresh();
            Debug.Log($"[Chart] Compiled {ok}/{chartFiles.Length} chart(s).");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    /// <summary>
    /// Compiles a chart JSON string into a <see cref="CompileResult"/>. Pure with
    /// respect to Unity assets (no probe, no buffer load) so tests can call it
    /// directly. Clip-link validation is a separate, probe-backed step.
    /// </summary>
    public static CompileResult Compile(string chartJsonText, string stageDir)
    {
        var result = new CompileResult { StageDir = stageDir };

        JObject root;
        try
        {
            root = JObject.Parse(chartJsonText);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Chart JSON parse failed: {ex.Message}");
            return result;
        }

        JObject meta = root["meta"] as JObject ?? new JObject();
        string stageName = (string)meta["stageName"] ?? stageDir;
        double bpm = ReadDouble(meta["bpm"], 120.0);
        int measure = (int)ReadDouble(meta["measure"], 4.0);
        double beatOffsetSec = ReadDouble(meta["beatOffsetSec"], 0.0);
        int barCount = (int)ReadDouble(meta["barCount"], 0.0);
        float delayTime = (float)ReadDouble(meta["delayTime"], 0.0);
        string stageDescription = (string)meta["stageDescription"] ?? "";

        result.StageName = stageName;

        // Markers dictionary (name -> expression string).
        var markers = new Dictionary<string, string>(StringComparer.Ordinal);
        if (root["markers"] is JObject markersObj)
        {
            foreach (KeyValuePair<string, JToken> kv in markersObj)
            {
                if (kv.Key.StartsWith("_"))
                {
                    continue; // _comment
                }
                markers[kv.Key] = kv.Value.Type == JTokenType.String
                    ? (string)kv.Value
                    : kv.Value.ToString(Formatting.None);
            }
        }

        ChartTimeExpr.Context ctx;
        try
        {
            ctx = new ChartTimeExpr.Context(bpm, measure, beatOffsetSec, markers);
        }
        catch (ChartTimeExpr.ChartTimeException ex)
        {
            result.Errors.Add(ex.Message);
            return result;
        }

        // ---- Events -> bulletSpawners (+ patternEvents) ----
        var spawners = new List<BulletSpawner>();
        var spawnerJsonArray = new JArray();
        var patternJsonArray = new JArray();
        if (root["events"] is JArray events)
        {
            int i = 0;
            foreach (JToken evTok in events)
            {
                i++;
                if (!(evTok is JObject ev))
                {
                    continue;
                }

                JToken atTok = ev["at"];
                if (atTok == null)
                {
                    result.Errors.Add($"events[{i}] missing 'at'.");
                    continue;
                }
                string atExpr = atTok.Type == JTokenType.String ? (string)atTok : atTok.ToString(Formatting.None);
                bool isBareSeconds = IsBareNumber(atExpr);

                double seconds;
                try
                {
                    seconds = ChartTimeExpr.Evaluate(atExpr, ctx);
                }
                catch (ChartTimeExpr.ChartTimeException ex)
                {
                    result.Errors.Add($"events[{i}] at='{atExpr}': {ex.Message}");
                    continue;
                }

                if (isBareSeconds)
                {
                    result.SecondsFallbackCount++;
                }

                double time6 = Round6(seconds);

                // New P3 pattern event. Only 'at' is baked to seconds here; the
                // pattern's own beat-domain params (warnBeats/fallBeats/...) are left
                // verbatim and resolved at runtime from the stage BPM.
                string patternType = (string)ev["pattern"];
                if (!string.IsNullOrEmpty(patternType))
                {
                    patternJsonArray.Add(BuildPatternJson(patternType, time6, ev["params"] as JObject, ev, result, i));
                    result.EventCount++;
                    continue;
                }

                string clipName;
                string kind = (string)ev["kind"];
                if (string.Equals(kind, "clear", StringComparison.OrdinalIgnoreCase))
                {
                    clipName = StageScheduleExpander.ClearClipName;
                }
                else
                {
                    clipName = (string)ev["clip"];
                    if (string.IsNullOrEmpty(clipName))
                    {
                        result.Errors.Add($"events[{i}] has neither 'clip' nor kind:'clear'.");
                        continue;
                    }
                }

                int count = (int)ReadDouble(ev["count"], 1.0);
                float interval = (float)ReadDouble(ev["interval"], 0.0);
                float angleDeg = (float)ReadDouble(ev["angleDeg"], 0.0);
                float angleInterval = (float)ReadDouble(ev["angleIntervalDeg"], 0.0);
                float2 pos = ReadVec2(ev["pos"]);
                float2 originVlc = ReadVec2(ev["originVlc"]);
                float4 color = ReadColor(ev["color"]);

                spawners.Add(new BulletSpawner
                {
                    index = 0,
                    count = count,
                    interval = interval,
                    time = (float)time6,
                    pos = pos,
                    originVlc = originVlc,
                    angle = angleDeg,
                    angleInterval = angleInterval,
                    color = color,
                    clipName = clipName
                });

                spawnerJsonArray.Add(BuildSpawnerJson(clipName, count, interval, time6, pos, originVlc, angleDeg, angleInterval, color, ev, result, i));
                result.EventCount++;
            }
        }

        // ---- Build the in-memory StageData used for golden parity ----
        result.StageDataForGolden = new StageData
        {
            stageDirectoryName = stageDir,
            source = StageData.StageSource.Official,
            stageName = stageName,
            bulletSpawners = spawners
        };

        // ---- Enemies -> enemySpawners (disk output only; not in golden) ----
        JArray enemyJsonArray;
        try
        {
            enemyJsonArray = BuildEnemyJson(root["enemies"] as JArray, ctx, result);
        }
        catch (ChartTimeExpr.ChartTimeException ex)
        {
            result.Errors.Add($"enemies: {ex.Message}");
            enemyJsonArray = new JArray();
        }

        // ---- Full stage.json ----
        result.StageJson = BuildStageJson(
            stageName, bpm, measure, barCount, delayTime, stageDescription,
            root["enemyVisuals"] as JArray, enemyJsonArray, spawnerJsonArray, patternJsonArray, stageDir);

        return result;
    }

    /// <summary>Builds one patternEvents entry. Params are cloned verbatim (beat
    /// fields stay beat-domain, resolved at runtime); only <c>time</c> is baked.
    /// P5 difficulty modifiers are flattened from the friendly nested chart syntax
    /// and appended only when present.</summary>
    private static JObject BuildPatternJson(string patternType, double time6, JObject paramsObj, JObject ev, CompileResult result, int index)
    {
        var o = new JObject
        {
            ["time"] = time6,
            ["patternType"] = patternType,
            ["args"] = paramsObj != null ? (JObject)paramsObj.DeepClone() : new JObject()
        };
        ApplyDifficultyMods(o, ev, includeScale: true, result, index);
        return o;
    }

    /// <summary>
    /// Reads the friendly nested difficulty modifiers off a chart event and writes
    /// the flat, JsonUtility-friendly fields onto <paramref name="target"/>. Only
    /// non-default values are emitted, keeping the generated stage.json diff minimal.
    /// Chart syntax:
    /// <code>
    ///   "minDifficulty": "normal",
    ///   "thin": { "easy": 2, "normal": 3 },
    ///   "diffScale": { "easy": { "speed": 0.8, "count": 0.6 }, "normal": { ... } }
    /// </code>
    /// <paramref name="includeScale"/> is false for clip spawners (diffScale is a
    /// pattern-only concept: clips have no speed/count arguments to scale).
    /// </summary>
    private static void ApplyDifficultyMods(JObject target, JObject ev, bool includeScale, CompileResult result, int index)
    {
        string min = (string)ev["minDifficulty"];
        if (!string.IsNullOrWhiteSpace(min))
        {
            string norm = min.Trim().ToLowerInvariant();
            if (norm != "easy" && norm != "normal" && norm != "lunatic")
            {
                result.Warnings.Add($"events[{index}] minDifficulty='{min}' is not easy/normal/lunatic; it will be treated as 'easy' (always shown).");
            }
            target["minDifficulty"] = norm;
        }

        if (ev["thin"] is JObject thin)
        {
            int te = (int)ReadDouble(thin["easy"], 0.0);
            int tn = (int)ReadDouble(thin["normal"], 0.0);
            if (te != 0) target["thinEasy"] = te;
            if (tn != 0) target["thinNormal"] = tn;
        }

        if (includeScale && ev["diffScale"] is JObject scale)
        {
            JObject easy = scale["easy"] as JObject;
            JObject normal = scale["normal"] as JObject;
            AddScale(target, "scaleEasySpeed", easy?["speed"]);
            AddScale(target, "scaleEasyCount", easy?["count"]);
            AddScale(target, "scaleNormalSpeed", normal?["speed"]);
            AddScale(target, "scaleNormalCount", normal?["count"]);
        }
    }

    private static void AddScale(JObject target, string key, JToken tok)
    {
        double v = ReadDouble(tok, 0.0);
        if (v != 0.0) target[key] = Round6(v);
    }

    /// <summary>
    /// Probe-backed clip-link validation: every event clip (except Clear) must
    /// resolve to a loaded buffer for the stage. Called by the menu path.
    /// </summary>
    public static void ValidateClipLinks(CompileResult result, string stageDir)
    {
        if (result?.StageDataForGolden?.bulletSpawners == null)
        {
            return;
        }

        try
        {
            using (new EditorStageProbe(StageGoldenDumper.BtdbAssetPath, StageGoldenDumper.EdbAssetPath))
            {
                BulletBufferManager buffers = new BulletBufferManager();
                buffers.ReloadForStageBulletBuffersAsync(stageDir).GetAwaiter().GetResult();
                var missing = new HashSet<string>(StringComparer.Ordinal);
                foreach (BulletSpawner s in result.StageDataForGolden.bulletSpawners)
                {
                    if (s.clipName == StageScheduleExpander.ClearClipName)
                    {
                        continue;
                    }
                    if (!buffers.TryGetBulletClipIndex(s.clipName, out _) && missing.Add(s.clipName))
                    {
                        result.Errors.Add($"Unresolved clip '{s.clipName}' (no loaded buffer for stage '{stageDir}').");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"Clip-link validation skipped: {ex.Message}");
        }
    }

    // ---- Enemy compilation ----

    private static JArray BuildEnemyJson(JArray enemies, ChartTimeExpr.Context ctx, CompileResult result)
    {
        var arr = new JArray();
        if (enemies == null)
        {
            return arr;
        }

        foreach (JToken enTok in enemies)
        {
            if (!(enTok is JObject en))
            {
                continue;
            }

            var outEn = new JObject();
            outEn["enemyName"] = (string)en["enemyName"] ?? "";
            outEn["visualId"] = (string)en["visualId"] ?? "";

            // animation with expression-aware event times.
            var animOut = new JObject();
            JObject anim = en["animation"] as JObject;
            animOut["initialClip"] = (string)(anim?["initialClip"]) ?? "idle";

            double appearAtSec = 0.0;
            if (en["appearAt"] != null)
            {
                appearAtSec = ChartTimeExpr.Evaluate(TokenToExpr(en["appearAt"]), ctx);
            }

            var evOut = new JArray();
            if (anim?["events"] is JArray animEvents)
            {
                foreach (JToken aeTok in animEvents)
                {
                    if (!(aeTok is JObject ae))
                    {
                        continue;
                    }
                    double relTime;
                    if (ae["atAbs"] != null)
                    {
                        double abs = ChartTimeExpr.Evaluate(TokenToExpr(ae["atAbs"]), ctx);
                        relTime = abs - appearAtSec;
                    }
                    else
                    {
                        relTime = ReadDouble(ae["time"], 0.0);
                    }
                    var o = new JObject
                    {
                        ["time"] = Round6(relTime),
                        ["clip"] = (string)ae["clip"] ?? "",
                        ["next"] = (string)ae["next"] ?? ""
                    };
                    evOut.Add(o);
                }
            }
            animOut["events"] = evOut;
            outEn["animation"] = animOut;

            outEn["count"] = (int)ReadDouble(en["count"], 1.0);
            outEn["enemyInterval"] = Round6(ReadDouble(en["enemyInterval"], 0.0));
            outEn["enemyAppearTime"] = Round6(appearAtSec);
            outEn["bulletEmitTime"] = Round6(ReadDouble(en["bulletEmitTime"], 0.0));
            outEn["bulletCount"] = (int)ReadDouble(en["bulletCount"], 0.0);
            outEn["life"] = Round6(ReadDouble(en["life"], 0.0));
            outEn["fadeInSec"] = Round6(ReadDouble(en["fadeInSec"], 0.0));
            outEn["fadeOutSec"] = Round6(ReadDouble(en["fadeOutSec"], 0.0));
            outEn["sortingOrder"] = (int)ReadDouble(en["sortingOrder"], 0.0);

            // Passthrough of the heavy static structures (deep clone, verbatim).
            outEn["orbit"] = en["orbit"] != null ? en["orbit"].DeepClone() : new JObject();
            outEn["bulletClip"] = en["bulletClip"] != null ? en["bulletClip"].DeepClone() : new JObject();
            outEn["bulletChangeClips"] = en["bulletChangeClips"] != null ? en["bulletChangeClips"].DeepClone() : new JArray();

            arr.Add(outEn);
        }
        return arr;
    }

    // ---- JSON builders ----

    private static JObject BuildSpawnerJson(string clipName, int count, float interval, double time6,
        float2 pos, float2 originVlc, float angle, float angleInterval, float4 color, JObject ev, CompileResult result, int index)
    {
        var o = new JObject
        {
            ["index"] = 0,
            ["count"] = count,
            ["interval"] = Round6(interval),
            ["time"] = time6,
            ["pos"] = new JObject { ["x"] = Round6(pos.x), ["y"] = Round6(pos.y) },
            ["originVlc"] = new JObject { ["x"] = Round6(originVlc.x), ["y"] = Round6(originVlc.y) },
            ["angle"] = Round6(angle),
            ["angleInterval"] = Round6(angleInterval),
            ["color"] = new JObject { ["x"] = Round6(color.x), ["y"] = Round6(color.y), ["z"] = Round6(color.z), ["w"] = Round6(color.w) },
            ["clipName"] = clipName
        };
        // Clip spawners get minDifficulty + thin (no diffScale — that scales pattern args).
        ApplyDifficultyMods(o, ev, includeScale: false, result, index);
        return o;
    }

    private static string BuildStageJson(string stageName, double bpm, int measure, int barCount,
        float delayTime, string stageDescription, JArray enemyVisuals, JArray enemySpawners,
        JArray bulletSpawners, JArray patternEvents, string stageDir)
    {
        var music = new JObject
        {
            ["barCount"] = barCount,
            ["BPM"] = bpm,
            ["beatTimings"] = BeatTimings(measure),
            ["measure"] = measure,
            ["barStartOffsetBeats"] = 0
        };

        var root = new JObject
        {
            ["_generatedFrom"] = stageDir + ".chart.json",
            ["stageName"] = stageName,
            ["MusicEvents"] = new JArray { music },
            ["delayTime"] = Round6(delayTime),
            ["stageDescription"] = stageDescription,
            ["enemyVisuals"] = enemyVisuals != null ? (JArray)enemyVisuals.DeepClone() : new JArray(),
            ["enemySpawners"] = enemySpawners ?? new JArray(),
            ["bulletSpawners"] = bulletSpawners ?? new JArray(),
            ["patternEvents"] = patternEvents ?? new JArray()
        };

        return root.ToString(Formatting.Indented);
    }

    private static JArray BeatTimings(int measure)
    {
        var arr = new JArray();
        for (int i = 0; i < measure; i++)
        {
            arr.Add(i);
        }
        return arr;
    }

    // ---- Helpers ----

    /// <summary>Round to 6 decimals as a double, normalizing -0. This is the sole
    /// materialization-time quantization (see class remarks).</summary>
    private static double Round6(double v)
    {
        double d = Math.Round(v, 6, MidpointRounding.ToEven);
        return d == 0.0 ? 0.0 : d;
    }

    private static double ReadDouble(JToken tok, double fallback)
    {
        if (tok == null || tok.Type == JTokenType.Null)
        {
            return fallback;
        }
        if (tok.Type == JTokenType.Integer || tok.Type == JTokenType.Float)
        {
            return (double)tok;
        }
        if (tok.Type == JTokenType.String && double.TryParse((string)tok, NumberStyles.Float, Inv, out double v))
        {
            return v;
        }
        return fallback;
    }

    private static float2 ReadVec2(JToken tok)
    {
        if (tok is JObject o)
        {
            return new float2((float)ReadDouble(o["x"], 0.0), (float)ReadDouble(o["y"], 0.0));
        }
        if (tok is JArray a && a.Count >= 2)
        {
            return new float2((float)ReadDouble(a[0], 0.0), (float)ReadDouble(a[1], 0.0));
        }
        return float2.zero;
    }

    private static float4 ReadColor(JToken tok)
    {
        if (tok is JObject o)
        {
            return new float4((float)ReadDouble(o["x"], 1.0), (float)ReadDouble(o["y"], 1.0),
                (float)ReadDouble(o["z"], 1.0), (float)ReadDouble(o["w"], 1.0));
        }
        if (tok is JArray a && a.Count >= 4)
        {
            return new float4((float)ReadDouble(a[0], 1.0), (float)ReadDouble(a[1], 1.0),
                (float)ReadDouble(a[2], 1.0), (float)ReadDouble(a[3], 1.0));
        }
        return new float4(1f, 1f, 1f, 1f);
    }

    private static string TokenToExpr(JToken tok)
    {
        return tok.Type == JTokenType.String ? (string)tok : tok.ToString(Formatting.None);
    }

    private static bool IsBareNumber(string expr)
    {
        return double.TryParse(expr.Trim(), NumberStyles.Float, Inv, out _);
    }
}

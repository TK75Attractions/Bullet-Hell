using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only golden-master generator. Loads each official stage exactly the
/// way the runtime does (real <see cref="StageDataManager"/> +
/// <see cref="BulletBufferManager"/>), expands its schedule through the shared
/// <see cref="StageScheduleExpander"/>, and writes a deterministic JSON digest
/// to <c>&lt;project&gt;/Tests/Golden/&lt;stageDir&gt;.golden.json</c>.
///
/// The digest is the safety net for the refactor: it captures the expanded spawn
/// event stream (time / clip / resolved index / pos / angle / color) plus a SHA1
/// per referenced bullet buffer, so any accidental behavior change surfaces as a
/// diff.
/// </summary>
public static class StageGoldenDumper
{
    public const string DumpMenuPath = "Tools/Bullet Hell/Golden/Dump All Stages";

    public static readonly string[] OfficialStageDirs =
    {
        "25", "captain", "debug", "debug(nature)", "stone", "mirror"
    };

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public const string BtdbAssetPath = "Assets/Scripts/Bullets/BulletTypes/BulletTypeDataBase.asset";
    public const string EdbAssetPath = "Assets/Scripts/Enemies/Enemies/EnemyDataBase.asset";

    [MenuItem(DumpMenuPath)]
    public static void DumpAllStagesMenu()
    {
        int written = 0;
        try
        {
            Dictionary<string, string> goldens = BuildAllGoldens();
            Directory.CreateDirectory(GoldenDirectory());
            foreach (KeyValuePair<string, string> kv in goldens)
            {
                File.WriteAllText(GoldenPath(kv.Key), kv.Value, new UTF8Encoding(false));
                written++;
            }

            int knownUnresolved = WriteKnownUnresolvedBaseline();
            Debug.Log($"[Golden] Wrote {written} golden files and {knownUnresolved} known-unresolved link(s) to {GoldenDirectory()}");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    public static string GoldenDirectory()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tests", "Golden"));
    }

    public static string GoldenPath(string stageDir)
    {
        return Path.Combine(GoldenDirectory(), stageDir + ".golden.json");
    }

    /// <summary>
    /// Regenerates the committed baseline of clip links that currently do not
    /// resolve (pre-existing data debt, e.g. mojibake buffer names in stage 25,
    /// and the debug scratch stage referencing a buffer outside its set). The
    /// linter/tests treat entries here as tracked warnings rather than errors.
    /// Returns the number of tracked entries.
    /// </summary>
    public static int WriteKnownUnresolvedBaseline()
    {
        List<string> keys = new List<string>();
        using (EditorStageProbe probe = new EditorStageProbe(BtdbAssetPath, EdbAssetPath))
        {
            Dictionary<string, StageData> stages = LoadOfficialStages();
            foreach (KeyValuePair<string, string> pair in StageValidation.CollectUnresolvedLinks(stages))
            {
                keys.Add(StageValidation.MakeKey(pair.Key, pair.Value));
            }
        }
        keys.Sort(StringComparer.Ordinal);

        StringBuilder sb = new StringBuilder();
        sb.Append("# Known-unresolved stage clip links (pre-existing data debt, tracked for P1).\n");
        sb.Append("# Format: <stageDir>\\t<clipName>. Regenerate via '").Append(DumpMenuPath).Append("'.\n");
        foreach (string key in keys)
        {
            sb.Append(key).Append('\n');
        }

        Directory.CreateDirectory(GoldenDirectory());
        File.WriteAllText(StageValidation.KnownUnresolvedPath(), sb.ToString(), new UTF8Encoding(false));
        return keys.Count;
    }

    /// <summary>
    /// Builds golden JSON for every official stage under a single editor probe /
    /// stage-load pass, so callers (menu + tests) do not repeat the heavy load.
    /// </summary>
    public static Dictionary<string, string> BuildAllGoldens()
    {
        Dictionary<string, string> result = new Dictionary<string, string>();
        using (EditorStageProbe probe = new EditorStageProbe(BtdbAssetPath, EdbAssetPath))
        {
            Dictionary<string, StageData> stages = LoadOfficialStages();
            foreach (string dir in OfficialStageDirs)
            {
                if (!stages.TryGetValue(dir, out StageData stage))
                {
                    throw new Exception($"[Golden] Official stage '{dir}' was not found on disk.");
                }
                result[dir] = BuildGolden(stage, dir);
            }
        }
        return result;
    }

    /// <summary>
    /// Loads the official stages via the real <see cref="StageDataManager"/> and
    /// keys them by directory name. Requires an active <see cref="EditorStageProbe"/>.
    /// </summary>
    public static Dictionary<string, StageData> LoadOfficialStages()
    {
        StageDataManager manager = new StageDataManager();
        List<StageData> all = manager.GetAllStageData();
        Dictionary<string, StageData> byDir = new Dictionary<string, StageData>();
        HashSet<string> wanted = new HashSet<string>(OfficialStageDirs, StringComparer.Ordinal);
        foreach (StageData stage in all)
        {
            if (stage.source == StageData.StageSource.Official &&
                stage.stageDirectoryName != null &&
                wanted.Contains(stage.stageDirectoryName) &&
                !byDir.ContainsKey(stage.stageDirectoryName))
            {
                byDir[stage.stageDirectoryName] = stage;
            }
        }
        return byDir;
    }

    public static string BuildGolden(StageData stage, string stageDir)
    {
        // Load buffers exactly as the runtime does for this stage.
        BulletBufferManager buffers = new BulletBufferManager();
        buffers.ReloadForStageBulletBuffersAsync(stageDir).GetAwaiter().GetResult();

        // Resolve clip indices (same rules as StageReader.Init) on a working copy.
        List<BulletSpawner> spawners = new List<BulletSpawner>(stage.bulletSpawners ?? new List<BulletSpawner>());
        Dictionary<int, string> indexToClip = new Dictionary<int, string>();
        for (int i = 0; i < spawners.Count; i++)
        {
            BulletSpawner spawner = spawners[i];
            if (buffers.TryGetBulletClipIndex(spawner.clipName, out int clipIndex))
            {
                spawner.index = clipIndex;
            }
            else if (spawner.clipName == StageScheduleExpander.ClearClipName)
            {
                spawner.index = StageScheduleExpander.ClearEventIndex;
            }
            else
            {
                spawner.index = StageScheduleExpander.UnresolvedIndex;
            }
            spawners[i] = spawner;
            if (!indexToClip.ContainsKey(spawner.index))
            {
                indexToClip[spawner.index] = spawner.index == StageScheduleExpander.ClearEventIndex ? StageScheduleExpander.ClearClipName : spawner.clipName;
            }
        }

        List<StageScheduleExpander.ScheduledSpawn> events = StageScheduleExpander.Expand(spawners);

        // Distinct referenced (real) buffers, name-sorted, with content digests.
        SortedSet<string> referenced = new SortedSet<string>(StringComparer.Ordinal);
        foreach (StageScheduleExpander.ScheduledSpawn e in events)
        {
            if (e.index >= 0 && indexToClip.TryGetValue(e.index, out string clip))
            {
                referenced.Add(clip);
            }
        }

        StringBuilder sb = new StringBuilder(1 << 16);
        sb.Append("{\n");
        sb.Append("  \"stageDir\": ").Append(Quote(stageDir)).Append(",\n");
        sb.Append("  \"stageName\": ").Append(Quote(stage.stageName)).Append(",\n");
        sb.Append("  \"eventCount\": ").Append(events.Count.ToString(Inv)).Append(",\n");

        sb.Append("  \"events\": [");
        for (int i = 0; i < events.Count; i++)
        {
            StageScheduleExpander.ScheduledSpawn e = events[i];
            indexToClip.TryGetValue(e.index, out string clip);
            sb.Append(i == 0 ? "\n" : ",\n");
            sb.Append("    {")
              .Append("\"t\": ").Append(R6(e.time))
              .Append(", \"clip\": ").Append(Quote(clip ?? ""))
              .Append(", \"idx\": ").Append(e.index.ToString(Inv))
              .Append(", \"x\": ").Append(R6(e.pos.x))
              .Append(", \"y\": ").Append(R6(e.pos.y))
              .Append(", \"ovx\": ").Append(R6(e.originVlc.x))
              .Append(", \"ovy\": ").Append(R6(e.originVlc.y))
              .Append(", \"ang\": ").Append(R6(e.angle))
              .Append(", \"col\": [").Append(R6(e.color.x)).Append(", ").Append(R6(e.color.y))
              .Append(", ").Append(R6(e.color.z)).Append(", ").Append(R6(e.color.w)).Append("]")
              .Append("}");
        }
        sb.Append(events.Count == 0 ? "],\n" : "\n  ],\n");

        sb.Append("  \"buffers\": [");
        bool firstBuf = true;
        foreach (string name in referenced)
        {
            if (!buffers.TryGetLoadedBufferForEditor(name, out BulletBufferManager.EditorBufferView view))
            {
                continue;
            }
            sb.Append(firstBuf ? "\n" : ",\n");
            firstBuf = false;
            sb.Append("    {")
              .Append("\"name\": ").Append(Quote(view.name))
              .Append(", \"homing\": ").Append(view.homing ? "true" : "false")
              .Append(", \"isLaser\": ").Append(view.isLaser ? "true" : "false")
              .Append(", \"count\": ").Append((view.bullets?.Count ?? 0).ToString(Inv))
              .Append(", \"sha1\": ").Append(Quote(BufferDigest(view.bullets)))
              .Append("}");
        }
        sb.Append(firstBuf ? "]\n" : "\n  ]\n");

        sb.Append("}\n");
        return sb.ToString();
    }

    // ---- Digest helpers ----

    public static string BufferDigest(List<BulletData> bullets)
    {
        StringBuilder sb = new StringBuilder();
        if (bullets != null)
        {
            for (int i = 0; i < bullets.Count; i++)
            {
                sb.Append(BulletFields(bullets[i])).Append(';');
            }
        }
        using (SHA1 sha1 = SHA1.Create())
        {
            byte[] hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
            StringBuilder hex = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash)
            {
                hex.Append(b.ToString("x2", Inv));
            }
            return hex.ToString();
        }
    }

    private static string BulletFields(BulletData b)
    {
        // Canonical, rounded field dump. Order is fixed; any change here changes
        // all goldens, so keep it stable.
        return string.Join("|", new[]
        {
            b.typeId.ToString(Inv),
            R6(b.scale.x), R6(b.scale.y),
            R6(b.color.x), R6(b.color.y), R6(b.color.z), R6(b.color.w),
            R6(b.speed), R6(b.gravity), R6(b.angleSpeed), R6(b.initialAngle),
            R6(b.polarForm.x), R6(b.polarForm.y), R6(b.radiusVlc), R6(b.thetaVlc),
            R6(b.startX), R6(b.startPos.x), R6(b.startPos.y),
            R6(b.polynomial.x), R6(b.polynomial.y), R6(b.polynomial.z), R6(b.polynomial.w),
            R6(b.originPos.x), R6(b.originPos.y), R6(b.originVlc.x), R6(b.originVlc.y),
            R6(b.playerInfluence.x), R6(b.playerInfluence.y),
            R6(b.appearTime), R6(b.appearDuration), R6(b.life), R6(b.random), R6(b.warpCooldown),
            b.unCounterable ? "1" : "0", b.lockRotation ? "1" : "0"
        });
    }

    public static string R6(float value)
    {
        double d = Math.Round((double)value, 6, MidpointRounding.AwayFromZero);
        if (d == 0d)
        {
            d = 0d; // normalize -0
        }
        return d.ToString("0.######", Inv);
    }

    private static string Quote(string value)
    {
        if (value == null)
        {
            return "\"\"";
        }
        StringBuilder sb = new StringBuilder(value.Length + 2);
        sb.Append('"');
        foreach (char c in value)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default: sb.Append(c); break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }
}

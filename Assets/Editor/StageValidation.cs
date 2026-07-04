using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Shared, side-effect-free validation used by both the editor linter menu and
/// the EditMode tests, so the two never drift. Every check returns messages into
/// a <see cref="Report"/>; errors are hard failures ("not green"), warnings are
/// advisories that do not fail the build.
/// </summary>
public static class StageValidation
{
    public sealed class Report
    {
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public bool IsGreen => Errors.Count == 0;

        public void Error(string message) => Errors.Add(message);
        public void Warn(string message) => Warnings.Add(message);
    }

    [Serializable]
    private class BufferFileJson
    {
        public string name;
        public List<BulletDataJson> bullets;
        public bool homing;
        public bool isLaser;
    }

    private const float AreaWidth = 32f;
    private const float AreaHeight = 18f;

    // ---- Buffer schema + range validation (no probe needed) ----

    public static void ValidateBuffers(BulletTypeDataBase btdb, Report report)
    {
        HashSet<string> validTypeNames = BuildValidTypeNames(btdb);
        foreach (string file in EnumerateBufferFiles())
        {
            string rel = ToAssetRelative(file);
            string text;
            try
            {
                text = File.ReadAllText(file);
            }
            catch (Exception ex)
            {
                report.Error($"[Buffer] Unable to read {rel}: {ex.Message}");
                continue;
            }

            BufferFileJson data;
            try
            {
                data = JsonUtility.FromJson<BufferFileJson>(text);
            }
            catch (Exception ex)
            {
                report.Error($"[Buffer] JSON parse failed for {rel}: {ex.Message}");
                continue;
            }

            if (data == null)
            {
                report.Error($"[Buffer] JSON produced null object: {rel}");
                continue;
            }

            if (data.bullets == null)
            {
                report.Error($"[Buffer] Missing required 'bullets' array: {rel}");
                continue;
            }

            if (data.bullets.Count == 0)
            {
                report.Warn($"[Buffer] Empty 'bullets' array: {rel}");
            }

            for (int i = 0; i < data.bullets.Count; i++)
            {
                BulletDataJson bullet = data.bullets[i];

                if (!data.isLaser)
                {
                    string typeName = bullet.typeName;
                    if (string.IsNullOrEmpty(typeName))
                    {
                        report.Error($"[Buffer] {rel} bullet[{i}] has empty typeName.");
                    }
                    else if (!validTypeNames.Contains(typeName))
                    {
                        report.Error($"[Buffer] {rel} bullet[{i}] typeName '{typeName}' is not in BulletTypeDataBase.");
                    }
                }

                // Range advisories.
                if (bullet.originPos.x < 0f || bullet.originPos.x > AreaWidth ||
                    bullet.originPos.y < 0f || bullet.originPos.y > AreaHeight)
                {
                    report.Warn($"[Buffer] {rel} bullet[{i}] originPos ({bullet.originPos.x}, {bullet.originPos.y}) is outside 0..{AreaWidth}/0..{AreaHeight}.");
                }

                if (bullet.life < 0f)
                {
                    report.Warn($"[Buffer] {rel} bullet[{i}] has negative life {bullet.life}.");
                }

                // For laser buffers appearTime is the beam width, not a spawn
                // time, so comparing it against life is meaningless there.
                if (!data.isLaser && bullet.life > 0f && bullet.appearTime > bullet.life)
                {
                    report.Warn($"[Buffer] {rel} bullet[{i}] appearTime {bullet.appearTime} exceeds life {bullet.life}.");
                }

                // Negative appearDuration is silently replaced by
                // BulletData.DefaultAppearDuration in BulletDataJson.ToBulletData();
                // authors should write the intended value explicitly.
                if (bullet.appearDuration < 0f)
                {
                    report.Warn($"[Buffer] {rel} bullet[{i}] appearDuration {bullet.appearDuration} is negative (runtime substitutes DefaultAppearDuration; write the explicit value).");
                }

                // polarForm.y and initialAngle are radians. A magnitude beyond
                // two full turns (4*pi) in these static angles almost always
                // means degrees were written by mistake (spawner angles are
                // degrees, JSON angles are radians). Rate fields
                // (thetaVlc/angleSpeed) are excluded: fast spins are legal.
                const float twoTurnsRad = 4f * Mathf.PI;
                if (Mathf.Abs(bullet.polarForm.y) > twoTurnsRad)
                {
                    report.Warn($"[Buffer] {rel} bullet[{i}] polarForm.y {bullet.polarForm.y} exceeds 4*pi — degrees written where radians are expected?");
                }

                if (Mathf.Abs(bullet.initialAngle) > twoTurnsRad)
                {
                    report.Warn($"[Buffer] {rel} bullet[{i}] initialAngle {bullet.initialAngle} exceeds 4*pi — degrees written where radians are expected?");
                }

                // color is a multiplier against the spawner color; w==0 is the
                // documented "show the sprite's own colors" mode. Components
                // outside 0..1 have no defined meaning in this renderer.
                if (OutsideUnitRange(bullet.color.x) || OutsideUnitRange(bullet.color.y) ||
                    OutsideUnitRange(bullet.color.z) || OutsideUnitRange(bullet.color.w))
                {
                    report.Warn($"[Buffer] {rel} bullet[{i}] color ({bullet.color.x}, {bullet.color.y}, {bullet.color.z}, {bullet.color.w}) has components outside 0..1.");
                }
            }
        }
    }

    private static bool OutsideUnitRange(float v) => v < 0f || v > 1f;

    // ---- Buffer file format validation (raw bytes; no probe needed) ----

    /// <summary>
    /// Validates the raw bytes of every BulletBuffer JSON. Files must be valid
    /// UTF-8 and must use one consistent line-ending style per file. A
    /// "CR CR" sequence is the signature of a text-mode double conversion
    /// accident (a real incident on 2026-07-04 corrupted files to \r\r\n) and is
    /// always an error. BOM/no-BOM and the CRLF-vs-LF choice both exist in the
    /// repo and are accepted as-is; only corruption and intra-file mixing fail.
    /// </summary>
    public static void ValidateBufferFileFormat(Report report)
    {
        foreach (string file in EnumerateBufferFiles())
        {
            string rel = ToAssetRelative(file);
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(file);
            }
            catch (Exception ex)
            {
                report.Error($"[Format] Unable to read {rel}: {ex.Message}");
                continue;
            }

            ValidateBufferBytes(rel, bytes, report);
        }
    }

    /// <summary>
    /// Byte-level format check for a single buffer file. Public so tests can
    /// feed synthetic corrupt content and prove the detector actually fires.
    /// </summary>
    public static void ValidateBufferBytes(string rel, byte[] bytes, Report report)
    {
        try
        {
            new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            report.Error($"[Format] {rel} is not valid UTF-8.");
            return;
        }

        int crlf = 0, bareLf = 0, bareCr = 0, crCr = 0;
        for (int i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] == (byte)'\r')
            {
                if (i + 1 < bytes.Length && bytes[i + 1] == (byte)'\r')
                {
                    crCr++;
                }

                if (i + 1 < bytes.Length && bytes[i + 1] == (byte)'\n')
                {
                    crlf++;
                    i++;
                }
                else
                {
                    bareCr++;
                }
            }
            else if (bytes[i] == (byte)'\n')
            {
                bareLf++;
            }
        }

        if (crCr > 0)
        {
            report.Error($"[Format] {rel} contains {crCr} CR CR sequence(s) (text-mode double conversion corruption; restore from git and rewrite in binary mode).");
        }
        else if (bareCr > 0)
        {
            report.Error($"[Format] {rel} contains {bareCr} bare CR character(s) not followed by LF.");
        }

        if (crlf > 0 && bareLf > 0)
        {
            report.Error($"[Format] {rel} mixes CRLF ({crlf}) and LF ({bareLf}) line endings.");
        }
    }

    // ---- Buffer registration-name validation (load-scope uniqueness) ----

    /// <summary>
    /// Built-in buffer names registered by <see cref="BulletBufferManager"/>
    /// before any JSON is read.
    /// </summary>
    private static readonly string[] BuiltInBufferNames = { "Rumia_0", "Rumia_1", "Line", "LineLaser", "Circle" };

    /// <summary>
    /// Mirrors BulletBufferManager.CommonDirectoryNames: these folders are
    /// loaded for every stage, before the stage's own folder.
    /// </summary>
    private static readonly HashSet<string> CommonBufferDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "common", "debug" };

    private const string ArchiveBufferDir = "_archive";

    /// <summary>
    /// Validates registration-name uniqueness per runtime load scope. A stage
    /// run loads: built-ins + every buffer under common/ and debug/ + the
    /// stage's own folder (BulletBufferManager.ReloadForStageBulletBuffersAsync).
    /// The registration key is the JSON 'name', falling back to the file name
    /// without extension when blank. A duplicate key inside one folder means
    /// one file silently replaces the other (last file wins by OS enumeration
    /// order) — always an authoring accident, so an error. A stage name that
    /// shadows a common/built-in buffer is reported as a warning because
    /// replace-by-name is documented behaviour and could be intentional.
    /// _archive/ is excluded: it is never a stage directory name, so the
    /// per-stage loader never reads it.
    /// </summary>
    public static void ValidateBufferNames(Report report)
    {
        // topDir -> (effectiveName -> first file registered under that name)
        Dictionary<string, Dictionary<string, string>> byDir =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (string file in EnumerateBufferFiles())
        {
            string top = TopLevelBufferDir(file);
            if (top == null || top.Equals(ArchiveBufferDir, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string rel = ToAssetRelative(file);
            BufferFileJson data;
            try
            {
                // Read/parse failures are already hard errors in ValidateBuffers;
                // skip here instead of double-reporting.
                data = JsonUtility.FromJson<BufferFileJson>(File.ReadAllText(file));
            }
            catch (Exception)
            {
                continue;
            }

            if (data == null)
            {
                continue;
            }

            string effective = data.name;
            if (string.IsNullOrWhiteSpace(effective))
            {
                effective = Path.GetFileNameWithoutExtension(file);
                report.Warn($"[Name] {rel} has a blank 'name'; runtime falls back to the file name '{effective}'. Prefer an explicit name.");
            }

            if (!byDir.TryGetValue(top, out Dictionary<string, string> names))
            {
                names = new Dictionary<string, string>(StringComparer.Ordinal);
                byDir.Add(top, names);
            }

            if (names.TryGetValue(effective, out string firstFile))
            {
                report.Error($"[Name] Duplicate buffer name '{effective}' inside '{top}': {ToAssetRelative(firstFile)} and {rel} (one silently replaces the other).");
            }
            else
            {
                names.Add(effective, file);
            }
        }

        // Base scope shared by every stage: built-ins + common folders.
        Dictionary<string, string> baseScope = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string builtIn in BuiltInBufferNames)
        {
            baseScope[builtIn] = "<built-in>";
        }

        foreach (KeyValuePair<string, Dictionary<string, string>> dir in byDir)
        {
            if (!CommonBufferDirs.Contains(dir.Key))
            {
                continue;
            }

            foreach (KeyValuePair<string, string> entry in dir.Value)
            {
                if (baseScope.TryGetValue(entry.Key, out string firstOwner))
                {
                    report.Error($"[Name] Buffer name '{entry.Key}' in {ToAssetRelative(entry.Value)} collides with {firstOwner} (both are loaded for every stage).");
                }
                else
                {
                    baseScope.Add(entry.Key, ToAssetRelative(entry.Value));
                }
            }
        }

        // Each stage folder is overlaid on the base scope.
        foreach (KeyValuePair<string, Dictionary<string, string>> dir in byDir)
        {
            if (CommonBufferDirs.Contains(dir.Key))
            {
                continue;
            }

            foreach (KeyValuePair<string, string> entry in dir.Value)
            {
                if (baseScope.TryGetValue(entry.Key, out string owner))
                {
                    report.Warn($"[Name] Stage buffer {ToAssetRelative(entry.Value)} shadows '{entry.Key}' from {owner} (replace-by-name; verify this is intentional).");
                }
            }
        }
    }

    /// <summary>
    /// Returns the top-level folder name under Assets/BulletBuffers for the
    /// given file (e.g. "stone", "common"), or null for files directly at the
    /// root.
    /// </summary>
    private static string TopLevelBufferDir(string absolutePath)
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "BulletBuffers"));
        string full = Path.GetFullPath(absolutePath);
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string remainder = full.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        int cut = remainder.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
        return cut < 0 ? null : remainder.Substring(0, cut);
    }

    // ---- BulletTypeDataBase integrity (no probe needed) ----

    public static void ValidateTypeDatabase(BulletTypeDataBase source, Report report)
    {
        if (source == null)
        {
            report.Error("[Types] BulletTypeDataBase asset is null.");
            return;
        }

        // Advisory: BulletType assets on disk that are not registered in the DB.
        // They are unusable from JSON until added (see 'Sync Bullet Types').
        foreach (string path in FindUnregisteredBulletTypeAssetPaths(source))
        {
            report.Warn($"[Types] BulletType asset '{path}' is not registered in BulletTypeDataBase (run 'Tools/Bullet Hell/Sync Bullet Types').");
        }

        // Work on a clone so Init() (which rebuilds parallel arrays) never dirties
        // the real asset.
        BulletTypeDataBase clone = UnityEngine.Object.Instantiate(source);
        try
        {
            clone.Init();

            if (clone.types == null || clone.types.Length == 0)
            {
                report.Error("[Types] BulletTypeDataBase has no types.");
                return;
            }

            Texture2D[] baseTextures = clone.GetBaseTextures();
            Texture2D[] maskTextures = clone.GetMaskTextures();
            if (baseTextures.Length != clone.types.Length || maskTextures.Length != clone.types.Length)
            {
                report.Error("[Types] Texture accessor length mismatch with types array.");
            }

            HashSet<string> seenNames = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < clone.types.Length; i++)
            {
                BulletType type = clone.types[i];
                if (type == null)
                {
                    report.Warn($"[Types] Null BulletType entry at index {i}.");
                    continue;
                }

                if (string.IsNullOrEmpty(type.typeName))
                {
                    report.Warn($"[Types] BulletType at index {i} has an empty typeName (unusable from JSON).");
                }
                else
                {
                    if (!seenNames.Add(type.typeName))
                    {
                        report.Error($"[Types] Duplicate typeName '{type.typeName}' at index {i}.");
                    }

                    int resolved = clone.GetTypeId(type.typeName);
                    if (resolved != i)
                    {
                        report.Error($"[Types] typeName '{type.typeName}' resolves to {resolved} but is stored at {i}.");
                    }
                }

                if (type.verts == null)
                {
                    report.Error($"[Types] BulletType '{type.typeName}' (index {i}) has null verts array.");
                }
                else if (type.verts.Length > 0 && type.verts.Length < 3)
                {
                    report.Warn($"[Types] BulletType '{type.typeName}' (index {i}) has degenerate verts ({type.verts.Length}).");
                }

                if (type.baseSprite == null)
                {
                    report.Warn($"[Types] BulletType '{type.typeName}' (index {i}) has no baseSprite texture.");
                }
                else if (!type.baseSprite.isReadable)
                {
                    report.Warn($"[Types] BulletType '{type.typeName}' (index {i}) baseSprite '{type.baseSprite.name}' is not marked Read/Write.");
                }

                if (type.maskSprite != null && !type.maskSprite.isReadable)
                {
                    report.Warn($"[Types] BulletType '{type.typeName}' (index {i}) maskSprite '{type.maskSprite.name}' is not marked Read/Write.");
                }

                WarnIfTextureCompressed(type.baseSprite, "baseSprite", type.typeName, i, report);
                WarnIfTextureCompressed(type.maskSprite, "maskSprite", type.typeName, i, report);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(clone);
        }
    }

    // ---- Stage clipName link validation (requires an active EditorStageProbe) ----

    public const string KnownUnresolvedFileName = "known-unresolved-links.tsv";

    /// <summary>
    /// Enumerates every (stageDir, clipName) whose clip does not resolve to a
    /// loaded buffer. "Clear" is excluded (documented special case). Requires an
    /// active <see cref="EditorStageProbe"/>. Shared by the linter/test path and
    /// the baseline generator so they never diverge.
    /// </summary>
    public static List<KeyValuePair<string, string>> CollectUnresolvedLinks(IReadOnlyDictionary<string, StageData> stages, Report report = null)
    {
        List<KeyValuePair<string, string>> unresolved = new List<KeyValuePair<string, string>>();
        foreach (string dir in StageGoldenDumper.OfficialStageDirs)
        {
            if (!stages.TryGetValue(dir, out StageData stage))
            {
                report?.Error($"[Link] Official stage '{dir}' was not loaded.");
                continue;
            }

            BulletBufferManager buffers = new BulletBufferManager();
            buffers.ReloadForStageBulletBuffersAsync(dir).GetAwaiter().GetResult();

            List<BulletSpawner> spawners = stage.bulletSpawners ?? new List<BulletSpawner>();
            for (int i = 0; i < spawners.Count; i++)
            {
                string clip = spawners[i].clipName;
                if (clip == StageScheduleExpander.ClearClipName)
                {
                    continue;
                }

                if (!buffers.TryGetBulletClipIndex(clip, out _))
                {
                    unresolved.Add(new KeyValuePair<string, string>(dir, clip));
                }
            }
        }
        return unresolved;
    }

    /// <summary>
    /// Validates stage clip links. Clips that do not resolve are hard errors
    /// unless they appear in the committed known-unresolved baseline
    /// (pre-existing data debt scheduled for P1, e.g. mojibake buffer names in
    /// stage 25), in which case they are reported as tracked warnings so the
    /// suite stays green while still surfacing the debt.
    /// </summary>
    public static void ValidateStageLinks(IReadOnlyDictionary<string, StageData> stages, Report report)
    {
        HashSet<string> baseline = LoadKnownUnresolved();
        foreach (KeyValuePair<string, string> pair in CollectUnresolvedLinks(stages, report))
        {
            string key = MakeKey(pair.Key, pair.Value);
            if (baseline.Contains(key))
            {
                report.Warn($"[Link] Known pre-existing unresolved clipName '{pair.Value}' in stage '{pair.Key}' (tracked for P1).");
            }
            else
            {
                report.Error($"[Link] Stage '{pair.Key}' clipName '{pair.Value}' does not resolve to any loaded buffer.");
            }
        }
    }

    public static string KnownUnresolvedPath()
    {
        return System.IO.Path.Combine(StageGoldenDumper.GoldenDirectory(), KnownUnresolvedFileName);
    }

    public static string MakeKey(string stageDir, string clip)
    {
        return stageDir + "\t" + clip;
    }

    private static HashSet<string> LoadKnownUnresolved()
    {
        HashSet<string> set = new HashSet<string>(StringComparer.Ordinal);
        string path = KnownUnresolvedPath();
        if (!File.Exists(path))
        {
            return set;
        }

        foreach (string line in File.ReadAllLines(path))
        {
            string trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("#"))
            {
                continue;
            }
            set.Add(trimmed);
        }
        return set;
    }

    // ---- Stage enemy-structure typeName validation (static; no probe needed) ----

    // Lint-only mirrors of the private DTOs in StageDataManager.cs
    // (EnemySpawnerJson / BulletClipJson / BulletChangeClipJson /
    // BulletDataJsonDeserializer). JsonUtility ignores JSON keys absent from the
    // target type, so a subset mirror sees exactly what the runtime sees for the
    // fields we check. Keep these fields in sync if StageDataManager's DTOs move.
    [Serializable] private struct EnemyBulletLintJson { public string typeName; }
    [Serializable] private struct EnemyClipLintJson { public EnemyBulletLintJson data; public int number; }
    [Serializable] private struct EnemyChangeClipLintJson { public EnemyClipLintJson clip; }
    [Serializable]
    private class EnemySpawnerLintJson
    {
        public string enemyName = "";
        public int count;
        public int bulletCount;
        public EnemyBulletLintJson orbit;
        public EnemyClipLintJson bulletClip;
        public List<EnemyChangeClipLintJson> bulletChangeClips = new List<EnemyChangeClipLintJson>();
    }
    [Serializable]
    private class StageEnemyLintJson
    {
        public List<EnemySpawnerLintJson> enemySpawners = new List<EnemySpawnerLintJson>();
    }

    /// <summary>
    /// Enumerates every official stage JSON on disk: Assets/StageData/&lt;dir&gt;/&lt;dir&gt;.json.
    /// Mirrors StageDataManager's per-directory naming rule.
    /// </summary>
    public static IEnumerable<string> EnumerateStageJsonFiles()
    {
        string root = Path.Combine(Application.dataPath, "StageData");
        if (!Directory.Exists(root))
        {
            yield break;
        }

        string[] dirs = Directory.GetDirectories(root);
        Array.Sort(dirs, StringComparer.Ordinal);
        foreach (string dir in dirs)
        {
            string candidate = Path.Combine(dir, Path.GetFileName(dir) + ".json");
            if (File.Exists(candidate))
            {
                yield return candidate;
            }
        }
    }

    /// <summary>
    /// Validates the bullet typeNames declared by every stage's enemySpawners,
    /// statically (no probe / Play Mode). Severity is keyed to the runtime firing
    /// semantics measured in MultiBullet + QuadOrder: a spawner emits bullets iff
    /// count &gt; 0 && bulletCount &gt; 0 && bulletClip.number &gt; 0, and
    /// bulletChangeClips only ever apply to bullets that were emitted. An
    /// unresolved typeName on a firing bullet resolves to typeId -1 and misbehaves
    /// at runtime, so those are errors; the same typo on a dormant (never-firing)
    /// spawner is only a warning, keeping the debt visible without failing the
    /// suite. orbit.typeName is never required — the enemy's own trajectory
    /// ignores typeId and every real spawner leaves it empty, so empty is silent.
    /// Error-free and warning-free on the current data (captain + stone, the only
    /// dirs with enemySpawners); keep it that way.
    /// </summary>
    public static void ValidateStageEnemyTypeNames(BulletTypeDataBase btdb, Report report)
    {
        foreach (string file in EnumerateStageJsonFiles())
        {
            string rel = ToAssetRelative(file);
            string json;
            try
            {
                json = File.ReadAllText(file);
            }
            catch (Exception ex)
            {
                report.Error($"[Enemy] Unable to read {rel}: {ex.Message}");
                continue;
            }

            ValidateStageEnemyJson(rel, json, btdb, report);
        }
    }

    /// <summary>
    /// Enemy-structure typeName check for a single stage JSON. Public so tests can
    /// feed synthetic JSON and prove the detector actually fires. See
    /// <see cref="ValidateStageEnemyTypeNames"/> for the severity rationale.
    /// </summary>
    public static void ValidateStageEnemyJson(string rel, string json, BulletTypeDataBase btdb, Report report)
    {
        StageEnemyLintJson data;
        try
        {
            data = JsonUtility.FromJson<StageEnemyLintJson>(json);
        }
        catch (Exception ex)
        {
            report.Error($"[Enemy] JSON parse failed for {rel}: {ex.Message}");
            return;
        }

        if (data == null)
        {
            report.Error($"[Enemy] JSON produced null object: {rel}");
            return;
        }

        if (data.enemySpawners == null || data.enemySpawners.Count == 0)
        {
            return;
        }

        HashSet<string> valid = BuildValidTypeNames(btdb);

        for (int i = 0; i < data.enemySpawners.Count; i++)
        {
            EnemySpawnerLintJson s = data.enemySpawners[i];
            if (s == null)
            {
                continue;
            }

            bool fires = s.count > 0 && s.bulletCount > 0 && s.bulletClip.number > 0;

            // orbit.typeName drives only the enemy's own trajectory, which ignores
            // typeId; empty is the universal real-data pattern and never flagged.
            // A non-empty unresolved value is still an authoring typo worth an error.
            string orbitName = s.orbit.typeName;
            if (!string.IsNullOrEmpty(orbitName) && !valid.Contains(orbitName))
            {
                report.Error($"[Enemy] {rel} enemySpawners[{i}] orbit typeName '{orbitName}' is not in BulletTypeDataBase.");
            }

            string clipName = s.bulletClip.data.typeName;
            if (fires && string.IsNullOrEmpty(clipName))
            {
                report.Error($"[Enemy] {rel} enemySpawners[{i}] fires {s.bulletCount} time(s) but bulletClip typeName is empty (bullets would resolve to typeId -1).");
            }
            else if (fires && !valid.Contains(clipName))
            {
                report.Error($"[Enemy] {rel} enemySpawners[{i}] bulletClip typeName '{clipName}' is not in BulletTypeDataBase.");
            }
            else if (!fires && !string.IsNullOrEmpty(clipName) && !valid.Contains(clipName))
            {
                report.Warn($"[Enemy] {rel} enemySpawners[{i}] dormant bulletClip typeName '{clipName}' is not in BulletTypeDataBase.");
            }

            // Configured to fire (count/bulletCount > 0) but the clip holds zero
            // bullets, so nothing is ever emitted: surface the dead configuration.
            if (s.count > 0 && s.bulletCount > 0 && s.bulletClip.number <= 0)
            {
                report.Warn($"[Enemy] {rel} enemySpawners[{i}] bulletCount {s.bulletCount} > 0 but bulletClip.number is {s.bulletClip.number}; the spawner never emits bullets.");
            }

            if (s.bulletChangeClips == null)
            {
                continue;
            }

            for (int k = 0; k < s.bulletChangeClips.Count; k++)
            {
                // A change clip replaces the in-flight bullet wholesale; an empty
                // or unresolved typeName resolves to typeId -1 and makes the fired
                // bullet vanish, so it is an error whenever the parent fires.
                string changeName = s.bulletChangeClips[k].clip.data.typeName;
                bool changeResolved = !string.IsNullOrEmpty(changeName) && valid.Contains(changeName);
                if (fires && !changeResolved)
                {
                    string why = string.IsNullOrEmpty(changeName) ? "is empty" : "is not in BulletTypeDataBase";
                    report.Error($"[Enemy] {rel} enemySpawners[{i}] bulletChangeClips[{k}] typeName '{changeName}' {why}.");
                }
                else if (!fires && !string.IsNullOrEmpty(changeName) && !valid.Contains(changeName))
                {
                    report.Warn($"[Enemy] {rel} enemySpawners[{i}] dormant bulletChangeClips[{k}] typeName '{changeName}' is not in BulletTypeDataBase.");
                }
            }
        }
    }

    // ---- BulletType asset registration / import validation ----

    public const string BulletTypesFolder = "Assets/Scripts/Bullets/BulletTypes";

    /// <summary>
    /// Returns the asset paths of every <see cref="BulletType"/> under
    /// <see cref="BulletTypesFolder"/> that is not present in the given
    /// database's <c>types</c> array. Shared by the linter and the
    /// "Sync Bullet Types" menu so they never diverge.
    /// </summary>
    public static List<string> FindUnregisteredBulletTypeAssetPaths(BulletTypeDataBase btdb)
    {
        List<string> result = new List<string>();
        HashSet<BulletType> registered = new HashSet<BulletType>();
        if (btdb != null && btdb.types != null)
        {
            foreach (BulletType type in btdb.types)
            {
                if (type != null)
                {
                    registered.Add(type);
                }
            }
        }

        string[] guids = AssetDatabase.FindAssets("t:BulletType", new[] { BulletTypesFolder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            BulletType asset = AssetDatabase.LoadAssetAtPath<BulletType>(path);
            if (asset != null && !registered.Contains(asset))
            {
                result.Add(path);
            }
        }
        result.Sort(StringComparer.Ordinal);
        return result;
    }

    private static void WarnIfTextureCompressed(Texture2D texture, string role, string typeName, int index, Report report)
    {
        if (texture == null)
        {
            return;
        }

        string path = AssetDatabase.GetAssetPath(texture);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            report.Warn($"[Types] BulletType '{typeName}' (index {index}) {role} '{texture.name}' is compressed ({importer.textureCompression}); the renderer expects Uncompressed textures.");
        }
    }

    // ---- Helpers ----

    private static HashSet<string> BuildValidTypeNames(BulletTypeDataBase btdb)
    {
        HashSet<string> names = new HashSet<string>(StringComparer.Ordinal)
        {
            BulletData.ScreenNoiseTypeName
        };
        if (btdb != null && btdb.types != null)
        {
            foreach (BulletType type in btdb.types)
            {
                if (type != null && !string.IsNullOrEmpty(type.typeName))
                {
                    names.Add(type.typeName);
                }
            }
        }
        return names;
    }

    public static IEnumerable<string> EnumerateBufferFiles()
    {
        string root = Path.Combine(Application.dataPath, "BulletBuffers");
        if (!Directory.Exists(root))
        {
            yield break;
        }

        string[] files = Directory.GetFiles(root, "*.json", SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.Ordinal);
        foreach (string file in files)
        {
            yield return file;
        }
    }

    private static string ToAssetRelative(string absolutePath)
    {
        string full = Path.GetFullPath(absolutePath).Replace('\\', '/');
        string data = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
        return full.StartsWith(data, StringComparison.OrdinalIgnoreCase)
            ? "Assets" + full.Substring(data.Length)
            : full;
    }
}

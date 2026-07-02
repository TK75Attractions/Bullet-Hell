using System;
using System.Collections.Generic;
using System.IO;
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

                if (bullet.life > 0f && bullet.appearTime > bullet.life)
                {
                    report.Warn($"[Buffer] {rel} bullet[{i}] appearTime {bullet.appearTime} exceeds life {bullet.life}.");
                }
            }
        }
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

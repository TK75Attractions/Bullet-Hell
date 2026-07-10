using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

/// <summary>
/// Captain と同じ Sprite フレーム + EnemyVisualSetAsset の形式で、追加キャラクターの GIF を変換する。
/// 魔法陣・本・銃はキャラクターと別の SpriteRenderer に表示できるよう、独立した Visual Set にする。
/// </summary>
public static class AdditionalEnemyVisualSetGenerator
{
    private const string AddressableGroupName = "EnemyVisuals_Additional";
    private const float PixelsPerUnit = 100f;

    private static readonly VisualSetSpec[] VisualSets =
    {
        new VisualSetSpec(
            "fifth-student",
            "Assets/Sprites/Enemy/5th Student",
            "Assets/Sprites/Enemy/5th Student/FifthStudentVisualSet.asset",
            "Assets/Sprites/Enemy/5th Student/GeneratedFrames/Character",
            "enemy/fifth-student",
            "enemy-visual-fifth-student",
            new[]
            {
                new ClipSpec("idle", "五番弟子アニメ(通常).gif", true, ""),
                new ClipSpec("cast1", "五番弟子アニメ(詠唱1).gif", false, "idle"),
                new ClipSpec("up1", "五番弟子アニメ(上1).gif", false, "idle"),
                new ClipSpec("up2", "五番弟子アニメ(上2).gif", false, "idle"),
                new ClipSpec("up3", "五番弟子アニメ(上3).gif", false, "idle"),
                new ClipSpec("left1", "五番弟子アニメ(左1).gif", false, "idle"),
                new ClipSpec("left2", "五番弟子アニメ(左2).gif", false, "idle"),
                new ClipSpec("left3", "五番弟子アニメ(左3).gif", false, "idle"),
                new ClipSpec("defeat", "五番弟子アニメ(撃破).gif", false, "")
            }),
        new VisualSetSpec(
            "fifth-student-magic-circle",
            "Assets/Sprites/Enemy/5th Student",
            "Assets/Sprites/Enemy/5th Student/FifthStudentMagicCircleVisualSet.asset",
            "Assets/Sprites/Enemy/5th Student/GeneratedFrames/MagicCircle",
            "enemy/fifth-student-magic-circle",
            "enemy-visual-fifth-student-magic-circle",
            new[]
            {
                new ClipSpec("cast1", "魔法陣アニメ(詠唱1).gif", true, ""),
                new ClipSpec("cast2", "魔法陣アニメ(詠唱2).gif", true, ""),
                new ClipSpec("cast3", "魔法陣アニメ(詠唱3).gif", true, "")
            }),
        new VisualSetSpec(
            "immortal",
            "Assets/Sprites/Enemy/Immortal",
            "Assets/Sprites/Enemy/Immortal/ImmortalVisualSet.asset",
            "Assets/Sprites/Enemy/Immortal/GeneratedFrames/Character",
            "enemy/immortal",
            "enemy-visual-immortal",
            new[]
            {
                new ClipSpec("idle", "不死者アニメ(通常1).gif", true, ""),
                new ClipSpec("idle2", "不死者アニメ(通常2).gif", true, ""),
                new ClipSpec("blink", "不死者アニメ(瞬き).gif", false, "idle"),
                new ClipSpec("wake", "不死者アニメ(起床).gif", false, "idle"),
                new ClipSpec("defeat", "不死者アニメ(撃破).gif", false, "")
            }),
        new VisualSetSpec(
            "immortal-magic-circle",
            "Assets/Sprites/Enemy/Immortal",
            "Assets/Sprites/Enemy/Immortal/ImmortalMagicCircleVisualSet.asset",
            "Assets/Sprites/Enemy/Immortal/GeneratedFrames/MagicCircle",
            "enemy/immortal-magic-circle",
            "enemy-visual-immortal-magic-circle",
            new[]
            {
                new ClipSpec("deploy", "魔法陣アニメ(展開).gif", false, "chant"),
                new ClipSpec("chant", "魔法陣アニメ(詠唱).gif", true, "")
            }),
        new VisualSetSpec(
            "scholar",
            "Assets/Sprites/Enemy/Scholar",
            "Assets/Sprites/Enemy/Scholar/ScholarVisualSet.asset",
            "Assets/Sprites/Enemy/Scholar/GeneratedFrames/Character",
            "enemy/scholar",
            "enemy-visual-scholar",
            new[]
            {
                new ClipSpec("idle", "学者アニメ(通常).gif", true, ""),
                new ClipSpec("reading", "学者アニメ(読書).gif", true, ""),
                new ClipSpec("receiveGun", "学者アニメ(銃受取).gif", false, "readyGun"),
                new ClipSpec("readyGun", "学者アニメ(銃構え).gif", true, ""),
                new ClipSpec("fireGun", "学者アニメ(銃発砲).gif", false, "readyGun"),
                new ClipSpec("throwGun", "学者アニメ(銃投擲).gif", false, "idle"),
                new ClipSpec("defeat", "学者アニメ(撃破).gif", false, "")
            }),
        new VisualSetSpec(
            "scholar-book",
            "Assets/Sprites/Enemy/Scholar",
            "Assets/Sprites/Enemy/Scholar/ScholarBookVisualSet.asset",
            "Assets/Sprites/Enemy/Scholar/GeneratedFrames/Book",
            "enemy/scholar-book",
            "enemy-visual-scholar-book",
            new[]
            {
                new ClipSpec("idle", "本アニメ(通常).gif", true, ""),
                new ClipSpec("drop", "本アニメ(落下).gif", false, "")
            }),
        new VisualSetSpec(
            "scholar-gun",
            "Assets/Sprites/Enemy/Scholar",
            "Assets/Sprites/Enemy/Scholar/ScholarGunVisualSet.asset",
            "Assets/Sprites/Enemy/Scholar/GeneratedFrames/Gun",
            "enemy/scholar-gun",
            "enemy-visual-scholar-gun",
            new[]
            {
                new ClipSpec("idle", "銃アニメ(通常).gif", true, ""),
                new ClipSpec("reload", "銃アニメ(装填).gif", false, "idle")
            })
    };

    [MenuItem("Tools/Bullet Hell/Enemy Visuals/Generate Additional Enemy Visual Sets")]
    public static void GenerateAll()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        if (settings == null)
        {
            Debug.LogError("AddressableAssetSettings could not be created.");
            return;
        }

        AddressableAssetGroup group = GetOrCreateGroup(settings, AddressableGroupName);
        int generatedCount = 0;
        for (int i = 0; i < VisualSets.Length; i++)
        {
            if (GenerateVisualSet(VisualSets[i], settings, group))
            {
                generatedCount++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Generated {generatedCount}/{VisualSets.Length} additional enemy visual sets.");
    }

    private static bool GenerateVisualSet(VisualSetSpec spec, AddressableAssetSettings settings, AddressableAssetGroup group)
    {
        if (!Directory.Exists(spec.sourceDirectory))
        {
            Debug.LogWarning($"Enemy GIF directory not found: {spec.sourceDirectory}");
            return false;
        }

        EnsureCleanDirectory(spec.frameDirectory);

        EnemyVisualSetAsset visualSet = AssetDatabase.LoadAssetAtPath<EnemyVisualSetAsset>(spec.assetPath);
        if (visualSet == null)
        {
            visualSet = ScriptableObject.CreateInstance<EnemyVisualSetAsset>();
            AssetDatabase.CreateAsset(visualSet, spec.assetPath);
        }

        visualSet.visualId = spec.visualId;
        visualSet.clips.Clear();
        for (int i = 0; i < spec.clips.Length; i++)
        {
            EnemyVisualClipAsset clip = GenerateClip(spec, spec.clips[i]);
            if (clip != null)
            {
                visualSet.clips.Add(clip);
            }
        }

        visualSet.fallbackSprite = FindFallbackSprite(visualSet);
        EditorUtility.SetDirty(visualSet);
        AssetDatabase.SaveAssets();

        RegisterAddressable(settings, group, spec);
        Debug.Log($"Generated enemy visual set: {spec.assetPath} ({visualSet.clips.Count} clips, address '{spec.address}').");
        return visualSet.clips.Count > 0;
    }

    private static EnemyVisualClipAsset GenerateClip(VisualSetSpec spec, ClipSpec clip)
    {
        string gifPath = Path.Combine(spec.sourceDirectory, clip.fileName).Replace('\\', '/');
        if (!File.Exists(gifPath))
        {
            Debug.LogWarning($"Enemy GIF not found: {gifPath}");
            return null;
        }

        GifAnimationData gif = GifAnimationDecoder.Decode(File.ReadAllBytes(gifPath), 0.1f);
        string clipDirectory = Path.Combine(spec.frameDirectory, clip.name).Replace('\\', '/');
        Directory.CreateDirectory(clipDirectory);

        List<Sprite> sprites = new List<Sprite>();
        List<float> durations = new List<float>();
        for (int i = 0; i < gif.frames.Count; i++)
        {
            GifFrameData frame = gif.frames[i];
            string framePath = Path.Combine(clipDirectory, $"{clip.name}_{i:00}.png").Replace('\\', '/');
            WriteFramePng(framePath, gif.width, gif.height, frame.pixels);
            ImportFrameAsSprite(framePath);

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(framePath);
            if (sprite == null)
            {
                Debug.LogWarning($"Generated frame could not be loaded as Sprite: {framePath}");
                continue;
            }

            sprites.Add(sprite);
            durations.Add(frame.delaySeconds > 0f ? frame.delaySeconds : 0.1f);
        }

        return new EnemyVisualClipAsset
        {
            name = clip.name,
            frames = sprites,
            frameDurations = durations,
            loop = clip.loop,
            next = clip.next
        };
    }

    private static void WriteFramePng(string framePath, int width, int height, Color32[] topLeftPixels)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels32(EnemyVisualLoader.ConvertTopLeftPixelsToUnityPixels(topLeftPixels, width, height));
        texture.Apply(false, false);
        File.WriteAllBytes(framePath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
    }

    private static void ImportFrameAsSprite(string framePath)
    {
        AssetDatabase.ImportAsset(framePath, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(framePath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
    }

    private static Sprite FindFallbackSprite(EnemyVisualSetAsset visualSet)
    {
        EnemyVisualClipAsset idle = visualSet.clips.Find(clip => clip != null && clip.name == "idle");
        if (idle != null && idle.frames != null && idle.frames.Count > 0)
        {
            return idle.frames[0];
        }

        for (int i = 0; i < visualSet.clips.Count; i++)
        {
            EnemyVisualClipAsset clip = visualSet.clips[i];
            if (clip != null && clip.frames != null && clip.frames.Count > 0)
            {
                return clip.frames[0];
            }
        }

        return null;
    }

    private static void RegisterAddressable(AddressableAssetSettings settings, AddressableAssetGroup group, VisualSetSpec spec)
    {
        string guid = AssetDatabase.AssetPathToGUID(spec.assetPath);
        if (string.IsNullOrWhiteSpace(guid))
        {
            Debug.LogError($"Visual set GUID was not found: {spec.assetPath}");
            return;
        }

        AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group);
        entry.address = spec.address;
        settings.AddLabel(spec.label);
        entry.SetLabel(spec.label, true, true);
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
    }

    private static AddressableAssetGroup GetOrCreateGroup(AddressableAssetSettings settings, string groupName)
    {
        AddressableAssetGroup group = settings.FindGroup(groupName);
        if (group != null)
        {
            return group;
        }

        List<AddressableAssetGroupSchema> schemas = settings.DefaultGroup != null
            ? settings.DefaultGroup.Schemas
            : null;
        return settings.CreateGroup(groupName, false, false, true, schemas);
    }

    private static void EnsureCleanDirectory(string assetDirectory)
    {
        if (AssetDatabase.IsValidFolder(assetDirectory))
        {
            AssetDatabase.DeleteAsset(assetDirectory);
        }

        string parent = Path.GetDirectoryName(assetDirectory)?.Replace('\\', '/');
        string name = Path.GetFileName(assetDirectory);
        if (!AssetDatabase.IsValidFolder(parent))
        {
            Directory.CreateDirectory(parent);
            AssetDatabase.Refresh();
        }

        AssetDatabase.CreateFolder(parent, name);
    }

    private readonly struct VisualSetSpec
    {
        public readonly string visualId;
        public readonly string sourceDirectory;
        public readonly string assetPath;
        public readonly string frameDirectory;
        public readonly string address;
        public readonly string label;
        public readonly ClipSpec[] clips;

        public VisualSetSpec(
            string visualId,
            string sourceDirectory,
            string assetPath,
            string frameDirectory,
            string address,
            string label,
            ClipSpec[] clips)
        {
            this.visualId = visualId;
            this.sourceDirectory = sourceDirectory;
            this.assetPath = assetPath;
            this.frameDirectory = frameDirectory;
            this.address = address;
            this.label = label;
            this.clips = clips;
        }
    }

    private readonly struct ClipSpec
    {
        public readonly string name;
        public readonly string fileName;
        public readonly bool loop;
        public readonly string next;

        public ClipSpec(string name, string fileName, bool loop, string next)
        {
            this.name = name;
            this.fileName = fileName;
            this.loop = loop;
            this.next = next;
        }
    }
}

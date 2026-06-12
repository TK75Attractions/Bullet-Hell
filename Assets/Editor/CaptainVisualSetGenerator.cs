using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

public static class CaptainVisualSetGenerator
{
    private const string GifRoot = "Assets/Sprites/Enemy/Captain";
    private const string FrameRoot = "Assets/Sprites/Enemy/Captain/GeneratedFrames";
    private const string VisualSetPath = "Assets/Sprites/Enemy/Captain/CaptainVisualSet.asset";
    private const string CaptainStageJsonPath = "Assets/StageData/captain/captain.json";
    private const string AddressableGroupName = "EnemyVisuals_Captain";
    private const string AddressableAddress = "enemy/captain";
    private const string AddressableLabel = "enemy-visual-captain";
    private const float PixelsPerUnit = 100f;

    private static readonly ClipSpec[] Clips =
    {
        new ClipSpec("idle", "艦長アニメ(通常).gif", true, ""),
        new ClipSpec("windup", "艦長アニメ(錨構え1).gif", false, "charge"),
        new ClipSpec("charge", "艦長アニメ(錨構え2).gif", true, ""),
        new ClipSpec("attack", "艦長アニメ(抜錨1).gif", false, "idle"),
        new ClipSpec("attackHold1", "艦長アニメ(抜錨2).gif", false, "idle"),
        new ClipSpec("attack2", "艦長アニメ(抜錨3).gif", false, "idle"),
        new ClipSpec("attackHold2", "艦長アニメ(抜錨4).gif", false, "idle"),
        new ClipSpec("attack3", "艦長アニメ(抜錨5).gif", false, "idle"),
        new ClipSpec("defeat", "艦長アニメ(撃破).gif", false, ""),
        new ClipSpec("portrait", "艦長アニメ(立ち絵).gif", false, ""),
    };

    [MenuItem("Tools/Bullet Hell/Enemy Visuals/Generate Captain Visual Set")]
    public static void GenerateCaptainVisualSet()
    {
        if (!Directory.Exists(GifRoot))
        {
            Debug.LogError($"Captain GIF directory not found: {GifRoot}");
            return;
        }

        EnsureCleanDirectory(FrameRoot);

        EnemyVisualSetAsset visualSet = AssetDatabase.LoadAssetAtPath<EnemyVisualSetAsset>(VisualSetPath);
        if (visualSet == null)
        {
            visualSet = ScriptableObject.CreateInstance<EnemyVisualSetAsset>();
            AssetDatabase.CreateAsset(visualSet, VisualSetPath);
        }

        visualSet.visualId = "captain";
        visualSet.clips.Clear();

        foreach (ClipSpec clip in Clips)
        {
            EnemyVisualClipAsset clipAsset = GenerateClip(clip);
            if (clipAsset != null)
            {
                visualSet.clips.Add(clipAsset);
            }
        }

        visualSet.fallbackSprite = FindFallbackSprite(visualSet);
        EditorUtility.SetDirty(visualSet);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        RegisterAddressable();

        Debug.Log($"Generated Captain visual set: {VisualSetPath} ({visualSet.clips.Count} clips, address '{AddressableAddress}').");
    }

    [MenuItem("Tools/Bullet Hell/Enemy Visuals/Use Captain Addressable Visual In Stage JSON")]
    public static void UseCaptainAddressableVisualInStageJson()
    {
        if (!File.Exists(CaptainStageJsonPath))
        {
            Debug.LogError($"Captain stage json not found: {CaptainStageJsonPath}");
            return;
        }

        string json = File.ReadAllText(CaptainStageJsonPath);
        string block = BuildAddressableEnemyVisualsBlock();
        if (!TryReplaceJsonPropertyBlock(json, "enemyVisuals", block, out string updatedJson))
        {
            Debug.LogError($"Failed to replace enemyVisuals in {CaptainStageJsonPath}");
            return;
        }

        File.WriteAllText(CaptainStageJsonPath, updatedJson);
        AssetDatabase.ImportAsset(CaptainStageJsonPath);
        Debug.Log($"Updated {CaptainStageJsonPath} to use addressable visual '{AddressableAddress}'.");
    }

    [MenuItem("Tools/Bullet Hell/Enemy Visuals/Generate Captain Visual Set And Use In Stage JSON")]
    public static void GenerateCaptainVisualSetAndUseInStageJson()
    {
        GenerateCaptainVisualSet();
        UseCaptainAddressableVisualInStageJson();
    }

    private static EnemyVisualClipAsset GenerateClip(ClipSpec clip)
    {
        string gifPath = Path.Combine(GifRoot, clip.fileName).Replace('\\', '/');
        if (!File.Exists(gifPath))
        {
            Debug.LogWarning($"Captain GIF not found: {gifPath}");
            return null;
        }

        GifAnimationData gif = GifAnimationDecoder.Decode(File.ReadAllBytes(gifPath), 0.1f);
        string clipDirectory = Path.Combine(FrameRoot, clip.name).Replace('\\', '/');
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
            if (sprite != null)
            {
                sprites.Add(sprite);
                durations.Add(frame.delaySeconds);
            }
            else
            {
                Debug.LogWarning($"Generated frame could not be loaded as Sprite: {framePath}");
            }
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

    private static void RegisterAddressable()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        if (settings == null)
        {
            Debug.LogError("AddressableAssetSettings could not be created.");
            return;
        }

        AddressableAssetGroup group = GetOrCreateGroup(settings, AddressableGroupName);
        string guid = AssetDatabase.AssetPathToGUID(VisualSetPath);
        if (string.IsNullOrWhiteSpace(guid))
        {
            Debug.LogError($"Visual set guid was not found: {VisualSetPath}");
            return;
        }

        AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group);
        entry.address = AddressableAddress;
        settings.AddLabel(AddressableLabel);
        entry.SetLabel(AddressableLabel, true, true);
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
        AssetDatabase.SaveAssets();
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

    private static string BuildAddressableEnemyVisualsBlock()
    {
        return
            "  \"enemyVisuals\": [\n" +
            "    {\n" +
            "      \"id\": \"captain\",\n" +
            "      \"source\": \"addressable\",\n" +
            $"      \"address\": \"{AddressableAddress}\",\n" +
            "      \"fallbackSpriteEnemyName\": \"demo\"\n" +
            "    }\n" +
            "  ]";
    }

    private static bool TryReplaceJsonPropertyBlock(string json, string propertyName, string replacementBlock, out string updatedJson)
    {
        updatedJson = json;
        string quotedProperty = $"\"{propertyName}\"";
        int propertyIndex = json.IndexOf(quotedProperty, StringComparison.Ordinal);
        if (propertyIndex < 0)
        {
            return false;
        }

        int colonIndex = json.IndexOf(':', propertyIndex + quotedProperty.Length);
        if (colonIndex < 0)
        {
            return false;
        }

        int valueStart = colonIndex + 1;
        while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
        {
            valueStart++;
        }

        if (valueStart >= json.Length || json[valueStart] != '[')
        {
            return false;
        }

        int valueEnd = FindMatchingBracket(json, valueStart, '[', ']');
        if (valueEnd < 0)
        {
            return false;
        }

        int lineStart = json.LastIndexOf('\n', propertyIndex);
        lineStart = lineStart < 0 ? 0 : lineStart + 1;
        updatedJson = json.Substring(0, lineStart) + replacementBlock + json.Substring(valueEnd + 1);
        return true;
    }

    private static int FindMatchingBracket(string text, int start, char open, char close)
    {
        bool inString = false;
        bool escaping = false;
        int depth = 0;

        for (int i = start; i < text.Length; i++)
        {
            char c = text[i];
            if (inString)
            {
                if (escaping)
                {
                    escaping = false;
                }
                else if (c == '\\')
                {
                    escaping = true;
                }
                else if (c == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (c == '"')
            {
                inString = true;
            }
            else if (c == open)
            {
                depth++;
            }
            else if (c == close)
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
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

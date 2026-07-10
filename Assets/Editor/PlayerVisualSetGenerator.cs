using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// プレイヤー GIF を EnemyVisualSet と同じフレーム列へ展開し、Resources から利用できる Visual Set を生成する。
/// </summary>
public static class PlayerVisualSetGenerator
{
    private const string GifRoot = "Assets/Sprites/Player";
    private const string FrameRoot = "Assets/Sprites/Player/GeneratedFrames";
    private const string VisualSetPath = "Assets/Resources/PlayerVisualSet.asset";
    private const string MaterialPath = "Assets/Resources/PlayerPaletteSwap.mat";
    private const float PixelsPerUnit = 100f;

    [MenuItem("Tools/Bullet Hell/Player Visuals/Generate Player Visual Set")]
    public static void Generate()
    {
        if (!Directory.Exists(GifRoot))
        {
            Debug.LogError($"Player GIF directory not found: {GifRoot}");
            return;
        }

        EnsureCleanDirectory(FrameRoot);

        PlayerVisualSetAsset visualSet = AssetDatabase.LoadAssetAtPath<PlayerVisualSetAsset>(VisualSetPath);
        if (visualSet == null)
        {
            visualSet = ScriptableObject.CreateInstance<PlayerVisualSetAsset>();
            AssetDatabase.CreateAsset(visualSet, VisualSetPath);
        }

        visualSet.neutral = GenerateClip("neutral", "Player_Neutral.gif");
        visualSet.left = GenerateClip("left", "Player_Left.gif");
        visualSet.right = GenerateClip("right", "Player_Right.gif");
        EditorUtility.SetDirty(visualSet);

        CreateOrUpdatePaletteMaterial();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Generated PlayerVisualSet: {VisualSetPath} (neutral={visualSet.neutral.frames.Count}, left={visualSet.left.frames.Count}, right={visualSet.right.frames.Count}).");
    }

    private static PlayerVisualClipAsset GenerateClip(string clipName, string fileName)
    {
        string gifPath = Path.Combine(GifRoot, fileName).Replace('\\', '/');
        if (!File.Exists(gifPath))
        {
            throw new FileNotFoundException($"Player GIF not found: {gifPath}", gifPath);
        }

        GifAnimationData gif = GifAnimationDecoder.Decode(File.ReadAllBytes(gifPath), 0.1f);
        string clipDirectory = Path.Combine(FrameRoot, clipName).Replace('\\', '/');
        Directory.CreateDirectory(clipDirectory);

        PlayerVisualClipAsset clip = new PlayerVisualClipAsset { name = clipName };
        for (int i = 0; i < gif.frames.Count; i++)
        {
            GifFrameData frame = gif.frames[i];
            string framePath = Path.Combine(clipDirectory, $"{clipName}_{i:00}.png").Replace('\\', '/');
            WriteFramePng(framePath, gif.width, gif.height, frame.pixels);
            ImportFrameAsSprite(framePath);

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(framePath);
            if (sprite == null)
            {
                throw new IOException($"Generated player frame could not be loaded as Sprite: {framePath}");
            }

            clip.frames.Add(sprite);
            clip.frameDurations.Add(frame.delaySeconds > 0f ? frame.delaySeconds : 0.1f);
        }

        return clip;
    }

    private static void CreateOrUpdatePaletteMaterial()
    {
        Shader shader = Shader.Find("Custom/PlayerPaletteSwap");
        if (shader == null)
        {
            throw new MissingReferenceException("Custom/PlayerPaletteSwap shader could not be found.");
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        material.shader = shader;
        material.SetColor("_Color1", PlayerPaletteDefaults.Color1Linear);
        material.SetColor("_Color2", PlayerPaletteDefaults.Color2Linear);
        EditorUtility.SetDirty(material);
    }

    private static void WriteFramePng(string framePath, int width, int height, Color32[] topLeftPixels)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels32(EnemyVisualLoader.ConvertTopLeftPixelsToUnityPixels(topLeftPixels, width, height));
        texture.Apply(false, false);
        File.WriteAllBytes(framePath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
    }

    private static void ImportFrameAsSprite(string framePath)
    {
        AssetDatabase.ImportAsset(framePath, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(framePath) as TextureImporter;
        if (importer == null)
        {
            throw new IOException($"Generated player frame importer was not found: {framePath}");
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
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public static class EnemyVisualLoader
{
    public static async Task<EnemyVisualCatalog> LoadCatalogAsync(StageData stageData)
    {
        EnemyVisualCatalog catalog = new EnemyVisualCatalog();
        if (stageData == null || stageData.enemyVisuals == null)
        {
            return catalog;
        }

        for (int i = 0; i < stageData.enemyVisuals.Count; i++)
        {
            EnemyVisualDefinition definition = stageData.enemyVisuals[i];
            if (definition == null || string.IsNullOrWhiteSpace(definition.id))
            {
                continue;
            }

            EnemyVisualSetRuntime visual = null;
            if (definition.IsExternalGif())
            {
                visual = LoadExternalGifVisual(stageData, definition);
            }
            else if (definition.IsAddressable())
            {
                visual = await LoadAddressableVisualAsync(definition, catalog);
            }

            if (visual == null)
            {
                continue;
            }

            if (visual.fallbackSprite == null)
            {
                visual.fallbackSprite = ResolveFallbackSprite(definition);
            }

            catalog.AddVisual(visual);
        }

        return catalog;
    }

    private static async Task<EnemyVisualSetRuntime> LoadAddressableVisualAsync(EnemyVisualDefinition definition, EnemyVisualCatalog catalog)
    {
        if (string.IsNullOrWhiteSpace(definition.address))
        {
            Debug.LogWarning($"Enemy visual '{definition.id}' is addressable but has no address.");
            return null;
        }

        AsyncOperationHandle<EnemyVisualSetAsset> handle = Addressables.LoadAssetAsync<EnemyVisualSetAsset>(definition.address);
        await WaitForAddressable(handle);

        if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
        {
            Debug.LogWarning($"Failed to load enemy visual addressable '{definition.address}' for '{definition.id}'.");
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
            return null;
        }

        catalog.AddAddressableHandle(handle);
        return EnemyVisualSetRuntime.FromAsset(handle.Result, definition.id);
    }

    private static EnemyVisualSetRuntime LoadExternalGifVisual(StageData stageData, EnemyVisualDefinition definition)
    {
        EnemyVisualSetRuntime visual = new EnemyVisualSetRuntime
        {
            id = definition.id,
            fallbackSprite = ResolveFallbackSprite(definition)
        };

        if (definition.clips == null || definition.clips.Count == 0)
        {
            Debug.LogWarning($"External GIF enemy visual '{definition.id}' has no clips.");
            return visual;
        }

        for (int i = 0; i < definition.clips.Count; i++)
        {
            EnemyVisualClipDefinition clipDefinition = definition.clips[i];
            if (clipDefinition == null || string.IsNullOrWhiteSpace(clipDefinition.name) || string.IsNullOrWhiteSpace(clipDefinition.path))
            {
                continue;
            }

            string gifPath = ResolveExternalPath(stageData, definition, clipDefinition.path);
            if (string.IsNullOrWhiteSpace(gifPath) || !File.Exists(gifPath))
            {
                Debug.LogWarning($"Enemy visual GIF not found: {gifPath}");
                continue;
            }

            try
            {
                GifAnimationData gif = GifAnimationDecoder.Decode(File.ReadAllBytes(gifPath), clipDefinition.frameDuration);
                EnemyVisualClipRuntime clip = CreateRuntimeClipFromGif(definition, clipDefinition, gif);
                visual.AddClip(clip);

                for (int frameIndex = 0; frameIndex < clip.frames.Length; frameIndex++)
                {
                    Sprite sprite = clip.frames[frameIndex];
                    if (sprite == null)
                    {
                        continue;
                    }

                    visual.AddOwnedObject(sprite);
                    visual.AddOwnedObject(sprite.texture);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to load enemy visual GIF '{gifPath}'. {ex.Message}");
            }
        }

        return visual;
    }

    private static EnemyVisualClipRuntime CreateRuntimeClipFromGif(
        EnemyVisualDefinition definition,
        EnemyVisualClipDefinition clipDefinition,
        GifAnimationData gif)
    {
        List<Sprite> sprites = new List<Sprite>();
        List<float> durations = new List<float>();
        float pixelsPerUnit = definition.pixelsPerUnit > 0f ? definition.pixelsPerUnit : 100f;
        Vector2 pivot = definition.pivot == Vector2.zero ? new Vector2(0.5f, 0.5f) : definition.pivot;

        int frameCount = gif.frames.Count;
        if (clipDefinition.maxFrames > 0)
        {
            frameCount = Mathf.Min(frameCount, clipDefinition.maxFrames);
        }

        for (int i = 0; i < frameCount; i++)
        {
            GifFrameData frame = gif.frames[i];
            Texture2D texture = new Texture2D(gif.width, gif.height, TextureFormat.RGBA32, false);
            texture.name = $"{definition.id}_{clipDefinition.name}_{i}";
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(ConvertTopLeftPixelsToUnityPixels(
                frame.pixels,
                gif.width,
                gif.height,
                definition.transparentBackground,
                definition.transparentTolerance));
            texture.Apply(false, false);

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, gif.width, gif.height), pivot, pixelsPerUnit);
            sprite.name = texture.name;
            sprites.Add(sprite);
            durations.Add(frame.delaySeconds);
        }

        return new EnemyVisualClipRuntime
        {
            name = clipDefinition.name,
            frames = sprites.ToArray(),
            frameDurations = durations.ToArray(),
            loop = clipDefinition.loop,
            next = clipDefinition.next
        };
    }

    private static Sprite ResolveFallbackSprite(EnemyVisualDefinition definition)
    {
        if (definition == null || string.IsNullOrWhiteSpace(definition.fallbackSpriteEnemyName))
        {
            return null;
        }

        if (GManager.Control == null || GManager.Control.EDB == null)
        {
            return null;
        }

        int id = GManager.Control.EDB.GetEnemyId(definition.fallbackSpriteEnemyName);
        return id >= 0 ? GManager.Control.EDB.GetSprite(id) : null;
    }

    private static string ResolveExternalPath(StageData stageData, EnemyVisualDefinition definition, string clipPath)
    {
        if (string.IsNullOrWhiteSpace(clipPath))
        {
            return null;
        }

        if (Path.IsPathRooted(clipPath))
        {
            return Path.GetFullPath(clipPath);
        }

        string basePath = definition != null ? definition.basePath : "";
        string baseDirectory = ResolveExternalBaseDirectory(stageData, basePath);
        return Path.GetFullPath(Path.Combine(baseDirectory, clipPath));
    }

    private static string ResolveExternalBaseDirectory(StageData stageData, string basePath)
    {
        if (!string.IsNullOrWhiteSpace(basePath) && Path.IsPathRooted(basePath))
        {
            return Path.GetFullPath(basePath);
        }

        string root = null;
        if (stageData != null && !string.IsNullOrWhiteSpace(stageData.baseDirectory))
        {
            root = stageData.baseDirectory;
        }
        else if (stageData != null && !string.IsNullOrWhiteSpace(stageData.stageDirectoryName))
        {
            root = Path.Combine(Application.dataPath, "StageData", stageData.stageDirectoryName);
        }
        else
        {
            root = Application.dataPath;
        }

        return string.IsNullOrWhiteSpace(basePath)
            ? Path.GetFullPath(root)
            : Path.GetFullPath(Path.Combine(root, basePath));
    }

    private static async Task WaitForAddressable<T>(AsyncOperationHandle<T> handle)
    {
        while (!handle.IsDone)
        {
            await Task.Yield();
        }
    }

    public static Color32[] ConvertTopLeftPixelsToUnityPixels(
        Color32[] source,
        int width,
        int height,
        bool transparentBackground = false,
        int transparentTolerance = 0)
    {
        Color32[] converted = new Color32[source.Length];
        Color32 backgroundColor = source.Length > 0 ? source[0] : new Color32(0, 0, 0, 0);
        int tolerance = Mathf.Max(0, transparentTolerance);
        for (int y = 0; y < height; y++)
        {
            int sourceRow = y * width;
            int destRow = (height - 1 - y) * width;
            for (int x = 0; x < width; x++)
            {
                Color32 color = source[sourceRow + x];
                if (transparentBackground && IsKeyColor(color, backgroundColor, tolerance))
                {
                    color.a = 0;
                }
                converted[destRow + x] = color;
            }
        }

        return converted;
    }

    private static bool IsKeyColor(Color32 color, Color32 key, int tolerance)
    {
        if (color.a == 0)
        {
            return true;
        }

        return Mathf.Abs(color.r - key.r) <= tolerance
            && Mathf.Abs(color.g - key.g) <= tolerance
            && Mathf.Abs(color.b - key.b) <= tolerance;
    }
}

using System.Collections.Generic;
using UnityEngine;

public class EnemyVisualSetRuntime
{
    private readonly Dictionary<string, EnemyVisualClipRuntime> clipsByName = new Dictionary<string, EnemyVisualClipRuntime>();
    private readonly List<Object> ownedObjects = new List<Object>();

    public string id;
    public Sprite fallbackSprite;

    public IEnumerable<EnemyVisualClipRuntime> Clips => clipsByName.Values;

    public void AddClip(EnemyVisualClipRuntime clip)
    {
        if (clip == null || string.IsNullOrWhiteSpace(clip.name))
        {
            return;
        }

        clipsByName[clip.name] = clip;
    }

    public bool TryGetClip(string clipName, out EnemyVisualClipRuntime clip)
    {
        clip = null;
        if (string.IsNullOrWhiteSpace(clipName))
        {
            return false;
        }

        return clipsByName.TryGetValue(clipName, out clip);
    }

    public EnemyVisualClipRuntime GetDefaultClip()
    {
        if (TryGetClip("idle", out EnemyVisualClipRuntime idle))
        {
            return idle;
        }

        foreach (EnemyVisualClipRuntime clip in clipsByName.Values)
        {
            return clip;
        }

        return null;
    }

    public void AddOwnedObject(Object ownedObject)
    {
        if (ownedObject != null)
        {
            ownedObjects.Add(ownedObject);
        }
    }

    public void ReleaseOwnedObjects()
    {
        for (int i = 0; i < ownedObjects.Count; i++)
        {
            Object ownedObject = ownedObjects[i];
            if (ownedObject == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(ownedObject);
            }
            else
            {
                Object.DestroyImmediate(ownedObject);
            }
        }

        ownedObjects.Clear();
    }

    public static EnemyVisualSetRuntime FromAsset(EnemyVisualSetAsset asset, string overrideId = null)
    {
        if (asset == null)
        {
            return null;
        }

        EnemyVisualSetRuntime runtime = new EnemyVisualSetRuntime
        {
            id = string.IsNullOrWhiteSpace(overrideId) ? asset.visualId : overrideId,
            fallbackSprite = asset.fallbackSprite
        };

        if (asset.clips == null)
        {
            return runtime;
        }

        for (int i = 0; i < asset.clips.Count; i++)
        {
            EnemyVisualClipAsset source = asset.clips[i];
            if (source == null)
            {
                continue;
            }

            runtime.AddClip(new EnemyVisualClipRuntime
            {
                name = source.name,
                frames = source.frames != null ? source.frames.ToArray() : new Sprite[0],
                frameDurations = source.frameDurations != null ? source.frameDurations.ToArray() : new float[0],
                loop = source.loop,
                next = source.next
            });
        }

        if (string.IsNullOrWhiteSpace(runtime.id))
        {
            runtime.id = asset.name;
        }

        return runtime;
    }
}

public class EnemyVisualClipRuntime
{
    public string name;
    public Sprite[] frames = new Sprite[0];
    public float[] frameDurations = new float[0];
    public bool loop = true;
    public string next = "";

    public float GetFrameDuration(int frameIndex)
    {
        if (frameDurations != null && frameIndex >= 0 && frameIndex < frameDurations.Length && frameDurations[frameIndex] > 0f)
        {
            return frameDurations[frameIndex];
        }

        return 0.1f;
    }
}

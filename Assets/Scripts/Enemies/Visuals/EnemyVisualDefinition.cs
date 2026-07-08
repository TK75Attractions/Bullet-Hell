using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class EnemyVisualDefinition
{
    public string id = "";
    public string source = "";
    public string address = "";
    public string basePath = "";
    public string fallbackSpriteEnemyName = "";
    public float pixelsPerUnit = 100f;
    public Vector2 pivot = new Vector2(0.5f, 0.5f);
    public List<EnemyVisualClipDefinition> clips = new List<EnemyVisualClipDefinition>();

    public bool IsAddressable()
    {
        return string.Equals(source, EnemyVisualSource.Addressable, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(address) && !IsExternalGif());
    }

    public bool IsExternalGif()
    {
        return string.Equals(source, EnemyVisualSource.ExternalGif, StringComparison.OrdinalIgnoreCase);
    }
}

[Serializable]
public class EnemyVisualClipDefinition
{
    public string name = "";
    public string path = "";
    public bool loop;
    public string next = "";
    public float frameDuration = 0.1f;
    // GIF の先頭 maxFrames 枚だけ使う(0=全フレーム)。idle 等の maxFrames:1 で1枚静止に戻す。
    public int maxFrames;
}

public static class EnemyVisualSource
{
    public const string Addressable = "addressable";
    public const string ExternalGif = "externalGif";
}

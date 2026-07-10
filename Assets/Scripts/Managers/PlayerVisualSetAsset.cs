using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerVisualSet", menuName = "Player/PlayerVisualSet")]
public class PlayerVisualSetAsset : ScriptableObject
{
    public PlayerVisualClipAsset neutral = new PlayerVisualClipAsset();
    public PlayerVisualClipAsset left = new PlayerVisualClipAsset();
    public PlayerVisualClipAsset right = new PlayerVisualClipAsset();
}

[Serializable]
public class PlayerVisualClipAsset
{
    public string name = "";
    public List<Sprite> frames = new List<Sprite>();
    public List<float> frameDurations = new List<float>();

    public float GetFrameDuration(int frameIndex)
    {
        if (frameDurations != null
            && frameIndex >= 0
            && frameIndex < frameDurations.Count
            && frameDurations[frameIndex] > 0f)
        {
            return frameDurations[frameIndex];
        }

        return 0.1f;
    }
}

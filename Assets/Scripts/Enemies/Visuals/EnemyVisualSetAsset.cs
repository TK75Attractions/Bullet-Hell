using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EnemyVisualSet", menuName = "Enemy/EnemyVisualSet")]
public class EnemyVisualSetAsset : ScriptableObject
{
    public string visualId = "";
    public Sprite fallbackSprite;
    public List<EnemyVisualClipAsset> clips = new List<EnemyVisualClipAsset>();
}

[Serializable]
public class EnemyVisualClipAsset
{
    public string name = "";
    public List<Sprite> frames = new List<Sprite>();
    public List<float> frameDurations = new List<float>();
    public bool loop = true;
    public string next = "";
}

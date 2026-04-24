using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;
using System;

[Serializable]
public class EnemySpawner
{
    public int id;
    public int count;
    public float interval;
    public float next;
    public int bulletCount;
    public BulletData orbit;
    public BulletClip bulletClip;
    public List<BulletChangeClip> bulletChangeClips = new List<BulletChangeClip>();
}

using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;
using System;

[Serializable]
public struct BulletSpawner
{
    public int index;
    public int count;
    public float interval;
    public float time;
    public float2 pos;
    public float2 originVlc;
    public float angle;
    public float4 color;
    public string clipName;

    [Header("Clip Overrides")]
    [Min(0f)] public float speed;
    [Min(0f)] public float size;

    [Header("Laser Overrides")]
    [Min(0)] public int laserCount;
    [Min(0f)] public float laserSpacing;
    [Min(0f)] public float laserLength;
    [Min(0f)] public float laserWidth;
}

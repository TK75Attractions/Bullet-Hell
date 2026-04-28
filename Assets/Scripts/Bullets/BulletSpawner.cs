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
    public float angle;
    public float4 color;
    public string clipName;
}
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
    public float2 pos;//まじでたくさんつかう
    public float2 originVlc;// あんまつかわないで
    public float angle;
    public float angleInterval;
    public float4 color;
    public string clipName;
}
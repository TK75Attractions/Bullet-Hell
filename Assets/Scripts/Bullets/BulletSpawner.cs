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
    // DEGREES. JSON key must remain "angle" for compatibility, so the field is not
    // renamed; converted to radians at the spawn boundary (see BulletBufferManager.GetBulletClip).
    public float angle;
    // DEGREES per emitted bullet (added to angle for count > 1).
    public float angleInterval;
    public float4 color;
    public string clipName;
}
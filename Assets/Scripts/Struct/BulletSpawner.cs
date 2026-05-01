using Unity.Mathematics;
using System;

[Serializable]
public struct BulletSpawner
{
    public int index { get; set; }
    public int count { get; set; }
    public float interval { get; set; }
    public float time { get; set; }
    public float2 pos { get; set; }
    public float2 originVlc { get; set; }
    public float angle { get; set; }
    public float4 color { get; set; }
    public string clipName { get; set; }
}
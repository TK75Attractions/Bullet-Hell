using Unity.Mathematics;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public struct BulletRenderData
{
    public const float DefaultRenderMode = 0f;
    public const float WarpZoneRenderMode = 1f;
    public const float AttentionRenderMode = 2f;
    public const float CounterBulletRenderMode = 3f;

    public float2 pos;
    public float2 scale;
    public float angle;
    public float texIndex;
    public float maskIndex;
    public float appear;
    public float4 color;
    public int renderPriority;
    public float renderMode;
}

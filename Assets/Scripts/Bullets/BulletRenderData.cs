using Unity.Mathematics;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public struct BulletRenderData
{
    public float2 pos;
    public float2 scale;
    public float angle;
    public float texIndex;
    public float maskIndex;
    public float appear;
    public float4 color;
}

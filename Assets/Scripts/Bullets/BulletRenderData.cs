using Unity.Mathematics;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public struct BulletRenderData
{
    public float2 pos;
    public float angle;
    public float size;
    public float texIndex;
    public float maskIndex;
    public float4 color;
}

using System;
using Unity.Mathematics;

[Serializable]

public struct LASERCell
{
    public float2 vert0;
    public float2 vert1;
    public float2 vert2;

    public LASERCell(float2 v0, float2 v1, float2 v2)
    {
        vert0 = v0;
        vert1 = v1;
        vert2 = v2;
    }
}
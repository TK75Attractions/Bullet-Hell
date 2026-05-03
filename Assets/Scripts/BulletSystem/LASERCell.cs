using System;
using Unity.Mathematics;

namespace BulletHell.Bullets
{
    [Serializable]

    public struct LASERCell
    {
        public float2 vert0 { get; private set; }
        public float2 vert1 { get; private set; }
        public float2 vert2 { get; private set; }

        public LASERCell(float2 v0, float2 v1, float2 v2)
        {
            vert0 = v0;
            vert1 = v1;
            vert2 = v2;
        }
    }
}
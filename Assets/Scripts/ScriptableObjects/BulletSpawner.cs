using Unity.Mathematics;
using System;

namespace BulletHell.Bullets
{
    [Serializable]
    public class BulletSpawner
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
    }
}
using Unity.Mathematics;
using System;

namespace BulletHell.Bullets
{
    public interface IBulletSpawner
    {
        int index { get; }
        int count { get; }
        float interval { get; }
        float time { get; }
        float2 pos { get; }
        float2 originVlc { get; }
        float angle { get; }
        float4 color { get; }
        string clipName { get; }
    }
}
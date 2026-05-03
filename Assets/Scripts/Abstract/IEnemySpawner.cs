using System.Collections.Generic;

using BulletHell.Bullets;

namespace BulletHell.Enemies
{
    public interface IEnemySpawner
    {
        int id { get; }
        int count { get; }
        float interval { get; }
        float time { get; }
        int bulletCount { get; }
        BulletData orbit { get; }
        BulletClip bulletClip { get; }
        List<IBulletChangeClip> bulletChangeClips { get; }
    }
}
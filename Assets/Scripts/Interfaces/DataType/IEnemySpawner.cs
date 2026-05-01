using System.Collections.Generic;

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
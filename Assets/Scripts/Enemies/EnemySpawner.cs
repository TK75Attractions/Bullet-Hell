using System.Collections.Generic;
using System;

[Serializable]
public class EnemySpawner
{
    public int count;
    public float enemyInterval;
    public float enemyAppearTime;
    public float bulletEmitTime;
    public int bulletCount;
    public float bulletInterval;
    public BulletData orbit;
    public BulletClip bulletClip;
    public List<BulletChangeClip> bulletChangeClips = new List<BulletChangeClip>();
}

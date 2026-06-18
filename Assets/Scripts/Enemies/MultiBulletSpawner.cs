using System;
using System.Collections.Generic;

[Serializable]
public class MultiBulletSpawner
{
    public int count;
    public float enemyInterval;
    public float enemyAppearTime;
    public float bulletEmitTime;
    public int bulletCount;
    public float bulletInterval;
    public BulletData orbit;

    public BulletBufferEmission bulletEmission = new BulletBufferEmission();
    public List<BulletBufferEmission> bulletBufferTriggers = new List<BulletBufferEmission>();

    public BulletClip bulletClip;

    public List<BulletChangeClip> bulletChangeClips = new List<BulletChangeClip>();
}

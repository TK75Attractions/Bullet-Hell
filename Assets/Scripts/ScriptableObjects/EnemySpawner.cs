using System.Collections.Generic;
using System;

using BulletHell.Bullets;
using System.Linq;

namespace BulletHell.Enemies
{
    [Serializable]
    public class EnemySpawner : IEnemySpawner
    {

        public int id;
        public int count;
        public float interval;
        public float time;
        public int bulletCount;
        public BulletData orbit;
        public BulletClip bulletClip;
        public List<BulletChangeClip> bulletChangeClips = new List<BulletChangeClip>();

        public int Getid() => id;
        public int Getcount() => count;
        public float GetInterval() => interval;
        public float GetTime() => time;
        public int GetBulletCount() => bulletCount;
        public BulletData GetOrbit() => orbit;
        public BulletClip GetBulletClip() => bulletClip;
        public List<IBulletChangeClip> GetBulletChangeClips() => bulletChangeClips.Cast<IBulletChangeClip>().ToList();

    }
}
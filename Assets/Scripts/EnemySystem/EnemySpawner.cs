using System.Collections.Generic;
using System;

using BulletHell.Bullets;

namespace BulletHell.Enemies
{
    [Serializable]
    public class EnemySpawner : IEnemySpawner
    {
        public int id { get; set; }
        public int count { get; set; }
        public float interval { get; set; }
        public float time { get; set; }
        public int bulletCount { get; set; }
        public BulletData orbit { get; set; }
        public BulletClip bulletClip { get; set; }
        public List<IBulletChangeClip> bulletChangeClips { get; set; } = new List<IBulletChangeClip>();
    }
}
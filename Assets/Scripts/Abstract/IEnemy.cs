using System.Collections.Generic;
using UnityEngine;

using BulletHell.Bullets;

namespace BulletHell.Enemies
{
    public interface IEnemy
    {
        public int id { get; set; }
        public int arrayIndex { get; set; }
        public Transform trans { get; set; }
        public bool isActive { get; set; }

        public BulletClip bulletClip { get; set; }
        public List<IBulletChangeClip> bulletChangeClips { get; set; }

        public float time { get; set; }

        public void Init(int index, IQuadOrder quadOrder, IEnemySpawner spawner, IEnemyDB enemyDB);

        public void UpdateEnemy(float dt);

        public void Destroy();
    }
}

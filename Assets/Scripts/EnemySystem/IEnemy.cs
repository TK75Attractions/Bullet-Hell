using System.Collections.Generic;
using UnityEngine;

namespace BulletHell.Enemies
{
    public interface IEnemy<T>
    {
        public readonly static float startInterval = 1.6f;
        public int id { get; set; }
        public int arrayIndex { get; set; }
        public Transform trans { get; set; }
        public bool isActive { get; set; }

        public BulletClip bulletClip { get; set; }
        public List<IBulletChangeClip> bulletChangeClips { get; set; }

        public float time { get; set; }

        public void Init(int index, IEnemySpawner spawner, T enemyDB);

        public void UpdateEnemy(float dt);

        public void Destroy();
    }
}
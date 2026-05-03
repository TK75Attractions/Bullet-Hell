using UnityEngine;
using System.Collections.Generic;
using BulletHell.Bullets;

namespace BulletHell.Enemies
{
    public class Enemy : MonoBehaviour, IEnemy<IEnemyDB>
    {
        private IQuadBulletStore QOrder;
        private IEnemyDB EDB;
        public readonly static float startInterval = 1.6f;
        public int id { get; set; } = 0;
        public int arrayIndex { get; set; } = 0;
        public Transform trans { get; set; }
        private SpriteRenderer SR = null;
        private float interval = 0;
        private int BulletCount = 0;
        public bool isActive { get; set; } = false;
        private bool isReady = false;
        private int count = 0;

        public BulletClip bulletClip { get; set; } = new BulletClip();
        public List<IBulletChangeClip> bulletChangeClips { get; set; } = new List<IBulletChangeClip>();
        private List<BulletChache> bulletChaches = new List<BulletChache>();

        public float time { get; set; } = 0;

        public void Init(int index, IEnemySpawner spawner, IEnemyDB enemyDB)
        {
            EDB = enemyDB;
            
            id = spawner.id;
            arrayIndex = index;

            interval = spawner.interval;
            BulletCount = spawner.bulletCount;
            bulletClip = spawner.bulletClip;
            bulletChangeClips = spawner.bulletChangeClips;

            trans = transform;
            trans.localScale = new Vector3(spawner.orbit.size, spawner.orbit.size, 1);
            SR = GetComponent<SpriteRenderer>();
            SR.sprite = EDB.GetSprite(spawner.id);

            isActive = true;
        }

        public void UpdateEnemy(float dt)
        {
            time += dt;

            if (isActive)
            {
                Shot();
                UpdateChache(dt);
            }
        }

        private void Shot()
        {
            if (!isReady)
            {
                if (time > startInterval)
                {
                    isReady = true;
                    time = 0;
                }
            }
            else
            {
                if (time > interval)
                {
                    time = 0;
                    if (count < BulletCount)
                    {
                        //修正対象
                        BulletChache chache = new BulletChache(QOrder.EmitEnemyBullet(bulletClip, arrayIndex,new Unity.Mathematics.float2()), bulletChangeClips[0].time, 0);
                        bulletChaches.Add(chache);
                        count++;
                    }
                }
            }
        }

        private void UpdateChache(float dt)
        {
            for (int i = bulletChaches.Count - 1; i >= 0; i--)
            {
                BulletChache chache = bulletChaches[i];
                chache.time -= dt;
                if (chache.time <= 0)
                {
                    //修正対象
                    QOrder.UpdateBulletData(chache.indexes, bulletChangeClips[chache.clipCount].clip,new Unity.Mathematics.float2());
                    bulletChaches.RemoveAt(i);
                }
            }
        }

        public void Destroy()
        {
            Destroy(this.gameObject);
        }
    }
}
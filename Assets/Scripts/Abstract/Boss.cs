using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;
using Unity.Collections;
using System;

using BulletHell.Core.Math;

using BulletHell.Bullets;

namespace BulletHell.Enemies
    {
    public class Boss : MonoBehaviour
    {

        private IQuadOrder QOrder;
        private IBulletPaternProvider BClipManager;
        private PerlinRandom PRandom;

        public int bossId;
        public string bossName;
        public Sprite bossImage;

        [SerializeField] private BossState state;

        public float2 pos;
        public float2 distination;

        private bool ready = false;
        private float time;
        private float move;
        private int count;
        [SerializeField] private int repeat;
        [SerializeField] private List<BulletPatternReference> bulletPatterns = new List<BulletPatternReference>();

        [Serializable]
        private class BulletPatternReference
        {
            public string clipName;
            public float spawnInterval;
            [NonSerialized] public int clipIndex = -1;
        }

        public void Init(IQuadOrder quadOrder, IBulletPaternProvider bClipManager, PerlinRandom pRandom)
        {
            QOrder = quadOrder;
            BClipManager = bClipManager;

            time = 0;
            move = 2.5f;
            count = 0;
            time = 0;
            count = 0;
            ready = false;
            state = BossState.Attack;

            if (bulletPatterns.Count == 0)
            {
                bulletPatterns.Add(new BulletPatternReference { clipName = "Rumia_0", spawnInterval = 0.6f, clipIndex = -1 });
                bulletPatterns.Add(new BulletPatternReference { clipName = "Rumia_1", spawnInterval = 0.6f, clipIndex = -1 });
            }

            for (int i = 0; i < bulletPatterns.Count; i++)
            {
                bulletPatterns[i].clipIndex = -1;
            }
        }

        public void UpdateBoss(float dt)
        {
            time += dt;

            if (ready == false && time > 3)
            {
                ready = true;
                time = 0;
            }
            if (!ready) return;

            //Attack
            if (bulletPatterns.Count == 0) return;

            int patternIndex = count % bulletPatterns.Count;
            BulletPatternReference pattern = bulletPatterns[patternIndex];
            if (time > pattern.spawnInterval && state == BossState.Attack)
            {
                time -= pattern.spawnInterval;

                if (BClipManager == null)
                {
                    Debug.LogError("BulletClipManager is not available.");
                    return;
                }

                if (pattern.clipIndex == -1)
                {
                    if (!BClipManager.TryGetBulletClipIndex(pattern.clipName, out pattern.clipIndex))
                    {
                        Debug.LogError($"Bullet clip not found: {pattern.clipName}");
                        return;
                    }
                }


                List<BulletData> bullets = BClipManager.GetBulletClip(pattern.clipIndex, new float2(0, 0), new float2(0, 0), 0, out bool isLaser);
                NativeArray<BulletData> bulletsArray = new NativeArray<BulletData>(bullets.ToArray(), Allocator.Temp);
                QOrder.AddEnemyHomingBullets(bulletsArray, pos); //修正対象

                bulletsArray.Dispose();
                count++;

                if (count % repeat == 0)
                {
                    state = BossState.Move;
                    double t = PRandom.Noise(count);
                    float2 d = new float2(math.cos((float)t * 2 * math.PI), math.sin((float)t * 2 * math.PI)) * move;
                    distination = pos + d;
                    state = BossState.Move;
                }
                return;
            }

            /*
            if (time < 1 && state == BossState.Move)
                pos = distination;
                time = 0;
                state = BossState.Attack;
            }
            */
        }
    }
}
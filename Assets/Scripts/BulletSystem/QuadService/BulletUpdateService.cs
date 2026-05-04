using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace BulletHell.Bullets
{
    public class BulletUpdateService
    {
        [SerializeField] private List<BulletEvent> bulletEvents = new List<BulletEvent>();

        public void StartBulletEvent(BulletEvent bulletEvent)
        {
            if (bulletEvent == null) return;

            if (bulletEvent.Evoke()) return;
            bulletEvents.Add(bulletEvent);
        }

        public void UpdateBullets(float _dt,
            NativeArray<BulletData> playerBullets,
            NativeArray<BulletData> enemyBullets,
            NativeArray<BulletData> enemiesOrbitBullets,
            int cellLength,
            float cellSize
            )
        {
            for (int i = 0; i < bulletEvents.Count; i++)
            {
                if (bulletEvents[i].Update(_dt))
                {
                    bulletEvents.RemoveAt(i);
                    i--;
                }
            }
            NativeArray<BulletData> playerBulletsArray = playerBullets;
            BulletDataUpdateJob job0 = new()
            {
                bullets = playerBulletsArray,
                dt = _dt,
                cellSize = cellSize,
                totalCellCount = cellLength
            };
            JobHandle handle0 = job0.Schedule(playerBullets.Length, 64);
            handle0.Complete();

            NativeArray<BulletData> EnemyBulletsArray = enemyBullets;
            BulletDataUpdateJob job1 = new()
            {
                bullets = EnemyBulletsArray,
                dt = _dt,
                cellSize = cellSize,
                totalCellCount = cellLength
            };
            JobHandle handle1 = job1.Schedule(EnemyBulletsArray.Length, 64);
            handle1.Complete();

            NativeArray<BulletData> OrbitBulletsArray = enemiesOrbitBullets;
            //Debug.Log($"Updating {bullets.Length} orbit bullets");
            BulletDataUpdateJob job2 = new()
            {
                bullets = OrbitBulletsArray,
                dt = _dt,
                cellSize = cellSize,
                totalCellCount = cellLength
            };
            JobHandle handle2 = job2.Schedule(OrbitBulletsArray.Length, 64);
            handle2.Complete();
        
        // areaNum を使ってセルを再構築
        
        }
    }
}

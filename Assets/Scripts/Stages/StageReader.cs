using UnityEngine;
using System.Threading.Tasks;
using System.Collections.Generic;
using Unity.Mathematics;
using System;

using BulletHell.Audio;
using BulletHell.App;
using BulletHell.Bullets;
using BulletHell.Enemies;


namespace BulletHell.Stages
{
    public class StageReader : MonoBehaviour
    {
        private AudioManager AManager;
        private IEnemyService EController;
        private IQuadOrder QOrder;
        private IBulletPaternProvider bulletPaternProvider;
        private const double BgmLeadTime = 0.2d;
        [SerializeField] private IStageData stageData;
        [SerializeField] private List<BulletSpawnEvent> spawnEvents = new List<BulletSpawnEvent>();
        [SerializeField] private float time = 0f;
        private int enemyCount = 0;
        private int bulletCount = 0;
        private bool isReady = false;

        [Serializable]
        private struct BulletSpawnEvent
        {
            public float time;
            public float2 pos;
            public float angle;
            public int index;
        }

        public void Initialize(AudioManager audioManager, IEnemyService enemyController, IQuadOrder quadOrder, IBulletPaternProvider bulletPaternProvider)
        {
            AManager = audioManager;
            EController = enemyController;
            QOrder = quadOrder;
            this.bulletPaternProvider = bulletPaternProvider;
        }

        public async Task<bool> Init(IStageData data)
        {
            stageData = data;
            time = 0f;
            enemyCount = 0;
            bulletCount = 0;
            if (AManager != null && GManager.Control.BManager != null)
            {
                AudioSource bgmSource = await AManager.PlayBGM(stageData.audioClip);
                double scheduledDspTime = AudioSettings.dspTime + BgmLeadTime;
                bgmSource.PlayScheduled(scheduledDspTime);
                GManager.Control.BManager.SetBeat(stageData.audioClip, stageData.MusicEvents, scheduledDspTime, stageData.delayTime);
                GManager.Control.musicOn = true;
            }

            stageData.enemySpawners.Sort((a, b) => a.time.CompareTo(b.time));

            for (int i = 0; i < stageData.bulletSpawners.Count; i++)
            {
                BulletSpawner spawner = stageData.bulletSpawners[i];

                if (bulletPaternProvider.TryGetBulletClipIndex(spawner.clipName, out int clipIndex))
                {
                    spawner.index = clipIndex;
                    stageData.bulletSpawners[i] = spawner; // Update the spawner with the correct index
                    Debug.Log($"Bullet clip found: {spawner.clipName} at index {clipIndex}");
                }
                else
                {
                    Debug.LogError($"Bullet clip not found: {spawner.clipName}");
                    continue;
                }

                for (int k = 0; k < spawner.count; k++)
                {
                    BulletSpawnEvent spawnEvent = new BulletSpawnEvent
                    {
                        time = spawner.time + k * spawner.interval,
                        pos = spawner.pos,
                        angle = spawner.angle,
                        index = spawner.index
                    };
                    spawnEvents.Add(spawnEvent);
                }
            }
            spawnEvents.Sort((a, b) => a.time.CompareTo(b.time));

            isReady = true;
            return true;
        }

        public void UpdateStage(float dt)
        {
            if (stageData == null || !isReady) return;
            time += dt;

            if (stageData.enemySpawners.Count > enemyCount)
            {
                if (stageData.enemySpawners[enemyCount].time <= time)
                {
                    IEnemySpawner spawner = stageData.enemySpawners[enemyCount];
                    EController.AddEnemy(spawner);
                    Debug.Log($"Spawned enemy: {spawner.orbit.speed}");
                    enemyCount++;
                }
            }

            if (stageData.bulletSpawners.Count > bulletCount)
            {
                if (stageData.bulletSpawners[bulletCount].time <= time)
                {
                    BulletSpawner spawner = stageData.bulletSpawners[bulletCount];
                    QOrder.AddEnemyBullets(spawner);
                    Debug.Log($"Spawned bullet: {spawner.clipName}");
                    bulletCount++;
                }
            }

        }
    }
}
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Threading.Tasks;
using System.Collections.Generic;
using Unity.Mathematics;
using System;

public class StageReader : MonoBehaviour
{
    private const double BgmLeadTime = 0.2d;
    [SerializeField] private StageData stageData;
    [SerializeField] private List<BulletSpawnEvent> spawnEvents = new List<BulletSpawnEvent>();
    [SerializeField] private float time = 0f;
    private int enemyCount = 0;
    private int bulletCount = 0;
    private bool isReady = false;

    [Serializable]
    private struct BulletSpawnEvent
    {
        public float time;
        public BulletSpawner spawner;
    }

    public async Task<bool> Init(StageData data)
    {
        stageData = data;
        time = 0f;
        enemyCount = 0;
        bulletCount = 0;
        spawnEvents.Clear();
        if (GManager.Control.AManager != null && GManager.Control.BManager != null)
        {
            AudioSource bgmSource = await GManager.Control.AManager.PlayBGM(stageData.audioClip);
            double scheduledDspTime = AudioSettings.dspTime + BgmLeadTime;
            bgmSource.PlayScheduled(scheduledDspTime);
            GManager.Control.BManager.SetBeat(stageData.audioClip, stageData.MusicEvents, scheduledDspTime, stageData.delayTime);
            GManager.Control.musicOn = true;
        }

        stageData.enemySpawners.Sort((a, b) => a.time.CompareTo(b.time));

        for (int i = 0; i < stageData.bulletSpawners.Count; i++)
        {
            BulletSpawner spawner = stageData.bulletSpawners[i];

            if (GManager.Control.BClipManager.TryGetBulletClipIndex(spawner.clipName, out int clipIndex))
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
                BulletSpawner eventSpawner = spawner;
                eventSpawner.time = spawner.time + k * spawner.interval;
                BulletSpawnEvent spawnEvent = new BulletSpawnEvent
                {
                    time = eventSpawner.time,
                    spawner = eventSpawner
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
                EnemySpawner spawner = stageData.enemySpawners[enemyCount];
                GManager.Control.QOrder.AddEnemy(spawner);
                Debug.Log($"Spawned enemy: {spawner.orbit.speed}");
                enemyCount++;
            }
        }

        while (spawnEvents.Count > bulletCount)
        {
            if (spawnEvents[bulletCount].time > time) break;

            BulletSpawner spawner = spawnEvents[bulletCount].spawner;
            GManager.Control.QOrder.AddEnemyBullets(spawner);
            Debug.Log($"Spawned bullet: {spawner.clipName}");
            bulletCount++;
        }

    }
}

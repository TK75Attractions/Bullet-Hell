using UnityEditor.SceneManagement;
using UnityEngine;
using System.Threading.Tasks;
using System.Collections.Generic;
using Unity.Mathematics;
using System;

public class StageReader : MonoBehaviour
{
    private const double BgmLeadTime = 2d;
    [SerializeField] private StageData stageData;
    [SerializeField] private List<BulletSpawnEvent> spawnEvents = new List<BulletSpawnEvent>();
    [SerializeField] private float time = 0f;
    [SerializeField] private int enemyCount = 0;
    [SerializeField] private int bulletCount = 0;
    [SerializeField] private bool isReady = false;

    [Serializable]
    private struct BulletSpawnEvent
    {
        public float time;
        public float2 pos;
        public float2 originVlc;
        public float angle;
        public int index;
        public float4 color;
    }

    public async Task<bool> Init(StageData data)
    {
        stageData = data;
        time = 0f;
        enemyCount = 0;
        bulletCount = 0;
        if (GManager.Control.AManager != null && GManager.Control.BManager != null)
        {
            AudioSource bgmSource = await GManager.Control.AManager.PlayBGM(stageData.audioClip);
            if (bgmSource != null && stageData.audioClip != null)
            {
                double scheduledDspTime = AudioSettings.dspTime + BgmLeadTime;
                bgmSource.PlayScheduled(scheduledDspTime);
                GManager.Control.BManager.SetBeat(bgmSource, stageData.audioClip, stageData.MusicEvents, scheduledDspTime, stageData.delayTime);
                GManager.Control.musicOn = true;
            }
            else
            {
                GManager.Control.musicOn = false;
                Debug.LogWarning($"BGM was not started for stage '{stageData.stageName}' because audio clip failed to load.");
            }
        }

        stageData.enemySpawners.Sort((a, b) => a.enemyAppearTime.CompareTo(b.enemyAppearTime));

        for (int i = 0; i < stageData.bulletSpawners.Count; i++)
        {
            BulletSpawner spawner = stageData.bulletSpawners[i];

            if (GManager.Control.BClipManager.TryGetBulletClipIndex(spawner.clipName, out int clipIndex))
            {
                spawner.index = clipIndex;
                stageData.bulletSpawners[i] = spawner; // Update the spawner with the correct index
                Debug.Log($"Bullet clip found: {spawner.clipName} at index {clipIndex}");
            }
            else if (spawner.clipName == "Clear") // "Clear" という名前のクリップは存在しないが、特別な意味を持つと仮定
            {
                spawner.index = -3; // No bullet clip, set index to -3
                stageData.bulletSpawners[i] = spawner; // Update the spawner with the correct index
                Debug.Log($"No bullet clip for spawner at time {spawner.time}, using index -3 for 'Clear'");
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
                    angle = spawner.angle + k * spawner.angleInterval,
                    index = spawner.index,
                    originVlc = spawner.originVlc,
                    color = spawner.color
                };
                Debug.Log($"Scheduled bullet spawn: time={spawnEvent.time}, pos={spawnEvent.pos}, angle={spawnEvent.angle}, index={spawnEvent.index}");
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
            if (stageData.enemySpawners[enemyCount].enemyAppearTime <= time)
            {
                EnemySpawner spawner = stageData.enemySpawners[enemyCount];
                GManager.Control.QOrder.AddEnemy(spawner);
                Debug.Log($"Spawned enemy: {spawner.orbit.speed}");
                enemyCount++;
            }
        }

        if (spawnEvents.Count > bulletCount)
        {
            if (spawnEvents[bulletCount].time <= time)
            {
                BulletSpawnEvent spawner = spawnEvents[bulletCount];
                bulletCount++;
                if (spawner.index == -3)
                {
                    GManager.Control.QOrder.ClearManagedEnemyDanmaku();
                    Debug.Log($"Cleared enemy bullets");
                }

                GManager.Control.QOrder.AddEnemyBullets(spawner.index, spawner.pos, spawner.originVlc, spawner.angle, spawner.color);
                //Debug.Log($"Spawned bullet: {spawner.index}");
            }
        }

    }
}
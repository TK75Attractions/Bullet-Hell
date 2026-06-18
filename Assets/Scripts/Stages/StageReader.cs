using UnityEngine;
using System.Threading.Tasks;
using System.Collections.Generic;
using Unity.Mathematics;
using System;

public class StageReader : MonoBehaviour
{
    private const double BgmLeadTime = 2d;
    private const bool LogStageSchedule = false;
    [SerializeField] private StageData stageData;
    [SerializeField] private List<BulletSpawnEvent> spawnEvents = new List<BulletSpawnEvent>();
    [SerializeField] private float time = 0f;
    [SerializeField] private int multiBulletSpawnerCount = 0;
    [SerializeField] private int bulletCount = 0;
    [SerializeField] private bool isReady = false;
    private EnemyVisualCatalog enemyVisualCatalog;
    private BossManager bossManager;

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
        multiBulletSpawnerCount = 0;
        bulletCount = 0;
        spawnEvents.Clear();
        isReady = false;

        if (GManager.Control.SDB != null)
        {
            await GManager.Control.SDB.EnsureRuntimeMediaLoadedAsync(stageData);
        }

        enemyVisualCatalog?.Release();
        enemyVisualCatalog = await EnemyVisualLoader.LoadCatalogAsync(stageData);
        stageData.enemyVisualCatalog = enemyVisualCatalog;

        bossManager = GetComponent<BossManager>();
        if (bossManager == null)
        {
            bossManager = gameObject.AddComponent<BossManager>();
        }
        bossManager.Init(stageData, this);

        if (GManager.Control.BClipManager != null)
        {
            if (stageData.source == StageData.StageSource.Mod)
            {
                await GManager.Control.BClipManager.ReloadForModStageBulletBuffersAsync(stageData);
            }
            else
            {
                string bulletBufferDirectory = string.IsNullOrWhiteSpace(stageData.stageDirectoryName)
                    ? stageData.stageName
                    : stageData.stageDirectoryName;
                await GManager.Control.BClipManager.ReloadForStageBulletBuffersAsync(bulletBufferDirectory);
            }
        }

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

        stageData.multiBulletSpawners.Sort((a, b) => a.enemyAppearTime.CompareTo(b.enemyAppearTime));
        ResolveMultiBulletSpawnerBulletBuffers();

        for (int i = 0; i < stageData.bulletSpawners.Count; i++)
        {
            BulletSpawner spawner = stageData.bulletSpawners[i];

            if (GManager.Control.BClipManager.TryGetBulletClipIndex(spawner.clipName, out int clipIndex))
            {
                spawner.index = clipIndex;
                stageData.bulletSpawners[i] = spawner; // Update the spawner with the correct index
                if (LogStageSchedule) Debug.Log($"Bullet clip found: {spawner.clipName} at index {clipIndex}");
            }
            else if (spawner.clipName == "Clear") // "Clear" という名前のクリップは存在しないが、特別な意味を持つと仮定
            {
                spawner.index = -3; // No bullet clip, set index to -3
                stageData.bulletSpawners[i] = spawner; // Update the spawner with the correct index
                if (LogStageSchedule) Debug.Log($"No bullet clip for spawner at time {spawner.time}, using index -3 for 'Clear'");
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
                if (LogStageSchedule) Debug.Log($"Scheduled bullet spawn: time={spawnEvent.time}, pos={spawnEvent.pos}, angle={spawnEvent.angle}, index={spawnEvent.index}");
                spawnEvents.Add(spawnEvent);
            }
        }
        spawnEvents.Sort((a, b) => a.time.CompareTo(b.time));

        isReady = true;
        return true;
    }

    private void ResolveMultiBulletSpawnerBulletBuffers()
    {
        if (stageData == null || stageData.multiBulletSpawners == null) return;
        if (GManager.Control.BClipManager == null) return;

        for (int i = 0; i < stageData.multiBulletSpawners.Count; i++)
        {
            MultiBulletSpawner spawner = stageData.multiBulletSpawners[i];
            if (spawner == null) continue;

            if (spawner.bulletEmission == null)
            {
                spawner.bulletEmission = new BulletBufferEmission();
            }

            if (spawner.bulletBufferTriggers == null)
            {
                spawner.bulletBufferTriggers = new List<BulletBufferEmission>();
            }

            ResolveBulletBufferEmission(spawner.bulletEmission, $"multiBulletSpawners[{i}].bulletEmission");

            for (int triggerIndex = 0; triggerIndex < spawner.bulletBufferTriggers.Count; triggerIndex++)
            {
                ResolveBulletBufferEmission(
                    spawner.bulletBufferTriggers[triggerIndex],
                    $"multiBulletSpawners[{i}].bulletBufferTriggers[{triggerIndex}]");
            }
        }
    }

    private void ResolveBulletBufferEmission(BulletBufferEmission emission, string context)
    {
        if (emission == null || string.IsNullOrWhiteSpace(emission.clipName)) return;

        if (GManager.Control.BClipManager.TryGetBulletClipIndex(emission.clipName, out int clipIndex))
        {
            emission.index = clipIndex;
            if (LogStageSchedule) Debug.Log($"Bullet buffer found: {emission.clipName} at index {clipIndex} for {context}");
            return;
        }

        if (string.Equals(emission.clipName, "Clear", StringComparison.Ordinal))
        {
            emission.index = -3;
            return;
        }

        Debug.LogError($"Bullet buffer not found: {emission.clipName} for {context}");
    }

    public EnemyVisualSetRuntime GetEnemyVisual(string visualId)
    {
        return enemyVisualCatalog?.GetVisual(visualId);
    }

    public void UpdateStage(float dt)
    {
        if (stageData == null || !isReady) return;
        time += dt;
        bossManager?.UpdateBosses(dt, time);

        while (stageData.multiBulletSpawners.Count > multiBulletSpawnerCount && stageData.multiBulletSpawners[multiBulletSpawnerCount].enemyAppearTime <= time)
        {
            MultiBulletSpawner spawner = stageData.multiBulletSpawners[multiBulletSpawnerCount];
            GManager.Control.QOrder.AddMultiBullet(spawner);
            if (LogStageSchedule) Debug.Log($"Spawned multi bullet: {spawner.orbit.speed}");
            multiBulletSpawnerCount++;
        }

        while (spawnEvents.Count > bulletCount && spawnEvents[bulletCount].time <= time)
        {
            BulletSpawnEvent spawner = spawnEvents[bulletCount];
            bulletCount++;
            if (spawner.index == -3)
            {
                GManager.Control.QOrder.ClearManagedEnemyDanmaku();
                if (LogStageSchedule) Debug.Log($"Cleared enemy bullets");
            }

            GManager.Control.QOrder.AddEnemyBullets(spawner.index, spawner.pos, spawner.originVlc, spawner.angle, spawner.color);
            //Debug.Log($"Spawned bullet: {spawner.index}");
        }

    }

    private void OnDestroy()
    {
        bossManager?.Clear();
        enemyVisualCatalog?.Release();
        enemyVisualCatalog = null;
    }
}

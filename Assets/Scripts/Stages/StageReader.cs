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
    [SerializeField] private List<StageScheduleExpander.ScheduledSpawn> spawnEvents = new List<StageScheduleExpander.ScheduledSpawn>();
    [SerializeField] private float time = 0f;
    [SerializeField] private int enemyCount = 0;
    [SerializeField] private int bulletCount = 0;
    [SerializeField] private bool isReady = false;
    private bool clearRecorded = false;
    private EnemyVisualCatalog enemyVisualCatalog;
    private AudioSource stageBgmSource;
    private double stageBgmScheduledDspTime = -1d;

    public async Task<bool> Init(StageData data)
    {
        stageData = data;
        LogStageInit($"Init start: {stageData?.stageName}");
        time = 0f;
        enemyCount = 0;
        bulletCount = 0;
        spawnEvents.Clear();
        isReady = false;
        clearRecorded = false;
        stageBgmSource = null;
        stageBgmScheduledDspTime = -1d;

        if (GManager.Control.SDB != null)
        {
            await GManager.Control.SDB.EnsureRuntimeMediaLoadedAsync(stageData);
            LogStageInit("Runtime media loaded");
        }

        enemyVisualCatalog?.Release();
        enemyVisualCatalog = await EnemyVisualLoader.LoadCatalogAsync(stageData);
        stageData.enemyVisualCatalog = enemyVisualCatalog;
        LogStageInit("Enemy visuals loaded");

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
            LogStageInit("Bullet buffers loaded");
        }

        if (GManager.Control.AManager != null && GManager.Control.BManager != null)
        {
            LogStageInit("BGM load start");
            AudioSource bgmSource = await GManager.Control.AManager.PlayBGM(stageData.audioClip);
            if (bgmSource != null && stageData.audioClip != null)
            {
                stageBgmSource = bgmSource;
                ResetStageClockToScheduledStart();
                GManager.Control.musicOn = true;
                LogStageInit("BGM scheduled");
            }
            else
            {
                GManager.Control.musicOn = false;
                Debug.LogWarning($"BGM was not started for stage '{stageData.stageName}' because audio clip failed to load.");
                LogStageInit("BGM failed");
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
                spawner.index = StageScheduleExpander.UnresolvedIndex;
                stageData.bulletSpawners[i] = spawner; // Mark unresolved so expansion skips it.
                continue;
            }
        }

        // Expand resolved spawners into the time-sorted event list. Shared with
        // the golden-master dumper so runtime and tests stay in lockstep.
        StageScheduleExpander.Expand(stageData.bulletSpawners, spawnEvents);
        if (LogStageSchedule)
        {
            for (int e = 0; e < spawnEvents.Count; e++)
            {
                StageScheduleExpander.ScheduledSpawn spawnEvent = spawnEvents[e];
                Debug.Log($"Scheduled bullet spawn: time={spawnEvent.time}, pos={spawnEvent.pos}, angle={spawnEvent.angle}, index={spawnEvent.index}");
            }
        }

        isReady = true;
        LogStageInit("Init ready");
        return true;
    }

    public void ResetStageClockToScheduledStart()
    {
        time = 0f;
        enemyCount = 0;
        bulletCount = 0;

        if (stageBgmSource == null || stageData == null || stageData.audioClip == null)
        {
            stageBgmScheduledDspTime = -1d;
            return;
        }

        double scheduledDspTime = AudioSettings.dspTime + BgmLeadTime;
        stageBgmScheduledDspTime = scheduledDspTime;
        stageBgmSource.Stop();
        stageBgmSource.timeSamples = 0;
        stageBgmSource.time = 0f;
        stageBgmSource.PlayScheduled(scheduledDspTime);
        GManager.Control.BManager.SetBeat(
            stageBgmSource,
            stageData.audioClip,
            stageData.MusicEvents,
            scheduledDspTime,
            stageData.delayTime);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void LogStageInit(string message)
    {
        Debug.Log($"[StageReaderInit] {message}", this);
    }

    public EnemyVisualSetRuntime GetEnemyVisual(string visualId)
    {
        return enemyVisualCatalog?.GetVisual(visualId);
    }

    public void UpdateStage(float dt)
    {
        if (stageData == null || !isReady) return;
        if (GManager.Control == null || GManager.Control.state != GManager.GameState.Playing) return;

        if (stageBgmSource != null && stageData.audioClip != null)
        {
            if (stageBgmScheduledDspTime > 0d)
            {
                double syncedTime = AudioSettings.dspTime - stageBgmScheduledDspTime - stageData.delayTime;
                if (syncedTime < 0d)
                {
                    return;
                }

                time = Mathf.Max(time, (float)syncedTime);
            }
            else
            {
                if (!stageBgmSource.isPlaying && stageBgmSource.timeSamples <= 0)
                {
                    return;
                }

                float syncedTime = stageBgmSource.time - stageData.delayTime;
                if (syncedTime < 0f)
                {
                    return;
                }

                time = Mathf.Max(time, syncedTime);
            }
        }
        else
        {
            time += dt;
        }

        while (stageData.enemySpawners.Count > enemyCount && stageData.enemySpawners[enemyCount].enemyAppearTime <= time)
        {
            EnemySpawner spawner = stageData.enemySpawners[enemyCount];
            GManager.Control.QOrder.AddMultiBullet(spawner);
            if (LogStageSchedule) Debug.Log($"Spawned enemy: {spawner.orbit.speed}");
            enemyCount++;
        }

        while (spawnEvents.Count > bulletCount && spawnEvents[bulletCount].time <= time)
        {
            StageScheduleExpander.ScheduledSpawn spawner = spawnEvents[bulletCount];
            bulletCount++;
            if (spawner.index == -3)
            {
                GManager.Control.QOrder.ClearManagedEnemyDanmaku();
                if (LogStageSchedule) Debug.Log($"Cleared enemy bullets");

                // The "Clear" marker signals stage completion. Record it once
                // per play so a repeat visitor accrues clear counts.
                if (!clearRecorded)
                {
                    clearRecorded = true;
                    string dir = string.IsNullOrWhiteSpace(stageData.stageDirectoryName)
                        ? stageData.stageName
                        : stageData.stageDirectoryName;
                    PlayHistory.RecordClear(dir);
                }
            }

            GManager.Control.QOrder.AddEnemyBullets(spawner.index, spawner.pos, spawner.originVlc, spawner.angle, spawner.color);
            //Debug.Log($"Spawned bullet: {spawner.index}");
        }

    }

    private void OnDestroy()
    {
        enemyVisualCatalog?.Release();
        enemyVisualCatalog = null;
    }
}

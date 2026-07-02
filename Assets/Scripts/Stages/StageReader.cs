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
    // Flattened, time-sorted runtime pattern emissions. Each pattern event is
    // expanded once at Init into individual timed bullets, mirroring how
    // spawnEvents expands bullet spawners. Consumed the same way in UpdateStage.
    private struct PatternBulletEvent
    {
        public float time;
        public BulletData bullet;
    }
    private List<PatternBulletEvent> patternBulletEvents = new List<PatternBulletEvent>();
    private int patternBulletCount = 0;
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
        patternBulletEvents.Clear();
        patternBulletCount = 0;
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
            else if (spawner.clipName == StageScheduleExpander.ClearClipName) // "Clear" という名前のクリップは存在しないが、特別な意味を持つと仮定
            {
                spawner.index = StageScheduleExpander.ClearEventIndex; // No bullet clip, mark as the Clear sentinel
                stageData.bulletSpawners[i] = spawner; // Update the spawner with the correct index
                if (LogStageSchedule) Debug.Log($"No bullet clip for spawner at time {spawner.time}, using Clear sentinel index for 'Clear'");
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

        ExpandPatternEvents();

        isReady = true;
        LogStageInit("Init ready");
        return true;
    }

    /// <summary>
    /// Expands every StagePattern event into a flat, time-sorted list of individual
    /// bullet emissions. Done once at Init (BPM is known), so a chart BPM change
    /// (baked into MusicEvents) re-times beat-domain pattern params. Runtime, not
    /// pre-baked into buffers, so this coexists with the legacy clip path.
    /// </summary>
    private void ExpandPatternEvents()
    {
        patternBulletEvents.Clear();
        patternBulletCount = 0;

        if (stageData == null || stageData.patternEvents == null || stageData.patternEvents.Count == 0)
        {
            return;
        }

        float bpm = 120f;
        if (stageData.MusicEvents != null && stageData.MusicEvents.Count > 0 && stageData.MusicEvents[0].BPM > 0f)
        {
            bpm = stageData.MusicEvents[0].BPM;
        }

        BulletTypeDataBase typeDB = GManager.Control != null ? GManager.Control.BTDB : null;
        PatternContext ctx = new PatternContext
        {
            BeatSeconds = 60f / bpm,
            ResolveTypeId = name => BulletData.ResolveTypeId(name, typeDB)
        };

        List<PatternEmission> emissions = new List<PatternEmission>();
        foreach (PatternEventData ev in stageData.patternEvents)
        {
            if (ev == null || string.IsNullOrEmpty(ev.patternType))
            {
                continue;
            }

            emissions.Clear();
            if (!PatternExecutor.Expand(ev.patternType, ev.args, ctx, emissions))
            {
                Debug.LogWarning($"[StageReader] Unknown pattern type '{ev.patternType}' at t={ev.time}.");
                continue;
            }

            for (int i = 0; i < emissions.Count; i++)
            {
                patternBulletEvents.Add(new PatternBulletEvent
                {
                    time = ev.time + emissions[i].TimeOffset,
                    bullet = emissions[i].Bullet
                });
            }
        }

        patternBulletEvents.Sort((a, b) => a.time.CompareTo(b.time));
        LogStageInit($"Pattern events expanded: {stageData.patternEvents.Count} event(s) -> {patternBulletEvents.Count} bullet(s)");
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
            if (spawner.index == StageScheduleExpander.ClearEventIndex)
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

        while (patternBulletEvents.Count > patternBulletCount && patternBulletEvents[patternBulletCount].time <= time)
        {
            GManager.Control.QOrder.AddPreparedEnemyBullet(patternBulletEvents[patternBulletCount].bullet);
            patternBulletCount++;
        }
    }

    private void OnDestroy()
    {
        enemyVisualCatalog?.Release();
        enemyVisualCatalog = null;
    }
}

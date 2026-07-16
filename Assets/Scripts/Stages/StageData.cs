using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

[System.Serializable]
public class StageData
{
    public enum StageSource
    {
        Official,
        Mod
    }

    public string stageDirectoryName;
    public StageSource source = StageSource.Official;
    public string modId;
    public string baseDirectory;
    public string audioPath;
    public string videoPath;
    public string bulletBufferDirectory;
    public string stageName;
    public VideoClip videoClip;
    public AudioClip audioClip;
    public List<MusicEvent> MusicEvents;

    [System.Serializable]
    public class MusicEvent
    {
        public int barCount;
        public float BPM;
        public List<int> beatTimings;
        public int measure;
        public int barStartOffsetBeats = 0;

        public void Refresh()
        {
            List<int> newBeatTimings = new List<int>();
            for (int i = 0; i < beatTimings.Count; i++)
            {
                if (beatTimings[i] < measure)
                {
                    if (newBeatTimings.Contains(beatTimings[i])) continue;
                    newBeatTimings.Add(beatTimings[i]);
                }
                else continue;
            }
            beatTimings = newBeatTimings;
        }
    }

    public float delayTime;//Delay time before the stage starts, in seconds

    [Min(0f)]
    public float endTime;

    [TextArea]
    public string stageDescription;

    public List<EnemyVisualDefinition> enemyVisuals = new List<EnemyVisualDefinition>();

    public List<StageDifficultyData> difficulties = new List<StageDifficultyData>();

    public List<MultiBulletSpawner> multiBulletSpawners = new List<MultiBulletSpawner>();

    public List<BossSpawner> bossSpawners = new List<BossSpawner>();

    public List<BulletSpawner> bulletSpawners = new List<BulletSpawner>();

    // v2 ステージイベントチャンネル(SPEC-RUNTIME-V2.md P1-c)。省略可・弾データに影響しない。
    public List<StageEventSpawn> stageEvents = new List<StageEventSpawn>();

    [NonSerialized] public DifficultySelection requestedDifficulty;
    [NonSerialized] public DifficultySelection activeDifficulty;
    [NonSerialized] public DifficultySelection resolvedDataDifficulty;

    [System.NonSerialized] public EnemyVisualCatalog enemyVisualCatalog;

    public StageData CreateRuntimeCopy(Difficulty difficulty)
    {
        return CreateRuntimeCopy(DifficultySelection.FromOfficial(difficulty));
    }

    public StageData CreateRuntimeCopy(DifficultySelection difficulty)
    {
        StageDifficultyData selectedDifficulty = ResolveDifficultyData(difficulty);
        DifficultySelection normalizedDifficulty = NormalizeDifficultySelection(difficulty);
        DifficultySelection dataSelection = selectedDifficulty != null
            ? ToDifficultySelection(selectedDifficulty)
            : normalizedDifficulty;

        StageData runtimeData = new StageData
        {
            stageDirectoryName = stageDirectoryName,
            source = source,
            modId = modId,
            baseDirectory = baseDirectory,
            audioPath = audioPath,
            videoPath = videoPath,
            bulletBufferDirectory = bulletBufferDirectory,
            stageName = stageName,
            videoClip = videoClip,
            audioClip = audioClip,
            MusicEvents = CloneMusicEvents(MusicEvents),
            delayTime = delayTime,
            endTime = endTime,
            stageDescription = stageDescription,
            enemyVisuals = CloneEnemyVisuals(enemyVisuals),
            difficulties = CloneDifficultyDataList(difficulties),
            requestedDifficulty = normalizedDifficulty,
            activeDifficulty = normalizedDifficulty,
            resolvedDataDifficulty = dataSelection,
            multiBulletSpawners = selectedDifficulty != null
                ? CloneMultiBulletSpawners(selectedDifficulty.multiBulletSpawners)
                : CloneMultiBulletSpawners(multiBulletSpawners),
            bossSpawners = selectedDifficulty != null
                ? CloneBossSpawners(selectedDifficulty.bossSpawners)
                : CloneBossSpawners(bossSpawners),
            bulletSpawners = selectedDifficulty != null
                ? CloneBulletSpawners(selectedDifficulty.bulletSpawners)
                : CloneBulletSpawners(bulletSpawners),
            stageEvents = CloneStageEvents(stageEvents)
        };

        return runtimeData;
    }

    public void SetActiveDifficultyForPreview(Difficulty difficulty)
    {
        SetActiveDifficultyForPreview(DifficultySelection.FromOfficial(difficulty));
    }

    public void SetActiveDifficultyForPreview(DifficultySelection difficulty)
    {
        StageDifficultyData selectedDifficulty = ResolveDifficultyData(difficulty);
        if (selectedDifficulty == null) return;

        requestedDifficulty = NormalizeDifficultySelection(difficulty);
        activeDifficulty = requestedDifficulty;
        resolvedDataDifficulty = ToDifficultySelection(selectedDifficulty);
        multiBulletSpawners = CloneMultiBulletSpawners(selectedDifficulty.multiBulletSpawners);
        bossSpawners = CloneBossSpawners(selectedDifficulty.bossSpawners);
        bulletSpawners = CloneBulletSpawners(selectedDifficulty.bulletSpawners);
    }

    public StageDifficultyData GetDifficultyData(Difficulty difficulty)
    {
        return GetDifficultyData(DifficultyUtility.GetId(difficulty));
    }

    public StageDifficultyData GetDifficultyData(DifficultySelection difficulty)
    {
        return GetDifficultyData(difficulty.id);
    }

    public StageDifficultyData GetDifficultyData(string difficultyId)
    {
        if (difficulties == null) return null;

        string normalizedId = DifficultyUtility.NormalizeId(difficultyId);
        if (string.IsNullOrWhiteSpace(normalizedId)) return null;

        for (int i = 0; i < difficulties.Count; i++)
        {
            StageDifficultyData difficultyData = difficulties[i];
            if (DifficultyDataMatchesId(difficultyData, normalizedId))
            {
                return difficultyData;
            }
        }

        return null;
    }

    public List<DifficultySelection> GetDifficultySelections()
    {
        List<DifficultySelection> selections = DifficultyUtility.GetOfficialSelections();
        HashSet<string> usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < selections.Count; i++)
        {
            usedIds.Add(selections[i].id);
        }

        if (difficulties == null) return selections;

        for (int i = 0; i < difficulties.Count; i++)
        {
            StageDifficultyData difficultyData = difficulties[i];
            if (difficultyData == null) continue;

            DifficultySelection selection = ToDifficultySelection(difficultyData);
            if (!selection.IsValid() || usedIds.Contains(selection.id)) continue;

            selections.Add(selection);
            usedIds.Add(selection.id);
        }

        return selections;
    }

    public DifficultySelection ResolveDifficultySelection(DifficultySelection difficulty)
    {
        StageDifficultyData selectedDifficulty = ResolveDifficultyData(difficulty);
        return selectedDifficulty != null
            ? ToDifficultySelection(selectedDifficulty)
            : NormalizeDifficultySelection(difficulty);
    }

    private StageDifficultyData ResolveDifficultyData(DifficultySelection difficulty)
    {
        return GetDifficultyData(difficulty)
            ?? GetDifficultyData(Difficulty.Normal)
            ?? GetDifficultyData(Difficulty.Lunatic)
            ?? GetDifficultyData(Difficulty.Easy)
            ?? GetFirstDifficultyData();
    }

    private StageDifficultyData GetFirstDifficultyData()
    {
        if (difficulties == null) return null;

        for (int i = 0; i < difficulties.Count; i++)
        {
            if (difficulties[i] != null)
            {
                return difficulties[i];
            }
        }

        return null;
    }

    private static DifficultySelection NormalizeDifficultySelection(DifficultySelection difficulty)
    {
        return difficulty.IsValid()
            ? DifficultySelection.FromId(difficulty.id, difficulty.displayName)
            : DifficultySelection.FromOfficial(Difficulty.Easy);
    }

    private static DifficultySelection ToDifficultySelection(StageDifficultyData difficultyData)
    {
        if (difficultyData == null) return DifficultySelection.FromOfficial(Difficulty.Easy);

        string difficultyId = GetDifficultyId(difficultyData);
        return DifficultySelection.FromId(difficultyId, difficultyData.displayName);
    }

    private static bool DifficultyDataMatchesId(StageDifficultyData difficultyData, string difficultyId)
    {
        if (difficultyData == null || string.IsNullOrWhiteSpace(difficultyId)) return false;

        return string.Equals(
            GetDifficultyId(difficultyData),
            DifficultyUtility.NormalizeId(difficultyId),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string GetDifficultyId(StageDifficultyData difficultyData)
    {
        if (difficultyData == null) return string.Empty;

        return string.IsNullOrWhiteSpace(difficultyData.difficultyId)
            ? DifficultyUtility.GetId(difficultyData.difficulty)
            : DifficultyUtility.NormalizeId(difficultyData.difficultyId);
    }

    private static List<MusicEvent> CloneMusicEvents(List<MusicEvent> source)
    {
        List<MusicEvent> result = new List<MusicEvent>();
        if (source == null) return result;

        for (int i = 0; i < source.Count; i++)
        {
            MusicEvent musicEvent = source[i];
            if (musicEvent == null) continue;

            result.Add(new MusicEvent
            {
                barCount = musicEvent.barCount,
                BPM = musicEvent.BPM,
                beatTimings = musicEvent.beatTimings != null
                    ? new List<int>(musicEvent.beatTimings)
                    : new List<int>(),
                measure = musicEvent.measure,
                barStartOffsetBeats = musicEvent.barStartOffsetBeats
            });
        }

        return result;
    }

    private static List<EnemyVisualDefinition> CloneEnemyVisuals(List<EnemyVisualDefinition> source)
    {
        List<EnemyVisualDefinition> result = new List<EnemyVisualDefinition>();
        if (source == null) return result;

        for (int i = 0; i < source.Count; i++)
        {
            EnemyVisualDefinition definition = source[i];
            if (definition == null) continue;

            EnemyVisualDefinition clone = new EnemyVisualDefinition
            {
                id = definition.id,
                source = definition.source,
                address = definition.address,
                basePath = definition.basePath,
                fallbackSpriteEnemyName = definition.fallbackSpriteEnemyName,
                pixelsPerUnit = definition.pixelsPerUnit,
                pivot = definition.pivot,
                clips = new List<EnemyVisualClipDefinition>()
            };

            if (definition.clips != null)
            {
                for (int clipIndex = 0; clipIndex < definition.clips.Count; clipIndex++)
                {
                    EnemyVisualClipDefinition clip = definition.clips[clipIndex];
                    if (clip == null) continue;

                    clone.clips.Add(new EnemyVisualClipDefinition
                    {
                        name = clip.name,
                        path = clip.path,
                        loop = clip.loop,
                        next = clip.next,
                        frameDuration = clip.frameDuration
                    });
                }
            }

            result.Add(clone);
        }

        return result;
    }

    private static List<StageDifficultyData> CloneDifficultyDataList(List<StageDifficultyData> source)
    {
        List<StageDifficultyData> result = new List<StageDifficultyData>();
        if (source == null) return result;

        for (int i = 0; i < source.Count; i++)
        {
            StageDifficultyData difficultyData = source[i];
            if (difficultyData == null) continue;

            result.Add(new StageDifficultyData
            {
                difficulty = difficultyData.difficulty,
                difficultyId = GetDifficultyId(difficultyData),
                displayName = difficultyData.displayName,
                multiBulletSpawners = CloneMultiBulletSpawners(difficultyData.multiBulletSpawners),
                bossSpawners = CloneBossSpawners(difficultyData.bossSpawners),
                bulletSpawners = CloneBulletSpawners(difficultyData.bulletSpawners)
            });
        }

        return result;
    }

    public static List<MultiBulletSpawner> CloneMultiBulletSpawners(List<MultiBulletSpawner> source)
    {
        List<MultiBulletSpawner> result = new List<MultiBulletSpawner>();
        if (source == null) return result;

        for (int i = 0; i < source.Count; i++)
        {
            MultiBulletSpawner spawner = source[i];
            if (spawner == null) continue;

            result.Add(new MultiBulletSpawner
            {
                pos = spawner.pos,
                time = spawner.time,
                bulletEmission = CloneBulletBufferEmission(spawner.bulletEmission),
                bulletBufferTriggers = CloneBulletBufferEmissions(spawner.bulletBufferTriggers)
            });
        }

        return result;
    }

    public static List<BossSpawner> CloneBossSpawners(List<BossSpawner> source)
    {
        List<BossSpawner> result = new List<BossSpawner>();
        if (source == null) return result;

        for (int i = 0; i < source.Count; i++)
        {
            BossSpawner spawner = source[i];
            if (spawner == null) continue;

            result.Add(new BossSpawner
            {
                bossId = spawner.bossId,
                bossName = spawner.bossName,
                visualId = spawner.visualId,
                appearTime = spawner.appearTime,
                lifeTime = spawner.lifeTime,
                maxHp = spawner.maxHp,
                startPos = spawner.startPos,
                scale = spawner.scale,
                angle = spawner.angle,
                sortingOrder = spawner.sortingOrder,
                fadeInSec = spawner.fadeInSec,
                fadeOutSec = spawner.fadeOutSec,
                animation = CloneBossAnimationPlan(spawner.animation),
                moves = CloneBossMoveEvents(spawner.moves)
            });
        }

        return result;
    }

    public static List<BulletSpawner> CloneBulletSpawners(List<BulletSpawner> source)
    {
        return source != null ? new List<BulletSpawner>(source) : new List<BulletSpawner>();
    }

    public static List<StageEventSpawn> CloneStageEvents(List<StageEventSpawn> source)
    {
        return source != null ? new List<StageEventSpawn>(source) : new List<StageEventSpawn>();
    }

    private static BulletBufferEmission CloneBulletBufferEmission(BulletBufferEmission source)
    {
        if (source == null) return new BulletBufferEmission();

        return new BulletBufferEmission
        {
            clipName = source.clipName,
            index = source.index,
            time = source.time,
            angleOffset = source.angleOffset,
            applyBulletOrbit = source.applyBulletOrbit,
            inheritSourceAngle = source.inheritSourceAngle,
            inheritSourceVelocity = source.inheritSourceVelocity,
            deactivateSource = source.deactivateSource,
            originVlc = source.originVlc,
            color = source.color
        };
    }

    private static List<BulletBufferEmission> CloneBulletBufferEmissions(List<BulletBufferEmission> source)
    {
        List<BulletBufferEmission> result = new List<BulletBufferEmission>();
        if (source == null) return result;

        for (int i = 0; i < source.Count; i++)
        {
            result.Add(CloneBulletBufferEmission(source[i]));
        }

        return result;
    }

    private static BossAnimationPlan CloneBossAnimationPlan(BossAnimationPlan source)
    {
        BossAnimationPlan normalized = BossAnimationPlan.Normalize(source);
        BossAnimationPlan clone = new BossAnimationPlan
        {
            initialClip = normalized.initialClip,
            events = new List<BossAnimationEventData>(),
            triggers = new List<BossAnimationTriggerData>()
        };

        for (int i = 0; i < normalized.events.Count; i++)
        {
            BossAnimationEventData sourceEvent = normalized.events[i];
            if (sourceEvent == null) continue;

            clone.events.Add(new BossAnimationEventData
            {
                time = sourceEvent.time,
                clip = sourceEvent.clip,
                next = sourceEvent.next,
                overrideLoop = sourceEvent.overrideLoop,
                loop = sourceEvent.loop
            });
        }

        for (int i = 0; i < normalized.triggers.Count; i++)
        {
            BossAnimationTriggerData sourceTrigger = normalized.triggers[i];
            if (sourceTrigger == null) continue;

            clone.triggers.Add(new BossAnimationTriggerData
            {
                trigger = sourceTrigger.trigger,
                clip = sourceTrigger.clip,
                next = sourceTrigger.next,
                overrideLoop = sourceTrigger.overrideLoop,
                loop = sourceTrigger.loop
            });
        }

        return clone;
    }

    private static List<BossMoveEvent> CloneBossMoveEvents(List<BossMoveEvent> source)
    {
        List<BossMoveEvent> result = new List<BossMoveEvent>();
        if (source == null) return result;

        for (int i = 0; i < source.Count; i++)
        {
            BossMoveEvent moveEvent = source[i];
            if (moveEvent == null) continue;

            result.Add(new BossMoveEvent
            {
                time = moveEvent.time,
                duration = moveEvent.duration,
                type = moveEvent.type,
                to = moveEvent.to,
                control = moveEvent.control,
                easing = moveEvent.easing,
                relative = moveEvent.relative
            });
        }

        return result;
    }
}

[Serializable]
public class StageDifficultyData
{
    public Difficulty difficulty = Difficulty.Normal;
    public string difficultyId = "";
    public string displayName = "";
    public List<MultiBulletSpawner> multiBulletSpawners = new List<MultiBulletSpawner>();
    public List<BossSpawner> bossSpawners = new List<BossSpawner>();
    public List<BulletSpawner> bulletSpawners = new List<BulletSpawner>();
}

using UnityEditor;
using UnityEngine;
using System.Reflection;

public static class StoneStageDebugMenu
{
    private const string DumpMenuPath = "Tools/Bullet Hell/Debug/Dump Stone Debug State";
    private const string StartMenuPath = "Tools/Bullet Hell/Debug/Start Stone Stage";
    private const string StageName = "石工";
    private const double DebugBgmLeadTime = 0.25d;
    private const double DebugClockResetTimeoutSeconds = 10d;
    private static StageData pendingDebugClockResetStage;
    private static double pendingDebugClockResetDeadline;

    [MenuItem(DumpMenuPath)]
    private static void DumpStoneDebugState()
    {
        bool hasManager = GManager.Control != null;
        bool hasStageDatabase = hasManager && GManager.Control.SDB != null;
        int stageIndex = hasStageDatabase ? FindStageIndex(StageName) : -1;
        int enemyBulletCount = GManager.Control?.QOrder != null ? GManager.Control.QOrder.GetEnemyBulletCount() : -1;
        int warpZoneCount = GManager.Control?.QOrder != null ? GManager.Control.QOrder.GetWarpZoneCount() : -1;
        int counterBulletCount = GManager.Control?.QOrder != null ? GManager.Control.QOrder.GetCounterBulletCount() : -1;
        Debug.Log(
            $"[StoneDebug] isPlaying={EditorApplication.isPlaying}, " +
            $"isPaused={EditorApplication.isPaused}, " +
            $"GManager={hasManager}, " +
            $"ready={GManager.Control?.ready}, " +
            $"state={GManager.Control?.state}, " +
            $"musicOn={GManager.Control?.musicOn}, " +
            $"gameTime={GManager.Control?.gameTime}, " +
            $"enemyBullets={enemyBulletCount}, " +
            $"warpZones={warpZoneCount}, " +
            $"counterBullets={counterBulletCount}, " +
            $"stageDatabase={hasStageDatabase}, " +
            $"stoneIndex={stageIndex}");

        if (hasStageDatabase)
        {
            Debug.Log($"[StoneDebug] stageCount={GManager.Control.SDB.GetStageCount()}");
        }

        DumpEnemyBulletSamples();
        DumpStageReaderState();
    }

    [MenuItem(StartMenuPath)]
    private static async void StartStoneStage()
    {
        try
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[StoneDebug] Enter Play mode first. This menu no longer enters Play mode automatically.");
                return;
            }

            if (EditorApplication.isPaused)
            {
                EditorApplication.isPaused = false;
            }

            if (GManager.Control == null || !GManager.Control.ready || GManager.Control.SDB == null)
            {
                Debug.LogWarning(
                    $"[StoneDebug] GManager is not ready. " +
                    $"GManager={GManager.Control != null}, " +
                    $"ready={GManager.Control?.ready}, " +
                    $"stageDatabase={GManager.Control?.SDB != null}");
                return;
            }

            int stageIndex = FindStageIndex(StageName);
            if (stageIndex < 0)
            {
                Debug.LogError($"[StoneDebug] stage '{StageName}' was not found.");
                return;
            }

            Time.timeScale = 1f;
            AudioListener.pause = false;
            Debug.Log($"[StoneDebug] Starting stage '{StageName}' at index {stageIndex}.");
            StageData stage = GManager.Control.SDB.GetStage(stageIndex);
            QueueStageClockResetForDebug(stage);
            await GManager.Control.GoGameAsync(stageIndex);
            ResetStageClockForDebug(stage);
            ClearQueuedStageClockReset();
            Debug.Log($"[StoneDebug] Started stage '{StageName}'.");
            HideSelectionCanvases();
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    private static void QueueStageClockResetForDebug(StageData stage)
    {
        pendingDebugClockResetStage = stage;
        pendingDebugClockResetDeadline = EditorApplication.timeSinceStartup + DebugClockResetTimeoutSeconds;
        EditorApplication.update -= TryApplyQueuedStageClockReset;
        EditorApplication.update += TryApplyQueuedStageClockReset;
    }

    private static void TryApplyQueuedStageClockReset()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > pendingDebugClockResetDeadline)
        {
            ClearQueuedStageClockReset();
            return;
        }

        StageReader reader = GManager.Control?.SReader;
        if (reader == null || GManager.Control.state != GManager.GameState.Playing)
        {
            return;
        }

        if (!GetPrivateField<bool>(reader, "isReady"))
        {
            return;
        }

        ResetStageClockForDebug(pendingDebugClockResetStage);
        ClearQueuedStageClockReset();
    }

    private static void ClearQueuedStageClockReset()
    {
        EditorApplication.update -= TryApplyQueuedStageClockReset;
        pendingDebugClockResetStage = null;
        pendingDebugClockResetDeadline = 0d;
    }

    private static int FindStageIndex(string stageName)
    {
        if (GManager.Control == null || GManager.Control.SDB == null) return -1;

        int count = GManager.Control.SDB.GetStageCount();
        for (int i = 0; i < count; i++)
        {
            StageData stage = GManager.Control.SDB.GetStage(i);
            if (stage != null && stage.stageName == stageName) return i;
        }

        return -1;
    }

    private static void HideSelectionCanvases()
    {
        if (GManager.Control == null || GManager.Control.transform.parent == null) return;

        Transform root = GManager.Control.transform.parent;
        SetChildActive(root, "Canvases/StageCanvas", false);
        SetChildActive(root, "Canvases/StaticCanvas", false);
        SetChildActive(root, "Canvases/TutorialUI", false);
        SetChildActive(root, "Canvases/PlayHUD", false);
        SetChildActive(root, "Canvases/OptionScreen", false);
    }

    private static void SetChildActive(Transform root, string path, bool active)
    {
        Transform child = root.Find(path);
        if (child != null) child.gameObject.SetActive(active);
    }

    private static void DumpStageReaderState()
    {
        StageReader reader = GManager.Control?.SReader;
        if (reader == null)
        {
            Debug.Log("[StoneDebug] StageReader=null");
            return;
        }

        float stageTime = GetPrivateField<float>(reader, "time");
        int enemyCount = GetPrivateField<int>(reader, "enemyCount");
        int bulletCount = GetPrivateField<int>(reader, "bulletCount");
        bool isReady = GetPrivateField<bool>(reader, "isReady");
        double scheduledDspTime = GetPrivateField<double>(reader, "stageBgmScheduledDspTime");
        AudioSource bgmSource = GetPrivateField<AudioSource>(reader, "stageBgmSource");
        Debug.Log(
            $"[StoneDebug] StageReader isReady={isReady}, time={stageTime}, " +
            $"enemyCount={enemyCount}, bulletCount={bulletCount}, " +
            $"scheduledDspTime={scheduledDspTime}, " +
            $"bgmPlaying={bgmSource != null && bgmSource.isPlaying}, " +
            $"bgmTime={bgmSource?.time}, " +
            $"bgmSamples={bgmSource?.timeSamples}");
    }

    private static void DumpEnemyBulletSamples()
    {
        if (GManager.Control?.QOrder == null)
        {
            return;
        }

        int logged = 0;
        for (int i = 0; i < 64 && logged < 8; i++)
        {
            try
            {
                BulletData bullet = GManager.Control.QOrder.GetEnemyBulletData(i);
                if (!bullet.isActive)
                {
                    continue;
                }

                Debug.Log(
                    $"[StoneDebug] Bullet[{i}] type={bullet.typeId}, " +
                    $"pos=({bullet.position.x:0.###},{bullet.position.y:0.###}), " +
                    $"scale=({bullet.scale.x:0.###},{bullet.scale.y:0.###}), " +
                    $"time={bullet.time:0.###}, life={bullet.life:0.###}, " +
                    $"color=({bullet.color.x:0.###},{bullet.color.y:0.###},{bullet.color.z:0.###},{bullet.color.w:0.###})");
                logged++;
            }
            catch
            {
                break;
            }
        }

        if (logged == 0)
        {
            Debug.Log("[StoneDebug] No active bullet samples in first 64 slots.");
        }
    }

    private static void ResetStageClockForDebug(StageData stage)
    {
        StageReader reader = GManager.Control?.SReader;
        if (reader == null) return;

        SetPrivateField(reader, "time", 0f);
        SetPrivateField(reader, "enemyCount", 0);
        SetPrivateField(reader, "bulletCount", 0);
        GManager.Control.QOrder?.ClearManagedEnemyDanmaku();

        AudioSource bgmSource = GetPrivateField<AudioSource>(reader, "stageBgmSource");
        if (bgmSource == null || bgmSource.clip == null)
        {
            SetPrivateField(reader, "stageBgmScheduledDspTime", -1d);
            return;
        }

        double scheduledDspTime = AudioSettings.dspTime + DebugBgmLeadTime;
        bgmSource.Stop();
        bgmSource.timeSamples = 0;
        bgmSource.time = 0f;
        bgmSource.PlayScheduled(scheduledDspTime);
        SetPrivateField(reader, "stageBgmScheduledDspTime", scheduledDspTime);
        if (stage != null)
        {
            GManager.Control.BManager.SetBeat(
                bgmSource,
                bgmSource.clip,
                stage.MusicEvents,
                scheduledDspTime,
                stage.delayTime);
        }
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) return default;
        object value = field.GetValue(target);
        return value is T typedValue ? typedValue : default;
    }

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field?.SetValue(target, value);
    }
}

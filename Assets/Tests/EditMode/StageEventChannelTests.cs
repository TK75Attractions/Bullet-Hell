using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// SPEC-RUNTIME-V2.md P1-c (ステージイベントチャンネル) の検証。
/// StageReader.TryDispatchNextStageEvent は GManager 抜きで直接呼べる純粋関数として
/// 切り出してあるので、spawnEvents/bulletCount と同じカーソル走査を単体で確認する。
/// </summary>
public class StageEventChannelTests
{
    [Test]
    public void TryDispatch_ReturnsFalse_WhenScheduleEmpty()
    {
        int cursor = 0;
        bool dispatched = StageReader.TryDispatchNextStageEvent(new List<StageEventSpawn>(), ref cursor, 5f, out _);

        Assert.IsFalse(dispatched);
        Assert.AreEqual(0, cursor);
    }

    [Test]
    public void TryDispatch_ReturnsFalse_WhenNextEventNotYetDue()
    {
        List<StageEventSpawn> schedule = new List<StageEventSpawn>
        {
            new StageEventSpawn { eventName = "wave1", time = 3f }
        };
        int cursor = 0;

        bool dispatched = StageReader.TryDispatchNextStageEvent(schedule, ref cursor, 2.9f, out _);

        Assert.IsFalse(dispatched);
        Assert.AreEqual(0, cursor);
    }

    [Test]
    public void TryDispatch_FiresExactlyOnce_WhenTimeReached()
    {
        List<StageEventSpawn> schedule = new List<StageEventSpawn>
        {
            new StageEventSpawn { eventName = "wave1", time = 3f }
        };
        int cursor = 0;

        bool first = StageReader.TryDispatchNextStageEvent(schedule, ref cursor, 3f, out StageEventSpawn dueEvent);
        Assert.IsTrue(first);
        Assert.AreEqual("wave1", dueEvent.eventName);
        Assert.AreEqual(1, cursor);

        bool second = StageReader.TryDispatchNextStageEvent(schedule, ref cursor, 3f, out _);
        Assert.IsFalse(second);
        Assert.AreEqual(1, cursor);
    }

    [Test]
    public void TryDispatch_DispatchesInScheduleOrder_OneAtATimePerCall()
    {
        List<StageEventSpawn> schedule = new List<StageEventSpawn>
        {
            new StageEventSpawn { eventName = "a", time = 1f },
            new StageEventSpawn { eventName = "b", time = 2f },
            new StageEventSpawn { eventName = "c", time = 2f },
        };
        int cursor = 0;
        List<string> fired = new List<string>();

        // UpdateStage は while ループで「現在時刻までに達した分すべて」を1フレームで消化する。
        while (StageReader.TryDispatchNextStageEvent(schedule, ref cursor, 5f, out StageEventSpawn e))
        {
            fired.Add(e.eventName);
        }

        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, fired);
        Assert.AreEqual(3, cursor);
    }

    [Test]
    public void StageData_CreateRuntimeCopy_PreservesStageEvents()
    {
        StageData source = new StageData
        {
            endTime = 10f,
            stageEvents = new List<StageEventSpawn>
            {
                new StageEventSpawn { eventName = "bossRaiseStaff", time = 4.5f }
            }
        };

        StageData copy = source.CreateRuntimeCopy(Difficulty.Normal);

        Assert.AreEqual(1, copy.stageEvents.Count);
        Assert.AreEqual("bossRaiseStaff", copy.stageEvents[0].eventName);
        Assert.AreEqual(4.5f, copy.stageEvents[0].time);
        // clone であって同一リスト参照ではないこと(片方を変更してももう片方に影響しない)。
        Assert.AreNotSame(source.stageEvents, copy.stageEvents);
    }

    [Test]
    public void StageData_CreateRuntimeCopy_DefaultsToEmptyList_WhenSourceHasNone()
    {
        StageData source = new StageData { endTime = 10f, stageEvents = null };

        StageData copy = source.CreateRuntimeCopy(Difficulty.Normal);

        Assert.IsNotNull(copy.stageEvents);
        Assert.AreEqual(0, copy.stageEvents.Count);
    }
}

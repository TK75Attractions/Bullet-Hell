using System.Collections.Generic;
using Unity.Mathematics;

/// <summary>
/// Pure expansion of resolved bullet spawners into a time-sorted spawn event
/// list. Extracted verbatim from <see cref="StageReader"/>.Init so the runtime
/// and the golden-master dumper share a single implementation.
///
/// Behavior must stay identical to the original inline loop: same per-event
/// arithmetic (<c>time = time + k*interval</c>, <c>angle = angle + k*angleInterval</c>)
/// and the same <see cref="List{T}.Sort(System.Comparison{T})"/> ordering.
/// </summary>
public static class StageScheduleExpander
{
    /// <summary>
    /// Sentinel index written back onto a spawner whose clipName could not be
    /// resolved. Such spawners produce no events (matching the original
    /// <c>continue</c> before the event-building loop).
    /// </summary>
    public const int UnresolvedIndex = int.MinValue;

    [System.Serializable]
    public struct ScheduledSpawn
    {
        public float time;
        public float2 pos;
        public float2 originVlc;
        public float angle;
        public int index;
        public float4 color;
    }

    public static List<ScheduledSpawn> Expand(IReadOnlyList<BulletSpawner> spawners)
    {
        List<ScheduledSpawn> events = new List<ScheduledSpawn>();
        Expand(spawners, events);
        return events;
    }

    /// <summary>
    /// Fills <paramref name="events"/> (cleared first) with the expanded,
    /// time-sorted spawn events for the given resolved spawners.
    /// </summary>
    public static void Expand(IReadOnlyList<BulletSpawner> spawners, List<ScheduledSpawn> events)
    {
        events.Clear();
        if (spawners == null)
        {
            return;
        }

        for (int i = 0; i < spawners.Count; i++)
        {
            BulletSpawner spawner = spawners[i];
            if (spawner.index == UnresolvedIndex)
            {
                continue;
            }

            for (int k = 0; k < spawner.count; k++)
            {
                events.Add(new ScheduledSpawn
                {
                    time = spawner.time + k * spawner.interval,
                    pos = spawner.pos,
                    angle = spawner.angle + k * spawner.angleInterval,
                    index = spawner.index,
                    originVlc = spawner.originVlc,
                    color = spawner.color
                });
            }
        }

        events.Sort((a, b) => a.time.CompareTo(b.time));
    }
}

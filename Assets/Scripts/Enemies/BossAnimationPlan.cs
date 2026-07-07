using System;
using System.Collections.Generic;

[Serializable]
public class BossAnimationPlan
{
    public string initialClip = "idle";
    public List<BossAnimationEventData> events = new List<BossAnimationEventData>();
    public List<BossAnimationTriggerData> triggers = new List<BossAnimationTriggerData>();

    public static BossAnimationPlan Normalize(BossAnimationPlan plan)
    {
        if (plan == null)
        {
            plan = new BossAnimationPlan();
        }

        if (plan.events == null)
        {
            plan.events = new List<BossAnimationEventData>();
        }

        if (plan.triggers == null)
        {
            plan.triggers = new List<BossAnimationTriggerData>();
        }

        if (string.IsNullOrWhiteSpace(plan.initialClip))
        {
            plan.initialClip = "idle";
        }

        return plan;
    }

    public EnemyAnimationPlan ToEnemyAnimationPlan()
    {
        BossAnimationPlan normalized = Normalize(this);
        EnemyAnimationPlan plan = new EnemyAnimationPlan
        {
            initialClip = normalized.initialClip,
            events = new List<EnemyAnimationEventData>(),
            triggers = new List<EnemyAnimationTriggerData>()
        };

        for (int i = 0; i < normalized.events.Count; i++)
        {
            BossAnimationEventData source = normalized.events[i];
            if (source == null) continue;
            plan.events.Add(new EnemyAnimationEventData
            {
                time = source.time,
                clip = source.clip,
                next = source.next,
                overrideLoop = source.overrideLoop,
                loop = source.loop
            });
        }

        for (int i = 0; i < normalized.triggers.Count; i++)
        {
            BossAnimationTriggerData source = normalized.triggers[i];
            if (source == null) continue;
            plan.triggers.Add(new EnemyAnimationTriggerData
            {
                trigger = source.trigger,
                clip = source.clip,
                next = source.next,
                overrideLoop = source.overrideLoop,
                loop = source.loop
            });
        }

        return plan;
    }
}

[Serializable]
public class BossAnimationEventData
{
    public float time;
    public string clip = "";
    public string next = "";
    public bool overrideLoop;
    public bool loop;
}

[Serializable]
public class BossAnimationTriggerData
{
    public string trigger = "";
    public string clip = "";
    public string next = "";
    public bool overrideLoop;
    public bool loop;
}

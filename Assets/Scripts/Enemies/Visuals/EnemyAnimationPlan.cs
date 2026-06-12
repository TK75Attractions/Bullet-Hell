using System;
using System.Collections.Generic;

[Serializable]
public class EnemyAnimationPlan
{
    public string initialClip = "idle";
    public List<EnemyAnimationEventData> events = new List<EnemyAnimationEventData>();
    public List<EnemyAnimationTriggerData> triggers = new List<EnemyAnimationTriggerData>();
}

[Serializable]
public class EnemyAnimationEventData
{
    public float time;
    public string clip = "";
    public string next = "";
    public bool overrideLoop;
    public bool loop;
}

[Serializable]
public class EnemyAnimationTriggerData
{
    public string trigger = "";
    public string clip = "";
    public string next = "";
    public bool overrideLoop;
    public bool loop;
}

public static class EnemyAnimationTriggers
{
    public const string Shot = "shot";
    public const string Death = "death";
}

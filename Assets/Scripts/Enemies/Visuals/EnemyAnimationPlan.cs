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
    // 石工 v34: 「両手を上げて保持 → 少し後で下ろす」用の 2 つ。既定は false/0 なので、
    // これらを書かない既存イベント（艦長の attack 等）の挙動は一切変わらない。
    //   hold + holdFrame … そのクリップを holdFrame 番目のコマ（0 始まり）で止めて待つ。
    //   resume          … 止めていたコマから続きを再生する（clip は空でよい）。
    public bool hold;
    public int holdFrame;
    public bool resume;
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

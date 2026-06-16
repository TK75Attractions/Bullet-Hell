using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BossSpawner
{
    public string bossId = "";
    public string bossName = "";
    public string visualId = "";
    public float appearTime;
    public float lifeTime = -1f;
    public Vector2 startPos;
    public Vector2 scale = Vector2.one;
    public float angle;
    public BossAnimationPlan animation = new BossAnimationPlan();
    public List<BossMoveEvent> moves = new List<BossMoveEvent>();
}

[Serializable]
public class BossMoveEvent
{
    public float time;
    public float duration;
    public BossMoveType type = BossMoveType.MoveTo;
    public Vector2 to;
    public Vector2 control;
    public string easing = "linear";
    public bool relative;
}

public enum BossMoveType
{
    SetPosition,
    MoveTo,
    BezierTo,
    AddVelocity,
    Stop
}

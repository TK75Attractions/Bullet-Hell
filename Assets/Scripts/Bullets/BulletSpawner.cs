using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;
using System;

[Serializable]
public struct BulletSpawner
{
    public int index;
    public int count;
    public float interval;
    public float time;
    public float2 pos;//まじでたくさんつかう
    public float2 originVlc;// あんまつかわないで
    // DEGREES. JSON key must remain "angle" for compatibility, so the field is not
    // renamed; converted to radians at the spawn boundary (see BulletBufferManager.GetBulletClip).
    public float angle;
    // DEGREES per emitted bullet (added to angle for count > 1).
    public float angleInterval;
    public float4 color;
    public string clipName;

    // ---- P5 difficulty modifiers (flat; empty/0 => no modification) ----
    // The chart is authored at full (Lunatic) density; these subtract for lower
    // difficulties at runtime. minDifficulty hides the whole spawner below a
    // threshold; thinEasy/thinNormal decimate the buffer's bullets by index.
    // See DifficultyResolver. Golden dumps ignore these (always full density).
    public string minDifficulty;   // "" / "easy" => all; "normal"; "lunatic"
    public int thinEasy;           // decimation stride on EASY (0 => keep all)
    public int thinNormal;         // decimation stride on NORMAL (0 => keep all)
}
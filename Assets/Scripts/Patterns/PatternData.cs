using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Serializable union of every parameter any built-in pattern reads. A single flat
/// struct keeps <see cref="UnityEngine.JsonUtility"/> happy (no polymorphic JSON)
/// while each <see cref="IBulletPattern"/> interprets only the fields it needs.
///
/// Convention: a field left at its zero/empty value means "use the pattern's
/// natural default" (JsonUtility cannot distinguish an omitted key from an explicit
/// zero, so 0 is treated as unset for fields where 0 is not a meaningful value).
/// Time-domain fields ending in <c>Beats</c> are resolved to seconds at runtime from
/// the stage BPM (see <see cref="PatternContext.BeatSeconds"/>), so a chart BPM
/// change re-times the pattern without recompiling bullet data.
/// </summary>
[Serializable]
public class PatternParamsJson
{
    // ---- shared ----
    public List<Vector2> positions;      // world-space anchor points (block centres, burst origins…)
    public float scale;                  // visual scale (0 => 1)
    public int seed;                     // deterministic RNG seed for tumble etc.
    public Vector4 color;                // primary tint (0,0,0,0 => pattern palette default)
    public Vector4 warnColor;            // warning/ghost tint (0,0,0,0 => palette default)

    // ---- FallingBlock ----
    public float warnBeats;              // ghost warning before the block appears
    public float holdBeats;              // block sits still after appearing, before dropping
    public float fallBeats;              // drop duration; gravity is reverse-solved from this (0 => 1)
    public float landY;                  // landing centre y (0 => ground = 1.4 + scale/2)
    public float untilSec;               // how long the landed block persists (0 => 2)
    public bool dust;                    // emit landing dust
    public bool burst;                   // emit landing burst flash

    // ---- RadialBurst ----
    public int shardCount;               // number of shards (0 => 16)
    public float speed;                  // shard / cutter speed (0 => pattern default)
    public float gravity;                // shard gravity
    public float life;                   // shard / bullet life (0 => pattern default)
    public float tumble;                 // shard spin magnitude (angleSpeed)
    public string shardType;             // bullet type name for shards (empty => stone_shard)
    public bool flash;                   // emit the 3-stage burst flash (RadialBurst)

    // ---- CutterSweep / GhostPreview ----
    public List<CutterDefJson> cutters;  // one entry per cutter blade
    public float dirDeg;                 // GhostPreview travel direction (degrees)
    public float ghostBeats;             // GhostPreview ghost-preview duration
    public string cutterType;            // bullet type name for cutters (empty => stone_cutter)
}

/// <summary>One cutter blade for <c>CutterSweep</c>.</summary>
[Serializable]
public struct CutterDefJson
{
    public Vector2 pos;      // entry position
    public float dirDeg;     // travel direction (degrees)
    public float speed;      // travel speed (0 => 8)
    public float angleSpeed; // spin (0 => 10)
    public float life;       // life after appearing (0 => 4)
    public float ghostBeats; // ghost preview at the entry position (0 => none)
    public float scale;      // overrides params.scale when > 0
}

/// <summary>
/// One StageChart pattern event, deserialized straight from stage.json. Old
/// stages simply omit the <c>patternEvents</c> array, so this is inert for them.
///
/// The difficulty fields are the P5 subtractive modifiers, stored in a flat,
/// JsonUtility-friendly form (the compiler flattens the friendly nested chart
/// syntax into these). All default to "no modification", so an un-annotated event
/// behaves identically on every difficulty. See <see cref="DifficultyResolver"/>.
/// </summary>
[Serializable]
public class PatternEventData
{
    public float time;                 // absolute trigger time (seconds), baked by the compiler
    public string patternType;         // registry key, e.g. "FallingBlock"
    public PatternParamsJson args = new PatternParamsJson();

    // ---- P5 difficulty modifiers (flat; empty/0 => no modification) ----
    public string minDifficulty;       // "" / "easy" => all; "normal"; "lunatic"
    public int thinEasy;               // decimation stride on EASY (0 => keep all)
    public int thinNormal;             // decimation stride on NORMAL (0 => keep all)
    public float scaleEasySpeed;       // EASY speed factor (0 => 1)
    public float scaleEasyCount;       // EASY count factor (0 => 1)
    public float scaleNormalSpeed;     // NORMAL speed factor (0 => 1)
    public float scaleNormalCount;     // NORMAL count factor (0 => 1)
}

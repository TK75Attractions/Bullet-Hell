using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The three selectable difficulties. The integer values match
/// <see cref="GManager.selectedDifficulty"/> (0/1/2) and the difficulty-bar index.
/// </summary>
public enum Difficulty
{
    Easy = 0,
    Normal = 1,
    Lunatic = 2
}

/// <summary>
/// Pure, Unity-independent resolver for the P5 "Lunatic-first" subtractive
/// difficulty flow. Charts are always authored at full (Lunatic) density; lower
/// difficulties are produced at runtime by three deterministic modifiers:
///
/// <list type="bullet">
/// <item><b>minDifficulty</b> — the lowest difficulty at which an event appears.
///   Empty/"easy" means "always" (default). "normal" hides it on EASY, "lunatic"
///   hides it on EASY and NORMAL.</item>
/// <item><b>thin</b> — deterministic, index-based decimation of the bullets a
///   buffer or pattern emits: keep every bullet except every N-th one
///   (<c>index % N == N-1</c> is skipped). N&le;1 keeps everything. So easy:2
///   removes half, normal:3 removes one third.</item>
/// <item><b>diffScale</b> — multiplicative speed/count factors applied to a
///   pattern's arguments before it emits (see <see cref="ApplyScale"/>).</item>
/// </list>
///
/// Everything here is deterministic and side-effect free so it can be pinned by
/// EditMode tests. The golden-master dump is difficulty-independent: it never
/// calls into this class, so it always captures the full Lunatic stream.
/// </summary>
public static class DifficultyResolver
{
    /// <summary>Clamps a raw selected-difficulty integer to a valid enum value.</summary>
    public static Difficulty Clamp(int selected)
    {
        if (selected <= (int)Difficulty.Easy) return Difficulty.Easy;
        if (selected >= (int)Difficulty.Lunatic) return Difficulty.Lunatic;
        return Difficulty.Normal;
    }

    /// <summary>
    /// Parses a minDifficulty token. Null/empty/"easy"/"all" =&gt; Easy (appears
    /// everywhere). "normal" =&gt; Normal, "lunatic" =&gt; Lunatic. Unknown tokens
    /// fail open (Easy) so a typo never silently hides content on every difficulty.
    /// </summary>
    public static Difficulty ParseMinDifficulty(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return Difficulty.Easy;
        switch (token.Trim().ToLowerInvariant())
        {
            case "normal": case "n": return Difficulty.Normal;
            case "lunatic": case "l": return Difficulty.Lunatic;
            case "easy": case "e": case "all": default: return Difficulty.Easy;
        }
    }

    /// <summary>True if an event with the given <paramref name="minDifficulty"/>
    /// should appear at the <paramref name="selected"/> difficulty.</summary>
    public static bool ShouldEmitEvent(string minDifficulty, int selected)
    {
        return (int)Clamp(selected) >= (int)ParseMinDifficulty(minDifficulty);
    }

    /// <summary>
    /// The decimation stride for the selected difficulty. EASY uses
    /// <paramref name="thinEasy"/>, NORMAL uses <paramref name="thinNormal"/>,
    /// LUNATIC never thins (returns 0 = keep all).
    /// </summary>
    public static int ThinForDifficulty(int selected, int thinEasy, int thinNormal)
    {
        switch (Clamp(selected))
        {
            case Difficulty.Easy: return thinEasy;
            case Difficulty.Normal: return thinNormal;
            default: return 0;
        }
    }

    /// <summary>
    /// Deterministic decimation predicate. Returns true if the bullet at
    /// <paramref name="index"/> survives a thin of stride <paramref name="thinN"/>.
    /// Every N-th bullet is dropped (<c>index % N == N-1</c>); N&le;1 keeps all.
    /// </summary>
    public static bool ShouldEmitBullet(int index, int thinN)
    {
        if (thinN <= 1) return true;
        return (index % thinN) != (thinN - 1);
    }

    /// <summary>
    /// Selects a per-difficulty scale factor. EASY uses <paramref name="easyVal"/>,
    /// NORMAL uses <paramref name="normalVal"/>, LUNATIC is always 1. A stored 0
    /// (JsonUtility's "unset") is treated as 1 (no scaling).
    /// </summary>
    public static float SelectScale(int selected, float easyVal, float normalVal)
    {
        float v;
        switch (Clamp(selected))
        {
            case Difficulty.Easy: v = easyVal; break;
            case Difficulty.Normal: v = normalVal; break;
            default: v = 1f; break;
        }
        return v > 0f ? v : 1f;
    }

    /// <summary>
    /// Returns a copy of <paramref name="args"/> with speed/count arguments scaled,
    /// or the original instance when both factors are 1 (no allocation). The count
    /// factor scales the pattern's "how many" argument (RadialBurst.shardCount);
    /// FallingBlock positions and CutterSweep blade counts are structural and are
    /// governed by minDifficulty/thin instead. The speed factor scales the shared
    /// <c>speed</c> and every per-blade cutter speed.
    /// </summary>
    public static PatternParamsJson ApplyScale(PatternParamsJson args, float speedMul, float countMul)
    {
        if (args == null) return null;
        if (speedMul == 1f && countMul == 1f) return args;

        PatternParamsJson clone = Clone(args);
        if (clone.speed != 0f) clone.speed *= speedMul;
        if (countMul != 1f && clone.shardCount > 0)
        {
            clone.shardCount = Mathf.Max(1, Mathf.RoundToInt(clone.shardCount * countMul));
        }
        if (speedMul != 1f && clone.cutters != null)
        {
            for (int i = 0; i < clone.cutters.Count; i++)
            {
                CutterDefJson c = clone.cutters[i];
                if (c.speed != 0f) c.speed *= speedMul;
                clone.cutters[i] = c;
            }
        }
        return clone;
    }

    /// <summary>Field-by-field copy of a params block. Lists that ApplyScale may
    /// mutate (cutters) are copied; positions are shared (never mutated).</summary>
    private static PatternParamsJson Clone(PatternParamsJson a)
    {
        return new PatternParamsJson
        {
            positions = a.positions,
            scale = a.scale,
            seed = a.seed,
            color = a.color,
            warnColor = a.warnColor,
            warnBeats = a.warnBeats,
            holdBeats = a.holdBeats,
            fallBeats = a.fallBeats,
            landY = a.landY,
            untilSec = a.untilSec,
            dust = a.dust,
            burst = a.burst,
            shardCount = a.shardCount,
            speed = a.speed,
            gravity = a.gravity,
            life = a.life,
            tumble = a.tumble,
            shardType = a.shardType,
            flash = a.flash,
            cutters = a.cutters != null ? new List<CutterDefJson>(a.cutters) : null,
            dirDeg = a.dirDeg,
            ghostBeats = a.ghostBeats,
            cutterType = a.cutterType
        };
    }
}

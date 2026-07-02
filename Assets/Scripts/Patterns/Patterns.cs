using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

// Built-in bullet patterns. Each promotes a stone-stage gimmick (design v2) from a
// hand-authored 6-buffer convention into one parameterised generator. All numbers
// come from PatternParamsJson; the literals here are only the documented defaults.

/// <summary>
/// warn (ghost blink) -> optional static hold -> gravity drop (fall time reverse-
/// solved) -> landed block -> optional landing dust + burst flash, per position.
/// </summary>
public sealed class FallingBlockPattern : IBulletPattern
{
    public string TypeName => "FallingBlock";

    public void Emit(PatternParamsJson a, in PatternContext ctx, List<PatternEmission> output)
    {
        if (a.positions == null || a.positions.Count == 0) return;

        float scale = PatternMath.Or(a.scale, 2f);
        float2 s = new float2(scale, scale);
        float bs = ctx.BeatSeconds;
        float warnSec = a.warnBeats * bs;
        float holdSec = a.holdBeats * bs;
        float fallSec = PatternMath.Or(a.fallBeats, 1f) * bs;
        float landY = PatternMath.Or(a.landY, PatternMath.GroundY(scale));
        float untilSec = PatternMath.Or(a.untilSec, 2f);

        float4 blockCol = PatternMath.OrColor(ToF4(a.color), PatternMath.ColorBlock);
        float4 warnCol = PatternMath.OrColor(ToF4(a.warnColor), PatternMath.ColorWarn);

        int blockId = ctx.Resolve("stone_block");
        int warnId = ctx.Resolve("stone_warning");
        int dustId = ctx.Resolve("stone_dust");
        int burstId = ctx.Resolve("stone_burst");

        float tAppear = warnSec;
        float tDrop = warnSec + holdSec;
        float tLand = tDrop + fallSec;

        foreach (Vector2 p in a.positions)
        {
            float2 pos = new float2(p.x, p.y);

            if (warnSec > 0f)
            {
                BulletData warn = PatternMath.Make(warnId, pos, 0f, 0f, 0f, 0f, s * 1.05f, warnCol, warnSec);
                output.Add(new PatternEmission(0f, PatternMath.AsFullBlink(warn)));
            }

            if (holdSec > 0f)
            {
                BulletData hold = PatternMath.Make(blockId, pos, 0f, 0f, 0f, 0f, s, blockCol, holdSec);
                output.Add(new PatternEmission(tAppear, hold));
            }

            float gravity = PatternMath.GravityForDrop(pos.y - landY, fallSec);
            BulletData fall = PatternMath.Make(blockId, pos, 0f, 0f, gravity, 0f, s, blockCol, fallSec);
            output.Add(new PatternEmission(tDrop, fall));

            float2 landPos = new float2(pos.x, landY);
            BulletData landed = PatternMath.Make(blockId, landPos, 0f, 0f, 0f, 0f, s, blockCol, untilSec);
            output.Add(new PatternEmission(tLand, landed));

            if (a.dust)
            {
                PatternHelpers.EmitDust(output, tLand, new float2(pos.x, landY - scale * 0.5f), dustId);
            }
            if (a.burst)
            {
                PatternHelpers.EmitBurstFlash(output, tLand, landPos, burstId, 1f);
            }
        }
    }

    private static float4 ToF4(Vector4 v) => new float4(v.x, v.y, v.z, v.w);
}

/// <summary>n-way shard spray (gravity + tumble) plus a 3-stage expanding burst flash.</summary>
public sealed class RadialBurstPattern : IBulletPattern
{
    public string TypeName => "RadialBurst";

    public void Emit(PatternParamsJson a, in PatternContext ctx, List<PatternEmission> output)
    {
        if (a.positions == null || a.positions.Count == 0) return;

        int n = PatternMath.Or(a.shardCount, 16);
        float speed = PatternMath.Or(a.speed, 7f);
        float gravity = PatternMath.Or(a.gravity, 4f);
        float life = PatternMath.Or(a.life, 2.2f);
        float tumble = PatternMath.Or(a.tumble, 8f);
        float scale = PatternMath.Or(a.scale, 0.9f);
        float2 s = new float2(scale, scale);
        float4 shardCol = PatternMath.OrColor(new float4(a.color.x, a.color.y, a.color.z, a.color.w), PatternMath.ColorShard);

        int shardId = ctx.Resolve(string.IsNullOrEmpty(a.shardType) ? "stone_shard" : a.shardType);
        int burstId = ctx.Resolve("stone_burst");

        foreach (Vector2 p in a.positions)
        {
            float2 pos = new float2(p.x, p.y);
            for (int i = 0; i < n; i++)
            {
                float dir = 2f * math.PI * i / n;
                float spin = tumble * PatternMath.HashSigned(a.seed * 131 + i * 7 + 1);
                BulletData shard = PatternMath.Make(shardId, pos, dir, speed, gravity, spin, s, shardCol, life);
                output.Add(new PatternEmission(0f, shard));
            }

            // The multi-stage flash is intrinsic to RadialBurst (design v2 §Phase C).
            PatternHelpers.EmitBurstFlash(output, 0f, pos, burstId, 1.3f);
        }
    }
}

/// <summary>Straight-travelling cutter blades, each with an optional entry ghost.</summary>
public sealed class CutterSweepPattern : IBulletPattern
{
    public string TypeName => "CutterSweep";

    public void Emit(PatternParamsJson a, in PatternContext ctx, List<PatternEmission> output)
    {
        if (a.cutters == null || a.cutters.Count == 0) return;

        float bs = ctx.BeatSeconds;
        float baseScale = PatternMath.Or(a.scale, 3f);
        int cutterId = ctx.Resolve(string.IsNullOrEmpty(a.cutterType) ? "stone_cutter" : a.cutterType);

        foreach (CutterDefJson c in a.cutters)
        {
            float scale = PatternMath.Or(c.scale, baseScale);
            float2 s = new float2(scale, scale);
            float speed = PatternMath.Or(c.speed, 8f);
            float angleSpeed = PatternMath.Or(c.angleSpeed, 10f);
            float life = PatternMath.Or(c.life, 4f);
            float dir = math.radians(c.dirDeg);
            float2 pos = new float2(c.pos.x, c.pos.y);

            BulletData cutter = PatternMath.Make(
                cutterId, pos, dir, speed, 0f, angleSpeed, s, PatternMath.ColorCutter, life,
                unCounterable: true);
            cutter = PatternMath.WithGhost(cutter, c.ghostBeats * bs);
            output.Add(new PatternEmission(0f, cutter));
        }
    }
}

/// <summary>Standalone beat-synced blink warning (harmless ghost frames).</summary>
public sealed class BeatPulseWarnPattern : IBulletPattern
{
    public string TypeName => "BeatPulseWarn";

    public void Emit(PatternParamsJson a, in PatternContext ctx, List<PatternEmission> output)
    {
        if (a.positions == null || a.positions.Count == 0) return;

        float scale = PatternMath.Or(a.scale, 2.6f);
        float2 s = new float2(scale, scale);
        float life = PatternMath.Or(a.warnBeats, 2f) * ctx.BeatSeconds;
        float4 warnCol = PatternMath.OrColor(new float4(a.warnColor.x, a.warnColor.y, a.warnColor.z, a.warnColor.w), PatternMath.ColorWarn);
        int warnId = ctx.Resolve("stone_warning");

        foreach (Vector2 p in a.positions)
        {
            BulletData warn = PatternMath.Make(warnId, new float2(p.x, p.y), 0f, 0f, 0f, 0f, s, warnCol, life);
            output.Add(new PatternEmission(0f, PatternMath.AsFullBlink(warn)));
        }
    }
}

/// <summary>Demonstrates the ghost-preview modifier: a straight shard that blinks
/// at its start for a couple of beats before launching.</summary>
public sealed class GhostPreviewPattern : IBulletPattern
{
    public string TypeName => "GhostPreview";

    public void Emit(PatternParamsJson a, in PatternContext ctx, List<PatternEmission> output)
    {
        if (a.positions == null || a.positions.Count == 0) return;

        float scale = PatternMath.Or(a.scale, 0.9f);
        float2 s = new float2(scale, scale);
        float speed = PatternMath.Or(a.speed, 6f);
        float life = PatternMath.Or(a.life, 3f);
        float ghost = PatternMath.Or(a.ghostBeats, 2f) * ctx.BeatSeconds;
        float dir = math.radians(a.dirDeg);
        float tumble = a.tumble;
        int shardId = ctx.Resolve(string.IsNullOrEmpty(a.shardType) ? "stone_shard" : a.shardType);

        foreach (Vector2 p in a.positions)
        {
            BulletData b = PatternMath.Make(shardId, new float2(p.x, p.y), dir, speed, 0f, tumble, s, PatternMath.ColorShard, life);
            output.Add(new PatternEmission(0f, PatternMath.WithGhost(b, ghost)));
        }
    }
}

/// <summary>Shared sub-emitters used by more than one pattern.</summary>
internal static class PatternHelpers
{
    /// <summary>Three harmless dust shards (150/90/30 deg fan) at a landing point.</summary>
    public static void EmitDust(List<PatternEmission> output, float offset, float2 pos, int dustId)
    {
        float[] dirsDeg = { 150f, 90f, 30f };
        for (int i = 0; i < dirsDeg.Length; i++)
        {
            float dir = math.radians(dirsDeg[i]);
            BulletData dust = PatternMath.Make(dustId, pos, dir, 3f, 10f, 0f, new float2(0.35f, 0.35f), PatternMath.ColorDust, 0.35f);
            output.Add(new PatternEmission(offset, dust));
        }
    }

    /// <summary>Three-stage expanding burst flash (scales 0.7/1.0/1.3, staggered
    /// appearTime) that reads as a growing ring.</summary>
    public static void EmitBurstFlash(List<PatternEmission> output, float offset, float2 pos, int burstId, float sizeMul)
    {
        float[] scales = { 0.7f, 1.0f, 1.3f };
        for (int i = 0; i < scales.Length; i++)
        {
            float sc = scales[i] * sizeMul;
            float appear = i * 0.05f;
            BulletData flash = PatternMath.Make(
                burstId, pos, 0f, 0f, 0f, 0f, new float2(sc, sc), PatternMath.ColorBurst, 0.2f + appear,
                appearTime: appear, appearDuration: 0f);
            output.Add(new PatternEmission(offset, flash));
        }
    }
}

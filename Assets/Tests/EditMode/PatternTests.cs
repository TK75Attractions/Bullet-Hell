using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Pure tests for the P3 pattern library: gravity/ground math, deterministic
/// expansion, and the FallingBlock event timeline. No Unity runtime — patterns
/// receive a stub type resolver so BulletData is produced without the asset DB.
/// </summary>
public class PatternTests
{
    // Stub resolver: distinct positive ids per stone type so emissions are
    // distinguishable and never negative (which the executor would drop).
    private static int StubResolve(string name)
    {
        switch (name)
        {
            case "stone_warning": return 1;
            case "stone_block": return 2;
            case "stone_dust": return 3;
            case "stone_burst": return 4;
            case "stone_shard": return 5;
            case "stone_cutter": return 6;
            default: return 7;
        }
    }

    private static PatternContext Ctx(float bpm = 120f) => new PatternContext
    {
        BeatSeconds = 60f / bpm,
        ResolveTypeId = StubResolve
    };

    // ---- PatternMath ----

    [Test]
    public void GravityForDrop_ReverseSolvesFallTime()
    {
        // g such that a body falls 11.1 units in exactly 1.0s from rest: g = 2*Δy/t².
        float g = PatternMath.GravityForDrop(11.1f, 1.0f);
        Assert.AreEqual(22.2f, g, 1e-4f);
        // Sanity: h(t)=g t²/2 reproduces the drop distance.
        Assert.AreEqual(11.1f, g * 1.0f * 1.0f / 2f, 1e-3f);
    }

    [Test]
    public void GroundY_UsesHalfScaleOffset()
    {
        Assert.AreEqual(2.9f, PatternMath.GroundY(3f), 1e-5f); // 1.4 + 3/2
    }

    // ---- FallingBlock timeline ----

    private static List<PatternEmission> ExpandFallingBlock(bool dust, bool burst)
    {
        var args = new PatternParamsJson
        {
            positions = new List<Vector2> { new Vector2(10f, 14f) },
            scale = 3f,
            warnBeats = 2f,
            holdBeats = 1f,
            fallBeats = 2f,
            untilSec = 3f,
            dust = dust,
            burst = burst
        };
        var output = new List<PatternEmission>();
        Assert.IsTrue(PatternExecutor.Expand("FallingBlock", args, Ctx(), output));
        return output;
    }

    [Test]
    public void FallingBlock_TimelineOffsetsAreOrdered()
    {
        // bpm 120 => beat 0.5s. warn=1.0, hold=0.5, fall=1.0.
        List<PatternEmission> output = ExpandFallingBlock(dust: false, burst: false);

        // Dashed-frame warning: many dash bullets (typeId 1) at offset 0, then the
        // hold -> fall -> land blocks (typeId 2) in order.
        List<PatternEmission> warns = output.FindAll(e => e.Bullet.typeId == 1);
        List<PatternEmission> blocks = output.FindAll(e => e.Bullet.typeId == 2);

        Assert.Greater(warns.Count, 4, "warning is a dotted frame of many dashes");
        foreach (PatternEmission w in warns)
            Assert.AreEqual(0f, w.TimeOffset, 1e-5f, "dashes appear immediately");

        Assert.AreEqual(3, blocks.Count, "hold + fall + land");
        Assert.AreEqual(1.0f, blocks[0].TimeOffset, 1e-5f); // hold: warnBeats*beat
        Assert.AreEqual(1.5f, blocks[1].TimeOffset, 1e-5f); // fall: (warn+hold)*beat
        Assert.AreEqual(2.5f, blocks[2].TimeOffset, 1e-5f); // land: (warn+hold+fall)*beat
    }

    [Test]
    public void FallingBlock_FallingBulletUsesReverseSolvedGravity_AndLandsAtGround()
    {
        List<PatternEmission> output = ExpandFallingBlock(dust: false, burst: false);
        List<PatternEmission> blocks = output.FindAll(e => e.Bullet.typeId == 2);
        PatternEmission fall = blocks[1];

        float fallSec = 1.0f;               // 2 beats @ 120 bpm
        float landY = PatternMath.GroundY(3f); // 2.9
        float expectedG = PatternMath.GravityForDrop(14f - landY, fallSec);

        Assert.AreEqual(expectedG, fall.Bullet.gravity, 1e-3f);
        Assert.AreEqual(fallSec, fall.Bullet.life, 1e-5f);

        // Landed block sits exactly on the ground line.
        PatternEmission landed = blocks[2];
        Assert.AreEqual(landY, landed.Bullet.originPos.y, 1e-4f);
        Assert.AreEqual(0f, landed.Bullet.gravity, 1e-5f);
        Assert.AreEqual(3f, landed.Bullet.life, 1e-5f); // untilSec
    }

    [Test]
    public void FallingBlock_DustAndBurstAddNineExtraEmissionsAtLanding()
    {
        List<PatternEmission> plain = ExpandFallingBlock(dust: false, burst: false);
        List<PatternEmission> full = ExpandFallingBlock(dust: true, burst: true);

        // +3 dust, +3 burst per block.
        Assert.AreEqual(plain.Count + 6, full.Count);

        int dustCount = 0, burstCount = 0;
        foreach (PatternEmission e in full)
        {
            if (e.Bullet.typeId == 3) { dustCount++; Assert.AreEqual(2.5f, e.TimeOffset, 1e-5f); }
            if (e.Bullet.typeId == 4) { burstCount++; Assert.AreEqual(2.5f, e.TimeOffset, 1e-5f); }
        }
        Assert.AreEqual(3, dustCount);
        Assert.AreEqual(3, burstCount);
    }

    [Test]
    public void FallingBlock_WarningIsFullBlinkGhost()
    {
        List<PatternEmission> output = ExpandFallingBlock(dust: false, burst: false);
        BulletData warn = output[0].Bullet;
        // Full-window beat blink: appearTime == appearDuration == life.
        Assert.Greater(warn.life, 0f);
        Assert.AreEqual(warn.life, warn.appearTime, 1e-5f);
        Assert.AreEqual(warn.life, warn.appearDuration, 1e-5f);
    }

    // ---- RadialBurst determinism + executor guards ----

    [Test]
    public void RadialBurst_IsDeterministicForSameSeed()
    {
        var args = new PatternParamsJson
        {
            positions = new List<Vector2> { new Vector2(16f, 9f) },
            shardCount = 12,
            seed = 999
        };

        List<PatternEmission> a = Shards(args);
        List<PatternEmission> b = Shards(args);

        Assert.AreEqual(12, a.Count, "12 shards");
        Assert.AreEqual(a.Count, b.Count);
        for (int i = 0; i < a.Count; i++)
        {
            Assert.AreEqual(a[i].Bullet.angleSpeed, b[i].Bullet.angleSpeed, 1e-6f, $"shard {i} tumble");
            Assert.AreEqual(a[i].Bullet.polarForm.y, b[i].Bullet.polarForm.y, 1e-6f, $"shard {i} direction");
        }
    }

    // Shard emissions only (typeId 5), excluding the intrinsic burst flash.
    private static List<PatternEmission> Shards(PatternParamsJson args)
    {
        var all = new List<PatternEmission>();
        PatternExecutor.Expand("RadialBurst", args, Ctx(), all);
        var shards = new List<PatternEmission>();
        foreach (PatternEmission e in all)
        {
            if (e.Bullet.typeId == 5) shards.Add(e);
        }
        return shards;
    }

    [Test]
    public void Executor_DropsBulletsWithUnresolvedType()
    {
        var ctx = new PatternContext { BeatSeconds = 0.5f, ResolveTypeId = _ => -1 };
        var args = new PatternParamsJson { positions = new List<Vector2> { new Vector2(8f, 8f) } };
        var output = new List<PatternEmission>();
        Assert.IsTrue(PatternExecutor.Expand("BeatPulseWarn", args, ctx, output));
        Assert.AreEqual(0, output.Count, "emissions with typeId < 0 must be dropped");
    }

    [Test]
    public void Executor_ReturnsFalseForUnknownPattern()
    {
        var output = new List<PatternEmission>();
        Assert.IsFalse(PatternExecutor.Expand("NoSuchPattern", new PatternParamsJson(), Ctx(), output));
    }

    [Test]
    public void CutterSweep_AppliesEntryGhostAndUncounterable()
    {
        var args = new PatternParamsJson
        {
            scale = 3f,
            cutters = new List<CutterDefJson>
            {
                new CutterDefJson { pos = new Vector2(0.3f, 12f), dirDeg = 0f, speed = 8f, angleSpeed = 10f, life = 4f, ghostBeats = 2f }
            }
        };
        var output = new List<PatternEmission>();
        Assert.IsTrue(PatternExecutor.Expand("CutterSweep", args, Ctx(), output));
        Assert.AreEqual(1, output.Count);

        BulletData c = output[0].Bullet;
        Assert.AreEqual(6, c.typeId, "stone_cutter");
        Assert.IsTrue(c.unCounterable);
        Assert.AreEqual(1.0f, c.appearTime, 1e-5f);      // ghostBeats(2) * beat(0.5)
        Assert.AreEqual(1.0f, c.appearDuration, 1e-5f);
        Assert.AreEqual(5.0f, c.life, 1e-5f);            // life(4) + ghost(1)
    }
}

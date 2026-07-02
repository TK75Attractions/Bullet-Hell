using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Pins the P5 subtractive-difficulty rules: minDifficulty gating, deterministic
/// index-based thinning, per-difficulty scaling, and the "no modifiers =&gt; full
/// Lunatic stream" invariant that keeps old stages behaving identically.
/// </summary>
public class DifficultyResolverTests
{
    private const int Easy = 0;
    private const int Normal = 1;
    private const int Lunatic = 2;

    // ---- minDifficulty gating (boundaries) ----

    [Test]
    public void ShouldEmitEvent_MinNormal_HiddenOnEasyOnly()
    {
        Assert.IsFalse(DifficultyResolver.ShouldEmitEvent("normal", Easy));
        Assert.IsTrue(DifficultyResolver.ShouldEmitEvent("normal", Normal));
        Assert.IsTrue(DifficultyResolver.ShouldEmitEvent("normal", Lunatic));
    }

    [Test]
    public void ShouldEmitEvent_MinLunatic_OnlyOnLunatic()
    {
        Assert.IsFalse(DifficultyResolver.ShouldEmitEvent("lunatic", Easy));
        Assert.IsFalse(DifficultyResolver.ShouldEmitEvent("lunatic", Normal));
        Assert.IsTrue(DifficultyResolver.ShouldEmitEvent("lunatic", Lunatic));
    }

    [Test]
    public void ShouldEmitEvent_EmptyOrEasy_AlwaysShown()
    {
        foreach (int sel in new[] { Easy, Normal, Lunatic })
        {
            Assert.IsTrue(DifficultyResolver.ShouldEmitEvent(null, sel), $"null@{sel}");
            Assert.IsTrue(DifficultyResolver.ShouldEmitEvent("", sel), $"empty@{sel}");
            Assert.IsTrue(DifficultyResolver.ShouldEmitEvent("easy", sel), $"easy@{sel}");
        }
        // Unknown tokens fail open (treated as easy => always shown).
        Assert.IsTrue(DifficultyResolver.ShouldEmitEvent("banana", Easy));
    }

    // ---- deterministic thinning ----

    [Test]
    public void ShouldEmitBullet_IsDeterministicAndDecimatesByStride()
    {
        // stride 2 removes every 2nd bullet (index 1,3,5...) => half survive.
        int kept = 0;
        for (int i = 0; i < 10; i++) if (DifficultyResolver.ShouldEmitBullet(i, 2)) kept++;
        Assert.AreEqual(5, kept, "easy:2 keeps half of 10");
        Assert.IsTrue(DifficultyResolver.ShouldEmitBullet(0, 2));
        Assert.IsFalse(DifficultyResolver.ShouldEmitBullet(1, 2));

        // stride 3 removes every 3rd (index 2,5,8) => 7 of 10 survive.
        int kept3 = 0;
        for (int i = 0; i < 9; i++) if (DifficultyResolver.ShouldEmitBullet(i, 3)) kept3++;
        Assert.AreEqual(6, kept3, "normal:3 removes one third of 9");
        Assert.IsFalse(DifficultyResolver.ShouldEmitBullet(2, 3));

        // Determinism: same index+stride always agree.
        for (int i = 0; i < 100; i++)
        {
            Assert.AreEqual(DifficultyResolver.ShouldEmitBullet(i, 4), DifficultyResolver.ShouldEmitBullet(i, 4));
        }
    }

    [Test]
    public void ShouldEmitBullet_StrideZeroOrOne_KeepsEverything()
    {
        for (int i = 0; i < 20; i++)
        {
            Assert.IsTrue(DifficultyResolver.ShouldEmitBullet(i, 0), "stride 0 keeps all");
            Assert.IsTrue(DifficultyResolver.ShouldEmitBullet(i, 1), "stride 1 keeps all");
        }
    }

    [Test]
    public void ThinForDifficulty_PicksPerDifficultyStride_LunaticNeverThins()
    {
        Assert.AreEqual(2, DifficultyResolver.ThinForDifficulty(Easy, 2, 3));
        Assert.AreEqual(3, DifficultyResolver.ThinForDifficulty(Normal, 2, 3));
        Assert.AreEqual(0, DifficultyResolver.ThinForDifficulty(Lunatic, 2, 3), "Lunatic keeps the full stream");
    }

    // ---- scaling ----

    [Test]
    public void SelectScale_PicksPerDifficulty_TreatsZeroAsOne()
    {
        Assert.AreEqual(0.8f, DifficultyResolver.SelectScale(Easy, 0.8f, 0.9f), 1e-6f);
        Assert.AreEqual(0.9f, DifficultyResolver.SelectScale(Normal, 0.8f, 0.9f), 1e-6f);
        Assert.AreEqual(1f, DifficultyResolver.SelectScale(Lunatic, 0.8f, 0.9f), 1e-6f);
        Assert.AreEqual(1f, DifficultyResolver.SelectScale(Easy, 0f, 0.9f), 1e-6f, "0 => no scaling");
    }

    [Test]
    public void ApplyScale_ScalesSpeedAndCount_AndClonesCutters()
    {
        var args = new PatternParamsJson
        {
            speed = 8f,
            shardCount = 20,
            cutters = new List<CutterDefJson> { new CutterDefJson { speed = 10f } }
        };

        PatternParamsJson scaled = DifficultyResolver.ApplyScale(args, 0.5f, 0.5f);

        Assert.AreNotSame(args, scaled, "scaling clones the params");
        Assert.AreEqual(4f, scaled.speed, 1e-6f);
        Assert.AreEqual(10, scaled.shardCount, "20 * 0.5 rounded");
        Assert.AreEqual(5f, scaled.cutters[0].speed, 1e-6f);

        // Original is untouched.
        Assert.AreEqual(8f, args.speed, 1e-6f);
        Assert.AreEqual(20, args.shardCount);
        Assert.AreEqual(10f, args.cutters[0].speed, 1e-6f);
    }

    [Test]
    public void ApplyScale_ShardCountFloorsToOne()
    {
        var args = new PatternParamsJson { shardCount = 3 };
        PatternParamsJson scaled = DifficultyResolver.ApplyScale(args, 1f, 0.1f);
        Assert.AreEqual(1, scaled.shardCount, "count never scales below 1 while > 0");
    }

    // ---- no-modifier invariant (old stages behave identically) ----

    [Test]
    public void NoModifiers_EmitEverywhere_FullDensity_NoScaleAllocation()
    {
        // An un-annotated spawner: empty minDifficulty, zero thin strides.
        foreach (int sel in new[] { Easy, Normal, Lunatic })
        {
            Assert.IsTrue(DifficultyResolver.ShouldEmitEvent(null, sel));
            Assert.AreEqual(0, DifficultyResolver.ThinForDifficulty(sel, 0, 0));
        }

        // No scaling => the very same instance is returned (no allocation, no change).
        var args = new PatternParamsJson { speed = 7f, shardCount = 16 };
        PatternParamsJson same = DifficultyResolver.ApplyScale(args, 1f, 1f);
        Assert.AreSame(args, same);
    }
}

// EditMode tests: composite spawn-position invariants. Each static bulletSpawner
// is composed against its clip's bullets (world = spawnerPos + Rotate(originPos,
// angle)) and any bullet whose spawn position leaves the runtime survival region
// [-2, 36)^2 is flagged, because it is culled before it ever appears. The real
// official data currently carries a known set of such dead spawns (captain
// shellsplash off-screen tails + the stone belt-dash bug); the synthetic
// self-tests prove the geometry actually detects, rotates, and bounds correctly.
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;

public class StageSpawnPositionLintTests
{
    // shellsplash (126) rises outside the visible x range on purpose and is the
    // only official clip whose composed spawns currently leave the survival
    // region. The stone belt dash (previously 45 = 9 bullets x 5 spawners, all
    // culled off-screen) was removed as a superfluous decoration per user
    // feedback, which also cleared that tracked spawn bug; the ratchet dropped
    // 171 -> 126 accordingly.
    private const int KnownOutOfRangeWarnings = 126;
    private static readonly string[] KnownOutOfRangeClips = { "shellsplash" };

    [Test]
    public void AllStageSpawnPositionsAreKnownAndErrorFree()
    {
        StageValidation.Report report = new StageValidation.Report();
        using (EditorStageProbe probe = new EditorStageProbe(StageGoldenDumper.BtdbAssetPath, StageGoldenDumper.EdbAssetPath))
        {
            Dictionary<string, StageData> stages = StageGoldenDumper.LoadOfficialStages();
            StageValidation.ValidateStageSpawnPositions(stages, report);
        }

        Assert.IsEmpty(report.Errors, "Spawn-position check must never error:\n" + string.Join("\n", report.Errors));

        // Ratchet: every warning must belong to one of the two known dead-spawn
        // clips, and the total must not drift. If the belt-dash data is later
        // fixed (a pending authoring decision) this count drops and the test is
        // updated deliberately, exactly like a golden.
        foreach (string w in report.Warnings)
        {
            bool known = false;
            foreach (string clip in KnownOutOfRangeClips)
            {
                if (w.Contains(clip)) { known = true; break; }
            }
            Assert.IsTrue(known, "Unexpected out-of-range spawn warning (new debt?):\n" + w);
        }

        Assert.AreEqual(KnownOutOfRangeWarnings, report.Warnings.Count,
            "Out-of-range spawn count drifted:\n" + string.Join("\n", report.Warnings));
    }

    // ---- Detector self-tests with synthetic geometry (no probe) ----

    [Test]
    public void DetectorFiresOnOutOfRangeComposite()
    {
        StageValidation.Report report = new StageValidation.Report();
        // spawner near the right edge + a clip origin pushing further right:
        // world = (30,12) + (10,0) = (40,12), x >= 36 -> culled.
        StageValidation.CheckSpawnPositions("synthetic", 0, "clip",
            new float2(30f, 12f), 0f, new List<float2> { new float2(10f, 0f) }, report);

        Assert.IsEmpty(report.Errors);
        Assert.That(report.Warnings, Has.Some.Contains("[Spawn]"));
        Assert.AreEqual(1, report.Warnings.Count);
    }

    [Test]
    public void DetectorPassesInRangeComposite()
    {
        StageValidation.Report report = new StageValidation.Report();
        StageValidation.CheckSpawnPositions("synthetic", 0, "clip",
            new float2(16f, 9f), 0f,
            new List<float2> { new float2(0f, 0f), new float2(5f, 3f), new float2(-4f, -4f) }, report);

        Assert.IsEmpty(report.Warnings, "In-range composites must not warn:\n" + string.Join("\n", report.Warnings));
    }

    [Test]
    public void DetectorAppliesSpawnerRotation()
    {
        // Same spawner + origin: at 0deg it composes right (out of range); rotated
        // 180deg it composes left, back inside. Proves the angle is really used.
        float2 spawner = new float2(30f, 12f);
        List<float2> origin = new List<float2> { new float2(10f, 0f) };

        StageValidation.Report at0 = new StageValidation.Report();
        StageValidation.CheckSpawnPositions("synthetic", 0, "clip", spawner, 0f, origin, at0);
        Assert.AreEqual(1, at0.Warnings.Count, "0deg should push the bullet out of range.");

        StageValidation.Report at180 = new StageValidation.Report();
        StageValidation.CheckSpawnPositions("synthetic", 0, "clip", spawner, 180f, origin, at180);
        Assert.IsEmpty(at180.Warnings, "180deg should bring the bullet back in range.");
    }

    [Test]
    public void ComposeMatchesTranslateThenRotate()
    {
        // 0deg is pure translation.
        float2 straight = StageValidation.ComposeSpawnWorldPosition(new float2(28f, 12f), 0f, new float2(10f, 0f));
        Assert.AreEqual(38f, straight.x, 1e-4f);
        Assert.AreEqual(12f, straight.y, 1e-4f);

        // 90deg rotates (10,0) -> (0,10) before translating.
        float2 turned = StageValidation.ComposeSpawnWorldPosition(new float2(28f, 12f), 90f, new float2(10f, 0f));
        Assert.AreEqual(28f, turned.x, 1e-3f);
        Assert.AreEqual(22f, turned.y, 1e-3f);
    }

    [Test]
    public void SurvivalRegionBoundsAreLowerInclusiveUpperExclusive()
    {
        Assert.IsTrue(StageValidation.IsInsideSurvivalRegion(new float2(-2f, -2f)), "-2 is inside (CullingMargin).");
        Assert.IsTrue(StageValidation.IsInsideSurvivalRegion(new float2(35.99f, 35.99f)), "just under 36 is inside.");
        Assert.IsFalse(StageValidation.IsInsideSurvivalRegion(new float2(36f, 0f)), "36 is culled (grid extent).");
        Assert.IsFalse(StageValidation.IsInsideSurvivalRegion(new float2(0f, 36f)), "36 is culled on y too.");
        Assert.IsFalse(StageValidation.IsInsideSurvivalRegion(new float2(-2.01f, 0f)), "below -2 is culled.");
    }
}

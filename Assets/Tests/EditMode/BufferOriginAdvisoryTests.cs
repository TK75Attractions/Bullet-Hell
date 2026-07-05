// EditMode tests: raw originPos advisory scope after the [Spawn] supersession.
// The raw advisory compares a clip-LOCAL originPos against the world play area,
// which is structurally imprecise; for clips the composite [Spawn] check
// examines (official-stage bulletSpawner references, non-laser, non-homing) the
// advisory is suppressed. These tests pin the surviving real-data set (ratchet)
// and prove the supersession resolution on synthetic layouts: it must never
// silence a clip the [Spawn] check cannot see.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

public class BufferOriginAdvisoryTests
{
    // Surviving raw originPos advisories on the current official data: only
    // clips outside the [Spawn] scope (laser clips, unreferenced/dead buffers,
    // _archive). Any drift is a deliberate data change, exactly like a golden.
    private const int KnownOriginPosWarnings = 66;
    private static readonly string[] KnownOriginPosFiles =
    {
        "debug/HexagonClockwise.json",                    // unreferenced, 12
        "debug/LightMagic_HelixPillar_Strands.json",      // laser, 6
        "mirror/mirror_Insane.json",                      // laser, 27
        "_archive/stone/belt_flow_dash.json",             // archived (removed from chart), 11
        "_archive/stone/big_block_hammer_3.json",         // archived (51s rework), 2
        "_archive/stone/stone_block_drop_a.json",         // archive, 3
        "_archive/stone/stone_block_drop_b.json",         // archive, 3
        "_archive/stone/stone_conveyor_left_a.json",      // archive, 1
        "_archive/stone/stone_conveyor_left_b.json",      // archive, 1
    };

    [Test]
    public void OriginPosAdvisoriesAreKnownAndOutsideSpawnScope()
    {
        BulletTypeDataBase btdb = AssetDatabase.LoadAssetAtPath<BulletTypeDataBase>(StageGoldenDumper.BtdbAssetPath);
        Assert.IsNotNull(btdb, "BulletTypeDataBase asset not found.");

        StageValidation.Report report = new StageValidation.Report();
        StageValidation.ValidateBuffers(btdb, report);
        Assert.IsEmpty(report.Errors, "Buffer errors:\n" + string.Join("\n", report.Errors));

        List<string> originWarnings = new List<string>();
        foreach (string w in report.Warnings)
        {
            if (w.StartsWith("[Buffer]") && w.Contains("originPos"))
            {
                originWarnings.Add(w);
            }
        }

        foreach (string w in originWarnings)
        {
            bool known = false;
            foreach (string file in KnownOriginPosFiles)
            {
                if (w.Contains(file)) { known = true; break; }
            }
            Assert.IsTrue(known, "Unexpected raw originPos advisory (new dead data, or a clip the [Spawn] check should cover?):\n" + w);
        }

        Assert.AreEqual(KnownOriginPosWarnings, originWarnings.Count,
            "Raw originPos advisory count drifted:\n" + string.Join("\n", originWarnings));
    }

    // ---- Supersession resolution self-tests (synthetic, no disk) ----

    private static StageValidation.BufferClipRef Clip(string file, string topDir, string name, bool laser = false, bool homing = false)
    {
        return new StageValidation.BufferClipRef { File = file, TopDir = topDir, Name = name, IsLaser = laser, Homing = homing };
    }

    private static Dictionary<string, HashSet<string>> Refs(string stageDir, params string[] clips)
    {
        return new Dictionary<string, HashSet<string>>
        {
            { stageDir, new HashSet<string>(clips) }
        };
    }

    [Test]
    public void ReferencedStageClipIsSuperseded()
    {
        var buffers = new List<StageValidation.BufferClipRef> { Clip("f1", "stone", "clipA") };
        var result = StageValidation.ComputeSpawnSupersededBufferFiles(buffers, Refs("stone", "clipA"));
        Assert.IsTrue(result.Contains("f1"), "A stage-referenced non-laser non-homing clip is examined by [Spawn] and must be superseded.");
    }

    [Test]
    public void LaserAndHomingClipsAreNeverSuperseded()
    {
        var buffers = new List<StageValidation.BufferClipRef>
        {
            Clip("fLaser", "stone", "clipL", laser: true),
            Clip("fHoming", "stone", "clipH", homing: true),
        };
        var result = StageValidation.ComputeSpawnSupersededBufferFiles(buffers, Refs("stone", "clipL", "clipH"));
        Assert.IsEmpty(result, "[Spawn] skips laser/homing clips, so their raw advisory must survive.");
    }

    [Test]
    public void UnreferencedClipIsNotSuperseded()
    {
        var buffers = new List<StageValidation.BufferClipRef> { Clip("f1", "stone", "deadClip") };
        var result = StageValidation.ComputeSpawnSupersededBufferFiles(buffers, Refs("stone", "someOtherClip"));
        Assert.IsEmpty(result, "A clip no bulletSpawner references is never examined by [Spawn].");
    }

    [Test]
    public void CommonClipReferencedByStageIsSuperseded()
    {
        var buffers = new List<StageValidation.BufferClipRef> { Clip("fCommon", "common", "sharedClip") };
        var result = StageValidation.ComputeSpawnSupersededBufferFiles(buffers, Refs("stone", "sharedClip"));
        Assert.IsTrue(result.Contains("fCommon"), "common/ is loaded for every stage, so its referenced clips are in [Spawn] scope.");
    }

    [Test]
    public void StageFolderShadowsCommonClip()
    {
        // Stage's own file wins name resolution (replace-by-name): [Spawn] only
        // ever sees the stage copy, so the shadowed common copy keeps its advisory.
        var buffers = new List<StageValidation.BufferClipRef>
        {
            Clip("fCommon", "common", "clipX"),
            Clip("fStone", "stone", "clipX"),
        };
        var result = StageValidation.ComputeSpawnSupersededBufferFiles(buffers, Refs("stone", "clipX"));
        Assert.IsTrue(result.Contains("fStone"), "The stage copy wins resolution and is examined.");
        Assert.IsFalse(result.Contains("fCommon"), "The shadowed common copy is never examined by [Spawn].");
    }

    [Test]
    public void ClipInAnotherStagesFolderIsNotSuperseded()
    {
        // mirror/ is only loaded when the mirror stage runs; a stone reference
        // never resolves to it, so it stays outside [Spawn] scope.
        var buffers = new List<StageValidation.BufferClipRef> { Clip("fMirror", "mirror", "clipY") };
        var result = StageValidation.ComputeSpawnSupersededBufferFiles(buffers, Refs("stone", "clipY"));
        Assert.IsEmpty(result, "Another stage's folder is not in this stage's load scope.");
    }
}

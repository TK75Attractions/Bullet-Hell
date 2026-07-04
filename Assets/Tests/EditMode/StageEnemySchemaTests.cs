// EditMode tests: stage enemySpawner JSON schema-key invariants.
using NUnit.Framework;

/// <summary>
/// Guards the accident class where a stage's enemySpawner JSON carries a key that
/// no runtime DTO field accepts. JsonUtility silently drops any key absent from
/// the target type, so a misspelled or unsupported key is dead data that no one
/// notices — it never crashes, so every finding is a warning, not an error. The
/// historical case was captain, which wrote bulletInterval on all 6 spawners even
/// though EnemySpawnerJson has no such field (the runtime recomputes it from
/// bulletEmitTime/bulletCount); that data was cleaned in 2026-07, so the real-data
/// test now ratchets on zero dead keys: any new one fails the suite and must be
/// fixed or explicitly accepted. The synthetic self-tests prove each nesting level
/// of the detector actually fires.
/// </summary>
public class StageEnemySchemaTests
{
    [Test]
    public void StageEnemySpawnersHaveNoUnknownKeys()
    {
        StageValidation.Report report = new StageValidation.Report();
        StageValidation.ValidateStageEnemySchema(report);

        Assert.IsEmpty(report.Errors, "Stage enemy schema errors:\n" + string.Join("\n", report.Errors));

        // Ratchet: the known bulletInterval debt was removed from captain.json,
        // so any warning here is a NEW dead key and must be addressed deliberately.
        Assert.IsEmpty(report.Warnings,
            "Unexpected schema warnings:\n" + string.Join("\n", report.Warnings));
    }

    // ---- Detector self-tests with synthetic JSON (prove the checks fire) ----

    [Test]
    public void SchemaValidatorCatchesUnknownSpawnerKey()
    {
        StageValidation.Report report = new StageValidation.Report();
        string json = "{\"enemySpawners\":[{\"count\":1,\"bulletIntervall\":0.5}]}";
        StageValidation.ValidateStageEnemySchemaJson("synthetic.json", json, report);

        Assert.That(report.Warnings, Has.Some.Contains("key 'bulletIntervall'"));
        Assert.IsEmpty(report.Errors, "Unexpected errors:\n" + string.Join("\n", report.Errors));
    }

    [Test]
    public void SchemaValidatorCatchesPlayerInfluenceInOrbit()
    {
        StageValidation.Report report = new StageValidation.Report();
        string json = "{\"enemySpawners\":[{\"orbit\":{\"typeName\":\"\",\"playerInfluence\":{\"x\":1,\"y\":0}}}]}";
        StageValidation.ValidateStageEnemySchemaJson("synthetic.json", json, report);

        Assert.That(report.Warnings, Has.Some.Contains("'playerInfluence'"));
        Assert.That(report.Warnings, Has.Some.Contains("buffer BulletDataJson"));
        Assert.IsEmpty(report.Errors, "Unexpected errors:\n" + string.Join("\n", report.Errors));
    }

    [Test]
    public void SchemaValidatorCatchesUnknownKeyInChangeClipData()
    {
        StageValidation.Report report = new StageValidation.Report();
        string json = "{\"enemySpawners\":[{\"bulletChangeClips\":[{\"time\":1,\"clip\":{\"number\":1,\"data\":{\"warpCooldown\":2}}}]}]}";
        StageValidation.ValidateStageEnemySchemaJson("synthetic.json", json, report);

        Assert.That(report.Warnings, Has.Some.Contains("'warpCooldown'"));
        Assert.IsEmpty(report.Errors, "Unexpected errors:\n" + string.Join("\n", report.Errors));
    }

    [Test]
    public void SchemaValidatorAcceptsCleanSpawner()
    {
        StageValidation.Report report = new StageValidation.Report();
        // Only real keys at every level, plus an animation subtree with a made-up
        // key to prove the validator does NOT descend into animation.
        string json =
            "{\"enemySpawners\":[{" +
            "\"enemyName\":\"x\",\"count\":1,\"bulletCount\":1," +
            "\"orbit\":{\"typeName\":\"\",\"appearTime\":0}," +
            "\"bulletClip\":{\"data\":{\"typeName\":\"\"},\"number\":1,\"disRad\":0.1,\"homing\":false}," +
            "\"bulletChangeClips\":[{\"clip\":{\"data\":{\"typeName\":\"\"},\"number\":1},\"time\":1}]," +
            "\"animation\":{\"initialClip\":\"idle\",\"madeUpKey\":1}" +
            "}]}";
        StageValidation.ValidateStageEnemySchemaJson("synthetic.json", json, report);

        Assert.IsEmpty(report.Warnings, "Clean spawner produced warnings:\n" + string.Join("\n", report.Warnings));
        Assert.IsEmpty(report.Errors, "Clean spawner produced errors:\n" + string.Join("\n", report.Errors));
    }
}

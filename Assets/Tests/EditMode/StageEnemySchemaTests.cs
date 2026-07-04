// EditMode tests: stage enemySpawner JSON schema-key invariants.
using NUnit.Framework;

/// <summary>
/// Guards the accident class where a stage's enemySpawner JSON carries a key that
/// no runtime DTO field accepts. JsonUtility silently drops any key absent from
/// the target type, so a misspelled or unsupported key is dead data that no one
/// notices — it never crashes, so every finding is a warning, not an error. The
/// live case is captain, which writes bulletInterval on all 6 spawners even though
/// EnemySpawnerJson has no such field; the runtime recomputes bulletInterval from
/// bulletEmitTime/bulletCount, so edits to that key do nothing. The real-data test
/// ratchets on exactly that one known dead key: any NEW dead key fails the suite
/// and must be fixed or explicitly accepted. The synthetic self-tests prove each
/// nesting level of the detector actually fires.
/// </summary>
public class StageEnemySchemaTests
{
    [Test]
    public void StageEnemyUnknownKeysAreOnlyTheKnownBulletIntervalDebt()
    {
        StageValidation.Report report = new StageValidation.Report();
        StageValidation.ValidateStageEnemySchema(report);

        Assert.IsEmpty(report.Errors, "Stage enemy schema errors:\n" + string.Join("\n", report.Errors));

        // Ratchet: bulletInterval is today's only known dead key. If captain is
        // regenerated without it this assertion is vacuously green; a new dead key
        // trips it here and must be addressed deliberately.
        Assert.That(report.Warnings, Has.All.Contains("key 'bulletInterval'"),
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

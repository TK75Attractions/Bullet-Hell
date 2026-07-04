// EditMode tests: stage patternEvents authoring invariants.
using NUnit.Framework;
using UnityEditor;

/// <summary>
/// Guards the accident class where a stage's patternEvents reference something
/// that does not exist. All three runtime failures are silent: an empty
/// patternType is dropped by NormalizePatternEvents, an unregistered patternType
/// makes PatternExecutor.Expand return false, and an unresolved bullet type
/// (an explicit shard/cutter override or any of the fixed structural/fallback
/// types in PatternDefaults.RequiredTypeNames) is filtered out (typeId &lt; 0)
/// before rendering — no bullets, no console message. Severity matches: unknown
/// pattern / bullet types are errors, empty patternType and
/// off-area/negative-beats advisories are warnings. The real data (pattern_demo
/// only) is both error-free and warning-free; the synthetic self-tests prove
/// each branch actually fires.
/// </summary>
public class StagePatternLintTests
{
    [Test]
    public void AllStagePatternEventsAreClean()
    {
        BulletTypeDataBase btdb = AssetDatabase.LoadAssetAtPath<BulletTypeDataBase>(StageGoldenDumper.BtdbAssetPath);
        Assert.IsNotNull(btdb, "BulletTypeDataBase asset not found.");

        StageValidation.Report report = new StageValidation.Report();
        StageValidation.ValidateStagePatternEvents(btdb, report);

        Assert.IsEmpty(report.Errors, "Stage pattern errors:\n" + string.Join("\n", report.Errors));
        Assert.IsEmpty(report.Warnings, "Stage pattern warnings:\n" + string.Join("\n", report.Warnings));
    }

    // ---- Detector self-tests with synthetic JSON (prove the checks fire) ----

    [Test]
    public void PatternValidatorCatchesUnknownPatternType()
    {
        BulletTypeDataBase btdb = AssetDatabase.LoadAssetAtPath<BulletTypeDataBase>(StageGoldenDumper.BtdbAssetPath);
        Assert.IsNotNull(btdb, "BulletTypeDataBase asset not found.");

        StageValidation.Report report = new StageValidation.Report();
        string json = "{\"patternEvents\":[{\"patternType\":\"NoSuchPattern\",\"time\":1}]}";
        StageValidation.ValidateStagePatternJson("synthetic.json", json, btdb, report);

        Assert.That(report.Errors, Has.Some.Contains("patternType 'NoSuchPattern'"));
    }

    [Test]
    public void PatternValidatorWarnsOnEmptyPatternType()
    {
        BulletTypeDataBase btdb = AssetDatabase.LoadAssetAtPath<BulletTypeDataBase>(StageGoldenDumper.BtdbAssetPath);
        Assert.IsNotNull(btdb, "BulletTypeDataBase asset not found.");

        StageValidation.Report report = new StageValidation.Report();
        string json = "{\"patternEvents\":[{\"patternType\":\"\",\"time\":1}]}";
        StageValidation.ValidateStagePatternJson("synthetic.json", json, btdb, report);

        Assert.IsEmpty(report.Errors, "Empty patternType must not be an error:\n" + string.Join("\n", report.Errors));
        Assert.That(report.Warnings, Has.Some.Contains("empty patternType"));
    }

    [Test]
    public void PatternValidatorCatchesUnknownShardType()
    {
        BulletTypeDataBase btdb = AssetDatabase.LoadAssetAtPath<BulletTypeDataBase>(StageGoldenDumper.BtdbAssetPath);
        Assert.IsNotNull(btdb, "BulletTypeDataBase asset not found.");

        StageValidation.Report report = new StageValidation.Report();
        string json = "{\"patternEvents\":[{\"patternType\":\"RadialBurst\",\"time\":1,\"args\":{\"shardType\":\"no_such_type_xyz\"}}]}";
        StageValidation.ValidateStagePatternJson("synthetic.json", json, btdb, report);

        Assert.That(report.Errors, Has.Some.Contains("shardType 'no_such_type_xyz'"));
    }

    [Test]
    public void PatternValidatorWarnsOnOutOfAreaPosition()
    {
        BulletTypeDataBase btdb = AssetDatabase.LoadAssetAtPath<BulletTypeDataBase>(StageGoldenDumper.BtdbAssetPath);
        Assert.IsNotNull(btdb, "BulletTypeDataBase asset not found.");

        StageValidation.Report report = new StageValidation.Report();
        string json = "{\"patternEvents\":[{\"patternType\":\"RadialBurst\",\"time\":1,\"args\":{\"positions\":[{\"x\":-5,\"y\":9}]}}]}";
        StageValidation.ValidateStagePatternJson("synthetic.json", json, btdb, report);

        Assert.IsEmpty(report.Errors, "Off-area position must not be an error:\n" + string.Join("\n", report.Errors));
        Assert.That(report.Warnings, Has.Some.Contains("positions[0]"));
    }

    [Test]
    public void PatternValidatorWarnsOnNegativeFallBeats()
    {
        BulletTypeDataBase btdb = AssetDatabase.LoadAssetAtPath<BulletTypeDataBase>(StageGoldenDumper.BtdbAssetPath);
        Assert.IsNotNull(btdb, "BulletTypeDataBase asset not found.");

        StageValidation.Report report = new StageValidation.Report();
        string json = "{\"patternEvents\":[{\"patternType\":\"FallingBlock\",\"time\":1,\"args\":{\"fallBeats\":-2}}]}";
        StageValidation.ValidateStagePatternJson("synthetic.json", json, btdb, report);

        Assert.IsEmpty(report.Errors, "Negative fallBeats must not be an error:\n" + string.Join("\n", report.Errors));
        Assert.That(report.Warnings, Has.Some.Contains("fallBeats"));
    }

    [Test]
    public void PatternValidatorAcceptsCleanEvent()
    {
        BulletTypeDataBase btdb = AssetDatabase.LoadAssetAtPath<BulletTypeDataBase>(StageGoldenDumper.BtdbAssetPath);
        Assert.IsNotNull(btdb, "BulletTypeDataBase asset not found.");

        StageValidation.Report report = new StageValidation.Report();
        string json = "{\"patternEvents\":[{\"patternType\":\"BeatPulseWarn\",\"time\":1,\"args\":{\"positions\":[{\"x\":16,\"y\":9}]}}]}";
        StageValidation.ValidateStagePatternJson("synthetic.json", json, btdb, report);

        Assert.IsEmpty(report.Errors, "Clean event produced errors:\n" + string.Join("\n", report.Errors));
        Assert.IsEmpty(report.Warnings, "Clean event produced warnings:\n" + string.Join("\n", report.Warnings));
    }
}

// EditMode tests: stage enemySpawner bullet typeName link invariants.
using NUnit.Framework;
using UnityEditor;

/// <summary>
/// Guards the accident class where a stage's enemySpawner declares a bullet
/// typeName that does not exist in BulletTypeDataBase. At runtime an unknown
/// name resolves to typeId -1 with only a console warning — nothing fails — so
/// a firing bullet silently misbehaves. Severity mirrors the firing semantics
/// (a spawner emits iff count &gt; 0 && bulletCount &gt; 0 && bulletClip.number
/// &gt; 0; change clips apply only to emitted bullets): unresolved names on
/// firing bullets are errors, on dormant spawners warnings, and empty
/// orbit typeNames are never flagged. The real data (captain + stone) is both
/// error-free and warning-free; the synthetic self-tests prove each branch
/// actually fires.
/// </summary>
public class StageEnemyLinkTests
{
    [Test]
    public void AllStageEnemyTypeNamesResolve()
    {
        BulletTypeDataBase btdb = AssetDatabase.LoadAssetAtPath<BulletTypeDataBase>(StageGoldenDumper.BtdbAssetPath);
        Assert.IsNotNull(btdb, "BulletTypeDataBase asset not found.");

        StageValidation.Report report = new StageValidation.Report();
        StageValidation.ValidateStageEnemyTypeNames(btdb, report);

        Assert.IsEmpty(report.Errors, "Stage enemy typeName errors:\n" + string.Join("\n", report.Errors));
        Assert.IsEmpty(report.Warnings, "Stage enemy typeName warnings:\n" + string.Join("\n", report.Warnings));
    }

    // ---- Detector self-tests with synthetic JSON (prove the checks fire) ----

    [Test]
    public void EnemyValidatorCatchesUnknownOrbitTypeName()
    {
        BulletTypeDataBase btdb = AssetDatabase.LoadAssetAtPath<BulletTypeDataBase>(StageGoldenDumper.BtdbAssetPath);
        Assert.IsNotNull(btdb, "BulletTypeDataBase asset not found.");

        StageValidation.Report report = new StageValidation.Report();
        string json = "{\"enemySpawners\":[{\"count\":1,\"bulletCount\":0,\"orbit\":{\"typeName\":\"no_such_type_xyz\"}}]}";
        StageValidation.ValidateStageEnemyJson("synthetic.json", json, btdb, report);

        Assert.That(report.Errors, Has.Some.Contains("orbit typeName 'no_such_type_xyz'"));
    }

    [Test]
    public void EnemyValidatorCatchesEmptyTypeNameOnFiringClip()
    {
        BulletTypeDataBase btdb = AssetDatabase.LoadAssetAtPath<BulletTypeDataBase>(StageGoldenDumper.BtdbAssetPath);
        Assert.IsNotNull(btdb, "BulletTypeDataBase asset not found.");

        StageValidation.Report report = new StageValidation.Report();
        string json = "{\"enemySpawners\":[{\"count\":1,\"bulletCount\":1,\"bulletClip\":{\"data\":{},\"number\":8}}]}";
        StageValidation.ValidateStageEnemyJson("synthetic.json", json, btdb, report);

        Assert.That(report.Errors, Has.Some.Contains("typeName is empty"));
    }

    [Test]
    public void EnemyValidatorCatchesUnknownTypeNameOnFiringClip()
    {
        BulletTypeDataBase btdb = AssetDatabase.LoadAssetAtPath<BulletTypeDataBase>(StageGoldenDumper.BtdbAssetPath);
        Assert.IsNotNull(btdb, "BulletTypeDataBase asset not found.");

        StageValidation.Report report = new StageValidation.Report();
        string json = "{\"enemySpawners\":[{\"count\":1,\"bulletCount\":1,\"bulletClip\":{\"data\":{\"typeName\":\"no_such_type_xyz\"},\"number\":8}}]}";
        StageValidation.ValidateStageEnemyJson("synthetic.json", json, btdb, report);

        Assert.That(report.Errors, Has.Some.Contains("bulletClip typeName 'no_such_type_xyz'"));
    }

    [Test]
    public void EnemyValidatorCatchesUnknownChangeClipUnderFiringParent()
    {
        BulletTypeDataBase btdb = AssetDatabase.LoadAssetAtPath<BulletTypeDataBase>(StageGoldenDumper.BtdbAssetPath);
        Assert.IsNotNull(btdb, "BulletTypeDataBase asset not found.");

        // Pick a real registered name at runtime so the parent clip resolves and
        // the spawner actually fires — isolating the change-clip error.
        string validName = null;
        foreach (BulletType type in btdb.types)
        {
            if (type != null && !string.IsNullOrEmpty(type.typeName))
            {
                validName = type.typeName;
                break;
            }
        }
        Assert.IsNotNull(validName, "BulletTypeDataBase has no usable typeName.");

        StageValidation.Report report = new StageValidation.Report();
        string json =
            "{\"enemySpawners\":[{\"count\":1,\"bulletCount\":1," +
            "\"bulletClip\":{\"data\":{\"typeName\":\"" + validName + "\"},\"number\":8}," +
            "\"bulletChangeClips\":[{\"clip\":{\"data\":{\"typeName\":\"no_such_type_xyz\"},\"number\":1}}]}]}";
        StageValidation.ValidateStageEnemyJson("synthetic.json", json, btdb, report);

        Assert.That(report.Errors, Has.Some.Contains("bulletChangeClips[0]"));
    }

    [Test]
    public void EnemyValidatorAcceptsDormantSpawnerShapedLikeStone()
    {
        BulletTypeDataBase btdb = AssetDatabase.LoadAssetAtPath<BulletTypeDataBase>(StageGoldenDumper.BtdbAssetPath);
        Assert.IsNotNull(btdb, "BulletTypeDataBase asset not found.");

        StageValidation.Report report = new StageValidation.Report();
        string json = "{\"enemySpawners\":[{\"count\":1,\"bulletCount\":0,\"orbit\":{\"typeName\":\"\"},\"bulletClip\":{\"data\":{\"typeName\":\"\"},\"number\":0}}]}";
        StageValidation.ValidateStageEnemyJson("synthetic.json", json, btdb, report);

        Assert.IsEmpty(report.Errors, "Dormant spawner produced errors:\n" + string.Join("\n", report.Errors));
        Assert.IsEmpty(report.Warnings, "Dormant spawner produced warnings:\n" + string.Join("\n", report.Warnings));
    }

    [Test]
    public void EnemyValidatorWarnsButDoesNotFailOnDormantTypo()
    {
        BulletTypeDataBase btdb = AssetDatabase.LoadAssetAtPath<BulletTypeDataBase>(StageGoldenDumper.BtdbAssetPath);
        Assert.IsNotNull(btdb, "BulletTypeDataBase asset not found.");

        StageValidation.Report report = new StageValidation.Report();
        string json = "{\"enemySpawners\":[{\"count\":1,\"bulletCount\":0,\"bulletClip\":{\"data\":{\"typeName\":\"no_such_type_xyz\"},\"number\":0}}]}";
        StageValidation.ValidateStageEnemyJson("synthetic.json", json, btdb, report);

        Assert.IsEmpty(report.Errors, "Dormant typo must not be an error:\n" + string.Join("\n", report.Errors));
        Assert.That(report.Warnings, Has.Some.Contains("dormant"));
    }
}

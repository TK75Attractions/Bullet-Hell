// EditMode tests: stage enemySpawner.visualId -> enemyVisuals link invariants.
using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Guards the accident class where an enemySpawner's visualId does not resolve
/// to a loadable enemyVisuals definition. The runtime failure is silent:
/// Boss.ResolveVisualSet falls back from visualId to the enemyName visual and
/// then to the EDB sprite/prefab marker with no console message, so a typo'd or
/// never-loading visual just shows the wrong art. Severity matches the enemy
/// typeName check: a broken reference on a firing spawner (count &gt; 0) is an
/// error, on a dormant one a warning; definition-side root causes (blank id,
/// never-loads, dead data, missing GIF files) are warnings; a duplicate
/// registered id is an error because the catalog silently keeps only the last
/// one. The real data (stone + captain are the only stages with enemyVisuals)
/// is both error-free and warning-free; the synthetic self-tests prove each
/// branch actually fires.
/// </summary>
public class StageVisualLintTests
{
    private static string StoneStageDir => Path.Combine(Application.dataPath, "StageData/stone");

    [Test]
    public void AllStageEnemyVisualLinksAreClean()
    {
        StageValidation.Report report = new StageValidation.Report();
        StageValidation.ValidateStageEnemyVisuals(report);

        Assert.IsEmpty(report.Errors, "Stage enemy-visual errors:\n" + string.Join("\n", report.Errors));
        Assert.IsEmpty(report.Warnings, "Stage enemy-visual warnings:\n" + string.Join("\n", report.Warnings));
    }

    // ---- Detector self-tests with synthetic JSON (prove the checks fire) ----

    [Test]
    public void VisualValidatorCatchesFiringSpawnerWithMissingDefinition()
    {
        StageValidation.Report report = new StageValidation.Report();
        string json = "{\"enemySpawners\":[{\"enemyName\":\"demo\",\"visualId\":\"no_such_visual\",\"count\":1}]}";
        StageValidation.ValidateStageEnemyVisualsJson("synthetic.json", json, null, report);

        Assert.That(report.Errors, Has.Some.Contains("visualId 'no_such_visual'"));
    }

    [Test]
    public void VisualValidatorWarnsOnDormantSpawnerWithMissingDefinition()
    {
        StageValidation.Report report = new StageValidation.Report();
        string json = "{\"enemySpawners\":[{\"enemyName\":\"demo\",\"visualId\":\"no_such_visual\",\"count\":0}]}";
        StageValidation.ValidateStageEnemyVisualsJson("synthetic.json", json, null, report);

        Assert.IsEmpty(report.Errors, "Dormant spawner must not be an error:\n" + string.Join("\n", report.Errors));
        Assert.That(report.Warnings, Has.Some.Contains("dormant"));
    }

    [Test]
    public void VisualValidatorCatchesDuplicateRegisteredId()
    {
        StageValidation.Report report = new StageValidation.Report();
        string json = "{\"enemyVisuals\":[" +
            "{\"id\":\"dup\",\"source\":\"externalGif\",\"clips\":[{\"name\":\"idle\",\"path\":\"idle.gif\"}]}," +
            "{\"id\":\"dup\",\"source\":\"externalGif\",\"clips\":[{\"name\":\"idle\",\"path\":\"idle.gif\"}]}]," +
            "\"enemySpawners\":[{\"enemyName\":\"demo\",\"visualId\":\"dup\",\"count\":1}]}";
        StageValidation.ValidateStageEnemyVisualsJson("synthetic.json", json, null, report);

        Assert.That(report.Errors, Has.Some.Contains("duplicates"));
    }

    [Test]
    public void VisualValidatorCatchesFiringSpawnerReferencingNeverLoadingDefinition()
    {
        StageValidation.Report report = new StageValidation.Report();
        string json = "{\"enemyVisuals\":[{\"id\":\"cap\",\"source\":\"addressable\",\"address\":\"\"}]," +
            "\"enemySpawners\":[{\"enemyName\":\"demo\",\"visualId\":\"cap\",\"count\":1}]}";
        StageValidation.ValidateStageEnemyVisualsJson("synthetic.json", json, null, report);

        Assert.That(report.Errors, Has.Some.Contains("never loads"));
        Assert.That(report.Warnings, Has.Some.Contains("address is blank"));
    }

    [Test]
    public void VisualValidatorWarnsOnBlankDefinitionId()
    {
        StageValidation.Report report = new StageValidation.Report();
        string json = "{\"enemyVisuals\":[{\"id\":\"\",\"source\":\"externalGif\"}]}";
        StageValidation.ValidateStageEnemyVisualsJson("synthetic.json", json, null, report);

        Assert.IsEmpty(report.Errors, "Blank id must not be an error:\n" + string.Join("\n", report.Errors));
        Assert.That(report.Warnings, Has.Some.Contains("blank id"));
    }

    [Test]
    public void VisualValidatorWarnsOnUnknownSource()
    {
        StageValidation.Report report = new StageValidation.Report();
        string json = "{\"enemyVisuals\":[{\"id\":\"odd\",\"source\":\"resources\"}]}";
        StageValidation.ValidateStageEnemyVisualsJson("synthetic.json", json, null, report);

        Assert.IsEmpty(report.Errors, "Unknown source alone must not be an error:\n" + string.Join("\n", report.Errors));
        Assert.That(report.Warnings, Has.Some.Contains("neither"));
    }

    [Test]
    public void VisualValidatorWarnsOnUnreferencedRegisteredDefinition()
    {
        StageValidation.Report report = new StageValidation.Report();
        string json = "{\"enemyVisuals\":[{\"id\":\"ghost\",\"source\":\"externalGif\",\"clips\":[{\"name\":\"idle\",\"path\":\"idle.gif\"}]}]}";
        StageValidation.ValidateStageEnemyVisualsJson("synthetic.json", json, null, report);

        Assert.IsEmpty(report.Errors, "Dead data must not be an error:\n" + string.Join("\n", report.Errors));
        Assert.That(report.Warnings, Has.Some.Contains("not referenced"));
    }

    [Test]
    public void VisualValidatorWarnsOnGifDefinitionWithoutClips()
    {
        StageValidation.Report report = new StageValidation.Report();
        string json = "{\"enemyVisuals\":[{\"id\":\"bare\",\"source\":\"externalGif\"}]," +
            "\"enemySpawners\":[{\"enemyName\":\"demo\",\"visualId\":\"bare\",\"count\":1}]}";
        StageValidation.ValidateStageEnemyVisualsJson("synthetic.json", json, null, report);

        Assert.IsEmpty(report.Errors, "Clipless GIF visual must not be an error (it still registers):\n" + string.Join("\n", report.Errors));
        Assert.That(report.Warnings, Has.Some.Contains("no clips"));
    }

    [Test]
    public void VisualValidatorWarnsOnBlankClipPath()
    {
        StageValidation.Report report = new StageValidation.Report();
        string json = "{\"enemyVisuals\":[{\"id\":\"half\",\"source\":\"externalGif\",\"clips\":[{\"name\":\"idle\",\"path\":\"\"}]}]," +
            "\"enemySpawners\":[{\"enemyName\":\"demo\",\"visualId\":\"half\",\"count\":1}]}";
        StageValidation.ValidateStageEnemyVisualsJson("synthetic.json", json, null, report);

        Assert.IsEmpty(report.Errors, "Blank clip path must not be an error:\n" + string.Join("\n", report.Errors));
        Assert.That(report.Warnings, Has.Some.Contains("blank name or path"));
    }

    [Test]
    public void VisualValidatorWarnsOnMissingGifFile()
    {
        StageValidation.Report report = new StageValidation.Report();
        string json = "{\"enemyVisuals\":[{\"id\":\"stone\",\"source\":\"externalGif\",\"basePath\":\"Visuals\",\"clips\":[{\"name\":\"idle\",\"path\":\"no_such_file.gif\"}]}]," +
            "\"enemySpawners\":[{\"enemyName\":\"demo\",\"visualId\":\"stone\",\"count\":1}]}";
        StageValidation.ValidateStageEnemyVisualsJson("synthetic.json", json, StoneStageDir, report);

        Assert.IsEmpty(report.Errors, "Missing GIF file must not be an error (runtime degrades to fallback):\n" + string.Join("\n", report.Errors));
        Assert.That(report.Warnings, Has.Some.Contains("does not exist"));
    }

    [Test]
    public void VisualValidatorAcceptsCleanGifVisualWithRealFile()
    {
        StageValidation.Report report = new StageValidation.Report();
        string json = "{\"enemyVisuals\":[{\"id\":\"stone\",\"source\":\"externalGif\",\"basePath\":\"Visuals\",\"clips\":[{\"name\":\"idle\",\"path\":\"stone_idle.gif\"}]}]," +
            "\"enemySpawners\":[{\"enemyName\":\"demo\",\"visualId\":\"stone\",\"count\":1}]}";
        StageValidation.ValidateStageEnemyVisualsJson("synthetic.json", json, StoneStageDir, report);

        Assert.IsEmpty(report.Errors, "Clean visual produced errors:\n" + string.Join("\n", report.Errors));
        Assert.IsEmpty(report.Warnings, "Clean visual produced warnings:\n" + string.Join("\n", report.Warnings));
    }
}

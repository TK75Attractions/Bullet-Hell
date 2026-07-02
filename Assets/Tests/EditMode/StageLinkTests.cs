using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Asserts that every official stage's spawner clipName resolves to a loaded
/// buffer ("Clear" is the documented special case that resolves to no buffer).
/// </summary>
public class StageLinkTests
{
    [Test]
    public void AllStageClipNamesResolve()
    {
        StageValidation.Report report = new StageValidation.Report();
        using (EditorStageProbe probe = new EditorStageProbe(StageGoldenDumper.BtdbAssetPath, StageGoldenDumper.EdbAssetPath))
        {
            Dictionary<string, StageData> stages = StageGoldenDumper.LoadOfficialStages();
            StageValidation.ValidateStageLinks(stages, report);
        }

        Assert.IsEmpty(report.Errors, "Stage clipName link errors:\n" + string.Join("\n", report.Errors));
    }
}

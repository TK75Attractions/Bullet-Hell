using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

/// <summary>
/// Regenerates the schedule/buffer golden for every official stage and asserts
/// it matches the committed <c>Tests/Golden/*.golden.json</c>. This is the
/// behavior-lock for the refactor: any change to schedule expansion or buffer
/// content shows up here.
/// </summary>
public class GoldenScheduleTest
{
    private static Dictionary<string, string> regenerated;

    [OneTimeSetUp]
    public void RegenerateGoldens()
    {
        regenerated = StageGoldenDumper.BuildAllGoldens();
    }

    private static IEnumerable<string> StageDirs => StageGoldenDumper.OfficialStageDirs;

    [Test]
    [TestCaseSource(nameof(StageDirs))]
    public void GoldenMatches(string stageDir)
    {
        string path = StageGoldenDumper.GoldenPath(stageDir);
        Assert.IsTrue(File.Exists(path),
            $"Golden file missing for '{stageDir}'. Run menu '{StageGoldenDumper.DumpMenuPath}' to create it.");

        string expected = Normalize(File.ReadAllText(path));
        string actual = Normalize(regenerated[stageDir]);
        Assert.AreEqual(expected, actual, $"Golden mismatch for stage '{stageDir}'.");
    }

    private static string Normalize(string text)
    {
        return text.Replace("\r\n", "\n").Replace("\r", "\n");
    }
}

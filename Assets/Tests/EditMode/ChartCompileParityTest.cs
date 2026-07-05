using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// End-to-end proof for P2: compiling <c>stone.chart.json</c> and running the
/// result through the exact same golden dumper used for the committed baseline
/// must reproduce <c>Tests/Golden/stone.golden.json</c> byte-for-byte (modulo
/// line endings). This certifies that beat-domain authoring lowers to the same
/// spawn schedule as the hand-authored stage.json.
/// </summary>
public class ChartCompileParityTest
{
    private const string StageDir = "stone";

    private static string ChartPath =>
        Path.Combine(Application.dataPath, "StageData", StageDir, StageDir + ".chart.json");

    [Test]
    public void StoneChartCompilesToGolden()
    {
        Assert.IsTrue(File.Exists(ChartPath), $"Chart not found: {ChartPath}");

        StageChartCompiler.CompileResult result = StageChartCompiler.Compile(File.ReadAllText(ChartPath), StageDir);
        Assert.IsTrue(result.IsGreen, "Compile reported errors:\n" + string.Join("\n", result.Errors));
        Assert.AreEqual(104, result.EventCount, "Expected 104 compiled bullet events.");

        string goldenPath = StageGoldenDumper.GoldenPath(StageDir);
        Assert.IsTrue(File.Exists(goldenPath), $"Committed golden missing: {goldenPath}");
        string expected = Normalize(File.ReadAllText(goldenPath));

        string actual;
        using (new EditorStageProbe(StageGoldenDumper.BtdbAssetPath, StageGoldenDumper.EdbAssetPath))
        {
            actual = Normalize(StageGoldenDumper.BuildGolden(result.StageDataForGolden, StageDir));
        }

        Assert.AreEqual(expected, actual, "Compiled stone chart golden differs from committed baseline.");
    }

    private static string Normalize(string text)
    {
        return text.Replace("\r\n", "\n").Replace("\r", "\n");
    }
}

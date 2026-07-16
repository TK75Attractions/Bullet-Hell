using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 引き継ぎコードのペイロード元になる実績データ(TransferAchievements)を固定する。
/// PlayerPrefs キー "transferAchievements.v1" を退避・復元し、実機の実績を汚さない。
/// </summary>
public class TransferAchievementsTests
{
    private const string PrefsKey = "transferAchievements.v1";
    private string savedJson;
    private bool hadJson;

    [SetUp]
    public void SetUp()
    {
        hadJson = PlayerPrefs.HasKey(PrefsKey);
        savedJson = hadJson ? PlayerPrefs.GetString(PrefsKey) : null;
        PlayerPrefs.DeleteKey(PrefsKey);
        ResetCache();
    }

    [TearDown]
    public void TearDown()
    {
        if (hadJson) PlayerPrefs.SetString(PrefsKey, savedJson);
        else PlayerPrefs.DeleteKey(PrefsKey);
        PlayerPrefs.Save();
        ResetCache();
    }

    private static void ResetCache()
    {
        typeof(TransferAchievements)
            .GetField("cache", BindingFlags.NonPublic | BindingFlags.Static)
            .SetValue(null, null);
    }

    [Test]
    public void RecordClear_UnknownStage_IsIgnored()
    {
        TransferAchievements.RecordClear("debug", 2, false); // コード対象外ステージ(§1.4)
        Assert.IsFalse(TransferAchievements.HasAnyAchievement);
    }

    [Test]
    public void RecordClear_KnownStage_SetsClearFlag()
    {
        TransferAchievements.RecordClear("captain", 1, false); // Normal
        DirectionTransferCode.Payload payload = TransferAchievements.BuildPayload();
        int stageIndex = System.Array.IndexOf(DirectionTransferCode.StageOrder, "captain");
        int clearIndex = stageIndex * DirectionTransferCode.DifficultyCount + 1;
        Assert.IsTrue(payload.Clear[clearIndex]);
        // 他の難易度/ステージは立たない。
        for (int i = 0; i < payload.Clear.Length; i++)
        {
            if (i != clearIndex) Assert.IsFalse(payload.Clear[i], $"unexpected clear bit at {i}");
        }
    }

    [Test]
    public void RecordClear_NoMiss_SetsNoMissFlagForStageRegardlessOfDifficulty()
    {
        TransferAchievements.RecordClear("stone", 0, true);
        DirectionTransferCode.Payload payload = TransferAchievements.BuildPayload();
        int stageIndex = System.Array.IndexOf(DirectionTransferCode.StageOrder, "stone");
        Assert.IsTrue(payload.NoMiss[stageIndex]);
    }

    [Test]
    public void ApplyPayload_MergesWithOr_NeverLosesExistingProgress()
    {
        TransferAchievements.RecordClear("captain", 2, false); // Lunatic クリア済み
        DirectionTransferCode.Payload incoming = DirectionTransferCode.Payload.CreateEmpty();
        int stageIndex = System.Array.IndexOf(DirectionTransferCode.StageOrder, "captain");
        incoming.Clear[stageIndex * DirectionTransferCode.DifficultyCount + 0] = true; // Easy だけの他人のコード

        TransferAchievements.ApplyPayload(incoming);

        DirectionTransferCode.Payload merged = TransferAchievements.BuildPayload();
        Assert.IsTrue(merged.Clear[stageIndex * DirectionTransferCode.DifficultyCount + 0], "incoming Easy lost");
        Assert.IsTrue(merged.Clear[stageIndex * DirectionTransferCode.DifficultyCount + 2], "existing Lunatic lost");
    }

    [Test]
    public void ClearAll_ResetsEverything()
    {
        TransferAchievements.RecordClear("captain", 2, true);
        TransferAchievements.ClearAll();
        Assert.IsFalse(TransferAchievements.HasAnyAchievement);
    }

    [Test]
    public void SummaryLines_EmptyPayload_ReturnsNoLines()
    {
        DirectionTransferCode.Payload payload = DirectionTransferCode.Payload.CreateEmpty();
        Assert.AreEqual(0, TransferAchievements.SummaryLines(payload).Count);
    }

    [Test]
    public void SummaryLines_ReportsClearedDifficultiesAndNoMiss()
    {
        DirectionTransferCode.Payload payload = DirectionTransferCode.Payload.CreateEmpty();
        int stageIndex = System.Array.IndexOf(DirectionTransferCode.StageOrder, "captain");
        payload.Clear[stageIndex * DirectionTransferCode.DifficultyCount + 2] = true; // Lunatic
        payload.NoMiss[stageIndex] = true;

        var lines = TransferAchievements.SummaryLines(payload);
        Assert.AreEqual(1, lines.Count);
        StringAssert.Contains("艦長", lines[0]);
        StringAssert.Contains("LUNATIC", lines[0]);
        StringAssert.Contains("ノーミス", lines[0]);
    }
}

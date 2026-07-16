using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// アーケード式ランキング(Instructions/ranking-transfer/SPEC.md §2)の永続化/集計/
/// エクスポート・インポートを固定する。RankingStore は persistentDataPath 上の実ファイルを
/// 読み書きするため、実機の ranking.v1.json を退避・復元してからテストする(この開発機に
/// 実データがあっても壊さない)。Export/Import のテストは実デスクトップを使わず一時
/// フォルダのみを使う(ユーザーの実ファイルに触れない)。
/// </summary>
public class RankingStoreTests
{
    private string filePath;
    private bool hadFile;
    private byte[] backupBytes;
    private string tempFolder;

    [SetUp]
    public void SetUp()
    {
        filePath = Path.Combine(Application.persistentDataPath, "ranking.v1.json");
        hadFile = File.Exists(filePath);
        if (hadFile) backupBytes = File.ReadAllBytes(filePath);
        else if (File.Exists(filePath)) File.Delete(filePath);
        ResetCache();

        tempFolder = Path.Combine(Application.temporaryCachePath, "ranking_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);
    }

    [TearDown]
    public void TearDown()
    {
        if (hadFile) File.WriteAllBytes(filePath, backupBytes);
        else if (File.Exists(filePath)) File.Delete(filePath);
        ResetCache();

        if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true);
    }

    private static void ResetCache()
    {
        typeof(RankingStore)
            .GetField("cache", BindingFlags.NonPublic | BindingFlags.Static)
            .SetValue(null, null);
    }

    [Test]
    public void AddEntry_PersistsAcrossCacheReload()
    {
        RankingStore.AddEntry("ABC", 12345, "captain", 2, "1P", new DateTime(2026, 7, 16, 10, 0, 0));
        ResetCache(); // ディスクから読み直させる

        List<RankingStore.Entry> all = RankingStore.LoadAll();
        Assert.AreEqual(1, all.Count);
        Assert.AreEqual("ABC", all[0].name);
        Assert.AreEqual(12345, all[0].score);
        Assert.AreEqual("captain", all[0].stage);
        Assert.AreEqual(2, all[0].difficulty);
        Assert.AreEqual("1P", all[0].mode);
        Assert.IsFalse(string.IsNullOrEmpty(all[0].entryId));
        Assert.IsFalse(string.IsNullOrEmpty(all[0].cabinetId));
    }

    [Test]
    public void GetTop_OrdersByScoreDescending()
    {
        RankingStore.AddEntry("AAA", 100, "captain", 2, "1P", DateTime.Now);
        RankingStore.AddEntry("BBB", 300, "captain", 2, "1P", DateTime.Now);
        RankingStore.AddEntry("CCC", 200, "captain", 2, "1P", DateTime.Now);

        List<RankingStore.Entry> top = RankingStore.GetTop("captain", 2, "1P");
        Assert.AreEqual(3, top.Count);
        Assert.AreEqual("BBB", top[0].name);
        Assert.AreEqual("CCC", top[1].name);
        Assert.AreEqual("AAA", top[2].name);
    }

    [Test]
    public void GetTop_FiltersByStageDifficultyMode()
    {
        RankingStore.AddEntry("P1A", 500, "captain", 2, "1P", DateTime.Now);
        RankingStore.AddEntry("P1B", 999, "captain", 1, "1P", DateTime.Now); // 難易度違い
        RankingStore.AddEntry("P1C", 999, "stone", 2, "1P", DateTime.Now);  // ステージ違い
        RankingStore.AddEntry("P2A", 999, "captain", 2, "2P", DateTime.Now); // モード違い

        List<RankingStore.Entry> top = RankingStore.GetTop("captain", 2, "1P");
        Assert.AreEqual(1, top.Count);
        Assert.AreEqual("P1A", top[0].name);
    }

    [Test]
    public void QualifiesForTop_TrueWhenBoardNotFull()
    {
        for (int i = 0; i < 5; i++)
            RankingStore.AddEntry("X" + i, 100 + i, "captain", 2, "1P", DateTime.Now);

        Assert.IsTrue(RankingStore.QualifiesForTop("captain", 2, "1P", 1)); // 板が満杯でなければ最下位点でも入れる
    }

    [Test]
    public void QualifiesForTop_FullBoard_OnlyBeatsLowestScore()
    {
        for (int i = 0; i < RankingStore.TopCount; i++)
            RankingStore.AddEntry("X" + i, (i + 1) * 100, "captain", 2, "1P", DateTime.Now); // 100..1000

        Assert.IsFalse(RankingStore.QualifiesForTop("captain", 2, "1P", 50));
        Assert.IsTrue(RankingStore.QualifiesForTop("captain", 2, "1P", 100));  // 同点は入れる側
        Assert.IsTrue(RankingStore.QualifiesForTop("captain", 2, "1P", 1001));
    }

    [Test]
    public void ClearAll_RemovesAllEntriesAndFile()
    {
        RankingStore.AddEntry("AAA", 100, "captain", 2, "1P", DateTime.Now);
        RankingStore.ClearAll();
        Assert.AreEqual(0, RankingStore.LoadAll().Count);
        Assert.IsFalse(File.Exists(filePath));
    }

    [Test]
    public void ExportImport_RoundTrip_RestoresEntries()
    {
        RankingStore.AddEntry("AAA", 100, "captain", 2, "1P", DateTime.Now);
        RankingStore.AddEntry("BBB", 200, "stone", 1, "2P", DateTime.Now);
        string exportPath = RankingStore.ExportToFile(tempFolder);
        Assert.IsNotNull(exportPath);
        Assert.IsTrue(File.Exists(exportPath));

        RankingStore.ClearAll();
        Assert.AreEqual(0, RankingStore.LoadAll().Count);

        int imported = RankingStore.ImportFromFile(exportPath);
        Assert.AreEqual(2, imported);
        Assert.AreEqual(2, RankingStore.LoadAll().Count);
    }

    // 和集合マージ(SPEC §2.3): 同じファイルを2回インポートしても重複しない(冪等)。
    [Test]
    public void Import_SameFileTwice_IsIdempotent()
    {
        RankingStore.AddEntry("AAA", 100, "captain", 2, "1P", DateTime.Now);
        string exportPath = RankingStore.ExportToFile(tempFolder);

        int firstImport = RankingStore.ImportFromFile(exportPath);
        int secondImport = RankingStore.ImportFromFile(exportPath);

        Assert.AreEqual(0, firstImport); // 既にローカルにある(entryId一致)ので追加0件
        Assert.AreEqual(0, secondImport);
        Assert.AreEqual(1, RankingStore.LoadAll().Count);
    }

    [Test]
    public void Import_UnionMerge_KeepsBothCabinetsEntries()
    {
        // 筐体A相当: 1件エクスポート
        RankingStore.AddEntry("AAA", 100, "captain", 2, "1P", DateTime.Now);
        string exportA = RankingStore.ExportToFile(tempFolder);

        // 筐体B相当: 別データに切り替え、Aのエクスポートを取り込む
        RankingStore.ClearAll();
        RankingStore.AddEntry("BBB", 200, "captain", 2, "1P", DateTime.Now);

        int imported = RankingStore.ImportFromFile(exportA);
        Assert.AreEqual(1, imported);
        List<RankingStore.Entry> all = RankingStore.LoadAll();
        Assert.AreEqual(2, all.Count);
    }

    [Test]
    public void ImportFromFolder_ScansAllExportFiles()
    {
        RankingStore.AddEntry("AAA", 100, "captain", 2, "1P", DateTime.Now);
        RankingStore.ExportToFile(tempFolder);
        RankingStore.ClearAll();
        RankingStore.AddEntry("BBB", 200, "captain", 2, "1P", DateTime.Now);
        RankingStore.ExportToFile(tempFolder);
        RankingStore.ClearAll();

        int imported = RankingStore.ImportFromFolder(tempFolder);
        Assert.AreEqual(2, imported);
        Assert.AreEqual(2, RankingStore.LoadAll().Count);
    }

    [Test]
    public void CabinetId_IsStableAcrossCalls()
    {
        string a = RankingStore.CabinetId;
        string b = RankingStore.CabinetId;
        Assert.AreEqual(a, b);
        Assert.IsFalse(string.IsNullOrEmpty(a));
    }
}

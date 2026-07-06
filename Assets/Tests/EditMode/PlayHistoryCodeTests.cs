using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 引き継ぎコードの仕様を固定する。第30便で発行コードを v2(4文字: 合計プレイ/
/// クリア+CRC8)へ短縮したが、旧 v1(16文字: ステージ別4bit×8スロット)コードの
/// 読み込み互換は維持する。テストは実エディタの PlayerPrefs を汚さないよう、
/// 保存値を退避・復元し、PlayHistory の static キャッシュもリセットする。
/// </summary>
public class PlayHistoryCodeTests
{
    private const string PrefsKey = "playHistory.v1";
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

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

    // Load() がテスト中の状態を持ち越さないよう、private static cache を破棄する。
    private static void ResetCache()
    {
        typeof(PlayHistory)
            .GetField("cache", BindingFlags.NonPublic | BindingFlags.Static)
            .SetValue(null, null);
    }

    // ---- v2 発行コード ----

    [Test]
    public void ExportCode_IsFourBase32Symbols()
    {
        PlayHistory.RecordPlay("stone");
        PlayHistory.RecordClear("stone");
        string code = PlayHistory.ExportCode();
        Assert.AreEqual(4, code.Length);
        foreach (char ch in code)
        {
            Assert.GreaterOrEqual(Alphabet.IndexOf(ch), 0, $"non-alphabet symbol '{ch}' in {code}");
        }
    }

    [Test]
    public void ExportImport_RoundTripPreservesTotals()
    {
        for (int i = 0; i < 3; i++) PlayHistory.RecordPlay("stone");
        PlayHistory.RecordClear("stone");
        PlayHistory.RecordPlay("mirror");
        int plays = PlayHistory.TotalPlays;   // 4
        int clears = PlayHistory.TotalClears; // 1
        string code = PlayHistory.ExportCode();

        PlayHistory.ClearAll();
        Assert.IsFalse(PlayHistory.HasHistory);

        Assert.IsTrue(PlayHistory.TryImportCode(code, out string error), error);
        Assert.AreEqual(plays, PlayHistory.TotalPlays);
        Assert.AreEqual(clears, PlayHistory.TotalClears);
        Assert.IsTrue(PlayHistory.HasHistory);
    }

    [Test]
    public void Import_AcceptsLowercaseAndSeparators()
    {
        PlayHistory.RecordPlay("stone");
        string code = PlayHistory.ExportCode();
        PlayHistory.ClearAll();
        string sloppy = " " + code.ToLowerInvariant().Insert(2, "-") + " ";
        Assert.IsTrue(PlayHistory.TryImportCode(sloppy, out string error), error);
        Assert.AreEqual(1, PlayHistory.TotalPlays);
    }

    [Test]
    public void Import_RejectsSingleSymbolCorruption()
    {
        PlayHistory.RecordPlay("stone");
        PlayHistory.RecordClear("stone");
        string code = PlayHistory.ExportCode();
        // 1文字の破損は5bit以下のバースト誤りなので CRC8 が必ず検出する。
        // 全4桁それぞれについて、別の正当な文字への置換が全て弾かれることを確認。
        for (int pos = 0; pos < 4; pos++)
        {
            char original = code[pos];
            char alt = original == Alphabet[0] ? Alphabet[1] : Alphabet[0];
            string corrupted = code.Substring(0, pos) + alt + code.Substring(pos + 1);
            Assert.IsFalse(PlayHistory.TryImportCode(corrupted, out _), $"corrupted code {corrupted} was accepted");
        }
    }

    [Test]
    public void Import_RejectsWrongLength()
    {
        Assert.IsFalse(PlayHistory.TryImportCode("ABC", out _));
        Assert.IsFalse(PlayHistory.TryImportCode("ABCDE", out _));
        Assert.IsFalse(PlayHistory.TryImportCode("", out _));
        Assert.IsFalse(PlayHistory.TryImportCode(null, out _));
    }

    [Test]
    public void EmptyHistory_RoundTripStaysEmpty()
    {
        string code = PlayHistory.ExportCode();
        Assert.AreEqual(4, code.Length);
        Assert.IsTrue(PlayHistory.TryImportCode(code, out string error), error);
        Assert.IsFalse(PlayHistory.HasHistory);
        Assert.AreEqual(0, PlayHistory.TotalPlays);
    }

    [Test]
    public void ImportedTotals_SurviveFurtherPlaysAndReExport()
    {
        // v2 取り込み(合成キー)の上に実プレイが加算され、再発行にも反映される。
        PlayHistory.RecordPlay("stone");
        PlayHistory.RecordPlay("stone");
        string code = PlayHistory.ExportCode();
        PlayHistory.ClearAll();
        Assert.IsTrue(PlayHistory.TryImportCode(code, out _));
        PlayHistory.RecordPlay("stone");
        Assert.AreEqual(3, PlayHistory.TotalPlays);

        string code2 = PlayHistory.ExportCode();
        PlayHistory.ClearAll();
        Assert.IsTrue(PlayHistory.TryImportCode(code2, out _));
        Assert.AreEqual(3, PlayHistory.TotalPlays);
    }

    // ---- v1 レガシーコードの読み込み互換 ----

    [Test]
    public void Import_LegacyV1Code_StillWorks()
    {
        // stone は FixedSlotMap でスロット4に固定されている。
        var slots = new (int p, int c)[8];
        slots[4] = (3, 1);
        string legacy = BuildV1Code(slots);

        Assert.IsTrue(PlayHistory.TryImportCode(legacy, out string error), error);
        Assert.AreEqual(3, PlayHistory.TotalPlays);
        Assert.AreEqual(1, PlayHistory.TotalClears);
    }

    [Test]
    public void Import_LegacyV1Code_ThenReExportsAsV2()
    {
        var slots = new (int p, int c)[8];
        slots[4] = (5, 2);
        slots[5] = (1, 0);
        Assert.IsTrue(PlayHistory.TryImportCode(BuildV1Code(slots), out _));

        string v2 = PlayHistory.ExportCode();
        Assert.AreEqual(4, v2.Length);
        PlayHistory.ClearAll();
        Assert.IsTrue(PlayHistory.TryImportCode(v2, out _));
        Assert.AreEqual(6, PlayHistory.TotalPlays);
        Assert.AreEqual(2, PlayHistory.TotalClears);
    }

    [Test]
    public void Import_LegacyV1Code_RejectsCorruption()
    {
        var slots = new (int p, int c)[8];
        slots[4] = (3, 1);
        string legacy = BuildV1Code(slots);
        char alt = legacy[0] == Alphabet[0] ? Alphabet[1] : Alphabet[0];
        string corrupted = alt + legacy.Substring(1);
        Assert.IsFalse(PlayHistory.TryImportCode(corrupted, out _));
    }

    // v1 コードの独立実装(PlayHistory と同じ仕様: version4bit + 8スロット×
    // (p4+c4) + CRC8(poly 0x07, init 0) → 80bit へゼロ詰め → Base32 16文字)。
    // 本体のエンコーダを使わずに組み立てることで、読み込み互換を仕様として固定する。
    private static string BuildV1Code((int p, int c)[] slots)
    {
        var bits = new List<int>(80);
        WriteBits(bits, 1, 4);
        for (int i = 0; i < 8; i++)
        {
            WriteBits(bits, slots[i].p, 4);
            WriteBits(bits, slots[i].c, 4);
        }
        WriteBits(bits, Crc8(bits), 8);
        while (bits.Count % 5 != 0) bits.Add(0);

        var sb = new StringBuilder();
        for (int i = 0; i < bits.Count; i += 5)
        {
            int value = 0;
            for (int b = 0; b < 5; b++) value = (value << 1) | bits[i + b];
            sb.Append(Alphabet[value]);
        }
        return sb.ToString();
    }

    private static void WriteBits(List<int> bits, int value, int count)
    {
        for (int i = count - 1; i >= 0; i--) bits.Add((value >> i) & 1);
    }

    private static int Crc8(List<int> bits)
    {
        int crc = 0;
        foreach (int b in bits)
        {
            int top = ((crc >> 7) & 1) ^ (b & 1);
            crc = (crc << 1) & 0xFF;
            if (top == 1) crc ^= 0x07;
        }
        return crc;
    }
}

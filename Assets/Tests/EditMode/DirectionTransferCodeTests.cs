using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// 方向シーケンス引き継ぎコード(Instructions/ranking-transfer/SPEC.md §1)の
/// エンコード/デコードを固定する。DirectionTransferCode は PlayerPrefs 等の外部状態を
/// 持たない純粋関数なので、他テストのような退避/復元は不要。
/// </summary>
public class DirectionTransferCodeTests
{
    [Test]
    public void Encode_ProducesExactDigitCountArrowSymbols()
    {
        DirectionTransferCode.Payload payload = DirectionTransferCode.Payload.CreateEmpty();
        string code = DirectionTransferCode.Encode(payload);
        // 4桁ごとに空白区切り: 12桁 → 11区切り文字を含む文字数 = 12 + (12/4 - 1) = 14。
        int expectedLength = DirectionTransferCode.DigitCount + (DirectionTransferCode.DigitCount / DirectionTransferCode.DigitGroupSize - 1);
        Assert.AreEqual(expectedLength, code.Length);
        foreach (char ch in code)
        {
            Assert.IsTrue(ch == ' ' || System.Array.IndexOf(DirectionTransferCode.Symbols, ch) >= 0,
                $"unexpected character '{ch}' in {code}");
        }
    }

    [Test]
    public void EncodeDigits_AllValuesWithinBase4Range()
    {
        DirectionTransferCode.Payload payload = DirectionTransferCode.Payload.CreateEmpty();
        payload.Clear[0] = true;
        payload.NoMiss[1] = true;
        List<int> digits = DirectionTransferCode.EncodeDigits(payload);
        Assert.AreEqual(DirectionTransferCode.DigitCount, digits.Count);
        foreach (int d in digits) Assert.IsTrue(d >= 0 && d <= 3);
    }

    // SPEC §1.1: ↑=0, ↓=1, ←=2, →=3 で固定。
    [Test]
    public void Symbols_MatchSpecOrder()
    {
        Assert.AreEqual('↑', DirectionTransferCode.Symbols[0]);
        Assert.AreEqual('↓', DirectionTransferCode.Symbols[1]);
        Assert.AreEqual('←', DirectionTransferCode.Symbols[2]);
        Assert.AreEqual('→', DirectionTransferCode.Symbols[3]);
    }

    // 全 bit パターンの往復(SPEC §3)。Clear[12]+NoMiss[4]=16bit の全組み合わせ
    // (予備2bitは常に0で固定なので対象外)を Encode→TryDecode し、完全一致を確認する。
    [Test]
    public void RoundTrip_AllPayloadBitPatterns_Exhaustive()
    {
        int totalBits = DirectionTransferCode.ClearBits + DirectionTransferCode.NoMissBits; // 16
        int combos = 1 << totalBits; // 65536
        for (int mask = 0; mask < combos; mask++)
        {
            DirectionTransferCode.Payload payload = DirectionTransferCode.Payload.CreateEmpty();
            for (int i = 0; i < DirectionTransferCode.ClearBits; i++)
                payload.Clear[i] = ((mask >> i) & 1) != 0;
            for (int i = 0; i < DirectionTransferCode.NoMissBits; i++)
                payload.NoMiss[i] = ((mask >> (DirectionTransferCode.ClearBits + i)) & 1) != 0;

            List<int> digits = DirectionTransferCode.EncodeDigits(payload);
            bool ok = DirectionTransferCode.TryDecode(digits, out DirectionTransferCode.Payload decoded, out string error);
            Assert.IsTrue(ok, $"mask {mask} failed to decode: {error}");
            for (int i = 0; i < DirectionTransferCode.ClearBits; i++)
                Assert.AreEqual(payload.Clear[i], decoded.Clear[i], $"mask {mask} clear[{i}] mismatch");
            for (int i = 0; i < DirectionTransferCode.NoMissBits; i++)
                Assert.AreEqual(payload.NoMiss[i], decoded.NoMiss[i], $"mask {mask} noMiss[{i}] mismatch");
        }
    }

    // チェックサム破損検知(SPEC §3)。ペイロード桁(0..8)のいずれかを別の値へ書き換えると
    // 18bitペイロードの該当6bitチャンク和が変わり、必ず検出される(mod64での不一致)。
    [Test]
    public void SingleDigitCorruption_InPayload_AlwaysDetected()
    {
        DirectionTransferCode.Payload payload = DirectionTransferCode.Payload.CreateEmpty();
        payload.Clear[0] = true;
        payload.Clear[5] = true;
        payload.Clear[11] = true;
        payload.NoMiss[2] = true;
        List<int> digits = DirectionTransferCode.EncodeDigits(payload);
        int payloadDigitCount = DirectionTransferCode.PayloadBits / 2; // 9

        for (int pos = 0; pos < payloadDigitCount; pos++)
        {
            int original = digits[pos];
            for (int alt = 0; alt < 4; alt++)
            {
                if (alt == original) continue;
                List<int> corrupted = new List<int>(digits);
                corrupted[pos] = alt;
                bool ok = DirectionTransferCode.TryDecode(corrupted, out _, out string error);
                Assert.IsFalse(ok, $"digit {pos} {original}->{alt} was NOT detected as corruption");
                Assert.AreEqual("コードがちがうようです", error);
            }
        }
    }

    // チェックサム桁自体(9..11)の破損も同様に検出される。
    [Test]
    public void SingleDigitCorruption_InChecksum_AlwaysDetected()
    {
        DirectionTransferCode.Payload payload = DirectionTransferCode.Payload.CreateEmpty();
        payload.Clear[3] = true;
        List<int> digits = DirectionTransferCode.EncodeDigits(payload);
        int payloadDigitCount = DirectionTransferCode.PayloadBits / 2;

        for (int pos = payloadDigitCount; pos < DirectionTransferCode.DigitCount; pos++)
        {
            int original = digits[pos];
            for (int alt = 0; alt < 4; alt++)
            {
                if (alt == original) continue;
                List<int> corrupted = new List<int>(digits);
                corrupted[pos] = alt;
                Assert.IsFalse(DirectionTransferCode.TryDecode(corrupted, out _, out _),
                    $"checksum digit {pos} {original}->{alt} was NOT detected as corruption");
            }
        }
    }

    [Test]
    public void TryDecode_WrongDigitCount_Rejected()
    {
        Assert.IsFalse(DirectionTransferCode.TryDecode(new List<int> { 0, 1, 2 }, out _, out string error));
        Assert.AreEqual("コードが正しくありません", error);
        Assert.IsFalse(DirectionTransferCode.TryDecode(null, out _, out _));
    }

    [Test]
    public void TryDecode_OutOfRangeDigitValue_Rejected()
    {
        List<int> digits = new List<int>(new int[DirectionTransferCode.DigitCount]);
        digits[0] = 4; // base-4 の範囲外
        Assert.IsFalse(DirectionTransferCode.TryDecode(digits, out _, out string error));
        Assert.AreEqual("コードが正しくありません", error);
    }

    [Test]
    public void FormatDigits_GroupsByFourWithSpaces()
    {
        List<int> digits = new List<int> { 0, 1, 2, 3, 0, 1, 2, 3, 0, 1, 2, 3 };
        string formatted = DirectionTransferCode.FormatDigits(digits);
        Assert.AreEqual("↑↓←→ ↑↓←→ ↑↓←→", formatted);
    }

    [Test]
    public void FormatDigits_PartialInput_NoTrailingGroupSeparatorIssue()
    {
        List<int> digits = new List<int> { 0, 1, 2 };
        string formatted = DirectionTransferCode.FormatDigits(digits);
        Assert.AreEqual("↑↓←", formatted);
    }
}

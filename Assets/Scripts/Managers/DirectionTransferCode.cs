using System;
using System.Collections.Generic;
using System.Text;

// 方向シーケンス方式の引き継ぎコード(Instructions/ranking-transfer/SPEC.md §1)。
// 記号は↑↓←→の4種のみ(base-4)。スティック入力とキーボードの矢印キー入力を
// そのままコードの1桁として使えるよう、値は 0..3 の int で表す(↑=0,↓=1,←=2,→=3)。
//
// ペイロード設計(18bit。SPEC §1.4 の例を実データに合わせて確定):
//   ステージクリアフラグ: StageOrder(4) x Difficulty(Easy/Normal/Lunatic=3) = 12bit
//   ノーミスクリアフラグ: StageOrder(4) x 1 = 4bit (難易度不問・そのステージを一度でも
//     被弾なしでクリアしたか)
//   予備: 2bit (将来拡張用。エンコードは常に0、デコードは無視する)
//   合計 18bit
// チェックサム: 6bit(18bitペイロードを6bitずつ3分割し加算・mod64。SPEC「単純和 mod64」)。
// 合計 24bit = 12桁(1桁=2bit)。SPEC上限の16桁(26bitペイロード)には現状収まるため未使用。
//
// 2P プレイの実績は引き継ぎ対象外(SPEC §1.4)。TransferAchievements が 1P 実績のみを
// 集積してこの Payload を組み立てる。
public static class DirectionTransferCode
{
    public const int StageCount = 4;
    public const int DifficultyCount = 3;
    public const int ClearBits = StageCount * DifficultyCount; // 12
    public const int NoMissBits = StageCount;                  // 4
    public const int ReservedBits = 2;
    public const int PayloadBits = ClearBits + NoMissBits + ReservedBits; // 18
    public const int ChecksumBits = 6;
    public const int TotalBits = PayloadBits + ChecksumBits; // 24
    public const int DigitCount = TotalBits / 2;             // 12 (2bit/桁)
    public const int DigitGroupSize = 4;                     // 表示は4桁区切り

    // コード上のステージ順(stageDirectoryName)。StageDataBase.VisibleOrder と現状一致するが、
    // 将来の並び替えで既発行コードが壊れないよう、ここで独立して固定する。
    public static readonly string[] StageOrder = { "captain", "stone", "vagrant", "mirror" };

    // ↑=0, ↓=1, ←=2, →=3 (SPEC §1.1)。
    public static readonly char[] Symbols = { '↑', '↓', '←', '→' };

    public struct Payload
    {
        // [stageIndex * DifficultyCount + difficultyIndex] = そのステージ・難易度を
        // 一度でもクリアしたか。
        public bool[] Clear;
        // [stageIndex] = そのステージを(難易度不問で)一度でもノーミスクリアしたか。
        public bool[] NoMiss;

        public static Payload CreateEmpty()
        {
            return new Payload { Clear = new bool[ClearBits], NoMiss = new bool[NoMissBits] };
        }
    }

    // digits(0..3, 長さ DigitCount)からコードを生成。矢印文字を4桁ごとに空白区切り。
    public static string Encode(Payload payload)
    {
        List<int> digits = EncodeDigits(payload);
        return FormatDigits(digits);
    }

    public static List<int> EncodeDigits(Payload payload)
    {
        List<int> bits = new List<int>(PayloadBits);
        for (int i = 0; i < ClearBits; i++)
            bits.Add((payload.Clear != null && i < payload.Clear.Length && payload.Clear[i]) ? 1 : 0);
        for (int i = 0; i < NoMissBits; i++)
            bits.Add((payload.NoMiss != null && i < payload.NoMiss.Length && payload.NoMiss[i]) ? 1 : 0);
        for (int i = 0; i < ReservedBits; i++) bits.Add(0);

        int checksum = ComputeChecksum(bits);
        WriteBits(bits, checksum, ChecksumBits);

        return BitsToDigits(bits);
    }

    // digits(0..3 の値、要素数は DigitCount 固定)を検証・復号する。
    // 桁数不一致/範囲外の値は「コードが正しくありません」、チェックサム不一致は
    // 「コードがちがうようです」("入力ミス"を示唆。全消去はしない=呼び出し側は
    // digits をそのまま保持してよい)。
    public static bool TryDecode(IReadOnlyList<int> digits, out Payload payload, out string error)
    {
        payload = Payload.CreateEmpty();
        error = null;

        if (digits == null || digits.Count != DigitCount)
        {
            error = "コードが正しくありません";
            return false;
        }
        foreach (int d in digits)
        {
            if (d < 0 || d > 3)
            {
                error = "コードが正しくありません";
                return false;
            }
        }

        List<int> bits = DigitsToBits(digits);
        List<int> payloadBits = bits.GetRange(0, PayloadBits);
        int storedChecksum = ReadBits(bits, PayloadBits, ChecksumBits);
        int calcChecksum = ComputeChecksum(payloadBits);
        if (storedChecksum != calcChecksum)
        {
            error = "コードがちがうようです";
            return false;
        }

        Payload result = Payload.CreateEmpty();
        for (int i = 0; i < ClearBits; i++) result.Clear[i] = payloadBits[i] != 0;
        for (int i = 0; i < NoMissBits; i++) result.NoMiss[i] = payloadBits[ClearBits + i] != 0;
        // 残り2bit(予備)は現状無視する。

        payload = result;
        return true;
    }

    // 表示用: digits(部分入力も可)を矢印文字へ変換し、4桁ごとに空白を挟む。
    public static string FormatDigits(IReadOnlyList<int> digits)
    {
        if (digits == null || digits.Count == 0) return string.Empty;
        StringBuilder sb = new StringBuilder(digits.Count + digits.Count / DigitGroupSize);
        for (int i = 0; i < digits.Count; i++)
        {
            if (i > 0 && i % DigitGroupSize == 0) sb.Append(' ');
            int d = digits[i];
            sb.Append(d >= 0 && d <= 3 ? Symbols[d] : '_');
        }
        return sb.ToString();
    }

    private static int ComputeChecksum(List<int> payloadBits)
    {
        // 18bit ペイロードを 6bit x 3 に分割して加算, mod 64(SPEC「単純和 mod64」)。
        int sum = 0;
        int chunks = PayloadBits / ChecksumBits;
        for (int c = 0; c < chunks; c++)
        {
            int v = 0;
            for (int b = 0; b < ChecksumBits; b++) v = (v << 1) | payloadBits[c * ChecksumBits + b];
            sum += v;
        }
        return sum & 0x3F;
    }

    private static List<int> BitsToDigits(List<int> bits)
    {
        List<int> digits = new List<int>(bits.Count / 2);
        for (int i = 0; i < bits.Count; i += 2)
        {
            digits.Add((bits[i] << 1) | bits[i + 1]);
        }
        return digits;
    }

    private static List<int> DigitsToBits(IReadOnlyList<int> digits)
    {
        List<int> bits = new List<int>(digits.Count * 2);
        foreach (int d in digits)
        {
            bits.Add((d >> 1) & 1);
            bits.Add(d & 1);
        }
        return bits;
    }

    private static void WriteBits(List<int> bits, int value, int count)
    {
        for (int i = count - 1; i >= 0; i--) bits.Add((value >> i) & 1);
    }

    private static int ReadBits(List<int> bits, int start, int count)
    {
        int value = 0;
        for (int i = 0; i < count; i++) value = (value << 1) | bits[start + i];
        return value;
    }
}

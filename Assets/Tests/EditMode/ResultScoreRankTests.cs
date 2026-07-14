using NUnit.Framework;

// ResultScreen の暫定スコア計算式と総合ランク判定を機械照合する。
// 便: 総合判定ゆるめ + スコア式差し替え（2026-07-15）で仕様変更したため追加。
//  - スコア = クリア +500,000 / カウンター ×20,000 / 被弾 ×10,000（clamp [0,999999]）
//  - ランク = 難易度別しきい値（S は全難易度で 0 被弾、未クリアは F 固定）
public class ResultScoreRankTests
{
    // ヘルパ: elapsed/end は式で未使用のはずなので任意値を渡す。
    private static int Score(bool cleared, int hit, int counter)
        => ResultScreen.CalculateProvisionalScore(cleared, hit, counter, 12.3f, 45.6f);

    [Test]
    public void ScoreAndRank_MatchSpec()
    {
        // === スコア式 ===
        // ユーザー提示の代表例: クリア + カウンター10 + 被弾3 = 500000 + 200000 - 30000。
        Assert.AreEqual(670000, Score(true, 3, 10), "cleared+counter10+hit3");
        // 未クリア・カウンター0・被弾0 → 0。
        Assert.AreEqual(0, Score(false, 0, 0), "failed baseline");
        // クリアのみ（被弾0/カウンター0）→ 500,000。
        Assert.AreEqual(500000, Score(true, 0, 0), "clear only");
        // カウンター加点（1発 = +20,000）。
        Assert.AreEqual(520000, Score(true, 0, 1), "clear+counter1");
        // 被弾減点（1発 = -10,000）。
        Assert.AreEqual(490000, Score(true, 1, 0), "clear+hit1");
        // 上限クランプ: 500000 + 30*20000 = 1,100,000 → 999,999。
        Assert.AreEqual(999999, Score(true, 0, 30), "upper clamp");
        // 下限クランプ: 500000 - 60*10000 = -100,000 → 0。
        Assert.AreEqual(0, Score(true, 60, 0), "lower clamp");
        // 未クリアでもカウンター加点は乗る（cleared 分の 500,000 のみ非加算）。
        Assert.AreEqual(100000, Score(false, 0, 5), "failed+counter5");
        // elapsed/end 引数は結果に影響しない（進行率係数を廃止したことの確認）。
        Assert.AreEqual(
            ResultScreen.CalculateProvisionalScore(true, 2, 4, 0f, 0f),
            ResultScreen.CalculateProvisionalScore(true, 2, 4, 999f, 5f),
            "elapsed/end must not affect score");
        // 負のカウンター/被弾はガード（Max(0, _)）。
        Assert.AreEqual(500000, Score(true, -3, -3), "negative inputs guarded");

        // === ランク（難易度別しきい値） ===
        const int EASY = 0, NORMAL = 1, LUNATIC = 2, HARD = 3;

        // 未クリアは全難易度で F。
        Assert.AreEqual("F", ResultScreen.EvaluateRank(false, 0, EASY), "fail=F easy");
        Assert.AreEqual("F", ResultScreen.EvaluateRank(false, 0, NORMAL), "fail=F normal");
        Assert.AreEqual("F", ResultScreen.EvaluateRank(false, 20, LUNATIC), "fail=F lunatic");

        // S は全難易度で 0 被弾のみ。
        Assert.AreEqual("S", ResultScreen.EvaluateRank(true, 0, EASY), "S easy");
        Assert.AreEqual("S", ResultScreen.EvaluateRank(true, 0, NORMAL), "S normal");
        Assert.AreEqual("S", ResultScreen.EvaluateRank(true, 0, LUNATIC), "S lunatic");

        // EASY: S=0 / A≤2 / B≤5 / C=6+
        Assert.AreEqual("A", ResultScreen.EvaluateRank(true, 1, EASY), "easy 1=A");
        Assert.AreEqual("A", ResultScreen.EvaluateRank(true, 2, EASY), "easy 2=A");
        Assert.AreEqual("B", ResultScreen.EvaluateRank(true, 3, EASY), "easy 3=B");
        Assert.AreEqual("B", ResultScreen.EvaluateRank(true, 5, EASY), "easy 5=B");
        Assert.AreEqual("C", ResultScreen.EvaluateRank(true, 6, EASY), "easy 6=C");

        // NORMAL: S=0 / A≤5 / B≤10 / C=11+
        Assert.AreEqual("A", ResultScreen.EvaluateRank(true, 3, NORMAL), "normal 3=A");
        Assert.AreEqual("A", ResultScreen.EvaluateRank(true, 5, NORMAL), "normal 5=A");
        Assert.AreEqual("B", ResultScreen.EvaluateRank(true, 6, NORMAL), "normal 6=B");
        Assert.AreEqual("B", ResultScreen.EvaluateRank(true, 10, NORMAL), "normal 10=B");
        Assert.AreEqual("C", ResultScreen.EvaluateRank(true, 11, NORMAL), "normal 11=C");

        // LUNATIC: S=0 / A≤8 / B≤15 / C=16+
        Assert.AreEqual("A", ResultScreen.EvaluateRank(true, 6, LUNATIC), "lunatic 6=A");
        Assert.AreEqual("A", ResultScreen.EvaluateRank(true, 8, LUNATIC), "lunatic 8=A");
        Assert.AreEqual("B", ResultScreen.EvaluateRank(true, 9, LUNATIC), "lunatic 9=B");
        Assert.AreEqual("B", ResultScreen.EvaluateRank(true, 15, LUNATIC), "lunatic 15=B");
        Assert.AreEqual("C", ResultScreen.EvaluateRank(true, 16, LUNATIC), "lunatic 16=C");

        // HARD は存在しない想定 → NORMAL 相当へフォールバック（防御的）。
        Assert.AreEqual("A", ResultScreen.EvaluateRank(true, 5, HARD), "hard→normal 5=A");
        Assert.AreEqual("B", ResultScreen.EvaluateRank(true, 10, HARD), "hard→normal 10=B");
        Assert.AreEqual("C", ResultScreen.EvaluateRank(true, 11, HARD), "hard→normal 11=C");
    }
}

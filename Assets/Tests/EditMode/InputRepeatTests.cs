using NUnit.Framework;

/// <summary>
/// HoldTrigger(長押し検出)/HoldRepeatTrigger(ホールドリピート)の純粋ロジックを固定する。
/// 引き継ぎ入力のB長押し(SPEC §1.2)・ランキングのイニシャル文字送り(SPEC §2.1)・
/// F2デバッグのランキング全消去の三箇所で共用するため、境界条件を個別に確認する。
/// </summary>
public class InputRepeatTests
{
    [Test]
    public void HoldTrigger_FiresOnceWhenThresholdCrossed()
    {
        HoldTrigger trigger = default;
        const float dt = 0.1f;
        const float threshold = 0.6f;
        bool fired = false;
        for (int i = 0; i < 10; i++) // 0.1s x 10 = 1.0s > 0.6s
        {
            if (trigger.Tick(true, dt, threshold))
            {
                Assert.IsFalse(fired, "fired more than once");
                fired = true;
            }
        }
        Assert.IsTrue(fired, "never fired despite exceeding threshold");
    }

    [Test]
    public void HoldTrigger_ReleasedBeforeThreshold_ResetsElapsedTime()
    {
        HoldTrigger trigger = default;
        const float dt = 0.1f;
        const float threshold = 0.6f;
        for (int i = 0; i < 3; i++) // 0.3s held(閾値未満)
        {
            Assert.IsFalse(trigger.Tick(true, dt, threshold));
        }
        Assert.IsFalse(trigger.Tick(false, dt, threshold)); // 離す→経過時間リセット
        Assert.AreEqual(0f, trigger.HeldSeconds);

        // 離した直後に再度押しても、最初から数え直すので即座には発火しない
        // (0.3s 分の経過が持ち越されるなら 3 ステップ目で 0.6s に達して発火してしまう)。
        for (int i = 0; i < 3; i++)
        {
            Assert.IsFalse(trigger.Tick(true, dt, threshold));
        }
    }

    [Test]
    public void HoldTrigger_ResetAllowsRefire()
    {
        HoldTrigger trigger = default;
        Assert.IsTrue(trigger.Tick(true, 1.0f, 0.6f));
        Assert.IsFalse(trigger.Tick(true, 0.1f, 0.6f)); // 既発火なので連続しては鳴らない
        Assert.IsFalse(trigger.Tick(false, 0.1f, 0.6f)); // 離す
        Assert.IsTrue(trigger.Tick(true, 1.0f, 0.6f));   // 再度押し直せば再発火
    }

    [Test]
    public void HoldRepeatTrigger_FiresImmediatelyOnPressEdge()
    {
        HoldRepeatTrigger repeat = default;
        Assert.IsTrue(repeat.Tick(pressedEdge: true, held: true, dt: 0f, initialDelay: 0.4f, repeatInterval: 0.12f));
    }

    [Test]
    public void HoldRepeatTrigger_NoRepeatBeforeInitialDelay()
    {
        HoldRepeatTrigger repeat = default;
        Assert.IsTrue(repeat.Tick(true, true, 0f, 0.4f, 0.12f)); // 押した瞬間
        Assert.IsFalse(repeat.Tick(false, true, 0.2f, 0.4f, 0.12f)); // 0.2s < 0.4s
    }

    [Test]
    public void HoldRepeatTrigger_RepeatsAtIntervalAfterInitialDelay()
    {
        HoldRepeatTrigger repeat = default;
        Assert.IsTrue(repeat.Tick(true, true, 0f, 0.4f, 0.12f)); // t=0 押下
        int fires = 0;
        // t=0.05刻みで1.0s分進める。初回0.4s後+以後0.12s毎に発火するはず。
        for (int i = 0; i < 20; i++)
        {
            if (repeat.Tick(false, true, 0.05f, 0.4f, 0.12f)) fires++;
        }
        Assert.Greater(fires, 0, "should repeat-fire while held past the initial delay");
    }

    [Test]
    public void HoldRepeatTrigger_StopsWhenReleased()
    {
        HoldRepeatTrigger repeat = default;
        repeat.Tick(true, true, 0f, 0.4f, 0.12f);
        Assert.IsFalse(repeat.Tick(false, false, 1.0f, 0.4f, 0.12f)); // 離した状態では鳴らない
        Assert.IsFalse(repeat.Tick(false, false, 1.0f, 0.4f, 0.12f));
    }
}

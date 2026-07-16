using NUnit.Framework;

/// <summary>
/// InputManager.TryConsumeDirection(方向シーケンス入力の150msクールダウン消費)を固定する。
/// 引き継ぎコード入力(SPEC §1.3: 「連続入力防止に150ms程度のリピート抑制」)で使う。
/// </summary>
public class InputManagerDirectionTests
{
    [Test]
    public void FirstEdge_IsConsumedImmediately()
    {
        float cooldown = 0f;
        int digit = InputManager.TryConsumeDirection(true, false, false, false, 0f, ref cooldown);
        Assert.AreEqual(0, digit); // up=0
        Assert.Greater(cooldown, 0f);
    }

    [Test]
    public void SecondEdge_WithinCooldown_IsIgnored()
    {
        float cooldown = 0f;
        InputManager.TryConsumeDirection(true, false, false, false, 0f, ref cooldown);
        int digit = InputManager.TryConsumeDirection(false, true, false, false, 0.05f, ref cooldown); // 50ms後(<150ms)
        Assert.AreEqual(-1, digit);
    }

    [Test]
    public void EdgeAfterCooldownElapsed_IsConsumed()
    {
        float cooldown = 0f;
        InputManager.TryConsumeDirection(true, false, false, false, 0f, ref cooldown);
        int digit = InputManager.TryConsumeDirection(false, true, false, false, 0.2f, ref cooldown); // 200ms後(>150ms)
        Assert.AreEqual(1, digit); // down=1
    }

    [Test]
    public void NoEdge_ReturnsNegativeOne()
    {
        float cooldown = 0f;
        int digit = InputManager.TryConsumeDirection(false, false, false, false, 0.1f, ref cooldown);
        Assert.AreEqual(-1, digit);
    }

    [Test]
    public void Priority_UpBeatsDownLeftRight()
    {
        float cooldown = 0f;
        int digit = InputManager.TryConsumeDirection(true, true, true, true, 0f, ref cooldown);
        Assert.AreEqual(0, digit);
    }

    [Test]
    public void DigitMapping_MatchesDirectionOrder()
    {
        // up=0, down=1, left=2, right=3 (DirectionTransferCode.Symbols と同順)。
        float cooldown = 0f;
        Assert.AreEqual(2, InputManager.TryConsumeDirection(false, false, true, false, 1f, ref cooldown));
        cooldown = 0f;
        Assert.AreEqual(3, InputManager.TryConsumeDirection(false, false, false, true, 1f, ref cooldown));
    }
}

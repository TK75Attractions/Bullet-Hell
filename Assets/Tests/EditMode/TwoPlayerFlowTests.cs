using NUnit.Framework;

public class TwoPlayerFlowTests
{
    [TestCase(false, true, false, true)]
    [TestCase(false, false, true, false)]
    [TestCase(true, true, false, false)]
    [TestCase(true, false, true, false)]
    [TestCase(true, true, true, true)]
    public void TutorialStepComplete_WaitsForRequiredPlayers(
        bool twoPlayer, bool p1Complete, bool p2Complete, bool expected)
    {
        Assert.AreEqual(expected,
            TutorialManager.IsTutorialStepComplete(twoPlayer, p1Complete, p2Complete));
    }

    [Test]
    public void StartPositions_CanSwapPlayerSides()
    {
        Assert.AreEqual(14f, GManager.GetPlayerStartPosition(0, false).x, 0.001f);
        Assert.AreEqual(18f, GManager.GetPlayerStartPosition(1, false).x, 0.001f);
        Assert.AreEqual(18f, GManager.GetPlayerStartPosition(0, true).x, 0.001f);
        Assert.AreEqual(14f, GManager.GetPlayerStartPosition(1, true).x, 0.001f);
    }

    [Test]
    public void ResultSides_FollowPlayerSideSetting()
    {
        Assert.AreEqual(0, ResultScreen.PlayerIndexForResultSide(false, false));
        Assert.AreEqual(1, ResultScreen.PlayerIndexForResultSide(true, false));
        Assert.AreEqual(1, ResultScreen.PlayerIndexForResultSide(false, true));
        Assert.AreEqual(0, ResultScreen.PlayerIndexForResultSide(true, true));
    }
}
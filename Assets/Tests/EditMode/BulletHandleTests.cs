using NUnit.Framework;

/// <summary>
/// Pure tests for the S4 generation-stamped <see cref="BulletHandle"/>. No Unity
/// runtime required: the validity rule is a plain function of (generation, length).
/// </summary>
public class BulletHandleTests
{
    [Test]
    public void ValidHandle_ResolvesWithinRangeAndGeneration()
    {
        var h = new BulletHandle(3, 7);
        Assert.IsTrue(h.IsValidFor(currentGeneration: 7, listLength: 10));
    }

    [Test]
    public void StaleGeneration_IsInvalid()
    {
        var h = new BulletHandle(3, 7);
        // A clear bumped the generation to 8: the old handle must not resolve.
        Assert.IsFalse(h.IsValidFor(currentGeneration: 8, listLength: 10));
    }

    [Test]
    public void OutOfRangeIndex_IsInvalid()
    {
        var h = new BulletHandle(12, 7);
        Assert.IsFalse(h.IsValidFor(currentGeneration: 7, listLength: 10));
    }

    [Test]
    public void Invalid_HasNegativeIndexAndNeverResolves()
    {
        Assert.AreEqual(-1, BulletHandle.Invalid.Index);
        Assert.IsFalse(BulletHandle.Invalid.IsSlotAssigned);
        Assert.IsFalse(BulletHandle.Invalid.IsValidFor(-1, 100));
    }

    [Test]
    public void Equality_MatchesOnIndexAndGeneration()
    {
        Assert.AreEqual(new BulletHandle(2, 5), new BulletHandle(2, 5));
        Assert.AreNotEqual(new BulletHandle(2, 5), new BulletHandle(2, 6));
    }
}

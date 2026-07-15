using NUnit.Framework;

public class QuadOrderCollisionRegistrationTests
{
    [Test]
    public void CollisionRegistration_SkipsBulletsBeforeAppearance()
    {
        BulletData bullet = new BulletData
        {
            isActive = true,
            time = 1f,
            appearTime = 2f
        };

        Assert.IsFalse(QuadOrder.ShouldRegisterBulletForCollision(bullet));

        bullet.time = 2f;
        Assert.IsTrue(QuadOrder.ShouldRegisterBulletForCollision(bullet));
    }

    [Test]
    public void CollisionRegistration_SkipsInactiveAndClearingBullets()
    {
        BulletData bullet = new BulletData
        {
            isActive = false,
            time = 2f,
            appearTime = 1f
        };

        Assert.IsFalse(QuadOrder.ShouldRegisterBulletForCollision(bullet));

        bullet.isActive = true;
        bullet.isClearing = true;
        Assert.IsFalse(QuadOrder.ShouldRegisterBulletForCollision(bullet));
    }
}

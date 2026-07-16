using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

/// <summary>
/// SPEC-RUNTIME-V2.md P1-b (リアルタイム自機狙い) の検証。
/// polarForm.y を現在角として毎フレーム homingTurnRate*dt だけ自機方向へ旋回し、
/// homingDuration 経過後は最後の方向のまま直進する(BulletV2UpdateJob.Update 参照)。
/// </summary>
public class BulletV2HomingMotionTests
{
    private static QuadGrid CreateGrid() => new QuadGrid(new float2(-2f, -2f), 1f, 64, 4096);

    private static BulletData MakeHomingBullet(float turnRate, float duration, float speed = 1f)
    {
        return new BulletData
        {
            position = float2.zero,
            originPos = float2.zero,
            originVlc = float2.zero,
            polarForm = new float2(1f, 0f), // 初期角0(+X方向)
            scale = new float2(1f, 1f),
            speed = speed,
            life = 100f,
            appearTime = 0f,
            isActive = true,
            homingTurnRate = turnRate,
            homingDuration = duration
        };
    }

    private static float RunFrame(ref NativeArray<BulletData> arr, float dt, float2 playerPos)
    {
        BulletV2UpdateJob job = new BulletV2UpdateJob
        {
            bullets = arr,
            dt = dt,
            grid = CreateGrid(),
            playerVelocity = float2.zero,
            playerPosition = playerPos
        };
        job.Execute(0);
        return arr[0].polarForm.y;
    }

    [Test]
    public void HasV2Motion_RequiresBothTurnRateAndDuration()
    {
        Assert.IsFalse(MakeHomingBullet(0f, 1f).HasV2Motion);
        Assert.IsFalse(MakeHomingBullet(1f, 0f).HasV2Motion);
        Assert.IsTrue(MakeHomingBullet(1f, 1f).HasV2Motion);
    }

    [Test]
    public void Homing_TurnIsClampedToTurnRatePerFrame()
    {
        const float dt = 1f / 60f;
        const float turnRate = 1f; // rad/s
        BulletData bullet = MakeHomingBullet(turnRate, duration: 10f);
        // 遠方(0, 1e6)に固定した自機: 弾の移動量に対して角度がほぼ一定(pi/2)であり続ける。
        float2 farAbovePlayer = new float2(0f, 1e6f);

        NativeArray<BulletData> arr = new NativeArray<BulletData>(1, Allocator.Temp);
        arr[0] = bullet;
        try
        {
            float angleAfterFrame1 = RunFrame(ref arr, dt, farAbovePlayer);
            Assert.AreEqual(turnRate * dt, angleAfterFrame1, 1e-5f);

            float angleAfterFrame2 = RunFrame(ref arr, dt, farAbovePlayer);
            Assert.AreEqual(turnRate * dt * 2f, angleAfterFrame2, 1e-5f);
        }
        finally
        {
            arr.Dispose();
        }
    }

    [Test]
    public void Homing_LargeTurnRateSnapsFullyToTargetInOneFrame()
    {
        const float dt = 1f / 60f;
        BulletData bullet = MakeHomingBullet(turnRate: 1000f, duration: 10f);
        float2 directlyAbove = new float2(0f, 1e6f);

        NativeArray<BulletData> arr = new NativeArray<BulletData>(1, Allocator.Temp);
        arr[0] = bullet;
        try
        {
            RunFrame(ref arr, dt, directlyAbove);
            Assert.AreEqual(math.PI / 2f, arr[0].polarForm.y, 1e-4f);

            float2 velocity = arr[0].velocity;
            float velocityAngle = math.atan2(velocity.y, velocity.x);
            Assert.AreEqual(math.PI / 2f, velocityAngle, 1e-4f);
        }
        finally
        {
            arr.Dispose();
        }
    }

    [Test]
    public void Homing_FreezesDirectionAfterDurationElapses()
    {
        const float dt = 1f / 60f;
        const float turnRate = 1f;
        // duration をフレーム2.5個分に設定: lapse=dt,2dt は旋回対象、lapse=3dt以降は対象外。
        float duration = 2.5f * dt;
        BulletData bullet = MakeHomingBullet(turnRate, duration);
        float2 farAbovePlayer = new float2(0f, 1e6f);

        NativeArray<BulletData> arr = new NativeArray<BulletData>(1, Allocator.Temp);
        arr[0] = bullet;
        try
        {
            RunFrame(ref arr, dt, farAbovePlayer); // lapse=dt (旋回する)
            RunFrame(ref arr, dt, farAbovePlayer); // lapse=2dt (旋回する)
            float angleAtFreezeStart = arr[0].polarForm.y;
            Assert.AreEqual(turnRate * dt * 2f, angleAtFreezeStart, 1e-5f);

            float angleFrame3 = RunFrame(ref arr, dt, farAbovePlayer); // lapse=3dt (対象外)
            float angleFrame4 = RunFrame(ref arr, dt, farAbovePlayer); // lapse=4dt (対象外)
            float angleFrame5 = RunFrame(ref arr, dt, farAbovePlayer);

            Assert.AreEqual(angleAtFreezeStart, angleFrame3, 1e-6f);
            Assert.AreEqual(angleAtFreezeStart, angleFrame4, 1e-6f);
            Assert.AreEqual(angleAtFreezeStart, angleFrame5, 1e-6f);
        }
        finally
        {
            arr.Dispose();
        }
    }

    [Test]
    public void Homing_MovesForwardEachFrame()
    {
        const float dt = 1f / 60f;
        BulletData bullet = MakeHomingBullet(turnRate: 0f, duration: 0f, speed: 4f);
        // turnRate/duration が0でもHasV2Motionはfalseになる組み合わせなので、
        // ここでは直接 Update 相当の直進(speed分の前進)だけを確認したいために非0にする。
        bullet.homingTurnRate = 0.0001f;
        bullet.homingDuration = 0.0001f;

        NativeArray<BulletData> arr = new NativeArray<BulletData>(1, Allocator.Temp);
        arr[0] = bullet;
        try
        {
            RunFrame(ref arr, dt, float2.zero);
            float2 expected = new float2(4f * dt, 0f); // 角0のまま speed*dt だけ+X方向へ
            Assert.Less(math.distance(expected, arr[0].position), 1e-4f);
        }
        finally
        {
            arr.Dispose();
        }
    }

    [Test]
    public void Segments_TakePriorityOverHoming_WhenBothPresent()
    {
        BulletData bullet = MakeHomingBullet(turnRate: 1000f, duration: 10f);
        bullet.v2Segments.Add(new BulletV2Segment { duration = 0f, vlc = new float2(3f, 0f) });

        NativeArray<BulletData> arr = new NativeArray<BulletData>(1, Allocator.Temp);
        arr[0] = bullet;
        try
        {
            const float dt = 1f / 60f;
            BulletV2UpdateJob job = new BulletV2UpdateJob
            {
                bullets = arr,
                dt = dt,
                grid = CreateGrid(),
                playerVelocity = float2.zero,
                playerPosition = new float2(0f, 1e6f)
            };
            job.Execute(0);

            // homing(旋回1000rad/s)ではなく segments(直進vlc=(3,0))の結果になっているはず。
            Assert.Less(math.distance(new float2(3f * dt, 0f), arr[0].position), 1e-4f);
            // polarForm.y は homing 側の状態なので、segments優先時は変化しない。
            Assert.AreEqual(0f, arr[0].polarForm.y);
        }
        finally
        {
            arr.Dispose();
        }
    }
}

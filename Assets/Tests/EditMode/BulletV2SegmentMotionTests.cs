using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.TestTools;

/// <summary>
/// SPEC-RUNTIME-V2.md P1-a (segments[]) の運動モデル検証。
/// BulletV2UpdateJob.Execute を直接呼び、bullet.time を明示的に差し替えることで
/// 「経過時間(lapse)= L」の瞬間だけを厳密にサンプリングする(フレーム刻みのdtに依存しない)。
/// </summary>
public class BulletV2SegmentMotionTests
{
    private const string BulletTypeDatabasePath = "Assets/Scripts/Bullets/BulletTypes/BulletTypeDataBase.asset";
    private const string EnemyDatabasePath = "Assets/Scripts/Enemies/Enemies/EnemyDataBase.asset";

    private static QuadGrid CreateGrid() => new QuadGrid(new float2(-2f, -2f), 1f, 64, 4096);

    private static float2 PositionAtLapse(BulletData bullet, float lapse)
    {
        bullet.time = bullet.appearTime + lapse;
        NativeArray<BulletData> arr = new NativeArray<BulletData>(1, Allocator.Temp);
        arr[0] = bullet;
        try
        {
            BulletV2UpdateJob job = new BulletV2UpdateJob
            {
                bullets = arr,
                dt = 0f,
                grid = CreateGrid(),
                playerVelocity = float2.zero,
                playerPosition = float2.zero
            };
            job.Execute(0);
            return arr[0].position;
        }
        finally
        {
            arr.Dispose();
        }
    }

    private static BulletData MakeBaseBullet()
    {
        return new BulletData
        {
            position = float2.zero,
            originPos = float2.zero,
            originVlc = float2.zero,
            polarForm = new float2(1f, 0f),
            scale = new float2(1f, 1f),
            life = 100f,
            appearTime = 0f,
            isActive = true
        };
    }

    [Test]
    public void HasV2Motion_FalseWhenNoSegmentsOrHoming()
    {
        BulletData bullet = MakeBaseBullet();
        Assert.IsFalse(bullet.HasV2Motion);
    }

    [Test]
    public void HasV2Motion_TrueWhenSegmentsPresent()
    {
        BulletData bullet = MakeBaseBullet();
        bullet.v2Segments.Add(new BulletV2Segment { duration = 0f, vlc = new float2(1f, 0f) });
        Assert.IsTrue(bullet.HasV2Motion);
    }

    [Test]
    public void StraightInfiniteSegment_MatchesLinearFormula()
    {
        BulletData bullet = MakeBaseBullet();
        bullet.v2Segments.Add(new BulletV2Segment { duration = 0f, vlc = new float2(2f, -1f) });

        float2 pos = PositionAtLapse(bullet, 3f);

        AssertApprox(new float2(6f, -3f), pos);
    }

    [Test]
    public void RotatingSegment_HalfTurn_MatchesClosedFormArc()
    {
        // omega=pi rad/s, v0=(1,0): 1秒でちょうど半回転する円弧。
        // 閉形式: dx = v0.x*sin(wt)/w, dy = v0.x*(1-cos(wt))/w (v0.y=0のため)
        BulletData bullet = MakeBaseBullet();
        bullet.v2Segments.Add(new BulletV2Segment { duration = 0f, vlc = new float2(1f, 0f), thetaVlc = math.PI });

        float2 pos = PositionAtLapse(bullet, 1f);

        AssertApprox(new float2(0f, 2f / math.PI), pos);
    }

    [Test]
    public void RotatingSegment_QuarterTurn_MatchesClosedFormArc()
    {
        BulletData bullet = MakeBaseBullet();
        bullet.v2Segments.Add(new BulletV2Segment { duration = 0f, vlc = new float2(1f, 0f), thetaVlc = math.PI / 2f });

        float2 pos = PositionAtLapse(bullet, 1f);

        AssertApprox(new float2(2f / math.PI, 2f / math.PI), pos);
    }

    [Test]
    public void GravitySegment_MatchesProjectileFormula()
    {
        BulletData bullet = MakeBaseBullet();
        bullet.v2Segments.Add(new BulletV2Segment
        {
            duration = 0f,
            vlc = float2.zero,
            gravity = new float2(10f, -math.PI / 2f) // 大きさ10, 下向き(-Y)
        });

        float2 pos = PositionAtLapse(bullet, 1f);

        // disp = gravityDir * (mag * t^2 / 2) = (0,-1) * (10*1/2) = (0,-5)
        AssertApprox(new float2(0f, -5f), pos);
    }

    [Test]
    public void SegmentBoundary_PositionIsContinuous()
    {
        // segment0: 1秒間 vlc=(2,0)。segment1: 以降ずっと vlc=(0,3)(infinite)。
        // 境界(lapse=1.0)をまたいで、閉形式の区分線形と厳密一致することを複数点で確認する
        // = 境界で位置が連続であることの証明(速度は不連続でよい/むしろ意図通り)。
        BulletData bullet = MakeBaseBullet();
        bullet.v2Segments.Add(new BulletV2Segment { duration = 1f, vlc = new float2(2f, 0f) });
        bullet.v2Segments.Add(new BulletV2Segment { duration = 0f, vlc = new float2(0f, 3f) });

        float[] lapses = { 0f, 0.5f, 0.999f, 1.0f, 1.001f, 1.5f };
        foreach (float lapse in lapses)
        {
            float2 expected = lapse <= 1f
                ? new float2(2f * lapse, 0f)
                : new float2(2f, 3f * (lapse - 1f));

            float2 actual = PositionAtLapse(bullet, lapse);
            AssertApprox(expected, actual, $"lapse={lapse}");
        }
    }

    [Test]
    public void SegmentBoundary_ThreeSegments_ChainWithoutJump()
    {
        BulletData bullet = MakeBaseBullet();
        bullet.v2Segments.Add(new BulletV2Segment { duration = 0.5f, vlc = new float2(4f, 0f) });
        bullet.v2Segments.Add(new BulletV2Segment { duration = 0.5f, vlc = new float2(0f, 4f) });
        bullet.v2Segments.Add(new BulletV2Segment { duration = 0f, vlc = new float2(-4f, 0f) });

        float2 atFirstBoundary = PositionAtLapse(bullet, 0.5f);
        AssertApprox(new float2(2f, 0f), atFirstBoundary, "first boundary");

        float2 atSecondBoundary = PositionAtLapse(bullet, 1.0f);
        AssertApprox(new float2(2f, 2f), atSecondBoundary, "second boundary");

        float2 afterSecondBoundary = PositionAtLapse(bullet, 1.25f);
        AssertApprox(new float2(1f, 2f), afterSecondBoundary, "past second boundary");
    }

    [Test]
    public void V1Job_SkipsV2Bullet()
    {
        BulletData bullet = MakeBaseBullet();
        bullet.v2Segments.Add(new BulletV2Segment { duration = 0f, vlc = new float2(5f, 0f) });
        bullet.position = new float2(1f, 2f);

        NativeArray<BulletData> arr = new NativeArray<BulletData>(1, Allocator.Temp);
        arr[0] = bullet;
        try
        {
            BulletDataUpdateJob job = new BulletDataUpdateJob
            {
                bullets = arr,
                dt = 1f / 60f,
                grid = CreateGrid(),
                playerVelocity = float2.zero
            };
            job.Execute(0);

            Assert.AreEqual(new float2(1f, 2f), arr[0].position);
            Assert.AreEqual(0f, arr[0].time);
        }
        finally
        {
            arr.Dispose();
        }
    }

    [Test]
    public void V2Job_SkipsNonV2Bullet()
    {
        BulletData bullet = MakeBaseBullet();
        bullet.position = new float2(1f, 2f);
        bullet.speed = 5f;
        Assert.IsFalse(bullet.HasV2Motion);

        NativeArray<BulletData> arr = new NativeArray<BulletData>(1, Allocator.Temp);
        arr[0] = bullet;
        try
        {
            BulletV2UpdateJob job = new BulletV2UpdateJob
            {
                bullets = arr,
                dt = 1f / 60f,
                grid = CreateGrid(),
                playerVelocity = float2.zero,
                playerPosition = float2.zero
            };
            job.Execute(0);

            Assert.AreEqual(new float2(1f, 2f), arr[0].position);
            Assert.AreEqual(0f, arr[0].time);
        }
        finally
        {
            arr.Dispose();
        }
    }

    [Test]
    public void BulletBufferJson_Segments_ParsedIntoV2Segments()
    {
        string json = "{\"name\":\"test\",\"bullets\":[{\"typeName\":\"tear\",\"segments\":[" +
            "{\"duration\":1,\"vlc\":{\"x\":2,\"y\":0}},{\"duration\":0,\"vlc\":{\"x\":0,\"y\":3},\"thetaVlc\":1.5}]}]}";

        List<BulletData> bullets = ParseBullets(json);

        Assert.IsTrue(bullets[0].HasV2Motion);
        Assert.AreEqual(2, bullets[0].v2Segments.Length);
        Assert.AreEqual(1f, bullets[0].v2Segments[0].duration);
        Assert.AreEqual(new float2(2f, 0f), bullets[0].v2Segments[0].vlc);
        Assert.AreEqual(1.5f, bullets[0].v2Segments[1].thetaVlc);
    }

    [Test]
    public void BulletBufferJson_Homing_ParsedIntoHomingFields()
    {
        string json = "{\"name\":\"test\",\"bullets\":[{\"typeName\":\"tear\",\"homingTurnRate\":2.5,\"homingDuration\":1.2}]}";

        List<BulletData> bullets = ParseBullets(json);

        Assert.IsTrue(bullets[0].HasV2Motion);
        Assert.AreEqual(2.5f, bullets[0].homingTurnRate);
        Assert.AreEqual(1.2f, bullets[0].homingDuration);
    }

    [Test]
    public void BulletBufferJson_OmittedV2Fields_HasV2MotionFalse()
    {
        List<BulletData> bullets = ParseBullets("{\"name\":\"test\",\"bullets\":[{\"typeName\":\"tear\"}]}");

        Assert.IsFalse(bullets[0].HasV2Motion);
        Assert.AreEqual(0, bullets[0].v2Segments.Length);
    }

    [Test]
    public void ResolveV2Segments_TruncatesBeyondCapacity_WithWarning()
    {
        int capacity = new FixedList128Bytes<BulletV2Segment>().Capacity;
        var sb = new System.Text.StringBuilder();
        sb.Append("{\"name\":\"test\",\"bullets\":[{\"typeName\":\"tear\",\"segments\":[");
        for (int i = 0; i < capacity + 2; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append("{\"duration\":0.1,\"vlc\":{\"x\":1,\"y\":0}}");
        }
        sb.Append("]}]}");

        LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("exceeds capacity"));
        List<BulletData> bullets = ParseBullets(sb.ToString());

        Assert.AreEqual(capacity, bullets[0].v2Segments.Length);
    }

    private static void AssertApprox(float2 expected, float2 actual, string context = null)
    {
        float distance = math.distance(expected, actual);
        Assert.Less(distance, 1e-4f,
            $"{context}: expected {expected} but was {actual} (distance {distance})");
    }

    private static List<BulletData> ParseBullets(string json)
    {
        using (new EditorStageProbe(BulletTypeDatabasePath, EnemyDatabasePath))
        {
            MethodInfo readMethod = typeof(BulletBufferManager).GetMethod(
                "ReadBulletBufferFromJson",
                BindingFlags.Instance | BindingFlags.NonPublic);
            object buffer = readMethod.Invoke(new BulletBufferManager(), new object[] { "test.json", json });
            FieldInfo bulletsField = buffer.GetType().GetField("bullets",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return (List<BulletData>)bulletsField.GetValue(buffer);
        }
    }
}

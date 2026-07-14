using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

public class WarpableBulletTests
{
    private const string BulletTypeDatabasePath = "Assets/Scripts/Bullets/BulletTypes/BulletTypeDataBase.asset";
    private const string EnemyDatabasePath = "Assets/Scripts/Enemies/Enemies/EnemyDataBase.asset";

    [Test]
    public void BulletBufferJson_OmittedWarpable_DefaultsToTrue()
    {
        List<BulletData> bullets = ParseBullets("{\"name\":\"test\",\"bullets\":[{\"typeName\":\"tear\"}]}");

        Assert.IsTrue(bullets[0].warpable);
    }

    [Test]
    public void BulletBufferJson_WarpableFalse_IsPreserved()
    {
        List<BulletData> bullets = ParseBullets("{\"name\":\"test\",\"bullets\":[{\"typeName\":\"tear\",\"warpable\":false}]}");

        Assert.IsFalse(bullets[0].warpable);
    }

    [Test]
    public void WarpJob_SkipsBulletMarkedNotWarpable()
    {
        NativeArray<BulletData> bullets = new NativeArray<BulletData>(1, Allocator.Temp);
        NativeArray<BulletData> zones = new NativeArray<BulletData>(2, Allocator.Temp);
        try
        {
            bullets[0] = CreateBullet(new float2(1f, 1f), warpable: false);
            zones[0] = CreateZone(new float2(1f, 1f));
            zones[1] = CreateZone(new float2(10f, 1f));

            CreateWarpJob(bullets, zones).Execute(0);

            Assert.AreEqual(new float2(1f, 1f), bullets[0].position);
            Assert.AreEqual(0f, bullets[0].warpCooldown);
        }
        finally
        {
            bullets.Dispose();
            zones.Dispose();
        }
    }

    private static List<BulletData> ParseBullets(string json)
    {
        using (new EditorStageProbe(BulletTypeDatabasePath, EnemyDatabasePath))
        {
            MethodInfo readMethod = typeof(BulletBufferManager).GetMethod(
                "ReadBulletBufferFromJson",
                BindingFlags.Instance | BindingFlags.NonPublic);
            object buffer = readMethod.Invoke(new BulletBufferManager(), new object[] { "test.json", json });
            // BulletBuffer.bullets は public フィールド(main/feature 両方)なので Public を含める。
            // NonPublic のみだと GetField が null を返し NRE になる(main 由来テストの既存不具合を統合時に修正)。
            FieldInfo bulletsField = buffer.GetType().GetField("bullets",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return (List<BulletData>)bulletsField.GetValue(buffer);
        }
    }

    private static WarpBulletJob CreateWarpJob(NativeArray<BulletData> bullets, NativeArray<BulletData> zones)
    {
        return new WarpBulletJob
        {
            bullets = bullets,
            warpZones = zones,
            dt = 0.1f,
            warpCooldown = 3f,
            grid = new QuadGrid(new float2(-2f, -2f), 1f, 64, 4096),
            reflectXTypeId = -1,
            reflectYTypeId = -1
        };
    }

    private static BulletData CreateBullet(float2 position, bool warpable)
    {
        return new BulletData
        {
            position = position,
            velocity = new float2(0.1f, 0f),
            originPos = position,
            originVlc = new float2(1f, 0f),
            scale = new float2(1f, 1f),
            polarForm = new float2(1f, 0f),
            isActive = true,
            warpable = warpable
        };
    }

    private static BulletData CreateZone(float2 position)
    {
        return new BulletData
        {
            position = position,
            scale = new float2(4f, 4f),
            polarForm = new float2(1f, 0f),
            isActive = true
        };
    }
}

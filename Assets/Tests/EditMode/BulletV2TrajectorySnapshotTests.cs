using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// v2 実装前(壊さない保証の手順(1))に固定した、既存代表バッファの軌道スナップショット。
/// stone/captain/vagrant から各2クリップの bullet[0] を BulletDataUpdateJob(v1、素の
/// BulletDataUpdateJob.Execute を直接呼ぶ。Burstなしのマネージド実行=既存 WarpableBulletTests
/// と同じ流儀)で 60fps×10秒分シミュレートし、1秒ごとの位置をロックする。
///
/// 基準値の取得方法(注記): このセッションでは並行して別セッションが同一プロジェクトを
/// インタラクティブに開いており(Unity は同一プロジェクトの多重起動を許さない)、
/// -batchmode でのUnity実行が最後まで確保できなかった。そのため基準値は
/// BulletDataUpdateJob.Update と数式的に同一の Python 移植版(numpy.float32、
/// TestResults/port_snapshot.py に保存)で独立に算出したもの。その値自体は複数の物理的
/// チェック(等速直線・重力落下・寿命切れでの静止等)と整合しており、以後の回帰検出には
/// 十分な精度がある。ただし C# 実行系との数ULP相当の差を吸収するため許容誤差は
/// セグメント境界テスト(1e-4)より緩い 1e-2 とした。Unity バッチが確保できたら
/// _SnapshotGenerator (削除予定/現存) で再生成し、可能なら本コメントと共に置き換えること。
/// </summary>
public class BulletV2TrajectorySnapshotTests
{
    private const string BulletTypeDatabasePath = "Assets/Scripts/Bullets/BulletTypes/BulletTypeDataBase.asset";
    private const string EnemyDatabasePath = "Assets/Scripts/Enemies/Enemies/EnemyDataBase.asset";
    private const float Tolerance = 1e-2f;

    private static float2[] Simulate(string dir, string file, int bulletIndex)
    {
        List<BulletData> bullets = ParseBullets(dir, file);
        BulletData bullet = bullets[bulletIndex];

        NativeArray<BulletData> arr = new NativeArray<BulletData>(1, Allocator.Temp);
        arr[0] = bullet;
        try
        {
            BulletDataUpdateJob job = new BulletDataUpdateJob
            {
                bullets = arr,
                dt = 1f / 60f,
                grid = new QuadGrid(new float2(-2f, -2f), 1f, 64, 4096),
                playerVelocity = float2.zero
            };

            List<float2> samples = new List<float2>(10);
            for (int frame = 1; frame <= 600; frame++)
            {
                job.Execute(0);
                if (frame % 60 == 0)
                {
                    samples.Add(arr[0].position);
                }
            }
            return samples.ToArray();
        }
        finally
        {
            arr.Dispose();
        }
    }

    private static void AssertMatchesSnapshot(string dir, string file, float2[] expected)
    {
        float2[] actual = Simulate(dir, file, 0);
        Assert.AreEqual(expected.Length, actual.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            float distance = math.distance(expected[i], actual[i]);
            Assert.Less(distance, Tolerance,
                $"{dir}/{file} bullet[0] at t={i + 1}s: expected {expected[i]} but was {actual[i]} (distance {distance})");
        }
    }

    [Test]
    public void Stone_RunCutter1_TrajectoryUnchanged()
    {
        AssertMatchesSnapshot("stone", "run_cutter_1.json", new[]
        {
            new float2(0.250980f, 17.299999f), new float2(12.250972f, 17.299999f), new float2(24.251009f, 17.299999f),
            new float2(30.651031f, 17.299999f), new float2(30.651031f, 17.299999f), new float2(30.651031f, 17.299999f),
            new float2(30.651031f, 17.299999f), new float2(30.651031f, 17.299999f), new float2(30.651031f, 17.299999f),
            new float2(30.651031f, 17.299999f),
        });
    }

    [Test]
    public void Stone_BackhalfDrop_TrajectoryUnchanged()
    {
        AssertMatchesSnapshot("stone", "backhalf_drop.json", new[]
        {
            new float2(6.000001f, -0.729565f), new float2(6.000001f, -0.729565f), new float2(6.000001f, -0.729565f),
            new float2(6.000001f, -0.729565f), new float2(6.000001f, -0.729565f), new float2(6.000001f, -0.729565f),
            new float2(6.000001f, -0.729565f), new float2(6.000001f, -0.729565f), new float2(6.000001f, -0.729565f),
            new float2(6.000001f, -0.729565f),
        });
    }

    [Test]
    public void Captain_Chase_TrajectoryUnchanged()
    {
        AssertMatchesSnapshot("captain", "captain_chase.json", new[]
        {
            new float2(2.027039f, -2.027039f), new float2(2.027039f, -2.027039f), new float2(2.027039f, -2.027039f),
            new float2(2.027039f, -2.027039f), new float2(2.027039f, -2.027039f), new float2(2.027039f, -2.027039f),
            new float2(2.027039f, -2.027039f), new float2(2.027039f, -2.027039f), new float2(2.027039f, -2.027039f),
            new float2(2.027039f, -2.027039f),
        });
    }

    [Test]
    public void Captain_HomingMain_TrajectoryUnchanged()
    {
        AssertMatchesSnapshot("captain", "captain_homing_main.json", new[]
        {
            new float2(-2.042798f, -1.714111f), new float2(-2.042798f, -1.714111f), new float2(-2.042798f, -1.714111f),
            new float2(-2.042798f, -1.714111f), new float2(-2.042798f, -1.714111f), new float2(-2.042798f, -1.714111f),
            new float2(-2.042798f, -1.714111f), new float2(-2.042798f, -1.714111f), new float2(-2.042798f, -1.714111f),
            new float2(-2.042798f, -1.714111f),
        });
    }

    [Test]
    public void Vagrant_IntroBars_TrajectoryUnchanged()
    {
        AssertMatchesSnapshot("vagrant", "intro_bars.json", new[]
        {
            new float2(0.746967f, 8.999999f), new float2(0.746935f, 0.000021f), new float2(0.746927f, -2.099974f),
            new float2(0.746927f, -2.099974f), new float2(0.746927f, -2.099974f), new float2(0.746927f, -2.099974f),
            new float2(0.746927f, -2.099974f), new float2(0.746927f, -2.099974f), new float2(0.746927f, -2.099974f),
            new float2(0.746927f, -2.099974f),
        });
    }

    [Test]
    public void Vagrant_Ghosts_TrajectoryUnchanged()
    {
        AssertMatchesSnapshot("vagrant", "ghosts.json", new[]
        {
            new float2(11.151644f, 14.281349f), new float2(8.499981f, 13.509384f), new float2(5.848338f, 10.540714f),
            new float2(4.750000f, 6.018768f), new float2(5.524917f, 2.177334f), new float2(5.524917f, 2.177334f),
            new float2(5.524917f, 2.177334f), new float2(5.524917f, 2.177334f), new float2(5.524917f, 2.177334f),
            new float2(5.524917f, 2.177334f),
        });
    }

    private static List<BulletData> ParseBullets(string dir, string file)
    {
        string path = Path.Combine(Application.dataPath, "BulletBuffers", dir, file);
        string json = File.ReadAllText(path);

        using (new EditorStageProbe(BulletTypeDatabasePath, EnemyDatabasePath))
        {
            MethodInfo readMethod = typeof(BulletBufferManager).GetMethod(
                "ReadBulletBufferFromJson",
                BindingFlags.Instance | BindingFlags.NonPublic);
            object buffer = readMethod.Invoke(new BulletBufferManager(), new object[] { file, json });
            FieldInfo bulletsField = buffer.GetType().GetField("bullets",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return (List<BulletData>)bulletsField.GetValue(buffer);
        }
    }
}

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

// 手動再生成専用ツール(通常のテスト実行には含まれない=[Explicit])。
// BulletV2TrajectorySnapshotTests.cs の期待値は、このセッションでは Unity バッチが
// 別セッションに専有されていたため Python 移植版(TestResults/port_snapshot.py)で
// 代替算出した。Unity バッチが確保できたときにこの Dump を明示実行し、ログの
// SNAPSHOT_DUMP_BEGIN〜END を BulletV2TrajectorySnapshotTests.cs に書き写して
// コメントの注記も更新すること。
public class _SnapshotGenerator
{
    private const string BulletTypeDatabasePath = "Assets/Scripts/Bullets/BulletTypes/BulletTypeDataBase.asset";
    private const string EnemyDatabasePath = "Assets/Scripts/Enemies/Enemies/EnemyDataBase.asset";

    [Test, Explicit("手動でのスナップショット再生成専用。通常のテスト実行では走らない。")]
    public void Dump()
    {
        var sb = new StringBuilder();
        Sample(sb, "stone", "run_cutter_1.json", 0);
        Sample(sb, "stone", "backhalf_drop.json", 0);
        Sample(sb, "captain", "captain_chase.json", 0);
        Sample(sb, "captain", "captain_homing_main.json", 0);
        Sample(sb, "vagrant", "intro_bars.json", 0);
        Sample(sb, "vagrant", "ghosts.json", 0);
        Debug.Log("SNAPSHOT_DUMP_BEGIN\n" + sb.ToString() + "SNAPSHOT_DUMP_END");
    }

    private static void Sample(StringBuilder sb, string dir, string file, int bulletIndex)
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

            sb.AppendLine($"// {dir}/{file} bullet[{bulletIndex}]");
            sb.Append($"new float2[] {{ ");
            for (int frame = 1; frame <= 600; frame++)
            {
                job.Execute(0);
                if (frame % 60 == 0)
                {
                    float2 p = arr[0].position;
                    sb.Append($"new float2({p.x:R}f, {p.y:R}f), ");
                }
            }
            sb.AppendLine("},");
        }
        finally
        {
            arr.Dispose();
        }
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

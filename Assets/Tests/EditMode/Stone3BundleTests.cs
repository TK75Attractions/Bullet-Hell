using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

// 段階1(bh-bundle-1)の決定性固定テスト（BULLET-DATA-REFACTOR-PLAN_20260903.md 段階1）。
//
// 1. Stone3BundleParityTests: 旧形式(1クリップ1ファイル、.tmp_stage1/old/stone3 に退避済みの
//    バックアップ)を旧経路(ReadBulletBufferFromJson)で読んだ結果と、bundle(bh-bundle-1)を
//    新経路(ReadBulletBuffersFromJsonMulti/ReadBundleBulletBuffersFromJson)で読んだ結果が、
//    クリップ名・弾数・全フィールドで一致することを固定する。
// 2. Stone3BundleGoldenTest: bundle を読んだ結果のハッシュを Tests/Golden/stone3.golden.json
//    として登録し固定する。
//
// 注記: 既存の Tests/Golden/*.golden.json はステージの spawner スケジュール(t/clip/idx/pos/...)
// を記録したものであり、それを比較する C# テスト本体（Docs/BulletBufferContext.md が言及する
// GoldenScheduleTest.cs 等）は現ツリーに存在しない（要ユーザー確認・段階1作業メモ
// .tmp_stage1/progress.md 参照）。本テストは「BulletBuffer展開結果の決定性を固定する」という
// 目的に沿って、bundle 読み込み結果（クリップごとの弾配列）を対象にした新規フォーマットの
// golden を採用する。
public class Stone3BundleTests
{
    private const string BulletTypeDatabasePath = "Assets/Scripts/Bullets/BulletTypes/BulletTypeDataBase.asset";
    private const string EnemyDatabasePath = "Assets/Scripts/Enemies/Enemies/EnemyDataBase.asset";
    private const string OldFormatBackupDir = "../.tmp_stage1/old/stone3"; // Assets/.. からの相対＝リポジトリ直下
    private const string BundleDir = "Assets/BulletBuffers/stone3";
    private const string GoldenPath = "../Tests/Golden/stone3.golden.json";

    private static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

    [Test]
    public void OldFormatAndBundle_ProduceIdenticalBulletBuffers()
    {
        string oldDir = Path.GetFullPath(Path.Combine(Application.dataPath, OldFormatBackupDir));
        string bundleDir = Path.GetFullPath(Path.Combine(RepoRoot, BundleDir));

        Assert.IsTrue(Directory.Exists(oldDir),
            $"旧形式バックアップが見つかりません: {oldDir}（.tmp_stage1/old/stone3 に退避済みである前提）");
        Assert.IsTrue(Directory.Exists(bundleDir), $"bundle ディレクトリが見つかりません: {bundleDir}");

        using (new EditorStageProbe(BulletTypeDatabasePath, EnemyDatabasePath))
        {
            Dictionary<string, ClipRecord> oldClips = LoadClipsFromDirectory(oldDir);
            Dictionary<string, ClipRecord> newClips = LoadClipsFromDirectory(bundleDir);

            Assert.Greater(oldClips.Count, 0, "旧形式クリップが1件も読めませんでした");
            Assert.AreEqual(oldClips.Count, newClips.Count, "クリップ数が一致しません");

            List<string> missingInNew = new List<string>();
            List<string> mismatches = new List<string>();

            foreach (KeyValuePair<string, ClipRecord> kv in oldClips)
            {
                if (!newClips.TryGetValue(kv.Key, out ClipRecord newClip))
                {
                    missingInNew.Add(kv.Key);
                    continue;
                }

                string diff = CompareClips(kv.Value, newClip);
                if (diff != null)
                {
                    mismatches.Add($"{kv.Key}: {diff}");
                }
            }

            if (missingInNew.Count > 0)
            {
                Assert.Fail($"bundle 側に無いクリップが {missingInNew.Count} 件: {string.Join(", ", missingInNew.GetRange(0, Math.Min(10, missingInNew.Count)))}");
            }
            if (mismatches.Count > 0)
            {
                Assert.Fail($"内容が一致しないクリップが {mismatches.Count} 件:\n{string.Join("\n", mismatches.GetRange(0, Math.Min(10, mismatches.Count)))}");
            }
        }
    }

    [Test]
    public void Bundle_MatchesGoldenHash()
    {
        string bundleDir = Path.GetFullPath(Path.Combine(RepoRoot, BundleDir));
        string goldenPath = Path.GetFullPath(Path.Combine(Application.dataPath, GoldenPath));

        Assert.IsTrue(Directory.Exists(bundleDir), $"bundle ディレクトリが見つかりません: {bundleDir}");
        Assert.IsTrue(File.Exists(goldenPath), $"golden ファイルが見つかりません: {goldenPath}");

        using (new EditorStageProbe(BulletTypeDatabasePath, EnemyDatabasePath))
        {
            Dictionary<string, ClipRecord> clips = LoadClipsFromDirectory(bundleDir);
            StageDigest digest = ComputeDigest(clips);

            string goldenJson = File.ReadAllText(goldenPath);
            StageDigestGolden golden = JsonUtility.FromJson<StageDigestGolden>(goldenJson);

            Assert.AreEqual(golden.clipCount, digest.clipCount, "clipCount が golden と一致しません");
            Assert.AreEqual(golden.totalBulletCount, digest.totalBulletCount, "totalBulletCount が golden と一致しません");
            Assert.AreEqual(golden.sha256, digest.sha256, "bundle 展開結果のハッシュが golden と一致しません（意図した変更なら Tests/Golden/stone3.golden.json を再生成すること）");
        }
    }

    // --- helpers -----------------------------------------------------------

    private class ClipRecord
    {
        public string name;
        public bool homing;
        public bool isLaser;
        public List<BulletData> bullets;
    }

    private static Dictionary<string, ClipRecord> LoadClipsFromDirectory(string directoryPath)
    {
        Dictionary<string, ClipRecord> result = new Dictionary<string, ClipRecord>(StringComparer.Ordinal);
        BulletBufferManager manager = new BulletBufferManager();

        MethodInfo readDirMethod = typeof(BulletBufferManager).GetMethod(
            "ReadBulletBuffersFromAbsoluteDirectory", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(readDirMethod, "BulletBufferManager.ReadBulletBuffersFromAbsoluteDirectory が見つかりません（リフレクション対象名の変更?）");
        readDirMethod.Invoke(manager, new object[] { directoryPath });

        FieldInfo buffersField = typeof(BulletBufferManager).GetField(
            "bulletBuffers", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(buffersField, "BulletBufferManager.bulletBuffers フィールドが見つかりません");
        IList bufferList = (IList)buffersField.GetValue(manager);

        foreach (object bufferObj in bufferList)
        {
            Type bufferType = bufferObj.GetType();
            string name = (string)bufferType.GetField("name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(bufferObj);
            bool homing = (bool)bufferType.GetField("homing", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(bufferObj);
            bool isLaser = (bool)bufferType.GetField("isLaser", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(bufferObj);
            List<BulletData> bullets = (List<BulletData>)bufferType.GetField("bullets", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(bufferObj);

            result[name] = new ClipRecord { name = name, homing = homing, isLaser = isLaser, bullets = bullets };
        }

        return result;
    }

    private static string CompareClips(ClipRecord a, ClipRecord b)
    {
        if (a.homing != b.homing) return $"homing 不一致 ({a.homing} vs {b.homing})";
        if (a.isLaser != b.isLaser) return $"isLaser 不一致 ({a.isLaser} vs {b.isLaser})";
        if (a.bullets.Count != b.bullets.Count) return $"bullets.Count 不一致 ({a.bullets.Count} vs {b.bullets.Count})";

        for (int i = 0; i < a.bullets.Count; i++)
        {
            string diff = CompareBulletData(a.bullets[i], b.bullets[i]);
            if (diff != null) return $"bullets[{i}] {diff}";
        }
        return null;
    }

    private static string CompareBulletData(BulletData a, BulletData b)
    {
        if (!a.position.Equals(b.position)) return $"position {a.position} vs {b.position}";
        if (!a.velocity.Equals(b.velocity)) return $"velocity {a.velocity} vs {b.velocity}";
        if (a.angle != b.angle) return $"angle {a.angle} vs {b.angle}";
        if (a.useVelocityAngle != b.useVelocityAngle) return "useVelocityAngle";
        if (!a.originPos.Equals(b.originPos)) return $"originPos {a.originPos} vs {b.originPos}";
        if (!a.originVlc.Equals(b.originVlc)) return $"originVlc {a.originVlc} vs {b.originVlc}";
        if (!a.playerInfluence.Equals(b.playerInfluence)) return "playerInfluence";
        if (a.startX != b.startX) return "startX";
        if (a.speed != b.speed) return "speed";
        if (!a.gravity.Equals(b.gravity)) return "gravity";
        if (a.angleSpeed != b.angleSpeed) return "angleSpeed";
        if (a.initialAngle != b.initialAngle) return "initialAngle";
        if (!a.polarForm.Equals(b.polarForm)) return "polarForm";
        if (a.radiusVlc != b.radiusVlc) return "radiusVlc";
        if (a.radiusAccel != b.radiusAccel) return "radiusAccel";
        if (a.thetaVlc != b.thetaVlc) return "thetaVlc";
        if (a.thetaAccel != b.thetaAccel) return "thetaAccel";
        if (!a.startPos.Equals(b.startPos)) return "startPos";
        if (!a.nowCalculateVlc.Equals(b.nowCalculateVlc)) return "nowCalculateVlc";
        if (a.nowCalculateX != b.nowCalculateX) return "nowCalculateX";
        if (!a.polynomial.Equals(b.polynomial)) return "polynomial";
        if (a.typeId != b.typeId) return $"typeId {a.typeId} vs {b.typeId}";
        if (!a.scale.Equals(b.scale)) return "scale";
        if (!a.color.Equals(b.color)) return "color";
        if (!a.scaleEnd.Equals(b.scaleEnd)) return "scaleEnd";
        if (!a.colorEnd.Equals(b.colorEnd)) return "colorEnd";
        if (a.animDuration != b.animDuration) return "animDuration";
        if (a.areaNum != b.areaNum) return "areaNum";
        if (a.time != b.time) return "time";
        if (a.appearTime != b.appearTime) return "appearTime";
        if (a.appearDuration != b.appearDuration) return "appearDuration";
        if (a.life != b.life) return "life";
        if (a.random != b.random) return "random";
        if (a.warpCooldown != b.warpCooldown) return "warpCooldown";
        if (a.warpable != b.warpable) return "warpable";
        if (a.ignoreOutOfBoundsCulling != b.ignoreOutOfBoundsCulling) return "ignoreOutOfBoundsCulling";
        if (a.isActive != b.isActive) return "isActive";
        if (a.isClearing != b.isClearing) return "isClearing";
        if (a.clearTime != b.clearTime) return "clearTime";
        if (a.clearDuration != b.clearDuration) return "clearDuration";
        if (a.unCounterable != b.unCounterable) return "unCounterable";
        if (a.homingTurnRate != b.homingTurnRate) return "homingTurnRate";
        if (a.homingDuration != b.homingDuration) return "homingDuration";
        if (!a.v2LocalOffset.Equals(b.v2LocalOffset)) return "v2LocalOffset";

        string segDiff = CompareSegments(a.v2Segments, b.v2Segments);
        if (segDiff != null) return segDiff;

        return null;
    }

    private static string CompareSegments(FixedList512Bytes<BulletV2Segment> a, FixedList512Bytes<BulletV2Segment> b)
    {
        if (a.Length != b.Length) return $"v2Segments.Length {a.Length} vs {b.Length}";
        for (int i = 0; i < a.Length; i++)
        {
            BulletV2Segment sa = a[i];
            BulletV2Segment sb = b[i];
            if (sa.duration != sb.duration) return $"v2Segments[{i}].duration";
            if (!sa.vlc.Equals(sb.vlc)) return $"v2Segments[{i}].vlc";
            if (!sa.gravity.Equals(sb.gravity)) return $"v2Segments[{i}].gravity";
            if (sa.thetaVlc != sb.thetaVlc) return $"v2Segments[{i}].thetaVlc";
        }
        return null;
    }

    [Serializable]
    private class StageDigestGolden
    {
        public string stageDir;
        public string format;
        public int clipCount;
        public int totalBulletCount;
        public string sha256;
    }

    private struct StageDigest
    {
        public int clipCount;
        public int totalBulletCount;
        public string sha256;
    }

    // クリップ名でソートしたうえで、各弾の全フィールドを固定書式の文字列に変換して連結し、
    // SHA256 を取る。フィールド順序・書式は Stone3BundleGoldenGenerator（このファイル内の
    // ComputeDigest と同一実装）で固定し、golden 再生成時も必ずこの実装を使う。
    private static StageDigest ComputeDigest(Dictionary<string, ClipRecord> clips)
    {
        List<string> names = new List<string>(clips.Keys);
        names.Sort(StringComparer.Ordinal);

        StringBuilder sb = new StringBuilder();
        int totalBullets = 0;
        foreach (string name in names)
        {
            ClipRecord clip = clips[name];
            sb.Append("CLIP:").Append(name)
              .Append(";homing=").Append(clip.homing)
              .Append(";isLaser=").Append(clip.isLaser)
              .Append(";count=").Append(clip.bullets.Count).Append('\n');
            foreach (BulletData bullet in clip.bullets)
            {
                AppendBullet(sb, bullet);
                totalBullets++;
            }
        }

        byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
        using (SHA256 sha = SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(bytes);
            StringBuilder hex = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash) hex.Append(b.ToString("x2"));
            return new StageDigest { clipCount = names.Count, totalBulletCount = totalBullets, sha256 = hex.ToString() };
        }
    }

    private static void AppendBullet(StringBuilder sb, BulletData b)
    {
        sb.Append(F(b.position)).Append('|').Append(F(b.velocity)).Append('|').Append(R(b.angle)).Append('|').Append(b.useVelocityAngle)
          .Append('|').Append(F(b.originPos)).Append('|').Append(F(b.originVlc)).Append('|').Append(F(b.playerInfluence))
          .Append('|').Append(R(b.startX)).Append('|').Append(R(b.speed)).Append('|').Append(F(b.gravity))
          .Append('|').Append(R(b.angleSpeed)).Append('|').Append(R(b.initialAngle)).Append('|').Append(F(b.polarForm))
          .Append('|').Append(R(b.radiusVlc)).Append('|').Append(R(b.radiusAccel)).Append('|').Append(R(b.thetaVlc)).Append('|').Append(R(b.thetaAccel))
          .Append('|').Append(F(b.startPos)).Append('|').Append(F(b.nowCalculateVlc)).Append('|').Append(R(b.nowCalculateX))
          .Append('|').Append(F4(b.polynomial)).Append('|').Append(b.typeId).Append('|').Append(F(b.scale)).Append('|').Append(F4(b.color))
          .Append('|').Append(F(b.scaleEnd)).Append('|').Append(F4(b.colorEnd)).Append('|').Append(R(b.animDuration))
          .Append('|').Append(b.areaNum).Append('|').Append(R(b.time)).Append('|').Append(R(b.appearTime)).Append('|').Append(R(b.appearDuration))
          .Append('|').Append(R(b.life)).Append('|').Append(R(b.random)).Append('|').Append(R(b.warpCooldown)).Append('|').Append(b.warpable)
          .Append('|').Append(b.ignoreOutOfBoundsCulling).Append('|').Append(b.isActive).Append('|').Append(b.isClearing)
          .Append('|').Append(R(b.clearTime)).Append('|').Append(R(b.clearDuration)).Append('|').Append(b.unCounterable)
          .Append('|').Append(R(b.homingTurnRate)).Append('|').Append(R(b.homingDuration)).Append('|').Append(F(b.v2LocalOffset));

        sb.Append("|seg[").Append(b.v2Segments.Length).Append(']');
        for (int i = 0; i < b.v2Segments.Length; i++)
        {
            BulletV2Segment seg = b.v2Segments[i];
            sb.Append('(').Append(R(seg.duration)).Append(',').Append(F(seg.vlc)).Append(',').Append(F(seg.gravity)).Append(',').Append(R(seg.thetaVlc)).Append(')');
        }
        sb.Append('\n');
    }

    private static string R(float v) => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
    private static string F(float2 v) => $"{R(v.x)},{R(v.y)}";
    private static string F4(float4 v) => $"{R(v.x)},{R(v.y)},{R(v.z)},{R(v.w)}";
}

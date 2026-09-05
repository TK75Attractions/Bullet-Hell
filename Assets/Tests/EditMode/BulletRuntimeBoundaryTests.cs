using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class BulletRuntimeBoundaryTests
{
    const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    [TestCase(false)]
    [TestCase(true)]
    public void SpawnKeepsAngleUnitsSourceOffsetsAndTemplate(bool homing)
    {
        var manager = new BulletBufferManager();
        var template = new BulletData
        {
            originPos = new float2(1, 0), originVlc = new float2(1, 0),
            polarForm = new float2(1, 0), velocity = new float2(9, 9),
            life = 5, color = new float4(1), scale = new float2(1)
        };
        var originals = new List<BulletData> { template };
        Type type = typeof(BulletBufferManager).GetNestedType("BulletBuffer", BindingFlags.NonPublic);
        object buffer = Activator.CreateInstance(type, Flags, null,
            new object[] { "test", originals, homing, true }, null);
        var list = (IList)typeof(BulletBufferManager).GetField("bulletBuffers", Flags).GetValue(manager);
        list.Add(buffer);
        List<BulletData> result = manager.CreateSpawnedBullets(0, new float2(7, 4),
            new float2(3, 4), new float2(0, 2), 90, new float4(1), out bool laser);
        Assert.IsTrue(laser);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(homing ? 0 : math.PI / 2, result[0].polarForm.y, 1e-6);
        Assert.Less(math.distance(homing ? new float2(4, 4) : new float2(3, 5), result[0].position), 1e-6);
        Assert.Less(math.distance(homing ? new float2(1, 2) : new float2(0, 3), result[0].originVlc), 1e-6);
        Assert.AreEqual(float2.zero, result[0].velocity);
        Assert.AreEqual(5, result[0].life);
        Assert.AreEqual(template, originals[0]);
        Assert.AreNotSame(originals, result);
        Assert.IsNull(manager.CreateSpawnedBullets(-3, default, default, default, 0, default, out bool clearLaser));
        Assert.IsFalse(clearLaser);
    }

    [TestCase(-1)]
    [TestCase(int.MaxValue)]
    public void InvalidRenderTypeIsSkippedWithoutHidingNextValidBullet(int invalidType)
    {
        using (var probe = new EditorStageProbe(
            "Assets/Scripts/Bullets/BulletTypes/BulletTypeDataBase.asset",
            "Assets/Scripts/Enemies/Enemies/EnemyDataBase.asset"))
        {
            int validType = Array.FindIndex(probe.Btdb.types, type => type != null);
            Assert.GreaterOrEqual(validType, 0);
            var go = new GameObject("~BulletRenderBoundaryTest");
            try
            {
                var renderer = go.AddComponent<BulletRenderSystem>();
                var output = new BulletRenderData[2];
                typeof(BulletRenderSystem).GetField("renderArray", Flags).SetValue(renderer, output);
                using (var bullets = new NativeArray<BulletData>(new[]
                {
                    new BulletData { typeId = invalidType, isActive = true },
                    new BulletData { typeId = validType, isActive = true,
                        position = new float2(2, 3), scale = new float2(1), color = new float4(1), life = 10, time = 1 }
                }, Allocator.Temp))
                {
                    MethodInfo append = typeof(BulletRenderSystem).GetMethod("AppendRenderData", Flags,
                        null, new[] { typeof(NativeArray<BulletData>), typeof(int), typeof(int), typeof(int) }, null);
                    Assert.IsNotNull(append);
                    Assert.AreEqual(1, append.Invoke(renderer, new object[] { bullets, 2, 0, 2 }));
                    Assert.AreEqual(validType, output[0].texIndex);
                    Assert.AreEqual(new float2(2, 3), output[0].pos);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}

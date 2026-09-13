using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using NUnit.Framework;

// BulletBufferManager の List 正本と名前検索キャッシュの契約を固定するテスト。
// 配置先は Assets/Tests/EditMode/。
public class BulletBufferManagerIndexTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly Type BufferType = typeof(BulletBufferManager).GetNestedType("BulletBuffer", BindingFlags.NonPublic);
    private static readonly FieldInfo BufferListField = typeof(BulletBufferManager).GetField("bulletBuffers", InstanceFlags);
    private static readonly FieldInfo IndexField = typeof(BulletBufferManager).GetField("bulletBufferIndexByName", InstanceFlags);
    private static readonly FieldInfo BufferNameField = BufferType?.GetField("name", InstanceFlags);
    private static readonly MethodInfo AddOrReplaceMethod = typeof(BulletBufferManager).GetMethod("AddOrReplaceBulletBuffer", InstanceFlags);
    private static readonly MethodInfo ResetMethod = typeof(BulletBufferManager).GetMethod("ResetBuffers", InstanceFlags);

    [Test]
    public void LookupUsesOrdinalNamesAndPreservesInsertionOrder()
    {
        BulletBufferManager manager = CreateManager();
        AssertMissing(manager, "missing");
        Add(manager, "alpha");
        Add(manager, "ALPHA");
        Add(manager, "");

        AssertLookup(manager, "alpha", 0);
        AssertLookup(manager, "ALPHA", 1);
        AssertLookup(manager, "", 2);
        Assert.AreEqual(3, GetBuffers(manager).Count);
    }

    [Test]
    public void DuplicateNameReplacesAtTheExistingIndex()
    {
        BulletBufferManager manager = CreateManager();
        Add(manager, "same");
        object original = GetBuffers(manager)[0];

        Add(manager, "other");
        Add(manager, "same");
        object replacement = GetBuffers(manager)[0];

        Assert.AreEqual(2, GetBuffers(manager).Count);
        AssertLookup(manager, "same", 0);
        Assert.AreNotSame(original, replacement);
        Assert.AreSame(replacement, GetBuffers(manager)[0]);
    }

    [Test]
    public void ResetRemovesOldNamesAndRestoresBuiltInOrder()
    {
        BulletBufferManager manager = CreateManager();
        Add(manager, "old_name");

        Reset(manager);

        AssertMissing(manager, "old_name");
        string[] expected = { "Rumia_0", "Rumia_1", "Line", "LineLaser", "Circle" };
        IList buffers = GetBuffers(manager);
        Assert.AreEqual(expected.Length, buffers.Count);
        for (int i = 0; i < expected.Length; i++)
        {
            AssertLookup(manager, expected[i], i);
            Assert.AreEqual(expected[i], BufferNameField.GetValue(buffers[i]));
        }
    }

    [Test]
    public void UnloadRemovesNamesFromTheIndexAndAllowsFreshAdds()
    {
        BulletBufferManager manager = CreateManager();
        Reset(manager);
        AssertLookup(manager, "Circle", 4);

        manager.UnloadAllBulletBuffers();

        AssertMissing(manager, "Circle");
        Add(manager, "fresh_name");
        AssertLookup(manager, "fresh_name", 0);
        Assert.AreEqual(1, GetBuffers(manager).Count);
    }

    [Test]
    public void LookupRebuildsCacheAfterUnitySerializationBoundary()
    {
        BulletBufferManager manager = CreateManager();
        Reset(manager);

        // bulletBuffers は SerializeField、辞書は NonSerialized のため、
        // Unity デシリアライズ直後を辞書 null の状態で再現する。
        IndexField.SetValue(manager, null);

        AssertLookup(manager, "Circle", 4);
        Assert.IsNotNull(IndexField.GetValue(manager));
    }

    [Test]
    public void NullAndEmptyNamesKeepTheirExistingMeaning()
    {
        BulletBufferManager manager = CreateManager();
        Add(manager, "");
        Add(manager, null);

        AssertLookup(manager, "", 0);
        AssertLookup(manager, null, 1);

        // null 名も先頭一致で置換し、重複要素を増やさない。
        Add(manager, null);
        AssertLookup(manager, null, 1);
        Assert.AreEqual(2, GetBuffers(manager).Count);
    }

    private static BulletBufferManager CreateManager()
    {
        Assert.IsNotNull(BufferType, "BulletBufferManager.BulletBuffer が見つかりません");
        Assert.IsNotNull(BufferListField, "bulletBuffers フィールドが見つかりません");
        Assert.IsNotNull(IndexField, "bulletBufferIndexByName フィールドが見つかりません");
        Assert.IsNotNull(BufferNameField, "BulletBuffer.name フィールドが見つかりません");
        Assert.IsNotNull(AddOrReplaceMethod, "AddOrReplaceBulletBuffer が見つかりません");
        Assert.IsNotNull(ResetMethod, "ResetBuffers が見つかりません");
        return new BulletBufferManager();
    }

    private static void Add(BulletBufferManager manager, string name)
    {
        object buffer = Activator.CreateInstance(
            BufferType,
            InstanceFlags,
            binder: null,
            args: new object[] { name, new List<BulletData>(), false, false },
            culture: CultureInfo.InvariantCulture);
        AddOrReplaceMethod.Invoke(manager, new[] { buffer });
    }

    private static void Reset(BulletBufferManager manager)
    {
        ResetMethod.Invoke(manager, null);
    }

    private static IList GetBuffers(BulletBufferManager manager)
    {
        return (IList)BufferListField.GetValue(manager);
    }

    private static void AssertLookup(BulletBufferManager manager, string name, int expectedIndex)
    {
        Assert.IsTrue(manager.TryGetBulletBufferIndex(name, out int actualIndex), $"名前 '{name}' が見つかりません");
        Assert.AreEqual(expectedIndex, actualIndex, $"名前 '{name}' の index が不一致です");
    }

    private static void AssertMissing(BulletBufferManager manager, string name)
    {
        bool found = manager.TryGetBulletBufferIndex(name, out int actualIndex);
        Assert.IsFalse(found, $"名前 '{name}' が未登録なのに見つかりました");
        Assert.AreEqual(-1, actualIndex, $"名前 '{name}' の未登録時 index は -1 である必要があります");
    }
}

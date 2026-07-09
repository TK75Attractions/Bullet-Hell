using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Sets up the minimal <see cref="GManager"/> singleton context that the real
/// parsing code (<see cref="BulletDataJson.ToBulletData"/> reads
/// <c>GManager.Control.BTDB</c>; enemy-spawner parsing reads
/// <c>GManager.Control.EDB</c>) expects, so those code paths can be reused from
/// editor tooling and EditMode tests without entering Play mode.
///
/// If a live manager already exists (e.g. Play mode) with the databases wired,
/// it is reused untouched. Otherwise a detached, hidden manager is created with
/// the databases loaded from assets and torn down on dispose. No <c>Init()</c>
/// is called on the databases: <c>GetTypeId</c>/<c>GetEnemyId</c> are pure name
/// lookups whose result is independent of the parallel-array rebuild that
/// <c>Init()</c> performs.
/// </summary>
public sealed class EditorStageProbe : IDisposable
{
    private readonly GManager previousControl;
    private readonly GameObject tempObject;

    public BulletTypeDataBase Btdb { get; }
    public EnemyDataBase Edb { get; }

    public EditorStageProbe(string btdbAssetPath, string edbAssetPath)
    {
        previousControl = GManager.Control;

        if (previousControl != null && previousControl.BTDB != null && previousControl.EDB != null)
        {
            // Reuse a fully wired live manager.
            Btdb = previousControl.BTDB;
            Edb = previousControl.EDB;
            tempObject = null;
            return;
        }

        Btdb = AssetDatabase.LoadAssetAtPath<BulletTypeDataBase>(btdbAssetPath);
        Edb = AssetDatabase.LoadAssetAtPath<EnemyDataBase>(edbAssetPath);
        if (Btdb == null)
        {
            throw new Exception($"[EditorStageProbe] BulletTypeDataBase not found at {btdbAssetPath}");
        }
        if (Edb == null)
        {
            throw new Exception($"[EditorStageProbe] EnemyDataBase not found at {edbAssetPath}");
        }

        tempObject = new GameObject("~EditorStageProbe") { hideFlags = HideFlags.HideAndDontSave };
        // AddComponent does not run MonoBehaviour.Awake in edit mode, so the
        // manager's heavy async bootstrap never fires; we only need the fields.
        GManager manager = tempObject.AddComponent<GManager>();
        manager.BTDB = Btdb;
        manager.EDB = Edb;
        GManager.Control = manager;
    }

    public void Dispose()
    {
        GManager.Control = previousControl;
        if (tempObject != null)
        {
            UnityEngine.Object.DestroyImmediate(tempObject);
        }
    }
}

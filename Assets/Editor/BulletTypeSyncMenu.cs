using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor menu that scans <see cref="StageValidation.BulletTypesFolder"/> for
/// <see cref="BulletType"/> assets that are not yet registered in the
/// <see cref="BulletTypeDataBase"/> and appends them, so adding a new bullet
/// type is "drop the .asset in the folder + run this" instead of hand-editing
/// the database.
///
/// New types are appended at the end, which keeps every existing typeId stable
/// (existing JSON typeName resolution is unaffected).
/// </summary>
public static class BulletTypeSyncMenu
{
    public const string SyncMenuPath = "Tools/Bullet Hell/Sync Bullet Types";

    [MenuItem(SyncMenuPath)]
    public static void SyncBulletTypes()
    {
        BulletTypeDataBase btdb = AssetDatabase.LoadAssetAtPath<BulletTypeDataBase>(StageGoldenDumper.BtdbAssetPath);
        if (btdb == null)
        {
            Debug.LogError($"[SyncTypes] BulletTypeDataBase not found at {StageGoldenDumper.BtdbAssetPath}");
            return;
        }

        List<string> unregistered = StageValidation.FindUnregisteredBulletTypeAssetPaths(btdb);
        if (unregistered.Count == 0)
        {
            Debug.Log("[SyncTypes] All BulletType assets are already registered. Nothing to do.");
            return;
        }

        List<BulletType> merged = new List<BulletType>(btdb.types ?? new BulletType[0]);
        List<string> added = new List<string>();
        foreach (string path in unregistered)
        {
            BulletType asset = AssetDatabase.LoadAssetAtPath<BulletType>(path);
            if (asset == null)
            {
                continue;
            }
            merged.Add(asset);
            added.Add($"[{merged.Count - 1}] {(string.IsNullOrEmpty(asset.typeName) ? "(no typeName)" : asset.typeName)} <- {path}");
        }

        btdb.types = merged.ToArray();
        EditorUtility.SetDirty(btdb);
        AssetDatabase.SaveAssets();

        Debug.Log($"[SyncTypes] Registered {added.Count} new BulletType asset(s):\n  " + string.Join("\n  ", added));
    }
}

using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// New P3 runtime path: pattern-generated bullets and the generation-stamped
/// handle API (S4). Data ownership is unchanged — everything here operates on the
/// enemy-bullet list owned by the core QuadOrder partial. The legacy clip path is
/// untouched.
/// </summary>
public partial class QuadOrder
{
    /// <summary>Generation stamped onto handles minted now. Bumped on clear.</summary>
    public int EnemyBulletGeneration => enemyBulletGeneration;

    /// <summary>Current length of the append-only enemy-bullet slot list.</summary>
    public int EnemyBulletSlotCount => enemyBullets.IsCreated ? enemyBullets.Length : 0;

    /// <summary>Stamps a raw slot index with the current generation.</summary>
    public BulletHandle MakeHandle(int index)
    {
        return index >= 0 ? new BulletHandle(index, enemyBulletGeneration) : BulletHandle.Invalid;
    }

    /// <summary>Wraps a list of raw indices as current-generation handles.</summary>
    public List<BulletHandle> ToHandles(List<int> indices)
    {
        List<BulletHandle> handles = new List<BulletHandle>(indices != null ? indices.Count : 0);
        if (indices == null) return handles;
        for (int i = 0; i < indices.Count; i++)
        {
            handles.Add(MakeHandle(indices[i]));
        }
        return handles;
    }

    /// <summary>Resolves a handle to a live slot index, or false if it is stale
    /// (older generation) or out of range.</summary>
    public bool TryResolveHandle(BulletHandle handle, out int index)
    {
        int length = enemyBullets.IsCreated ? enemyBullets.Length : 0;
        if (handle.IsValidFor(enemyBulletGeneration, length))
        {
            index = handle.Index;
            return true;
        }
        index = -1;
        return false;
    }

    /// <summary>
    /// Adds one fully-formed pattern bullet (world-space origin already baked) to
    /// the enemy-bullet pipeline. Screen-noise and warp-zone typed bullets are
    /// routed exactly like the buffer path. Returns the handle (Invalid if the
    /// bullet was consumed by a special typed sink).
    /// </summary>
    public BulletHandle AddPreparedEnemyBullet(BulletData bullet)
    {
        if (TryAddGeneratedBullet(bullet, out int index))
        {
            return MakeHandle(index);
        }
        return BulletHandle.Invalid;
    }

    /// <summary>
    /// Handle-safe variant of <see cref="UpdateBulletData"/>: stale handles (from a
    /// prior clear) are dropped instead of mutating reused slots. Returns handles to
    /// the resulting bullets.
    /// </summary>
    public List<BulletHandle> UpdateBulletDataByHandle(List<BulletHandle> handles, BulletClip clip)
    {
        List<BulletHandle> result = new List<BulletHandle>();
        if (handles == null || handles.Count == 0) return result;

        List<int> liveIndices = new List<int>(handles.Count);
        for (int i = 0; i < handles.Count; i++)
        {
            if (TryResolveHandle(handles[i], out int index))
            {
                liveIndices.Add(index);
            }
        }

        if (liveIndices.Count == 0) return result;

        List<int> updated = UpdateBulletData(liveIndices, clip);
        return ToHandles(updated);
    }
}

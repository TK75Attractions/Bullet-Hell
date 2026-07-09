using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public enum ManagedBulletKind
{
    EnemyBullet,
    WarpZone
}

[Serializable]
public struct ManagedBulletHandle
{
    public ManagedBulletKind kind;
    public int index;

    public ManagedBulletHandle(ManagedBulletKind kind, int index)
    {
        this.kind = kind;
        this.index = index;
    }

    public bool IsValid => index >= 0;
}

[Serializable]
public class QuadOrder : MonoBehaviour
{
    #region//CellManagers
    [NonSerialized] private QuadCell[] cells = Array.Empty<QuadCell>();
    [SerializeField] private float cellSize;
    [SerializeField] private int separateLevel;
    [SerializeField] private Vector2 quadOrigin = new Vector2(-2f, -9f);
    [SerializeField] public int cellCount;
    [NonSerialized] private int gridResolution;
    private List<Vector2Int> cellOffsets = new List<Vector2Int>()
    {
        new Vector2Int(0, 0),
        new Vector2Int(1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(1, 1),
        new Vector2Int(-1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(-1, -1),
        new Vector2Int(1, -1),
        new Vector2Int(-1, 1)
    };

    [Serializable]
    private class QuadCell
    {
        public List<MultiBullet> multiBullets = new List<MultiBullet>();
        public List<BulletData> enemyBullets = new List<BulletData>();
        public void AddMultiBullet(MultiBullet multiBullet) => multiBullets.Add(multiBullet);
        public void ClearMultiBullets() => multiBullets.Clear();

        public void ClearAllBullets()
        {
            enemyBullets.Clear();
        }

        public void AddEnemyBullet(BulletData bullet) => enemyBullets.Add(bullet);
        public void ClearEnemyBullets() => enemyBullets.Clear();
        public int GetEnemyBulletCount() => enemyBullets.Count;
        public List<BulletData> GetEnemyBullets() => enemyBullets;
    }
    #endregion

    #region //Arrays
    [SerializeField] private Boss boss = null;
    private NativeList<BulletData> enemyBullets;
    private NativeList<CounterBullet> counterBullets;
    [SerializeField]
    private List<MultiBullet> multiBullets = new List<MultiBullet>();
    private NativeList<BulletData> warpZones;
    private NativeArray<float2> collisionVerts;
    private NativeArray<int2> collisionVertRanges;
    private NativeArray<float> bulletPowers;
    private NativeArray<int> collisionHitFlag;
    private NativeArray<float> grazePower;
    private NativeList<BulletData> collisionCheckBullets;
    private NativeList<float2> laserBatchVerts;
    private NativeList<int> laserBatchCellIndices;
    private NativeList<byte> dashCollisionActiveFlags;

    #endregion

    private bool collisionDataDirty = true;
    private List<int> collisionCheckCells = new List<int>(9);
    private int enemySpawnGeneration = 0;
    [SerializeField] private bool drawEnemyBulletCollisionGizmos = true;
    [SerializeField] private Color enemyBulletCollisionGizmoColor = new Color(0.1f, 1f, 0.3f, 0.9f);
    [SerializeField] private float enemyBulletCollisionGizmoZ = 0f;
    [SerializeField] private bool drawLaserCollisionGizmos = true;
    [SerializeField] private Color laserCollisionGizmoColor = new Color(1f, 0.2f, 0.2f, 0.9f);
    [SerializeField] private float laserCollisionGizmoZ = 0f;
    [Header("Warp Zones")]
    [SerializeField] private int warpZoneTypeId = -1;
    [SerializeField] private string warpZoneTypeName = BulletData.WarpZoneTypeName;
    [SerializeField] private int warpZoneReflectXTypeId = -1;
    [SerializeField] private string warpZoneReflectXTypeName = BulletData.WarpZoneReflectXTypeName;
    [SerializeField] private int warpZoneReflectYTypeId = -1;
    [SerializeField] private string warpZoneReflectYTypeName = BulletData.WarpZoneReflectYTypeName;
    [SerializeField] private float defaultWarpCooldown = 0.1f;
    private bool warpZoneTypeResolutionAttempted;
    [Header("Debug")]
    [SerializeField] private bool debugSyncNativeBulletListsToInspector;
    [SerializeField] private int debugNativeBulletDisplayLimit = 128;
    [SerializeField] private int debugEnemyBulletSlotCount;
    [SerializeField] private int debugEnemyBulletActiveCount;
    [SerializeField] private List<NativeBulletDebugEntry> debugEnemyBulletEntries = new List<NativeBulletDebugEntry>();
    [SerializeField] private int debugMultiBulletOrbitSlotCount;
    [SerializeField] private int debugMultiBulletOrbitActiveCount;
    [SerializeField] private List<NativeBulletDebugEntry> debugMultiBulletOrbitEntries = new List<NativeBulletDebugEntry>();

    public LaserEmitter laserEmitter;

    [Serializable]
    private class NativeBulletDebugEntry
    {
        public int index;
        public bool isActive;
        public int areaNum;
        public int typeId;
        public float time;
        public float angle;
        public Vector2 scale;
        public Vector2 position;
        public Vector2 velocity;
        public Vector2 originPos;
        public Vector2 originVlc;
        public Vector2 polarForm;
    }

    public List<LASER> allLASERs = new();
    private readonly List<LASER> laserBatchLasers = new List<LASER>(128);
    private readonly List<int> laserBatchStarts = new List<int>(128);
    private readonly List<int> laserBatchCounts = new List<int>(128);

    private QuadGrid CreateQuadGrid()
    {
        int count = cells != null && cells.Length > 0 ? cells.Length : cellCount;
        return new QuadGrid(new float2(quadOrigin.x, quadOrigin.y), cellSize, gridResolution, count);
    }

    public void AwakeSetting()
    {
        int side = 1;
        for (int i = 0; i < separateLevel; i++) side *= 2;
        int n = side * side;
        cellCount = n;
        gridResolution = side;

        float cL = cellSize * side;
        quadOrigin = new(-(cL - 36) / 2, -(cL - 18) / 2);

        QuadCell[] t = new QuadCell[n];
        for (int i = 0; i < n; i++) t[i] = new();
        cells = t;

        BuildCollisionData();
        if (!collisionHitFlag.IsCreated)
        {
            collisionHitFlag = new NativeArray<int>(1, Allocator.Persistent);
        }
        if (!grazePower.IsCreated)
        {
            grazePower = new NativeArray<float>(1, Allocator.Persistent);
        }
        if (!collisionCheckBullets.IsCreated)
        {
            collisionCheckBullets = new NativeList<BulletData>(256, Allocator.Persistent);
        }
        if (!laserBatchVerts.IsCreated)
        {
            laserBatchVerts = new NativeList<float2>(256, Allocator.Persistent);
        }
        if (!laserBatchCellIndices.IsCreated)
        {
            laserBatchCellIndices = new NativeList<int>(256, Allocator.Persistent);
        }
        if (!enemyBullets.IsCreated)
        {
            enemyBullets = new NativeList<BulletData>(256, Allocator.Persistent);
        }
        if (!counterBullets.IsCreated)
        {
            counterBullets = new NativeList<CounterBullet>(256, Allocator.Persistent);
        }
        if (!warpZones.IsCreated)
        {
            warpZones = new NativeList<BulletData>(16, Allocator.Persistent);
        }
        if (!dashCollisionActiveFlags.IsCreated)
        {
            dashCollisionActiveFlags = new NativeList<byte>(256, Allocator.Persistent);
        }
        ResolveWarpZoneTypeId();

        if (boss != null) boss.Init();
    }

    private void OnDestroy()
    {
        if (enemyBullets.IsCreated) enemyBullets.Dispose();
        if (counterBullets.IsCreated) counterBullets.Dispose();
        if (warpZones.IsCreated) warpZones.Dispose();
        if (collisionVerts.IsCreated) collisionVerts.Dispose();
        if (collisionVertRanges.IsCreated) collisionVertRanges.Dispose();
        if (bulletPowers.IsCreated) bulletPowers.Dispose();
        if (collisionHitFlag.IsCreated) collisionHitFlag.Dispose();
        if (grazePower.IsCreated) grazePower.Dispose();
        if (collisionCheckBullets.IsCreated) collisionCheckBullets.Dispose();
        if (laserBatchVerts.IsCreated) laserBatchVerts.Dispose();
        if (laserBatchCellIndices.IsCreated) laserBatchCellIndices.Dispose();
        if (dashCollisionActiveFlags.IsCreated) dashCollisionActiveFlags.Dispose();
    }

    private void BuildCollisionData()
    {
        if (collisionVerts.IsCreated) collisionVerts.Dispose();
        if (collisionVertRanges.IsCreated) collisionVertRanges.Dispose();
        if (bulletPowers.IsCreated) bulletPowers.Dispose();

        var bulletTypeDB = GManager.Control.BTDB;
        if (bulletTypeDB == null || bulletTypeDB.types == null)
        {
            collisionVerts = new NativeArray<float2>(0, Allocator.Persistent);
            collisionVertRanges = new NativeArray<int2>(0, Allocator.Persistent);
            bulletPowers = new NativeArray<float>(0, Allocator.Persistent);
            collisionDataDirty = false;
            return;
        }

        int typeCount = bulletTypeDB.types.Length;
        List<float2[]> vertsByType = bulletTypeDB.bVerts;
        List<float> powersByType = bulletTypeDB.bPower;

        int totalVertCount = 0;
        for (int i = 0; i < typeCount; i++)
        {
            if (vertsByType != null && i < vertsByType.Count && vertsByType[i] != null)
            {
                totalVertCount += vertsByType[i].Length;
            }
        }

        collisionVertRanges = new NativeArray<int2>(typeCount, Allocator.Persistent);
        collisionVerts = new NativeArray<float2>(totalVertCount, Allocator.Persistent);
        bulletPowers = new NativeArray<float>(typeCount, Allocator.Persistent);

        int offset = 0;
        for (int i = 0; i < typeCount; i++)
        {
            float2[] verts = (vertsByType != null && i < vertsByType.Count) ? vertsByType[i] : null;
            int length = verts != null ? verts.Length : 0;
            collisionVertRanges[i] = new int2(offset, length);

            for (int j = 0; j < length; j++)
            {
                collisionVerts[offset + j] = verts[j];
            }

            bulletPowers[i] = (powersByType != null && i < powersByType.Count) ? powersByType[i] : 0f;
            offset += length;
        }

        collisionDataDirty = false;
    }

    public void MarkCollisionDataDirty()
    {
        collisionDataDirty = true;
    }

    private void ResolveWarpZoneTypeId()
    {
        if (warpZoneTypeId >= 0 && warpZoneReflectXTypeId >= 0 && warpZoneReflectYTypeId >= 0) return;
        if (warpZoneTypeResolutionAttempted) return;
        if (GManager.Control == null || GManager.Control.BTDB == null) return;

        warpZoneTypeResolutionAttempted = true;
        TryResolveWarpZoneTypeId(warpZoneTypeName, ref warpZoneTypeId);
        TryResolveWarpZoneTypeId(warpZoneReflectXTypeName, ref warpZoneReflectXTypeId);
        TryResolveWarpZoneTypeId(warpZoneReflectYTypeName, ref warpZoneReflectYTypeId);
    }

    private void TryResolveWarpZoneTypeId(string typeName, ref int typeId)
    {
        if (typeId >= 0) return;
        if (string.IsNullOrWhiteSpace(typeName)) return;

        int resolvedTypeId = BulletData.ResolveTypeId(typeName, GManager.Control.BTDB);
        if (resolvedTypeId >= 0) typeId = resolvedTypeId;
    }

    private bool IsWarpZone(BulletData bullet)
    {
        return (warpZoneTypeId >= 0 && bullet.typeId == warpZoneTypeId)
            || (warpZoneReflectXTypeId >= 0 && bullet.typeId == warpZoneReflectXTypeId)
            || (warpZoneReflectYTypeId >= 0 && bullet.typeId == warpZoneReflectYTypeId);
    }

    private bool IsScreenNoise(BulletData bullet)
    {
        return BulletData.IsScreenNoise(bullet);
    }

    public void QuadUpdate(float _dt)
    {
        BulletUpdate(_dt);
        CheckCollisionWithEnemy(GManager.Control.PController.pos, _dt);
        for (int i = 0; i < allLASERs.Count; i++)
        {
            LASER laser = allLASERs[i];
            if (laser == null)
            {
                allLASERs.RemoveAt(i);
                i--;
                continue;
            }

            if (laser.UpdateSet(_dt))
            {
                laser.Destroy();
                allLASERs.RemoveAt(i);
                i--;
            }
        }
        UpdateDirtyLASERCellIndices();
        CheckCollisionWithLASER(GManager.Control.PController.pos);

        UpdateMultiBullets(_dt);

        if (boss != null) boss.UpdateBoss(_dt);
        UpdateCounterBullets(_dt);
    }

    #region //BulletMethods
    public void BulletUpdate(float _dt)
    {
        bool hasEnemyBullets = enemyBullets.IsCreated && enemyBullets.Length > 0;
        bool hasWarpZones = warpZones.IsCreated && warpZones.Length > 0;
        if (!hasEnemyBullets && !hasWarpZones)
        {
            ClearAllCells();
            SyncNativeBulletDebugViews();
            return;
        }

        //敵の弾の更新
        float2 playerVelocity = GManager.Control.PController != null ? GManager.Control.PController.velocity : new float2(0, 0);
        QuadGrid grid = CreateQuadGrid();
        if (hasEnemyBullets)
        {
            NativeArray<BulletData> bullets = enemyBullets.AsArray();
            BulletDataUpdateJob job1 = new()
            {
                bullets = bullets,
                dt = _dt,
                grid = grid,
                playerVelocity = playerVelocity
            };
            JobHandle handle1 = job1.Schedule(bullets.Length, 64);
            handle1.Complete();
        }

        // areaNum を使ってセルを再構築
        if (hasWarpZones)
        {
            NativeArray<BulletData> bullets = warpZones.AsArray();
            BulletDataUpdateJob job3 = new()
            {
                bullets = bullets,
                dt = _dt,
                grid = grid,
                playerVelocity = playerVelocity
            };
            JobHandle handle3 = job3.Schedule(bullets.Length, 64);
            handle3.Complete();
        }

        ApplyWarpZones(_dt);

        RebuildCellsFromBullets();
        SyncNativeBulletDebugViews();
    }

    private void ApplyWarpZones(float dt)
    {
        if (!enemyBullets.IsCreated || enemyBullets.Length == 0) return;
        if (!warpZones.IsCreated || warpZones.Length < 2) return;

        WarpBulletJob job = new WarpBulletJob
        {
            bullets = enemyBullets.AsArray(),
            warpZones = warpZones.AsArray(),
            dt = dt,
            warpCooldown = defaultWarpCooldown,
            grid = CreateQuadGrid(),
            reflectXTypeId = warpZoneReflectXTypeId,
            reflectYTypeId = warpZoneReflectYTypeId
        };

        JobHandle handle = job.Schedule(enemyBullets.Length, 64);
        handle.Complete();
    }

    private void ClearAllCells()
    {
        for (int i = 0; i < cells.Length; i++)
        {
            cells[i].ClearMultiBullets();
            cells[i].ClearAllBullets();
        }
    }

    private void RebuildCellsFromBullets()
    {
        ClearAllCells();

        if (enemyBullets.IsCreated)
        {
            for (int i = 0; i < enemyBullets.Length; i++)
            {
                BulletData bullet = enemyBullets[i];
                if (!bullet.isActive || bullet.isClearing) continue;
                RegisterBulletToCollisionCells(bullet);
            }
        }
    }

    private void RegisterBulletToCollisionCells(BulletData bullet)
    {
        QuadGrid grid = CreateQuadGrid();
        if (!grid.IsValid)
        {
            int fallbackCell = bullet.areaNum;
            if (fallbackCell >= 0 && fallbackCell < cells.Length)
            {
                cells[fallbackCell].enemyBullets.Add(bullet);
            }
            return;
        }

        float radius = GetCollisionBroadphaseRadius(bullet);
        if (radius <= 0f)
        {
            int fallbackCell = bullet.areaNum;
            if (fallbackCell >= 0 && fallbackCell < cells.Length)
            {
                cells[fallbackCell].enemyBullets.Add(bullet);
            }
            return;
        }

        if (!grid.TryGetCellRange(bullet.position, radius, out int2 minCell, out int2 maxCell)) return;

        for (int y = minCell.y; y <= maxCell.y; y++)
        {
            for (int x = minCell.x; x <= maxCell.x; x++)
            {
                int areaNum = grid.GetTreeNum(x, y);
                if (areaNum < 0 || areaNum >= cells.Length) continue;
                cells[areaNum].enemyBullets.Add(bullet);
            }
        }
    }

    private float GetCollisionBroadphaseRadius(BulletData bullet)
    {
        float uniformScale = math.cmax(math.abs(bullet.scale));
        if (!collisionVertRanges.IsCreated || !collisionVerts.IsCreated)
        {
            return uniformScale;
        }

        if (bullet.typeId < 0 || bullet.typeId >= collisionVertRanges.Length)
        {
            return uniformScale;
        }

        int2 range = collisionVertRanges[bullet.typeId];
        if (range.x < 0 || range.y <= 0 || range.x + range.y > collisionVerts.Length)
        {
            return uniformScale;
        }

        float2 absScale = math.abs(bullet.scale);
        float maxLenSq = 0f;
        for (int i = 0; i < range.y; i++)
        {
            float2 scaled = collisionVerts[range.x + i] * absScale;
            maxLenSq = math.max(maxLenSq, math.lengthsq(scaled));
        }

        return maxLenSq > 0f ? math.sqrt(maxLenSq) : uniformScale;
    }

    private void EnsureEnemyBulletList(int capacity = 256)
    {
        if (!enemyBullets.IsCreated)
        {
            enemyBullets = new NativeList<BulletData>(capacity, Allocator.Persistent);
        }
    }

    private void EnsureWarpZoneList(int capacity = 16)
    {
        if (!warpZones.IsCreated)
        {
            warpZones = new NativeList<BulletData>(capacity, Allocator.Persistent);
        }
    }

    private int AddWarpZone(BulletData bullet)
    {
        EnsureWarpZoneList();
        if (warpZones.Length >= warpZones.Capacity)
        {
            int nextCapacity = math.max(warpZones.Length + 1, math.max(16, warpZones.Capacity * 2));
            warpZones.Capacity = nextCapacity;
        }
        int warpZoneIndex = warpZones.Length;
        warpZones.Add(bullet);
        return warpZoneIndex;
    }

    private int AddEnemyBulletSlot(BulletData bullet)
    {
        EnsureEnemyBulletList();
        if (enemyBullets.Length >= enemyBullets.Capacity)
        {
            int nextCapacity = math.max(enemyBullets.Length + 1, math.max(256, enemyBullets.Capacity * 2));
            enemyBullets.Capacity = nextCapacity;
        }

        int bulletIndex = enemyBullets.Length;
        enemyBullets.Add(bullet);
        return bulletIndex;
    }

    private bool TryAddGeneratedBullet(BulletData bullet, out int enemyBulletIndex)
    {
        if (TryAddGeneratedManagedBullet(bullet, out ManagedBulletHandle handle)
            && handle.kind == ManagedBulletKind.EnemyBullet)
        {
            enemyBulletIndex = handle.index;
            return true;
        }

        enemyBulletIndex = -1;
        return false;
    }

    public List<int> AddEnemyBullets(NativeArray<BulletData> newBullets, float2 fromPos = new float2())
    {
        if (newBullets.Length == 0) return new List<int>();

        List<int> indexes = new List<int>(newBullets.Length);

        // 新しい弾をコピー
        for (int i = 0; i < newBullets.Length; i++)
        {
            if (TryAddGeneratedBullet(newBullets[i], out int enemyBulletIndex))
            {
                indexes.Add(enemyBulletIndex);
            }
        }
        return indexes;
    }

    private bool TryAddGeneratedManagedBullet(BulletData bullet, out ManagedBulletHandle handle)
    {
        if (IsScreenNoise(bullet))
        {
            GManager.Control.CManager?.StartScreenNoise(bullet);
            handle = new ManagedBulletHandle(ManagedBulletKind.EnemyBullet, -1);
            return false;
        }

        ResolveWarpZoneTypeId();
        if (IsWarpZone(bullet))
        {
            int warpZoneIndex = AddWarpZone(bullet);
            handle = new ManagedBulletHandle(ManagedBulletKind.WarpZone, warpZoneIndex);
            return true;
        }

        int enemyBulletIndex = AddEnemyBulletSlot(bullet);
        handle = new ManagedBulletHandle(ManagedBulletKind.EnemyBullet, enemyBulletIndex);
        return true;
    }

    public List<ManagedBulletHandle> AddManagedBullets(NativeArray<BulletData> newBullets)
    {
        if (newBullets.Length == 0) return new List<ManagedBulletHandle>();

        List<ManagedBulletHandle> handles = new List<ManagedBulletHandle>(newBullets.Length);
        for (int i = 0; i < newBullets.Length; i++)
        {
            if (TryAddGeneratedManagedBullet(newBullets[i], out ManagedBulletHandle handle) && handle.IsValid)
            {
                handles.Add(handle);
            }
        }

        return handles;
    }

    public List<int> AddEnemyBullets(int index, float2 pos, float2 originVlc, float angle, float4 color)
    {
        return EmitBulletBuffer(index, pos, originVlc, angle, color);
    }

    public List<int> EmitBulletBuffer(int index, float2 pos, float2 originVlc, float angle, float4 color)
    {
        List<BulletData> bullets = GManager.Control.BulletBuffers.CreateSpawnedBullets(index, GManager.Control.PController.pos, pos, originVlc, angle, color, out bool isLaser);

        if (bullets == null || bullets.Count == 0)
        {
            return new List<int>();
        }

        if (isLaser)
        {
            //Debug.Log($"Emitting LASER with index {index} at pos {pos} with angle {angle}");
            allLASERs.AddRange(laserEmitter.EmitLASER(bullets));
            return new List<int>();
        }

        NativeArray<BulletData> newBullets = new NativeArray<BulletData>(bullets.ToArray(), Allocator.Temp);
        List<int> indexes = AddEnemyBullets(newBullets);
        newBullets.Dispose();
        return indexes;
    }

    public List<ManagedBulletHandle> EmitManagedBulletBuffer(int index, float2 pos, float2 originVlc, float angle, float4 color)
    {
        List<BulletData> bullets = GManager.Control.BulletBuffers.CreateSpawnedBullets(index, GManager.Control.PController.pos, pos, originVlc, angle, color, out bool isLaser);

        if (bullets == null || bullets.Count == 0)
        {
            return new List<ManagedBulletHandle>();
        }

        if (isLaser)
        {
            allLASERs.AddRange(laserEmitter.EmitLASER(bullets));
            return new List<ManagedBulletHandle>();
        }

        NativeArray<BulletData> newBullets = new NativeArray<BulletData>(bullets.ToArray(), Allocator.Temp);
        List<ManagedBulletHandle> handles = AddManagedBullets(newBullets);
        newBullets.Dispose();
        return handles;
    }

    public List<int> EmitBulletBuffer(BulletBufferEmission emission, BulletData source, float dt = 0f)
    {
        if (emission == null || !emission.HasResolvedClip) return new List<int>();

        if (emission.index == -3)
        {
            ClearManagedEnemyDanmaku();
            return new List<int>();
        }

        float angle = emission.inheritSourceAngle
            ? source.angle * Mathf.Rad2Deg + emission.angleOffset
            : emission.angleOffset;
        float2 originVlc = emission.originVlc;
        if (emission.inheritSourceVelocity && dt > 1e-5f)
        {
            originVlc += source.velocity / dt;
        }

        return EmitBulletBuffer(emission.index, source.position, originVlc, angle, emission.color);
    }

    public List<ManagedBulletHandle> EmitManagedBulletBuffer(BulletBufferEmission emission, BulletData source, float dt = 0f)
    {
        if (emission == null || !emission.HasResolvedClip) return new List<ManagedBulletHandle>();

        if (emission.index == -3)
        {
            ClearManagedEnemyDanmaku();
            return new List<ManagedBulletHandle>();
        }

        float angle = emission.inheritSourceAngle
            ? source.angle * Mathf.Rad2Deg + emission.angleOffset
            : emission.angleOffset;
        float2 originVlc = emission.originVlc;
        if (emission.inheritSourceVelocity && dt > 1e-5f)
        {
            originVlc += source.velocity / dt;
        }

        return EmitManagedBulletBuffer(emission.index, source.position, originVlc, angle, emission.color);
    }

    public void ApplyBulletOrbit(List<int> bulletIndexes, BulletBufferEmission emission)
    {
        if (bulletIndexes == null || bulletIndexes.Count == 0) return;

        for (int i = 0; i < bulletIndexes.Count; i++)
        {
            int index = bulletIndexes[i];
            if (index < 0 || index >= enemyBullets.Length) continue;

            BulletData bullet = enemyBullets[index];
            if (!bullet.isActive) continue;

            if (GManager.Control.BulletBuffers.TryGetBulletBuffer(emission.index, out BulletData bulletData))
            {
                bullet.speed = bulletData.speed;
                bullet.gravity = bulletData.gravity;
                bullet.angleSpeed = bulletData.angleSpeed;
                bullet.initialAngle = bulletData.initialAngle;
                bullet.useVelocityAngle = bulletData.useVelocityAngle;
                bullet.polarForm = new float2(bulletData.polarForm.x, bulletData.polarForm.y + bullet.polarForm.y);
                bullet.radiusVlc = bulletData.radiusVlc;
                bullet.radiusAccel = bulletData.radiusAccel;
                bullet.thetaVlc = bulletData.thetaVlc;
                bullet.thetaAccel = bulletData.thetaAccel;
                bullet.polynomial = bulletData.polynomial;

                enemyBullets[index] = bullet;
            }
        }
    }

    public void ApplyBulletOrbit(List<ManagedBulletHandle> bulletHandles, BulletBufferEmission emission)
    {
        if (bulletHandles == null || bulletHandles.Count == 0) return;
        if (!GManager.Control.BulletBuffers.TryGetBulletBuffer(emission.index, out BulletData bulletData)) return;

        for (int i = 0; i < bulletHandles.Count; i++)
        {
            ManagedBulletHandle handle = bulletHandles[i];
            if (!TryGetManagedBulletData(handle, out BulletData bullet)) continue;
            if (!bullet.isActive) continue;

            ApplyBulletOrbitProperties(ref bullet, bulletData);
            SetManagedBulletData(handle, bullet);
        }
    }

    private static void ApplyBulletOrbitProperties(ref BulletData bullet, BulletData bulletData)
    {
        bullet.speed = bulletData.speed;
        bullet.gravity = bulletData.gravity;
        bullet.angleSpeed = bulletData.angleSpeed;
        bullet.initialAngle = bulletData.initialAngle;
        bullet.useVelocityAngle = bulletData.useVelocityAngle;
        bullet.polarForm = new float2(bulletData.polarForm.x, bulletData.polarForm.y + bullet.polarForm.y);
        bullet.radiusVlc = bulletData.radiusVlc;
        bullet.radiusAccel = bulletData.radiusAccel;
        bullet.thetaVlc = bulletData.thetaVlc;
        bullet.thetaAccel = bulletData.thetaAccel;
        bullet.polynomial = bulletData.polynomial;
    }

    public NativeArray<BulletData> GetEnemyBullets() => enemyBullets.IsCreated ? enemyBullets.AsArray() : default;

    public int GetEnemyBulletCount() => CountActiveBullets(enemyBullets);

    public NativeArray<BulletData> GetWarpZones() => warpZones.IsCreated ? warpZones.AsArray() : default;

    public int GetWarpZoneCount() => CountActiveBullets(warpZones);

    public bool TryGetEnemyBulletData(int index, out BulletData bullet)
    {
        if (!enemyBullets.IsCreated || index < 0 || index >= enemyBullets.Length)
        {
            bullet = default;
            return false;
        }

        bullet = enemyBullets[index];
        return true;
    }

    public bool TryGetManagedBulletData(ManagedBulletHandle handle, out BulletData bullet)
    {
        if (!handle.IsValid)
        {
            bullet = default;
            return false;
        }

        switch (handle.kind)
        {
            case ManagedBulletKind.EnemyBullet:
                return TryGetEnemyBulletData(handle.index, out bullet);
            case ManagedBulletKind.WarpZone:
                if (!warpZones.IsCreated || handle.index < 0 || handle.index >= warpZones.Length)
                {
                    bullet = default;
                    return false;
                }

                bullet = warpZones[handle.index];
                return true;
            default:
                bullet = default;
                return false;
        }
    }

    public BulletData GetEnemyBulletData(int index)
    {
        if (!enemyBullets.IsCreated || index < 0 || index >= enemyBullets.Length)
        {
            throw new IndexOutOfRangeException($"Bullet index {index} is out of range.");
        }
        return enemyBullets[index];
    }

    public void SetEnemyBulletActive(int index, bool active)
    {
        if (!enemyBullets.IsCreated || index < 0 || index >= enemyBullets.Length) return;

        BulletData bullet = enemyBullets[index];
        bullet.isActive = active;
        enemyBullets[index] = bullet;
    }

    public void SetManagedBulletActive(ManagedBulletHandle handle, bool active)
    {
        if (!TryGetManagedBulletData(handle, out BulletData bullet)) return;

        bullet.isActive = active;
        SetManagedBulletData(handle, bullet);
    }

    private void SetManagedBulletData(ManagedBulletHandle handle, BulletData bullet)
    {
        if (!handle.IsValid) return;

        switch (handle.kind)
        {
            case ManagedBulletKind.EnemyBullet:
                if (!enemyBullets.IsCreated || handle.index < 0 || handle.index >= enemyBullets.Length) return;
                enemyBullets[handle.index] = bullet;
                break;
            case ManagedBulletKind.WarpZone:
                if (!warpZones.IsCreated || handle.index < 0 || handle.index >= warpZones.Length) return;
                warpZones[handle.index] = bullet;
                break;
        }
    }

    public NativeArray<CounterBullet> GetCounterBullets() => counterBullets.IsCreated ? counterBullets.AsArray() : default;

    public int GetCounterBulletCount() => CountActiveCounterBullets(counterBullets);

    private int CountActiveBullets(NativeList<BulletData> bullets)
    {
        if (!bullets.IsCreated || bullets.Length == 0) return 0;

        int count = 0;
        for (int i = 0; i < bullets.Length; i++)
        {
            if (bullets[i].isActive) count++;
        }

        return count;
    }

    private int CountActiveCounterBullets(NativeList<CounterBullet> bullets)
    {
        if (!bullets.IsCreated || bullets.Length == 0) return 0;

        int count = 0;
        for (int i = 0; i < bullets.Length; i++)
        {
            if (bullets[i].isActive) count++;
        }

        return count;
    }

    private void SpawnCounterBullet(BulletData sourceBullet, float dt)
    {
        if (!counterBullets.IsCreated)
        {
            counterBullets = new NativeList<CounterBullet>(256, Allocator.Persistent);
        }

        float invDt = dt > 1e-5f ? 1f / dt : 0f;

        CounterBullet counterBullet = new CounterBullet
        {
            position = sourceBullet.position,
            velocity = sourceBullet.velocity * invDt,
            damage = bulletPowers.IsCreated && sourceBullet.typeId >= 0 && sourceBullet.typeId < bulletPowers.Length
                ? bulletPowers[sourceBullet.typeId] * math.cmax(math.abs(sourceBullet.scale))
                : 0f,
            isActive = true,
            launched = false,
            homingElapsed = 0f,
            spawnElapsed = 0f,
            spawnDelay = CounterBullet.SpawnDelay,
            sourceTypeId = sourceBullet.typeId,
            sourceScale = sourceBullet.scale,
            sourceAngle = sourceBullet.GetRotationAngle(),
            sourceColor = sourceBullet.color,
        };

        counterBullets.Add(counterBullet);
    }

    private void UpdateCounterBullets(float dt)
    {
        if (!counterBullets.IsCreated || counterBullets.Length == 0) return;
        int activeBeforeUpdate = CountActiveCounterBullets(counterBullets);

        float2 bossPos = boss != null
            ? new float2(boss.transform.position.x, boss.transform.position.y)
            : new float2(GManager.Control.PController.pos.x, GManager.Control.PController.pos.y);

        CounterBulletUpdateJob job = new CounterBulletUpdateJob
        {
            bullets = counterBullets.AsArray(),
            bossPos = bossPos,
            dt = dt
        };

        JobHandle handle = job.Schedule(counterBullets.Length, 64);
        handle.Complete();

        if (boss != null)
        {
            int activeAfterUpdate = CountActiveCounterBullets(counterBullets);
            int hitCount = activeBeforeUpdate - activeAfterUpdate;
            if (hitCount > 0)
            {
                GManager.Control?.AddCounterHitBossCount(hitCount);
            }
        }
    }

    [ContextMenu("Refresh Native Bullet Debug Views")]
    private void RefreshNativeBulletDebugViews()
    {
        SyncNativeBulletDebugViews(forceRefresh: true);
    }

    private bool IsRaymeeDebugEnabled()
    {
        return GManager.Control != null && GManager.Control.isRaymeeDebug;
    }

    private void SyncNativeBulletDebugViews(bool forceRefresh = false)
    {
        if (!IsRaymeeDebugEnabled())
        {
            ClearNativeBulletDebugView(
                ref debugEnemyBulletSlotCount,
                ref debugEnemyBulletActiveCount,
                debugEnemyBulletEntries
            );

            ClearNativeBulletDebugView(
                ref debugMultiBulletOrbitSlotCount,
                ref debugMultiBulletOrbitActiveCount,
                debugMultiBulletOrbitEntries
            );
            return;
        }

        SyncNativeBulletDebugView(
            enemyBullets,
            ref debugEnemyBulletSlotCount,
            ref debugEnemyBulletActiveCount,
            debugEnemyBulletEntries,
            forceRefresh
        );
    }

    private void ClearNativeBulletDebugView(
        ref int slotCount,
        ref int activeCount,
        List<NativeBulletDebugEntry> debugEntries)
    {
        slotCount = 0;
        activeCount = 0;
        if (debugEntries.Count > 0)
        {
            debugEntries.Clear();
        }
    }

    private void SyncNativeBulletDebugView(
        NativeList<BulletData> bullets,
        ref int slotCount,
        ref int activeCount,
        List<NativeBulletDebugEntry> debugEntries,
        bool forceRefresh)
    {
        slotCount = bullets.IsCreated ? bullets.Length : 0;
        activeCount = CountActiveBullets(bullets);

        if (!debugSyncNativeBulletListsToInspector && !forceRefresh)
        {
            if (debugEntries.Count > 0)
            {
                debugEntries.Clear();
            }
            return;
        }

        debugEntries.Clear();
        if (!bullets.IsCreated || bullets.Length == 0) return;

        int displayCount = Mathf.Clamp(debugNativeBulletDisplayLimit, 0, bullets.Length);
        for (int i = 0; i < displayCount; i++)
        {
            BulletData bullet = bullets[i];
            debugEntries.Add(new NativeBulletDebugEntry()
            {
                index = i,
                isActive = bullet.isActive,
                areaNum = bullet.areaNum,
                typeId = bullet.typeId,
                time = bullet.time,
                angle = bullet.angle,
                scale = new Vector2(bullet.scale.x, bullet.scale.y),
                position = new Vector2(bullet.position.x, bullet.position.y),
                velocity = new Vector2(bullet.velocity.x, bullet.velocity.y),
                originPos = new Vector2(bullet.originPos.x, bullet.originPos.y),
                originVlc = new Vector2(bullet.originVlc.x, bullet.originVlc.y),
                polarForm = new Vector2(bullet.polarForm.x, bullet.polarForm.y),
            });
        }
    }
    #endregion

    #region //collisionMethods
    private void NotifyPlayerHit(string source)
    {
        PlayerController player = GManager.Control?.PController;
        if (player == null) return;
        if (!player.TryHit()) return;

        GManager.Control?.AddPlayerHitCount();

        if (IsRaymeeDebugEnabled())
        {
            Debug.Log($"{source} collision detected.");
        }
    }

    public void CheckCollisionWithEnemy(float2 pPos, float dt)
    {
        if (collisionDataDirty || !collisionVerts.IsCreated || !collisionVertRanges.IsCreated || !bulletPowers.IsCreated) BuildCollisionData();
        if (!collisionHitFlag.IsCreated) collisionHitFlag = new NativeArray<int>(1, Allocator.Persistent);
        if (!grazePower.IsCreated) grazePower = new NativeArray<float>(1, Allocator.Persistent);
        if (!collisionCheckBullets.IsCreated) collisionCheckBullets = new NativeList<BulletData>(256, Allocator.Persistent);
        if (!bulletPowers.IsCreated) bulletPowers = new NativeArray<float>(0, Allocator.Persistent);

        bool isPlayerDash = GManager.Control.PController.invincible;
        NativeArray<BulletData> checkBullets;

        if (isPlayerDash)
        {
            if (!enemyBullets.IsCreated || enemyBullets.Length == 0) return;
            if (!dashCollisionActiveFlags.IsCreated)
            {
                dashCollisionActiveFlags = new NativeList<byte>(math.max(256, enemyBullets.Length), Allocator.Persistent);
            }
            if (dashCollisionActiveFlags.Capacity < enemyBullets.Length)
            {
                dashCollisionActiveFlags.Capacity = enemyBullets.Length;
            }
            dashCollisionActiveFlags.ResizeUninitialized(enemyBullets.Length);
            for (int i = 0; i < enemyBullets.Length; i++)
            {
                dashCollisionActiveFlags[i] = enemyBullets[i].isActive ? (byte)1 : (byte)0;
            }
            // Dash中は実体の弾配列を直接処理して、isActive変更を反映する
            checkBullets = enemyBullets.AsArray();
        }
        else
        {
            QuadGrid grid = CreateQuadGrid();
            int pCellIndex = grid.GetTreeNum(pPos);
            if (pCellIndex < 0) return;

            int2 pCellCoords = QuadGrid.BitCompact32(pCellIndex);
            Vector2Int pCell = new Vector2Int(pCellCoords.x, pCellCoords.y);
            collisionCheckCells.Clear();
            int bulletCount = 0;
            foreach (var cell in cellOffsets)
            {
                Vector2Int checkCell = pCell + cell;
                int treeNum = grid.GetTreeNum(checkCell.x, checkCell.y);
                if (treeNum < 0 || treeNum >= cells.Length) continue;
                collisionCheckCells.Add(treeNum);
                bulletCount += cells[treeNum].enemyBullets.Count;
            }

            if (bulletCount == 0) return;

            collisionCheckBullets.Clear();
            if (collisionCheckBullets.Capacity < bulletCount)
            {
                collisionCheckBullets.Capacity = bulletCount;
            }

            for (int i = 0; i < collisionCheckCells.Count; i++)
            {
                int treeNum = collisionCheckCells[i];
                for (int j = 0; j < cells[treeNum].enemyBullets.Count; j++)
                {
                    collisionCheckBullets.Add(cells[treeNum].enemyBullets[j]);
                }
            }

            checkBullets = collisionCheckBullets.AsArray();
        }
        collisionHitFlag[0] = 0;
        grazePower[0] = 0f;

        BulletCollisionJob collisionJob = new()
        {
            bullets = checkBullets,
            bVerts = collisionVerts,
            bVertRanges = collisionVertRanges,
            bPowers = bulletPowers,
            pPos = pPos,
            grazeRange = 10f,
            isPlayerDash = isPlayerDash,
            isCollided = collisionHitFlag,
            attackPower = grazePower
        };

        if (isPlayerDash)
        {
            collisionJob.Run(checkBullets.Length);

            for (int i = 0; i < enemyBullets.Length && i < dashCollisionActiveFlags.Length; i++)
            {
                if (dashCollisionActiveFlags[i] == 0) continue;
                if (enemyBullets[i].isActive) continue;
                SpawnCounterBullet(enemyBullets[i], dt);
            }
        }
        else
        {
            JobHandle handle = collisionJob.Schedule(checkBullets.Length, 64);
            handle.Complete();
        }

        if (collisionHitFlag[0] != 0)
        {
            NotifyPlayerHit("Enemy bullet");
        }

        if (grazePower[0] > 0f && IsRaymeeDebugEnabled())
        {
            Debug.Log($"Graze detected. Graze power: {grazePower[0]}");
        }
    }

    private void UpdateDirtyLASERCellIndices()
    {
        if (allLASERs == null || allLASERs.Count == 0) return;

        laserBatchLasers.Clear();
        laserBatchStarts.Clear();
        laserBatchCounts.Clear();

        int totalVertCount = 0;
        for (int i = 0; i < allLASERs.Count; i++)
        {
            LASER laser = allLASERs[i];
            if (laser == null || !laser.NeedsCellUpdate) continue;

            int vertexCount = laser.CollisionVertexCount;
            if (vertexCount <= 0)
            {
                laser.ClearCollisionCells();
                continue;
            }

            laserBatchLasers.Add(laser);
            laserBatchStarts.Add(totalVertCount);
            laserBatchCounts.Add(vertexCount);
            totalVertCount += vertexCount;
        }

        if (totalVertCount == 0) return;

        if (!laserBatchVerts.IsCreated)
        {
            laserBatchVerts = new NativeList<float2>(math.max(totalVertCount, 1), Allocator.Persistent);
        }
        if (!laserBatchCellIndices.IsCreated)
        {
            laserBatchCellIndices = new NativeList<int>(math.max(totalVertCount, 1), Allocator.Persistent);
        }
        if (laserBatchVerts.Capacity < totalVertCount) laserBatchVerts.Capacity = totalVertCount;
        if (laserBatchCellIndices.Capacity < totalVertCount) laserBatchCellIndices.Capacity = totalVertCount;

        laserBatchVerts.ResizeUninitialized(totalVertCount);
        laserBatchCellIndices.ResizeUninitialized(totalVertCount);

        NativeArray<float2> batchVerts = laserBatchVerts.AsArray();
        for (int i = 0; i < laserBatchLasers.Count; i++)
        {
            laserBatchLasers[i].CopyCollisionVertsTo(batchVerts, laserBatchStarts[i]);
        }

        LASERQuadJob job = new LASERQuadJob()
        {
            vertsSet = batchVerts,
            vertCellIndices = laserBatchCellIndices.AsArray(),
            grid = CreateQuadGrid()
        };

        JobHandle handle = job.Schedule(totalVertCount, 64);
        handle.Complete();

        NativeArray<int> batchCellIndices = laserBatchCellIndices.AsArray();
        for (int i = 0; i < laserBatchLasers.Count; i++)
        {
            laserBatchLasers[i].ApplyCellIndices(batchCellIndices, laserBatchStarts[i], laserBatchCounts[i]);
        }
    }

    public void CheckCollisionWithLASER(float2 pPos)
    {
        PlayerController player = GManager.Control?.PController;
        if (player == null || player.invincible) return;

        int pCell = GetTreeNum(pPos);
        if (pCell == -1) return;

        for (int i = 0; i < allLASERs.Count; i++)
        {
            LASER laser = allLASERs[i];
            if (laser == null || laser.IsClearing) continue;
            NativeArray<LASERCell> float2sets = laser.GetQuadVerts(pCell);
            if (float2sets.Length == 0)
            {
                float2sets.Dispose();
                continue;
            }
            collisionHitFlag[0] = 0;

            LASERCollisionJob collisionJob = new LASERCollisionJob()
            {
                pPos = pPos,
                laserCells = float2sets,
                isCollided = collisionHitFlag
            };

            JobHandle handle = collisionJob.Schedule(float2sets.Length, 64);
            handle.Complete();

            float2sets.Dispose();
            if (collisionHitFlag[0] != 0)
            {
                NotifyPlayerHit("Laser");
                break;
            }
        }
    }

    public void ClearManagedEnemyDanmaku()
    {
        // Invalidate currently running async enemy spawns.
        enemySpawnGeneration++;
        const float fadeDuration = 0.15f;

        if (enemyBullets.IsCreated)
        {
            for (int i = 0; i < enemyBullets.Length; i++)
            {
                BulletData bullet = enemyBullets[i];
                if (!bullet.isActive || bullet.isClearing) continue;
                bullet.BeginClearFade(fadeDuration);
                enemyBullets[i] = bullet;
            }
        }

        for (int i = 0; i < allLASERs.Count; i++)
        {
            if (allLASERs[i] == null) continue;
            allLASERs[i].BeginFadeOut(fadeDuration);
        }

        for (int i = multiBullets.Count - 1; i >= 0; i--)
        {
            MultiBullet multiBullet = multiBullets[i];
            if (multiBullet == null) continue;
            multiBullet.isActive = false;
        }
        multiBullets.Clear();

        if (warpZones.IsCreated) warpZones.Clear();
        if (collisionCheckBullets.IsCreated) collisionCheckBullets.Clear();
        GManager.Control.CManager?.StopScreenNoise();

        ClearAllCells();

        SyncNativeBulletDebugViews(forceRefresh: true);
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        if (!IsRaymeeDebugEnabled()) return;

        if (drawEnemyBulletCollisionGizmos)
        {
            DrawEnemyBulletCollisionGizmos();
        }

        if (drawLaserCollisionGizmos && allLASERs != null && allLASERs.Count > 0)
        {
            DrawLaserCollisionGizmos();
        }
    }

    private void DrawEnemyBulletCollisionGizmos()
    {
        if (!enemyBullets.IsCreated || enemyBullets.Length == 0) return;
        if (!EnsureCollisionGizmoData()) return;

        Color prevColor = Gizmos.color;
        Gizmos.color = enemyBulletCollisionGizmoColor;

        for (int i = 0; i < enemyBullets.Length; i++)
        {
            BulletData bullet = enemyBullets[i];
            if (!ShouldDrawEnemyBulletCollisionGizmo(bullet)) continue;

            int2 range = collisionVertRanges[bullet.typeId];
            if (!IsValidCollisionVertRange(range)) continue;

            DrawEnemyBulletCollisionPolygon(bullet, range);
        }

        Gizmos.color = prevColor;
    }

    private bool EnsureCollisionGizmoData()
    {
        if (collisionDataDirty || !collisionVerts.IsCreated || !collisionVertRanges.IsCreated)
        {
            if (GManager.Control == null) return false;
            BuildCollisionData();
        }

        return collisionVerts.IsCreated && collisionVertRanges.IsCreated;
    }

    private bool ShouldDrawEnemyBulletCollisionGizmo(BulletData bullet)
    {
        if (bullet.isClearing) return false;
        if (!bullet.isActive) return false;
        if (bullet.appearTime > bullet.time) return false;
        if (bullet.typeId < 0 || bullet.typeId >= collisionVertRanges.Length) return false;
        return true;
    }

    private bool IsValidCollisionVertRange(int2 range)
    {
        if (range.x < 0 || range.y < 3) return false;
        if (range.x >= collisionVerts.Length) return false;
        return range.x + range.y <= collisionVerts.Length;
    }

    private void DrawEnemyBulletCollisionPolygon(BulletData bullet, int2 range)
    {
        Vector3 first = GetEnemyBulletCollisionGizmoVertex(bullet, collisionVerts[range.x]);
        Vector3 previous = first;

        for (int i = 1; i < range.y; i++)
        {
            Vector3 current = GetEnemyBulletCollisionGizmoVertex(bullet, collisionVerts[range.x + i]);
            Gizmos.DrawLine(previous, current);
            previous = current;
        }

        Gizmos.DrawLine(previous, first);
    }

    private Vector3 GetEnemyBulletCollisionGizmoVertex(BulletData bullet, float2 vertex)
    {
        float2 local = vertex * math.abs(bullet.scale);
        float collisionAngle = bullet.GetRotationAngle();
        float cos = math.cos(collisionAngle);
        float sin = math.sin(collisionAngle);
        float2 world = bullet.position + new float2(
            local.x * cos - local.y * sin,
            local.x * sin + local.y * cos
        );

        return new Vector3(world.x, world.y, enemyBulletCollisionGizmoZ);
    }

    private void DrawLaserCollisionGizmos()
    {
        Color prevColor = Gizmos.color;
        Gizmos.color = laserCollisionGizmoColor;

        IterateLaserCollisionTriangles((v0, v1, v2) =>
        {
            Gizmos.DrawLine(v0, v1);
            Gizmos.DrawLine(v1, v2);
            Gizmos.DrawLine(v2, v0);
        });

        Gizmos.color = prevColor;
    }

    private void IterateLaserCollisionTriangles(Action<Vector3, Vector3, Vector3> drawTriangle)
    {
        if (allLASERs == null || allLASERs.Count == 0) return;

        for (int i = 0; i < allLASERs.Count; i++)
        {
            LASER laser = allLASERs[i];
            if (laser == null) continue;

            for (int cellIndex = 0; cellIndex < cellCount; cellIndex++)
            {
                NativeArray<LASERCell> cells = laser.GetQuadVerts(cellIndex);
                for (int k = 0; k < cells.Length; k++)
                {
                    LASERCell cell = cells[k];
                    Vector3 v0 = new Vector3(cell.vert0.x, cell.vert0.y, laserCollisionGizmoZ);
                    Vector3 v1 = new Vector3(cell.vert1.x, cell.vert1.y, laserCollisionGizmoZ);
                    Vector3 v2 = new Vector3(cell.vert2.x, cell.vert2.y, laserCollisionGizmoZ);
                    drawTriangle(v0, v1, v2);
                }
                cells.Dispose();
            }
        }
    }
    #endregion

    #region //MultiBulletMethods
    public void AddMultiBullet(MultiBulletSpawner spawner)
    {
        int multiBulletIndex = multiBullets.Count;
        MultiBullet multiBullet = new();
        multiBullet.Init(multiBulletIndex, spawner);
        multiBullets.Add(multiBullet);
    }

    private void UpdateMultiBullets(float dt)
    {
        for (int i = multiBullets.Count - 1; i >= 0; i--)
        {
            if (i >= multiBullets.Count) continue;

            MultiBullet multiBullet = multiBullets[i];
            if (multiBullet == null || !multiBullet.isActive)
            {
                multiBullets.RemoveAt(i);
                continue;
            }

            multiBullet.UpdateMultiBullet(dt);
            if (i < multiBullets.Count && ReferenceEquals(multiBullets[i], multiBullet) && !multiBullet.isActive)
            {
                multiBullets.RemoveAt(i);
            }
        }
    }
    #endregion

    #region//CellsMethods
    public int GetTreeNum(float2 pos)
    {
        return CreateQuadGrid().GetTreeNum(pos);
    }

    public int BitSeparate32(int n)
    {
        return QuadGrid.BitSeparate32(n);
    }

    public int GetTreeNum(int x, int y)
    {
        return CreateQuadGrid().GetTreeNum(x, y);
    }
    public Vector2Int BitCompact32(int n)
    {
        int2 cell = QuadGrid.BitCompact32(n);
        return new Vector2Int(cell.x, cell.y);
    }

    #endregion
}

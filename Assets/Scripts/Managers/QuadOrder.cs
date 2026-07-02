using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public partial class QuadOrder : MonoBehaviour
{
    #region//CellManagers
    [NonSerialized] private QuadCell[] cells = Array.Empty<QuadCell>();
    [SerializeField] private float cellSize;
    [SerializeField] private int separateLevel;
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
    // One entry per spawned enemy: its MultiBullet, optional Boss visual, and the
    // index of its orbit bullet inside multiBulletOrbitBullets. Replaces the old
    // trio of parallel lists (multiBullets / bossDisplays / orbit array) so the
    // orbit index lives in exactly one place. Orbit data itself stays in the
    // NativeList below for Burst processing.
    [Serializable]
    private class EnemyEntry
    {
        public MultiBullet multiBullet;
        public Boss boss;
        public int orbitIndex;
    }
    [SerializeField]
    private List<EnemyEntry> enemyEntries = new List<EnemyEntry>();
    private NativeList<BulletData> multiBulletOrbitBullets;
    private NativeList<BulletData> warpZones;
    private NativeArray<float2> collisionVerts;
    private NativeArray<int2> collisionVertRanges;
    private NativeArray<float> bulletPowers;
    private NativeArray<int> collisionHitFlag;
    private NativeArray<float> grazePower;
    private NativeList<BulletData> collisionCheckBullets;
    private NativeList<int> laserVertCellIndices;
    private NativeList<byte> dashCollisionActiveFlags;

    [SerializeField] private List<BulletEvent> bulletEvents = new List<BulletEvent>();
    #endregion

    private bool collisionDataDirty = true;
    private List<int> collisionCheckCells = new List<int>(9);
    private int enemySpawnGeneration = 0;
    // Bumped whenever the managed enemy danmaku is cleared. BulletHandles stamped
    // with an older generation are treated as stale (see BulletHandle). The
    // enemyBullets list itself is append-only within a play, so raw indices stay
    // valid; the generation guards against acting on bullets from a prior clear.
    private int enemyBulletGeneration = 0;
    [SerializeField] private int inactiveCleanupInterval = 8;
    private int inactiveCleanupCounter = 0;
    [SerializeField] private bool drawLaserCollisionGizmos = true;
    [SerializeField] private Color laserCollisionGizmoColor = new Color(1f, 0.2f, 0.2f, 0.9f);
    [SerializeField] private float laserCollisionGizmoZ = 0f;
    [Header("Warp Zones")]
    [SerializeField] private int warpZoneTypeId = -1;
    [SerializeField] private string warpZoneTypeName = BulletData.WarpZoneTypeName;
    [SerializeField] private float defaultWarpCooldown = 2f;
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

    // LASERSet moved to QuadOrder.Laser.cs (S7 split).
    public List<LASER> allLASERs = new();

    public List<List<int>> laserVertsIndex = new List<List<int>>();

    public void AwakeSetting()
    {
        int side = 1;
        for (int i = 0; i < separateLevel; i++) side *= 2;
        int n = side * side;
        cellCount = n;
        gridResolution = side;
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
        if (!laserVertCellIndices.IsCreated)
        {
            laserVertCellIndices = new NativeList<int>(256, Allocator.Persistent);
        }
        if (!enemyBullets.IsCreated)
        {
            enemyBullets = new NativeList<BulletData>(256, Allocator.Persistent);
        }
        if (!counterBullets.IsCreated)
        {
            counterBullets = new NativeList<CounterBullet>(256, Allocator.Persistent);
        }
        if (!multiBulletOrbitBullets.IsCreated)
        {
            multiBulletOrbitBullets = new NativeList<BulletData>(256, Allocator.Persistent);
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

        List<BulletClip> clips = new List<BulletClip>() {
                new BulletClip()
                {
                    data = new BulletData(new(0, 0), new(0, 0), 8, 0, 0, 0, new(1, 0), 0, 3, 0, new(0, -2, 1, 0), 0, new float4(1, 1, 1, 1)),
                    number = 8,
                    disRad = 0.3f,
                    homing = false,
                },/*
                new BulletClip()
                {
                    data = new BulletData(new(0, 0), new(0, 0), 8, 0, 0, new(1, 0), 0, 3, -3, 0, new(0, -3, 0, 1), new(1, 1)),
                    number = 3,
                    disRad = 0.3f,
                    homing = false,
                },
                new BulletClip()
                {
                    data = new BulletData(new(0, 0), new(0, 0), 8, 0, 0, new(1, 0), 0, 3, -3, 0, new(0, -2, 4, -1), new(1, 1)),
                    number = 3,
                    disRad = 0.3f,
                    homing = false,
                },*/
            };

        allLASERs.AddRange(laserEmitter.EmitLASER(clips[0], new float2(0, 0)));
        if (boss != null) boss.Init();
    }

    private void OnDestroy()
    {
        if (enemyBullets.IsCreated) enemyBullets.Dispose();
        if (counterBullets.IsCreated) counterBullets.Dispose();
        if (multiBulletOrbitBullets.IsCreated) multiBulletOrbitBullets.Dispose();
        if (warpZones.IsCreated) warpZones.Dispose();
        if (collisionVerts.IsCreated) collisionVerts.Dispose();
        if (collisionVertRanges.IsCreated) collisionVertRanges.Dispose();
        if (bulletPowers.IsCreated) bulletPowers.Dispose();
        if (collisionHitFlag.IsCreated) collisionHitFlag.Dispose();
        if (grazePower.IsCreated) grazePower.Dispose();
        if (collisionCheckBullets.IsCreated) collisionCheckBullets.Dispose();
        if (laserVertCellIndices.IsCreated) laserVertCellIndices.Dispose();
        if (dashCollisionActiveFlags.IsCreated) dashCollisionActiveFlags.Dispose();
    }

    // BuildCollisionData / MarkCollisionDataDirty moved to QuadOrder.Collision.cs (S7 split).

    private void ResolveWarpZoneTypeId()
    {
        if (warpZoneTypeId >= 0) return;
        if (warpZoneTypeResolutionAttempted) return;
        if (string.IsNullOrWhiteSpace(warpZoneTypeName)) return;
        if (GManager.Control == null || GManager.Control.BTDB == null) return;

        warpZoneTypeResolutionAttempted = true;
        int resolvedTypeId = BulletData.ResolveTypeId(warpZoneTypeName, GManager.Control.BTDB);
        if (resolvedTypeId >= 0)
        {
            warpZoneTypeId = resolvedTypeId;
        }
    }

    private bool IsWarpZone(BulletData bullet)
    {
        return warpZoneTypeId >= 0 && bullet.typeId == warpZoneTypeId;
    }

    private bool IsScreenNoise(BulletData bullet)
    {
        return BulletData.IsScreenNoise(bullet);
    }

    private struct BulletComperer : IComparer<BulletData>
    {
        public int Compare(BulletData x, BulletData y) => x.areaNum.CompareTo(y.areaNum);
    }

    public void QuadUpdate(float _dt)
    {
        BulletUpdate(_dt);
        CheckCollisionWithEnemy(GManager.Control.PController.pos, _dt);
        for (int i = 0; i < allLASERs.Count; i++)
        {
            if (allLASERs[i].UpdateSet(_dt))
            {
                allLASERs[i].Destroy();
                allLASERs.RemoveAt(i);
                i--;
            }
        }
        CheckCollisionWithLASER(GManager.Control.PController.pos);

        UpdateMultiBulletPos(_dt);

        //UpdateChangeClip();
        if (boss != null) boss.UpdateBoss(_dt);
        UpdateCounterBullets(_dt);
    }

    #region //BulletMethods
    public void BulletUpdate(float _dt)
    {
        bool hasEnemyBullets = enemyBullets.IsCreated && enemyBullets.Length > 0;
        bool hasMultiBulletOrbitBullets = multiBulletOrbitBullets.IsCreated && multiBulletOrbitBullets.Length > 0;
        bool hasWarpZones = warpZones.IsCreated && warpZones.Length > 0;
        if (!hasEnemyBullets && !hasMultiBulletOrbitBullets && !hasWarpZones)
        {
            ClearAllCells();
            SyncNativeBulletDebugViews();
            return;
        }

        //BulletEventの更新
        for (int i = 0; i < bulletEvents.Count; i++)
        {
            if (bulletEvents[i].Update(_dt))
            {
                bulletEvents.RemoveAt(i);
                i--;
            }
        }

        //敵の弾の更新
        float2 playerVelocity = GManager.Control.PController != null ? GManager.Control.PController.velocity : new float2(0, 0);
        if (hasEnemyBullets)
        {
            NativeArray<BulletData> bullets = enemyBullets.AsArray();
            BulletDataUpdateJob job1 = new()
            {
                bullets = bullets,
                dt = _dt,
                cellSize = cellSize,
                totalCellCount = cells.Length,
                playerVelocity = playerVelocity
            };
            JobHandle handle1 = job1.Schedule(bullets.Length, 64);
            handle1.Complete();
        }

        if (hasMultiBulletOrbitBullets)
        {
            NativeArray<BulletData> bullets = multiBulletOrbitBullets.AsArray();
            //Debug.Log($"Updating {bullets.Length} orbit bullets");
            BulletDataUpdateJob job2 = new()
            {
                bullets = bullets,
                dt = _dt,
                cellSize = cellSize,
                totalCellCount = cells.Length,
                playerVelocity = playerVelocity
            };
            JobHandle handle2 = job2.Schedule(bullets.Length, 64);
            handle2.Complete();
        }

        // areaNum を使ってセルを再構築
        if (hasWarpZones)
        {
            NativeArray<BulletData> bullets = warpZones.AsArray();
            BulletDataUpdateJob job3 = new()
            {
                bullets = bullets,
                dt = _dt,
                cellSize = cellSize,
                totalCellCount = cells.Length,
                playerVelocity = playerVelocity
            };
            JobHandle handle3 = job3.Schedule(bullets.Length, 64);
            handle3.Complete();
        }

        ApplyWarpZones(_dt);

        RebuildCellsFromBullets();
        SyncNativeBulletDebugViews();
    }

    private void RemoveInactiveBullets()
    {
        if (!enemyBullets.IsCreated || enemyBullets.Length == 0) return;


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
            cellSize = cellSize,
            totalCellCount = cells.Length
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

        if (multiBulletOrbitBullets.IsCreated)
        {
            for (int i = 0; i < multiBulletOrbitBullets.Length; i++)
            {
                BulletData bullet = multiBulletOrbitBullets[i];
                if (!bullet.isActive) continue;
                int areaNum = bullet.areaNum;
                if (areaNum < 0 || areaNum >= cells.Length) continue;
                if (i >= enemyEntries.Count) continue;
                cells[areaNum].multiBullets.Add(enemyEntries[i].multiBullet);
            }
        }
    }

    // RegisterBulletToCollisionCells / GetCollisionBroadphaseRadius moved to QuadOrder.Collision.cs (S7 split).

    public List<int> AddEnemyHomingBullets(NativeArray<BulletData> newBullets, float2 fromPos)
    {
        NativeArray<BulletData> tempBullets = newBullets;
        float2 toPlayer = GManager.Control.PController.pos - fromPos;
        float angleToPlayer = math.atan2(toPlayer.y, toPlayer.x);
        for (int i = 0; i < newBullets.Length; i++)
        {
            BulletData bullet = newBullets[i];
            bullet.Init(fromPos);
            bullet.originPos = fromPos;
            bullet.polarForm = new float2(bullet.polarForm.x, bullet.polarForm.y + angleToPlayer);
            tempBullets[i] = bullet;
        }

        List<int> indexes = AddEnemyBullets(tempBullets);
        newBullets.Dispose();
        return indexes;
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

    private void AddWarpZone(BulletData bullet)
    {
        EnsureWarpZoneList();
        if (warpZones.Length >= warpZones.Capacity)
        {
            int nextCapacity = math.max(warpZones.Length + 1, math.max(16, warpZones.Capacity * 2));
            warpZones.Capacity = nextCapacity;
        }
        warpZones.Add(bullet);
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
        if (IsScreenNoise(bullet))
        {
            GManager.Control.CManager?.StartScreenNoise(bullet);
            enemyBulletIndex = -1;
            return false;
        }

        ResolveWarpZoneTypeId();
        if (IsWarpZone(bullet))
        {
            AddWarpZone(bullet);
            enemyBulletIndex = -1;
            return false;
        }

        enemyBulletIndex = AddEnemyBulletSlot(bullet);
        return true;
    }

    public List<int> AddEnemyBullets(NativeArray<BulletData> newBullets, float2 fromPos = new float2())
    {
        if (newBullets.Length == 0) return null;

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

    public void AddEnemyBullets(int index, float2 pos, float2 originVlc, float angle, float4 color)
    {
        List<BulletData> bullets = GManager.Control.BClipManager.GetBulletClip(index, GManager.Control.PController.pos, pos, originVlc, angle, color, out bool isLaser);

        if (bullets == null || bullets.Count == 0)
        {
            return;
        }

        if (isLaser)
        {
            //Debug.Log($"Emitting LASER with index {index} at pos {pos} with angle {angle}");
            allLASERs.AddRange(laserEmitter.EmitLASER(bullets, pos));
            return;
        }

        NativeArray<BulletData> newBullets = new NativeArray<BulletData>(bullets.ToArray(), Allocator.Temp);
        AddEnemyBullets(newBullets);
        newBullets.Dispose();
    }

    public List<int> EmitEnemyBullet(BulletClip clip, int EnemyIndex)
    {
        BulletData data = multiBulletOrbitBullets[EnemyIndex];
        float2 vlc = data.velocity;
        float angle = math.atan2(vlc.y, vlc.x);
        return EmitEnemyBullet(clip, multiBulletOrbitBullets[EnemyIndex].position, angle);
    }

    public List<int> EmitEnemyBullet(BulletClip clip, float2 pPos, float d)
    {
        if (clip.homing) return EmitEnemyBullet(clip, pPos);

        if (!enemyBullets.IsCreated)
        {
            enemyBullets = new NativeList<BulletData>(256, Allocator.Persistent);
        }

        if (enemyBullets.Length >= enemyBullets.Capacity)
        {
            int nextCapacity = math.max(enemyBullets.Length + 1, math.max(256, enemyBullets.Capacity * 2));
            enemyBullets.Capacity = nextCapacity;
        }

        NativeArray<BulletData> newBullets = new NativeArray<BulletData>(clip.number, Allocator.Temp);

        float range = (clip.number - 1) * clip.disRad;
        for (int i = 0; i < clip.number; i++)
        {
            BulletData bullet = clip.data;
            bullet.Init(pPos);
            float angle = d + math.radians(-range / 2 + clip.disRad * i);
            bullet.polarForm = new float2(bullet.polarForm.x, angle);
            newBullets[i] = bullet;
        }

        List<int> indexes = AddEnemyBullets(newBullets);
        newBullets.Dispose();
        return indexes;
    }
    public List<int> EmitEnemyBullet(BulletClip clip, float2 pPos)
    {
        if (!enemyBullets.IsCreated)
        {
            enemyBullets = new NativeList<BulletData>(256, Allocator.Persistent);
        }

        if (enemyBullets.Length >= enemyBullets.Capacity)
        {
            int nextCapacity = math.max(enemyBullets.Length + 1, math.max(256, enemyBullets.Capacity * 2));
            enemyBullets.Capacity = nextCapacity;
        }

        NativeArray<BulletData> newBullets = new NativeArray<BulletData>(clip.number, Allocator.Temp);

        float2 dis = new float2(GManager.Control.PController.pos.x, GManager.Control.PController.pos.y) - pPos;
        float range = (clip.number - 1) * clip.disRad;
        if (clip.homing)
        {
            float baseAngle = math.atan2(dis.y, dis.x);

            for (int i = 0; i < clip.number; i++)
            {
                BulletData bullet = clip.data;
                bullet.Init(pPos);
                float angle = baseAngle + math.radians(-range / 2 + clip.disRad * i);
                bullet.polarForm = new float2(bullet.polarForm.x, angle);
                newBullets[i] = bullet;
            }
        }
        else
        {
            for (int i = 0; i < clip.number; i++)
            {
                BulletData bullet = clip.data;
                bullet.Init(pPos);
                float angle = math.radians(-range / 2 + clip.disRad * i);
                bullet.polarForm = new float2(bullet.polarForm.x, angle);
                newBullets[i] = bullet;
            }
        }

        List<int> indexes = AddEnemyBullets(newBullets);
        newBullets.Dispose();
        return indexes;
    }

    public void StartBulletEvent(BulletEvent bulletEvent)
    {
        if (bulletEvent == null) return;

        if (bulletEvent.Evoke()) return;
        else bulletEvents.Add(bulletEvent);
    }

    public NativeArray<BulletData> GetEnemyBullets() => enemyBullets.IsCreated ? enemyBullets.AsArray() : default;

    public int GetEnemyBulletCount() => CountActiveBullets(enemyBullets);

    public NativeArray<BulletData> GetWarpZones() => warpZones.IsCreated ? warpZones.AsArray() : default;

    public int GetWarpZoneCount() => CountActiveBullets(warpZones);

    public BulletData GetEnemyBulletData(int index)
    {
        if (!enemyBullets.IsCreated || index < 0 || index >= enemyBullets.Length)
        {
            throw new IndexOutOfRangeException($"Bullet index {index} is out of range.");
        }
        return enemyBullets[index];
    }

    public NativeArray<CounterBullet> GetCounterBullets() => counterBullets.IsCreated ? counterBullets.AsArray() : default;

    public int GetCounterBulletCount() => CountActiveCounterBullets(counterBullets);

    // CountActiveBullets / CountActiveCounterBullets / SpawnCounterBullet /
    // UpdateCounterBullets moved to QuadOrder.Collision.cs (S7 split).

    [ContextMenu("Refresh Native Bullet Debug Views")]
    private void RefreshNativeBulletDebugViews()
    {
        SyncNativeBulletDebugViews(forceRefresh: true);
    }

    private void SyncNativeBulletDebugViews(bool forceRefresh = false)
    {
        SyncNativeBulletDebugView(
            enemyBullets,
            ref debugEnemyBulletSlotCount,
            ref debugEnemyBulletActiveCount,
            debugEnemyBulletEntries,
            forceRefresh
        );

        SyncNativeBulletDebugView(
            multiBulletOrbitBullets,
            ref debugMultiBulletOrbitSlotCount,
            ref debugMultiBulletOrbitActiveCount,
            debugMultiBulletOrbitEntries,
            forceRefresh
        );
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
    // NotifyPlayerHit / CheckCollisionWithEnemy moved to QuadOrder.Collision.cs (S7 split).
    // UpdateLASERVerts / CheckCollisionWithLASER moved to QuadOrder.Laser.cs (S7 split).

    public void ClearManagedEnemyDanmaku()
    {
        // Invalidate currently running async enemy spawns and any outstanding
        // BulletHandles (a new generation makes prior handles resolve as stale).
        enemySpawnGeneration++;
        enemyBulletGeneration++;
        bulletEvents.Clear();

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

        for (int i = enemyEntries.Count - 1; i >= 0; i--)
        {
            MultiBullet multiBullet = enemyEntries[i].multiBullet;
            if (multiBullet == null) continue;
            multiBullet.isActive = false;
            UnityEngine.Object.Destroy(multiBullet.gameObject);
        }
        enemyEntries.Clear();

        if (multiBulletOrbitBullets.IsCreated) multiBulletOrbitBullets.Clear();
        if (warpZones.IsCreated) warpZones.Clear();
        if (collisionCheckBullets.IsCreated) collisionCheckBullets.Clear();
        GManager.Control.CManager?.StopScreenNoise();

        ClearAllCells();

        SyncNativeBulletDebugViews(forceRefresh: true);
    }

    // OnDrawGizmos / DrawLaserCollisionGizmos / IterateLaserCollisionTriangles
    // moved to QuadOrder.Laser.cs (S7 split).
    #endregion

    // MultiBulletMethods region (AddMultiBullet / UpdateMultiBulletPos) and
    // GenerateMethods region (UpdateBulletData) moved to QuadOrder.Enemies.cs (S7 split).

    #region//CellsMethods
    public int GetTreeNum(float2 pos)
    {
        if (pos.x < 0 || pos.y < 0) return -1;
        int nx = Mathf.FloorToInt(pos.x / cellSize);
        int ny = Mathf.FloorToInt(pos.y / cellSize);

        int result = BitSeparate32(nx) | (BitSeparate32(ny) << 1);
        if (result >= 0 && result < cells.Length) return result;
        return -1;
    }

    public int BitSeparate32(int n)
    {
        n = (n | n << 8) & 0x00ff00ff;
        n = (n | n << 4) & 0x0f0f0f0f;
        n = (n | n << 2) & 0x33333333;
        return (n | n << 1) & 0x55555555;
    }

    public int GetTreeNum(int x, int y)
    {
        if (x < 0 || y < 0) return -1;
        return BitSeparate32(x) | (BitSeparate32(y) << 1);
    }
    public Vector2Int BitCompact32(int n)
    {
        int y = (n >> 1);
        n = (n & 0x55555555);               // 1ビットおきに残す
        n = (n | n >> 1) & 0x33333333;      // 2ビットおきに集約
        n = (n | n >> 2) & 0x0f0f0f0f;      // 4ビットおきに集約
        n = (n | n >> 4) & 0x00ff00ff;      // 8ビットおきに集約
        n = (n | n >> 8) & 0x0000ffff;      // 16ビットおきに集約


        y = (y & 0x55555555);               // 1ビットおきに残す
        y = (y | y >> 1) & 0x33333333;      // 2ビットおきに集約
        y = (y | y >> 2) & 0x0f0f0f0f;      // 4ビットおきに集約
        y = (y | y >> 4) & 0x00ff00ff;      // 8ビットおきに集約
        y = (y | y >> 8) & 0x0000ffff;      // 16ビットおきに集約

        return new Vector2Int(n, y);
    }

    #endregion
}

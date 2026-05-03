using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

using BulletHell.Enemies;
using BulletHell.Player;
using BulletHell.Data;
using BulletHell.Core;
using BulletHell.App;

namespace BulletHell.Bullets
{

[Serializable]
public class QuadOrder : MonoBehaviour, IQuadOrderDirty
{
    private IDBService DBService;
    private PlayerController PController;
    private IQuadGrid quadGrid;
    private IQuadBulletStore quadBulletStore;

    #region//CellManagers
    private QuadCell[] cells => quadGrid.cells;
    private float cellSize => quadGrid.cellSize;
    private int separateLevel => quadGrid.separateLevel;
    public int cellCount => quadGrid.cellCount;
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
    #endregion

    #region //Arrays
    [SerializeField] private List<Boss> bosses = new List<Boss>();

    private NativeArray<BulletData> playerBullets => quadBulletStore.playerBullets;
    private NativeArray<BulletData> enemyBullets => quadBulletStore.enemyBullets;
    private NativeArray<BulletData> enemiesOrbitBullets => quadBulletStore.enemiesOrbitBullets
    ;
    [SerializeField]
    private List<IEnemy<IEnemyDB>> enemies = new();
    private NativeArray<float2> collisionVerts;
    private NativeArray<int2> collisionVertRanges;
    private NativeArray<int> collisionHitFlag;
    private NativeList<BulletData> collisionCheckBullets;
    private NativeList<int> laserVertCellIndices;

    [SerializeField] private List<BulletEvent> bulletEvents = new List<BulletEvent>();
    #endregion

    private bool collisionDataDirty = true;
    private List<int> collisionCheckCells = new List<int>(9);
    [SerializeField] private int inactiveCleanupInterval = 8;
    private int inactiveCleanupCounter = 0;
    [SerializeField] private bool drawLaserCollisionGizmos = true;
    [SerializeField] private Color laserCollisionGizmoColor = new Color(1f, 0.2f, 0.2f, 0.9f);
    [SerializeField] private float laserCollisionGizmoZ = 0f;
    [Header("Debug")]
    [SerializeField] private bool debugSyncNativeBulletListsToInspector;
    [SerializeField] private int debugNativeBulletDisplayLimit = 128;
    [SerializeField] private int debugEnemyBulletSlotCount;
    [SerializeField] private int debugEnemyBulletActiveCount;
    [SerializeField] private List<NativeBulletDebugEntry> debugEnemyBulletEntries = new List<NativeBulletDebugEntry>();
    [SerializeField] private int debugEnemiesOrbitBulletSlotCount;
    [SerializeField] private int debugEnemiesOrbitBulletActiveCount;
    [SerializeField] private List<NativeBulletDebugEntry> debugEnemiesOrbitBulletEntries = new List<NativeBulletDebugEntry>();

    public LaserEmitter laserEmitter;

    public List<LASER> allLASERs = new();

    public List<List<int>> laserVertsIndex = new List<List<int>>();

    public void Init
    (
        IDBService dbService,
        PlayerController playerController,
        IQuadGrid quadGrid,
        IQuadBulletStore quadBulletStore,
        LaserEmitter laserEmitter
    )
    {
        DBService = dbService;
        PController = playerController;
        this.quadGrid = quadGrid;
        this.quadBulletStore = quadBulletStore;
        this.laserEmitter = laserEmitter;
    }

    public void AwakeSetting()
    {

        BuildCollisionData();
        if (!collisionHitFlag.IsCreated)
        {
            collisionHitFlag = new NativeArray<int>(1, Allocator.Persistent);
        }
        if (!collisionCheckBullets.IsCreated)
        {
            collisionCheckBullets = new NativeList<BulletData>(256, Allocator.Persistent);
        }
        if (!laserVertCellIndices.IsCreated)
        {
            laserVertCellIndices = new NativeList<int>(256, Allocator.Persistent);
        }

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

        for (int i = 0; i < bosses.Count; i++)
        {
            bosses[i].Init();
        }

    }

    private void OnDestroy()
    {
        quadBulletStore.Dispose();

        if (collisionVerts.IsCreated) collisionVerts.Dispose();
        if (collisionVertRanges.IsCreated) collisionVertRanges.Dispose();
        if (collisionHitFlag.IsCreated) collisionHitFlag.Dispose();
        if (collisionCheckBullets.IsCreated) collisionCheckBullets.Dispose();
        if (laserVertCellIndices.IsCreated) laserVertCellIndices.Dispose();
    }

    private void BuildCollisionData()
    {
        if (collisionVerts.IsCreated) collisionVerts.Dispose();
        if (collisionVertRanges.IsCreated) collisionVertRanges.Dispose();

        var bulletTypeDB = DBService.BTDB;
        if (bulletTypeDB == null || bulletTypeDB.types == null)
        {
            collisionVerts = new NativeArray<float2>(0, Allocator.Persistent);
            collisionVertRanges = new NativeArray<int2>(0, Allocator.Persistent);
            collisionDataDirty = false;
            return;
        }

        int typeCount = bulletTypeDB.types.Length;
        List<float2[]> vertsByType = bulletTypeDB.bVerts;

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

            offset += length;
        }

        collisionDataDirty = false;
    }

    public void MarkCollisionDataDirty()
    {
        collisionDataDirty = true;
    }

    private struct BulletComperer : IComparer<BulletData>
    {
        public int Compare(BulletData x, BulletData y) => x.areaNum.CompareTo(y.areaNum);
    }

    public void QuadUpdate(float _dt)
    {
        BulletUpdate(_dt);
        CheckCollisionWithEnemy(PController.pos);
        for (int i = 0; i < allLASERs.Count; i++)
        {
            if (allLASERs[i].UpdateSet(_dt))
            {
                allLASERs[i].Destroy();
                allLASERs.RemoveAt(i);
                i--;
            }
        }
        CheckCollisionWithLASER(PController.pos);

        UpdateEnemyPos(_dt);

        //UpdateChangeClip();

        for (int i = 0; i < bosses.Count; i++)
        {
            bosses[i].UpdateBoss(_dt);
        }
    }

    #region //BulletMethods
    public void BulletUpdate(float _dt)
    {
        bool hasEnemyBullets = enemyBullets.IsCreated && enemyBullets.Length > 0;
        bool hasPlayerBullets = playerBullets.IsCreated && playerBullets.Length > 0;
        bool hasEnemiesOrbitBullets = enemiesOrbitBullets.IsCreated && enemiesOrbitBullets.Length > 0;
        if (!hasEnemyBullets && !hasPlayerBullets && !hasEnemiesOrbitBullets)
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

        //プレーヤーの弾の更新
        if (hasPlayerBullets)
        {
            NativeArray<BulletData> playerBulletsArray = playerBullets;
            BulletDataUpdateJob job0 = new()
            {
                bullets = playerBulletsArray,
                dt = _dt,
                cellSize = cellSize,
                totalCellCount = cells.Length
            };
            JobHandle handle0 = job0.Schedule(playerBullets.Length, 64);
            handle0.Complete();
        }

        //敵の弾の更新
        if (hasEnemyBullets)
        {
            NativeArray<BulletData> bullets = enemyBullets;
            BulletDataUpdateJob job1 = new()
            {
                bullets = bullets,
                dt = _dt,
                cellSize = cellSize,
                totalCellCount = cells.Length
            };
            JobHandle handle1 = job1.Schedule(bullets.Length, 64);
            handle1.Complete();
        }

        if (hasEnemiesOrbitBullets)
        {
            NativeArray<BulletData> bullets = enemiesOrbitBullets;
            //Debug.Log($"Updating {bullets.Length} orbit bullets");
            BulletDataUpdateJob job2 = new()
            {
                bullets = bullets,
                dt = _dt,
                cellSize = cellSize,
                totalCellCount = cells.Length
            };
            JobHandle handle2 = job2.Schedule(bullets.Length, 64);
            handle2.Complete();
        }

        // areaNum を使ってセルを再構築
        RebuildCellsFromBullets();
        SyncNativeBulletDebugViews();
    }

    private void RemoveInactiveBullets()
    {
        if (!enemyBullets.IsCreated || enemyBullets.Length == 0) return;
    }

    private void ClearAllCells() => quadGrid.ClearAllCells();

    private void RebuildCellsFromBullets() => quadGrid.RebuildCellsFromBullets(playerBullets, enemyBullets, enemiesOrbitBullets, enemies);

    public List<int> AddEnemyHomingBullets(NativeArray<BulletData> newBullets, float2 fromPos)
    => quadBulletStore.AddEnemyHomingBullets(newBullets, fromPos, new float2(PController.pos.x, PController.pos.y));

    public List<int> AddEnemyBullets(NativeArray<BulletData> newBullets, float2 fromPos = new float2())
    => quadBulletStore.AddEnemyBullets(newBullets, fromPos);

    public void AddEnemyBullets(BulletSpawner spawner)
    {
        
        List<BulletData> bullets = GManager.Control.BClipManager.GetBulletClip(spawner.index, spawner.pos, spawner.originVlc, spawner.angle, out bool isLaser);
        

        if (isLaser)
        {
            allLASERs.AddRange(laserEmitter.EmitLASER(bullets, spawner.pos));
            return;
        }

        NativeArray<BulletData> newBullets = new (bullets.ToArray(), Allocator.Temp);
        quadBulletStore.AddEnemyBulletsBySpawner(newBullets);
        newBullets.Dispose();
    }

    public List<int> EmitEnemyBullet(BulletClip clip, int EnemyIndex)
    {
        return EmitEnemyBullet(clip, enemiesOrbitBullets[EnemyIndex].position);
    }

    public List<int> EmitEnemyBullet(BulletClip clip, float2 pPos)
    {
        return quadBulletStore.EmitEnemyBullet(clip, pPos, PController.pos);
    }

    public void StartBulletEvent(BulletEvent bulletEvent)
    {
        if (bulletEvent == null) return;

        if (bulletEvent.Evoke()) return;
        else bulletEvents.Add(bulletEvent);
    }

    public BulletData GetEnemyBulletData(int index) => quadBulletStore.GetEnemyBulletData(index);

    public void AddPlayerBullets(NativeArray<BulletData> newBullets) => quadBulletStore.AddPlayerBullets(newBullets);

    public NativeArray<BulletData> GetPlayerBullets() => quadBulletStore.GetPlayerBullets();
    public NativeArray<BulletData> GetEnemyBullets() => quadBulletStore.GetEnemyBullets();

    public int GetPlayerBulletCount() => quadBulletStore.GetPlayerBulletCount();
    public int GetEnemyBulletCount() => quadBulletStore.GetEnemyBulletCount();
    public int GetEnemiesOrbitBulletCount() => quadBulletStore.GetEnemiesOrbitBulletCount();

    private int CountActiveBullets(NativeArray<BulletData> bullets)
    {
        if (!bullets.IsCreated || bullets.Length == 0) return 0;

        int count = 0;
        for (int i = 0; i < bullets.Length; i++)
        {
            if (bullets[i].isActive) count++;
        }

        return count;
    }

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
            enemiesOrbitBullets,
            ref debugEnemiesOrbitBulletSlotCount,
            ref debugEnemiesOrbitBulletActiveCount,
            debugEnemiesOrbitBulletEntries,
            forceRefresh
        );
    }

    private void SyncNativeBulletDebugView(
        NativeArray<BulletData> bullets,
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
                size = bullet.size,
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
    public void CheckCollisionWithEnemy(float2 pPos)
    {
        if (collisionDataDirty || !collisionVerts.IsCreated || !collisionVertRanges.IsCreated) BuildCollisionData();
        if (!collisionHitFlag.IsCreated) collisionHitFlag = new NativeArray<int>(1, Allocator.Persistent);
        if (!collisionCheckBullets.IsCreated) collisionCheckBullets = new NativeList<BulletData>(256, Allocator.Persistent);

        Vector2Int pCell = BitCompact32(GetTreeNum(pPos));
        collisionCheckCells.Clear();
        int bulletCount = 0;
        foreach (var cell in cellOffsets)
        {
            Vector2Int checkCell = pCell + cell;
            int treeNum = GetTreeNum(checkCell.x, checkCell.y);
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

        NativeArray<BulletData> checkBullets = collisionCheckBullets.AsArray();
        collisionHitFlag[0] = 0;

        BulletCollisionJob collisionJob = new()
        {
            bullets = checkBullets,
            bVerts = collisionVerts,
            bVertRanges = collisionVertRanges,
            pPos = pPos,
            isCollided = collisionHitFlag
        };

        JobHandle handle = collisionJob.Schedule(checkBullets.Length, 64);
        handle.Complete();
    }

    public void UpdateLASERVerts(NativeList<float2> vertsSet, ref List<List<int>> quadVerts)
    {
        foreach (List<int> list in quadVerts) list.Clear();

        if (!vertsSet.IsCreated || vertsSet.Length == 0) return;

        if (!laserVertCellIndices.IsCreated)
        {
            laserVertCellIndices = new NativeList<int>(math.max(vertsSet.Length, 1), Allocator.Persistent);
        }
        if (laserVertCellIndices.Capacity < vertsSet.Length)
        {
            laserVertCellIndices.Capacity = vertsSet.Length;
        }
        laserVertCellIndices.ResizeUninitialized(vertsSet.Length);

        LASERQuadJob job = new LASERQuadJob()
        {
            vertsSet = vertsSet.AsArray(),
            vertCellIndices = laserVertCellIndices.AsArray(),
            cellSize = cellSize,
            cellCount = cells.Length
        };

        JobHandle handle = job.Schedule(vertsSet.Length, 64);
        handle.Complete();

        for (int i = 0; i < laserVertCellIndices.Length; i++)
        {
            int n = laserVertCellIndices[i];
            if (n >= 0 && n < quadVerts.Count) quadVerts[n].Add(i);
        }
    }

    public void CheckCollisionWithLASER(float2 pPos)
    {
        int pCell = GetTreeNum(pPos);
        if (pCell == -1) return;

        for (int i = 0; i < allLASERs.Count; i++)
        {
            LASER laser = allLASERs[i];
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
                break;
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (!drawLaserCollisionGizmos) return;
        if (!Application.isPlaying) return;
        if (allLASERs == null || allLASERs.Count == 0) return;
        DrawLaserCollisionGizmos();
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

    #region //EnemyMethods
    public async void AddEnemy(IEnemySpawner spawner)
    {
        float t = 0;

        for (int i = 0; i < spawner.count; i++)
        {
            while (t < spawner.interval * i)
            {
                await Task.Yield();
                t += Time.deltaTime;
                if (GManager.Control.state != GameState.Playing) return;
            }

            Enemy enemy = Instantiate(GManager.Control.EnemyObj).GetComponent<Enemy>();
            enemy.Init(enemies.Count, spawner,DBService.EDB);
            enemies.Add(enemy);
            //Debug.Log($"Spawned enemy: {spawner.orbit.speed}");
            quadBulletStore.AddEnemiesOrbitBullet(spawner.orbit);
        }
    }

    private void UpdateEnemyPos(float dt)
    {
        for (int i = 0; i < enemies.Count; i++)
        {
            enemies[i].trans.position = new Vector3(enemiesOrbitBullets[i].position.x, enemiesOrbitBullets[i].position.y, 0);
            enemies[i].trans.rotation = Quaternion.Euler(0, 0, enemiesOrbitBullets[i].angle * Mathf.Rad2Deg);
            enemies[i].UpdateEnemy(dt);
        }
    }
    #endregion

    #region //GenerateMethods
    public List<int> UpdateBulletData(List<int> indexes, BulletClip clip)
    {
        return quadBulletStore.UpdateBulletData(indexes, clip, new float2(PController.pos.x, PController.pos.y));
    }

    #endregion

    #region//CellsMethods
    public int GetTreeNum(float2 pos) => quadGrid.GetTreeNum(pos);

    public int BitSeparate32(int n) => quadGrid.BitSeparate32(n);

    public int GetTreeNum(int x, int y) => quadGrid.GetTreeNum(x, y);
    public Vector2Int BitCompact32(int n) => quadGrid.BitCompact32(n);

    #endregion
}
}
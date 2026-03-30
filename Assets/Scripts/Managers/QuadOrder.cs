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
public class QuadOrder : MonoBehaviour
{
    #region//CellManagers
    [NonSerialized] private QuadCell[] cells = Array.Empty<QuadCell>();
    [SerializeField] private float cellSize;
    [SerializeField] private int separateLevel;
    [SerializeField] public int cellCount;
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
        public List<Enemy> enemies = new List<Enemy>();
        public List<BulletData> enemyBullets = new List<BulletData>();
        public List<BulletData> playerBullets = new List<BulletData>();
        public void AddEnemy(Enemy enemy) => enemies.Add(enemy);
        public void ClearEnemies() => enemies.Clear();

        public void ClearAllBullets()
        {
            enemyBullets.Clear();
            playerBullets.Clear();
        }

        public void AddEnemyBullet(BulletData bullet) => enemyBullets.Add(bullet);
        public void ClearEnemyBullets() => enemyBullets.Clear();
        public int GetEnemyBulletCount() => enemyBullets.Count;
        public List<BulletData> GetEnemyBullets() => enemyBullets;
        public void AddPlayerBullet(BulletData bullet) => playerBullets.Add(bullet);
        public void ClearPlayerBullets() => playerBullets.Clear();
        public int GetPlayerBulletCount() => playerBullets.Count;
        public List<BulletData> GetPlayerBullets() => playerBullets;
    }
    #endregion

    #region //Arrays
    private NativeList<BulletData> playerBullets;
    private NativeList<BulletData> enemyBullets;
    [SerializeField]
    private List<Enemy> enemies = new List<Enemy>();
    private NativeList<BulletData> enemiesOrbitBullets; 
    private NativeArray<float2> collisionVerts;
    private NativeArray<int2> collisionVertRanges;
    private NativeArray<int> collisionHitFlag;
    private NativeList<BulletData> collisionCheckBullets;
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

    [Serializable]
    private class NativeBulletDebugEntry
    {
        public int index;
        public bool isActive;
        public int areaNum;
        public int typeId;
        public float time;
        public float angle;
        public float size;
        public Vector2 position;
        public Vector2 velocity;
        public Vector2 originPos;
        public Vector2 originVlc;
        public Vector2 polarForm;
    }

    private class LASERSet
    {
        public LASER laser;
        public List<List<int>> vertIndixes;

        public LASERSet(LASER laser, int cellCount)
        {
            this.laser = laser;
            this.vertIndixes = new List<List<int>>(cellCount);
            for (int i = 0; i < cellCount; i++) vertIndixes.Add(new List<int>());
        }
    }
    public List<LASER> allLASERs = new();

    public List<List<int>> laserVertsIndex = new List<List<int>>();

    public void AwakeSetting()
    {
        int n = 1;
        for (int i = 0; i < separateLevel; i++) n *= 2;
        n = n * n;
        cellCount = n;
        QuadCell[] t = new QuadCell[n];
        for (int i = 0; i < n; i++) t[i] = new();
        cells = t;

        GManager.Control.Log("CellCreated");
        GManager.Control.Log($"Level : {separateLevel}, Length : {n}");

        BuildCollisionData();
        if (!collisionHitFlag.IsCreated)
        {
            collisionHitFlag = new NativeArray<int>(1, Allocator.Persistent);
        }
        if (!collisionCheckBullets.IsCreated)
        {
            collisionCheckBullets = new NativeList<BulletData>(256, Allocator.Persistent);
        }
        if (!enemyBullets.IsCreated)
        {
            enemyBullets = new NativeList<BulletData>(256, Allocator.Persistent);
        }
        if (!playerBullets.IsCreated)
        {
            playerBullets = new NativeList<BulletData>(256, Allocator.Persistent);
        }
        if (!enemiesOrbitBullets.IsCreated)
        {
            enemiesOrbitBullets = new NativeList<BulletData>(256, Allocator.Persistent);
        }

        List<BulletClip> clips = new List<BulletClip>() {
                new BulletClip()
                {
                    data = new BulletData(new(0, 0), new(0, 0), 8, 0, 0, 0, new(1, 0), 0, 3, 0, new(0, -2, 1, 0), 0, 10, new float4(255, 255, 255, 255)),
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


    }

    private void OnDestroy()
    {
        if (playerBullets.IsCreated) playerBullets.Dispose();
        if (enemyBullets.IsCreated) enemyBullets.Dispose();
        if (enemiesOrbitBullets.IsCreated) enemiesOrbitBullets.Dispose();
        if (collisionVerts.IsCreated) collisionVerts.Dispose();
        if (collisionVertRanges.IsCreated) collisionVertRanges.Dispose();
        if (collisionHitFlag.IsCreated) collisionHitFlag.Dispose();
        if (collisionCheckBullets.IsCreated) collisionCheckBullets.Dispose();
    }

    private void BuildCollisionData()
    {
        if (collisionVerts.IsCreated) collisionVerts.Dispose();
        if (collisionVertRanges.IsCreated) collisionVertRanges.Dispose();

        var bulletTypeDB = GManager.Control.BTDB;
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
        CheckCollisionWithEnemy(GManager.Control.PController.pos);
        foreach (var laser in allLASERs) laser.UpdateSet(_dt, GManager.Control.PController.pos, out bool hit);
        CheckCollisionWithLASER(GManager.Control.PController.pos);
        UpdateEnemyPos(_dt);
        //UpdateChangeClip();
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

        //プレーヤーの弾の更新
        if (hasPlayerBullets)
        {
            NativeArray<BulletData> playerBulletsArray = playerBullets.AsArray();
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
            NativeArray<BulletData> bullets = enemyBullets.AsArray();
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

        if(hasEnemiesOrbitBullets)
        {
            NativeArray<BulletData> bullets = enemiesOrbitBullets.AsArray();
            Debug.Log($"Updating {bullets.Length} orbit bullets");
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

    private void ClearAllCells()
    {
        for (int i = 0; i < cells.Length; i++)
        {
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
                if (!bullet.isActive) continue;
                int areaNum = bullet.areaNum;
                if (areaNum < 0 || areaNum >= cells.Length) continue;
                cells[areaNum].enemyBullets.Add(bullet);
            }
        }

        if (playerBullets.IsCreated)
        {
            for (int i = 0; i < playerBullets.Length; i++)
            {
                BulletData bullet = playerBullets[i];
                if (!bullet.isActive) continue;
                int areaNum = bullet.areaNum;
                if (areaNum < 0 || areaNum >= cells.Length) continue;
                cells[areaNum].playerBullets.Add(bullet);
            }
        }

        if (enemiesOrbitBullets.IsCreated)
        {
            for (int i = 0; i < enemiesOrbitBullets.Length; i++)
            {
                BulletData bullet = enemiesOrbitBullets[i];
                if (!bullet.isActive) continue;
                int areaNum = bullet.areaNum;
                if (areaNum < 0 || areaNum >= cells.Length) continue;
                cells[areaNum].enemies.Add(enemies[i]);
            }
        }
    }

    public List<int> AddEnemyBullets(NativeArray<BulletData> newBullets)
    {
        if (newBullets.Length == 0) return null;

        if (!enemyBullets.IsCreated)
        {
            enemyBullets = new NativeList<BulletData>(math.max(256, newBullets.Length), Allocator.Persistent);
        }

        int oldLength = enemyBullets.Length;
        int newLength = oldLength + newBullets.Length;

        if (enemyBullets.Capacity < newLength)
        {
            int nextCapacity = math.max(newLength, math.max(256, enemyBullets.Capacity * 2));
            enemyBullets.Capacity = nextCapacity;
        }

        enemyBullets.ResizeUninitialized(newLength);
        List<int> indexes = new List<int>(newBullets.Length);

        // 新しい弾をコピー
        for (int i = 0; i < newBullets.Length; i++)
        {
            enemyBullets[oldLength + i] = newBullets[i];
            indexes.Add(oldLength + i);
        }
        return indexes;
    }

    public List<int> EmitEnemyBullet(BulletClip clip, int EnemyIndex)
    {
        return EmitEnemyBullet(clip, enemiesOrbitBullets[EnemyIndex].position);
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
                float angle = math.radians(range * i);
                bullet.polarForm = new float2(bullet.polarForm.x, angle);
                newBullets[i] = bullet;
            }
        }

        List<int> indexes = AddEnemyBullets(newBullets);
        newBullets.Dispose();
        return indexes;
    }

    public NativeArray<BulletData> GetEnemyBullets() => enemyBullets.IsCreated ? enemyBullets.AsArray() : default;
    
    public int GetEnemyBulletCount() => CountActiveBullets(enemyBullets);

    public void AddPlayerBullets(NativeArray<BulletData> newBullets)
    {
        if (newBullets.Length == 0) return;

        if (!playerBullets.IsCreated)
        {
            playerBullets = new NativeList<BulletData>(math.max(256, newBullets.Length), Allocator.Persistent);
        }

        int oldLength = playerBullets.Length;
        int newLength = oldLength + newBullets.Length;

        if (playerBullets.Capacity < newLength)
        {
            int nextCapacity = math.max(newLength, math.max(256, playerBullets.Capacity * 2));
            playerBullets.Capacity = nextCapacity;
        }

        playerBullets.ResizeUninitialized(newLength);

        // 新しい弾をコピー
        for (int i = 0; i < newBullets.Length; i++)
        {
            playerBullets[oldLength + i] = newBullets[i];
        }
    }

    public NativeArray<BulletData> GetPlayerBullets() => playerBullets.IsCreated ? playerBullets.AsArray() : default;
    
    public int GetPlayerBulletCount() => CountActiveBullets(playerBullets);

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

        NativeArray<int> vertCellIndices = new NativeArray<int>(vertsSet.Length, Allocator.TempJob);
        LASERQuadJob job = new LASERQuadJob()
        {
            vertsSet = vertsSet.AsArray(),
            vertCellIndices = vertCellIndices,
            cellSize = cellSize,
            cellCount = cells.Length
        };

        JobHandle handle = job.Schedule(vertsSet.Length, 64);
        handle.Complete();

        for (int i = 0; i < vertCellIndices.Length; i++)
        {
            int n = vertCellIndices[i];
            if (n >= 0 && n < quadVerts.Count) quadVerts[n].Add(i);
        }

        vertCellIndices.Dispose();
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
    public async void AddEnemy(EnemySpawner spawner)
    {
        float t = 0;

        for(int i = 0; i < spawner.count; i++)
        {
            while(t < spawner.interval * i)
            {
                await Task.Yield();
                t += Time.deltaTime;
                if(GManager.Control.state != GManager.GameState.Playing) return;
            }

            Enemy enemy = Instantiate(GManager.Control.EnemyObj).GetComponent<Enemy>();
            enemy.Init(enemies.Count, spawner);
            enemies.Add(enemy);
            Debug.Log($"Spawned enemy: {spawner.orbit.speed}");
            enemiesOrbitBullets.Add(spawner.orbit);
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
    private List<BulletChangeClip> bulletChangeClips = new();

    public void AddBulletChangeClip(BulletChangeClip clip)
    {
        clip.time += GManager.Control.gameTime;
        bulletChangeClips.Add(clip);
    }

    public List<int> UpdateBulletData(List<int> indexes, BulletClip clip)
    {
        if(indexes == null || indexes.Count == 0) return new();
        
        if(indexes.Count == 1 && clip.number == 1)
        {
            if(indexes[0] >= 0 && indexes[0] < enemyBullets.Length)
            {
                BulletData data = new BulletData(clip.data, enemyBullets[indexes[0]].position);
                enemyBullets[indexes[0]] = data;
                return new List<int>() { indexes[0] };
            }
        }
        else
        {
            List<int> temp = new();
            for(int i = 0; i < indexes.Count; i++)
            {
                int index = indexes[i];
                if(index < 0 || index >= enemyBullets.Length) continue;
                temp.Add(EmitEnemyBullet(clip, enemyBullets[index].position)[0]);
            }
            return temp;

        }
        return new();
    }
    
    #endregion

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

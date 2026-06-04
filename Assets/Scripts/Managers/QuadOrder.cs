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
        public List<Enemy> enemies = new List<Enemy>();
        public List<BulletData> enemyBullets = new List<BulletData>();
        public void AddEnemy(Enemy enemy) => enemies.Add(enemy);
        public void ClearEnemies() => enemies.Clear();

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
    private List<Enemy> enemies = new List<Enemy>();
    private NativeList<BulletData> enemiesOrbitBullets;
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
        public Vector2 scale;
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
        if (!enemiesOrbitBullets.IsCreated)
        {
            enemiesOrbitBullets = new NativeList<BulletData>(256, Allocator.Persistent);
        }
        if (!dashCollisionActiveFlags.IsCreated)
        {
            dashCollisionActiveFlags = new NativeList<byte>(256, Allocator.Persistent);
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
        if (boss != null) boss.Init();
    }

    private void OnDestroy()
    {
        if (enemyBullets.IsCreated) enemyBullets.Dispose();
        if (counterBullets.IsCreated) counterBullets.Dispose();
        if (enemiesOrbitBullets.IsCreated) enemiesOrbitBullets.Dispose();
        if (collisionVerts.IsCreated) collisionVerts.Dispose();
        if (collisionVertRanges.IsCreated) collisionVertRanges.Dispose();
        if (bulletPowers.IsCreated) bulletPowers.Dispose();
        if (collisionHitFlag.IsCreated) collisionHitFlag.Dispose();
        if (grazePower.IsCreated) grazePower.Dispose();
        if (collisionCheckBullets.IsCreated) collisionCheckBullets.Dispose();
        if (laserVertCellIndices.IsCreated) laserVertCellIndices.Dispose();
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

        UpdateEnemyPos(_dt);

        //UpdateChangeClip();
        if (boss != null) boss.UpdateBoss(_dt);
        UpdateCounterBullets(_dt);
    }

    #region //BulletMethods
    public void BulletUpdate(float _dt)
    {
        bool hasEnemyBullets = enemyBullets.IsCreated && enemyBullets.Length > 0;
        bool hasEnemiesOrbitBullets = enemiesOrbitBullets.IsCreated && enemiesOrbitBullets.Length > 0;
        if (!hasEnemyBullets && !hasEnemiesOrbitBullets)
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

        if (hasEnemiesOrbitBullets)
        {
            NativeArray<BulletData> bullets = enemiesOrbitBullets.AsArray();
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
            cells[i].ClearEnemies();
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

    private void RegisterBulletToCollisionCells(BulletData bullet)
    {
        if (gridResolution <= 0 || cellSize <= 0f)
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

        int minX = Mathf.FloorToInt((bullet.position.x - radius) / cellSize);
        int maxX = Mathf.FloorToInt((bullet.position.x + radius) / cellSize);
        int minY = Mathf.FloorToInt((bullet.position.y - radius) / cellSize);
        int maxY = Mathf.FloorToInt((bullet.position.y + radius) / cellSize);

        if (maxX < 0 || maxY < 0 || minX >= gridResolution || minY >= gridResolution) return;

        minX = Mathf.Clamp(minX, 0, gridResolution - 1);
        maxX = Mathf.Clamp(maxX, 0, gridResolution - 1);
        minY = Mathf.Clamp(minY, 0, gridResolution - 1);
        maxY = Mathf.Clamp(maxY, 0, gridResolution - 1);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                int areaNum = GetTreeNum(x, y);
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

    public List<int> AddEnemyBullets(NativeArray<BulletData> newBullets, float2 fromPos = new float2())
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

    public void AddEnemyBullets(int index, float2 pos, float2 originVlc, float angle, float4 color)
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
        int oldLength = enemyBullets.Length;
        int newLength = oldLength + newBullets.Length;
        enemyBullets.ResizeUninitialized(newLength);
        for (int i = 0; i < newBullets.Length; i++) enemyBullets[oldLength + i] = newBullets[i];
        newBullets.Dispose();
    }

    public List<int> EmitEnemyBullet(BulletClip clip, int EnemyIndex)
    {
        BulletData data = enemiesOrbitBullets[EnemyIndex];
        float2 vlc = data.velocity;
        float angle = math.atan2(vlc.y, vlc.x);
        return EmitEnemyBullet(clip, enemiesOrbitBullets[EnemyIndex].position, angle);
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
            homingElapsed = 0f,
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

        Debug.Log($"{source} collision detected.");
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

        if (grazePower[0] > 0f)
        {
            Debug.Log($"Graze detected. Graze power: {grazePower[0]}");
        }
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

        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            Enemy enemy = enemies[i];
            if (enemy == null) continue;
            enemy.isActive = false;
            UnityEngine.Object.Destroy(enemy.gameObject);
        }
        enemies.Clear();

        if (enemiesOrbitBullets.IsCreated) enemiesOrbitBullets.Clear();
        if (collisionCheckBullets.IsCreated) collisionCheckBullets.Clear();

        ClearAllCells();

        SyncNativeBulletDebugViews(forceRefresh: true);
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
        int spawnGeneration = enemySpawnGeneration;

        for (int i = 0; i < spawner.count; i++)
        {
            while (t < spawner.enemyInterval * i)
            {
                await Task.Yield();
                t += Time.deltaTime;
                if (spawnGeneration != enemySpawnGeneration) return;
                if (GManager.Control.state != GManager.GameState.Playing) return;
            }

            if (spawnGeneration != enemySpawnGeneration) return;

            Enemy enemy = Instantiate(GManager.Control.EnemyObj).GetComponent<Enemy>();
            enemy.Init(enemies.Count, spawner);
            enemies.Add(enemy);
            //Debug.Log($"Spawned enemy: {spawner.orbit.speed}");
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
    public List<int> UpdateBulletData(List<int> indexes, BulletClip clip)
    {
        if (indexes == null || indexes.Count == 0) return new();

        if (indexes.Count == 1 && clip.number == 1)
        {
            if (indexes[0] >= 0 && indexes[0] < enemyBullets.Length)
            {
                BulletData data = new BulletData(clip.data, new float2(0, 0), enemyBullets[indexes[0]].position, 0);
                enemyBullets[indexes[0]] = data;
                return new List<int>() { indexes[0] };
            }
        }
        else
        {
            List<int> temp = new();
            for (int i = 0; i < indexes.Count; i++)
            {
                int index = indexes[i];
                if (index < 0 || index >= enemyBullets.Length) continue;
                float angle = enemyBullets[index].angle;
                temp.Add(EmitEnemyBullet(clip, enemyBullets[index].position, angle)[0]);
                BulletData data = enemyBullets[index];
                data.isActive = false;
                enemyBullets[index] = data;
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

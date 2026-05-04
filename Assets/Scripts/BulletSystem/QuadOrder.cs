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
using BulletHell.Core.Math;

namespace BulletHell.Bullets
{

[Serializable]
public class QuadOrder : MonoBehaviour, IQuadOrderDirty, IQuadOrder
{
    private IDBService DBService;
    private IPlayerController PController;
    private IQuadGrid quadGrid;
    private IQuadBulletStore quadBulletStore;
    private IGameStateService state; //修正対象
    private IBulletPaternProvider BClipManager;
    private BulletUpdateService bulletUpdateService = new BulletUpdateService();
    private BulletCollisionService bulletCollisionService;
    private LaserCollisionService laserCollisionService;

    #region//CellManagers
    private QuadCell[] cells => quadGrid.cells;
    private float cellSize => quadGrid.cellSize;
    #endregion

    #region //Arrays
    [SerializeField] private List<Boss> bosses = new List<Boss>();

    private NativeArray<BulletData> playerBullets => quadBulletStore.playerBullets;
    private NativeArray<BulletData> enemyBullets => quadBulletStore.enemyBullets;
    private NativeArray<BulletData> enemiesOrbitBullets => quadBulletStore.enemiesOrbitBullets
    ;
    [SerializeField]
    private List<IEnemy> enemies = new();

    [SerializeField] private List<BulletEvent> bulletEvents = new List<BulletEvent>();
    #endregion

    [SerializeField] private int inactiveCleanupInterval = 8;
    private int inactiveCleanupCounter = 0;
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

    public List<List<int>> laserVertsIndex = new List<List<int>>();

    public void Init
    (
        IDBService dbService,
        IPlayerController playerController,
        IQuadGrid quadGrid,
        IQuadBulletStore quadBulletStore,
        LaserEmitter laserEmitter,
        IBulletPaternProvider bulletPaternProvider,
        BulletCollisionService bulletCollisionService,
        LaserCollisionService laserCollisionService,
        IGameStateService state
    )
    {
        DBService = dbService;
        PController = playerController;
        this.quadGrid = quadGrid;
        this.quadBulletStore = quadBulletStore;
        this.laserEmitter = laserEmitter;
        this.BClipManager = bulletPaternProvider;
        this.bulletCollisionService = bulletCollisionService;
        this.laserCollisionService = laserCollisionService;
        this.state = state;
    }

    public void AwakeSetting()
    {

        bulletCollisionService.Init();
        bulletCollisionService.BuildCollisionData();

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

        laserCollisionService.Init(clips);

        for (int i = 0; i < bosses.Count; i++)
        {
            bosses[i].Init(this, BClipManager, new PerlinRandom());
        }

    }

    private void OnDestroy()
    {
        quadBulletStore.Dispose();

        bulletCollisionService.Dispose();
        laserCollisionService.Dispose();
    }

    public void MarkCollisionDataDirty() => bulletCollisionService.MarkCollisionDataDirty();

    private struct BulletComperer : IComparer<BulletData>
    {
        public int Compare(BulletData x, BulletData y) => x.areaNum.CompareTo(y.areaNum);
    }

    public void QuadUpdate(float _dt)
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

        bulletUpdateService.UpdateBullets(_dt, playerBullets, enemyBullets, enemiesOrbitBullets, cells.Length, cellSize);

        RebuildCellsFromBullets();
        SyncNativeBulletDebugViews();

        bulletCollisionService.CheckCollisionWithEnemy(PController.pos);
        laserCollisionService.CheckCollisionWithLASER(_dt, PController.pos);

        //UpdateChangeClip();

        for (int i = 0; i < bosses.Count; i++)
        {
            bosses[i].UpdateBoss(_dt);
        }
    }

    #region //BulletMethods

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
        
        List<BulletData> bullets = BClipManager.GetBulletClip(spawner.index, spawner.pos, spawner.originVlc, spawner.angle, out bool isLaser);
        

        if (isLaser)
        {
            laserCollisionService.LaserAddRange(bullets, spawner.pos);
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
        bulletUpdateService.StartBulletEvent(bulletEvent);
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

    #endregion

    #region //EnemyMethods
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

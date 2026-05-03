using System;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.InputSystem;

public class GManager : MonoBehaviour, IGManagerQuad, IGameStateService,IGameStarter
{
    static public GManager Control;

    public IDBService DBService { get; private set; }

    public GameState state { get; set; } = GameState.Title;

    public GameObject PlayerObj;
    public GameObject EnemyObj;
    public PlayerController PController;

    public InputService IManager;
    public StageReader SReader;

    public BeatManager BManager;
    public PerlinRandom PRandom;
    public StageSelectManager SSManager;
    public BulletBufferManager BClipManager;
    public QuadOrder QOrder { get; set; }

    public IQuadOrderDirty QOrderDirty => QOrder;

    

    public BulletRenderSystem BRS;

    public float gameTime;
    public float beatTime;
    public bool ready = false;

    public bool musicOn = false;

    private readonly BulletData[] spawnBuffer = new BulletData[6];

    public BulletEvent testEvent = new BulletEvent();
    public void Construct(IDBService dbService, PlayerController playerController, StageReader stageReader, StageSelectManager stageSelectManager,QuadOrder quadOrder)
    {
        DBService = dbService;
        PController = playerController;
        SReader = stageReader;
        SSManager = stageSelectManager;
        QOrder = quadOrder;

        BRS = GetComponent<BulletRenderSystem>();
        BRS.Init(DBService.BTDB);

        ready = true;
    }

    public void Awake()
    {
        if (Control == null) Control = this;
        else
        {
            Destroy(this.gameObject);
            return;
        }

        IManager = new();
        IManager.Init();

        BManager = transform.parent.Find("BManager").GetComponent<BeatManager>();

        BClipManager = new();
        BClipManager.Init();

        PRandom = new PerlinRandom();

        InitSpawnBuffer();
        state = GameState.Title;
    }

    private void InitSpawnBuffer()
    {
        spawnBuffer[0] = new BulletData(new float2(5, 5), new float2(1, 0), 2f, 0, 0, 0, new float2(1, 0), 0, 2, 0, new float4(1, 0, 0, 0), 0, new float4(1, 0, 0, 1));
        spawnBuffer[1] = new BulletData(new float2(5, 5), new float2(1, 0), 2f, 0, 0, 0, new float2(1, math.PI / 3), 0, 2, 0, new float4(1, 0, 0, 0), 0, new float4(1, 0, 0, 1));
        spawnBuffer[2] = new BulletData(new float2(5, 5), new float2(1, 0), 2f, 0, 0, 0, new float2(1, 2 * math.PI / 3), 0, 2, 0, new float4(1, 0, 0, 0), 0, new float4(1, 0, 0, 1));
        spawnBuffer[3] = new BulletData(new float2(5, 5), new float2(1, 0), 2f, 0, 0, 0, new float2(1, 3 * math.PI / 3), 0, 2, 0, new float4(1, 0, 0, 0), 0, new float4(1, 0, 0, 1));
        spawnBuffer[4] = new BulletData(new float2(5, 5), new float2(1, 0), 2f, 0, 0, 0, new float2(1, 4 * math.PI / 3), 0, 2, 0, new float4(1, 0, 0, 0), 0, new float4(1, 0, 0, 1));
        spawnBuffer[5] = new BulletData(new float2(5, 5), new float2(1, 0), 2f, 0, 0, 0, new float2(1, 5 * math.PI / 3), 0, 2, 0, new float4(1, 0, 0, 0), 0, new float4(1, 0, 0, 1));
    }

    public void Update()
    {
        if (!ready) return;
        float t = Time.deltaTime;
        gameTime += t;
        QOrder.QuadUpdate(t);
        IManager.UpdateInput();
        if (musicOn)
        {
            BManager.UpdateBeat();
        }

        if (Keyboard.current != null && Keyboard.current.aKey.wasPressedThisFrame)
        {
            /*
            Debug.Log("aaa");
            NativeArray<BulletData> tempBullets = new NativeArray<BulletData>(
                spawnBuffer,
                Allocator.TempJob
            );
            List<int> indexes = QOrder.AddEnemyBullets(tempBullets);
            foreach (int index in indexes)
            {
                Debug.Log($"Spawned Bullet at index: {index}");
            }
            tempBullets.Dispose();
            */
            QOrder.StartBulletEvent(testEvent);
        }

        if (IManager.buttonPressed && state == GameState.Title)
        {
            state = GameState.ChoosingStage;
            // Transition to stage selection screen here
        }

        if (Keyboard.current != null && Keyboard.current.sKey.wasPressedThisFrame)
        {
            Debug.Log($"{beatTime}");
        }

        if (PController != null) PController.UpdatePos(t);
        SReader.UpdateStage(t);

        SSManager.UpdateSelect(IManager.upPressedThisFrame, IManager.downPressedThisFrame, t, IManager.buttonPressedThisFrame);
    }

    public void LateUpdate()
    {
        if (!ready) return;
        int enemyCount = QOrder.GetEnemyBulletCount();
        int playerCount = QOrder.GetPlayerBulletCount();
        //Debug.Log($"Enemy Bullet Count: {enemyCount}, Player Bullet Count: {playerCount}");

        if (enemyCount > 0 || playerCount > 0)
        {
            BRS.BuildRenderData(
                QOrder.GetEnemyBullets(),
                enemyCount,
                QOrder.GetPlayerBullets(),
                playerCount
            );
            BRS.Draw();
        }
    }


    public float GetAngleDeg(float x, float y)
    {
        double rad = Math.Atan2(y, x);
        double deg = rad * 180.0 / Math.PI;

        if (deg < 0) deg += 360.0;
        return (float)deg;
    }

    public async void GoGame(int index)
    {
        IStageData stage = DBService.SDB.GetStage(index);
        if (stage != null)
        {
            await SReader.Init(stage);
            state = GameState.Playing;
            Debug.Log($"Started Stage: {stage.stageName}");
        }
        else
        {
            Debug.LogError($"Stage with index {index} not found!");
        }
    }
}


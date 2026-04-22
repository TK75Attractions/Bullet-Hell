using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using JetBrains.Annotations;
using System.Linq;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine.InputSystem;

public class GManager : MonoBehaviour
{
    static public GManager Control;

    public enum GameState
    {
        Title,
        ChoosingStage,
        Playing,
        Result
    }

    public GameState state = GameState.Title;

    public GameObject PlayerObj;
    public GameObject EnemyObj;
    public PlayerController PController;

    public InputManager IManager;
    public StageReader SReader;
    public PerlinRandom PRandom;
    public StageSelectManager SSManager;
    public QuadOrder QOrder;
    public BulletTypeDataBase BTDB;
    public StageDataBase SDB;
    public EnemyDataBase EDB;
    public BulletRenderSystem BRS;

    public float gameTime;
    public float beatTime;
    public bool ready = false;


    private readonly BulletData[] spawnBuffer = new BulletData[6];

    public class PerlinRandom
    {
        private static readonly int[] permutation = {
        151,160,137,91,90,15,
        131,13,201,95,96,53,194,233,7,225,140,36,103,30,
        69,142,8,99,37,240,21,10,23,
        190, 6,148,247,120,234,75,0,26,197,62,94,252,219,
        203,117,35,11,32,57,177,33,88,237,149,56,87,174,
        20,125,136,171,168, 68,175,74,165,71,134,139,48,
        27,166,77,146,158,231,83,111,229,122,60,211,133,
        230,220,105,92,41,55,46,245,40,244,102,143,54,
        65,25,63,161, 1,216,80,73,209,76,132,187,208,
        89,18,169,200,196,135,130,116,188,159,86,164,
        100,109,198,173,186, 3,64,52,217,226,250,124,
        123,5,202,38,147,118,126,255,82,85,212,207,
        206,59,227,47,16,58,17,182,189,28,42,223,
        183,170,213,119,248,152, 2,44,154,163, 70,
        221,153,101,155,167, 43,172,9,129,22,39,
        253, 19,98,108,110,79,113,224,232,178,185,
        112,104,218,246,97,228,251,34,242,193,238,
        210,144,12,191,179,162,241, 81,51,145,235,
        249,14,239,107,49,192,214, 31,181,199,106,
        157,184, 84,204,176,115,121,50,45,127, 4,
        150,254,138,236,205,93,222,114, 67,29,24,
        72,243,141,128,195,78,66,215,61,156,180
        };

        private int[] p;

        public PerlinRandom()
        {
            p = new int[512];
            for (int i = 0; i < 256; i++)
            {
                p[i] = permutation[i];
                p[256 + i] = permutation[i];
            }

        }

        public double Noise(double x)
        {
            int xi = (int)Math.Floor(x) & 255;
            double xf = x - Math.Floor(x);

            double u = Fade(xf);

            int a = p[xi];
            int b = p[xi + 1];

            double gradA = Grad(a, xf);
            double gradB = Grad(b, xf - 1);

            return Lerp(u, gradA, gradB) * 0.5 + 0.5;
        }

        private double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);

        private double Lerp(double t, double a, double b) => a + t * (b - a);

        private double Grad(int hash, double x) => ((hash & 1) == 0) ? x : -x;


    }

    public void Awake()
    {
        if (Control == null) Control = this;
        else
        {
            Destroy(this.gameObject);
            return;
        }

        IManager = GetComponent<InputManager>();
        IManager.Init();

        BTDB.Init();
        SDB.Init();

        BRS = GetComponent<BulletRenderSystem>();
        BRS.Init();

        EDB.Init();

        SSManager = transform.parent.Find("Canvases").Find("StageCanvas").Find("StageBoxParent").GetComponent<StageSelectManager>();
        SSManager.Init();

        QOrder = GetComponent<QuadOrder>();
        QOrder.AwakeSetting();
        PRandom = new PerlinRandom();
        PController = new PlayerController();
        GameObject ptemp = Instantiate(PlayerObj);
        PController.Init(ptemp);

        SReader = GetComponent<StageReader>();

        InitSpawnBuffer();
        state = GameState.Title;

        ready = true;
    }

    private void InitSpawnBuffer()
    {
        spawnBuffer[0] = new BulletData(new float2(5, 5), new float2(1, 0), 2f, 0, 0, 0, new float2(1, 0), 0, 2, 0, new float4(1, 0, 0, 0), 0, 1f, new float4(1, 0, 0, 1));
        spawnBuffer[1] = new BulletData(new float2(5, 5), new float2(1, 0), 2f, 0, 0, 0, new float2(1, math.PI / 3), 0, 2, 0, new float4(1, 0, 0, 0), 0, 1f, new float4(1, 0, 0, 1));
        spawnBuffer[2] = new BulletData(new float2(5, 5), new float2(1, 0), 2f, 0, 0, 0, new float2(1, 2 * math.PI / 3), 0, 2, 0, new float4(1, 0, 0, 0), 0, 1f, new float4(1, 0, 0, 1));
        spawnBuffer[3] = new BulletData(new float2(5, 5), new float2(1, 0), 2f, 0, 0, 0, new float2(1, 3 * math.PI / 3), 0, 2, 0, new float4(1, 0, 0, 0), 0, 1f, new float4(1, 0, 0, 1));
        spawnBuffer[4] = new BulletData(new float2(5, 5), new float2(1, 0), 2f, 0, 0, 0, new float2(1, 4 * math.PI / 3), 0, 2, 0, new float4(1, 0, 0, 0), 0, 1f, new float4(1, 0, 0, 1));
        spawnBuffer[5] = new BulletData(new float2(5, 5), new float2(1, 0), 2f, 0, 0, 0, new float2(1, 5 * math.PI / 3), 0, 2, 0, new float4(1, 0, 0, 0), 0, 1f, new float4(1, 0, 0, 1));
    }

    public void Update()
    {
        if (!ready) return;
        float t = Time.deltaTime;
        gameTime += t;
        beatTime += t;
        QOrder.QuadUpdate(t);
        IManager.UpdateInput();

        if (Keyboard.current != null && Keyboard.current.aKey.wasPressedThisFrame)
        {
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
        }

        if (IManager.buttonPressed && state == GameState.Title)
        {
            state = GameState.ChoosingStage;
            // Transition to stage selection screen here
        }

        if (PController != null) PController.UpdatePos(t);
        SReader.UpdateStage(t);

        SSManager.UpdateSelect(IManager.upPressedThisFrame, IManager.downPressedThisFrame, t, IManager.buttonPressedThisFrame);
    }

    public void LateUpdate()
    {
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

    public void GoGame(int index)
    {
        StageData stage = SDB.GetStage(index);
        if (stage != null)
        {
            SReader.Init(stage);
            state = GameState.Playing;
            Debug.Log($"Started Stage: {stage.stageName}");
        }
        else
        {
            Debug.LogError($"Stage with index {index} not found!");
        }
    }
}


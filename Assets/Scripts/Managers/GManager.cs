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
    public bool isRaymeeDebug = false; // デバッグ用のフラグ。これが true のとき、特定のデバッグコードが有効になる。

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
    public AudioManager AManager;
    public BeatManager BManager;
    public StageSelectManager SSManager;
    public BulletBufferManager BClipManager;
    public QuadOrder QOrder;
    public BulletTypeDataBase BTDB;

    public StageDataBase SDB;
    public SEDataBase SEDB;
    public EnemyDataBase EDB;
    public BulletRenderSystem BRS;

    public float gameTime;
    public bool ready = false;

    public bool musicOn = false;
    public int playerHitCount = 0;
    public int counterHitBossCount = 0;

    public async void Awake()
    {
        if (Control == null) Control = this;
        else
        {
            Destroy(this.transform.parent.gameObject);
            return;
        }

        ready = false;

        IManager = GetComponent<InputManager>();
        IManager.Init();

        AManager = transform.parent.Find("AManager").GetComponent<AudioManager>();
        AManager.Init();

        BManager = transform.parent.Find("BManager").GetComponent<BeatManager>();

        BTDB.Init();
        SDB = new();
        await SDB.InitAsync();
        BClipManager = new();
        await BClipManager.InitAsync();

        BRS = GetComponent<BulletRenderSystem>();
        BRS.Init();

        EDB.Init();

        SSManager = transform.parent.Find("Canvases").Find("StageCanvas").Find("StageBoxParent").GetComponent<StageSelectManager>();
        SSManager.Init();

        QOrder = GetComponent<QuadOrder>();
        QOrder.AwakeSetting();
        PController = new PlayerController();
        GameObject ptemp = Instantiate(PlayerObj);
        PController.Init(ptemp);

        SReader = GetComponent<StageReader>();

        state = GameState.Title;

        ready = true;
    }



    public void Update()
    {
        if (!ready) return;
        float t = Time.deltaTime;
        gameTime += t;

        if (PController != null)
        {
            if (state == GameState.Playing) PController.UpdatePos(t);
        }

        SReader.UpdateStage(t);

        QOrder.QuadUpdate(t);
        IManager.UpdateInput();
        if (musicOn)
        {
            BManager.UpdateBeat();
        }

        if (IManager.buttonPressed && state == GameState.Title)
        {
            state = GameState.ChoosingStage;
            // Transition to stage selection screen here
        }

        SSManager.UpdateSelect(IManager.upPressedThisFrame, IManager.downPressedThisFrame, t, IManager.buttonPressedThisFrame);
    }

    public void LateUpdate()
    {
        if (!ready) return;

        int enemyCount = QOrder.GetEnemyBulletCount();
        int counterCount = QOrder.GetCounterBulletCount();
        //Debug.Log($"Enemy Bullet Count: {enemyCount}, Counter Bullet Count: {counterCount}");

        if (enemyCount > 0 || counterCount > 0)
        {
            BRS.BuildRenderData(
                QOrder.GetEnemyBullets(),
                enemyCount,
                QOrder.GetCounterBullets(),
                counterCount
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
        StageData stage = SDB.GetStage(index);
        if (stage != null)
        {
            playerHitCount = 0;
            counterHitBossCount = 0;
            await SReader.Init(stage);
            state = GameState.Playing;
            Debug.Log($"Started Stage: {stage.stageName}");
        }
        else
        {
            Debug.LogError($"Stage with index {index} not found!");
        }
    }

    public void AddPlayerHitCount(int value = 1)
    {
        if (value <= 0) return;
        playerHitCount += value;
    }

    public void AddCounterHitBossCount(int value = 1)
    {
        if (value <= 0) return;
        counterHitBossCount += value;
    }
}


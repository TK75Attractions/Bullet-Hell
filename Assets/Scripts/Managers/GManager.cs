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
using UnityEngine.Serialization;

public class GManager : MonoBehaviour
{
    static public GManager Control;
    public bool isRaymeeDebug = false; // デバッグ用のフラグ。これが true のとき、特定のデバッグコードが有効になる。

    public enum GameState
    {
        Title,
        ChoosingStage,
        Playing,
        Result,
        Tutorial
    }

    public GameState state = GameState.Title;

    public GameObject PlayerObj;
    [FormerlySerializedAs("EnemyObj")]
    public GameObject MultiBulletObj;
    public PlayerController PController;

    public InputManager IManager;
    public StageReader SReader;
    public AudioManager AManager;
    public BeatManager BManager;
    public CManager CManager;
    public StageSelectManager SSManager;
    public TitleManager TManager;
    public int selectedDifficulty = 1;
    public bool isPaused = false;
    private GameObject optionScreenObj;
    private OptionMenu optionMenu;
    private bool titleArmed = false;
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
        CManager = GetComponent<CManager>();
        if (CManager == null) CManager = FindObjectOfType<CManager>();
        if (CManager == null) CManager = gameObject.AddComponent<CManager>();

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

        Transform titleTrans = transform.parent.Find("Canvases").Find("StageCanvas").Find("Title");
        if (titleTrans != null)
        {
            TManager = titleTrans.GetComponent<TitleManager>();
            TManager?.Init();
        }

        Transform optionTrans = transform.parent.Find("Canvases").Find("StageCanvas").Find("OptionScreen");
        if (optionTrans != null)
        {
            optionScreenObj = optionTrans.gameObject;
            optionMenu = optionTrans.GetComponent<OptionMenu>();
        }

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

        // While paused, only watch for Esc to resume; gameplay updates are skipped
        // (timeScale is 0 and all audio is paused via AudioListener).
        if (isPaused)
        {
            IManager.UpdateInput();
            if (IManager.backPressedThisFrame)
            {
                // Esc closes the confirm popup first; otherwise it resumes.
                if (optionMenu == null || !optionMenu.HandleBack()) SetPaused(false);
            }
            else if (optionMenu != null)
            {
                optionMenu.UpdateMenu(Time.unscaledDeltaTime,
                    IManager.upPressedThisFrame, IManager.downPressedThisFrame,
                    IManager.leftPressedThisFrame, IManager.rightPressedThisFrame,
                    IManager.leftPressed, IManager.rightPressed,
                    IManager.buttonPressedThisFrame);
            }
            return;
        }

        float t = Time.deltaTime;
        gameTime += t;

        if (PController != null)
        {
            // The player can also move during the pre-stage tutorial.
            if (state == GameState.Playing || state == GameState.Tutorial) PController.UpdatePos(t);
        }

        SReader.UpdateStage(t);

        QOrder.QuadUpdate(t);
        IManager.UpdateInput();

        // Esc during gameplay opens the option (pause) screen.
        if (state == GameState.Playing && IManager.backPressedThisFrame)
        {
            SetPaused(true);
            return;
        }
        if (musicOn)
        {
            BManager.UpdateBeat();
        }

        bool stageSelectButton = IManager.buttonPressedThisFrame;

        if (state == GameState.Title)
        {
            TManager?.UpdateTitle(t);
            // Require the button to be released once before the title accepts a
            // press. Without this, a button still held from the previous screen
            // (e.g. the pause "quit" confirmation that reloaded into the title)
            // would instantly skip past the title into stage select.
            if (!titleArmed)
            {
                if (!IManager.buttonPressed) titleArmed = true;
            }
            else if (IManager.buttonPressed)
            {
                state = GameState.ChoosingStage;
                stageSelectButton = false;
                TManager?.Dismiss();
                SSManager.ResetTimer();
                SSManager.PlayEntrance();
            }
        }

        SSManager.UpdateSelect(IManager.upPressedThisFrame, IManager.downPressedThisFrame, t, stageSelectButton, IManager.backPressedThisFrame);
    }

    public void LateUpdate()
    {
        if (!ready) return;

        int enemyCount = QOrder.GetEnemyBulletCount();
        int warpZoneCount = QOrder.GetWarpZoneCount();
        int counterCount = QOrder.GetCounterBulletCount();
        //Debug.Log($"Enemy Bullet Count: {enemyCount}, Counter Bullet Count: {counterCount}");

        if (enemyCount > 0 || warpZoneCount > 0 || counterCount > 0)
        {
            BRS.BuildRenderData(
                QOrder.GetEnemyBullets(),
                enemyCount,
                QOrder.GetWarpZones(),
                warpZoneCount,
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
        await GoGameAsync(index);
    }

    public async Task GoGameAsync(int index)
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

    // Opens/closes the pause (option) screen. Freezes game time and all audio.
    public void SetPaused(bool pause)
    {
        isPaused = pause;
        Time.timeScale = pause ? 0f : 1f;
        AudioListener.pause = pause;
        if (optionScreenObj != null)
        {
            optionScreenObj.SetActive(pause);
            if (pause) optionMenu?.Open();
        }
    }

    // 「プレイを終了」(はい) : ends the session and reboots cleanly to the title
    // screen by reloading the scene.
    public void QuitPlay()
    {
        SetPaused(false);
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
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


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
    [Header("Debug Time Scale")]
    [SerializeField] private bool debugFastForwardTimeEnabled = true;
    [SerializeField, Min(1f)] private float debugFastForwardTimeScale = 16f;
    public bool isRaymeeDebug = false; // デバッグ用のフラグ。これが true のとき、特定のデバッグコードが有効になる。

    public enum GameState
    {
        Title,
        ChoosingStage,
        Loading,
        Playing,
        Result
    }

    public GameState state = GameState.Title;
    public GameObject PlayerObj;
    public PlayerController PController;

    public InputManager IManager;
    public StageReader SReader;
    public AudioManager AManager;
    public BeatManager BManager;
    public CManager CManager;
    public StageSelectManager SSManager;
    public BulletBufferManager BulletBuffers;
    public QuadOrder QOrder;
    public BulletTypeDataBase BTDB;

    public StageDataBase SDB;
    public SEDataBase SEDB;
    public EnemyDataBase EDB;
    public BulletRenderSystem BRS;

    public float gameTime { get; private set; } = 0f;
    public bool ready { get; private set; } = false;

    public bool musicOn = false;
    public int playerHitCount = 0;
    public int counterHitBossCount = 0;

    private float currentNoHitDuration;
    private float longestNoHitDuration;
    private float defaultFixedDeltaTime;
    private float appliedTimeScale = 1f;
    private readonly List<GameResultData> resultHistory = new List<GameResultData>();

    public GameResultData LastResult { get; private set; }
    public IReadOnlyList<GameResultData> ResultHistory => resultHistory;
    public event Action<GameResultData> ResultRecorded;
    public float CurrentNoHitDuration => currentNoHitDuration;
    public float LongestNoHitDuration => Mathf.Max(longestNoHitDuration, currentNoHitDuration);
    public int CurrentScore => GetCurrentScoreBreakdown().totalScore;

    public Difficulty CurrentDifficulty { get; private set; } = Difficulty.Easy;
    public DifficultySelection RequestedDifficultySelection { get; private set; } = DifficultySelection.FromOfficial(Difficulty.Easy);
    public DifficultySelection CurrentDifficultySelection { get; private set; } = DifficultySelection.FromOfficial(Difficulty.Easy);
    public DifficultySelection CurrentDataDifficultySelection { get; private set; } = DifficultySelection.FromOfficial(Difficulty.Easy);
    public int CurrentStageIndex { get; private set; } = -1;
    public StageData CurrentStageData { get; private set; }
    //ステージによって変化
    public Color playerColor = new(1, 1, 0.6f, 1);

    public async void Awake()
    {
        if (Control == null) Control = this;
        else
        {
            Destroy(this.transform.parent.gameObject);
            return;
        }

        ready = false;
        defaultFixedDeltaTime = Time.fixedDeltaTime;
        ApplyTimeScale(1f);

        IManager = GetComponent<InputManager>();
        IManager.Init();

        AManager = transform.parent.Find("AManager").GetComponent<AudioManager>();
        AManager.Init();

        BManager = transform.parent.Find("BManager").GetComponent<BeatManager>();
        CManager = GetComponent<CManager>();
        if (CManager == null) CManager = FindAnyObjectByType<CManager>();
        if (CManager == null) CManager = gameObject.AddComponent<CManager>();

        BTDB.Init();
        SDB = new();
        await SDB.InitAsync();
        BulletBuffers = new();
        await BulletBuffers.InitAsync();

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

        if (state == GameState.Playing)
        {
            currentNoHitDuration += t;
            PController?.UpdatePos(t);
            SReader.UpdateStage(t);
            QOrder.QuadUpdate(t);

            if (SReader.HasReachedEndTime)
            {
                FinishCurrentStage();
            }
        }
        IManager.UpdateInput();
        if (musicOn && state == GameState.Playing)
        {
            BManager.UpdateBeat();
        }

        bool stageSelectButton = IManager.buttonPressedThisFrame;

        if (IManager.buttonPressed && state == GameState.Title)
        {
            state = GameState.ChoosingStage;
            stageSelectButton = false;
        }

        SSManager.UpdateSelect(IManager.upPressedThisFrame, IManager.downPressedThisFrame, t, stageSelectButton, IManager.backPressedThisFrame);
        ApplyDebugTimeScale();
    }

    private void ApplyDebugTimeScale()
    {
        bool isFastForwarding = debugFastForwardTimeEnabled
            && IManager != null
            && IManager.isDebugMode
            && IManager.debugFastForwardPressed;

        ApplyTimeScale(isFastForwarding ? debugFastForwardTimeScale : 1f);
    }

    private void ApplyTimeScale(float timeScale)
    {
        if (defaultFixedDeltaTime <= 0f)
        {
            defaultFixedDeltaTime = Time.fixedDeltaTime;
        }

        timeScale = Mathf.Max(1f, timeScale);
        float targetFixedDeltaTime = defaultFixedDeltaTime * timeScale;
        if (Mathf.Approximately(appliedTimeScale, timeScale)
            && Mathf.Approximately(Time.timeScale, timeScale)
            && Mathf.Approximately(Time.fixedDeltaTime, targetFixedDeltaTime))
        {
            return;
        }

        Time.timeScale = timeScale;
        Time.fixedDeltaTime = targetFixedDeltaTime;
        appliedTimeScale = timeScale;
    }

    private void ResetDebugTimeScale()
    {
        ApplyTimeScale(1f);
    }

    private void OnDisable()
    {
        ResetDebugTimeScale();
    }

    private void OnDestroy()
    {
        ResetDebugTimeScale();
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

    public void GoGame(int index, Difficulty difficulty)
    {
        GoGame(index, DifficultySelection.FromOfficial(difficulty));
    }

    public async void GoGame(int index, DifficultySelection difficulty)
    {
        StageData stage = SDB.GetStage(index);
        if (stage == null)
        {
            Debug.LogError($"Stage with index {index} not found!");
            return;
        }

        StageData runtimeStage = stage.CreateRuntimeCopy(difficulty);
        if (runtimeStage.endTime <= 0f)
        {
            Debug.LogError($"Stage '{runtimeStage.stageName}' has an invalid endTime: {runtimeStage.endTime}");
            SSManager.ReturnToStageSelect();
            state = GameState.ChoosingStage;
            return;
        }

        state = GameState.Loading;

        CurrentStageIndex = index;
        CurrentStageData = runtimeStage;
        RequestedDifficultySelection = runtimeStage.requestedDifficulty;
        CurrentDifficultySelection = runtimeStage.activeDifficulty;
        CurrentDataDifficultySelection = runtimeStage.resolvedDataDifficulty;
        CurrentDifficulty = CurrentDifficultySelection.isOfficial
            ? CurrentDifficultySelection.officialDifficulty
            : Difficulty.Normal;

        playerHitCount = 0;
        counterHitBossCount = 0;
        currentNoHitDuration = 0f;
        longestNoHitDuration = 0f;
        PController?.ResetForStage();

        bool initialized = await SReader.Init(runtimeStage);
        if (!initialized)
        {
            SSManager.ReturnToStageSelect();
            state = GameState.ChoosingStage;
            return;
        }
        state = GameState.Playing;
        Debug.Log($"Started Stage: {runtimeStage.stageName}, Difficulty: {CurrentDifficultySelection.displayName} (Data: {CurrentDataDifficultySelection.displayName})");
    }

    public void AddPlayerHitCount(int value = 1)
    {
        if (value <= 0) return;
        longestNoHitDuration = Mathf.Max(longestNoHitDuration, currentNoHitDuration);
        currentNoHitDuration = 0f;
        playerHitCount += value;
    }

    public void AddCounterHitBossCount(int value = 1)
    {
        if (value <= 0) return;
        counterHitBossCount += value;
    }

    public GameScoreBreakdown GetCurrentScoreBreakdown()
    {
        bool bossDefeated = SReader != null && SReader.IsBossDefeated;
        return GameScoreCalculator.Calculate(counterHitBossCount, LongestNoHitDuration, bossDefeated);
    }

    private void FinishCurrentStage()
    {
        if (state != GameState.Playing || CurrentStageData == null) return;

        state = GameState.Result;
        longestNoHitDuration = Mathf.Max(longestNoHitDuration, currentNoHitDuration);

        GameResultData result = GameScoreCalculator.Create(
            CurrentStageIndex,
            CurrentStageData,
            CurrentDifficultySelection,
            SReader.ElapsedTime,
            counterHitBossCount,
            playerHitCount,
            longestNoHitDuration,
            SReader.CurrentBoss);

        LastResult = result;
        resultHistory.Add(result);

        QOrder.ClearAllGameplayBulletsImmediate();
        SReader.StopStage();
        BulletBuffers.UnloadAllBulletBuffers();
        AManager.StopBGM();
        BManager.StopBeat();
        CManager?.StopScreenNoise();
        musicOn = false;

        CurrentStageIndex = -1;
        CurrentStageData = null;
        SSManager.ReturnToStageSelect();
        state = GameState.ChoosingStage;

        NotifyResultUI(result);
        ResultRecorded?.Invoke(result);
        Debug.Log($"Stage finished. Clear={result.isClear}, Score={result.score.totalScore}");
    }

    private void NotifyResultUI(GameResultData result)
    {
        ResultUIManager[] resultUIs = FindObjectsByType<ResultUIManager>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < resultUIs.Length; i++)
        {
            if (resultUIs[i] == null) continue;
            resultUIs[i].ShowResult(result);
        }
    }
}


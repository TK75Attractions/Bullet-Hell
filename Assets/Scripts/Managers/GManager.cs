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
        Result,
        Tutorial
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
<<<<<<< HEAD
    public TitleManager TManager;
    // 0=Easy / 1=Normal / 2=Lunatic。現状データは Lunatic のみのため既定を Lunatic に。
    public int selectedDifficulty = 2;
    public bool isPaused = false;
    private GameObject optionScreenObj;
    private OptionMenu optionMenu;
    private bool titleArmed = false;

    // Which layer of the title screen currently owns input.
    // Starting はスタート決定後、タイトル退場演出がステージ選択に覆われる
    // までの待ち(入力は消費し、時間経過で ChoosingStage へ切り替える)。
    private enum TitlePhase { Menu, Options, Transfer, Starting }
    private TitlePhase titlePhase = TitlePhase.Menu;
    private float titleStartTimer;
    private int optionScreenSiblingIndex = -1;
    public BulletBufferManager BClipManager;
    // raymee ランタイム互換: raymee 系(StageReader/QuadOrder/BulletRenderSystem)は
    // バッファ管理を BulletBuffers、自機色を playerColor という名前で参照する。marron は
    // 同じ BulletBufferManager を BClipManager として保持しているためエイリアスで橋渡し。
    public BulletBufferManager BulletBuffers => BClipManager;
    public Color playerColor = new Color(1f, 1f, 0.6f, 1f);
=======
    public BulletBufferManager BulletBuffers;
>>>>>>> origin/main
    public QuadOrder QOrder;
    public BulletTypeDataBase BTDB;

    public StageDataBase SDB;
    public SEDataBase SEDB;
    public EnemyDataBase EDB;
    public BulletRenderSystem BRS;

<<<<<<< HEAD
    public float gameTime;
    // Play Mode 中にドメインリロード(スクリプト再コンパイル等)が走ると static の
    // Control や非シリアライズの実行時状態(BClipManager 等)は消えるが、この
    // インスタンス自体はシリアライズ経由で生き残る。ready が true のまま復元されると
    // Update が壊れた状態のまま毎フレーム走り、PlayerController.Move が
    // GManager.Control.IManager で NRE ループする。NonSerialized でリロード後は
    // 必ず false に戻し、Update/LateUpdate を安全に停止させる。
    [NonSerialized] public bool ready = false;
    private bool reloadDuringPlayWarned = false;
=======
    public float gameTime { get; private set; } = 0f;
    public bool ready { get; private set; } = false;
>>>>>>> origin/main

    public bool musicOn = false;
    public int playerHitCount = 0;
    public int counterHitBossCount = 0;

<<<<<<< HEAD
    // --- Stone stage M21 landing shake ------------------------------------
    // The stone stage's golem slams down at M21 (stage clock 63.333s). A short,
    // decaying camera shake sells the landing weight. Event-driven and gated on
    // the stone stage's clock, so no other stage ever shakes and the camera is
    // untouched until the exact crossing fires it once.
    private const string StoneStageName = "石工";
    private const float StoneLandingShakeTime = 63.333f;
    // The earlier 0.22u/0.16s shake measured ~8.8px peak for a single frame at
    // 720p (1.2% of the 18u view height) and settled inside ~0.12s — real, but
    // too small and brief to feel while dodging. A giant golem slam should land
    // as a clear thump, so amplitude/duration are raised to a felt-but-tasteful
    // impact: 0.6u ≈ 3.3% of view height (~24px @720p) decaying over 0.34s.
    private const float StoneLandingShakeAmplitude = 0.6f;
    private const float StoneLandingShakeDuration = 0.34f;
    private const float StoneLandingShakeFrequency = 18f;
    private float prevStageClock = -1f;
=======
    public Difficulty CurrentDifficulty { get; private set; } = Difficulty.Easy;
    public DifficultySelection RequestedDifficultySelection { get; private set; } = DifficultySelection.FromOfficial(Difficulty.Easy);
    public DifficultySelection CurrentDifficultySelection { get; private set; } = DifficultySelection.FromOfficial(Difficulty.Easy);
    public DifficultySelection CurrentDataDifficultySelection { get; private set; } = DifficultySelection.FromOfficial(Difficulty.Easy);
    public int CurrentStageIndex { get; private set; } = -1;
    public StageData CurrentStageData { get; private set; }
    //ステージによって変化
    public Color playerColor = new(1, 1, 0.6f, 1);
>>>>>>> origin/main

    public async void Awake()
    {
        try
        {
            LogStartup("Awake start");
            if (Control == null) Control = this;
            else
            {
                LogStartup("Duplicate manager destroyed");
                Destroy(this.transform.parent.gameObject);
                return;
            }

            ready = false;

            IManager = GetComponent<InputManager>();
            IManager.Init();
            LogStartup("Input initialized");

            AManager = transform.parent.Find("AManager").GetComponent<AudioManager>();
            AManager.Init();
            LogStartup("Audio initialized");

            BManager = transform.parent.Find("BManager").GetComponent<BeatManager>();
            CManager = GetComponent<CManager>();
            if (CManager == null) CManager = FindObjectOfType<CManager>();
            if (CManager == null) CManager = gameObject.AddComponent<CManager>();
            LogStartup("Core managers resolved");

            BTDB.Init();
            // raymee CounterBullet は struct で TypeId=18 の定数を持つため、marron の
            // 実行時 ResolveTypeId は不要(呼び出し削除)。ただし BulletTypeDataBase の並び順が
            // 変わると TypeId=18 が別の弾を指しカウンター弾が無言で壊れるので、起動時に検証する。
            if (BTDB.types == null || CounterBullet.TypeId >= BTDB.types.Length
                || BTDB.types[CounterBullet.TypeId] == null
                || BTDB.types[CounterBullet.TypeId].typeName != "counter_star")
            {
                Debug.LogError($"[GManager] CounterBullet.TypeId({CounterBullet.TypeId}) が 'counter_star' を指していません。" +
                               "BulletTypeDataBase の並び順を確認してください(18番より前への挿入は禁止)。");
            }
            LogStartup("Bullet types initialized");
            SDB = new();
            LogStartup("Stage database init start");
            await SDB.InitAsync();
            LogStartup("Stage database init done");
            BClipManager = new();
            LogStartup("Bullet buffers init start");
            await BClipManager.InitAsync();
            LogStartup("Bullet buffers init done");

            BRS = GetComponent<BulletRenderSystem>();
            BRS.Init();
            LogStartup("Bullet renderer initialized");

            EDB.Init();
            LogStartup("Enemies initialized");

            SSManager = transform.parent.Find("Canvases").Find("StageCanvas").Find("StageBoxParent").GetComponent<StageSelectManager>();
            SSManager.Init();
            LogStartup("Stage select initialized");

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
            // プレイエリア(32x18)の中央下をスタート位置にする。PlayerController.Init が
            // ここでの transform 位置を initialPos として取り込み、リトライ時もここへ戻る。
            ptemp.transform.position = new Vector3(16f, 3f, 0f);
            PController.Init(ptemp);
            // Keep the player readable on top of the GPU bullet layer (URP draws
            // the instanced bullets after the 2D sprite passes, otherwise burying it).
            if (ptemp.GetComponent<PlayerFrontOverlay>() == null)
            {
                ptemp.AddComponent<PlayerFrontOverlay>();
            }
            LogStartup("Player initialized");

            // Attach the (idle-by-default) camera shake to the main gameplay
            // camera so event-driven landing shakes (e.g. the stone stage M21
            // golem slam) can play. It never touches the transform until it is
            // triggered, so every other stage is unaffected.
            FreezeAspectRate cameraRig = FindObjectOfType<FreezeAspectRate>();
            if (cameraRig != null && cameraRig.GetComponent<CameraShake>() == null)
            {
                cameraRig.gameObject.AddComponent<CameraShake>();
            }

            SReader = GetComponent<StageReader>();

            state = GameState.Title;
            // SSManager.Init はこの代入より前に走る(シーン値の state を見ている)
            // ので、Title 確定後に JSAB オーバーレイの表示可否を取り直す。
            SSManager.NotifyGameStateChanged();

            ready = true;
            LogStartup("Awake ready");
        }
        catch (Exception ex)
        {
            ready = false;
            Debug.LogException(ex, this);
        }
<<<<<<< HEAD
=======

        ready = false;

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
>>>>>>> origin/main
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void LogStartup(string message)
    {
        Debug.Log($"[GManagerStartup] {message}", this);
    }

    // ドメインリロード後は Awake が再実行されず Control が null のままになるので、
    // OnEnable(リロード後にも呼ばれる)で張り直す。エディタ拡張の null ガードが
    // 「未初期化」として正しく扱えるようにするため。通常起動では Awake が先に設定済み。
    private void OnEnable()
    {
        if (Control == null) Control = this;
    }

    public void Update()
    {
        if (!ready)
        {
            // state はシリアライズされて生き残るため、Playing のまま ready=false は
            // Play 中のドメインリロードで実行時状態が失われた証拠。一度だけ知らせる。
            if (!reloadDuringPlayWarned && state != GameState.Title)
            {
                reloadDuringPlayWarned = true;
                Debug.LogWarning(
                    "[GManager] Domain reload during Play Mode detected; runtime state was lost. " +
                    "Stop Play Mode and restart the stage.", this);
            }
            return;
        }

        // While paused, only watch for Esc to resume; gameplay updates are skipped
        // (timeScale is 0 and all audio is paused via AudioListener).
        if (isPaused)
        {
            IManager.UpdateInput();
            if (IManager.backPressedThisFrame)
            {
                // Esc closes the confirm popup first; otherwise it resumes.
                if (optionMenu == null) SetPaused(false);
                else if (!optionMenu.HandleBack()) optionMenu.BeginResume();
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
        UpdateStoneLandingShake();

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
            if (UpdateTitleMenu(t, ref stageSelectButton))
            {
                // The settings or transfer layer consumed this frame; do not
                // leak input into stage select.
                return;
            }
        }

        SSManager.UpdateSelect(IManager.upPressedThisFrame, IManager.downPressedThisFrame, IManager.leftPressedThisFrame, IManager.rightPressedThisFrame, t, stageSelectButton, IManager.backPressedThisFrame);
    }

    // Fires the M21 golem landing camera shake once, the frame the stone stage
    // clock crosses StoneLandingShakeTime. Called only from the non-paused
    // Playing update path, so it is naturally pause- and stage-gated: the clock
    // does not advance while paused, and non-stone stages never match the name.
    // A restart or backward seek drops the clock below the threshold, which
    // re-arms the crossing so a replay shakes again.
    private void UpdateStoneLandingShake()
    {
        if (SReader == null || !SReader.IsReady)
        {
            prevStageClock = -1f;
            return;
        }

        StageData stage = SReader.CurrentStage;
        if (stage == null || stage.stageName != StoneStageName)
        {
            prevStageClock = -1f;
            return;
        }

        float now = SReader.CurrentTime;
        if (prevStageClock >= 0f
            && prevStageClock < StoneLandingShakeTime
            && now >= StoneLandingShakeTime)
        {
            CameraShake.Trigger(StoneLandingShakeAmplitude, StoneLandingShakeDuration, StoneLandingShakeFrequency);
        }
        prevStageClock = now;
    }

    // Drives the title menu / settings / transfer layers. Returns true when the
    // frame was fully consumed by an overlay (options or transfer).
    private bool UpdateTitleMenu(float t, ref bool stageSelectButton)
    {
        switch (titlePhase)
        {
            case TitlePhase.Options:
                UpdateTitleOptions();
                return true;

            case TitlePhase.Transfer:
                UpdateTitleTransfer();
                return true;

            case TitlePhase.Starting:
                // タイトル退場演出中。覆いどきが来たらステージ選択を重ねて出す
                // (演出の残りは選択画面のフェードインと交差して続く)。
                titleStartTimer -= t;
                if (titleStartTimer <= 0f)
                {
                    titlePhase = TitlePhase.Menu;
                    state = GameState.ChoosingStage;
                    SSManager.ResetTimer();
                    SSManager.PlayEntrance();
                }
                return true;

            default: // Menu
                stageSelectButton = false;
                TManager?.UpdateMenu(t, IManager.upPressedThisFrame, IManager.downPressedThisFrame);

                // Require the button to be released once before the title accepts
                // a press, so a button still held from a previous screen cannot
                // instantly trigger a menu item.
                if (!titleArmed)
                {
                    if (!IManager.buttonPressed) titleArmed = true;
                    return false;
                }

                if (IManager.buttonPressedThisFrame)
                {
                    TitleManager.TitleMenuAction action =
                        TManager != null ? TManager.CurrentAction : TitleManager.TitleMenuAction.Start;
                    switch (action)
                    {
                        case TitleManager.TitleMenuAction.Start:
                            // 即座に切り替えず、タイトル側の退場演出を先に走らせる。
                            titlePhase = TitlePhase.Starting;
                            titleStartTimer = TManager != null ? TitleManager.StartExitCoverDelay : 0f;
                            TManager?.PlayStartExit();
                            return true;
                        case TitleManager.TitleMenuAction.Options:
                            OpenTitleOptions();
                            return true;
                        case TitleManager.TitleMenuAction.Transfer:
                            titlePhase = TitlePhase.Transfer;
                            TManager?.OpenTransfer();
                            return true;
                    }
                }
                return false;
        }
    }

    private void OpenTitleOptions()
    {
        titlePhase = TitlePhase.Options;
        // メニューは隠さない。設定画面は完成フレーム(メニュー・ロゴを含む)を
        // 撮ってぼかし背景にするので、退場させず背景に残す(第31便)。
        // The title never freezes time or audio; the option screen simply
        // overlays the running title. The Title sibling is drawn above the
        // OptionScreen in the scene, so lift the option screen to the front
        // while it is open, then restore its order on close.
        if (optionScreenObj != null)
        {
            // 直前のクローズフェード中の再オープンでは、退避済みの元位置を保持する
            // (現在位置は最前面に持ち上げた後の値なので上書きしない)。
            if (optionScreenSiblingIndex < 0)
            {
                optionScreenSiblingIndex = optionScreenObj.transform.GetSiblingIndex();
            }
            optionScreenObj.transform.SetAsLastSibling();
            optionScreenObj.SetActive(true);
            // タイトル文脈: 終了行を隠し、再開する=設定を閉じてタイトルへ戻る。
            optionMenu?.Open(true, CloseTitleOptions);
        }
    }

    private void UpdateTitleOptions()
    {
        if (optionScreenObj == null || !optionScreenObj.activeSelf)
        {
            CloseTitleOptions();
            return;
        }

        if (IManager.backPressedThisFrame)
        {
            if (optionMenu == null || !optionMenu.HandleBack()) CloseTitleOptions();
            return;
        }

        // タイトル文脈: 終了行は隠してあり、確定ボタンは「再開する」= 設定を
        // 閉じてタイトルへ戻る動作にのみ効く(OptionMenu 側で分岐)。音量・
        // エフェクトは左右で調整できる。
        optionMenu?.UpdateMenu(Time.unscaledDeltaTime,
            IManager.upPressedThisFrame, IManager.downPressedThisFrame,
            IManager.leftPressedThisFrame, IManager.rightPressedThisFrame,
            IManager.leftPressed, IManager.rightPressed,
            IManager.buttonPressedThisFrame);
    }

    private void CloseTitleOptions()
    {
        titlePhase = TitlePhase.Menu;
        // Require the confirm button to be released again before the menu accepts
        // a press, so the input used to dismiss the option screen (or a button
        // still held from it) cannot leak into the menu and instantly fire an
        // item on the frame the title regains control.
        titleArmed = false;
        TManager?.ShowMenu();

        // 設定画面は瞬時に消さず短いフェードで閉じる。背後のタイトルは走り続けて
        // いるので、以前の PlayReturnEntrance(0.78→1 の急ズーム)のような復帰演出は
        // 行わない(「カメラが変な動きをする」の原因だった)。重ね順はフェードが
        // 消え切ってから元に戻す。
        if (optionScreenObj == null) return;
        if (optionScreenObj.activeSelf && optionMenu != null)
        {
            optionMenu.CloseForTitle(RestoreOptionScreenOrder);
        }
        else
        {
            optionScreenObj.SetActive(false);
            RestoreOptionScreenOrder();
        }
    }

    private void RestoreOptionScreenOrder()
    {
        if (optionScreenObj != null && optionScreenSiblingIndex >= 0)
        {
            optionScreenObj.transform.SetSiblingIndex(optionScreenSiblingIndex);
            optionScreenSiblingIndex = -1;
        }
    }

    private void UpdateTitleTransfer()
    {
        TManager?.TickTransfer();

        if (IManager.backPressedThisFrame)
        {
            TManager?.CloseTransfer();
            titlePhase = TitlePhase.Menu;
            // Same re-arming guard as the option screen: swallow the dismiss input
            // so it cannot immediately trigger a menu item.
            titleArmed = false;
            TManager?.ShowMenu();
            return;
        }

        // CTRL+C: 発行済みコードをコピー(入力欄のフォーカスと衝突しないキー)。
        Keyboard kb = Keyboard.current;
        if (kb != null && (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed) && kb.cKey.wasPressedThisFrame)
        {
            TManager?.CopyTransferCode();
            return;
        }

        if (IManager.buttonPressedThisFrame && (TManager == null || !TManager.IsTransferInputFocused))
        {
            TManager?.ApplyTransfer();
        }
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
        await GoGameAsync(index);
    }

    public async Task GoGameAsync(int index)
    {
        StageData stage = SDB.GetStage(index);
        if (stage == null)
        {
            Debug.LogError($"Stage with index {index} not found!");
            return;
        }

<<<<<<< HEAD
        // raymee ランタイム互換: 共有 StageData を直接 Init すると難易度が効かず、Init が
        // 共有インスタンスを mutate(sort/index書込)してリプレイで状態が蓄積する。難易度
        // 解決済みのランタイムコピーを渡す。UI の selectedDifficulty(0=Easy/1=Normal/2=Lunatic)
        // を反映。現状データは Lunatic のみ(legacy 自動ラップ)なので、Easy/Normal を選んでも
        // CreateRuntimeCopy が top-level(Lunatic)へフォールバックして正常に起動する。
        Difficulty selected = (Difficulty)Mathf.Clamp(selectedDifficulty, 0, 2);
        StageData runtimeStage = stage.CreateRuntimeCopy(selected);

        playerHitCount = 0;
        counterHitBossCount = 0;
        QOrder?.ClearManagedEnemyDanmaku();
        PController?.ResetToCenter();
        TManager?.Dismiss();
        HideTitleBossBackdrop();

        bool initialized = await SReader.Init(runtimeStage);
        if (!initialized)
        {
            Debug.LogError($"Stage '{runtimeStage.stageName}' の初期化に失敗(endTime<=0 か難易度データ無し)。旧スキーマの石工は Stage3 のデータ変換まで起動しない想定。");
            return;
        }

        SReader.ResetStageClockToScheduledStart();
        HideTitleBossBackdrop();
        state = GameState.Playing;
        string historyDir = string.IsNullOrWhiteSpace(runtimeStage.stageDirectoryName)
            ? runtimeStage.stageName
            : runtimeStage.stageDirectoryName;
        PlayHistory.RecordPlay(historyDir);
        Debug.Log($"Started Stage: {runtimeStage.stageName} (requested={selected}, data={runtimeStage.resolvedDataDifficulty.displayName})");
    }

    // Opens/closes the pause (option) screen. Freezes game time and all audio.
    public void SetPaused(bool pause)
    {
        isPaused = pause;
        Time.timeScale = pause ? 0f : 1f;
        AudioListener.pause = pause;
        // The option menu captures the completed game frame and applies a
        // full-resolution UI blur. Do not stack the gameplay noise blur over it.
        CManager?.SetMenuBlur(false);
        if (optionScreenObj != null)
        {
            optionScreenObj.SetActive(pause);
            if (pause) optionMenu?.Open();
        }
    }

    // 「プレイを終了」(はい) : ends the session and reboots cleanly to the title
    // screen by reloading the scene.
    public async void QuitPlay()
    {
        // Keep the option screen visible beneath the pixels. Time must resume
        // for scene systems, while the transition itself uses unscaled time.
        isPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = true;
        PixelTransition[] transitions = UnityEngine.Object.FindObjectsByType<PixelTransition>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (transitions.Length > 0)
        {
            transitions[0].SetColor(Color.white);
            await transitions[0].Cover();
            // Set this only after the previously inactive transition has run
            // Start(). Otherwise the outgoing scene consumes the flag before
            // the title scene is loaded and remains hidden behind the pixels.
            PixelTransition.RevealAfterNextSceneLoad(true);
        }
        AudioListener.pause = false;
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
=======
        StageData runtimeStage = stage.CreateRuntimeCopy(difficulty);

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
        await SReader.Init(runtimeStage);
        state = GameState.Playing;
        Debug.Log($"Started Stage: {runtimeStage.stageName}, Difficulty: {CurrentDifficultySelection.displayName} (Data: {CurrentDataDifficultySelection.displayName})");
>>>>>>> origin/main
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

    private void HideTitleBossBackdrop()
    {
        GameObject titleBoss = GameObject.Find("boss");
        if (titleBoss == null) return;

        Renderer[] renderers = titleBoss.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            renderer.enabled = false;
        }
    }
}

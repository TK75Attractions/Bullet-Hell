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
using TMPro;

public class GManager : MonoBehaviour
{
    static public GManager Control;
    public bool isRaymeeDebug = false; // デバッグ用のフラグ。これが true のとき、特定のデバッグコードが有効になる。

    [Header("Raymee Debug Controls")]
    [Tooltip("押している間だけゲーム全体の進行速度を上げるキー。isRaymeeDebug が true のときだけ有効です。")]
    [SerializeField] private Key debugBulletFastForwardKey = Key.Digit1;
    [Tooltip("自機の透過表示を切り替えるキー。isRaymeeDebug が true のときだけ有効です。")]
    [SerializeField] private Key debugPlayerTransparencyToggleKey = Key.Digit2;
    [Tooltip("自機の無敵状態を切り替えるキー。isRaymeeDebug が true のときだけ有効です。")]
    [SerializeField] private Key debugPlayerInvincibilityToggleKey = Key.Digit3;
    [FormerlySerializedAs("debugBulletTimeScale")]
    [SerializeField, Min(1f)] private float debugGameTimeScale = 4f;
    [SerializeField, Range(0f, 1f)] private float debugPlayerTransparencyAlpha = 0.35f;

    private bool debugPlayerTransparent;
    private bool debugPlayerInvincible;
    private bool debugFastForwardActive;

    /// <summary>
    /// Raymee デバッグで有効化された自機無敵状態。<see cref="isRaymeeDebug"/> が false に
    /// なった瞬間から必ず false になるため、通常プレイには影響しない。
    /// </summary>
    public bool IsRaymeeDebugPlayerInvincible => isRaymeeDebug && debugPlayerInvincible;

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
    // 2P(その1): 2 人プレイ時のみ使う 2 人目。既定は 1 人プレイなので生成のみ・非アクティブ。
    public PlayerController PController2;
    private GameObject player2Obj;
    // タイトルで選択される人数。false=1 人(既定)、true=2 人。1P モードは完全に従来どおり。
    public bool twoPlayer = false;

    private const string PlayerSidesReversedPrefsKey = "players.sidesReversed";
    private const float TwoPlayerLeftX = 14f;
    private const float TwoPlayerRightX = 18f;
    private const float PlayerStartY = 3f;
    [SerializeField] private bool playerSidesReversed = false;
    public bool PlayerSidesReversed => playerSidesReversed;

    public InputManager IManager;
    public StageReader SReader;
    public AudioManager AManager;
    public BeatManager BManager;
    public CManager CManager;
    public StageSelectManager SSManager;
    public TitleManager TManager;
    public ResultScreen RManager;
    // 0=Easy / 1=Normal / 2=Lunatic。現状データは Lunatic のみのため既定を Lunatic に。
    public int selectedDifficulty = 2;
    public bool isPaused = false;
    private GameObject optionScreenObj;
    private OptionMenu optionMenu;
    private bool titleArmed = false;
    private bool resultTransitioning = false;
    // ステージ選択(カルーセル)で Esc / B を押してタイトルへ戻る途中フラグ。
    // 白カバー→シーン再読込までの間、カウントダウン自動スタートやタイトル/選択の
    // 入力処理を止めるために立てる(再読込で GManager が作り直され false に戻る)。
    private bool returningToTitle = false;
    private int currentStageIndex = -1;

    // Which layer of the title screen currently owns input.
    // Starting はスタート決定後、タイトル退場演出がステージ選択に覆われる
    // までの待ち(入力は消費し、時間経過で ChoosingStage へ切り替える)。
    private enum TitlePhase { Menu, Options, Transfer, Ranking, Starting }
    private TitlePhase titlePhase = TitlePhase.Menu;
    private float titleStartTimer;
    private int optionScreenSiblingIndex = -1;
    public BulletBufferManager BClipManager;
    // raymee ランタイム互換: raymee 系(StageReader/QuadOrder/BulletRenderSystem)は
    // バッファ管理を BulletBuffers、自機色を playerColor という名前で参照する。marron は
    // 同じ BulletBufferManager を BClipManager として保持しているためエイリアスで橋渡し。
    public BulletBufferManager BulletBuffers => BClipManager;
    public Color playerColor = new Color(1f, 1f, 0.6f, 1f);
    [Header("Player Visual Palette")]
    public Color playerColor1 = new Color(23f / 255f, 178f / 255f, 1f, 1f);
    public Color playerColor2 = new Color(1f, 23f / 255f, 92f / 255f, 1f);
    public QuadOrder QOrder;

    /// <summary>
    /// 実行中のプレイヤーに 2 色パレットを即時反映する。
    /// Inspector から playerColor1 / playerColor2 を変更した場合も、次フレームに同じ値が反映される。
    /// </summary>
    public void SetPlayerPalette(Color color1, Color color2)
    {
        playerColor1 = color1;
        playerColor2 = color2;
        PController?.SetVisualColors(color1, color2);
    }
    public BulletTypeDataBase BTDB;

    public StageDataBase SDB;
    public SEDataBase SEDB;
    public EnemyDataBase EDB;
    public BulletRenderSystem BRS;

    public float gameTime;
    // Play Mode 中にドメインリロード(スクリプト再コンパイル等)が走ると static の
    // Control や非シリアライズの実行時状態(BClipManager 等)は消えるが、この
    // インスタンス自体はシリアライズ経由で生き残る。ready が true のまま復元されると
    // Update が壊れた状態のまま毎フレーム走り、PlayerController.Move が
    // GManager.Control.IManager で NRE ループする。NonSerialized でリロード後は
    // 必ず false に戻し、Update/LateUpdate を安全に停止させる。
    [NonSerialized] public bool ready = false;
    private bool reloadDuringPlayWarned = false;

    public bool musicOn = false;
    public int playerHitCount = 0;
    // 2P(その1): P2 の被弾回数は P1 とは別カウント(無敵時間も個別)。1P では未使用。
    public int playerHitCount2 = 0;
    public int counterHitBossCount = 0;

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

            Transform canvasesRoot = transform.parent.Find("Canvases");
            TMP_Text fontSource = canvasesRoot != null
                ? canvasesRoot.GetComponentInChildren<TMP_Text>(true)
                : null;
            RManager = ResultScreen.Create(canvasesRoot, fontSource != null ? fontSource.font : null);
            RManager.ActionRequested += HandleResultAction;
            currentStageIndex = SSManager.CurrentStageIndex;
            LogStartup("Result screen initialized");

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

            // 2P(その1): 2 人目を生成しておく(既定は非アクティブ)。実際に動かすのは
            // タイトルで 2 人プレイを選び、ステージ開始で twoPlayer が真のときだけ。
            // 1 人プレイでは非アクティブのまま=描画も更新もされず 1P 挙動に影響しない。
            PController2 = new PlayerController { playerIndex = 1 };
            player2Obj = Instantiate(PlayerObj);
            player2Obj.transform.position = new Vector3(20f, 3f, 0f);
            PController2.Init(player2Obj);
            if (player2Obj.GetComponent<PlayerFrontOverlay>() == null)
            {
                player2Obj.AddComponent<PlayerFrontOverlay>();
            }
            player2Obj.SetActive(false);
            LogStartup("Player2 initialized (inactive)");

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
        playerSidesReversed = PlayerPrefs.GetInt(
                PlayerSidesReversedPrefsKey, playerSidesReversed ? 1 : 0) != 0;

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
            UpdateRaymeeDebugControls();
            IManager.UpdateInput();
            if (IManager.backPressedThisFrame)
            {
                // Esc / P1 の B は確認ポップアップを閉じ、無ければポーズ解除。
                if (optionMenu == null) SetPaused(false);
                else if (!optionMenu.HandleBack()) optionMenu.BeginResume();
            }
            else if (IManager.p2BackPressedThisFrame)
            {
                // P2 の B はポーズのトグルのみ(メニュー階層の操作は P1 の設計)。
                if (optionMenu == null) SetPaused(false);
                else optionMenu.BeginResume();
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

        UpdateRaymeeDebugControls();
        float t = Time.deltaTime;
        gameTime += t;

        if (PController != null)
        {
            // The player can also move during the pre-stage tutorial.
            if (state == GameState.Playing || state == GameState.Tutorial) PController.UpdatePos(t);
        }
        // 2P: プレイ中だけでなく、両者の操作確認を行うチュートリアル中も P2 を動かす。
        if (twoPlayer && PController2 != null && player2Obj != null && player2Obj.activeSelf
            && (state == GameState.Playing || state == GameState.Tutorial))
        {
            PController2.UpdatePos(t);
        }

        SReader.UpdateStage(t, debugFastForwardActive);
        UpdateStoneLandingShake();

        if (state == GameState.Playing && SReader.HasReachedEndTime && !resultTransitioning)
        {
            ShowResult(true);
        }

        // Time.timeScale により自機・ステージ・演出を含むゲーム全体が加速済みなので、
        // 各ゲームロジックにはスケール済みの deltaTime をそのまま渡す。
        QOrder.QuadUpdate(t);
        IManager.UpdateInput();

        if (state == GameState.Result)
        {
            RManager?.Tick(
                IManager.leftPressedThisFrame,
                IManager.rightPressedThisFrame,
                IManager.upPressed,
                IManager.downPressed,
                IManager.upPressedThisFrame,
                IManager.downPressedThisFrame,
                IManager.buttonPressed,
                IManager.buttonPressedThisFrame,
                IManager.backPressedThisFrame);
            return;
        }

        // Esc / P1 の B、または P2 の B(プレイ中のポーズのみ有効)でポーズ画面を開く。
        if (state == GameState.Playing && (IManager.backPressedThisFrame || IManager.p2BackPressedThisFrame))
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

    /// <summary>
    /// Raymee 専用デバッグキーを処理する。
    /// 数字 1 は押している間だけ加速し、数字 2 / 3 は押すたびに透過 / 無敵を切り替える。
    /// </summary>
    private void UpdateRaymeeDebugControls()
    {
        if (!isRaymeeDebug)
        {
            // Inspector でデバッグをオフに戻した場合も、見た目・当たり判定を即座に
            // 通常状態へ復帰させる。
            debugPlayerTransparent = false;
            debugPlayerInvincible = false;
            debugFastForwardActive = false;
            PController?.SetDebugTransparency(false, 1f);
            ApplyDebugGameTimeScale();
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            debugFastForwardActive = false;
            PController?.SetDebugTransparency(debugPlayerTransparent, debugPlayerTransparencyAlpha);
            ApplyDebugGameTimeScale();
            return;
        }

        if (keyboard[debugPlayerTransparencyToggleKey].wasPressedThisFrame)
        {
            debugPlayerTransparent = !debugPlayerTransparent;
        }

        if (keyboard[debugPlayerInvincibilityToggleKey].wasPressedThisFrame)
        {
            debugPlayerInvincible = !debugPlayerInvincible;
        }

        PController?.SetDebugTransparency(debugPlayerTransparent, debugPlayerTransparencyAlpha);
        debugFastForwardActive = keyboard[debugBulletFastForwardKey].isPressed;
        ApplyDebugGameTimeScale();
    }

    private void ApplyDebugGameTimeScale()
    {
        if (isPaused)
        {
            Time.timeScale = 0f;
            return;
        }

        Time.timeScale = isRaymeeDebug && debugFastForwardActive
            ? Mathf.Max(1f, debugGameTimeScale)
            : 1f;
    }

    // Drives the title menu / settings / transfer layers. Returns true when the
    // frame was fully consumed by an overlay (options or transfer).
    private bool UpdateTitleMenu(float t, ref bool stageSelectButton)
    {
        // タイトルへ戻る遷移中(白カバー〜シーン再読込)は入力を全消費し、
        // タイトルメニューにも選択画面(下の SSManager.UpdateSelect)にも
        // 処理を漏らさない。カバー下でのメニュー誤発火や自動スタートを防ぐ。
        if (returningToTitle) return true;

        switch (titlePhase)
        {
            case TitlePhase.Options:
                UpdateTitleOptions();
                return true;

            case TitlePhase.Transfer:
                UpdateTitleTransfer();
                return true;

            case TitlePhase.Ranking:
                UpdateTitleRanking();
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

                // 人数選択: P1 の ←=1 人 / →=2 人。選択に合わせて入力基盤(P2 キーボード)も
                // 切り替える。実機は P1 スティック左右、キーボードは 2P モードだと矢印が P2 に
                // 回るため WASD の A/D で操作する(WASD は常に P1)。
                if (IManager.leftPressedThisFrame)
                {
                    TManager?.SetTwoPlayer(false);
                    SetPlayerCount(false);
                }
                else if (IManager.rightPressedThisFrame)
                {
                    TManager?.SetTwoPlayer(true);
                    SetPlayerCount(true);
                }

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
                    AManager?.PlayDecisionSE();   // タイトルメニュー確定
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
                        case TitleManager.TitleMenuAction.Ranking:
                            titlePhase = TitlePhase.Ranking;
                            TManager?.OpenRanking();
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

    // 方向シーケンス入力(SPEC §1)。B は TitleManager 側で短押し(1文字削除)/
    // 長押し(0.6s・画面を閉じる)を判定するため、backPressedThisFrame ではなく
    // backPressed(レベル)を渡す。戻り値 true は長押し完了=タイトルへ戻る合図。
    private void UpdateTitleTransfer()
    {
        if (TManager == null) return;
        bool close = TManager.TickTransferInput(Time.unscaledDeltaTime,
            IManager.upPressedThisFrame, IManager.downPressedThisFrame,
            IManager.leftPressedThisFrame, IManager.rightPressedThisFrame,
            IManager.buttonPressedThisFrame, IManager.backPressed);
        if (close)
        {
            TManager.CloseTransfer();
            titlePhase = TitlePhase.Menu;
            // Same re-arming guard as the option screen: swallow the dismiss input
            // so it cannot immediately trigger a menu item.
            titleArmed = false;
            TManager.ShowMenu();
        }
    }

    // ランキング盤面(SPEC §2.2)。B(edge)で戻る。
    private void UpdateTitleRanking()
    {
        if (TManager == null) return;
        bool close = TManager.TickRankingInput(
            IManager.leftPressedThisFrame, IManager.rightPressedThisFrame,
            IManager.upPressedThisFrame, IManager.downPressedThisFrame,
            IManager.buttonPressedThisFrame, IManager.backPressedThisFrame);
        if (close)
        {
            TManager.CloseRanking();
            titlePhase = TitlePhase.Menu;
            titleArmed = false;
            TManager.ShowMenu();
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

    public async void GoGame(int index)
    {
        await GoGameAsync(index);
    }

    public async Task GoGameAsync(int index, bool preservePlayerPositions = false)
    {
        StageData stage = SDB.GetStage(index);
        if (stage == null)
        {
            Debug.LogError($"Stage with index {index} not found!");
            return;
        }

        // raymee ランタイム互換: 共有 StageData を直接 Init すると難易度が効かず、Init が
        // 共有インスタンスを mutate(sort/index書込)してリプレイで状態が蓄積する。難易度
        // 解決済みのランタイムコピーを渡す。UI の selectedDifficulty(0=Easy/1=Normal/2=Lunatic)
        // を反映。現状データは Lunatic のみ(legacy 自動ラップ)なので、Easy/Normal を選んでも
        // CreateRuntimeCopy が top-level(Lunatic)へフォールバックして正常に起動する。
        Difficulty selected = (Difficulty)Mathf.Clamp(selectedDifficulty, 0, 2);
        StageData runtimeStage = stage.CreateRuntimeCopy(selected);
        currentStageIndex = index;

        playerHitCount = 0;
        playerHitCount2 = 0;
        counterHitBossCount = 0;
        QOrder?.ClearManagedEnemyDanmaku();
        if (!preservePlayerPositions)
        {
            PreparePlayersForStageStart();
        }
        else if (player2Obj != null)
        {
            player2Obj.SetActive(twoPlayer);
        }
        TManager?.Dismiss();
        HideTitleBossBackdrop();
        // 曲選択で流れていたタイトル BGM を、ステージ BGM 開始前にフェードアウトして
        // ぶつ切りを避ける(2026-07-13 指摘)。SReader.Init 内の PlayBGM が新しい再生を
        // 確定する際にフェードは自動キャンセルされる。リトライ時は共有 BGM が既に停止
        // 済みで、この呼び出しは安全に何もしない。
        AManager?.FadeOutAndStopBGM(0.5f);

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
        // 2P プレイの記録は 1P と別枠(別 PlayerPrefs キー)へ保存する。記録直前に
        // 現在の人数モードを反映し、scene 再読込後の静的状態の取り違えを防ぐ。
        PlayHistory.TwoPlayerMode = twoPlayer;
        PlayHistory.RecordPlay(historyDir);
        Debug.Log($"Started Stage: {runtimeStage.stageName} (requested={selected}, data={runtimeStage.resolvedDataDifficulty.displayName})");
    }
    // ステージ終了条件の共通入口。現状は StageReader.endTime 到達をクリアとして
    // 自動呼び出しする。TODO: ライフ制/失敗条件が追加されたら ShowResult(false)
    // をその確定箇所から呼ぶ。
    public async void ShowResult(bool cleared)
    {
        await ShowResultAsync(cleared, true);
    }

#if UNITY_EDITOR
    // Play Mode で UI を単独確認するための入口。履歴は変更しない。
    public async void DebugShowResult(bool cleared = true)
    {
        await ShowResultAsync(cleared, false);
    }
#endif

    private async Task ShowResultAsync(bool cleared, bool recordHistory)
    {
        if (resultTransitioning || RManager == null) return;
        resultTransitioning = true;

        StageData stage = SReader != null && SReader.CurrentStage != null
            ? SReader.CurrentStage
            : ResolveSelectedStage();
        if (stage == null) stage = ResolveSelectedStage();
        float endTime = stage != null ? stage.endTime : 0f;
        float elapsed = SReader != null && SReader.IsReady
            ? SReader.CurrentTime
            : (cleared ? endTime : endTime * 0.63f);

        PixelTransition transition = FindPixelTransition();
        if (transition != null)
        {
            transition.SetColor(Color.white);
            await transition.WhiteoutCover();
        }

        state = GameState.Result;
        musicOn = false;
        // ステージ BGM をぶつ切りにせずフェードアウト(2026-07-13 指摘)。リザルト BGM
        // は別ソース(ResultScreenBgm)で dspTime スケジュール再生されるので、覆いの下で
        // ステージ BGM のフェードアウトと自然にクロスする。
        AManager?.FadeOutAndStopBGM(0.5f);
        QOrder?.ClearAllGameplayBulletsImmediate();
        // 2P: リザルトへ移る際は P2 をフィールドから隠す(リザルトの左右分割は別便)。
        if (player2Obj != null) player2Obj.SetActive(false);

        if (recordHistory && cleared && stage != null)
        {
            string historyDir = string.IsNullOrWhiteSpace(stage.stageDirectoryName)
                ? stage.stageName
                : stage.stageDirectoryName;
            // クリア記録も人数モード別枠へ(RecordPlay と同様)。
            PlayHistory.TwoPlayerMode = twoPlayer;
            PlayHistory.RecordClear(historyDir);
            // 引き継ぎコードの実績は1P専用(SPEC §1.4: 2Pは引き継ぎ対象外)。
            if (!twoPlayer)
            {
                bool noMiss = playerHitCount <= 0;
                TransferAchievements.RecordClear(historyDir, selectedDifficulty, noMiss);
            }
        }

        RManager.Prepare(stage, selectedDifficulty, cleared, playerHitCount,
            counterHitBossCount, elapsed, endTime, twoPlayer, playerHitCount2);
        SReader?.StopStage();

        if (transition != null) await transition.MosaicReveal();
        RManager.PlayEntrance();
        resultTransitioning = false;
    }

    private StageData ResolveSelectedStage()
    {
        int index = currentStageIndex >= 0
            ? currentStageIndex
            : (SSManager != null ? SSManager.CurrentStageIndex : 0);
        if (SDB == null || index < 0 || index >= SDB.GetStageCount()) return null;
        currentStageIndex = index;
        return SDB.GetStage(index);
    }

    private PixelTransition FindPixelTransition()
    {
        PixelTransition[] transitions = UnityEngine.Object.FindObjectsByType<PixelTransition>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        return transitions.Length > 0 ? transitions[0] : null;
    }

    private async void HandleResultAction(ResultScreen.Action action)
    {
        if (resultTransitioning || RManager == null || !RManager.Visible) return;
        resultTransitioning = true;
        AManager?.PlayDecisionSE();   // リザルトのボタン確定(ステージ選択/プレイを終わる)

        // リザルト BGM をぶつ切りにせずフェードアウトしてから覆う(2026-07-13 指摘)。
        // await で無音まで下げ切ってから画面を覆う=遷移が自然につながる。
        await RManager.FadeOutBgmAsync(0.4f);

        PixelTransition transition = FindPixelTransition();
        if (transition != null)
        {
            transition.SetColor(Color.white);
            await transition.WhiteoutCover();
        }

        RManager.HideImmediate();
        AudioListener.pause = false;
        Time.timeScale = 1f;

        if (action == ResultScreen.Action.Title)
        {
            // タイトルへ: シーンを再読込してタイトルをクリーンに復元する(QuitPlay と
            // 同流儀)。タイトル BGM(Init→StartTitleBgm)・背景・入力状態がすべて
            // 初期化され、半端な復元の取りこぼしが無い。画面は WhiteoutCover で覆われた
            // ままなので、新シーンは覆いの下から現れる。
            QOrder?.ClearAllGameplayBulletsImmediate();
            PixelTransition.RevealAfterNextSceneLoad(true);
            resultTransitioning = false;
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            return;
        }

        if (action == ResultScreen.Action.Retry)
        {
            int retryIndex = currentStageIndex >= 0
                ? currentStageIndex
                : (SSManager != null ? SSManager.CurrentStageIndex : 0);
            await GoGameAsync(retryIndex);
        }
        else
        {
            QOrder?.ClearAllGameplayBulletsImmediate();
            state = GameState.ChoosingStage;
            SSManager?.PrepareReturnFromResult();
            // リザルトで止まっていたタイトル/選択 BGM を再開し、選択画面の無音を解消
            // (2026-07-13 指摘)。共有 BGMSource でフェードインしてつなぐ。
            TManager?.EnsureTitleBgm();
        }

        if (transition != null) await transition.MosaicReveal();
        resultTransitioning = false;
    }

    // Opens/closes the pause (option) screen. Freezes game time and all audio.
    public void SetPaused(bool pause)
    {
        isPaused = pause;
        ApplyDebugGameTimeScale();
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
    }

    // ステージ選択(カルーセル)で Esc / P1 の B を押したときにタイトルへ戻る。
    // Result→Title / QuitPlay と同流儀で白カバー→シーン再読込し、タイトル BGM・
    // 背景・入力状態をクリーンに復元する(半端な手動復元の取りこぼしを避ける)。
    // 再読込後は PixelTransition の title-return 演出でタイトルが復帰する。
    public async void ReturnToTitleFromSelect()
    {
        if (returningToTitle || state != GameState.ChoosingStage) return;
        returningToTitle = true;
        // 即座に Title 状態へ移し、選択画面のカウントダウン自動スタート等の
        // 副作用を止める(UpdateTitleMenu の returningToTitle ガードと二重の保険)。
        state = GameState.Title;
        SSManager?.NotifyGameStateChanged();
        // 選択/タイトル BGM は再読込で作り直されるが、覆いの間に手前でフェード
        // アウトしておくとぶつ切りにならない。
        AManager?.FadeOutAndStopBGM(0.4f);

        PixelTransition[] transitions = UnityEngine.Object.FindObjectsByType<PixelTransition>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (transitions.Length > 0)
        {
            transitions[0].SetColor(Color.white);
            await transitions[0].Cover();
            // 覆い切ってからフラグを立てる。先に立てると再読込前の旧シーンが
            // フラグを消費してタイトルが覆いの下に隠れたままになる(QuitPlay 準拠)。
            PixelTransition.RevealAfterNextSceneLoad(true);
        }
        QOrder?.ClearAllGameplayBulletsImmediate();
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    public void AddPlayerHitCount(int value = 1)
    {
        if (value <= 0) return;
        playerHitCount += value;
    }

    // 2P: プレイヤー別の被弾加算(0=P1, 1=P2)。衝突判定(QuadOrder)から player 別に呼ぶ。
    public void AddPlayerHitCount(int playerIndex, int value)
    {
        if (value <= 0) return;
        if (playerIndex == 1) playerHitCount2 += value;
        else playerHitCount += value;
    }

    // タイトルの人数選択から呼ぶ。入力基盤(キーボードの P2 割り当て)も同時に切り替える。
    public void SetPlayerCount(bool two)
    {
        twoPlayer = two;
        if (IManager != null) IManager.twoPlayerMode = two;
        // 履歴/引き継ぎコードもモード別枠に切り替える(1P と 2P を混ぜない)。
        PlayHistory.TwoPlayerMode = two;
    }

    // 2P のチュートリアルと通常開始で共用する左右対称の開始配置。
    public void PreparePlayersForStageStart()
    {
        if (twoPlayer)
        {
            PController?.ResetForStageAt(GetPlayerStartPosition(0, playerSidesReversed));
            if (player2Obj != null) player2Obj.SetActive(true);
            PController2?.ResetForStageAt(GetPlayerStartPosition(1, playerSidesReversed));
        }
        else
        {
            PController?.ResetForStageAt(new float2(16f, PlayerStartY));
            if (player2Obj != null) player2Obj.SetActive(false);
        }
    }

    public void PreparePlayersForTutorial()
    {
        PreparePlayersForStageStart();
    }

    public static float2 GetPlayerStartPosition(int playerIndex, bool reversed)
    {
        bool goesRight = (playerIndex == 1) != reversed;
        return new float2(goesRight ? TwoPlayerRightX : TwoPlayerLeftX, PlayerStartY);
    }

    public void SetPlayerSidesReversed(bool reversed)
    {
        if (playerSidesReversed == reversed) return;
        playerSidesReversed = reversed;
        PlayerPrefs.SetInt(PlayerSidesReversedPrefsKey, reversed ? 1 : 0);
        PlayerPrefs.Save();
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

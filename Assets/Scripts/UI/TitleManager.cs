using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TitleManager : MonoBehaviour
{
    // タイトル BGM(Discotheque)の実測 BPM。ロゴのビートパルス(振動)・図形の
    // フラッシュ周期をこの値に同期させる。測定: Tools/measure_bpm.py の
    // オンセット包絡自己相関 + 位相最適化櫛フィルタ(拍数正規化)で 130.00 BPM
    // (拍間隔 461.5ms)を確定(2026-07-13)。
    [SerializeField] private float bpm = 130f;
    // 引き継ぎコード表示・入力用の可読フォント(等幅コードフォント。0/O・1/I が
    // 紛れない)。未割当なら uiFont にフォールバック(第31便)。
    [SerializeField] private TMP_FontAsset codeFont;

    private CanvasGroup group;
    private TMP_Text promptText;
    private RectTransform logoRect;
    private float logoBaseY;
    private float animTime;
    private bool dismissed;
    // プレイ終了(シーン再読込)からの復帰専用のパンチイン演出の状態。設定画面を
    // 閉じたときには使わない(タイトルは背後で動き続けているため演出不要)。
    private bool returnAnimating;
    private Image returnBackdrop;

    private float beatTimer;
    private float beatPulse;
    // タイトル BGM(Discotheque)。AManager 常駐 BGMSource でループ再生し、
    // 戻り値の AudioSource から再生位置を読んでロゴのビートパルスを音にロックする。
    // clip の t=0 は実ダウンビート(2.763s)にトリム済み=位相 0 が拍頭。
    private AudioSource titleBgmSource;
    // Discotheque を stone(最も大きいステージ -10.7LUFS)より僅かに下の -12LUFS へ
    // 揃える減衰(実測 -6.7LUFS → -5.3dB)。Tools/measure_bpm.py / ffmpeg ebur128。
    // 2026-07-13 指摘「もう一段下げる」で 0.55→0.37(約 -3.4dB / -15.4LUFS 相当)。
    private const float TitleBgmVolume = 0.37f;
    // リザルト→選択復帰時のタイトル BGM 立ち上げ秒数(ぶつ切り防止)。
    private const float TitleBgmFadeIn = 0.6f;
    // ビートパルスの減衰(拍に対する割合)。frac=0(拍頭)で 1、この割合で 0 に。
    // 旧自走版(dt*5 減衰≒0.2s/拍0.46s)と同等の 0.43。
    private const float BeatPulseDecayFrac = 0.43f;
    private Graphic[] shapeGraphics = new Graphic[0];
    private float[] shapeBaseAlphas = new float[0];

    // Big flat shapes drifting slowly behind the logo (Just Shapes & Beats vibe).
    private struct ShapeAnim
    {
        public RectTransform rect;
        public Vector2 basePos;
        public float phase;
        public float speedX;
        public float speedY;
        public float ampX;
        public float ampY;
        public float rotSpeed;
    }

    private ShapeAnim[] shapes = new ShapeAnim[0];

    // ---- Title menu + transfer panel (black / cyan / deep navy) -------------
    public enum TitleMenuAction { Start = 0, Options = 1, Transfer = 2 }

    private static readonly Color Cyan = new Color(0.219f, 0.761f, 0.878f);
    private static readonly Color CyanDim = new Color(0.11f, 0.34f, 0.40f);
    private static readonly Color Navy = new Color(0.03f, 0.05f, 0.11f, 1f);
    private static readonly Color NavyDeep = new Color(0.015f, 0.028f, 0.06f, 0.98f);
    private static readonly Color ErrorRed = new Color(0.96f, 0.46f, 0.52f);

    // The title menu clones the difficulty-select rows (DefficultyBar) so both
    // screens share one design language: slanted StageBar banner + StageName
    // label + gliding white slash brackets. 統一便: バナー本体はリザルト画面で
    // 確立した焼き込み(銀枠+シアンリム+青縦グラデ・UiButtonStyle)へ差し替え。
    private static readonly Color MenuTextBase = new Color(0.85f, 0.93f, 1f);

    // How far above its scene-authored position the logo is lifted.
    // 2026-07-13: 上げすぎ(160)を元の位置(130)へ戻す。実フレーム(720p)で確認した
    // 通り、ボタン3行(660x160・行間172)は logo=130 でも画面内にそのまま収まり
    // (行はロゴと独立に配置され下端に余白がある)、ロゴ可視下端とスタート上端の
    // 間隔も約50px 空くため、行間側の調整は不要だった。
    private const float LogoRaiseOffset = 130f;

    // スタート決定からステージ選択を重ね始めるまでの時間。GManager がこの時間
    // 経過後に state を切り替えて SSManager.PlayEntrance を呼ぶ(演出は総尺
    // StartExitTotal まで続き、選択画面のフェードインと交差する)。
    // 第31便: スタート演出が「フラッシュみたい」に速く感じるため約1.25倍に伸ばす
    // (0.30→0.375 / 0.60→0.75)。CoverDelay はステージ選択が重なり始める時刻。
    public const float StartExitCoverDelay = 0.375f;
    private const float StartExitTotal = 0.75f;

    private TMP_FontAsset uiFont;
    private RectTransform menuRoot;
    private TMP_Text[] menuItems = new TMP_Text[0];
    private RectTransform[] menuItemRects = new RectTransform[0];
    private CanvasGroup[] menuRowCG = new CanvasGroup[0];
    private Image[] menuRowBars = new Image[0];
    // 決定閃光用の白オーバーレイ(バナーと同形の平行四辺形・通常 alpha0)。
    // 焼き込みスプライトは頂点色で白側へ飛ばせないため、色 lerp ではなく
    // オーバーレイの減衰で閃光を出す。
    private ParallelogramGraphic[] menuRowFlash = new ParallelogramGraphic[0];
    private Sprite menuButtonSprite;
    // 1P/2P トグル専用の焼き込み本体。menuButtonSprite(660x160)をトグル(176x78)へ
    // 流用すると非等倍ストレッチで 19° 斜辺が見かけ約 10.7° に歪むため、トグルの
    // 表示アスペクトに一致した解像度で別に焼く(2026-07-14 傾き一致対応)。
    private Sprite pcToggleSprite;
    private float[] menuItemSel = new float[0];
    private float[] menuRowY = new float[0];
    private int menuIndex;

    // 引き継ぎコードの導線(設計未確定のため無効化)。false で「引き継ぎ」行を
    // 灰色(disabled 見た目)+選択不可にする。画面(BuildTransferPanel)は残すので、
    // true に戻すだけで導線が復活する(2026-07-13 指摘)。
    private const bool TransferEnabled = false;
    // 引き継ぎ行の表示可否(2026-07-14 指摘「一旦引き継ぎは非表示に」)。false で行ごと消す
    // (灰色無効化からさらに進めて非表示)。true に戻すだけで復活(その場合 TransferEnabled で有効/灰色を選ぶ)。
    private const bool ShowTransferRow = false;
    private bool[] menuRowEnabled = new bool[0];
    // 無効行の見た目(灰色・沈む)。有効行は白/MenuTextBase。
    private static readonly Color MenuDisabledText = new Color(0.45f, 0.48f, 0.55f, 1f);
    private static readonly Color MenuDisabledBar = new Color(0.34f, 0.36f, 0.42f, 1f);

    // Cloned DefficultyBar "White" slash brackets; glide to the selected row.
    private RectTransform menuWhite;
    private float menuWhiteY;

    private GameObject transferRoot;
    private CanvasGroup transferCG;
    // 引き継ぎ画面の背景ぼかし(難易度オーバーレイと同構成: 完成フレームの
    // スナップショット+暗スクリム)。メニュー・ロゴを退場させず背景に残す。
    private RawImage transferBackdrop;
    private RenderTexture transferBlurRT;
    private Coroutine transferCaptureRoutine;
    private Coroutine transferCloseRoutine; // 閉じるときのフェードアウト(第34便)
    private TMP_Text transferCodeText;          // 履歴なしメッセージ(コードはブロック表示)
    private TMP_InputField transferInput;
    private TMP_Text transferMessageText;
    private Image applyButton;
    private TMP_Text applyLabelText;
    private bool transferOpen;
    // oracle レビュー反映(第27便): コード4ブロック表示・入力フォーカス枠・
    // CTRL+C コピー・斜めバナーヘッダー/ボタン・操作ヒントバー。
    private GameObject transferCodeBlocksRoot;
    private TMP_Text[] transferCodeBlockTexts = new TMP_Text[0];
    private TMP_Text[] transferCodeBlockShadows = new TMP_Text[0];
    private TMP_Text[] transferHyphenTexts = new TMP_Text[0]; // v2(4文字)コードでは非表示
    private Image transferInputBorder;
    private bool transferInputError;
    // ビルド時に空振りした光学中央補正の再適用フラグ(表示後の初回に確定)。
    private bool menuInkCentered;
    private bool transferInkCentered;

    private static readonly Color InputBorderIdle = new Color(0.118f, 0.812f, 0.878f, 0.65f);
    // 適用ボタンは入力が空だと沈み、入力があると点灯する(oracle 指摘: 常時
    // 最明度のボタンが主役のコード表示より目立っていた)。点灯時も主役の
    // コードチップよりわずかに彩度を抑える。
    private static readonly Color ApplyIdle = new Color(0.035f, 0.176f, 0.275f, 0.8f);
    private static readonly Color ApplyReady = new Color(0.086f, 0.561f, 0.784f, 0.92f);
    private static readonly Color ApplyLabelIdle = new Color(0.624f, 0.722f, 0.761f, 0.6f);
    private static readonly Color ApplyLabelReady = new Color(1f, 1f, 1f, 0.95f);

    // ---- 1P/2P 人数トグル(2P その1) --------------------------------------
    // 3 行メニュー(スタート/設定/引き継ぎ)は固定レイアウトのため触らず、ロゴと
    // メニューの間に独立した「1P | 2P」トグルを置く。既定 1P。P1 の ←/→ で切替。
    // 既存の平行四辺形ボタン様式(menuButtonSprite・19°スラッシュ言語)を流用する。
    private RectTransform playerCountRoot;
    private RectTransform titleControlGuideRoot;

    private readonly Image[] pcSegBars = new Image[2];
    private readonly TMP_Text[] pcSegLabels = new TMP_Text[2];
    private bool pcTwoPlayer = false;
    private static readonly Color PcSelText = Color.white;
    private static readonly Color PcSelBar = Color.white;
    private static readonly Color PcDimText = new Color(0.5f, 0.55f, 0.63f, 1f);
    private static readonly Color PcDimBar = new Color(0.38f, 0.42f, 0.5f, 0.9f);
    public bool TwoPlayerSelected => pcTwoPlayer;

    public int MenuIndex => menuIndex;
    public TitleMenuAction CurrentAction => (TitleMenuAction)menuIndex;
    public bool IsTransferOpen => transferOpen;
    public bool IsTransferInputFocused => transferInput != null && transferInput.isFocused;

    public void Init()
    {
        animTime = 0f;
        beatTimer = 0f;
        beatPulse = 0f;
        returnAnimating = false;
        titleBgmSource = null;
        StartTitleBgm();
        if (returnBackdrop != null) returnBackdrop.gameObject.SetActive(false);
        group = GetComponent<CanvasGroup>();
        Transform prompt = transform.Find("Prompt");
        if (prompt != null) promptText = prompt.GetComponent<TMP_Text>();
        Transform logo = transform.Find("Logo");
        if (logo != null)
        {
            logoRect = logo.GetComponent<RectTransform>();
            // Lift the logo above its scene-authored spot (float / beat pulse are
            // applied on top of this raised base position).
            logoBaseY = logoRect.anchoredPosition.y + LogoRaiseOffset;
            logoRect.anchoredPosition = new Vector2(logoRect.anchoredPosition.x, logoBaseY);
        }

        Transform shapesRoot = transform.Find("Shapes");
        if (shapesRoot != null)
        {
            shapes = new ShapeAnim[shapesRoot.childCount];
            shapeGraphics = new Graphic[shapesRoot.childCount];
            shapeBaseAlphas = new float[shapesRoot.childCount];
            for (int i = 0; i < shapesRoot.childCount; i++)
            {
                RectTransform rect = shapesRoot.GetChild(i) as RectTransform;
                shapeGraphics[i] = rect.GetComponent<Graphic>();
                shapeBaseAlphas[i] = shapeGraphics[i] != null ? shapeGraphics[i].color.a : 1f;
                shapes[i] = new ShapeAnim
                {
                    rect = rect,
                    basePos = rect.anchoredPosition,
                    phase = i * 1.7f,
                    speedX = 0.22f + 0.07f * (i % 3),
                    speedY = 0.17f + 0.06f * ((i + 1) % 4),
                    ampX = 70f + 30f * (i % 3),
                    ampY = 50f + 25f * ((i + 2) % 3),
                    rotSpeed = (i % 2 == 0 ? 1f : -1f) * (4f + 3f * (i % 3)),
                };
            }
        }

        EnsureUiBuilt();

        group.alpha = 1f;
        transform.localScale = Vector3.one;
        dismissed = false;
        gameObject.SetActive(true);
    }

    private void EnsureUiBuilt()
    {
        if (uiFont == null)
        {
            uiFont = promptText != null ? promptText.font : TMP_Settings.defaultFontAsset;
        }
        if (menuRoot == null) BuildMenu();
        if (transferRoot == null) BuildTransferPanel();

        menuIndex = 0;
        transferOpen = false;
        for (int i = 0; i < menuItemSel.Length; i++)
        {
            menuItemSel[i] = i == 0 ? 1f : 0f;
            ApplyMenuRowState(i, menuItemSel[i]);
        }
        if (menuWhite != null && menuRowY.Length > 0)
        {
            menuWhiteY = menuRowY[0];
            menuWhite.anchoredPosition = new Vector2(0f, menuWhiteY);
        }
        if (transferRoot != null) transferRoot.SetActive(false);
        if (menuRoot != null) menuRoot.gameObject.SetActive(true);

        // The scene-authored "PRESS ANY BUTTON" prompt is replaced by the menu.
        if (promptText != null)
        {
            promptText.gameObject.SetActive(false);
            promptText = null;
        }
    }

    // Returning from a quit-play scene reload: the title rushes toward the
    // viewer, overshoots slightly, then settles as the pixel cover clears.
    // (PixelTransition drives this; the title-options close path does not.)
    public void PrepareReturnEntrance()
    {
        EnsureReturnBackdrop();
        returnBackdrop.gameObject.SetActive(true);
        int titleSiblingIndex = transform.GetSiblingIndex();
        if (returnBackdrop.transform.GetSiblingIndex() > titleSiblingIndex)
        {
            returnBackdrop.transform.SetSiblingIndex(titleSiblingIndex);
        }
        returnAnimating = true;
        group.alpha = 1f;
        transform.localScale = Vector3.one * 0.78f;
        for (int i = 0; i < shapes.Length; i++)
        {
            if (shapes[i].rect != null) shapes[i].rect.anchoredPosition = shapes[i].basePos;
        }
        if (logoRect != null)
        {
            logoRect.localScale = Vector3.one;
            logoRect.anchoredPosition = new Vector2(logoRect.anchoredPosition.x, logoBaseY);
        }
        if (promptText != null) promptText.alpha = 0f;
    }

    public async void PlayReturnEntrance()
    {
        if (!returnAnimating) PrepareReturnEntrance();
        const float delay = 0.01f;
        float wait = 0f;
        while (wait < delay)
        {
            wait += Time.unscaledDeltaTime;
            await Task.Yield();
            if (this == null) return;
        }

        const float duration = 0.30f;
        float time = 0f;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(time / duration);
            float q = p - 1f;
            float easeOutBack = 1f + 2.2f * q * q * q + 1.2f * q * q;
            transform.localScale = Vector3.one * Mathf.LerpUnclamped(0.78f, 1f, easeOutBack);
            if (promptText != null) promptText.alpha = Mathf.Clamp01((p - 0.45f) / 0.4f);
            await Task.Yield();
            if (this == null || group == null) return;
        }
        if (logoRect != null)
        {
            logoRect.localScale = Vector3.one;
            logoRect.anchoredPosition = new Vector2(logoRect.anchoredPosition.x, logoBaseY);
        }
        transform.localScale = Vector3.one;
        group.alpha = 1f;
        animTime = 0f;
        beatTimer = 0f;
        beatPulse = 0f;
        returnAnimating = false;
        if (returnBackdrop != null) returnBackdrop.gameObject.SetActive(false);
    }

    private void EnsureReturnBackdrop()
    {
        if (returnBackdrop != null) return;

        Transform parent = transform.parent;
        Transform existing = parent != null ? parent.Find("TitleReturnBackdrop") : null;
        if (existing != null)
        {
            returnBackdrop = existing.GetComponent<Image>();
            if (returnBackdrop != null) return;
        }

        GameObject backdrop = new GameObject(
            "TitleReturnBackdrop",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform rect = backdrop.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;

        returnBackdrop = backdrop.GetComponent<Image>();
        returnBackdrop.color = new Color(0.018f, 0.02f, 0.035f, 1f);
        returnBackdrop.raycastTarget = false;
    }

    // タイトル BGM(Discotheque)を AManager 常駐 BGMSource でループ再生する。
    // 戻り値の AudioSource を保持し、UpdateTitle でロゴの拍を音に同期させる。
    // クリップは Resources/BGM/title_discotheque(1:50 でなく本編の頭 2.763s から
    // ダウンビート単位でトリム、末尾フェードは除去済みで自然にループ)。
    private async void StartTitleBgm(bool fadeIn = false)
    {
        AudioManager am = GManager.Control != null ? GManager.Control.AManager : null;
        if (am == null) return;
        AudioClip clip = Resources.Load<AudioClip>("BGM/title_discotheque");
        if (clip == null)
        {
            Debug.LogWarning("Title BGM clip not found: Resources/BGM/title_discotheque");
            return;
        }
        // fadeIn 時は音量 0 で再生開始し、直後に FadeInBGM で立ち上げる。
        AudioSource src = await am.PlayLoopingBGM(clip, fadeIn ? 0f : TitleBgmVolume);
        // 復帰などで await 中に破棄された場合の保険。
        if (this != null) titleBgmSource = src;
        if (fadeIn && src != null) am.FadeInBGM(TitleBgmVolume, TitleBgmFadeIn);
    }

    // リザルトから選択画面へ戻ったときに、共有 BGMSource で止まっているタイトル
    // BGM(Discotheque)を静かに再開する(2026-07-13 指摘: リザルト後の選択画面が
    // 無音になる問題への対策)。TitleManager の GameObject が非表示でも、再生自体は
    // 常駐 AManager 上で走るため呼び出せる。
    public void EnsureTitleBgm()
    {
        StartTitleBgm(fadeIn: true);
    }

    // Idle animation: prompt blinks, logo floats and bounces on the beat,
    // background shapes drift, spin and flash slightly in time with the BPM.
    public void UpdateTitle(float dt)
    {
        if (dismissed || returnAnimating) return;

        // メニューの光学中央補正はビルド時(シーンロード中)には TMP が文字を
        // 生成できず空振りすることがある(第30便で実測 8〜11px の上ずれ)。
        // 表示中の最初のフレームで測定できるようになってから確定させる。
        if (!menuInkCentered && menuItems != null)
        {
            bool all = true;
            foreach (TMP_Text item in menuItems)
            {
                if (item != null) all &= TmpAlign.CenterInkVertically(item);
            }
            menuInkCentered = all;
        }

        animTime += dt;

        // ビートパルス(ロゴの振動・図形フラッシュ): Discotheque が再生中なら
        // 再生位置から拍位相を取って音にロックする(clip の t=0=実ダウンビート)。
        // BGM 未再生(ロード中/失敗)のときだけ従来の自走タイマーで代替する。
        float beatInterval = 60f / Mathf.Max(1f, bpm);
        if (titleBgmSource != null && titleBgmSource.isPlaying && titleBgmSource.clip != null)
        {
            float beats = titleBgmSource.time / beatInterval;
            float frac = beats - Mathf.Floor(beats);         // 0=拍頭 → 1=次拍直前
            beatPulse = Mathf.Max(0f, 1f - frac / BeatPulseDecayFrac);
        }
        else
        {
            beatTimer += dt;
            if (beatTimer >= beatInterval)
            {
                beatTimer -= beatInterval;
                beatPulse = 1f;
            }
            beatPulse = Mathf.Max(0f, beatPulse - dt * 5f);
        }

        if (promptText != null)
        {
            promptText.alpha = 0.35f + 0.55f * (0.5f + 0.5f * Mathf.Sin(animTime * 3.5f));
        }
        if (logoRect != null)
        {
            logoRect.anchoredPosition = new Vector2(logoRect.anchoredPosition.x, logoBaseY + Mathf.Sin(animTime * 1.2f) * 10f);
            logoRect.localScale = Vector3.one * (1f + 0.035f * beatPulse);
        }
        for (int i = 0; i < shapeGraphics.Length; i++)
        {
            if (shapeGraphics[i] == null) continue;
            Color c = shapeGraphics[i].color;
            c.a = Mathf.Min(1f, shapeBaseAlphas[i] * (1f + 0.65f * beatPulse));
            shapeGraphics[i].color = c;
        }
        for (int i = 0; i < shapes.Length; i++)
        {
            ShapeAnim s = shapes[i];
            if (s.rect == null) continue;
            s.rect.anchoredPosition = s.basePos + new Vector2(
                (Mathf.Sin(animTime * s.speedX + s.phase) - Mathf.Sin(s.phase)) * s.ampX,
                (Mathf.Cos(animTime * s.speedY + s.phase * 1.3f) - Mathf.Cos(s.phase * 1.3f)) * s.ampY);
            s.rect.Rotate(0f, 0f, s.rotSpeed * dt);
        }
    }

    // Dive "through" the title when the player presses the button: the camera
    // rushes in (cubic ease-in zoom) and the screen blows past the viewer,
    // fading only near the end so the acceleration reads clearly.
    public async void Dismiss()
    {
        if (dismissed) return;
        dismissed = true;
        const float duration = 0.38f;
        float d = duration;
        while (d > 0f)
        {
            d -= Time.deltaTime;
            float t = 1f - Mathf.Clamp01(d / duration);
            transform.localScale = Vector3.one * (1f + 2.2f * t * t * t);
            float fade = Mathf.Clamp01((t - 0.4f) / 0.45f);
            group.alpha = 1f - fade * fade;
            await Task.Yield();
            if (this == null || group == null) return;
        }
        group.alpha = 0f;
        gameObject.SetActive(false);
    }

    // スタート決定の遷移演出: 選択バナーが白フラッシュ+小ポップ→行が右へ
    // 加速して飛び去り(選択行が先頭)、ロゴは上へ抜け、背景図形は加速する。
    // タイトルの背景は StartExitCoverDelay 経過後にステージ選択が重なって
    // くるまで残し、終盤で全体をフェードして交差させる(ハードカット防止)。
    public async void PlayStartExit()
    {
        if (dismissed) return;
        dismissed = true;

        const float flashDur = 0.175f;
        const float rowDur = 0.325f;
        const float slideDistance = 1500f;
        const float logoDelay = 0.125f;
        const float logoDur = 0.475f;
        const float fadeStart = 0.475f;

        int selected = Mathf.Clamp(menuIndex, 0, menuItemRects.Length > 0 ? menuItemRects.Length - 1 : 0);
        beatPulse = 1f; // 決定と同時に図形をひと光りさせる

        float time = 0f;
        while (time < StartExitTotal)
        {
            float dt = Time.deltaTime;
            time += dt;

            // 背景図形は加速しながら流れ続ける(dismissed 中は UpdateTitle が
            // 止まるので、同じ式をここで加速倍率付きで駆動する)。
            float speedMul = Mathf.Lerp(1f, 6f, Mathf.Clamp01(time / StartExitTotal));
            animTime += dt * speedMul;
            beatPulse = Mathf.Max(0f, beatPulse - dt * 5f);
            for (int i = 0; i < shapeGraphics.Length; i++)
            {
                if (shapeGraphics[i] == null) continue;
                Color c = shapeGraphics[i].color;
                c.a = Mathf.Min(1f, shapeBaseAlphas[i] * (1f + 0.65f * beatPulse));
                shapeGraphics[i].color = c;
            }
            for (int i = 0; i < shapes.Length; i++)
            {
                ShapeAnim s = shapes[i];
                if (s.rect == null) continue;
                s.rect.anchoredPosition = s.basePos + new Vector2(
                    (Mathf.Sin(animTime * s.speedX + s.phase) - Mathf.Sin(s.phase)) * s.ampX,
                    (Mathf.Cos(animTime * s.speedY + s.phase * 1.3f) - Mathf.Cos(s.phase * 1.3f)) * s.ampY);
                s.rect.Rotate(0f, 0f, s.rotSpeed * dt * speedMul);
            }

            // 選択バナーのフラッシュ(白オーバーレイ減衰)+小ポップ(1→1.06→1)。
            // 文字は白地に飛ばないよう反転(ネイビー→白)させ、決定の一拍を
            // 読めるまま見せる。ピーク0.5は旧「バナー青→白50%」相当。
            float flashP = Mathf.Clamp01(time / flashDur);
            if (selected < menuRowFlash.Length && menuRowFlash[selected] != null)
            {
                menuRowFlash[selected].color = new Color(1f, 1f, 1f, 0.5f * (1f - flashP * flashP));
            }
            if (selected < menuItems.Length && menuItems[selected] != null)
            {
                menuItems[selected].color = Color.Lerp(Navy, Color.white, flashP * flashP);
            }

            // 行スライドアウト: ease-in cubic で緩→急。選択行が先頭で飛び出す。
            for (int i = 0; i < menuItemRects.Length; i++)
            {
                if (menuItemRects[i] == null) continue;
                float delay = i == selected ? 0.125f : 0.2125f + 0.0625f * i;
                float p = Mathf.Clamp01((time - delay) / rowDur);
                float x = p * p * p * slideDistance;
                menuItemRects[i].anchoredPosition = new Vector2(x, menuRowY[i]);
                if (i == selected)
                {
                    float pop = Mathf.Sin(flashP * Mathf.PI) * 0.06f;
                    menuItemRects[i].localScale = Vector3.one * ((0.8f + 0.2f * menuItemSel[i]) + pop);
                    // 白ブラケットは選択行と一体で飛ぶ。
                    if (menuWhite != null) menuWhite.anchoredPosition = new Vector2(x, menuWhiteY);
                }
            }

            // ロゴは上へ加速して画面外に抜ける。
            if (logoRect != null)
            {
                float lp = Mathf.Clamp01((time - logoDelay) / logoDur);
                logoRect.anchoredPosition = new Vector2(
                    logoRect.anchoredPosition.x, logoBaseY + lp * lp * lp * 520f);
            }

            // 覆われ始めてから全体をフェード(選択画面側のフェードインと交差)。
            float fade = Mathf.Clamp01((time - fadeStart) / (StartExitTotal - fadeStart));
            group.alpha = 1f - fade * fade;

            await Task.Yield();
            if (this == null || group == null) return;
        }

        group.alpha = 0f;
        gameObject.SetActive(false);
        // 非表示中に退場前の配置へ戻し、次回 Init(再表示)を無傷にする。
        for (int i = 0; i < menuItemRects.Length; i++)
        {
            if (menuItemRects[i] == null) continue;
            menuItemRects[i].anchoredPosition = new Vector2(0f, menuRowY[i]);
            ApplyMenuRowState(i, menuItemSel[i]);
        }
        if (selected < menuRowFlash.Length && menuRowFlash[selected] != null)
        {
            menuRowFlash[selected].color = new Color(1f, 1f, 1f, 0f);
        }
        if (menuWhite != null) menuWhite.anchoredPosition = new Vector2(0f, menuWhiteY);
        if (logoRect != null)
        {
            logoRect.anchoredPosition = new Vector2(logoRect.anchoredPosition.x, logoBaseY);
        }
    }

    // ---- Menu -------------------------------------------------------------

    public void ShowMenu()
    {
        if (menuRoot != null) menuRoot.gameObject.SetActive(true);
        SetPlayerCountToggleVisible(true);
        if (titleControlGuideRoot != null) titleControlGuideRoot.gameObject.SetActive(true);

    }

    public void HideMenu()
    {
        if (menuRoot != null) menuRoot.gameObject.SetActive(false);
        SetPlayerCountToggleVisible(false);
        if (titleControlGuideRoot != null) titleControlGuideRoot.gameObject.SetActive(false);

    }

    // Navigate + animate the vertical menu. Selection mirrors DefficultyBox
    // exactly (alpha 0.4→1, scale 0.8→1, text toward white) and the cloned white
    // slash brackets glide to the selected row like DefficultyBar.Tick.
    public void UpdateMenu(float dt, bool up, bool down)
    {
        if (transferOpen || menuItems.Length == 0) return;

        // 無効行(引き継ぎ等)は飛ばして、その方向にある最も近い有効行へ移す。
        if (up) menuIndex = StepMenuIndex(menuIndex - 1, -1);
        else if (down) menuIndex = StepMenuIndex(menuIndex + 1, +1);

        float follow = 1f - Mathf.Exp(-14f * dt);
        for (int i = 0; i < menuItems.Length; i++)
        {
            float target = i == menuIndex ? 1f : 0f;
            menuItemSel[i] = Mathf.Abs(target - menuItemSel[i]) < 0.001f
                ? target
                : Mathf.Lerp(menuItemSel[i], target, follow);
            ApplyMenuRowState(i, menuItemSel[i]);
        }

        if (menuWhite != null && menuRowY.Length > menuIndex)
        {
            float targetY = menuRowY[menuIndex];
            menuWhiteY = Mathf.Abs(targetY - menuWhiteY) < 0.5f
                ? targetY
                : Mathf.Lerp(menuWhiteY, targetY, 1f - Mathf.Exp(-16f * dt));
            menuWhite.anchoredPosition = new Vector2(0f, menuWhiteY);
        }
    }

    // Same visual state math as DefficultyBox.SetPosition.
    private void ApplyMenuRowState(int i, float progress)
    {
        // 無効行は選択に反応させず、灰色(disabled 見た目)で固定する。
        if (!IsRowEnabled(i))
        {
            if (menuRowCG[i] != null) menuRowCG[i].alpha = 0.32f;
            if (menuItemRects[i] != null) menuItemRects[i].localScale = Vector3.one * 0.8f;
            if (menuItems[i] != null) menuItems[i].color = MenuDisabledText;
            if (menuRowBars[i] != null) menuRowBars[i].color = MenuDisabledBar;
            return;
        }
        if (menuRowBars[i] != null) menuRowBars[i].color = Color.white; // 有効行は焼き込みそのまま
        if (menuRowCG[i] != null) menuRowCG[i].alpha = 0.4f + 0.6f * progress;
        if (menuItemRects[i] != null) menuItemRects[i].localScale = Vector3.one * (0.8f + 0.2f * progress);
        if (menuItems[i] != null) menuItems[i].color = Color.Lerp(MenuTextBase, Color.white, progress);
    }

    private bool IsRowEnabled(int i)
        => menuRowEnabled == null || i < 0 || i >= menuRowEnabled.Length || menuRowEnabled[i];

    // from から dir 方向へ最初の有効行を探す。無ければ現在位置を維持(移動しない)。
    private int StepMenuIndex(int from, int dir)
    {
        for (int i = from; i >= 0 && i < menuItems.Length; i += dir)
            if (IsRowEnabled(i)) return i;
        return menuIndex;
    }

    private void BuildMenu()
    {
        GameObject rootObj = new GameObject("Menu", typeof(RectTransform));
        rootObj.layer = gameObject.layer;
        menuRoot = (RectTransform)rootObj.transform;
        menuRoot.SetParent(transform, false);
        menuRoot.anchorMin = menuRoot.anchorMax = new Vector2(0.5f, 0.5f);
        menuRoot.anchoredPosition = Vector2.zero;
        menuRoot.sizeDelta = new Vector2(700f, 500f);

        // 引き継ぎ行は既定で非表示(ShowTransferRow=false)。行ごと作らず 2 行に。
        string[] labels = ShowTransferRow
            ? new[] { "スタート", "設定", "引き継ぎ" }
            : new[] { "スタート", "設定" };
        // 難易度ボタン規格(660x160)。行間は難易度と同じ DiffRowSpacing。
        // n 行を rowCenter 中心に等間隔で並べる(3 行時は従来の {center+gap, center, center-gap} と一致)。
        float rowGap = UiButtonStyle.DiffRowSpacing;
        // 2 行時はロゴ下へ余裕をもって配置(トグルをスタート旧位置-106へ・2ボタンを下段へ)。
        // 3 行復活時は従来の -278 中心(3 行が画面内に収まる)。
        float rowCenter = ShowTransferRow ? -278f : -364f;
        float[] rowY = new float[labels.Length];
        for (int i = 0; i < labels.Length; i++)
            rowY[i] = rowCenter + (labels.Length - 1) * rowGap * 0.5f - i * rowGap;

        // The real difficulty-select column lives next to the title in the same
        // canvas; clone its row (banner + label) and white brackets so the title
        // menu is literally the same parts, not a lookalike.
        Transform diffSrc = transform.parent != null ? transform.parent.Find("StageBoxParent/DefficultyBar") : null;
        Transform rowSrc = diffSrc != null ? diffSrc.Find("List/Normal") : null;
        Transform whiteSrc = diffSrc != null ? diffSrc.Find("White") : null;

        menuItems = new TMP_Text[labels.Length];
        menuItemRects = new RectTransform[labels.Length];
        menuRowCG = new CanvasGroup[labels.Length];
        menuRowBars = new Image[labels.Length];
        menuRowFlash = new ParallelogramGraphic[labels.Length];
        menuItemSel = new float[labels.Length];
        menuRowY = rowY;

        // 各行の有効/無効。既定は全て有効。引き継ぎ行だけ TransferEnabled で切替。
        menuRowEnabled = new bool[labels.Length];
        for (int i = 0; i < labels.Length; i++) menuRowEnabled[i] = true;
        // 引き継ぎ行を表示する場合のみ有効/無効を設定(非表示時は行自体が無いので配列外参照を避ける)。
        if (ShowTransferRow) menuRowEnabled[(int)TitleMenuAction.Transfer] = TransferEnabled;

        // リザルト様式のボタン本体を難易度規格(660x160)で焼く(2026-07-13 統一)。
        if (menuButtonSprite == null)
            menuButtonSprite = UiButtonStyle.CreateBodySprite(
                (int)UiButtonStyle.DiffButtonW, (int)UiButtonStyle.DiffButtonH, null, null, "TitleMenuButton");

        for (int i = 0; i < labels.Length; i++)
        {
            RectTransform row;
            TMP_Text label;
            if (rowSrc != null)
            {
                GameObject rowObj = Instantiate(rowSrc.gameObject, menuRoot);
                rowObj.name = "Item" + i;
                rowObj.SetActive(true);
                row = (RectTransform)rowObj.transform;
                Image bar = rowObj.transform.Find("StageBar")?.GetComponent<Image>();
                if (bar != null)
                {
                    bar.sprite = menuButtonSprite;
                    bar.type = Image.Type.Simple;
                    bar.color = Color.white;
                    // クローン元(シーンの DefficultyBar)は起動順の都合で先に
                    // Init 済みで StageBar が拡大寸法に上書きされている。タイトル行も
                    // 難易度規格(660x160)へ揃えるため明示的に設定する(2026-07-13)。
                    bar.rectTransform.sizeDelta = new Vector2(UiButtonStyle.DiffButtonW, UiButtonStyle.DiffButtonH);
                    menuRowFlash[i] = CreateRowFlash(bar.rectTransform);
                }
                menuRowBars[i] = bar;
                label = rowObj.transform.Find("StageName")?.GetComponent<TMP_Text>();
            }
            else
            {
                // Degraded fallback (scene layout changed): plain banner + label.
                row = new GameObject("Item" + i, typeof(RectTransform)).GetComponent<RectTransform>();
                row.SetParent(menuRoot, false);
                Vector2 btnSize = new Vector2(UiButtonStyle.DiffButtonW, UiButtonStyle.DiffButtonH);
                menuRowBars[i] = CreatePanel("StageBar", row, Vector2.zero, btnSize, Color.white);
                menuRowBars[i].sprite = menuButtonSprite;
                menuRowFlash[i] = CreateRowFlash(menuRowBars[i].rectTransform);
                label = CreateText("StageName", row, Vector2.zero, btnSize, UiButtonStyle.LabelSizeDifficulty, MenuTextBase, TextAlignmentOptions.Center);
            }

            row.anchorMin = row.anchorMax = new Vector2(0.5f, 0.5f);
            row.pivot = new Vector2(0.5f, 0.5f);
            row.anchoredPosition = new Vector2(0f, rowY[i]);

            CanvasGroup cg = row.GetComponent<CanvasGroup>();
            if (cg == null) cg = row.gameObject.AddComponent<CanvasGroup>();

            // 行付属の灰スラッシュ(クローン元 sprite・角度約18°)はリザルト様式の
            // 細スラッシュ(19°・2.5px・白α0.5)へ差し替え、上下端をボタンの
            // 上下辺(焼き込み枠)に合わせる(2026-07-11 指摘)。x は共通則
            // ThinSlashX でボタン枠に密着させる(2026-07-11 指摘「離れすぎ」)。
            // 行 CanvasGroup の減光には子としてそのまま追従する。
            // RowSlashL/R は Init 済みクローン元から継承した難易度画面幅基準の
            // スラッシュ(位置が合わない)。無効化して下で 583 幅基準を付け直す。
            foreach (string grayName in new[] { "Gray_L", "Gray_R", "RowSlashL", "RowSlashR" })
            {
                Transform gray = row.Find(grayName);
                if (gray != null) gray.gameObject.SetActive(false);
            }
            float rowThinX = UiButtonStyle.ThinSlashX(UiButtonStyle.DiffButtonW);
            UiButtonStyle.AddSlash(row, "RowSlashL", new Color(1f, 1f, 1f, 0.5f),
                -rowThinX, 2.5f, UiButtonStyle.SlashHeight(UiButtonStyle.DiffButtonH));
            UiButtonStyle.AddSlash(row, "RowSlashR", new Color(1f, 1f, 1f, 0.5f),
                rowThinX, 2.5f, UiButtonStyle.SlashHeight(UiButtonStyle.DiffButtonH));

            if (label != null)
            {
                label.text = labels[i];
                // ラベルサイズは難易度ボタンと同じ規格へ統一(2026-07-13)。
                label.fontSize = UiButtonStyle.LabelSizeDifficulty;
                // Japanese labels ride high under Middle alignment (Latin UI font
                // + CJK fallback metrics); optically center them in the banner.
                TmpAlign.CenterInkVertically(label);
            }

            menuItems[i] = label;
            menuItemRects[i] = row;
            menuRowCG[i] = cg;
            menuItemSel[i] = i == 0 ? 1f : 0f;
            ApplyMenuRowState(i, menuItemSel[i]);
        }

        if (whiteSrc != null)
        {
            GameObject whiteObj = Instantiate(whiteSrc.gameObject, menuRoot);
            whiteObj.name = "White";
            whiteObj.SetActive(true);
            menuWhite = (RectTransform)whiteObj.transform;
            CanvasGroup whiteCG = whiteObj.GetComponent<CanvasGroup>();
            if (whiteCG != null) whiteCG.alpha = 1f;
            // クローン元の White は難易度画面の Init 済みで、難易度ボタン幅基準の
            // MarkerSlashL/R が付いている(起動順: SSManager.Init→ここ)。継承分を
            // 残すとマーカーが二重に見える(2026-07-11 実フレームで確認)ため無効化し、
            // タイトル幅(583)基準の自前スラッシュだけを使う。
            foreach (string inheritedName in new[] { "MarkerSlashL", "MarkerSlashR" })
            {
                Transform inherited = menuWhite.Find(inheritedName);
                if (inherited != null) inherited.gameObject.SetActive(false);
            }
            // 選択マーカーの白スラッシュ(クローン元 sprite・角度約21°)を
            // リザルト様式の太スラッシュ(19°・11px)へ差し替え、上下端を
            // ボタンの上下辺(焼き込み枠)に合わせる(2026-07-11 指摘)。
            // x は共通則 ThickSlashX でボタン枠のすぐ外に密着させる
            // (2026-07-11 指摘「離れすぎ」。クローン元のバナー外側配置を廃止)。
            foreach (string slashName in new[] { "White_L", "White_R" })
            {
                RectTransform slash = menuWhite.Find(slashName) as RectTransform;
                if (slash == null) continue;
                float slashX = Mathf.Sign(slash.anchoredPosition.x)
                    * UiButtonStyle.ThickSlashX(UiButtonStyle.DiffButtonW);
                slash.gameObject.SetActive(false);
                UiButtonStyle.AddSlash(menuWhite, slashName + "19", Color.white,
                    slashX, 11f, UiButtonStyle.SlashHeight(UiButtonStyle.DiffButtonH));
            }
            // Shine 用マスクの mask graphic は旧様式 SimpleBar(矩形枠)で、
            // 選択行に「横方向の白い線」(矩形の上下辺)を描いてしまう。
            // 描画を止め、マスク形状も焼き込みバナーの平行四辺形に合わせる
            // (2026-07-11 指摘「横方向の白い線もリザルト様式に統一」)。
            Transform shineMask = menuWhite.Find("ShineMask");
            if (shineMask != null)
            {
                Image maskImage = shineMask.GetComponent<Image>();
                if (maskImage != null) maskImage.sprite = menuButtonSprite;
                Mask mask = shineMask.GetComponent<Mask>();
                if (mask != null) mask.showMaskGraphic = false;
            }
            menuWhite.SetAsLastSibling();
            menuWhiteY = rowY[0];
            menuWhite.anchoredPosition = new Vector2(0f, menuWhiteY);
        }

        BuildPlayerCountToggle(rowY.Length > 0 ? rowY[0] : rowCenter + rowGap);
        BuildTitleControlGuide();

    }

    // ロゴとメニュー最上段の間に「1P | 2P」トグルを組む。topRowY はメニュー最上段の y。
    // その少し上(+124)へ 2 セグメントを横並びで置く。焼き込みボタン様式を縮小流用。
    private void BuildPlayerCountToggle(float topRowY)
    {
        GameObject rootObj = new GameObject("PlayerCountToggle", typeof(RectTransform));
        rootObj.layer = gameObject.layer;
        playerCountRoot = (RectTransform)rootObj.transform;
        playerCountRoot.SetParent(transform, false);
        playerCountRoot.anchorMin = playerCountRoot.anchorMax = new Vector2(0.5f, 0.5f);
        playerCountRoot.pivot = new Vector2(0.5f, 0.5f);
        // トグルはメニュー最上段の 1 行分(DiffRowSpacing)上へ。ロゴと最上段ボタンの中間で
        // 均等間隔になり、ロゴへの重なりを解消(2026-07-14 指摘「ぐちゃぐちゃ」)。
        playerCountRoot.anchoredPosition = new Vector2(0f, topRowY + UiButtonStyle.DiffRowSpacing);
        playerCountRoot.sizeDelta = new Vector2(520f, 96f);

        const float segW = 176f;
        const float segH = 78f;
        const float segGap = 24f;
        string[] segText = { "1P", "2P" };
        float[] segX = { -(segW + segGap) * 0.5f, (segW + segGap) * 0.5f };

        // トグル本体は segW:segH と同一アスペクト(=整数4倍 704x312)で焼く。表示は
        // 等倍スケールになるので焼き込み 19° 斜辺が歪まず、追加の細スラッシュ(真の
        // 19°)と平行に揃う。従来の menuButtonSprite(660x160)流用は非等倍ストレッチで
        // 見かけ角が約 10.7° に潰れていた(2026-07-14 指摘「1P/2P の傾きが食い違う」)。
        const int pcBakeScale = 4;
        if (pcToggleSprite == null)
            pcToggleSprite = UiButtonStyle.CreateBodySprite(
                (int)segW * pcBakeScale, (int)segH * pcBakeScale, null, null, "PlayerCountToggleButton");

        for (int i = 0; i < 2; i++)
        {
            GameObject segObj = new GameObject("Seg" + i, typeof(RectTransform));
            segObj.layer = gameObject.layer;
            RectTransform seg = (RectTransform)segObj.transform;
            seg.SetParent(playerCountRoot, false);
            seg.anchorMin = seg.anchorMax = new Vector2(0.5f, 0.5f);
            seg.pivot = new Vector2(0.5f, 0.5f);
            seg.anchoredPosition = new Vector2(segX[i], 0f);
            seg.sizeDelta = new Vector2(segW, segH);

            Image bar = segObj.AddComponent<Image>();
            bar.sprite = pcToggleSprite;   // segW:segH 一致で焼いた本体(等倍表示=19°不変)
            bar.type = Image.Type.Simple;
            bar.raycastTarget = false;
            pcSegBars[i] = bar;

            // 焼き込みボタンと同じ 19° 細スラッシュを左右端に添えてメニューと様式統一。
            float thinX = segW * 0.5f - 8f;
            UiButtonStyle.AddSlash(seg, "PcSlashL", new Color(1f, 1f, 1f, 0.5f), -thinX, 2.2f, segH - 16f);
            UiButtonStyle.AddSlash(seg, "PcSlashR", new Color(1f, 1f, 1f, 0.5f), thinX, 2.2f, segH - 16f);

            GameObject lblObj = new GameObject("Label", typeof(RectTransform));
            lblObj.layer = gameObject.layer;
            RectTransform lblRect = (RectTransform)lblObj.transform;
            lblRect.SetParent(seg, false);
            lblRect.anchorMin = Vector2.zero;
            lblRect.anchorMax = Vector2.one;
            lblRect.offsetMin = lblRect.offsetMax = Vector2.zero;
            TMP_Text lbl = lblObj.AddComponent<TextMeshProUGUI>();
            if (uiFont != null) lbl.font = uiFont;
            lbl.text = segText[i];
            lbl.fontSize = 40f;
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.raycastTarget = false;
            pcSegLabels[i] = lbl;
        }

        // 人数選択は 1P / 2P トグル自体で示す。

        ApplyPlayerCountVisual();
    }

    // タイトル右下に、筐体操作を常時確認できる簡潔なガイドを置く。
    private void BuildTitleControlGuide()
    {
        if (titleControlGuideRoot != null) return;

        GameObject rootObj = new GameObject("TitleControlGuide", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(ParallelogramGraphic));
        rootObj.layer = gameObject.layer;
        titleControlGuideRoot = (RectTransform)rootObj.transform;
        titleControlGuideRoot.SetParent(transform, false);
        titleControlGuideRoot.anchorMin = titleControlGuideRoot.anchorMax = new Vector2(1f, 0f);
        titleControlGuideRoot.pivot = new Vector2(1f, 0f);
        titleControlGuideRoot.anchoredPosition = new Vector2(-42f, 34f);
        titleControlGuideRoot.sizeDelta = new Vector2(530f, 68f);

        ParallelogramGraphic panel = rootObj.GetComponent<ParallelogramGraphic>();
        panel.color = new Color(0.012f, 0.03f, 0.075f, 0.86f);
        panel.Slant = 18f;
        panel.SlantRightEdge = true;
        panel.raycastTarget = false;

        AddTitleGuideIcon("Stick", UiIconFactory.StickLeftRight(), new Vector2(-192f, 0f), new Vector2(78f, 44f));
        TMP_Text select = CreateText("SelectLabel", titleControlGuideRoot,
            new Vector2(-112f, -1f), new Vector2(102f, 44f), 25f,
            new Color(0.86f, 0.93f, 1f, 0.96f), TextAlignmentOptions.MidlineLeft);
        select.text = "で選択";

        GameObject divider = new GameObject("Divider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        divider.layer = gameObject.layer;
        RectTransform dividerRect = (RectTransform)divider.transform;
        dividerRect.SetParent(titleControlGuideRoot, false);
        dividerRect.anchorMin = dividerRect.anchorMax = new Vector2(0.5f, 0.5f);
        dividerRect.anchoredPosition = new Vector2(-38f, 0f);
        dividerRect.sizeDelta = new Vector2(2f, 34f);
        Image dividerImage = divider.GetComponent<Image>();
        dividerImage.color = new Color(0.22f, 0.76f, 0.88f, 0.52f);
        dividerImage.raycastTarget = false;

        AddTitleGuideIcon("ConfirmButton", UiIconFactory.Button(), new Vector2(25f, 0f), new Vector2(48f, 48f));
        TMP_Text confirm = CreateText("ConfirmLabel", titleControlGuideRoot,
            new Vector2(96f, -1f), new Vector2(112f, 44f), 25f,
            new Color(0.86f, 0.93f, 1f, 0.96f), TextAlignmentOptions.MidlineLeft);
        confirm.text = "で決定";
    }

    private void AddTitleGuideIcon(string objectName, Sprite sprite, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(titleControlGuideRoot, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.color = new Color(0.56f, 0.87f, 1f, 0.98f);
        image.raycastTarget = false;
        go.AddComponent<ControlIconMotion>().Configure(sprite);
    }

    // トグルの選択状態を見た目に反映(選択セグメント=白/明、非選択=灰/沈む)。
    private void ApplyPlayerCountVisual()
    {
        for (int i = 0; i < 2; i++)
        {
            bool selected = (i == 1) == pcTwoPlayer;
            if (pcSegBars[i] != null) pcSegBars[i].color = selected ? PcSelBar : PcDimBar;
            if (pcSegLabels[i] != null)
            {
                pcSegLabels[i].color = selected ? PcSelText : PcDimText;
                pcSegLabels[i].fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
            }
        }
    }

    // GManager から呼ぶ。人数選択を設定して見た目を更新する。戻り値=変化したか。
    public bool SetTwoPlayer(bool two)
    {
        if (pcTwoPlayer == two) return false;
        pcTwoPlayer = two;
        ApplyPlayerCountVisual();
        return true;
    }

    // メニュー表示/非表示に人数トグルも追従させる。
    public void SetPlayerCountToggleVisible(bool visible)
    {
        if (playerCountRoot != null) playerCountRoot.gameObject.SetActive(visible);
    }

    // 決定閃光用の白オーバーレイ。焼き込みバナーの枠(内側 44/22 マージン)と同じ
    // 平行四辺形で、通常は alpha0。閃光時のみ白く光らせる。バナー寸法に追従させる
    // (難易度規格 660x160 なら 616x138)。
    private static ParallelogramGraphic CreateRowFlash(RectTransform barRect)
    {
        GameObject go = new GameObject("Flash", typeof(RectTransform), typeof(CanvasRenderer), typeof(ParallelogramGraphic));
        go.layer = barRect.gameObject.layer;
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(barRect, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        float flashW = barRect.sizeDelta.x - 44f;
        float flashH = barRect.sizeDelta.y - 22f;
        rect.sizeDelta = new Vector2(flashW, flashH);
        ParallelogramGraphic flash = go.GetComponent<ParallelogramGraphic>();
        flash.Slant = flashH * Mathf.Tan(UiButtonStyle.SlashAngleDeg * Mathf.Deg2Rad);
        flash.SlantRightEdge = true;
        flash.color = new Color(1f, 1f, 1f, 0f);
        flash.raycastTarget = false;
        return flash;
    }

    // ---- Transfer panel ---------------------------------------------------

    public void OpenTransfer()
    {
        // 導線が無効なら開かない(行は選択不可だが念のためガード)。
        if (!TransferEnabled) return;
        if (transferRoot == null) return;
        if (transferCloseRoutine != null) { StopCoroutine(transferCloseRoutine); transferCloseRoutine = null; }
        transferRoot.transform.localScale = Vector3.one; // 閉じるアニメの縮小をリセット
        transferOpen = true;
        transferInputError = false;
        // メニュー・ロゴは退場させない。難易度オーバーレイと同様、完成フレーム
        // (メニュー・ロゴを含む)を撮ってぼかし、その上にパネルを重ねる(第31便)。
        transferRoot.SetActive(true);
        transferRoot.transform.SetAsLastSibling();
        // 撮影フレームはパネルを不可視にして背景(タイトル)だけを撮る。撮影後に
        // ぼかしスナップショットを差し込んで表示する。
        if (transferCG != null) transferCG.alpha = 0f;
        if (transferBackdrop != null) transferBackdrop.gameObject.SetActive(false);
        if (transferCaptureRoutine != null) StopCoroutine(transferCaptureRoutine);
        transferCaptureRoutine = StartCoroutine(CaptureTransferBackdrop());
        // 固定ラベルの光学中央補正はビルド時(非アクティブ)に空振りしている
        // ことがあるため、初回オープン時に測り直す(チップ等の可変テキストは
        // RefreshTransferCode が毎回再適用する)。
        if (!transferInkCentered)
        {
            RectTransform rootRect = (RectTransform)transferRoot.transform;
            bool all = true;
            foreach (string n in new[] { "Heading", "HeadingSub", "CodeLabel", "InputLabel" })
            {
                TMP_Text label = rootRect.Find(n)?.GetComponent<TMP_Text>();
                if (label != null) all &= TmpAlign.CenterInkVertically(label);
            }
            if (applyLabelText != null) all &= TmpAlign.CenterInkVertically(applyLabelText);
            transferInkCentered = all;
        }
        RefreshTransferCode();
        if (transferMessageText != null) transferMessageText.text = string.Empty;
        if (transferInput != null)
        {
            transferInput.text = string.Empty;
            transferInput.ActivateInputField();
        }
    }

    public void CloseTransfer()
    {
        transferOpen = false;
        if (transferInput != null) transferInput.DeactivateInputField();
        if (transferCaptureRoutine != null) { StopCoroutine(transferCaptureRoutine); transferCaptureRoutine = null; }
        // 開くとき(0.14sフェードイン)と対称に、パネル+ぼかし背景をフェードアウト
        // (+わずかに縮小)して閉じる。メニュー・ロゴは隠していないので、フェードの
        // 裏に生きたタイトルが現れて自然に戻る(第34便: 従来は即 SetActive(false)で
        // アニメーション無しだった)。
        if (transferCloseRoutine != null) StopCoroutine(transferCloseRoutine);
        if (transferRoot != null && transferRoot.activeInHierarchy && gameObject.activeInHierarchy)
        {
            transferCloseRoutine = StartCoroutine(CloseTransferRoutine());
        }
        else
        {
            FinishCloseTransfer();
        }
    }

    private IEnumerator CloseTransferRoutine()
    {
        RectTransform rootRect = transferRoot != null ? (RectTransform)transferRoot.transform : null;
        float startAlpha = transferCG != null ? transferCG.alpha : 1f;
        float t = 0f;
        const float dur = 0.14f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / dur);
            if (transferCG != null) transferCG.alpha = startAlpha * (1f - p);
            if (rootRect != null) rootRect.localScale = Vector3.one * Mathf.Lerp(1f, 0.98f, p);
            yield return null;
        }
        transferCloseRoutine = null;
        FinishCloseTransfer();
    }

    private void FinishCloseTransfer()
    {
        if (transferRoot != null)
        {
            transferRoot.transform.localScale = Vector3.one;
            transferRoot.SetActive(false);
        }
        if (transferCG != null) transferCG.alpha = 1f; // 次回オープンは OpenTransfer が 0 から再フェード
        if (transferBackdrop != null) transferBackdrop.texture = null;
        ReleaseBackdropTexture();
        // メニュー・ロゴはそもそも隠していないので再表示は不要(念のため確認)。
        if (menuRoot != null) menuRoot.gameObject.SetActive(true);
        if (logoRect != null) logoRect.gameObject.SetActive(true);
    }

    // 引き継ぎパネルを一瞬透明にして背景(タイトル: メニュー・ロゴ・図形)だけを
    // 撮り、ぼかしマテリアル越しに背景として敷く。撮影はフレーム描画後に行う
    // (入力処理中の同期キャプチャは白バッファを返すことがある)。
    private IEnumerator CaptureTransferBackdrop()
    {
        yield return new WaitForEndOfFrame();
        transferCaptureRoutine = null;
        if (!transferOpen) yield break;
        ReleaseBackdropTexture();
        transferBlurRT = BackdropBlurUtil.CapturePyramidBlur();
        if (transferBackdrop != null)
        {
            transferBackdrop.texture = transferBlurRT;
            transferBackdrop.gameObject.SetActive(true);
        }
        // パネルをふわりと出す(急な表示を防ぐ。難易度オーバーレイと同傾向)。
        float t = 0f;
        while (t < 0.14f)
        {
            t += Time.unscaledDeltaTime;
            if (transferCG != null) transferCG.alpha = Mathf.Clamp01(t / 0.14f);
            yield return null;
        }
        if (transferCG != null) transferCG.alpha = 1f;
    }

    private void ReleaseBackdropTexture()
    {
        BackdropBlurUtil.ReleaseRT(ref transferBlurRT);
    }

    public void ApplyTransfer()
    {
        if (transferInput == null) return;
        if (PlayHistory.TryImportCode(transferInput.text, out string error))
        {
            transferInputError = false;
            if (transferMessageText != null)
            {
                transferMessageText.color = Cyan;
                // 1P/2P は別枠の履歴。取り込み先モードを明示して混同を避ける。
                string modeTag = PlayHistory.TwoPlayerMode ? "2人プレイ" : "1人プレイ";
                transferMessageText.text =
                    $"引き継ぎました[{modeTag}](プレイ {PlayHistory.TotalPlays} 回 / クリア {PlayHistory.TotalClears} 回)";
            }
            RefreshTransferCode();
            transferInput.text = string.Empty;
        }
        else
        {
            transferInputError = true;
            if (transferMessageText != null)
            {
                transferMessageText.color = ErrorRed;
                transferMessageText.text = error;
            }
        }
    }

    // 発行済みコードをクリップボードへ(CTRL+C。入力欄のフォーカスと衝突しない)。
    public bool CopyTransferCode()
    {
        if (!PlayHistory.HasHistory) return false;
        GUIUtility.systemCopyBuffer = PlayHistory.ExportCode();
        if (transferMessageText != null)
        {
            transferMessageText.color = Cyan;
            transferMessageText.text = "コードをコピーしました";
        }
        return true;
    }

    // 毎フレームの引き継ぎ画面装飾: 入力欄の枠色(フォーカス=シアン/エラー=赤)と
    // 適用ボタンの点灯(入力があるときだけ明るくなる)。
    public void TickTransfer()
    {
        if (!transferOpen || transferInputBorder == null || transferInput == null) return;
        // フォーカス枠も最明度にはせず、主役のコードチップ(枠 alpha 0.85)より
        // わずかに抑える(oracle 第29便: 主従の明確化)。
        transferInputBorder.color = transferInputError
            ? ErrorRed
            : (transferInput.isFocused ? new Color(Cyan.r, Cyan.g, Cyan.b, 0.75f) : InputBorderIdle);
        if (applyButton != null)
        {
            bool ready = transferInput.text.Length > 0;
            applyButton.color = ready ? ApplyReady : ApplyIdle;
            if (applyLabelText != null) applyLabelText.color = ready ? ApplyLabelReady : ApplyLabelIdle;
        }
    }

    private void RefreshTransferCode()
    {
        bool has = PlayHistory.HasHistory;
        if (transferCodeBlocksRoot != null) transferCodeBlocksRoot.SetActive(has);
        if (transferCodeText != null)
        {
            transferCodeText.gameObject.SetActive(!has);
            if (!has)
            {
                transferCodeText.text = "まだプレイ履歴がありません";
                TmpAlign.CenterInkVertically(transferCodeText);
            }
        }
        if (!has) return;
        // v2 コードは4文字(ハイフンなし)。第33便: 1文字=1チップの4分割をやめ、
        // 「C4D7」のように1枚の帯に連続表示する(可読フォントは維持)。
        string code = PlayHistory.ExportCode().Replace("-", "");
        if (transferCodeBlockTexts.Length > 0 && transferCodeBlockTexts[0] != null)
        {
            transferCodeBlockTexts[0].text = code;
            TmpAlign.CenterInkVertically(transferCodeBlockTexts[0]);
        }
        if (transferCodeBlockShadows.Length > 0 && transferCodeBlockShadows[0] != null)
        {
            transferCodeBlockShadows[0].text = code;
            TmpAlign.CenterInkVertically(transferCodeBlockShadows[0]);
        }
    }

    // 引き継ぎ画面(第29便: ミニマル再設計)。装飾(バナー/スラッシュ/カード枠/
    // ヒントバー)を取り払い、1枚の暗いパネルの上にタイポグラフィと余白だけで
    // 階層を作る。コードチップも一段小さくして画面の主張を抑える。
    private void BuildTransferPanel()
    {
        GameObject rootObj = new GameObject("TransferPanel", typeof(RectTransform));
        rootObj.layer = gameObject.layer;
        transferRoot = rootObj;
        RectTransform rootRect = (RectTransform)rootObj.transform;
        rootRect.SetParent(transform, false);
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        transferCG = rootObj.AddComponent<CanvasGroup>();

        // 最背面: 完成フレームのぼかしスナップショット(オープン時に差し込む)。
        // メニュー・ロゴを退場させず、その凍結ぼかしを背景として敷く(第31便)。
        transferBackdrop = CreateRawImage("Backdrop", rootRect);
        StretchToParent(transferBackdrop.rectTransform);
        // ぼかしは難易度オーバーレイと同じダウンサンプルピラミッド方式(BackdropBlurUtil)で
        // 作るため、シェーダマテリアルは使わない。既定マテリアルで 1/4 解像度のぼかし RT を
        // バイリニア拡大表示する。
        transferBackdrop.color = new Color(0.55f, 0.62f, 0.72f, 1f); // 難易度オーバーレイと同じ軽い減光
        transferBackdrop.gameObject.SetActive(false);

        // 背景: 薄いスクリム+中央の無枠パネル1枚のみ。パネルはわずかに透けさせ
        // (0.90)、上辺ハイライト+下辺シャドウの各1pxで「ただの黒い板」感を消す
        // (oracle 第29便)。
        // 第34便(oracle bin34): 幅を絞り(940→800)、ヒント行削除に合わせ高さも詰める
        // (600→480)。「黒い板」感を消すため、単色板の上に薄い青の内側レイヤー・
        // 上下の締め・辺ハイライトを重ねる(明るいシアン全周枠はコード帯のネオンと
        // 競合するため使わない)。
        const float panelW = 800f;
        // 第35便: ヒント行削除の名残で下部に空白が残っていたため高さを詰め(480→440)、
        // 内容ブロック(見出し〜メッセージ)を y-12 下げてパネル中央に再配置する。
        const float panelH = 440f;
        const float panelHalfW = panelW * 0.5f;
        const float panelHalfH = panelH * 0.5f;
        Vector2 panelSize = new Vector2(panelW, panelH);
        CreatePanel("Scrim", rootRect, Vector2.zero, new Vector2(4000f, 4000f), new Color(0f, 0.024f, 0.071f, 0.22f));
        // 背面の影板(わずかに右下へずらす。ぼかし無しでも黒板の浮きが和らぐ)。
        CreatePanel("PanelShadow", rootRect, new Vector2(6f, -8f), panelSize, new Color(0f, 0f, 0f, 0.24f));
        CreatePanel("Panel", rootRect, Vector2.zero, panelSize, new Color(0.008f, 0.031f, 0.078f, 0.90f));
        // 下部の締め(内側グラデの代替)。
        CreatePanel("PanelBottomDark", rootRect, new Vector2(0f, -(panelHalfH - 45f)), new Vector2(panelW, 90f), new Color(0f, 0f, 0f, 0.16f));
        // 辺は細い銀の額装枠のみ(統一便2で足した4隅ブラケット・ヘッダー帯・
        // スラッシュ・ダイヤノードの加飾は撤去。コードとコピー導線を主役に戻す)。
        Color edgeSilver = new Color(0.268f, 0.325f, 0.456f);
        CreatePanel("EdgeTop", rootRect, new Vector2(0f, panelHalfH - 1f), new Vector2(panelW, 2f), new Color(edgeSilver.r, edgeSilver.g, edgeSilver.b, 0.80f));
        CreatePanel("EdgeBottom", rootRect, new Vector2(0f, -(panelHalfH - 1f)), new Vector2(panelW, 2f), new Color(edgeSilver.r, edgeSilver.g, edgeSilver.b, 0.60f));
        CreatePanel("EdgeLeft", rootRect, new Vector2(-(panelHalfW - 1f), 0f), new Vector2(2f, panelH), new Color(edgeSilver.r, edgeSilver.g, edgeSilver.b, 0.60f));
        CreatePanel("EdgeRight", rootRect, new Vector2(panelHalfW - 1f, 0f), new Vector2(2f, panelH), new Color(edgeSilver.r, edgeSilver.g, edgeSilver.b, 0.60f));
        // コンテンツ(ラベル・コード帯・入力行)の左右端を揃える基準。
        const float contentHalf = 272f;

        // 見出しはタイポグラフィのみ(帯・スラッシュ・ルビ無し)。白見出し+シアン
        // 英字サブ+短い1本の細線で締める(pre-d4f748c のシンプル様式へ戻す)。
        TMP_Text heading = CreateText("Heading", rootRect, new Vector2(0f, 168f), new Vector2(700f, 52f), 40f, Color.white, TextAlignmentOptions.Center);
        heading.fontStyle = FontStyles.Bold;
        TMP_Text headingSub = CreateText("HeadingSub", rootRect, new Vector2(0f, 138f), new Vector2(700f, 22f), 15f, Cyan, TextAlignmentOptions.Center);
        headingSub.characterSpacing = 8f;
        headingSub.text = "TRANSFER CODE";
        CreatePanel("HeadingRule", rootRect, new Vector2(0f, 114f), new Vector2(240f, 1f), new Color(0.275f, 0.863f, 0.941f, 0.28f));

        // コード表示: ラベル+1枚のネオン帯(第33便: 1文字=1チップの4分割をやめ、
        // 「C4D7」のように連続した1フィールドにまとめる。可読フォントは維持)。
        // 装飾層(外グロー→外枠→本体→ハイライト/影/帯/アクセント)はチップ時代の
        // 見た目を踏襲しつつ、帯1枚に集約する。
        // 第35便: コードチップ幅を入力行の全幅(contentHalf*2=544)に合わせ、チップだけ
        // 狭かったのを解消。ラベルの左端(-contentHalf)もチップ左端に一致する。
        const float bandW = contentHalf * 2f;
        const float bandH = 64f;
        // ラベルはシアン1行(ルビ・英字サブは撤去してシンプルに)。
        TMP_Text codeLabel = CreateText("CodeLabel", rootRect, new Vector2(-contentHalf + 180f, 80f), new Vector2(360f, 30f), 20f, new Color(0.388f, 0.867f, 0.91f, 0.5f), TextAlignmentOptions.Left);
        codeLabel.characterSpacing = 3f;

        transferCodeBlocksRoot = new GameObject("CodeBlocks", typeof(RectTransform));
        transferCodeBlocksRoot.layer = gameObject.layer;
        RectTransform blocksRect = (RectTransform)transferCodeBlocksRoot.transform;
        blocksRect.SetParent(rootRect, false);
        blocksRect.anchorMin = blocksRect.anchorMax = new Vector2(0.5f, 0.5f);
        blocksRect.anchoredPosition = new Vector2(0f, 32f);
        transferCodeBlockTexts = new TMP_Text[1];
        transferCodeBlockShadows = new TMP_Text[1]; // 背面のシアン疑似グロー文字
        transferHyphenTexts = new TMP_Text[0];       // 分割しないのでハイフンは無し
        Vector2 bandSize = new Vector2(bandW, bandH);
        // 第35便: 外グロー箱を廃止し一重枠に(外グロー箱+内側枠が二重枠に見えていた)。
        // 外枠(シアン)。fill を 2px 内側に入れて枠を残す。
        Image border = CreatePanel("Band", blocksRect, Vector2.zero, bandSize, new Color(0.25f, 0.95f, 1f, 0.95f));
        Image fill = CreatePanel("Fill", border.rectTransform, Vector2.zero, Vector2.zero, new Color(0.018f, 0.075f, 0.125f, 0.92f));
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = new Vector2(2f, 2f);
        fill.rectTransform.offsetMax = new Vector2(-2f, -2f);
        // 上辺ハイライト・下辺影で「入力欄」ではなくネオンプレートに見せる。
        CreatePanel("TopHi", border.rectTransform, new Vector2(0f, bandH * 0.5f - 3f), new Vector2(bandW - 24f, 2f), new Color(0.55f, 1f, 1f, 0.55f));
        CreatePanel("BottomSh", border.rectTransform, new Vector2(0f, -(bandH * 0.5f - 2f)), new Vector2(bandW, 4f), new Color(0f, 0.02f, 0.05f, 0.45f));
        // 左端の内側グロー帯。
        CreatePanel("LeftStrip", border.rectTransform, new Vector2(-(bandW * 0.5f - 5f), 0f), new Vector2(3f, bandH - 16f), new Color(0.35f, 0.95f, 1f, 0.35f));
        // (第34便: 右上のマゼンタ斜めアクセントを削除し、帯全体をシアン系に統一)
        // 背面のシアン疑似グロー文字(本体文字の一回り大きいコピー)。
        // 4文字を1帯に並べるので characterSpacing で字間を広げて読みやすくする。
        TMP_Text glowText = CreateText("TextGlow", border.rectTransform, Vector2.zero, bandSize, 46f, new Color(0.15f, 0.95f, 1f, 0.145f), TextAlignmentOptions.Center);
        if (codeFont != null) glowText.font = codeFont;
        glowText.fontStyle = FontStyles.Bold;
        glowText.characterSpacing = 20f;
        glowText.rectTransform.localScale = Vector3.one * 1.05f;
        transferCodeBlockShadows[0] = glowText;
        TMP_Text bt = CreateText("Text", border.rectTransform, Vector2.zero, bandSize, 46f, new Color(0.62f, 0.98f, 1f), TextAlignmentOptions.Center);
        if (codeFont != null) bt.font = codeFont;
        bt.fontStyle = FontStyles.Bold;
        bt.characterSpacing = 20f;
        transferCodeBlockTexts[0] = bt;
        // 履歴なしのときだけ出すメッセージ(ブロックと同じ位置)。
        transferCodeText = CreateText("Code", rootRect, new Vector2(0f, 32f), new Vector2(720f, 80f), 30f, CyanDim, TextAlignmentOptions.Center);

        // 入力: ラベル+入力欄+適用ボタン(行の左右端はチップ列に揃える)。
        TMP_Text inputLabel = CreateText("InputLabel", rootRect, new Vector2(-contentHalf + 180f, -48f), new Vector2(360f, 30f), 20f, new Color(0.388f, 0.867f, 0.91f, 0.5f), TextAlignmentOptions.Left);
        inputLabel.characterSpacing = 3f;

        const float inputW = 420f;
        const float applyW = 108f;
        const float rowH = 56f;
        BuildInputField(rootRect, new Vector2(-contentHalf + inputW * 0.5f, -104f), new Vector2(inputW, rowH));

        applyButton = CreatePanel("ApplyButton", rootRect, new Vector2(contentHalf - applyW * 0.5f, -104f), new Vector2(applyW, rowH), ApplyIdle);
        TMP_Text applyLabel = CreateText("ApplyLabel", applyButton.rectTransform, Vector2.zero, new Vector2(applyW, rowH), 26f, ApplyLabelIdle, TextAlignmentOptions.Center);
        StretchToParent(applyLabel.rectTransform);
        applyLabel.fontStyle = FontStyles.Bold;
        applyLabelText = applyLabel;

        transferMessageText = CreateText("Message", rootRect, new Vector2(0f, -180f), new Vector2(720f, 36f), 22f, Cyan, TextAlignmentOptions.Center);

        // 操作ヒント行(ENTER 適用 / CTRL+C コピー / ESC 戻る)は第34便で削除。
        // 操作自体は有効なまま、画面の主張を抑える。

        // Fill the label texts.
        SetChildText(rootRect, "Heading", "引き継ぎ");
        SetChildText(rootRect, "CodeLabel", "あなたの引き継ぎコード");
        SetChildText(rootRect, "InputLabel", "コードを入力");
        SetChildText(applyButton.rectTransform, "ApplyLabel", "適用");
        transferMessageText.text = string.Empty;

        transferRoot.SetActive(false);
    }

    private void BuildInputField(RectTransform parent, Vector2 pos, Vector2 size)
    {
        GameObject fieldObj = new GameObject("CodeInput", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TMP_InputField));
        fieldObj.layer = gameObject.layer;
        RectTransform fieldRect = (RectTransform)fieldObj.transform;
        fieldRect.SetParent(parent, false);
        fieldRect.anchorMin = fieldRect.anchorMax = new Vector2(0.5f, 0.5f);
        fieldRect.anchoredPosition = pos;
        fieldRect.sizeDelta = size;
        // フィールド本体の Image は枠として使い、フォーカス/エラーで色を変える。
        Image bg = fieldObj.GetComponent<Image>();
        bg.color = InputBorderIdle;
        transferInputBorder = bg;

        Image fieldFill = CreatePanel("Fill", fieldRect, Vector2.zero, Vector2.zero, new Color(0.016f, 0.067f, 0.114f, 1f));
        fieldFill.rectTransform.anchorMin = Vector2.zero;
        fieldFill.rectTransform.anchorMax = Vector2.one;
        fieldFill.rectTransform.offsetMin = new Vector2(3f, 3f);
        fieldFill.rectTransform.offsetMax = new Vector2(-3f, -3f);

        GameObject areaObj = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
        areaObj.layer = gameObject.layer;
        RectTransform areaRect = (RectTransform)areaObj.transform;
        areaRect.SetParent(fieldRect, false);
        areaRect.anchorMin = Vector2.zero;
        areaRect.anchorMax = Vector2.one;
        // プレースホルダーと入力文字は左基準で揃える(中央寄せだと入力開始時に
        // 文字位置が跳ねる。oracle 第2周)。
        areaRect.offsetMin = new Vector2(24f, 6f);
        areaRect.offsetMax = new Vector2(-16f, -6f);

        TMP_Text placeholder = CreateText("Placeholder", areaRect, Vector2.zero, size, 28f, new Color(0.498f, 0.682f, 0.722f, 0.5f), TextAlignmentOptions.Left);
        StretchToParent(placeholder.rectTransform);
        if (codeFont != null) placeholder.font = codeFont;
        // 発行コードは4文字(v2)。旧16文字コードも引き続き入力・適用できる。
        placeholder.text = "XXXX";

        TMP_Text textComp = CreateText("Text", areaRect, Vector2.zero, size, 30f, new Color(0.953f, 0.984f, 1f, 0.95f), TextAlignmentOptions.Left);
        StretchToParent(textComp.rectTransform);
        if (codeFont != null) textComp.font = codeFont;
        textComp.fontStyle = FontStyles.Bold;
        textComp.characterSpacing = 6f;

        transferInput = fieldObj.GetComponent<TMP_InputField>();
        transferInput.textViewport = areaRect;
        transferInput.textComponent = textComp;
        transferInput.placeholder = placeholder;
        transferInput.fontAsset = codeFont != null ? codeFont : uiFont;
        transferInput.pointSize = 30f;
        transferInput.characterLimit = 19; // 16 symbols + 3 grouping hyphens
        transferInput.lineType = TMP_InputField.LineType.SingleLine;
        transferInput.richText = false;
        transferInput.customCaretColor = true;
        transferInput.caretColor = Cyan;
        transferInput.caretWidth = 3;
        transferInput.onValidateInput += (string text, int pos, char ch) => char.ToUpperInvariant(ch);
        // 入力し直したらエラー表示(赤枠)を解除する。
        transferInput.onValueChanged.AddListener(_ => transferInputError = false);
        transferInput.onSubmit.AddListener(_ => ApplyTransfer());
    }

    // ---- UI helpers -------------------------------------------------------

    private TMP_Text CreateText(string objectName, Transform parent, Vector2 pos, Vector2 size, float fontSize, Color color, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.layer = gameObject.layer;
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        if (uiFont != null) label.font = uiFont;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = align;
        label.raycastTarget = false;
        return label;
    }

    private void OnDestroy()
    {
        ReleaseBackdropTexture();
    }

    private RawImage CreateRawImage(string objectName, Transform parent)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        go.layer = gameObject.layer;
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        RawImage raw = go.GetComponent<RawImage>();
        raw.raycastTarget = false;
        return raw;
    }

    private Image CreatePanel(string objectName, Transform parent, Vector2 pos, Vector2 size, Color color)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetChildText(Transform parent, string childName, string value)
    {
        Transform child = parent.Find(childName);
        if (child == null) return;
        TMP_Text text = child.GetComponent<TMP_Text>();
        if (text != null)
        {
            text.text = value;
            TmpAlign.CenterInkVertically(text);
        }
    }
}

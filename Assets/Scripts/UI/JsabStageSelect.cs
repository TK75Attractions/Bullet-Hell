using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.InputSystem;
using TMPro;

// Runtime-built "Just Shapes & Beats"-style horizontal carousel variation of the
// stage select. Everything here is created procedurally so the scene keeps no
// permanent JSAB objects. StageSelectManager owns the lifecycle and forwards the
// current stage / input to this overlay while the JSAB style is active.
public class JsabStageSelect : MonoBehaviour
{
    // Palette: cyan + black + navy. No heavy white use (per design).
    private static readonly Color Cyan = new Color(0.22f, 0.76f, 0.878f, 1f);       // #38C2E0
    private static readonly Color CyanDim = new Color(0.22f, 0.76f, 0.878f, 0.55f);
    // 左右カルーセル矢印は白で目立たせる(2026-07-13 指摘「矢印が見づらい」)。
    // 銀/シアン様式と喧嘩しないよう、面は白・背後の発光ハローだけシアン寄りにする。
    private static readonly Color ArrowWhite = new Color(1f, 1f, 1f, 1f);
    // 発光ハローは弱めに(2026-07-14 指摘「矢印のグローを弱く」)。矢印本体は白のまま。
    // 前便 63f85b0 で 0.30 まで下げたが「まだ強い」の再指摘(2026-07-14 夜)で
    // 0.30→0.15 へ半減。ハローのサイズも 104→92 に微縮小(下記 BuildSidePanel)。
    private static readonly Color ArrowGlowColor = new Color(0.72f, 0.92f, 1f, 0.15f);
    private static readonly Color Navy = new Color(0.043f, 0.106f, 0.169f, 1f);      // #0B1B2B
    private static readonly Color NavyDeep = new Color(0.02f, 0.05f, 0.09f, 1f);
    // Mockup-derived accents: bright cyan edge highlight, white corner brackets,
    // pale thumbnail rim, side-card body.
    private static readonly Color AccentCyan = new Color(0.55f, 0.93f, 1f, 1f);
    private static readonly Color BracketColor = new Color(0.92f, 0.95f, 0.97f, 0.9f);
    // 統一様式の銀エッジ(UiButtonStyle の枠線と同じ視覚 sRGB 値)。
    private static readonly Color SilverEdge = new Color(0.412f, 0.400f, 0.447f, 0.85f);
    private static readonly Color ThumbRimColor = new Color(0.75f, 0.82f, 0.88f, 0.35f);
    private static readonly Color CardBg = new Color(0.035f, 0.075f, 0.125f, 1f);

    private CanvasGroup rootCG;
    private TMP_FontAsset font;
    private Sprite playerSprite;
    private Sprite glowFrameSprite;

    // Center card
    private RectTransform cardRect;
    private Image cardFrameImg;          // chamfered glow frame (sliced sprite)
    private Image[] cardAccents;         // bright edge segments (top-left / bottom-right)
    private Image[] cardBrackets;        // side-card look for the fly-out morph (alpha 0 at rest)
    private RectTransform cardMediaRect; // video + fallback wrapper (insets morph to side layout)
    private RawImage cardVideo;
    private Image cardFallback;
    private TMP_Text cardFallbackName;
    private Image cardScrim;             // dim wash for the fly-out morph (alpha 0 at rest)
    private TMP_Text cardInnerTitle;     // side-card in-card title, fades in while flying out
    // 着地時の受け渡しポップ根治用: サイドカードだけが持つ「淡色リム」と「山括弧
    // 矢印」を中央カードにも持たせ、飛行中にフェードインさせて着地状態と一致させる。
    private RectTransform cardRimRect;   // rim strips wrapper (insets follow the media morph)
    private Image[] cardRim;
    private TMP_Text cardArrow;
    private TMP_Text stageNameText;
    // ステージ名脇の統一様式スラッシュ([0]=左太 [1]=左細 [2]=右細 [3]=右太)。
    private ParallelogramGraphic[] stageNameSlashes;
    private const float StageNameSlashH = 54f;
    private float accentAlpha = 1f;      // accents fade back in after a landing

    // One pooled neighbour card. Panels swap roles (left/right/spare) on landing
    // so the incoming card can slide in from off-screen while the old one leaves
    // (5-slot band: off-left, left, center, right, off-right).
    private class SidePanel
    {
        public RectTransform rect;
        public CanvasGroup cg;
        public CanvasGroup decorCG;    // title + arrow + brackets (side-look decor)
        public Image[] brackets;       // white corner brackets (side look)
        public Image[] silverRim;      // パネル外周の銀エッジ(統一様式・decor と一緒にフェード)
        public Image[] thumbRim;       // thin pale frame around the thumbnail
        public Image glowFrame;        // center-look frame for the morph, alpha 0 at rest
        public RectTransform thumbArea;
        public CanvasGroup mediaCG;    // thumbArea 専用。サムネ準備待ちはここだけ透明にする
        public RawImage thumb;
        public Image fallback;
        public TMP_Text fbName;
        public Image scrim;
        public TMP_Text title;         // stage name inside the card top (per mockup)
        public TMP_Text arrow;
        public RectTransform arrowGlow; // 矢印背後の発光ハロー(矢印に追従)
        public VideoPlayer vp;
        public RenderTexture rt;
        // サムネイルまたは対象名フォールバックが表示可能かを示す。動画準備中も
        // 正しいフォールバックを即表示するため、通常は true のまま維持する。
        public bool contentReady = true;
        public int thumbnailRequestId;
        public float alphaTarget = 1f; // 端のステージで存在しない側は 0
        public int side;               // -1 left / +1 right (arrow glyph & placement)
    }

    private SidePanel leftPanel;
    private SidePanel rightPanel;
    private SidePanel sparePanel;

    // Page indicator (rebuilt when the stage count is known)
    private RectTransform progressRow;
    // Persistent player marker on the indicator; tweens between nodes.
    private RectTransform markerRect;
    private float markerToX;
    private float markerFromX;
    private float markerTweenTime = -1f;    // <0 == not animating
    private const float markerTweenDuration = 0.25f;
    private const float MarkerY = 28f;      // stands on the dotted line (feet at the line, = markerHeight/2)

    // Cloned style-0 top bar pieces that must mirror the live originals
    // (StageSelectManager keeps updating them even while alpha-hidden).
    private TMP_Text topBarTimerText;
    private RectTransform topBarTimeDim;
    private TMP_Text origTimerText;
    private RectTransform origTimeDim;
    // 上部バー(タイマー/残り時間)のクローンルート。難易度モーダルを開いている間だけ
    // diffRoot(ぼかし+暗幕)より前面へ持ち上げ、制限時間が隠れず読めるようにする
    // (2026-07-12 指摘「難易度選択中もバーを最前面に+制限時間を表示」)。
    private Transform topBarBaseRoot;
    private Transform topBarTextRoot;
    private int topBarBaseSiblingIndex = -1;
    private int topBarTextSiblingIndex = -1;
    private bool topBarRaised;

    // Carousel slots. Panels physically move between these on stage change.
    private const float BandYOffset = -60f;   // 全体を下げて上下の余白バランスを取る
    private const float SideSlotX = 712f;
    private const float OffSlotX = 1220f;     // 画面外スロット(登場/退場)の x
    private const float SideSlotY = 40f + BandYOffset;
    private static readonly Vector2 CenterSlotPos = new Vector2(0f, 70f + BandYOffset);
    private static readonly Vector2 CenterSlotSize = new Vector2(936f, 528f);
    private static readonly Vector2 SideSlotSize = new Vector2(456f, 304f);
    private static readonly Color ThumbDim = new Color(0.5f, 0.55f, 0.62f, 1f);
    private static readonly Color ThumbScrimColor = new Color(0.02f, 0.05f, 0.09f, 0.4f);

    // Side-card interior layout (mockup: title band on top, thumbnail below).
    private const float SideTitleBand = 52f;
    private const float SideThumbInset = 18f;
    private const float GlowMargin = 14f;     // glow sprite overhang outside the card rect

    private RectTransform stageNameRect;

    // Video
    private VideoPlayer videoPlayer;
    private RenderTexture videoRT;
    private RenderTexture departingVideoRT;

    // State
    private int currentIndex = 0;
    private int totalStages = 1;
    private float pulseTime;
    // Carousel transition (panels flying between slots). <0 == not animating.
    private float transTime = -1f;
    private int transDir;
    private const float transDuration = 0.3f;
    private float exitStartAlpha;       // 退場パネルの開始アルファ(端では元々 0)
    private bool nameSwapped;           // ステージ名クロスフェードの差し替え済みフラグ
    private bool exitHadFrame;          // 遷移開始時に中央RTへ動画フレームが出ていたか

    // --- In-screen difficulty overlay (built once, hidden until a stage is decided) ---
    private RectTransform diffRoot;      // container for blur + scrim + panel
    private RawImage diffBlur;           // frozen, downsampled snapshot of the carousel
    private Image diffScrim;             // dark wash for text contrast
    private RectTransform diffPanel;     // cloned style-0 difficulty column
    private DefficultyBar diffBar;       // the cloned component (style-0 look & animation)
    private readonly RectTransform[] diffBoxRects = new RectTransform[3]; // Easy/Normal/Lunatic bar rects (mouse hit areas)
    private bool difficultyOpen;
    private float diffOpenTime;           // unscaled time the modal opened (mouse debounce)
    private RenderTexture blurRT;
    private bool mouseConfirm;           // set when a difficulty button is left-clicked
    // 開閉フェード(alpha + パネルの軽いスケール 0.96→1.0)。急な表示/消灯を防ぐ。
    private CanvasGroup diffCG;
    private Coroutine diffFadeCo;
    // 難易度パネルの表示スケール。2026-07-11 指摘「ボタンを大きく」で
    // 0.8(style0 準拠) → 0.9 へ(ボタン自体の拡大 583→660 と合わせ実表示 +27%)。
    private const float DiffPanelBaseScale = 0.9f;
    private const float DiffFadeDuration = 0.18f;
    // プレイ決定時の退場演出(第30便): 行がタイトルのスタート演出と同系で右へ
    // スライドアウトしてからホワイトアウトへ渡す。退場中は Tick の行アニメ/
    // マウス選択を止める(Tick が whiteBar の X を 0 に固定し続けるため)。
    private bool diffExiting;
    private readonly RectTransform[] exitRowRects = new RectTransform[3];
    private readonly Vector2[] exitRowBasePos = new Vector2[3];
    private readonly Vector3[] exitRowBaseScale = new Vector3[3];
    private RectTransform exitWhiteRect;
    private Vector2 exitWhiteBasePos;
    private Image exitBarImg;
    private Color exitBarColor;
    // 統一様式ボタンは焼き込みテクスチャのため color 乗算では白フラッシュ
    // できない。ボタン外形と同じ平行四辺形の白オーバーレイで光らせる。
    private ParallelogramGraphic exitFlash;
    // 退場中にフェードで消す付随要素(見出し/ルビ/説明/プロンプト/装飾ライン)。
    // 見出しを残すとホワイトアウト中に黒い文字だけが最後まで浮いて見える。
    private TMP_Text[] exitFadeTexts = new TMP_Text[0];
    private float[] exitFadeTextAlphas = new float[0];
    private Image[] exitFadeImages = new Image[0];
    private float[] exitFadeImageAlphas = new float[0];

    public bool DifficultyOpen => difficultyOpen;
    public int DifficultyIndex => diffBar != null ? diffBar.index : 1;

    // Returns (and clears) whether the mouse just clicked a difficulty button.
    public bool ConsumeMouseConfirm()
    {
        bool v = mouseConfirm;
        mouseConfirm = false;
        return v;
    }

    public bool Visible { get; private set; }

    public static JsabStageSelect Create(Transform parent, TMP_FontAsset font, Sprite playerSprite)
    {
        GameObject go = new GameObject("JsabStageSelectCanvas");
        if (parent != null) go.transform.SetParent(parent, false);

        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5; // above the (mostly 0/1) select canvases, opaque so it covers them

        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();

        JsabStageSelect jsab = go.AddComponent<JsabStageSelect>();
        jsab.font = font;
        jsab.playerSprite = playerSprite;
        jsab.rootCG = go.AddComponent<CanvasGroup>();
        jsab.rootCG.interactable = false;
        jsab.rootCG.blocksRaycasts = false;
        jsab.Build((RectTransform)go.transform);
        jsab.SetVisible(false);
        return jsab;
    }

    private void Build(RectTransform root)
    {
        glowFrameSprite = CreateGlowFrameSprite();

        // Opaque black backdrop covering the whole screen.
        Image bg = NewImage("Background", root, Color.black);
        Stretch(bg.rectTransform);

        // タイトル画面と同じ幾何学図形を薄く敷く(カード類より背面)。
        BuildBackgroundShapes(root);

        // --- Top bar: clone of the default (style 0) header so both styles share
        // the exact same design. The timer text and the red time-dim panel mirror
        // the live originals every frame in Tick.
        CloneTopBar(root);
        RestyleTopBar();

        // --- Neighbour cards (pooled; spare parks off-screen until a transition) ---
        leftPanel = BuildSidePanel(root, "LeftCard");
        SetPanelSide(leftPanel, -1);
        leftPanel.rect.anchoredPosition = new Vector2(-SideSlotX, SideSlotY);
        rightPanel = BuildSidePanel(root, "RightCard");
        SetPanelSide(rightPanel, 1);
        rightPanel.rect.anchoredPosition = new Vector2(SideSlotX, SideSlotY);
        sparePanel = BuildSidePanel(root, "SparePanel");
        SetPanelSide(sparePanel, 1);
        sparePanel.rect.anchoredPosition = new Vector2(OffSlotX, SideSlotY);
        sparePanel.cg.alpha = 0f;
        sparePanel.alphaTarget = 0f;

        // --- Center card ---
        GameObject cardGO = new GameObject("CenterCard", typeof(RectTransform));
        cardGO.transform.SetParent(root, false);
        cardRect = (RectTransform)cardGO.transform;
        cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = CenterSlotPos;
        cardRect.sizeDelta = CenterSlotSize;

        Image cardBody = NewImage("CardBody", cardRect, NavyDeep);
        Stretch(cardBody.rectTransform);

        // Media wrapper (video + fallback). Its insets morph toward the side-card
        // thumbnail layout while the card flies out to a side slot.
        GameObject mediaGO = new GameObject("CardMedia", typeof(RectTransform));
        mediaGO.transform.SetParent(cardRect, false);
        cardMediaRect = (RectTransform)mediaGO.transform;
        Stretch(cardMediaRect);
        SetMediaInsets(cardMediaRect, 0f);

        // 映像/フォールバックの四隅がチャンファ枠からはみ出さないよう、枠と同じ
        // 面取り形状のステンシルマスクで子要素をクリップする。
        Image maskImg = mediaGO.AddComponent<Image>();
        maskImg.sprite = CreateChamferMaskSprite();
        maskImg.type = Image.Type.Sliced;
        maskImg.raycastTarget = false;
        Mask chamferMask = mediaGO.AddComponent<Mask>();
        chamferMask.showMaskGraphic = false;

        // Fallback (navy card + big name) shown when no video.
        cardFallback = NewImage("CardFallback", cardMediaRect, Navy);
        Stretch(cardFallback.rectTransform);
        cardFallbackName = NewText("CardFallbackName", cardFallback.rectTransform, "", 96f, Cyan, TextAlignmentOptions.Center);
        Stretch((RectTransform)cardFallbackName.transform);

        // Video surface.
        videoRT = new RenderTexture(768, 432, 0);
        videoRT.name = "JsabStageVideoRT";
        cardVideo = NewRawImage("CardVideo", cardMediaRect, videoRT);
        Stretch(cardVideo.rectTransform);

        // Dim wash used only while the card morphs into a side card.
        cardScrim = NewImage("CardScrim", cardMediaRect, new Color(ThumbScrimColor.r, ThumbScrimColor.g, ThumbScrimColor.b, 0f));
        Stretch(cardScrim.rectTransform);

        GameObject vpGO = new GameObject("StageVideoPlayer");
        vpGO.transform.SetParent(cardMediaRect, false);
        videoPlayer = vpGO.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.source = VideoSource.Url;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = videoRT;
        videoPlayer.isLooping = true;
        // 録画サムネ(924x754 ≒ 6:5)を 16:9 RT へ「見切れなく」収める。既定の
        // 詰め方だと上下がクロップされ中央帯しか映らないため FitInside=全体を
        // レターボックス表示(左右に余白)する。
        videoPlayer.aspectRatio = VideoAspectRatio.FitInside;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        videoPlayer.skipOnDrop = true;
        // The RenderTexture keeps the previous stage's frame while the next clip
        // prepares; visuals only swap once the new clip is ready (no flash).
        videoPlayer.prepareCompleted += OnMainVideoPrepared;

        // Side-card rim for the fly-out morph (alpha 0 at rest). Lives outside the
        // chamfer mask so its corners stay square like the real side-card rim.
        GameObject rimGO = new GameObject("CardRim", typeof(RectTransform));
        rimGO.transform.SetParent(cardRect, false);
        cardRimRect = (RectTransform)rimGO.transform;
        Stretch(cardRimRect);
        SetMediaInsets(cardRimRect, 0f);
        cardRim = BuildRim(cardRimRect);
        SetRim(cardRim, new Color(ThumbRimColor.r, ThumbRimColor.g, ThumbRimColor.b, 0f), 2f);

        // Chamfered glow frame (mockup: rounded/notched luminous cyan border).
        Image frame = NewImage("CardFrame", cardRect, Cyan);
        frame.sprite = glowFrameSprite;
        frame.type = Image.Type.Sliced;
        Stretch(frame.rectTransform);
        SetInset(frame.rectTransform, -GlowMargin);
        cardFrameImg = frame;

        // Bright edge accents (thicker stroke segments; mockup has them near the
        // top-left and bottom-right corners of the frame).
        cardAccents = new Image[2];
        for (int i = 0; i < 2; i++)
        {
            Image acc = NewImage("FrameAccent", cardRect, AccentCyan);
            RectTransform ar = acc.rectTransform;
            bool topLeft = i == 0;
            ar.anchorMin = ar.anchorMax = topLeft ? new Vector2(0f, 1f) : new Vector2(1f, 0f);
            ar.pivot = new Vector2(topLeft ? 0f : 1f, 0.5f);
            ar.anchoredPosition = new Vector2(topLeft ? 30f : -30f, 0f);
            ar.sizeDelta = new Vector2(130f, 7f);
            cardAccents[i] = acc;
        }

        // Side-card look pieces for the fly-out morph (invisible at rest).
        cardBrackets = BuildBrackets(cardRect);
        SetBracketAlpha(cardBrackets, 0f);
        cardInnerTitle = NewText("CardInnerTitle", cardRect, "", 30f, Cyan, TextAlignmentOptions.Center);
        RectTransform citr = (RectTransform)cardInnerTitle.transform;
        citr.anchorMin = new Vector2(0f, 1f);
        citr.anchorMax = new Vector2(1f, 1f);
        citr.pivot = new Vector2(0.5f, 1f);
        citr.anchoredPosition = new Vector2(0f, -6f);
        citr.sizeDelta = new Vector2(0f, SideTitleBand - 10f);
        cardInnerTitle.alpha = 0f;

        // Side-card arrow for the fly-out morph (alpha 0 at rest; BeginTransition
        // points it at the side the card is about to become).
        cardArrow = NewText("CardArrow", cardRect, ">", 60f, new Color(ArrowWhite.r, ArrowWhite.g, ArrowWhite.b, 0f), TextAlignmentOptions.Center);
        RectTransform car = (RectTransform)cardArrow.transform;
        car.pivot = new Vector2(0.5f, 0.5f);
        car.sizeDelta = new Vector2(64f, 90f);

        // Stage name above the card.
        stageNameText = NewText("StageName", root, "", 64f, Cyan, TextAlignmentOptions.Center);
        RectTransform snr = (RectTransform)stageNameText.transform;
        snr.anchorMin = snr.anchorMax = new Vector2(0.5f, 0.5f);
        snr.pivot = new Vector2(0.5f, 0.5f);
        // +46f keeps the name clear of the 120px-tall top bar (bottom edge y=420).
        snr.sizeDelta = new Vector2(1000f, 84f);
        snr.anchoredPosition = new Vector2(0f, CenterSlotPos.y + CenterSlotSize.y * 0.5f + 46f);
        stageNameRect = snr;

        // 統一様式(2026-07-11): ステージ名の左右に 19° 白スラッシュ対
        // (外=太・内=細α0.5)。x は名前のインク幅に追従(UpdateStageNameSlashes)。
        stageNameSlashes = new ParallelogramGraphic[4];
        stageNameSlashes[0] = UiButtonStyle.AddSlash(snr, "NameSlashL", Color.white, 0f, 8f, StageNameSlashH);
        stageNameSlashes[1] = UiButtonStyle.AddSlash(snr, "NameSlashLThin", new Color(1f, 1f, 1f, 0.5f), 0f, 2.5f, StageNameSlashH);
        stageNameSlashes[2] = UiButtonStyle.AddSlash(snr, "NameSlashRThin", new Color(1f, 1f, 1f, 0.5f), 0f, 2.5f, StageNameSlashH);
        stageNameSlashes[3] = UiButtonStyle.AddSlash(snr, "NameSlashR", Color.white, 0f, 8f, StageNameSlashH);

        // --- Progress indicator (rings + filled current dot + player marker) ---
        BuildProgressIndicator(root);

        // --- Bottom hint bar ---
        BuildHintBar(root);

        // --- In-screen difficulty overlay (hidden until a stage is decided) ---
        BuildDifficultyOverlay(root);
    }

    // タイトル画面の Shapes と同じ素材/生成方法(回転 Image 四角・SoftCircleGraphic・
    // EquilateralTriangleGraphic、ピンク/ブルー配色)を流用した背景装飾。タイトルの
    // alpha 0.28〜0.40 に対し 1/3 程度に抑え、カルーセルの視認性を邪魔しない。
    // ドリフトはタイトルと同系の ShapeDrifter(子を緩慢に漂わせる)に任せる。
    private void BuildBackgroundShapes(RectTransform root)
    {
        GameObject layer = new GameObject("BgShapes", typeof(RectTransform));
        layer.transform.SetParent(root, false);
        RectTransform lr = (RectTransform)layer.transform;
        Stretch(lr);

        AddShape<Image>(lr, "Square1", new Vector2(-700f, -395f), new Vector2(300f, 300f), 20f, new Color(0.95f, 0.20f, 0.50f, 0.12f));
        AddShape<Image>(lr, "Square2", new Vector2(760f, 350f), new Vector2(250f, 250f), 35f, new Color(0.12f, 0.35f, 0.80f, 0.13f));
        AddShape<SoftCircleGraphic>(lr, "Circle1", new Vector2(620f, -400f), new Vector2(320f, 320f), 0f, new Color(0.20f, 0.60f, 1.00f, 0.12f));
        AddShape<SoftCircleGraphic>(lr, "Circle2", new Vector2(-760f, 345f), new Vector2(190f, 190f), 0f, new Color(0.95f, 0.30f, 0.55f, 0.10f));
        AddShape<EquilateralTriangleGraphic>(lr, "Triangle1", new Vector2(-320f, -420f), new Vector2(260f, 225f), 345f, new Color(0.95f, 0.20f, 0.50f, 0.12f));
        AddShape<EquilateralTriangleGraphic>(lr, "Triangle2", new Vector2(430f, 385f), new Vector2(200f, 175f), 150f, new Color(0.25f, 0.50f, 0.95f, 0.11f));

        // ShapeDrifter.Awake は追加時点の子を拾うので、図形を並べ終えてから付ける。
        layer.AddComponent<ShapeDrifter>();
    }

    private void AddShape<T>(RectTransform parent, string shapeName, Vector2 pos, Vector2 size, float rotZ, Color color) where T : Graphic
    {
        GameObject go = new GameObject(shapeName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        rt.localEulerAngles = new Vector3(0f, 0f, rotZ);
        T g = go.AddComponent<T>();
        g.color = color;
        g.raycastTarget = false;
    }

    // Clones the two scene "Head" subtrees (base graphics from StaticCanvas, text
    // layer from StageCanvas) into this canvas. The clones are passive visuals;
    // dynamic parts (timer text, TimeDim width) are mirrored from the originals.
    private void CloneTopBar(RectTransform root)
    {
        Transform canvases = transform.parent;
        if (canvases == null) return;

        Transform staticHead = canvases.Find("StaticCanvas/StageBoxParent/Head");
        if (staticHead != null)
        {
            GameObject clone = Instantiate(staticHead.gameObject, root);
            clone.name = "TopBarBase";
            clone.SetActive(true);
            CopyRect((RectTransform)clone.transform, (RectTransform)staticHead);
            topBarBaseRoot = clone.transform;
            topBarTimeDim = clone.transform.Find("TimeDim") as RectTransform;
            origTimeDim = staticHead.Find("TimeDim") as RectTransform;
        }

        Transform textHead = canvases.Find("StageCanvas/StageBoxParent/Head");
        if (textHead != null)
        {
            GameObject clone = Instantiate(textHead.gameObject, root);
            clone.name = "TopBarText";
            clone.SetActive(true);
            CopyRect((RectTransform)clone.transform, (RectTransform)textHead);
            topBarTextRoot = clone.transform;
            // The clone must not react to state transitions; it is a static copy.
            Header dupHeader = clone.GetComponent<Header>();
            if (dupHeader != null) Destroy(dupHeader);
            Transform cloneTimer = clone.transform.Find("TimerText");
            topBarTimerText = cloneTimer != null ? cloneTimer.GetComponent<TMP_Text>() : null;
            Transform srcTimer = textHead.Find("TimerText");
            origTimerText = srcTimer != null ? srcTimer.GetComponent<TMP_Text>() : null;
        }
    }

    // 上部バーを確立済みデザイン言語(リザルト/設定のヘッダー帯様式)へ寄せる。
    // JSAB クローン(TopBarBase)側だけを対象にし、シーン原本(style0)には触れない。
    // 高さ・レイアウト・タイマー位置は不変で、面色/縁だけを様式統一する:
    //   主帯 head = フラット明青 → 横グラデ(左濃青#014190→右鮮青#026CDB)
    //   右副帯 Gray = 濃紺(リザルト副帯トーン)
    //   全幅に 3層エッジ(上=銀/その直下=シアン細線/下=沈み)を重ねる
    // (oracle 案「横グラデ+銀/シアン縁」。高さ縮小は全要素の再配置が要るため見送り)。
    private void RestyleTopBar()
    {
        if (topBarBaseRoot == null) return;
        Transform bandT = topBarBaseRoot.Find("head");
        Image band = bandT != null ? bandT.GetComponent<Image>() : null;
        if (band != null)
        {
            Vector2 sz = band.rectTransform.sizeDelta;
            band.sprite = CreateTopBandSprite(Mathf.RoundToInt(sz.x), Mathf.RoundToInt(sz.y));
            band.color = Color.white;
            band.type = Image.Type.Simple;
            // 3層エッジは帯・副帯・仕切りの上へ重ねたいので Head クローン直下に置き
            // 最前面へ(全幅を1本で通す)。
            float halfH = sz.y * 0.5f;
            AddBarEdge("BarEdgeSilver", sz.x, 2f, halfH - 1f, new Color(0.55f, 0.60f, 0.70f, 0.85f));
            AddBarEdge("BarEdgeCyan", sz.x, 1f, halfH - 3.5f, new Color(0.22f, 0.76f, 0.878f, 0.28f));
            AddBarEdge("BarEdgeDark", sz.x, 2f, -(halfH - 1f), new Color(0f, 0.02f, 0.05f, 0.6f));
        }
        Transform grayT = topBarBaseRoot.Find("Gray");
        Image gray = grayT != null ? grayT.GetComponent<Image>() : null;
        if (gray != null) gray.color = new Color(0.02f, 0.075f, 0.16f, 1f);
    }

    // 全幅の細帯(上部バーの縁)を Head クローン直下に最前面で1本足す。
    private void AddBarEdge(string name, float width, float height, float y, Color color)
    {
        Image e = NewImage(name, topBarBaseRoot, color);
        RectTransform rt = e.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.SetAsLastSibling();
    }

    // 上部バー主帯の横グラデ(左濃青→右鮮青。リザルトヘッダー主帯と同色)。
    private Sprite CreateTopBandSprite(int W, int H)
    {
        Texture2D tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        tex.name = "JsabTopBandTex";
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color mainL = new Color(0.004f, 0.255f, 0.565f);
        Color mainR = new Color(0.008f, 0.424f, 0.859f);
        Color32[] px = new Color32[W * H];
        for (int x = 0; x < W; x++)
        {
            Color32 c = Color.Lerp(mainL, mainR, W > 1 ? x / (float)(W - 1) : 0f);
            for (int y = 0; y < H; y++) px[y * W + x] = c;
        }
        tex.SetPixels32(px);
        tex.Apply();
        Sprite s = Sprite.Create(tex, new Rect(0f, 0f, W, H), new Vector2(0.5f, 0.5f), 100f);
        s.name = "JsabTopBand";
        return s;
    }

    private static void CopyRect(RectTransform dst, RectTransform src)
    {
        dst.anchorMin = src.anchorMin;
        dst.anchorMax = src.anchorMax;
        dst.pivot = src.pivot;
        dst.anchoredPosition = src.anchoredPosition;
        dst.sizeDelta = src.sizeDelta;
        dst.localScale = src.localScale;
    }

    // Builds one pooled neighbour card (mockup "べにぐち" card: white corner
    // brackets, stage name inside the top band, thumbnail with a pale rim below).
    private SidePanel BuildSidePanel(RectTransform root, string name)
    {
        SidePanel p = new SidePanel();
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(root, false);
        p.rect = (RectTransform)go.transform;
        p.rect.anchorMin = p.rect.anchorMax = new Vector2(0.5f, 0.5f);
        p.rect.pivot = new Vector2(0.5f, 0.5f);
        p.rect.sizeDelta = SideSlotSize;
        // 端のステージ/準備待ちで丸ごとフェードさせるための CanvasGroup。
        p.cg = go.AddComponent<CanvasGroup>();

        Image body = NewImage("CardBody", p.rect, CardBg);
        Stretch(body.rectTransform);

        // Thumbnail area (title band above, margins per mockup). The insets morph
        // to full-bleed while the panel flies into the center slot.
        GameObject areaGO = new GameObject("ThumbArea", typeof(RectTransform));
        areaGO.transform.SetParent(p.rect, false);
        p.thumbArea = (RectTransform)areaGO.transform;
        Stretch(p.thumbArea);
        SetMediaInsets(p.thumbArea, 1f);
        // サムネ準備待ちの間は thumbArea だけを透明にする(パネル全体を消すと
        // スライドイン中にカードが見えず、着地後の丸ごとポップになる)。
        p.mediaCG = areaGO.AddComponent<CanvasGroup>();

        // Fallback tile (stage name on navy) when the stage has no preview video.
        p.fallback = NewImage("ThumbFallback", p.thumbArea, new Color(0.04f, 0.09f, 0.14f, 1f));
        Stretch(p.fallback.rectTransform);
        p.fbName = NewText("ThumbFallbackName", p.fallback.rectTransform, "", 44f, CyanDim, TextAlignmentOptions.Center);
        Stretch((RectTransform)p.fbName.transform);

        // Dim thumbnail = the stage preview video rendered to a RenderTexture and
        // tinted down so the center card stays the focus.
        p.rt = new RenderTexture(384, 216, 0) { name = name + "RT" };
        p.thumb = NewRawImage("Thumb", p.thumbArea, p.rt);
        Stretch(p.thumb.rectTransform);
        p.thumb.color = ThumbDim;
        p.thumb.enabled = false;

        // A subtle dark wash over the thumbnail so it reads as "dimmed / inactive".
        p.scrim = NewImage("ThumbScrim", p.thumbArea, ThumbScrimColor);
        Stretch(p.scrim.rectTransform);

        // Thin pale rim around the thumbnail (mockup shows a light border).
        p.thumbRim = BuildRim(p.thumbArea);
        SetRim(p.thumbRim, ThumbRimColor, 2f);

        GameObject vpGO = new GameObject("VideoPlayer");
        vpGO.transform.SetParent(p.rect, false);
        p.vp = vpGO.AddComponent<VideoPlayer>();
        p.vp.playOnAwake = false;
        p.vp.source = VideoSource.Url;
        p.vp.renderMode = VideoRenderMode.RenderTexture;
        p.vp.targetTexture = p.rt;
        p.vp.isLooping = true;
        // 中央カードと同様に全体をレターボックス表示(見切れ防止)。
        p.vp.aspectRatio = VideoAspectRatio.FitInside;
        p.vp.waitForFirstFrame = true;
        p.vp.audioOutputMode = VideoAudioOutputMode.None;
        p.vp.skipOnDrop = true;
        // prepareCompleted / errorReceived は UpdateThumb で要求ごとに登録する。
        // パネル再利用後に古い非同期通知が届いても requestId で破棄できる。
        // エラー時のフォールバックも UpdateThumb の要求単位ハンドラで処理する。

        // Center-look glow frame for the flight morph (hidden at rest).
        Image glow = NewImage("GlowFrame", p.rect, new Color(Cyan.r, Cyan.g, Cyan.b, 0f));
        glow.sprite = glowFrameSprite;
        glow.type = Image.Type.Sliced;
        Stretch(glow.rectTransform);
        SetInset(glow.rectTransform, -GlowMargin);
        p.glowFrame = glow;

        // Decor (brackets + in-card title + arrow) sits in its own CanvasGroup so
        // it can fade out while the panel flies into the center slot.
        GameObject decorGO = new GameObject("Decor", typeof(RectTransform));
        decorGO.transform.SetParent(p.rect, false);
        RectTransform decorR = (RectTransform)decorGO.transform;
        Stretch(decorR);
        p.decorCG = decorGO.AddComponent<CanvasGroup>();

        p.brackets = BuildBrackets(decorR);

        // 統一様式(2026-07-11): パネル外周に銀エッジ(リザルト/引き継ぎパネルと
        // 同じ要素)。Decor 内に置き、中央への飛行モーフでブラケットと一緒に消える。
        p.silverRim = BuildRim(decorR);
        SetRim(p.silverRim, SilverEdge, 2f);

        // Stage name inside the top band of the card (per mockup).
        p.title = NewText("Title", decorR, "", 30f, Cyan, TextAlignmentOptions.Center);
        RectTransform tr = (RectTransform)p.title.transform;
        tr.anchorMin = new Vector2(0f, 1f);
        tr.anchorMax = new Vector2(1f, 1f);
        tr.pivot = new Vector2(0.5f, 1f);
        tr.anchoredPosition = new Vector2(0f, -6f);
        tr.sizeDelta = new Vector2(0f, SideTitleBand - 10f);

        // 矢印背後の発光ハロー(先に生成=矢印の背面)。SoftCircleGraphic の淡い円で、
        // 白い矢印を柔らかく発光させる。位置は SetPanelSide で矢印に合わせる。
        GameObject aglowGO = new GameObject("ArrowGlow", typeof(RectTransform));
        aglowGO.transform.SetParent(decorR, false);
        p.arrowGlow = (RectTransform)aglowGO.transform;
        p.arrowGlow.pivot = new Vector2(0.5f, 0.5f);
        // グロー減光の追加指摘(2026-07-14 夜)でハローも一回り小さく(104→92)。
        p.arrowGlow.sizeDelta = new Vector2(92f, 92f);
        SoftCircleGraphic aglow = aglowGO.AddComponent<SoftCircleGraphic>();
        aglow.color = ArrowGlowColor;
        aglow.raycastTarget = false;

        // 山括弧型の矢印。サムネイル外側の縁に上下中央で重ね、カルーセルの進行方向を
        // 示す。2026-07-13 指摘「見づらい」→ 白・不透明・やや大きく(60pt)し、背後の
        // ハローで発光させる(端36px は維持)。
        p.arrow = NewText("Arrow", decorR, ">", 60f, ArrowWhite, TextAlignmentOptions.Center);
        RectTransform ar2 = (RectTransform)p.arrow.transform;
        ar2.pivot = new Vector2(0.5f, 0.5f);
        ar2.sizeDelta = new Vector2(64f, 90f);
        return p;
    }

    // Assigns which side of the screen a pooled panel currently plays (arrow
    // glyph + arrow placement). Called on build and after each role rotation.
    private void SetPanelSide(SidePanel p, int side)
    {
        p.side = side;
        if (p.arrow == null) return;
        p.arrow.text = side < 0 ? "<" : ">";
        RectTransform ar = (RectTransform)p.arrow.transform;
        // カードの内側(中央カード寄り)のエッジに寄せる。中央カード本体
        // (エッジ±468)に隠れない範囲で、サイドカードと中央カードの間に見せる。
        ar.anchorMin = ar.anchorMax = new Vector2(side < 0 ? 1f : 0f, 0.5f);
        ar.anchoredPosition = new Vector2(side < 0 ? -36f : 36f, 0f);
        TmpAlign.CenterInkVertically(p.arrow);
        // 発光ハローを矢印の中心へ追従させる。
        if (p.arrowGlow != null)
        {
            p.arrowGlow.anchorMin = p.arrowGlow.anchorMax = ar.anchorMin;
            p.arrowGlow.anchoredPosition = ar.anchoredPosition;
        }
    }

    // ステージ名スラッシュの x をインク幅に追従させる(名前は可変長)。
    // 距離感はボタンの「枠のすぐ外」規則に合わせ、細=+24 / 太=+42。
    private void UpdateStageNameSlashes()
    {
        if (stageNameText == null || stageNameSlashes == null) return;
        stageNameText.ForceMeshUpdate();
        float half = stageNameText.preferredWidth * 0.5f;
        float thinX = half + 24f;
        float thickX = half + 42f;
        stageNameSlashes[0].rectTransform.anchoredPosition = new Vector2(-thickX, 0f);
        stageNameSlashes[1].rectTransform.anchoredPosition = new Vector2(-thinX, 0f);
        stageNameSlashes[2].rectTransform.anchoredPosition = new Vector2(thinX, 0f);
        stageNameSlashes[3].rectTransform.anchoredPosition = new Vector2(thickX, 0f);
    }

    // ステージ名スラッシュの alpha を名前のフェードに同期させる。
    private void SetStageNameSlashAlpha(float a)
    {
        if (stageNameSlashes == null) return;
        for (int i = 0; i < stageNameSlashes.Length; i++)
        {
            if (stageNameSlashes[i] == null) continue;
            Color c = Color.white;
            c.a = (i == 1 || i == 2 ? 0.5f : 1f) * a;
            stageNameSlashes[i].color = c;
        }
    }

    // Four white corner brackets (2 strips per corner), per the mockup side cards.
    private Image[] BuildBrackets(RectTransform parent)
    {
        const float arm = 26f;
        const float th = 3f;
        Image[] arr = new Image[8];
        for (int c = 0; c < 4; c++)
        {
            float ax = (c & 1) == 0 ? 0f : 1f;   // left / right
            float ay = (c & 2) == 0 ? 1f : 0f;   // top / bottom
            Vector2 anchor = new Vector2(ax, ay);

            Image h = NewImage("BracketH", parent, BracketColor);
            RectTransform hr = h.rectTransform;
            hr.anchorMin = hr.anchorMax = anchor;
            hr.pivot = anchor;
            hr.sizeDelta = new Vector2(arm, th);
            hr.anchoredPosition = Vector2.zero;

            Image v = NewImage("BracketV", parent, BracketColor);
            RectTransform vr = v.rectTransform;
            vr.anchorMin = vr.anchorMax = anchor;
            vr.pivot = anchor;
            vr.sizeDelta = new Vector2(th, arm);
            vr.anchoredPosition = Vector2.zero;

            arr[c * 2] = h;
            arr[c * 2 + 1] = v;
        }
        return arr;
    }

    private static void SetBracketAlpha(Image[] brackets, float a)
    {
        if (brackets == null) return;
        Color c = BracketColor;
        c.a *= a;
        for (int i = 0; i < brackets.Length; i++)
        {
            if (brackets[i] != null) brackets[i].color = c;
        }
    }

    // 4辺の帯(上/下/左/右)で細い枠線を作る。面で塗らないので CanvasGroup の
    // 中間 alpha でも中身の下から枠色が透けない。
    private Image[] BuildRim(RectTransform parent)
    {
        Image[] rim = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            Image s = NewImage("Rim" + i, parent, ThumbRimColor);
            RectTransform rt = s.rectTransform;
            switch (i)
            {
                case 0: rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f); break;   // top
                case 1: rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f); rt.pivot = new Vector2(0.5f, 0f); break;   // bottom
                case 2: rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 0.5f); break;   // left
                default: rt.anchorMin = new Vector2(1f, 0f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(1f, 0.5f); break;  // right
            }
            rt.anchoredPosition = Vector2.zero;
            rim[i] = s;
        }
        return rim;
    }

    // 帯4本の色と太さをまとめて更新する(遷移中の補間もここを通す)。
    private static void SetRim(Image[] rim, Color color, float thickness)
    {
        if (rim == null) return;
        for (int i = 0; i < rim.Length; i++)
        {
            if (rim[i] == null) continue;
            rim[i].color = color;
            RectTransform rt = rim[i].rectTransform;
            // 縦帯は上下の横帯ぶんを避けて角の二重描画を防ぐ。
            rt.sizeDelta = i < 2 ? new Vector2(0f, thickness) : new Vector2(thickness, -2f * thickness);
        }
    }

    // t=1 -> side-card layout (title band on top, thumbnail margins),
    // t=0 -> near full-bleed center layout. Shared by the center media wrapper
    // and the side thumb areas so the two looks morph into each other exactly.
    private static void SetMediaInsets(RectTransform r, float t)
    {
        float side = Mathf.Lerp(4f, SideThumbInset, t);
        r.offsetMin = new Vector2(side, Mathf.Lerp(4f, 16f, t));
        r.offsetMax = new Vector2(-side, -Mathf.Lerp(4f, SideTitleBand, t));
    }

    private Sprite ringSprite;
    private Sprite dotSprite;

    private void BuildProgressIndicator(RectTransform root)
    {
        // Container only; the dots are (re)built once the stage count is known so
        // the row can be centered under the card with one node per stage.
        progressRow = new GameObject("Progress", typeof(RectTransform)).GetComponent<RectTransform>();
        progressRow.SetParent(root, false);
        progressRow.anchorMin = progressRow.anchorMax = new Vector2(0.5f, 0.5f);
        progressRow.pivot = new Vector2(0.5f, 0.5f);
        progressRow.sizeDelta = new Vector2(0f, 60f);
        // ユーザー要望(2026-07-14「下の主人公の動くところ、もうちょっと下にして大きくして」):
        // プログレス行(主人公マーカーが歩く帯)全体をさらに下げ、マーカーを拡大する。
        // 下オフセットを 72→126(約54px下げ)。下部ヒントバー(上端 y≈-464)とは
        // マーカー足元 y≈-380 で約84px の余裕を確保して衝突しない。
        progressRow.anchoredPosition = new Vector2(0f, CenterSlotPos.y - CenterSlotSize.y * 0.5f - 126f);
        ringSprite = CreateRingSprite();
        dotSprite = CreateDotSprite();

        // Persistent player marker; RefreshProgress never destroys it so its
        // position can tween smoothly between nodes.
        // 選択画面下部の主人公マーカーは専用アート(探偵の立ち絵 Resources/UI/hero_select)を
        // 優先で使う。無ければ従来どおりプレイヤースプライトへフォールバック(2026-07-14 差替)。
        Sprite markerSprite = Resources.Load<Sprite>("UI/hero_select");
        if (markerSprite == null) markerSprite = playerSprite;
        Image marker = NewImage("Marker", progressRow, Color.white);
        if (markerSprite != null)
        {
            marker.sprite = markerSprite;
            marker.preserveAspect = true;
            Rect sr = markerSprite.rect;
            // 主人公マーカーを拡大(40→56, 約1.4倍)。MarkerY(=h/2=28)と対で足元が
            // rail に乗る。ユーザー要望「大きくして」。
            float h = 56f;
            float scale = sr.height <= 32f ? Mathf.Max(1f, Mathf.Floor(h / sr.height)) : h / sr.height;
            marker.rectTransform.sizeDelta = new Vector2(sr.width * scale, sr.height * scale);
        }
        else
        {
            marker.color = Cyan;
            marker.rectTransform.sizeDelta = new Vector2(26f, 26f);
        }
        marker.rectTransform.anchorMin = marker.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        marker.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        markerRect = marker.rectTransform;
    }

    // Rebuilds the node chain: hollow rings on a single navy rail (no dashes), one
    // node per stage; the current stage is a bigger filled cyan dot and non-current
    // rings are dimmed. The persistent player marker stands on the line and tweens.
    private void RefreshProgress(bool animate)
    {
        if (progressRow == null) return;
        for (int i = progressRow.childCount - 1; i >= 0; i--)
        {
            Transform child = progressRow.GetChild(i);
            if (markerRect != null && child == markerRect) continue;
            Destroy(child.gameObject);
        }

        int n = Mathf.Max(1, totalStages);
        const float nodeSize = 26f;
        const float dotSize = 32f;   // filled current-stage dot (bigger, per mockup)
        const float dashGap = 66f;   // center-to-center spacing between nodes
        float totalW = (n - 1) * dashGap;
        float x0 = -totalW * 0.5f;

        // 背面に紺の1本レール(破線ティックは廃止し控えめに統一。oracle 案)。
        // 列の端ノードから端ノードまでを1本で繋ぐ。
        if (n > 1)
        {
            Image rail = NewImage("Rail", progressRow, new Color(0.09f, 0.19f, 0.33f, 0.9f));
            rail.rectTransform.anchorMin = rail.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rail.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rail.rectTransform.sizeDelta = new Vector2(totalW, 4f);
            rail.rectTransform.anchoredPosition = Vector2.zero;
            rail.rectTransform.SetAsFirstSibling(); // ノードより背面へ
        }

        // 非選択ノードは減光(現在ノードだけ明るいシアンで際立たせる)。
        Color nodeDim = new Color(Cyan.r, Cyan.g, Cyan.b, 0.4f);
        for (int i = 0; i < n; i++)
        {
            float x = x0 + i * dashGap;
            bool current = i == currentIndex;
            Image node = NewImage(current ? "NodeCurrent" : "Node", progressRow, current ? Cyan : nodeDim);
            node.sprite = current ? dotSprite : ringSprite;
            node.type = Image.Type.Simple;
            node.rectTransform.anchorMin = node.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            node.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            node.rectTransform.sizeDelta = Vector2.one * (current ? dotSize : nodeSize);
            node.rectTransform.anchoredPosition = new Vector2(x, 0f);
        }

        markerToX = x0 + currentIndex * dashGap;
        if (markerRect != null)
        {
            markerRect.SetAsLastSibling(); // draw above rings while sliding over them
            if (animate && !Mathf.Approximately(markerRect.anchoredPosition.x, markerToX))
            {
                markerFromX = markerRect.anchoredPosition.x;
                markerTweenTime = 0f;
            }
            else
            {
                markerTweenTime = -1f;
                markerRect.anchoredPosition = new Vector2(markerToX, MarkerY);
            }
        }
    }

    private void BuildHintBar(RectTransform root)
    {
        Image bar = NewImage("HintBar", root, new Color(0.02f, 0.05f, 0.08f, 0.95f));
        RectTransform br = bar.rectTransform;
        br.anchorMin = new Vector2(0f, 0f);
        br.anchorMax = new Vector2(1f, 0f);
        br.pivot = new Vector2(0.5f, 0f);
        br.anchoredPosition = Vector2.zero;
        br.sizeDelta = new Vector2(0f, 76f);

        Image topLine = NewImage("HintBarLine", root, Cyan);
        RectTransform tlr = topLine.rectTransform;
        tlr.anchorMin = new Vector2(0f, 0f);
        tlr.anchorMax = new Vector2(1f, 0f);
        tlr.pivot = new Vector2(0.5f, 0f);
        tlr.anchoredPosition = new Vector2(0f, 76f);
        tlr.sizeDelta = new Vector2(0f, 2f);

        // 実機はジョイスティック+ボタンを想定(2026-07-13 指摘)。キー表記(←→/SPACE/
        // ESC/V)をやめ、スティックとボタンだけで案内する。スタイル切替(V)は実機に
        // 対応キーが無いので撤去。戻る(ESC)はシリアル未配線のため案内から外す
        // (最終報告の監査項目: 2 個目のボタンを戻るに割り当てれば復活可能)。
        BuildHintRowAt(br, 0f, 0f, 48f, 0f, new[]
        {
            new HintItem(new[] { "スティック" }, "選択"),
            new HintItem(new[] { "ボタン" }, "決定"),
        });
    }

    // ---- Key-cap hint rows (shared by the bottom bar and the difficulty overlay) ----

    private readonly struct HintItem
    {
        public readonly string[] Keys;
        public readonly string Label;
        public HintItem(string[] keys, string label) { Keys = keys; Label = label; }
    }

    // Lays out a horizontal row of "[key][key] label" groups using nested layout
    // groups so the row self-sizes regardless of content width.
    // anchorX/pivotX let the row hang from the left (0), center (0.5) or right (1).
    private void BuildHintRowAt(RectTransform parent, float anchorX, float pivotX, float x, float y, HintItem[] items)
    {
        RectTransform row = NewLayoutRow("HintRow", parent, 48f, 4f, TextAnchor.MiddleCenter);
        row.anchorMin = row.anchorMax = new Vector2(anchorX, 0.5f);
        row.pivot = new Vector2(pivotX, 0.5f);
        row.anchoredPosition = new Vector2(x, y);
        HorizontalLayoutGroup outer = row.GetComponent<HorizontalLayoutGroup>();
        outer.spacing = 40f;

        foreach (HintItem item in items)
        {
            RectTransform group = NewLayoutRow("Hint_" + item.Label, row, 48f, 10f, TextAnchor.MiddleCenter);
            foreach (string key in item.Keys)
            {
                NewKeyCap(group, key, 44f, 28f);
            }
            // ラベルはレイアウトグループが位置を管理するため、直接ではなく
            // コンテナを介して置き、内側の TMP をインク実測で光学中央へ寄せる
            // (日本語がフォールバックフォントの行メトリクスで上に乗る対策)。
            GameObject labelBox = new GameObject("LabelBox", typeof(RectTransform));
            labelBox.transform.SetParent(group, false);
            RectTransform labelBoxRect = (RectTransform)labelBox.transform;
            TMP_Text label = NewText("Label", labelBoxRect, item.Label, 28f, Cyan, TextAlignmentOptions.Left);
            AddLayoutElement(labelBoxRect, label.GetPreferredValues().x, 44f);
            Stretch((RectTransform)label.transform);
            TmpAlign.CenterInkVertically(label);
        }
    }

    // キーキャップは下部バー内の隣接要素(ラベル文字・上辺ライン)と同じ
    // アクセントシアン(#38C2E0)に統一する。トップバーの青(#4290DB)は面の
    // ブランド色で、チップは操作アクセント側なので、隣で並ぶラベルの色相に
    // 合わせないと2色の青が並んで濁って見える。縁=ラベルと同一のシアン、
    // 地=白文字が読めるよう同色相を一段暗くしたもの。
    private static readonly Color KeyCapBlue = new Color(0.13f, 0.46f, 0.55f, 1f);
    private static readonly Color KeyCapEdge = new Color(0.22f, 0.76f, 0.878f, 1f);

    private RectTransform NewKeyCap(RectTransform parent, string label, float height, float fontSize)
    {
        Image border = NewImage("Key_" + label, parent, KeyCapEdge);
        RectTransform br = border.rectTransform;

        Image fill = NewImage("Fill", br, KeyCapBlue);
        RectTransform fr = fill.rectTransform;
        fr.anchorMin = Vector2.zero;
        fr.anchorMax = Vector2.one;
        fr.offsetMin = new Vector2(2f, 2f);
        fr.offsetMax = new Vector2(-2f, -2f);

        TMP_Text t = NewText("L", br, label, fontSize, Color.white, TextAlignmentOptions.Center);
        // チップ幅は文字の実プリファード幅+左右パディングで確保する。旧実装の
        // 「24+文字数*fontSize*0.66」はラテン字送り(0.66em)を仮定しており、全角
        // カタカナ(スティック/ボタン)は約 1em 送りのため過小に見積もられ、
        // Center+Overflow の文字がチップ枠(青地)からはみ出していた
        // (2026-07-14 指摘「下部テキストが黒背景から切れる」)。font(CJK
        // フォールバック付き)割当済みの t を GetPreferredValues で実測して枠を合わせる。
        const float padX = 28f;
        float textW = t.GetPreferredValues(label).x;
        float width = Mathf.Max(height, textW + padX);
        br.sizeDelta = new Vector2(width, height);
        AddLayoutElement(br, width, height);

        Stretch((RectTransform)t.transform);
        // CJK フォールバックの行メトリクスで文字が上に乗るため、インク実測で光学中央へ。
        TmpAlign.CenterInkVertically(t);
        return br;
    }

    private RectTransform NewLayoutRow(string name, Transform parent, float height, float spacing, TextAnchor align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(0f, height);
        HorizontalLayoutGroup h = go.AddComponent<HorizontalLayoutGroup>();
        h.childAlignment = align;
        h.spacing = spacing;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;
        ContentSizeFitter fit = go.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        return rt;
    }

    private static void AddLayoutElement(RectTransform rt, float width, float height)
    {
        LayoutElement le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        le.minWidth = width;
        le.preferredHeight = height;
        le.minHeight = height;
    }

    // ---- In-screen difficulty overlay ----

    private void BuildDifficultyOverlay(RectTransform root)
    {
        GameObject rootGO = new GameObject("DifficultyOverlay", typeof(RectTransform));
        rootGO.transform.SetParent(root, false);
        diffRoot = (RectTransform)rootGO.transform;
        Stretch(diffRoot);
        diffCG = rootGO.AddComponent<CanvasGroup>();

        // Frozen, blurred snapshot of the carousel behind the modal.
        diffBlur = NewRawImage("DiffBlur", diffRoot, null);
        Stretch(diffBlur.rectTransform);
        diffBlur.color = new Color(0.55f, 0.62f, 0.72f, 1f); // slight dim on the snapshot

        // Dark wash for text contrast.
        diffScrim = NewImage("DiffScrim", diffRoot, new Color(0.01f, 0.03f, 0.06f, 0.55f));
        Stretch(diffScrim.rectTransform);

        // The panel itself is a clone of the default (style 0) difficulty column so
        // the design and its animations (white brackets, staggered rows, blinking
        // prompt) are exactly the original component's. Only the backdrop (frozen
        // blur + scrim) is JSAB-specific.
        Transform canvases = transform.parent;
        Transform src = canvases != null ? canvases.Find("StageCanvas/StageBoxParent/DefficultyBar") : null;
        if (src != null)
        {
            GameObject clone = Instantiate(src.gameObject, diffRoot);
            clone.name = "DifficultyColumn";
            clone.SetActive(true);
            diffPanel = (RectTransform)clone.transform;
            diffPanel.anchorMin = diffPanel.anchorMax = new Vector2(0.5f, 0.5f);
            diffPanel.pivot = new Vector2(0.5f, 0.5f);
            // y は行群が背後のぼかしカードに対して視覚センターへ乗る値
            // (ボタン縦拡大 140/180 の際、+30 だと行群が上寄りに見えると
            // oracle レビュー指摘 → 14px 下げ)。
            diffPanel.anchoredPosition = new Vector2(0f, 16f);
            diffPanel.localScale = Vector3.one * DiffPanelBaseScale; // matches the original's on-screen scale
            diffBar = clone.GetComponent<DefficultyBar>();
            if (diffBar != null)
            {
                diffBar.Init(font);
                diffBar.SetAlpha(1f);
                diffBar.SetEntranceProgress(1f);
            }
            // Mouse hit areas = the visible bar sprites (660x124), not the tiny row roots.
            string[] rows = { "Easy", "Normal", "Lunatic" };
            for (int i = 0; i < rows.Length; i++)
                diffBoxRects[i] = clone.transform.Find("List/" + rows[i] + "/StageBar") as RectTransform;
        }

        diffRoot.gameObject.SetActive(false);
    }

    // ---- Public difficulty API (driven by StageSelectManager) ----

    // 上部バー(タイマー/残り時間)を diffRoot より前面へ持ち上げる。難易度モーダルの
    // ぼかし+暗幕が制限時間を覆っていた指摘(2026-07-12)への対応。クローンは Tick で
    // 生タイマーをミラーし続けるので、前面に出せば残り時間がそのまま読める。
    private void RaiseTopBar()
    {
        if (topBarRaised) return;
        // 元の並び順は必ず「両方を動かす前」に保存する。base を先に SetAsLastSibling
        // すると、その後ろにあった text の sibling index が 1 つ繰り上がり、text の
        // 保存値が狂う。すると RestoreTopBar で text が base(不透明な帯)より後ろ=
        // 最背面へ戻り、上部テキスト(曲名/残り時間)が帯の裏に隠れて消える不具合に
        // なる(2026-07-13 修正: 難易度から Esc で戻ると上のテキストが消える)。
        if (topBarBaseRoot != null) topBarBaseSiblingIndex = topBarBaseRoot.GetSiblingIndex();
        if (topBarTextRoot != null) topBarTextSiblingIndex = topBarTextRoot.GetSiblingIndex();
        if (topBarBaseRoot != null) topBarBaseRoot.SetAsLastSibling();
        if (topBarTextRoot != null) topBarTextRoot.SetAsLastSibling(); // テキスト層を最前面(ベースの上)に
        topBarRaised = true;
    }

    private void RestoreTopBar()
    {
        if (!topBarRaised) return;
        if (topBarBaseRoot != null && topBarBaseSiblingIndex >= 0)
            topBarBaseRoot.SetSiblingIndex(topBarBaseSiblingIndex);
        if (topBarTextRoot != null && topBarTextSiblingIndex >= 0)
            topBarTextRoot.SetSiblingIndex(topBarTextSiblingIndex);
        topBarRaised = false;
    }

    public void OpenDifficulty()
    {
        if (diffRoot == null) return;
        difficultyOpen = true;
        diffOpenTime = Time.unscaledTime;
        mouseConfirm = false;
        if (diffBar != null)
        {
            ApplyDifficultyAvailability();     // 石工/浮浪者は EASY/LUNATIC を選択不可にする
            diffBar.ResetSelection(1);         // default NORMAL each time it opens
        }
        if (diffFadeCo != null) { StopCoroutine(diffFadeCo); diffFadeCo = null; }
        // ぼかしスナップショットが用意できるまでは全体を透明にしておき、準備完了
        // 後に CaptureBlurBackground がフェードインを開始する(急な表示を防ぐ)。
        if (diffCG != null) diffCG.alpha = 0f;
        if (diffPanel != null) diffPanel.localScale = Vector3.one * (DiffPanelBaseScale * 0.96f);
        diffRoot.gameObject.SetActive(true);
        RaiseTopBar();
        StartCoroutine(CaptureBlurBackground());
    }

    // 現在のステージに応じて選択可能な難易度を絞る。石工・浮浪者は EASY/LUNATIC が
    // 未完成のため NORMAL のみ選択可(グレーアウト+COMING SOON)。艦長は3難易度とも
    // 実データがあるので従来どおり。姿見(mirror)は endTime=0 の WIP=全難易度 COMING SOON
    // にして確定不可にする(CanConfirm でゲーム開始をブロック)。
    private void ApplyDifficultyAvailability()
    {
        if (diffBar == null) return;
        string dir = GetStage(currentIndex)?.stageDirectoryName;
        if (dir == "mirror") diffBar.SetEnabledMask(false, false, false);
        else if (dir == "stone" || dir == "vagrant") diffBar.SetEnabledMask(false, true, false);
        else diffBar.SetEnabledMask(true, true, true);
    }

    // 現在選択中の難易度が実際に確定可能か。姿見(WIP)は全行 COMING SOON なので false を
    // 返し、決定キー/時間切れによるゲーム開始をブロックする(endTime=0 の強制起動で
    // BulletRenderSystem が落ちるのを防ぐ)。マウスは元々無効行を確定できない。
    public bool CanConfirm()
    {
        return diffBar != null && diffBar.IsRowEnabled(diffBar.index);
    }

    public void CloseDifficulty()
    {
        difficultyOpen = false;
        RestoreTopBar();
        if (diffRoot == null || !diffRoot.gameObject.activeSelf) return;
        RestoreDifficultyExit();
        if (diffFadeCo != null) { StopCoroutine(diffFadeCo); diffFadeCo = null; }
        // 開く時と対称のフェードアウト。完了後に非アクティブ化する。
        if (gameObject.activeInHierarchy) diffFadeCo = StartCoroutine(FadeDifficulty(false));
        else diffRoot.gameObject.SetActive(false);
    }

    // プレイ決定時の退場: 選択行が白フラッシュ+小ポップの一拍を置いて先頭で
    // 右へ飛び去り、残りの行が追従する(タイトルの PlayStartExit と同系)。
    // 完了後にホワイトアウトが始まる前提なので、行が画面外へ出た時点で返る。
    public async Task PlayDifficultyExit()
    {
        if (diffRoot == null || !diffRoot.gameObject.activeSelf || diffPanel == null || diffExiting) return;
        diffExiting = true;

        const float flashDur = 0.10f;
        const float rowDur = 0.18f;
        const float slideDistance = 1700f;
        const float exitTotal = 0.32f;

        string[] rowNames = { "Easy", "Normal", "Lunatic" };
        for (int i = 0; i < rowNames.Length; i++)
        {
            exitRowRects[i] = diffPanel.Find("List/" + rowNames[i]) as RectTransform;
            if (exitRowRects[i] == null) continue;
            exitRowBasePos[i] = exitRowRects[i].anchoredPosition;
            exitRowBaseScale[i] = exitRowRects[i].localScale;
        }
        exitWhiteRect = diffPanel.Find("White") as RectTransform;
        exitWhiteBasePos = exitWhiteRect != null ? exitWhiteRect.anchoredPosition : Vector2.zero;

        int selected = Mathf.Clamp(DifficultyIndex, 0, 2);
        exitBarImg = exitRowRects[selected] != null
            ? exitRowRects[selected].Find("StageBar")?.GetComponent<Image>()
            : null;
        exitBarColor = exitBarImg != null ? exitBarImg.color : Color.white;
        exitFlash = GetOrCreateRowFlash(exitRowRects[selected]);
        TMP_Text selectedLabel = exitRowRects[selected] != null
            ? exitRowRects[selected].Find("StageName")?.GetComponent<TMP_Text>()
            : null;
        Color labelColor = selectedLabel != null ? selectedLabel.color : Color.white;
        // 見出し・説明文・プロンプト(ルビ含む)・装飾ラインは行より先にすっと消す。
        // 説明文の漢字直上ルビ(DescText の子 DescRuby0..2)も一緒に消す。
        exitFadeTexts = new[]
        {
            diffPanel.Find("Title")?.GetComponent<TMP_Text>(),
            diffPanel.Find("TitleRubyN")?.GetComponent<TMP_Text>(),
            diffPanel.Find("TitleRubyS")?.GetComponent<TMP_Text>(),
            diffPanel.Find("DescText")?.GetComponent<TMP_Text>(),
            diffPanel.Find("DescText/DescRuby0")?.GetComponent<TMP_Text>(),
            diffPanel.Find("DescText/DescRuby1")?.GetComponent<TMP_Text>(),
            diffPanel.Find("DescText/DescRuby2")?.GetComponent<TMP_Text>(),
            diffPanel.Find("Prompt")?.GetComponent<TMP_Text>(),
            diffPanel.Find("PromptRubyO")?.GetComponent<TMP_Text>(),
            diffPanel.Find("PromptRubyK")?.GetComponent<TMP_Text>(),
        };
        exitFadeTextAlphas = new float[exitFadeTexts.Length];
        for (int i = 0; i < exitFadeTexts.Length; i++)
        {
            exitFadeTextAlphas[i] = exitFadeTexts[i] != null ? exitFadeTexts[i].alpha : 1f;
        }
        exitFadeImages = new[]
        {
            diffPanel.Find("LineT")?.GetComponent<Image>(),
            diffPanel.Find("LineB")?.GetComponent<Image>(),
        };
        exitFadeImageAlphas = new float[exitFadeImages.Length];
        for (int i = 0; i < exitFadeImages.Length; i++)
        {
            exitFadeImageAlphas[i] = exitFadeImages[i] != null ? exitFadeImages[i].color.a : 1f;
        }

        void StepExit(float time)
        {
            // 決定の一拍: 選択バナーが白く光って元色へ戻る(文字は白地に飛ば
            // ないようネイビーへ反転してから戻す)。
            float flashP = Mathf.Clamp01(time / flashDur);
            if (exitBarImg != null) exitBarImg.color = Color.Lerp(Color.white, exitBarColor, flashP * flashP);
            if (exitFlash != null)
            {
                Color fc = Color.white;
                fc.a = 1f - flashP * flashP;
                exitFlash.color = fc;
            }
            if (selectedLabel != null) selectedLabel.color = Color.Lerp(Navy, labelColor, flashP * flashP);

            float fadeKeep = 1f - Mathf.Clamp01(time / 0.12f);
            for (int i = 0; i < exitFadeTexts.Length; i++)
            {
                if (exitFadeTexts[i] != null)
                {
                    exitFadeTexts[i].alpha = Mathf.Min(exitFadeTexts[i].alpha, exitFadeTextAlphas[i] * fadeKeep);
                }
            }
            for (int i = 0; i < exitFadeImages.Length; i++)
            {
                if (exitFadeImages[i] == null) continue;
                Color ic = exitFadeImages[i].color;
                ic.a = exitFadeImageAlphas[i] * fadeKeep;
                exitFadeImages[i].color = ic;
            }

            // 行スライドアウト: ease-in cubic で緩→急。選択行が先頭で飛び出し、
            // 白ブラケットは選択行と一体で飛ぶ。
            for (int i = 0; i < exitRowRects.Length; i++)
            {
                if (exitRowRects[i] == null) continue;
                float delay = i == selected ? 0.03f : 0.08f + 0.03f * i;
                float p = Mathf.Clamp01((time - delay) / rowDur);
                float x = p * p * p * slideDistance;
                exitRowRects[i].anchoredPosition = exitRowBasePos[i] + new Vector2(x, 0f);
                if (i == selected)
                {
                    float pop = Mathf.Sin(flashP * Mathf.PI) * 0.06f;
                    exitRowRects[i].localScale = exitRowBaseScale[i] * (1f + pop);
                    if (exitWhiteRect != null)
                    {
                        exitWhiteRect.anchoredPosition = exitWhiteBasePos + new Vector2(x, 0f);
                    }
                }
            }
        }

        // 選択行が抜け切った時点(0.18s)で制御を返し、残りの行はホワイトアウト
        // と重ねて飛ばせる(oracle 第30便: スライド→白の間で勢いが一瞬途切れる
        // のを防ぎ、「決定の衝撃で白に飲み込まれる」つなぎにする)。
        const float exitReturnTime = 0.18f;
        float t = 0f;
        while (t < exitReturnTime)
        {
            t += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            StepExit(t);
            await Task.Yield();
            if (this == null) return;
        }

        async void ContinueExit()
        {
            float time = t;
            while (time < exitTotal)
            {
                time += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
                // 白カバー完了後に CloseDifficulty が状態を復元したら手を引く。
                if (this == null || !diffExiting) return;
                StepExit(time);
                await Task.Yield();
            }
        }
        ContinueExit();
    }

    // 退場で動かした行/ブラケット/色を元へ戻す(ホワイトアウトで覆われた後の
    // CloseDifficulty から呼ばれるので見た目には出ない)。次回 OpenDifficulty の
    // ResetSelection が alpha/scale/説明文を整えるため、ここは位置と色だけ戻す。
    private void RestoreDifficultyExit()
    {
        if (!diffExiting) return;
        diffExiting = false;
        for (int i = 0; i < exitRowRects.Length; i++)
        {
            if (exitRowRects[i] == null) continue;
            exitRowRects[i].anchoredPosition = exitRowBasePos[i];
            exitRowRects[i].localScale = exitRowBaseScale[i];
        }
        if (exitWhiteRect != null) exitWhiteRect.anchoredPosition = exitWhiteBasePos;
        if (exitBarImg != null) exitBarImg.color = exitBarColor;
        if (exitFlash != null) exitFlash.gameObject.SetActive(false);
        for (int i = 0; i < exitFadeTexts.Length; i++)
        {
            if (exitFadeTexts[i] != null) exitFadeTexts[i].alpha = exitFadeTextAlphas[i];
        }
        for (int i = 0; i < exitFadeImages.Length; i++)
        {
            if (exitFadeImages[i] == null) continue;
            Color ic = exitFadeImages[i].color;
            ic.a = exitFadeImageAlphas[i];
            exitFadeImages[i].color = ic;
        }
        if (diffBar != null) diffBar.ResetSelection(diffBar.index);
    }

    // 決定フラッシュ用の白オーバーレイ(遅延生成)。統一様式ボタンの焼き込み
    // 枠と同じ外形(583x109 の枠: 上下11/左右22 内側・19° 斜辺)の平行四辺形。
    // 文字のネイビー反転を見せるため StageName の下に挿す。
    private ParallelogramGraphic GetOrCreateRowFlash(RectTransform row)
    {
        if (row == null) return null;
        Transform existing = row.Find("ExitFlash");
        ParallelogramGraphic flash;
        if (existing != null)
        {
            flash = existing.GetComponent<ParallelogramGraphic>();
        }
        else
        {
            float hw = 583f * 0.5f - 22f;
            float hh = 109f * 0.5f - 11f;
            float skew = 2f * hh * Mathf.Tan(UiButtonStyle.SlashAngleDeg * Mathf.Deg2Rad);
            GameObject go = new GameObject("ExitFlash", typeof(RectTransform), typeof(CanvasRenderer), typeof(ParallelogramGraphic));
            go.layer = row.gameObject.layer;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(row, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(hw * 2f, hh * 2f);
            Transform nameLabel = row.Find("StageName");
            if (nameLabel != null) rect.SetSiblingIndex(nameLabel.GetSiblingIndex());
            flash = go.GetComponent<ParallelogramGraphic>();
            flash.Slant = skew;
            flash.SlantRightEdge = true;
            flash.raycastTarget = false;
        }
        Color init = Color.white;
        init.a = 0f;
        flash.color = init;
        flash.gameObject.SetActive(true);
        return flash;
    }

    // 開閉共通のフェード: alpha トゥイーン+パネルの軽いスケール(0.96→1.0)。
    // スケールは常に現在 alpha に追随させるので、開閉が途中で切り替わっても連続。
    private IEnumerator FadeDifficulty(bool opening)
    {
        float from = diffCG != null ? diffCG.alpha : (opening ? 0f : 1f);
        float to = opening ? 1f : 0f;
        float t0 = Time.unscaledTime;
        while (true)
        {
            float p = Mathf.Clamp01((Time.unscaledTime - t0) / DiffFadeDuration);
            float e = 1f - Mathf.Pow(1f - p, 3f);
            float a = Mathf.Lerp(from, to, e);
            if (diffCG != null) diffCG.alpha = a;
            if (diffPanel != null) diffPanel.localScale = Vector3.one * (DiffPanelBaseScale * (0.96f + 0.04f * a));
            if (p >= 1f) break;
            yield return null;
        }
        diffFadeCo = null;
        if (!opening && diffRoot != null) diffRoot.gameObject.SetActive(false);
    }

    public void MoveDifficulty(int dir)
    {
        if (!difficultyOpen || diffBar == null) return;
        if (dir > 0) diffBar.Down();
        else diffBar.Up();
    }

    // Freezes the current screen into a downsampled (thus blurred) snapshot behind
    // the modal. Captured with our own overlay children hidden so only the carousel
    // shows through.
    private IEnumerator CaptureBlurBackground()
    {
        if (diffBlur != null) diffBlur.enabled = false;
        if (diffScrim != null) diffScrim.enabled = false;
        if (diffPanel != null) diffPanel.gameObject.SetActive(false);

        yield return new WaitForEndOfFrame();

        Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
        if (blurRT != null) { blurRT.Release(); Destroy(blurRT); }
        // 1/4 解像度で保持しつつ、2x ずつの縮小を 1/16 まで積んでから 2x ずつ
        // 戻す(ダウンサンプルピラミッド)。一足飛びの縮小はサンプル飛ばしで
        // モザイク状のジャギーになるため、全段をバイリニア 2x2 平均で畳んで
        // ガウシアン近似の滑らかなぼけを作る。
        int w4 = Mathf.Max(24, shot.width / 4);
        int h4 = Mathf.Max(14, shot.height / 4);
        blurRT = new RenderTexture(w4, h4, 0) { filterMode = FilterMode.Bilinear };
        RenderTexture half = RenderTexture.GetTemporary(Mathf.Max(2, shot.width / 2), Mathf.Max(2, shot.height / 2), 0);
        RenderTexture quarter = RenderTexture.GetTemporary(w4, h4, 0);
        RenderTexture eighth = RenderTexture.GetTemporary(Mathf.Max(2, shot.width / 8), Mathf.Max(2, shot.height / 8), 0);
        RenderTexture sixteenth = RenderTexture.GetTemporary(Mathf.Max(2, shot.width / 16), Mathf.Max(2, shot.height / 16), 0);
        half.filterMode = quarter.filterMode = eighth.filterMode = sixteenth.filterMode = FilterMode.Bilinear;
        Graphics.Blit(shot, half);
        Graphics.Blit(half, quarter);
        Graphics.Blit(quarter, eighth);
        Graphics.Blit(eighth, sixteenth);
        Graphics.Blit(sixteenth, eighth);
        Graphics.Blit(eighth, blurRT);
        RenderTexture.ReleaseTemporary(half);
        RenderTexture.ReleaseTemporary(quarter);
        RenderTexture.ReleaseTemporary(eighth);
        RenderTexture.ReleaseTemporary(sixteenth);
        Destroy(shot);

        if (diffBlur != null)
        {
            diffBlur.texture = blurRT;
            diffBlur.enabled = true;
        }
        if (diffScrim != null) diffScrim.enabled = true;
        if (diffPanel != null) diffPanel.gameObject.SetActive(true);

        // スナップショットが揃ったのでフェードイン開始(閉じられていなければ)。
        if (difficultyOpen)
        {
            if (diffFadeCo != null) StopCoroutine(diffFadeCo);
            diffFadeCo = StartCoroutine(FadeDifficulty(true));
        }
    }

    // ---- Public API used by StageSelectManager ----

    // タイトル→ステージ選択の入場フェード用。表示中のみ全体 alpha を上書きする
    // (最終状態は呼び出し側が SetVisible / RefreshStyleVisibility で確定させる)。
    public void SetEntranceAlpha(float alpha)
    {
        if (!Visible || rootCG == null) return;
        rootCG.alpha = Mathf.Clamp01(alpha);
    }

    public void SetVisible(bool visible)
    {
        Visible = visible;
        if (rootCG != null)
        {
            rootCG.alpha = visible ? 1f : 0f;
            rootCG.blocksRaycasts = false;
        }
        gameObject.SetActive(true); // keep active so Tick/video run; alpha hides it
        if (visible)
        {
            if (videoPlayer != null && !string.IsNullOrEmpty(videoPlayer.url) && !videoPlayer.isPlaying)
            {
                videoPlayer.Play();
            }
        }
        else
        {
            if (videoPlayer != null && videoPlayer.isPlaying) videoPlayer.Pause();
        }
    }

    public void SetStage(int index, int total, bool animate)
    {
        totalStages = Mathf.Max(1, total);
        int newIndex = Mathf.Clamp(index, 0, totalStages - 1);
        if (transTime >= 0f)
        {
            // 飛行中の同一インデックス通知は無視。連打時は現在の遷移を即着地
            // させてから次の遷移を始める(状態の取りこぼし防止)。
            if (newIndex == currentIndex) return;
            FinishTransition();
        }
        int dir = newIndex == currentIndex ? 0 : (newIndex > currentIndex ? 1 : -1);
        currentIndex = newIndex;
        RefreshProgress(animate && dir != 0);

        if (animate && dir != 0)
        {
            BeginTransition(dir);
        }
        else
        {
            ApplyStageContent(false);
            ResetPanels(true);
        }
    }

    // Starts the slide-carousel transition: the neighbour panel on the moved-to
    // side flies into the center slot, the center card flies out to the opposite
    // neighbour slot, the far panel exits off-screen and the spare panel slides
    // in from off-screen with the new neighbour (one continuous 5-slot band).
    private void BeginTransition(int dir)
    {
        transDir = dir;
        transTime = 0f;
        nameSwapped = false;
        SidePanel exitP = dir > 0 ? leftPanel : rightPanel;
        exitStartAlpha = exitP != null && exitP.cg != null ? exitP.cg.alpha : 1f;
        exitHadFrame = cardVideo != null && cardVideo.enabled;
        // 次動画の Prepare が中央 RT を上書きする前に、退場する中央フレームを退避する。
        if (exitHadFrame && videoRT != null)
        {
            if (departingVideoRT == null)
            {
                departingVideoRT = new RenderTexture(videoRT.width, videoRT.height, 0)
                {
                    name = "JsabDepartingVideoRT"
                };
            }
            Graphics.Blit(videoRT, departingVideoRT);
        }

        // 到着後すぐ再生に移れるよう、飛行中に新ステージの動画を裏で準備する。
        // 退場フレームは上の専用 RT に保存済みなので、中央 RT は安全に更新できる。
        StageData cur = GetStage(currentIndex);
        string path = VideoPath(cur);
        if (videoPlayer != null && path != null && videoPlayer.url != path)
        {
            videoPlayer.url = path;
            videoPlayer.Prepare();
        }
        if (cardRect != null) cardRect.localScale = Vector3.one; // 飛行中はパルス停止

        // 飛び出していく中央カードに、サイドカード用のカード内タイトル(旧ステージ名)
        // を仕込む。飛行中にフェードインし、着地時のサイドカード表示と一致する。
        if (cardInnerTitle != null)
        {
            cardInnerTitle.text = stageNameText != null ? stageNameText.text : "";
            TmpAlign.CenterInkVertically(cardInnerTitle);
            cardInnerTitle.alpha = 0f;
        }

        // 矢印も同様に仕込む: 中央カードは -dir 側のサイドカードになるので、
        // SetPanelSide と同じ向き/位置に合わせ、飛行中にフェードインさせる。
        if (cardArrow != null)
        {
            int newSide = -dir;
            cardArrow.text = newSide < 0 ? "<" : ">";
            RectTransform car = (RectTransform)cardArrow.transform;
            car.anchorMin = car.anchorMax = new Vector2(newSide < 0 ? 1f : 0f, 0.5f);
            car.anchoredPosition = new Vector2(newSide < 0 ? -36f : 36f, 0f);
            TmpAlign.CenterInkVertically(cardArrow);
            Color ac = ArrowWhite;
            ac.a = 0f;
            cardArrow.color = ac;
        }

        // 5スロット帯: 新しい隣ステージを spare パネルに載せ、画面外スロットから
        // 隣スロットへスライドインさせる(遷移後のその場フェードイン登場を廃止)。
        StageData incoming = GetStage(currentIndex + dir);
        SetPanelSide(sparePanel, dir);
        ApplySidePanelRest(sparePanel);
        sparePanel.rect.anchoredPosition = new Vector2(dir * OffSlotX, SideSlotY);
        sparePanel.cg.alpha = 0f;
        sparePanel.alphaTarget = incoming != null ? 1f : 0f;
        sparePanel.title.text = incoming != null ? SafeName(incoming) : "";
        TmpAlign.CenterInkVertically(sparePanel.title);
        sparePanel.fbName.text = incoming != null ? SafeName(incoming) : "";
        TmpAlign.CenterInkVertically(sparePanel.fbName);
        UpdateThumb(sparePanel, incoming);
    }

    // Drives one frame of the transition. p is linear progress [0..1].
    private void ApplyTransition(float p)
    {
        float e = 1f - Mathf.Pow(1f - p, 3f); // ease-out cubic

        Vector2 fromSide = new Vector2(transDir * SideSlotX, SideSlotY);   // 到着パネルの出発点
        Vector2 toSide = new Vector2(-transDir * SideSlotX, SideSlotY);    // 中央カードの行き先
        Vector2 offSide = new Vector2(-transDir * OffSlotX, SideSlotY);    // 退場パネルの行き先
        Vector2 inFrom = new Vector2(transDir * OffSlotX, SideSlotY);      // 登場パネルの出発点

        // 中央カード: 中央スロット → 反対隣スロットへ、縮小しながらサイドカードの
        // 見た目(ブラケット/タイトル帯/減光)へモーフ。
        if (cardRect != null)
        {
            cardRect.anchoredPosition = Vector2.Lerp(CenterSlotPos, toSide, e);
            cardRect.sizeDelta = Vector2.Lerp(CenterSlotSize, SideSlotSize, e);
        }
        if (cardFrameImg != null)
        {
            // 発光枠(シアン)を序盤で一気に消す(旧 1-e ease-out)と枠が痩せ、着地で
            // サイドパネルの銀エッジ(SilverEdge)が突然乗ってポップした。枠を消さず、
            // 着地パネルの銀エッジ色へ e で連続変化させて飛行中ずっと枠を残し、着地状態と
            // 色・濃さを一致させる(2026-07-14 指摘「移動前ステージの枠のアニメが不自然」)。
            cardFrameImg.color = Color.Lerp(Cyan, SilverEdge, e);
        }
        SetAccentAlpha(1f - e);
        // ブラケットは ease(e) だと序盤2フレームで一気に出て「突然出る」ため、
        // 線形 p ベースの約 0.12s(p 0→0.4)でなだらかに出す。
        SetBracketAlpha(cardBrackets, Mathf.Clamp01(p / 0.4f));
        if (cardMediaRect != null) SetMediaInsets(cardMediaRect, e);
        // サイドカードだけが持つ淡色リムと矢印は、着地の瞬間に受け渡し先で突然
        // 出さず、飛行中にフェードインさせて着地状態と一致させる。
        if (cardRimRect != null)
        {
            SetMediaInsets(cardRimRect, e);
            Color rimC = ThumbRimColor;
            rimC.a *= e;
            SetRim(cardRim, rimC, 2f);
        }
        if (cardArrow != null)
        {
            Color ac = ArrowWhite;
            ac.a = 0.9f * Mathf.Clamp01((p - 0.4f) / 0.5f);
            cardArrow.color = ac;
        }
        if (cardScrim != null)
        {
            Color sc = ThumbScrimColor;
            sc.a *= e;
            cardScrim.color = sc;
        }
        if (cardVideo != null) cardVideo.color = Color.Lerp(Color.white, ThumbDim, e);
        if (cardFallbackName != null) cardFallbackName.fontSize = Mathf.Lerp(96f, 44f, e);
        if (cardInnerTitle != null) cardInnerTitle.alpha = Mathf.Clamp01((p - 0.4f) / 0.5f);

        // 到着パネル: 隣スロット → 中央スロットへ、拡大しながら中央カードの見た目へ。
        SidePanel arrive = transDir > 0 ? rightPanel : leftPanel;
        if (arrive != null)
        {
            arrive.rect.anchoredPosition = Vector2.Lerp(fromSide, CenterSlotPos, e);
            arrive.rect.sizeDelta = Vector2.Lerp(SideSlotSize, CenterSlotSize, e);
            Color gc = Cyan;
            gc.a = e;
            arrive.glowFrame.color = gc;
            SetMediaInsets(arrive.thumbArea, 1f - e);
            Color rc = ThumbRimColor;
            rc.a *= 1f - e;
            SetRim(arrive.thumbRim, rc, 2f);
            if (arrive.thumb != null) arrive.thumb.color = Color.Lerp(ThumbDim, Color.white, e);
            Color sc2 = ThumbScrimColor;
            sc2.a = Mathf.Lerp(sc2.a, 0f, e);
            arrive.scrim.color = sc2;
            // 出現側(中央カードのブラケット)と対称の 0.12s で消す。p*3 の3フレーム
            // 消滅は「突然消える」側の不連続だった。
            arrive.decorCG.alpha = 1f - Mathf.Clamp01(p / 0.4f);
            arrive.cg.alpha = 1f;
            arrive.fbName.fontSize = Mathf.Lerp(44f, 96f, e);
        }

        // 退場パネル: 隣スロット → 画面外へフェードアウトしながら移動。
        SidePanel exitP = transDir > 0 ? leftPanel : rightPanel;
        if (exitP != null)
        {
            exitP.rect.anchoredPosition = Vector2.Lerp(toSide, offSide, e);
            exitP.cg.alpha = exitStartAlpha * (1f - p);
        }

        // 登場パネル: 画面外スロット → 隣スロットへ帯と一緒にスライドイン。
        // alpha は準備状況に応じて Tick 側でフェードする。
        if (sparePanel != null)
        {
            sparePanel.rect.anchoredPosition = Vector2.Lerp(inFrom, fromSide, e);
        }

        // ステージ名: 旧名は序盤(0〜0.35)で消し切り、新名は終盤(0.55〜1.0)で出す。
        // 中間フレームに旧名が薄く残ると退場中か選択中か曖昧に見えるため。
        if (stageNameText != null)
        {
            if (!nameSwapped && p >= 0.45f)
            {
                nameSwapped = true;
                StageData cur = GetStage(currentIndex);
                string curName = cur != null && !string.IsNullOrWhiteSpace(cur.stageName) ? cur.stageName : ("Stage " + currentIndex);
                stageNameText.text = curName;
                TmpAlign.CenterInkVertically(stageNameText);
                UpdateStageNameSlashes();
            }
            stageNameText.alpha = p < 0.35f ? 1f - p / 0.35f
                                : p < 0.55f ? 0f
                                : (p - 0.55f) / 0.45f;
            SetStageNameSlashAlpha(stageNameText.alpha);
        }
    }

    // Lands the transition: hands the old center frame to the panel that now sits
    // in the moved-from slot (no dark gap), copies the arriving panel's still
    // frame onto the center RenderTexture, rotates panel roles (incoming spare
    // becomes the new neighbour) and snaps everything to its home slot.
    private void FinishTransition()
    {
        if (transTime < 0f) return;
        transTime = -1f;

        SidePanel arrived = transDir > 0 ? rightPanel : leftPanel;   // now at the center slot
        SidePanel recycled = transDir > 0 ? leftPanel : rightPanel;  // flew off-screen
        SidePanel incoming = sparePanel;                             // slid in from off-screen

        // (1) 旧中央の凍結フレームを、旧中央側スロットを引き継ぐパネルへ写す。
        // 飛行中ずっと見えていた絵をそのまま受け渡すので消灯フレームが出ない。
        bool recycledHasFrame = exitHadFrame && departingVideoRT != null && recycled != null && recycled.rt != null;
        if (recycledHasFrame) Graphics.Blit(departingVideoRT, recycled.rt);

        // (2) 到着パネルの静止フレームを中央RTへ写す(着地フレームで絵が飛ばない)。
        bool arriveHadFrame = arrived != null && arrived.thumb != null && arrived.thumb.enabled && arrived.rt != null;
        if (arriveHadFrame && videoRT != null) Graphics.Blit(arrived.rt, videoRT);

        // (3) 役割ローテーション: 到着→spare / 退場→旧中央側スロット / 登場→移動先側。
        if (transDir > 0) { leftPanel = recycled; rightPanel = incoming; }
        else { rightPanel = recycled; leftPanel = incoming; }
        sparePanel = arrived;
        SetPanelSide(leftPanel, -1);
        SetPanelSide(rightPanel, 1);

        // (4) 旧中央側スロットの中身: blit した静止画(または fallback タイル)。
        StageData prevCenter = GetStage(currentIndex - transDir);
        recycled.title.text = SafeName(prevCenter);
        TmpAlign.CenterInkVertically(recycled.title);
        recycled.fbName.text = SafeName(prevCenter);
        TmpAlign.CenterInkVertically(recycled.fbName);
        if (recycled.vp != null)
        {
            recycled.vp.Stop();
            recycled.vp.url = null;
        }
        recycled.contentReady = true;
        // blit した静止画は飛行中に見えていた絵の受け渡しなので即時表示する。
        if (recycled.mediaCG != null) recycled.mediaCG.alpha = 1f;
        if (recycledHasFrame)
        {
            recycled.thumb.enabled = true;
            recycled.fallback.gameObject.SetActive(false);
        }
        else
        {
            recycled.thumb.enabled = false;
            recycled.fallback.gameObject.SetActive(true);
        }

        ResetPanels(false);

        // 中央カード: 新ステージの名前とメイン動画を適用。
        ApplyCenterContent(arriveHadFrame);
        if (stageNameText != null) stageNameText.alpha = 1f;
        SetStageNameSlashAlpha(1f);

        // 受け渡し側は途切れなく表示継続(飛行中の絵と同じ内容が同じ位置にある)。
        recycled.cg.alpha = recycled.alphaTarget;
        recycled.decorCG.alpha = 1f;

        // spare(旧到着パネル)は画面外へ退避。
        sparePanel.cg.alpha = 0f;
        sparePanel.alphaTarget = 0f;
        sparePanel.rect.anchoredPosition = new Vector2(OffSlotX, SideSlotY);
        if (sparePanel.vp != null) sparePanel.vp.Stop();

        // 中央枠のアクセントは着地後に短くフェードイン(到着パネルには無いため)。
        accentAlpha = 0f;
        SetAccentAlpha(0f);
    }

    // Restores a side panel's at-rest look (side-card layout, brackets, dim thumb).
    private void ApplySidePanelRest(SidePanel p)
    {
        if (p == null) return;
        p.rect.sizeDelta = SideSlotSize;
        Color gc = Cyan;
        gc.a = 0f;
        p.glowFrame.color = gc;
        SetMediaInsets(p.thumbArea, 1f);
        SetRim(p.thumbRim, ThumbRimColor, 2f);
        SetBracketAlpha(p.brackets, 1f);
        if (p.thumb != null) p.thumb.color = ThumbDim;
        if (p.scrim != null) p.scrim.color = ThumbScrimColor;
        if (p.decorCG != null) p.decorCG.alpha = 1f;
        if (p.fbName != null) p.fbName.fontSize = 44f;
    }

    // Puts every panel back to its home slot / home look. instantAlpha=true
    // snaps the side panels straight to their visibility target (initial show);
    // false keeps their current alpha (landing continuity; Tick fades them).
    private void ResetPanels(bool instantAlpha)
    {
        if (cardRect != null)
        {
            cardRect.anchoredPosition = CenterSlotPos;
            cardRect.sizeDelta = CenterSlotSize;
        }
        if (cardFrameImg != null) cardFrameImg.color = Cyan;
        SetBracketAlpha(cardBrackets, 0f);
        if (cardMediaRect != null) SetMediaInsets(cardMediaRect, 0f);
        if (cardScrim != null)
        {
            Color sc = ThumbScrimColor;
            sc.a = 0f;
            cardScrim.color = sc;
        }
        if (cardVideo != null) cardVideo.color = Color.white;
        if (cardFallbackName != null) cardFallbackName.fontSize = 96f;
        if (cardInnerTitle != null) cardInnerTitle.alpha = 0f;
        if (cardRimRect != null)
        {
            SetMediaInsets(cardRimRect, 0f);
            SetRim(cardRim, new Color(ThumbRimColor.r, ThumbRimColor.g, ThumbRimColor.b, 0f), 2f);
        }
        if (cardArrow != null)
        {
            Color ac = ArrowWhite;
            ac.a = 0f;
            cardArrow.color = ac;
        }

        if (leftPanel != null)
        {
            ApplySidePanelRest(leftPanel);
            leftPanel.rect.anchoredPosition = new Vector2(-SideSlotX, SideSlotY);
            leftPanel.alphaTarget = GetStage(currentIndex - 1) != null ? 1f : 0f;
        }
        if (rightPanel != null)
        {
            ApplySidePanelRest(rightPanel);
            rightPanel.rect.anchoredPosition = new Vector2(SideSlotX, SideSlotY);
            rightPanel.alphaTarget = GetStage(currentIndex + 1) != null ? 1f : 0f;
        }

        if (instantAlpha)
        {
            // 初期表示: パネル本体は即表示。サムネイル未準備なら thumbArea だけ
            // 0 から始め、Tick 側で準備完了後にフェードインさせる(瞬時ポップ防止)。
            if (leftPanel != null)
            {
                leftPanel.cg.alpha = leftPanel.alphaTarget;
                if (leftPanel.mediaCG != null) leftPanel.mediaCG.alpha = leftPanel.contentReady ? 1f : 0f;
            }
            if (rightPanel != null)
            {
                rightPanel.cg.alpha = rightPanel.alphaTarget;
                if (rightPanel.mediaCG != null) rightPanel.mediaCG.alpha = rightPanel.contentReady ? 1f : 0f;
            }
            accentAlpha = 1f;
            SetAccentAlpha(1f);
        }
        // instantAlpha=false(着地時)は alpha を触らない: 受け渡し側は呼び出し元が
        // 1 に、スライドイン側は途中のフェード値のまま Tick が引き継ぐ。
    }

    private void SetAccentAlpha(float a)
    {
        if (cardAccents == null) return;
        Color c = AccentCyan;
        c.a = a;
        for (int i = 0; i < cardAccents.Length; i++)
        {
            if (cardAccents[i] != null) cardAccents[i].color = c;
        }
    }

    // Applies the current stage's name and main video to the center card only.
    // keepBlittedFrame=true means the center RT already holds the arriving
    // panel's still frame, so it stays visible while the clip finishes preparing.
    private void ApplyCenterContent(bool keepBlittedFrame)
    {
        StageData cur = GetStage(currentIndex);
        string curName = cur != null && !string.IsNullOrWhiteSpace(cur.stageName) ? cur.stageName : ("Stage " + currentIndex);
        // Japanese stage names ride high under Middle alignment (Latin UI font +
        // CJK fallback metrics); optically center each by its ink bounds.
        if (stageNameText != null) { stageNameText.text = curName; TmpAlign.CenterInkVertically(stageNameText); }
        UpdateStageNameSlashes();
        if (cardFallbackName != null) { cardFallbackName.text = curName; TmpAlign.CenterInkVertically(cardFallbackName); }
        UpdateVideo(cur, keepBlittedFrame);
    }

    // Applies everything (center + both neighbour thumbnails). Used on the
    // non-animated path (initial show / hard jumps); landings hand content over
    // via FinishTransition instead so panels never reload what they already show.
    private void ApplyStageContent(bool keepBlittedFrame)
    {
        ApplyCenterContent(keepBlittedFrame);

        StageData left = GetStage(currentIndex - 1);
        StageData right = GetStage(currentIndex + 1);
        if (leftPanel != null)
        {
            leftPanel.title.text = SafeName(left);
            TmpAlign.CenterInkVertically(leftPanel.title);
            leftPanel.fbName.text = SafeName(left);
            TmpAlign.CenterInkVertically(leftPanel.fbName);
            UpdateThumb(leftPanel, left);
        }
        if (rightPanel != null)
        {
            rightPanel.title.text = SafeName(right);
            TmpAlign.CenterInkVertically(rightPanel.title);
            rightPanel.fbName.text = SafeName(right);
            TmpAlign.CenterInkVertically(rightPanel.fbName);
            UpdateThumb(rightPanel, right);
        }
    }

    public void Tick(float dt)
    {
        if (!Visible) return;

        // While the difficulty modal is open, the mouse can hover (to select) and
        // click (to confirm) any of the three buttons. Keyboard is driven by the
        // StageSelectManager; here we only translate pointer position/click.
        // Ignore the pointer for a beat after opening so the interaction that opened
        // the modal (or a focus click on the game view) cannot immediately confirm.
        if (difficultyOpen && !diffExiting)
        {
            if (diffBar != null) diffBar.Tick(dt);
            if (Time.unscaledTime - diffOpenTime >= 0.2f)
            {
                Mouse mouse = Mouse.current;
                if (mouse != null)
                {
                    Vector2 mp = mouse.position.ReadValue();
                    int hover = -1;
                    for (int i = 0; i < diffBoxRects.Length; i++)
                    {
                        if (diffBoxRects[i] == null) continue;
                        if (RectTransformUtility.RectangleContainsScreenPoint(diffBoxRects[i], mp, null))
                        {
                            hover = i;
                            break;
                        }
                    }
                    // 無効(COMING SOON)行はホバー選択・確定させない。ガードしないと
                    // Up/Down が無効行に止まれず while が無限ループになる。
                    if (hover >= 0 && diffBar != null && diffBar.IsRowEnabled(hover))
                    {
                        // Route through Up/Down so the description/brackets update too.
                        while (diffBar.index > hover) diffBar.Up();
                        while (diffBar.index < hover) diffBar.Down();
                        if (mouse.leftButton.wasPressedThisFrame) mouseConfirm = true;
                    }
                }
            }
        }

        // Mirror the live (alpha-hidden) top bar originals: timer text incl. the
        // low-time warning pulse, and the red time-dim panel width.
        if (topBarTimerText != null && origTimerText != null)
        {
            topBarTimerText.text = origTimerText.text;
            topBarTimerText.color = origTimerText.color;
            topBarTimerText.rectTransform.localScale = origTimerText.rectTransform.localScale;
        }
        if (topBarTimeDim != null && origTimeDim != null)
        {
            topBarTimeDim.sizeDelta = origTimeDim.sizeDelta;
            topBarTimeDim.anchoredPosition = origTimeDim.anchoredPosition;
        }

        pulseTime += dt;
        // Breathing pulse on the selected card (matches StageBox.SetPulse feel).
        float pulse = 1f + 0.02f * (0.5f + 0.5f * Mathf.Sin(pulseTime * 3f));

        // 動画 Prepare 直後は dt がスパイクするため 1/30s にクランプし、
        // フェードが1フレームに潰れて瞬時ポップに見えるのを防ぐ。
        float fadeStep = Mathf.Min(dt, 1f / 30f) / 0.15f;

        // Slide-carousel transition (panels flying between slots, ease-out).
        // The root CanvasGroup alpha is left alone: dipping it made the opaque
        // overlay translucent for a few frames, which read as a full-screen flash.
        if (transTime >= 0f)
        {
            transTime += dt;
            float p = Mathf.Clamp01(transTime / transDuration);
            ApplyTransition(p);
            // 登場パネル: カード本体(枠/ブラケット/タイトル)はスライドしながら
            // フェードイン。サムネイルは mediaCG 側で準備完了後に別途フェード。
            if (sparePanel != null)
            {
                sparePanel.cg.alpha = Mathf.MoveTowards(sparePanel.cg.alpha, sparePanel.alphaTarget, fadeStep);
            }
            TickMediaFade(leftPanel, fadeStep);
            TickMediaFade(rightPanel, fadeStep);
            TickMediaFade(sparePanel, fadeStep);
            if (p >= 1f) FinishTransition();
        }
        else
        {
            // 端フェード / 準備待ちのフェードイン。存在しない側は 0 に向かう。
            TickPanelFade(leftPanel, fadeStep);
            TickPanelFade(rightPanel, fadeStep);
            // 着地直後のアクセント(中央枠の明るいエッジ)フェードイン。
            if (accentAlpha < 1f)
            {
                accentAlpha = Mathf.MoveTowards(accentAlpha, 1f, fadeStep);
                SetAccentAlpha(accentAlpha);
            }
        }

        // Page-indicator marker tween (ease-out between nodes).
        if (markerTweenTime >= 0f && markerRect != null)
        {
            markerTweenTime += dt;
            float p = Mathf.Clamp01(markerTweenTime / markerTweenDuration);
            float ease = 1f - Mathf.Pow(1f - p, 3f);
            markerRect.anchoredPosition = new Vector2(Mathf.Lerp(markerFromX, markerToX, ease), MarkerY);
            if (p >= 1f) markerTweenTime = -1f;
        }

        if (cardRect != null && transTime < 0f) cardRect.localScale = Vector3.one * pulse;
    }

    private static void TickPanelFade(SidePanel p, float step)
    {
        if (p == null || p.cg == null) return;
        p.cg.alpha = Mathf.MoveTowards(p.cg.alpha, p.alphaTarget, step);
        if (p.decorCG != null) p.decorCG.alpha = Mathf.MoveTowards(p.decorCG.alpha, 1f, step);
        TickMediaFade(p, step);
    }

    // thumbArea(サムネ/フォールバック)の準備待ちフェード。パネル本体とは独立。
    private static void TickMediaFade(SidePanel p, float step)
    {
        if (p == null || p.mediaCG == null) return;
        p.mediaCG.alpha = Mathf.MoveTowards(p.mediaCG.alpha, p.contentReady ? 1f : 0f, step);
    }

    // ---- internals ----

    // Returns the preview-clip path for a stage, or null when it has none.
    private static string VideoPath(StageData data)
    {
        string dir = data != null ? data.stageDirectoryName : null;
        if (string.IsNullOrEmpty(dir)) return null;
        string path = Path.Combine(Application.dataPath, "StageData", dir, dir + ".mp4");
        return File.Exists(path) ? path : null;
    }

    private void UpdateVideo(StageData data, bool keepBlittedFrame)
    {
        string path = VideoPath(data);
        if (path != null)
        {
            if (videoPlayer == null) return;
            if (videoPlayer.url == path && videoPlayer.isPrepared)
            {
                if (cardVideo != null) cardVideo.enabled = true;
                if (cardFallback != null) cardFallback.gameObject.SetActive(false);
                if (!videoPlayer.isPlaying) videoPlayer.Play();
                return;
            }
            // 準備待ちの間の表示: 着地時に Blit した到着パネルの静止画があれば
            // それを見せ続ける。無い場合のみフォールバックカードで覆う。
            if (keepBlittedFrame)
            {
                if (cardVideo != null) cardVideo.enabled = true;
                if (cardFallback != null) cardFallback.gameObject.SetActive(false);
            }
            else if (cardVideo != null && !cardVideo.enabled && cardFallback != null)
            {
                cardFallback.gameObject.SetActive(true);
            }
            if (videoPlayer.url != path)
            {
                videoPlayer.url = path;
                videoPlayer.Prepare();
            }
        }
        else
        {
            if (cardVideo != null) cardVideo.enabled = false;
            if (cardFallback != null) cardFallback.gameObject.SetActive(true);
            if (videoPlayer != null)
            {
                videoPlayer.Stop();
                videoPlayer.url = null;
            }
        }
    }

    private void OnMainVideoPrepared(VideoPlayer vp)
    {
        // 飛行中は退場する中央カードに旧ステージの静止画を残したいので、
        // 表示切り替えと再生開始は着地時(FinishTransition 経由)に任せる。
        if (transTime >= 0f) return;
        if (cardVideo != null) cardVideo.enabled = true;
        if (cardFallback != null) cardFallback.gameObject.SetActive(false);
        vp.Play();
    }

    // Prepares a neighbour's preview video into its RenderTexture (paused on the
    // first frame = a still, dim thumbnail). Falls back to a navy tile if missing.
    private void UpdateThumb(SidePanel p, StageData data)
    {
        if (p == null || p.vp == null) return;
        string dir = data != null ? data.stageDirectoryName : null;
        string path = !string.IsNullOrEmpty(dir)
            ? Path.Combine(Application.dataPath, "StageData", dir, dir + ".mp4")
            : null;
        bool hasVideo = !string.IsNullOrEmpty(path) && File.Exists(path);

        int requestId = ++p.thumbnailRequestId;
        p.vp.Stop();
        if (p.thumb != null) p.thumb.enabled = false;

        // 動画の準備中も空欄にせず、対象ステージ名のフォールバックを即表示する。
        // これにより I/O 待ち中に以前のステージの RT が見えることもない。
        if (p.fallback != null) p.fallback.gameObject.SetActive(true);
        p.contentReady = true;
        if (p.mediaCG != null) p.mediaCG.alpha = 1f;

        if (!hasVideo)
        {
            p.vp.url = null;
            return;
        }

        VideoPlayer.EventHandler prepared = null;
        VideoPlayer.ErrorEventHandler failed = null;
        prepared = v =>
        {
            v.prepareCompleted -= prepared;
            v.errorReceived -= failed;
            if (p.thumbnailRequestId != requestId || !string.Equals(v.url, path, StringComparison.OrdinalIgnoreCase))
                return;

            v.Pause();
            if (p.thumb != null) p.thumb.enabled = true;
            if (p.fallback != null) p.fallback.gameObject.SetActive(false);
            p.contentReady = true;
            if (p.mediaCG != null) p.mediaCG.alpha = 1f;
        };
        failed = (v, message) =>
        {
            v.prepareCompleted -= prepared;
            v.errorReceived -= failed;
            if (p.thumbnailRequestId != requestId) return;

            if (p.thumb != null) p.thumb.enabled = false;
            if (p.fallback != null) p.fallback.gameObject.SetActive(true);
            p.contentReady = true;
            if (p.mediaCG != null) p.mediaCG.alpha = 1f;
        };

        p.vp.prepareCompleted += prepared;
        p.vp.errorReceived += failed;
        p.vp.url = path;
        p.vp.Prepare();
    }

    private StageData GetStage(int index)
    {
        if (index < 0 || index >= totalStages) return null;
        return GManager.Control != null && GManager.Control.SDB != null ? GManager.Control.SDB.GetStage(index) : null;
    }

    private static string SafeName(StageData d)
    {
        return d != null && !string.IsNullOrWhiteSpace(d.stageName) ? d.stageName : "";
    }

    private Image NewImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private RawImage NewRawImage(string name, Transform parent, Texture tex)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RawImage img = go.AddComponent<RawImage>();
        img.texture = tex;
        img.raycastTarget = false;
        return img;
    }

    private TMP_Text NewText(string name, Transform parent, string content, float size, Color color, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    private static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    // Uniform inset for a stretch-anchored rect (border thickness of a panel).
    private static void SetInset(RectTransform r, float inset)
    {
        r.offsetMin = new Vector2(inset, inset);
        r.offsetMax = new Vector2(-inset, -inset);
    }

    private Sprite CreateRingSprite()
    {
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Vector2 c = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float outer = size * 0.46f;
        float inner = size * 0.30f;
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c);
                float a = 0f;
                if (d <= outer && d >= inner)
                {
                    // soft edges
                    float edge = Mathf.Min(outer - d, d - inner);
                    a = Mathf.Clamp01(edge);
                }
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    // Solid filled circle with a soft edge (current-stage indicator dot).
    private Sprite CreateDotSprite()
    {
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Vector2 c = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float r = size * 0.46f;
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c);
                float a = Mathf.Clamp01(r - d);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    // Chamfered-rectangle stroke with a soft outer glow (mockup center frame),
    // exported as a 9-sliced sprite so the card can resize during the carousel
    // tween while the corners stay pixel-perfect.
    private Sprite CreateGlowFrameSprite()
    {
        const int size = 256;
        const float margin = GlowMargin;  // texture edge -> stroke outer edge (glow lives here)
        const float stroke = 3.5f;
        const float chamfer = 22f;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[size * size];
        float half = (size - 1) * 0.5f;
        float hx = half - margin;
        float hy = half - margin;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float ax = Mathf.Abs(x - half);
                float ay = Mathf.Abs(y - half);
                // Signed distance to the chamfered rect edge (>0 = outside).
                float d = Mathf.Max(ax - hx, ay - hy);
                d = Mathf.Max(d, (ax + ay - (hx + hy - chamfer)) * 0.7071f);
                float band = Mathf.Abs(d) - stroke * 0.5f;
                float a = band <= 0f ? 1f : Mathf.Clamp01(1f - band); // stroke core + AA
                if (d > 0f)
                {
                    // outer glow
                    float g = Mathf.Clamp01(1f - (d - stroke * 0.5f) / 11f);
                    a = Mathf.Max(a, 0.30f * g * g);
                }
                else
                {
                    // faint inner glow
                    float g = Mathf.Clamp01(1f + (d + stroke * 0.5f) / 7f);
                    a = Mathf.Max(a, 0.16f * g * g);
                }
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f,
            0, SpriteMeshType.FullRect, new Vector4(100f, 100f, 100f, 100f));
    }

    // Filled chamfered rect matching CreateGlowFrameSprite's geometry (margin 0,
    // same 22px chamfer). Used as a stencil mask so the center media never pokes
    // past the chamfered frame corners.
    private Sprite CreateChamferMaskSprite()
    {
        const int size = 256;
        const float chamfer = 22f;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[size * size];
        float half = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float ax = Mathf.Abs(x - half);
                float ay = Mathf.Abs(y - half);
                float d = Mathf.Max(ax - half, ay - half);
                d = Mathf.Max(d, (ax + ay - (2f * half - chamfer)) * 0.7071f);
                float a = Mathf.Clamp01(0.5f - d);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f,
            0, SpriteMeshType.FullRect, new Vector4(100f, 100f, 100f, 100f));
    }

    private void OnDestroy()
    {
        if (videoRT != null)
        {
            videoRT.Release();
            Destroy(videoRT);
        }
        if (blurRT != null)
        {
            blurRT.Release();
            Destroy(blurRT);
        }
        if (departingVideoRT != null)
        {
            departingVideoRT.Release();
            Destroy(departingVideoRT);
        }
        ReleasePanelRT(leftPanel);
        ReleasePanelRT(rightPanel);
        ReleasePanelRT(sparePanel);
    }

    private static void ReleasePanelRT(SidePanel p)
    {
        if (p == null || p.rt == null) return;
        p.rt.Release();
        Destroy(p.rt);
    }
}

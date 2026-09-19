using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ステージ終了後のリザルト画面。
//
// 2026-09-16 の全面改修で「夜の紺の半透明パネル + 細い金の罫線と小さな菱形」様式へ
// 作り直した。旧様式(銀枠・19° 平行四辺形・シアン/ブランドブルーの JSAB 風)は廃止
// (正は Docs/result-design-language.md の §10 以降)。背景はステージのプレイ中 CG が
// そのまま見えるので、この画面自体は不透明な地を敷かない。
//
// スコア・被弾・ランク判定・2P 分割・ランキング登録(イニシャル入力→Top10)・
// Retry/StageSelect/Title の 3 アクション・履歴保存のロジックと、
// Prepare / Tick / PlayEntrance / ActionRequested の外部 API は従来のまま。
public sealed class ResultScreen : MonoBehaviour
{
    public enum Action
    {
        Retry,
        StageSelect,
        Title,
    }

    // ---- 色 ---------------------------------------------------------------
    // 焼き込みテクスチャは視覚(sRGB)値をそのまま書き、頂点色(Image.color / TMP.color)は
    // Vis() で pre-linear へ落とす(memory: texture_vs_vertex_colorspace)。

    private static readonly Color32 TexPanelTop = new Color32(0x12, 0x1B, 0x35, 0xE0);
    private static readonly Color32 TexPanelBottom = new Color32(0x06, 0x0B, 0x1A, 0xEE);
    private static readonly Color32 TexGoldLine = new Color32(0xA8, 0x83, 0x45, 0xFF);
    private static readonly Color32 TexGoldBright = new Color32(0xE9, 0xB9, 0x6E, 0xFF);
    private static readonly Color32 TexGoldDim = new Color32(0x6E, 0x57, 0x2C, 0xFF);
    // ボタンの石目板。参考画像の実測は (33,32,48) 前後。
    private static readonly Color32 TexButtonTop = new Color32(0x2A, 0x28, 0x3A, 0xF4);
    private static readonly Color32 TexButtonBottom = new Color32(0x1B, 0x1A, 0x28, 0xF4);

    /// <summary>
    /// 視覚 sRGB(0..255) をそのまま頂点色にする。
    ///
    /// 2026-09-16 実測: このリザルトの Overlay Canvas では、頂点色(TMP.color / Image.color)を
    /// <c>.linear</c> に落として渡すと <b>画面に出るのが linear 値そのもの</b>になり、
    /// 一段暗く濁る(ラベルの狙い #B4BCC9 が実測 117,128,149 = linear 値)。
    /// 焼き込みテクスチャ側は sRGB テクスチャとして正しく変換される(枠線の #A88345 が実測 168,131,69)。
    /// よって頂点色は pre-linear にせず、視覚値をそのまま渡す。
    /// </summary>
    private static Color Vis(int r, int g, int b, float a = 1f)
    {
        return new Color(r / 255f, g / 255f, b / 255f, a);
    }

    // 参考画像の実測値に合わせた(値 = 254,234,113 / ラベル = 220,222,239 / 舞台名 = 白)。
    private static readonly Color GoldAccent = Vis(0xF8, 0xDC, 0x8C);   // 値・菱形・STAGE CLEAR
    private static readonly Color GoldBright = Vis(0xF8, 0xDD, 0x9E);   // ランク字のハイライト
    private static readonly Color GoldDim = Vis(0xC6, 0x9E, 0x5C);      // 罫線・アイコンの金
    private static readonly Color SilverLabel = Vis(0xDC, 0xDE, 0xEC);  // RESULT・情報行のラベル
    private static readonly Color InkWhite = Vis(0xFF, 0xFF, 0xFF);     // 舞台名の白
    private static readonly Color FailRed = Vis(0xE0, 0x6A, 0x74);      // STAGE FAILED

    // ---- 寸法(1080 基準) --------------------------------------------------
    // 値の正は参考画像 Instructions/リザルト/ref/result_mockup_gpt_20260916.png(1672x941)。
    // 参考画像の画素 → ここの値は ×1.1477(= 1080/941)。パネルは画面中央。
    private const float PanelW = 698f;     // 参考 608px = 画面幅の 36.4%
    private const float PanelH = 938f;     // 参考 817px = 画面高さの 86.8%
    // 第 U9 便: 板だけ下へ 14px 伸ばす。座標系(PanelH)は動かさないので、
    // HighlandUi.Y() で置いた中身はすべて同じ位置のまま、下端の余白だけ増える。
    // 「次へ」を見本の位置へ下ろすと、その下の引き継ぎコードが内枠に触れるため。
    private const float PanelExtendDown = 14f;
    private const float PlateH = PanelH + PanelExtendDown;
    private const float PanelTop = PanelH * 0.5f;
    private const float ScrimAlpha = 0.20f;

    private const float YResult = 434f;       // 参考 y=94.5
    private const float YTopRule = 400f;      // 参考 y=122(銀の細い罫 + 小菱形)
    private const float YVerdict = 355f;      // 参考 y=164
    private const float YRule1 = 303f;        // 参考 y=206(金の罫 + 菱形)
    private const float YStageTitle = 264f;   // 参考 y=243.5
    private const float YRule2 = 212f;        // 参考 y=286(矢羽根つきの飾り罫)
    private const float YCrest = 180f;        // 参考 y=314(小さな金の菱形)
    private const float YRankLabel = 137f;    // 参考 y=351
    private const float YRank = 48f;          // 参考 y=431(ランク字の中心。輪・月桂樹の中心)
    // ランク字だけは TMP の行送りのぶん下へずれるので、見本(基線 SVG 478.5)に
    // 合わせて 10.5px 持ち上げる(第 U8 便・実フレーム実測で -9 SVG のずれ)。
    private const float YRankText = YRank + 10.5f;
    private const float YLaurel = 27f;        // 参考 y=442.5(月桂樹の中心)
    private const float YKnot = -64f;         // 参考 y=527(月桂樹の結びの菱形)
    private const float YRowSep0 = -82f;      // 情報行の 1 本目の区切り線
    private const float RowPitch = 56.5f;     // 参考 50.25px
    // v11 の「次へ」ボタン(SVG 328x72・y 785..857)。第 U9 便で設計どおりの位置へ
    // 下ろした(旧 -364 = 設計より 38px 上。文字リンクの行を下に置くためだった)。
    // 代わりに「もう一度 / タイトルへ」はボタンの左右へ回し、ボタンの下は
    // 引き継ぎコードの 1 行だけにした。
    private const float YButton = -402.5f;   // = HighlandUi.Y(821)
    private const float ButtonW = 377f;
    private const float ButtonH = 83f;
    // 文字リンクはボタンと同じ高さの左右。ボタンの外光(ButtonW+22)に掛からない x。
    private const float XLinks = 255f;
    private const float LinkW = 108f;
    private const float LinkFont = 17f;
    private const float YTransfer = -459f;

    private const float RowHalfW = 249f;   // 情報行の左右端
    private const float RowIconX = -216.5f; // 参考 x=646.5(アイコンの中心)
    private const float RowLabelX = -158f;  // 参考 x=697(ラベルの左端)
    private const float RowSepX = 81.5f;    // 参考 x=906(1P の縦罫)
    private const float RowValueX = 137f;   // 参考 x=954(値の左端)
    private const float RowSepW = 435f;     // 参考 379px(行間の区切り線)
    private const float RowValueRight = 249f;

    // ---- 入場シーケンス(リザルト BGM のドロップ 1.44s にランクを着地させる) --
    private const float EnterPlateDur = 0.45f;
    private const float EnterHeadStart = 0.20f;
    private const float EnterHeadDur = 0.45f;
    private const float EnterTitleStart = 0.45f;
    private const float EnterTitleDur = 0.45f;
    private const float EnterRankStart = 1.10f;
    private const float EnterRankDur = 0.34f;   // 着地 = 1.44
    private const float EnterRowsStart = 1.52f;
    private const float EnterRowStagger = 0.09f;
    private const float EnterRowDur = 0.34f;
    private const float EnterCountStart = 1.55f;
    private const float EnterCountDur = 0.55f;
    private const float EnterButtonStart = 2.16f;
    private const float EnterButtonDur = 0.30f;
    private const float EnterTotal = 2.60f;

    private const float ResultBgmVolume = 0.52f;
    private const double BgmScheduleLead = 0.08d;

    // 背景ぼかし(項目 4)。既定は「有り・弱め」。A/B 用に外から切れる。
    public static bool BackdropBlurEnabled = true;
    private const float BlurFadeDur = 0.40f;

    // ---- 参照 -------------------------------------------------------------
    private TMP_FontAsset font;        // 呼び出し元の UI フォント(保険)
    private TMP_FontAsset mincho;      // ShipporiMincho(和文・欧文ともこれを使う)

    private readonly List<Texture2D> generatedTextures = new List<Texture2D>();
    private readonly List<Sprite> generatedSprites = new List<Sprite>();

    private RawImage blurImage;
    private RenderTexture blurRT;
    private float blurTargetAlpha;

    private Image scrimImage;
    private Image vignetteImage;
    private CanvasGroup contentGroup;
    private RectTransform contentRect;

    private CanvasGroup panelGroup;
    private RectTransform panelRect;

    private CanvasGroup headerGroup;
    private CanvasGroup titleGroup;
    private CanvasGroup rankGroup;
    private RectTransform rankGroupRect;
    private CanvasGroup rowsGroup;
    private CanvasGroup buttonGroup;
    private RectTransform buttonRect;
    private Vector2 buttonHome;

    private TMP_Text verdictText;
    private TMP_Text stageTitleText;
    private HighlandUi.RubyText stageTitleRuby;
    // v11 のふりがな(表示後の初回に実測で置き直す)。
    private readonly System.Collections.Generic.List<HighlandUi.RubyText> rubies
        = new System.Collections.Generic.List<HighlandUi.RubyText>();
    private bool rubiesPlaced;
    private TMP_Text rankText;
    private TMP_Text rankText2;
    private TMP_Text p1RankTag;
    private TMP_Text p2RankTag;
    private Material rankGlowMat;
    private Material rankGlowMat2;
    private Image laurelLeft;
    private Image laurelRight;
    private Image laurelKnot;
    private Image rankRings;
    private const float LaurelBox = 340f;   // 月桂樹スプライトの表示寸法(正方)
    private SoftCircleGraphic rankHalo;
    private float rankHaloBaseAlpha = 0.038f;

    // 情報行(SCORE / HITS / DIFFICULTY / RANKING)
    private const int RowCount = 4;
    private const int RowScore = 0;
    private const int RowHits = 1;
    private const int RowDifficulty = 2;
    private const int RowRanking = 3;
    private readonly CanvasGroup[] rowGroups = new CanvasGroup[RowCount];
    private readonly RectTransform[] rowRects = new RectTransform[RowCount];
    // 情報行の定位置(v11 の rowCy から作る)。入場アニメはここへ戻す。
    // 第 U8 便(2026-09-19「文字がずれている」)まで、入場アニメが旧 U4b 版の
    // YRow0 / RowPitch で毎フレーム上書きしていたため、4 行とも v11 の設計より
    // 約 21px(SVG 18.5)高い位置で止まっていた。
    private readonly float[] rowHomeY = new float[RowCount];
    private readonly TMP_Text[] rowLabels = new TMP_Text[RowCount];
    private readonly TMP_Text[] rowValues = new TMP_Text[RowCount];
    private readonly TMP_Text[] rowValues2 = new TMP_Text[RowCount];
    private readonly Image[] rowSeparators = new Image[RowCount];
    private readonly Image[] rowIcons = new Image[RowCount];
    private TMP_Text columnTagP1;
    private TMP_Text columnTagP2;

    private TMP_Text nextLabel;
    private readonly RectTransform[] actionRects = new RectTransform[3];
    private readonly TMP_Text[] actionLabels = new TMP_Text[3];
    private readonly Image[] actionUnderlines = new Image[3];
    private readonly Action[] actionValues = new Action[3];
    private Image nextButtonBody;
    private Image nextButtonGlow;
    private int selectedActionIndex;
    private bool navLeftPrev;
    private bool navRightPrev;
    private bool inputArmed;
    private bool entering;
    private bool entranceFinished;
    private Coroutine entranceRoutine;

    private TMP_Text transferCodeLine;

    private AudioSource bgmSource;
    private AudioClip resultBgm;

    // ---- 結果データ -------------------------------------------------------
    private bool resultCleared = true;
    private int finalScore;
    private int finalHit;
    private bool twoPlayerResult;
    private int finalScore2;
    private int finalHit2;
    private bool twoPlayerSidesReversed;

    // ---- ランキング -------------------------------------------------------
    private enum RankingFlowState { None, Initials, Board }
    private RankingFlowState rankingFlowState = RankingFlowState.None;
    private bool rankingQualifies;
    private bool rankingSubmitted;
    private string pendingRankStage;
    private int pendingRankDifficulty;
    private string pendingRankMode;
    private int pendingRankScore;
    private string pendingEntryId;

    private GameObject rankingOverlayRoot;
    private CanvasGroup rankingOverlayCG;
    private float rankingOverlayTarget;
    private TMP_Text rankingOverlayHeading;
    private GameObject initialsGroup;
    private TMP_Text initialsScoreLine;
    private readonly TMP_Text[] initialsSlotTexts = new TMP_Text[RankingStore.NameLength];
    private readonly int[] initialsCharIndex = new int[RankingStore.NameLength];
    private int initialsColumn;
    private float initialsIdleTimer;
    private HoldRepeatTrigger initialsUpRepeat;
    private HoldRepeatTrigger initialsDownRepeat;
    private GameObject boardGroup;
    private TMP_Text boardHeaderText;
    private readonly TMP_Text[] boardRowTexts = new TMP_Text[RankingStore.TopCount];

    public event System.Action<Action> ActionRequested;

    public bool Visible => gameObject.activeSelf;
    public bool Entering => entering;

    // =======================================================================
    //  構築
    // =======================================================================

    public static ResultScreen Create(Transform parent, TMP_FontAsset uiFont)
    {
        GameObject go = new GameObject("ResultScreenCanvas", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 15;

        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();

        ResultScreen screen = go.AddComponent<ResultScreen>();
        screen.font = uiFont;
        screen.Build((RectTransform)go.transform);
        go.SetActive(false);
        return screen;
    }

    private void Build(RectTransform root)
    {
        // 2026-09-19 U7: 書体を Highland UI v11 の Noto Serif JP へ。和文も欧文も同じ。
        mincho = HighlandUi.Serif;
        if (mincho == null) mincho = Resources.Load<TMP_FontAsset>("Fonts/ShipporiMincho-Regular SDF");

        // --- 背景のぼかし板(項目 4)と薄い暗幕 ---
        GameObject blurGo = new GameObject("BackdropBlur", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        blurGo.transform.SetParent(root, false);
        blurImage = blurGo.GetComponent<RawImage>();
        blurImage.raycastTarget = false;
        blurImage.color = new Color(1f, 1f, 1f, 0f);
        Stretch(blurImage.rectTransform);

        // 周辺減光(参考画像の背景は中央が明るく端が沈む)。ぼかし板の上に敷く。
        vignetteImage = NewImage("Vignette", root, Color.white);
        vignetteImage.sprite = CreateVignetteSprite();
        vignetteImage.type = Image.Type.Simple;
        Stretch(vignetteImage.rectTransform);
        vignetteImage.color = new Color(1f, 1f, 1f, 0f);

        // わずかな青寄せと減光をかける薄幕(彩度を落として夜へ寄せる)。
        scrimImage = NewImage("Scrim", root, new Color(0.016f, 0.022f, 0.055f, 0f));
        Stretch(scrimImage.rectTransform);

        GameObject content = NewRect("Content", root);
        contentRect = (RectTransform)content.transform;
        Stretch(contentRect);
        contentGroup = content.AddComponent<CanvasGroup>();

        // --- パネル ---
        GameObject panelGo = NewRect("Panel", contentRect);
        panelRect = (RectTransform)panelGo.transform;
        SetRect(panelRect, Vector2.zero, new Vector2(PanelW, PanelH));
        panelGroup = panelGo.AddComponent<CanvasGroup>();

        // 板は Highland UI v11 の「四隅を円弧でえぐった二重枠」(2026-09-19 U7)。
        // 旧 GoldPanelStyle.CreateOrnatePanelSprite(四隅のブラケット・翼形)は使わない。
        Image plate = NewImage("Plate", panelRect, Color.white);
        plate.sprite = HighlandUi.NotchPanel((int)PanelW, (int)PlateH, HighlandUi.L(19f), false,
            generatedTextures, generatedSprites, "ResultPanelV11",
            HighlandUi.L(5.2f), 1.45f * HighlandUi.S, 0.9f * HighlandUi.S);
        plate.type = Image.Type.Simple;
        SetRect(plate.rectTransform, new Vector2(0f, -PanelExtendDown * 0.5f),
            new Vector2(PanelW, PlateH));

        // 上下中央の中空菱形(枠を小さく切って重ねる)。下の 1 つは伸ばした下端へ。
        foreach (float cy in new[] { HighlandUi.Y(63f), HighlandUi.Y(878f) - PanelExtendDown })
        {
            Image cut = NewImage("CrestCut", panelRect, Vis(0x11, 0x11, 0x25));
            SetRect(cut.rectTransform, new Vector2(0f, cy),
                new Vector2(HighlandUi.L(20f), HighlandUi.L(12f)));
            Image gem = NewImage("Crest", panelRect, Vis(0xF4, 0xDF, 0x73));
            gem.sprite = HighlandUi.DiamondRect(Mathf.RoundToInt(HighlandUi.L(12.4f)),
                Mathf.RoundToInt(HighlandUi.L(16.4f)), false, 1.7f * HighlandUi.S,
                generatedTextures, generatedSprites, "ResultCrest");
            SetRect(gem.rectTransform, new Vector2(0f, cy),
                new Vector2(HighlandUi.L(12.4f), HighlandUi.L(16.4f)));
        }

        BuildHeader(panelRect);
        BuildRank(panelRect);
        BuildRows(panelRect);
        BuildActions(contentRect);
        BuildRankingOverlay(panelRect);
        BuildAudio();
    }

    // 上段: RESULT → 菱形 → STAGE CLEAR → 罫線 → 舞台名 → 罫線
    private void BuildHeader(RectTransform panel)
    {
        GameObject headGo = NewRect("Header", panel);
        RectTransform head = (RectTransform)headGo.transform;
        SetRect(head, Vector2.zero, new Vector2(PanelW, PanelH));
        headerGroup = headGo.AddComponent<CanvasGroup>();

        // 「結果」(ふりがな けっか)。
        HighlandUi.RubyText result = Ruby("ResultLabel", head, "[結果|けっか]", 23.5f,
            HighlandUi.InkSoft, TextAlignmentOptions.Center, false, 6f);
        HighlandUi.PlaceCentered(result.Body, 836f, 108.5f, 460f, 23.5f);

        // その下の銀の罫(両端が消える)+ 中空の白菱形。
        AddFadeRule(head, 123f, 482f, 26f, false);
        AddCrest(head, 123f, 9.6f, 12f, 1.55f, Vis(0xDE, 0xDE, 0xDE));

        // 「ステージクリア」。暖色のにじみを背後に敷く。
        Image clearHalo = NewImage("ClearHalo", head, new Color(1f, 1f, 1f, 0.30f));
        clearHalo.sprite = HighlandUi.Halo(generatedTextures, generatedSprites, "ResultClearHalo");
        SetRect(clearHalo.rectTransform, new Vector2(0f, HighlandUi.Y(166f)),
            new Vector2(HighlandUi.L(526f), HighlandUi.L(98f)));

        verdictText = HighlandUi.Text("Verdict", head, "ステージクリア", 49f, HighlandUi.Ink,
            TextAlignmentOptions.Center, true, 3.6f);
        HighlandUi.PlaceCentered(verdictText, 836f, 184f, 700f, 49f);
        ApplyTextGlow(verdictText, new Color(0.78f, 0.62f, 0.30f, 0.55f), 0.075f, 0.58f);

        AddFadeRule(head, 206f, 510f, 26f, true);
        AddCrest(head, 206f, 9.4f, 13.4f, 1.55f, Vis(0xFF, 0xE4, 0x70));

        GameObject titleGo = NewRect("TitleGroup", panel);
        RectTransform titleRect = (RectTransform)titleGo.transform;
        SetRect(titleRect, Vector2.zero, new Vector2(PanelW, PanelH));
        titleGroup = titleGo.AddComponent<CanvasGroup>();

        stageTitleRuby = Ruby("StageTitle", titleRect, "", 43f, HighlandUi.Ink,
            TextAlignmentOptions.Center, true, 10.1f);
        stageTitleText = stageTitleRuby.Body;
        HighlandUi.PlaceCentered(stageTitleText, 836f, 270f, 640f, 43f);
        ApplyTextGlow(stageTitleText, new Color(0.62f, 0.68f, 0.85f, 0.32f), 0.055f, 0.55f);

        AddFadeRule(titleRect, 286f, 339f, 30f, true);
        AddCrest(titleRect, 286f, 10.4f, 13.6f, 1.55f, Vis(0xFF, 0xE4, 0x70));
    }

    // v11 の飾り罫(両端が透明へ消える。gold=金 / それ以外は銀)。
    private void AddFadeRule(Transform parent, float svgY, float svgW, float svgGap, bool gold)
    {
        Color32[] cols = gold
            ? new[] { Col(0xBC, 0xAA, 0x4E), Col(0xBC, 0xAA, 0x4E), Col(0xFF, 0xE1, 0x6A),
                      Col(0xBC, 0xAA, 0x4E), Col(0xBC, 0xAA, 0x4E) }
            : new[] { Col(0xD6, 0xD6, 0xD6), Col(0xAD, 0xAD, 0xAD), Col(0xD0, 0xD0, 0xD0),
                      Col(0xAD, 0xAD, 0xAD), Col(0xD6, 0xD6, 0xD6) };
        float[] offs = gold ? new[] { 0f, 0.22f, 0.5f, 0.78f, 1f } : new[] { 0f, 0.18f, 0.5f, 0.82f, 1f };
        float[] al = gold ? new[] { 0f, 0.30f, 0.95f, 0.30f, 0f } : new[] { 0f, 0.45f, 0.72f, 0.45f, 0f };
        int w = Mathf.RoundToInt(HighlandUi.L(svgW));
        Image img = NewImage("Rule", parent, Color.white);
        img.sprite = HighlandUi.FadeRule(w, 12, 1.1f * HighlandUi.S, HighlandUi.L(svgGap),
            offs, cols, al, generatedTextures, generatedSprites, "ResultRule" + Mathf.RoundToInt(svgY));
        img.type = Image.Type.Simple;
        SetRect(img.rectTransform, new Vector2(0f, HighlandUi.Y(svgY)), new Vector2(w, 12f));
    }

    // 罫の中央に置く中空の菱形(+ にじみ)。
    private void AddCrest(Transform parent, float svgY, float svgHalfW, float svgHalfH,
        float strokeSvg, Color color)
    {
        Image halo = NewImage("CrestHalo", parent, new Color(1f, 1f, 1f, 0.85f));
        halo.sprite = HighlandUi.Halo(generatedTextures, generatedSprites, "ResultCrestHalo");
        SetRect(halo.rectTransform, new Vector2(0f, HighlandUi.Y(svgY)),
            new Vector2(HighlandUi.L(svgHalfW * 4.6f), HighlandUi.L(svgHalfH * 3.7f)));
        int w = Mathf.RoundToInt(HighlandUi.L(svgHalfW * 2f));
        int h = Mathf.RoundToInt(HighlandUi.L(svgHalfH * 2f));
        Image gem = NewImage("Crest", parent, color);
        gem.sprite = HighlandUi.DiamondRect(w, h, false, strokeSvg * HighlandUi.S,
            generatedTextures, generatedSprites, "ResultCrest" + Mathf.RoundToInt(svgY));
        gem.type = Image.Type.Simple;
        SetRect(gem.rectTransform, new Vector2(0f, HighlandUi.Y(svgY)), new Vector2(w, h));
    }

    private static Color32 Col(byte r, byte g, byte b) { return new Color32(r, g, b, 0xFF); }

    // HighlandUi.Place* はパネル中心基準で置くので、行の入れ物(中心 = rowCy)の
    // 中に入れるぶんだけ縦に戻す。ふりがなは Tick の EnsurePlaced で追従する。
    private static void OffsetInRow(HighlandUi.RubyText r, float rowSvgCy)
    {
        RectTransform rt = (RectTransform)r.Body.transform;
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x,
            rt.anchoredPosition.y - HighlandUi.Y(rowSvgCy));
    }

    private HighlandUi.RubyText Ruby(string name, Transform parent, string markup, float svgSize,
        Color color, TextAlignmentOptions align, bool bold, float tracking)
    {
        HighlandUi.RubyText r = HighlandUi.Ruby(name, parent, markup, svgSize, color, align, bold, tracking);
        rubies.Add(r);
        rubiesPlaced = false;
        return r;
    }

    // 中段: 月桂樹に囲まれた大きな金のランク字
    private void BuildRank(RectTransform panel)
    {
        GameObject go = NewRect("RankGroup", panel);
        rankGroupRect = (RectTransform)go.transform;
        SetRect(rankGroupRect, Vector2.zero, new Vector2(PanelW, PanelH));
        rankGroup = go.AddComponent<CanvasGroup>();

        // 背景の同心リング(参考画像の薄い金の円 3 本)。焼き込み 1 枚で持つ。
        rankRings = NewImage("RankRings", rankGroupRect, Color.white);
        rankRings.sprite = CreateRankRingsSprite();
        rankRings.type = Image.Type.Simple;
        SetRect(rankRings.rectTransform, new Vector2(0f, YRank), new Vector2(400f, 400f));

        rankHalo = NewGraphic<SoftCircleGraphic>("RankHalo", rankGroupRect);
        rankHalo.color = new Color(GoldAccent.r, GoldAccent.g, GoldAccent.b, 0.005f);
        SetRect(rankHalo.rectTransform, new Vector2(0f, YRank), new Vector2(330f, 330f));

        // 月桂樹: 左半分を焼き、右は localScale.x=-1 で反転(左右対称)。
        Sprite laurel = CreateLaurelSprite();
        laurelLeft = NewImage("LaurelL", rankGroupRect, Color.white);
        laurelLeft.sprite = laurel;
        SetRect(laurelLeft.rectTransform, new Vector2(0f, YLaurel), new Vector2(LaurelBox, LaurelBox));
        laurelRight = NewImage("LaurelR", rankGroupRect, Color.white);
        laurelRight.sprite = laurel;
        SetRect(laurelRight.rectTransform, new Vector2(0f, YLaurel), new Vector2(LaurelBox, LaurelBox));
        laurelRight.rectTransform.localScale = new Vector3(-1f, 1f, 1f);

        // 月桂樹の結び(下端中央の縦長の菱形)。
        laurelKnot = NewImage("LaurelKnot", rankGroupRect, GoldAccent);
        laurelKnot.sprite = CreateDiamondRingSprite();
        laurelKnot.type = Image.Type.Simple;
        SetRect(laurelKnot.rectTransform, new Vector2(0f, YKnot), new Vector2(23f, 31f));

        // v11: 見出しは「ランク」、ランク字は白(#FFFFFF)。
        TMP_Text rankLabel = HighlandUi.Text("RankLabel", rankGroupRect, "ランク", 27f,
            HighlandUi.Ink, TextAlignmentOptions.Center, false, 1.1f);
        HighlandUi.PlaceCentered(rankLabel, 836f, 361f, 300f, 27f);
        ApplyTextGlow(rankLabel, new Color(0.72f, 0.55f, 0.28f, 0.45f), 0.055f, 0.5f);

        rankText = NewText("Rank", rankGroupRect, "A", 138.5f * HighlandUi.S, HighlandUi.Ink, TextAlignmentOptions.Center);
        rankText.font = HighlandUi.SerifBold;
        SetRect((RectTransform)rankText.transform, new Vector2(0f, YRankText), new Vector2(460f, 260f));
        rankGlowMat = ApplyTextGlow(rankText, new Color(0.94f, 0.72f, 0.34f, 0.45f), 0.060f, 0.40f);

        rankText2 = NewText("Rank2", rankGroupRect, "A", 138.5f * HighlandUi.S, HighlandUi.Ink, TextAlignmentOptions.Center);
        rankText2.font = HighlandUi.SerifBold;
        SetRect((RectTransform)rankText2.transform, new Vector2(0f, YRankText), new Vector2(460f, 260f));
        rankGlowMat2 = ApplyTextGlow(rankText2, new Color(0.94f, 0.72f, 0.34f, 0.45f), 0.060f, 0.40f);
        rankText2.gameObject.SetActive(false);

        p1RankTag = NewText("P1Tag", rankGroupRect, "1P", 26f, GoldDim, TextAlignmentOptions.Center);
        p1RankTag.characterSpacing = 8f;
        SetRect((RectTransform)p1RankTag.transform, new Vector2(-144f, YRankLabel), new Vector2(120f, 34f));
        p1RankTag.gameObject.SetActive(false);

        p2RankTag = NewText("P2Tag", rankGroupRect, "2P", 26f, GoldDim, TextAlignmentOptions.Center);
        p2RankTag.characterSpacing = 8f;
        SetRect((RectTransform)p2RankTag.transform, new Vector2(144f, YRankLabel), new Vector2(120f, 34f));
        p2RankTag.gameObject.SetActive(false);

        // 左右の小さな菱形(参考画像 x=±192 / y=60)。
        AddDiamond(rankGroupRect, new Vector2(-192f, 60f), 13f, new Color(GoldDim.r, GoldDim.g, GoldDim.b, 0.85f));
        AddDiamond(rankGroupRect, new Vector2(192f, 60f), 13f, new Color(GoldDim.r, GoldDim.g, GoldDim.b, 0.85f));
    }

    // v11 の情報行(ふりがな付き)。
    private static readonly string[] RowLabelText =
        { "スコア", "[当|あ]たった[回数|かいすう]", "[難|むずか]しさ", "ランキング" };

    private void BuildRows(RectTransform panelParent)
    {
        // 情報行はまとめて 1 つの CanvasGroup に入れる(ランキングのパネルが出ている
        // あいだ、まとめて隠すため。TMP は独自マテリアルなので Image では隠せない)。
        GameObject host = NewRect("Rows", panelParent);
        RectTransform panel = (RectTransform)host.transform;
        SetRect(panel, Vector2.zero, new Vector2(PanelW, PanelH));
        rowsGroup = host.AddComponent<CanvasGroup>();

        // 行の間の薄い区切り線(v11 は 4 行の上下に 5 本・両端が消える銀)。
        float[] ruleY = { 558f, 610f, 658f, 707f, 760f };
        for (int i = 0; i <= RowCount; i++)
        {
            int w = Mathf.RoundToInt(HighlandUi.L(484f));
            Image line = NewImage("RowSep" + i, panel, new Color(1f, 1f, 1f, 0.75f));
            line.sprite = HighlandUi.FadeRule(w, 12, 1f * HighlandUi.S, 0f,
                new[] { 0f, 0.18f, 0.5f, 0.82f, 1f },
                new[] { Col(0xD6, 0xD6, 0xD6), Col(0xAD, 0xAD, 0xAD), Col(0xD0, 0xD0, 0xD0),
                        Col(0xAD, 0xAD, 0xAD), Col(0xD6, 0xD6, 0xD6) },
                new[] { 0f, 0.45f, 0.72f, 0.45f, 0f },
                generatedTextures, generatedSprites, "ResultTableRule");
            line.type = Image.Type.Simple;
            SetRect(line.rectTransform, new Vector2(0f, HighlandUi.Y(ruleY[i])), new Vector2(w, 12f));
        }

        // 行の中心(v11 のアイコン y)と、ラベル/値のベースライン。
        float[] rowCy = { 586f, 634f, 683f, 733f };
        float[] labelBase = { 592.5f, 647f, 696f, 739.5f };
        float[] valueBase = { 595.5f, 643.5f, 691.5f, 742.5f };
        float[] valueSize = { 27f, 27f, 24f, 27f };

        Sprite[] icons = CreateRowIconSprites();
        for (int i = 0; i < RowCount; i++)
        {
            GameObject rowGo = NewRect("Row" + i, panel);
            RectTransform rect = (RectTransform)rowGo.transform;
            Vector2 home = new Vector2(0f, HighlandUi.Y(rowCy[i]));
            rowHomeY[i] = home.y;
            SetRect(rect, home, new Vector2(PanelW, RowPitch));
            rowRects[i] = rect;
            rowGroups[i] = rowGo.AddComponent<CanvasGroup>();

            Image icon = NewImage("Icon", rect, Vis(0xED, 0xED, 0xED));
            icon.sprite = icons[i];
            icon.type = Image.Type.Simple;
            SetRect(icon.rectTransform, new Vector2(HighlandUi.X(647f), 0f),
                new Vector2(HighlandUi.L(34f), HighlandUi.L(34f)));
            rowIcons[i] = icon;

            HighlandUi.RubyText labelRuby = Ruby("Label", rect, RowLabelText[i], 20.5f,
                HighlandUi.InkSoft, TextAlignmentOptions.Left, false, 1.1f);
            TMP_Text label = labelRuby.Body;
            HighlandUi.PlaceLeft(label, 696f, labelBase[i], 300f, 20.5f);
            // 行は中心が (0, rowCy) なので、ベースラインから得た絶対 y を行内の相対へ直す。
            OffsetInRow(labelRuby, rowCy[i]);
            rowLabels[i] = label;

            Image sep = NewImage("Sep", rect, new Color(0.839f, 0.839f, 0.839f, 0.77f));
            sep.sprite = HighlandUi.SolidBar(generatedTextures, generatedSprites);
            SetRect(sep.rectTransform, new Vector2(HighlandUi.X(875f), 0f),
                new Vector2(HighlandUi.L(1f), HighlandUi.L(24.8f)));
            rowSeparators[i] = sep;

            TMP_Text value = HighlandUi.Text("Value", rect, "", valueSize[i], HighlandUi.Ink,
                TextAlignmentOptions.Left, false, 1.4f);
            HighlandUi.PlaceLeft(value, 914f, valueBase[i], 300f, valueSize[i]);
            value.rectTransform.anchoredPosition = new Vector2(
                value.rectTransform.anchoredPosition.x,
                value.rectTransform.anchoredPosition.y - HighlandUi.Y(rowCy[i]));
            rowValues[i] = value;

            TMP_Text value2 = HighlandUi.Text("Value2", rect, "", valueSize[i], HighlandUi.Ink,
                TextAlignmentOptions.Right, false, 1.4f);
            HighlandUi.PlaceRight(value2, 1078f, valueBase[i], 300f, valueSize[i]);
            value2.rectTransform.anchoredPosition = new Vector2(
                value2.rectTransform.anchoredPosition.x,
                value2.rectTransform.anchoredPosition.y - HighlandUi.Y(rowCy[i]));
            value2.gameObject.SetActive(false);
            rowValues2[i] = value2;
        }

        columnTagP1 = NewText("ColTagP1", panel, "1P", 18f, GoldDim, TextAlignmentOptions.Right);
        columnTagP1.characterSpacing = 8f;
        SetRect((RectTransform)columnTagP1.transform, new Vector2(103f - 90f, HighlandUi.Y(548f)), new Vector2(180f, 26f));
        columnTagP1.gameObject.SetActive(false);

        columnTagP2 = NewText("ColTagP2", panel, "2P", 18f, GoldDim, TextAlignmentOptions.Right);
        columnTagP2.characterSpacing = 8f;
        SetRect((RectTransform)columnTagP2.transform, new Vector2(RowValueRight - 90f, HighlandUi.Y(548f)), new Vector2(180f, 26f));
        columnTagP2.gameObject.SetActive(false);
    }

    // 下段: 金縁の暗い横長ボタン「次へ」+ 小さな文字リンク(もう一度 / タイトルへ)
    private void BuildActions(RectTransform content)
    {
        GameObject go = NewRect("Actions", content);
        buttonRect = (RectTransform)go.transform;
        SetRect(buttonRect, Vector2.zero, new Vector2(1200f, PanelH));
        buttonHome = buttonRect.anchoredPosition;
        buttonGroup = go.AddComponent<CanvasGroup>();

        // --- 主ボタン(次へ = ステージ選択) ---
        GameObject btnGo = NewRect("NextButton", buttonRect);
        RectTransform btnRect = (RectTransform)btnGo.transform;
        SetRect(btnRect, new Vector2(0f, YButton), new Vector2(ButtonW, ButtonH));
        actionRects[0] = btnRect;
        actionValues[0] = Action.StageSelect;

        // 外側のにじむ金の光。板より一回り大きい別 Image で持つ。
        nextButtonGlow = NewImage("Glow", btnRect, new Color(GoldAccent.r, GoldAccent.g, GoldAccent.b, 0.13f));
        nextButtonGlow.sprite = CreateButtonGlowSprite();
        nextButtonGlow.type = Image.Type.Simple;
        SetRect(nextButtonGlow.rectTransform, Vector2.zero, new Vector2(ButtonW + 22f, ButtonH + 22f));

        nextButtonBody = NewImage("Body", btnRect, Color.white);
        nextButtonBody.sprite = HighlandUi.NotchPanel((int)ButtonW, (int)ButtonH,
            HighlandUi.L(8.5f), true, generatedTextures, generatedSprites, "ResultNextV11",
            HighlandUi.L(4.5f), 1.8f * HighlandUi.S, 0.7f * HighlandUi.S);
        nextButtonBody.type = Image.Type.Simple;
        Stretch(nextButtonBody.rectTransform);
        nextButtonBody.raycastTarget = true;

        // v11: 〇 の操作アイコン + ふりがな付きの「次へ」。
        Image btnIcon = NewImage("Icon", btnRect, Color.white);
        btnIcon.sprite = HighlandUi.Icon("circle");
        SetRect(btnIcon.rectTransform, new Vector2(HighlandUi.L(-33f), 0f),
            new Vector2(HighlandUi.L(51.6f), HighlandUi.L(51.6f)));

        HighlandUi.RubyText nextRuby = Ruby("Label", btnRect, "[次|つぎ]へ", 26f,
            HighlandUi.Ink, TextAlignmentOptions.Left, true, 1f);
        nextLabel = nextRuby.Body;
        RectTransform nlr = (RectTransform)nextLabel.transform;
        nlr.anchorMin = nlr.anchorMax = new Vector2(0.5f, 0.5f);
        nlr.pivot = new Vector2(0f, 0.5f);
        nlr.sizeDelta = new Vector2(HighlandUi.L(160f), HighlandUi.L(42f));
        nlr.anchoredPosition = new Vector2(HighlandUi.L(0.68f), HighlandUi.L(-4f));
        ApplyTextGlow(nextLabel, new Color(0.70f, 0.56f, 0.28f, 0.45f), 0.05f, 0.5f);
        actionLabels[0] = nextLabel;

        Button button = btnGo.AddComponent<Button>();
        button.targetGraphic = nextButtonBody;
        button.transition = Selectable.Transition.None;
        Navigation nav = button.navigation;
        nav.mode = Navigation.Mode.None;
        button.navigation = nav;
        button.onClick.AddListener(() => { SetActionSelection(0); RequestAction(actionValues[0]); });
        EventTrigger trig = btnGo.AddComponent<EventTrigger>();
        AddTrigger(trig, EventTriggerType.PointerEnter, _ => SetActionSelection(0));

        // --- 文字リンク(ボタンの左右。下は引き継ぎコードの 1 行だけにする) ---
        BuildLink(1, new Vector2(-XLinks, YButton), "もう一度", Action.Retry);
        BuildLink(2, new Vector2(XLinks, YButton), "タイトルへ", Action.Title);

        transferCodeLine = NewText("TransferCodeLine", buttonRect, "", 13f,
            new Color(GoldDim.r, GoldDim.g, GoldDim.b, 0.9f), TextAlignmentOptions.Center);
        transferCodeLine.characterSpacing = 4f;
        SetRect((RectTransform)transferCodeLine.transform, new Vector2(0f, YTransfer), new Vector2(600f, 20f));
        transferCodeLine.gameObject.SetActive(false);

        selectedActionIndex = 0;
        RefreshActionSelection();
    }

    private void BuildLink(int slot, Vector2 pos, string labelText, Action action)
    {
        GameObject go = NewRect("Link" + slot, buttonRect);
        RectTransform rect = (RectTransform)go.transform;
        SetRect(rect, pos, new Vector2(LinkW, 30f));
        actionRects[slot] = rect;
        actionValues[slot] = action;

        TMP_Text label = NewText("Label", rect, labelText, LinkFont,
            new Color(SilverLabel.r, SilverLabel.g, SilverLabel.b, 0.78f), TextAlignmentOptions.Center);
        label.characterSpacing = 4f;
        Stretch((RectTransform)label.transform);
        TmpAlign.CenterInkVertically(label);
        actionLabels[slot] = label;

        Image underline = NewImage("Underline", rect, new Color(GoldAccent.r, GoldAccent.g, GoldAccent.b, 0f));
        SetRect(underline.rectTransform, new Vector2(0f, -15f), new Vector2(LinkW - 12f, 1.2f));
        actionUnderlines[slot] = underline;

        Image hit = NewImage("Hit", rect, new Color(0f, 0f, 0f, 0f));
        Stretch(hit.rectTransform);
        hit.raycastTarget = true;
        Button button = go.AddComponent<Button>();
        button.targetGraphic = hit;
        button.transition = Selectable.Transition.None;
        Navigation nav = button.navigation;
        nav.mode = Navigation.Mode.None;
        button.navigation = nav;
        int captured = slot;
        button.onClick.AddListener(() => { SetActionSelection(captured); RequestAction(actionValues[captured]); });
        EventTrigger trig = go.AddComponent<EventTrigger>();
        AddTrigger(trig, EventTriggerType.PointerEnter, _ => SetActionSelection(captured));
    }

    // ランキングのイニシャル入力 / Top10 盤面。パネル下半分(罫線から下)を同じ様式で
    // 覆い、ボタン領域を置き換える(項目 3)。開閉は 0.2 秒のフェード。
    private void BuildRankingOverlay(RectTransform panel)
    {
        const float overlayH = 628f + PanelExtendDown;
        const float overlayY = -(PanelH * 0.5f + PanelExtendDown) + overlayH * 0.5f + 10f;

        GameObject go = NewRect("RankingOverlay", panel);
        rankingOverlayRoot = go;
        RectTransform rect = (RectTransform)go.transform;
        SetRect(rect, new Vector2(0f, overlayY), new Vector2(PanelW - 24f, overlayH));
        rankingOverlayCG = go.AddComponent<CanvasGroup>();

        Image fill = NewImage("Fill", rect, Color.white);
        fill.sprite = CreateOverlaySprite();
        fill.type = Image.Type.Sliced;      // Simple だと 2px の金線が太い額縁に伸びる
        Stretch(fill.rectTransform);

        rankingOverlayHeading = NewText("Heading", rect, "", 32f, GoldAccent, TextAlignmentOptions.Center);
        rankingOverlayHeading.characterSpacing = 10f;
        SetRect((RectTransform)rankingOverlayHeading.transform, new Vector2(0f, overlayH * 0.5f - 50f), new Vector2(560f, 44f));
        ApplyTextGlow(rankingOverlayHeading, new Color(0.78f, 0.50f, 0.18f, 0.55f), 0.06f, 0.5f);
        Image ovOrn = NewImage("HeadOrnament", rect, Color.white);
        ovOrn.sprite = CreateOrnamentRuleSprite();
        ovOrn.type = Image.Type.Simple;
        SetRect(ovOrn.rectTransform, new Vector2(0f, overlayH * 0.5f - 84f), new Vector2(312f, 26f));

        // --- イニシャル入力(3 文字) ---
        initialsGroup = NewRect("Initials", rect);
        SetRect((RectTransform)initialsGroup.transform, Vector2.zero, new Vector2(PanelW - 24f, overlayH));
        const float slotW = 82f;
        const float slotGap = 26f;
        float startX = -(slotW + slotGap) * (RankingStore.NameLength - 1) * 0.5f;
        for (int i = 0; i < RankingStore.NameLength; i++)
        {
            TMP_Text slot = NewText("Slot" + i, initialsGroup.transform, "A", 62f, GoldBright, TextAlignmentOptions.Center);
            SetRect((RectTransform)slot.transform, new Vector2(startX + i * (slotW + slotGap), 108f), new Vector2(slotW, 88f));
            initialsSlotTexts[i] = slot;
            Image bar = NewImage("Bar" + i, initialsGroup.transform, new Color(GoldDim.r, GoldDim.g, GoldDim.b, 0.85f));
            bar.sprite = CreateRuleSprite();
            bar.type = Image.Type.Simple;
            SetRect(bar.rectTransform, new Vector2(startX + i * (slotW + slotGap), 56f), new Vector2(slotW - 6f, 3f));
        }
        initialsScoreLine = NewText("ScoreLine", initialsGroup.transform, "", 26f, GoldAccent,
            TextAlignmentOptions.Center);
        initialsScoreLine.characterSpacing = 6f;
        SetRect((RectTransform)initialsScoreLine.transform, new Vector2(0f, 14f), new Vector2(520f, 36f));

        TMP_Text hint = NewText("Hint", initialsGroup.transform,
            "↑↓ 文字送り / ←→ 桁移動 / A 決定(3 桁目で登録) / B 戻る", 17f,
            new Color(SilverLabel.r, SilverLabel.g, SilverLabel.b, 0.72f), TextAlignmentOptions.Center);
        SetRect((RectTransform)hint.transform, new Vector2(0f, -34f), new Vector2(600f, 28f));

        // --- 登録後の Top10 盤面 ---
        boardGroup = NewRect("Board", rect);
        SetRect((RectTransform)boardGroup.transform, Vector2.zero, new Vector2(PanelW - 24f, overlayH));
        boardHeaderText = NewText("BoardHeader", boardGroup.transform, "", 20f, SilverLabel, TextAlignmentOptions.Center);
        boardHeaderText.characterSpacing = 6f;
        SetRect((RectTransform)boardHeaderText.transform, new Vector2(0f, overlayH * 0.5f - 108f), new Vector2(520f, 28f));
        for (int i = 0; i < boardRowTexts.Length; i++)
        {
            TMP_Text row = NewText("Row" + i, boardGroup.transform, "", 21f, InkWhite, TextAlignmentOptions.Left);
            SetRect((RectTransform)row.transform, new Vector2(20f, overlayH * 0.5f - 146f - i * 38f), new Vector2(420f, 30f));
            boardRowTexts[i] = row;
        }
        TMP_Text boardHint = NewText("BoardHint", boardGroup.transform, "B で戻る", 17f,
            new Color(SilverLabel.r, SilverLabel.g, SilverLabel.b, 0.7f), TextAlignmentOptions.Center);
        SetRect((RectTransform)boardHint.transform, new Vector2(0f, -(overlayH * 0.5f - 34f)), new Vector2(360f, 26f));

        rankingOverlayCG.alpha = 0f;
        go.SetActive(false);
    }

    private void BuildAudio()
    {
        GameObject go = new GameObject("ResultScreenBgm");
        go.transform.SetParent(transform.parent, false);
        bgmSource = go.AddComponent<AudioSource>();
        bgmSource.playOnAwake = false;
        bgmSource.spatialBlend = 0f;
        bgmSource.loop = true;
        bgmSource.volume = ResultBgmVolume;

        resultBgm = Resources.Load<AudioClip>("BGM/result_killing_party");
        if (resultBgm != null)
        {
            bgmSource.clip = resultBgm;
            resultBgm.LoadAudioData();
        }
    }

    // =======================================================================
    //  表示データの流し込み
    // =======================================================================

    public void Prepare(StageData stage, int difficulty, bool cleared, int hitCount,
        int counterCount, float elapsedSeconds, float endSeconds,
        bool twoPlayer = false, int hitCount2 = 0)
    {
        gameObject.SetActive(true);
        inputArmed = false;
        entering = false;
        selectedActionIndex = 0;
        navLeftPrev = false;
        navRightPrev = false;
        RefreshActionSelection();
        TmpAlign.CenterInkVertically(nextLabel);

        resultCleared = cleared;
        verdictText.text = cleared ? "ステージクリア" : "ステージしっぱい";
        verdictText.color = cleared ? InkWhite : FailRed;

        // 見出しは既存のステージ名(石工 / 放浪者 / 艦長 / 浮浪者)。舞台名の仮置き
        // (StageCityProfile.stageTitle)は使わない ＝ ステージ選択・ランキング見出しと同じ名前で
        // 一貫させる(2026-09-16 ユーザー決定「名前は変えないで、元のまま」)。
        if (stageTitleRuby != null)
        {
            stageTitleRuby.Apply(StageCityProfile.ReadingMarkupOf(stage));
            rubiesPlaced = false;
        }
        else stageTitleText.text = StageCityProfile.DisplayNameOf(stage);

        int provisionalScore = CalculateProvisionalScore(
            cleared, hitCount, counterCount, elapsedSeconds, endSeconds);
        finalScore = provisionalScore;
        finalHit = Mathf.Max(0, hitCount);

        string rank = EvaluateRank(cleared, hitCount, difficulty);
        rankText.text = rank;
        rankText.color = cleared ? GoldBright : FailRed;
        if (rankGlowMat != null)
            rankGlowMat.SetColor(ShaderUtilities.ID_UnderlayColor,
                cleared ? new Color(0.92f, 0.62f, 0.22f, 0.55f) : new Color(0.75f, 0.12f, 0.20f, 0.5f));
        if (rankHalo != null)
        {
            rankHaloBaseAlpha = cleared ? 0.005f : 0.010f;
            rankHalo.color = cleared
                ? new Color(GoldAccent.r, GoldAccent.g, GoldAccent.b, rankHaloBaseAlpha)
                : new Color(0.55f, 0.08f, 0.12f, rankHaloBaseAlpha);
        }
        // 月桂樹は金のグラデーションを焼き込んであるので、クリアは白(素のまま)。
        Color laurelCol = cleared ? Color.white : new Color(0.62f, 0.26f, 0.26f, 0.95f);
        if (laurelLeft != null) laurelLeft.color = laurelCol;
        if (laurelRight != null) laurelRight.color = laurelCol;
        if (laurelKnot != null)
            laurelKnot.color = cleared ? GoldAccent : new Color(0.62f, 0.26f, 0.26f, 0.95f);
        if (rankRings != null)
            rankRings.color = cleared ? Color.white : new Color(1f, 0.55f, 0.55f, 1f);

        // 情報行の固定値(SCORE/HITS は入場のカウントアップで動く)。
        rowValues[RowDifficulty].text = DifficultyName(difficulty);
        rowValues[RowRanking].text = "—";

        if (twoPlayer)
            ApplyTwoPlayerResult(cleared, hitCount, hitCount2,
                counterCount, elapsedSeconds, endSeconds, provisionalScore, difficulty);
        else
            RestoreOnePlayerLayout();

        // ランキング登録の判定(SPEC §2.2)。
        string rankStageDir = stage != null
            ? (string.IsNullOrWhiteSpace(stage.stageDirectoryName) ? stage.stageName : stage.stageDirectoryName)
            : null;
        pendingRankStage = rankStageDir;
        pendingRankDifficulty = Mathf.Clamp(difficulty, 0, DirectionTransferCode.DifficultyCount - 1);
        pendingRankMode = twoPlayer ? "2P" : "1P";
        pendingRankScore = twoPlayer ? Mathf.Max(provisionalScore, finalScore2) : provisionalScore;
        rankingQualifies = !string.IsNullOrEmpty(rankStageDir)
            && RankingStore.QualifiesForTop(rankStageDir, pendingRankDifficulty, pendingRankMode, pendingRankScore);
        rankingFlowState = RankingFlowState.None;
        rankingSubmitted = false;
        pendingEntryId = null;
        rankingOverlayTarget = 0f;
        if (rankingOverlayCG != null) rankingOverlayCG.alpha = 0f;
        if (rankingOverlayRoot != null) rankingOverlayRoot.SetActive(false);

        // 引き継ぎコード発行(SPEC §1.3)。1P 専用(§1.4)。
        if (transferCodeLine != null)
        {
            bool showTransfer = !twoPlayer && TransferAchievements.HasAnyAchievement;
            transferCodeLine.gameObject.SetActive(showTransfer);
            if (showTransfer)
            {
                string code = DirectionTransferCode.Encode(TransferAchievements.BuildPayload());
                transferCodeLine.text = "ひきつぎコード " + code;
            }
        }

        entranceFinished = false;
        contentGroup.alpha = 1f;
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.localScale = Vector3.one;
        blurTargetAlpha = 0f;
        if (blurImage != null) blurImage.color = new Color(1f, 1f, 1f, 0f);
        StopEntranceRoutine();
        ApplyEntranceFrame(0f);
    }

    // 2P: ランクを左右 2 つに、情報行を「P1 値 ｜ P2 値」の 2 列にする。
    private void ApplyTwoPlayerResult(bool cleared, int hit1, int hit2,
        int counterCount, float elapsedSeconds, float endSeconds, int score1, int difficulty)
    {
        twoPlayerResult = true;
        finalScore2 = CalculateProvisionalScore(cleared, hit2, counterCount, elapsedSeconds, endSeconds);
        finalHit2 = Mathf.Max(0, hit2);
        twoPlayerSidesReversed = GManager.Control != null && GManager.Control.PlayerSidesReversed;
        bool p1OnLeft = PlayerIndexForResultSide(false, twoPlayerSidesReversed) == 0;

        string rank1 = EvaluateRank(cleared, hit1, difficulty);
        string rank2 = EvaluateRank(cleared, hit2, difficulty);
        const float rankScale = 0.62f;
        const float rankOffset = 144f;
        // 月桂樹はランク 1 文字を囲む飾りなので、2 つ並ぶ 2P では出さない(結びも同じ)。
        if (laurelLeft != null) laurelLeft.gameObject.SetActive(false);
        if (laurelRight != null) laurelRight.gameObject.SetActive(false);
        if (laurelKnot != null) laurelKnot.gameObject.SetActive(false);
        if (rankRings != null) rankRings.gameObject.SetActive(false);
        string leftRank = p1OnLeft ? rank1 : rank2;
        string rightRank = p1OnLeft ? rank2 : rank1;

        rankText.text = leftRank;
        rankText.rectTransform.anchoredPosition = new Vector2(-rankOffset, YRankText);
        rankText.rectTransform.localScale = Vector3.one * rankScale;
        rankText2.gameObject.SetActive(true);
        rankText2.text = rightRank;
        rankText2.rectTransform.anchoredPosition = new Vector2(rankOffset, YRankText);
        rankText2.rectTransform.localScale = Vector3.one * rankScale;
        rankText2.color = rankText.color;
        if (rankGlowMat2 != null && rankGlowMat != null)
            rankGlowMat2.SetColor(ShaderUtilities.ID_UnderlayColor,
                rankGlowMat.GetColor(ShaderUtilities.ID_UnderlayColor));

        p1RankTag.gameObject.SetActive(true);
        p2RankTag.gameObject.SetActive(true);
        ((RectTransform)p1RankTag.transform).anchoredPosition =
            new Vector2(p1OnLeft ? -rankOffset : rankOffset, YRankLabel);
        ((RectTransform)p2RankTag.transform).anchoredPosition =
            new Vector2(p1OnLeft ? rankOffset : -rankOffset, YRankLabel);

        // 情報行: SCORE/HITS は 2 列、DIFFICULTY/RANKING は共通なので 1 列のまま。
        for (int i = 0; i < RowCount; i++)
        {
            bool split = i == RowScore || i == RowHits;
            rowValues2[i].gameObject.SetActive(split);
            rowSeparators[i].gameObject.SetActive(!split);
            if (split)
            {
                // 2 列になる行だけ右揃え(数字の桁をそろえる)。v11 の表の左右端へ。
                PlaceValue(rowValues[i], 875f, true, 24f);
                PlaceValue(rowValues2[i], 1078f, true, 24f);
            }
        }
        columnTagP1.gameObject.SetActive(true);
        columnTagP2.gameObject.SetActive(true);
        columnTagP1.text = p1OnLeft ? "1P" : "2P";
        columnTagP2.text = p1OnLeft ? "2P" : "1P";
    }

    // 情報行の値を v11 の列へ置く(svgX は左揃えなら左端・右揃えなら右端)。
    private static void PlaceValue(TMP_Text text, float svgX, bool rightAligned, float svgSize)
    {
        RectTransform r = (RectTransform)text.transform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(rightAligned ? 1f : 0f, 0.5f);
        r.anchoredPosition = new Vector2(HighlandUi.X(svgX), 0f);
        text.alignment = rightAligned ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
        text.fontSize = svgSize * HighlandUi.S;
    }

    public static int PlayerIndexForResultSide(bool rightSide, bool reversed)
    {
        return (rightSide ? 1 : 0) ^ (reversed ? 1 : 0);
    }

    private void RestoreOnePlayerLayout()
    {
        twoPlayerResult = false;
        if (laurelLeft != null) laurelLeft.gameObject.SetActive(true);
        if (laurelRight != null) laurelRight.gameObject.SetActive(true);
        if (laurelKnot != null) laurelKnot.gameObject.SetActive(true);
        if (rankRings != null) rankRings.gameObject.SetActive(true);
        rankText.rectTransform.anchoredPosition = new Vector2(0f, YRankText);
        rankText.rectTransform.localScale = Vector3.one;
        rankText2.gameObject.SetActive(false);
        p1RankTag.gameObject.SetActive(false);
        p2RankTag.gameObject.SetActive(false);
        columnTagP1.gameObject.SetActive(false);
        columnTagP2.gameObject.SetActive(false);
        for (int i = 0; i < RowCount; i++)
        {
            rowValues2[i].gameObject.SetActive(false);
            rowSeparators[i].gameObject.SetActive(true);
            // 1P の値は v11 どおり縦罫の右 (x=914) から左揃え。難しさだけ一回り小さい。
            PlaceValue(rowValues[i], 914f, false, i == RowDifficulty ? 24f : 27f);
            PlaceValue(rowValues2[i], 1078f, true, 27f);
        }
    }

    // =======================================================================
    //  入場
    // =======================================================================

    public void PlayEntrance()
    {
        if (!gameObject.activeSelf || entering) return;
        if (BackdropBlurEnabled && blurImage != null && blurImage.texture != null)
            blurTargetAlpha = 1f;
        StopEntranceRoutine();
        entranceRoutine = StartCoroutine(EntranceRoutine());
    }

    /// <summary>プレイ中の画面をぼかした RenderTexture を受け取る(項目 4)。所有権もこちらへ移る。</summary>
    public void SetBackdropBlur(RenderTexture rt)
    {
        ReleaseBlur();
        blurRT = rt;
        if (blurImage != null)
        {
            blurImage.texture = rt;
            blurImage.color = new Color(1f, 1f, 1f, 0f);
        }
    }

    private void ReleaseBlur()
    {
        if (blurImage != null)
        {
            blurImage.texture = null;
            blurImage.color = new Color(1f, 1f, 1f, 0f);
        }
        BackdropBlurUtil.ReleaseRT(ref blurRT);
        blurTargetAlpha = 0f;
    }

    private void StopEntranceRoutine()
    {
        if (entranceRoutine != null)
        {
            StopCoroutine(entranceRoutine);
            entranceRoutine = null;
        }
        entering = false;
    }

    private void FinishEntranceImmediate()
    {
        StopEntranceRoutine();
        ApplyEntranceFrame(9999f);
        entranceFinished = true;
    }

    private IEnumerator EntranceRoutine()
    {
        entering = true;
        float t = 0f;
        ApplyEntranceFrame(0f);
        // 音ハメ: リザルト BGM を dspTime でスケジュールし、入場の時刻も dsp 基準で進める。
        // ランク着地(t=1.44)がクリップの強アクセントとフレーム精度で一致する。
        bool useDspClock = bgmSource != null && resultBgm != null;
        double startDsp = 0d;
        if (useDspClock)
        {
            bgmSource.volume = ResultBgmVolume;
            bgmSource.time = 0f;
            startDsp = AudioSettings.dspTime + BgmScheduleLead;
            bgmSource.PlayScheduled(startDsp);
        }
        while (t < EnterTotal)
        {
            yield return null;
            if (useDspClock)
            {
                double elapsed = AudioSettings.dspTime - startDsp;
                t = elapsed > 0d ? (float)elapsed : 0f;
            }
            else
            {
                t += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            }
            ApplyEntranceFrame(t);
        }
        ApplyEntranceFrame(9999f);
        entering = false;
        entranceFinished = true;
        entranceRoutine = null;
    }

    // 入場の時刻 t における全要素の状態を決める純関数(大きな t で最終状態)。
    private void ApplyEntranceFrame(float t)
    {
        float plate = EaseOutCubic(t / EnterPlateDur);
        if (scrimImage != null) scrimImage.color = new Color(0.016f, 0.022f, 0.055f, ScrimAlpha * plate);
        if (vignetteImage != null) vignetteImage.color = new Color(1f, 1f, 1f, plate);
        panelGroup.alpha = plate;
        panelRect.localScale = Vector3.one * Mathf.Lerp(1.03f, 1f, plate);
        panelRect.anchoredPosition = new Vector2(0f, 16f * (1f - plate));

        headerGroup.alpha = EaseOutCubic((t - EnterHeadStart) / EnterHeadDur);
        titleGroup.alpha = EaseOutCubic((t - EnterTitleStart) / EnterTitleDur);

        // ランク: 大きく淡い状態から縮んで着地し、着地で一瞬発光する。
        float rp = Mathf.Clamp01((t - EnterRankStart) / EnterRankDur);
        float re = rp * rp * rp;
        rankGroup.alpha = Mathf.Clamp01(rp / 0.30f);
        rankGroupRect.localScale = Vector3.one * Mathf.Lerp(1.26f, 1f, re);
        float postT = t - (EnterRankStart + EnterRankDur);
        float flash = 0f;
        if (postT > 0f && postT < 0.45f)
        {
            float k = 1f - postT / 0.45f;
            flash = k * k;
        }
        if (rankHalo != null)
        {
            Color c = rankHalo.color;
            rankHalo.color = new Color(c.r, c.g, c.b, rankHaloBaseAlpha * (1f + 2.5f * flash));
        }

        // 情報行: 少し右から時差スライドイン。
        for (int i = 0; i < RowCount; i++)
        {
            float p = EaseOutCubic((t - (EnterRowsStart + i * EnterRowStagger)) / EnterRowDur);
            rowGroups[i].alpha = p;
            rowRects[i].anchoredPosition = new Vector2(14f * (1f - p), rowHomeY[i]);
        }

        // 数値カウントアップ。
        float np = Mathf.Clamp01((t - EnterCountStart) / EnterCountDur);
        float ne = 1f - (1f - np) * (1f - np);
        if (twoPlayerResult)
        {
            int leftScore = twoPlayerSidesReversed ? finalScore2 : finalScore;
            int leftHit = twoPlayerSidesReversed ? finalHit2 : finalHit;
            int rightScore = twoPlayerSidesReversed ? finalScore : finalScore2;
            int rightHit = twoPlayerSidesReversed ? finalHit : finalHit2;
            rowValues[RowScore].text = Mathf.RoundToInt(leftScore * ne).ToString("N0");
            rowValues2[RowScore].text = Mathf.RoundToInt(rightScore * ne).ToString("N0");
            rowValues[RowHits].text = Mathf.RoundToInt(leftHit * ne).ToString("00");
            rowValues2[RowHits].text = Mathf.RoundToInt(rightHit * ne).ToString("00");
        }
        else
        {
            rowValues[RowScore].text = Mathf.RoundToInt(finalScore * ne).ToString("N0");
            rowValues[RowHits].text = Mathf.RoundToInt(finalHit * ne).ToString("00");
        }

        float bp = EaseOutCubic((t - EnterButtonStart) / EnterButtonDur);
        buttonGroup.alpha = bp;
        buttonRect.anchoredPosition = buttonHome + new Vector2(0f, -12f * (1f - bp));
        bool interactive = bp >= 0.999f;
        buttonGroup.interactable = interactive;
        buttonGroup.blocksRaycasts = interactive;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // ふりがなの漢字直上合わせは、メッシュ生成後(表示後の初回)でないと空振りする。
        if (!rubiesPlaced && Visible)
        {
            bool all = true;
            foreach (HighlandUi.RubyText r in rubies)
            {
                if (r == null || r.Body == null) continue;
                if (!r.Body.gameObject.activeInHierarchy) continue;
                all &= r.EnsurePlaced();
            }
            rubiesPlaced = all;
        }

        // 背景ぼかしのフェード(項目 4)。
        if (blurImage != null && blurImage.texture != null)
        {
            float a = blurImage.color.a;
            float target = BackdropBlurEnabled ? blurTargetAlpha : 0f;
            if (!Mathf.Approximately(a, target))
            {
                a = Mathf.MoveTowards(a, target, dt / BlurFadeDur);
                blurImage.color = new Color(1f, 1f, 1f, a);
            }
        }

        // ランキングオーバーレイの開閉フェード(項目 3: 1 コマ切替をやめる)。
        if (rankingOverlayCG != null && rankingOverlayRoot != null)
        {
            float a = rankingOverlayCG.alpha;
            if (!Mathf.Approximately(a, rankingOverlayTarget))
            {
                a = Mathf.MoveTowards(a, rankingOverlayTarget, dt / 0.20f);
                rankingOverlayCG.alpha = a;
                if (a <= 0.001f && rankingOverlayTarget <= 0f) rankingOverlayRoot.SetActive(false);
            }
            // ランキングのパネルが出ているあいだは、その下に隠れるはずの要素を
            // まとめてフェードで伏せる。TMP は独自マテリアルのぶん Overlay Canvas の
            // 重なり順が効かず、金の板 1 枚では隠せない(memory: canvas overlay sorting trap)。
            if (entranceFinished)
            {
                float show = 1f - a;
                if (rankGroup != null) rankGroup.alpha = show;
                if (rowsGroup != null) rowsGroup.alpha = show;
                if (buttonGroup != null)
                {
                    buttonGroup.alpha = show;
                    bool usable = a < 0.5f;
                    buttonGroup.interactable = usable;
                    buttonGroup.blocksRaycasts = usable;
                }
            }
        }
    }

    private static float EaseOutCubic(float p)
    {
        p = Mathf.Clamp01(p);
        return 1f - (1f - p) * (1f - p) * (1f - p);
    }

    // =======================================================================
    //  入力
    // =======================================================================

    // 主ボタン「次へ」(= ステージ選択)と、その下の文字リンク 2 つ。
    // 上下でボタン ↔ リンク行、左右でリンク間を移動する。B は従来どおり選択画面への近道。
    public void Tick(bool left, bool right, bool up, bool down, bool upEdge, bool downEdge,
        bool buttonHeld, bool buttonPressed, bool backPressed)
    {
        if (!gameObject.activeSelf) return;

        if (entering)
        {
            if (buttonPressed || backPressed)
            {
                FinishEntranceImmediate();
                inputArmed = false;
                navLeftPrev = left;
                navRightPrev = right;
            }
            return;
        }

        // 入場が終わるまでは何も受け付けない(Prepare から PlayEntrance までの
        // 終了シーケンス中に、ランキング入力が先に開いてしまうのを防ぐ)。
        if (!entranceFinished) return;

        if (rankingQualifies && !rankingSubmitted && rankingFlowState == RankingFlowState.None)
        {
            StartRankingEntryFlow();
        }
        if (rankingFlowState == RankingFlowState.Initials)
        {
            TickInitialsEntry(left, right, up, down, upEdge, downEdge, buttonPressed, backPressed);
            return;
        }
        if (rankingFlowState == RankingFlowState.Board)
        {
            if (backPressed || buttonPressed)
            {
                rankingFlowState = RankingFlowState.None;
                rankingOverlayTarget = 0f;   // 0.2 秒でフェードアウト
                inputArmed = false;
            }
            return;
        }

        if (backPressed)
        {
            RequestAction(Action.StageSelect);
            return;
        }

        if (!inputArmed)
        {
            if (!buttonHeld) inputArmed = true;
            navLeftPrev = left;
            navRightPrev = right;
            return;
        }

        if (downEdge) SetActionSelection(selectedActionIndex == 0 ? 1 : selectedActionIndex);
        else if (upEdge) SetActionSelection(0);
        // 第 U9 便で文字リンクをボタンの左右へ移したので、左右はどこからでも効く。
        if (left && !navLeftPrev) SetActionSelection(1);
        else if (right && !navRightPrev) SetActionSelection(2);
        navLeftPrev = left;
        navRightPrev = right;

        if (buttonPressed) RequestAction(actionValues[selectedActionIndex]);
    }

    private void SetActionSelection(int index)
    {
        selectedActionIndex = Mathf.Clamp(index, 0, actionRects.Length - 1);
        RefreshActionSelection();
    }

    private void RefreshActionSelection()
    {
        for (int i = 0; i < actionRects.Length; i++)
        {
            bool sel = i == selectedActionIndex;
            if (actionRects[i] != null)
                actionRects[i].localScale = Vector3.one * (sel ? 1.02f : 1f);
            if (actionLabels[i] != null)
            {
                Color c = actionLabels[i].color;
                actionLabels[i].color = new Color(c.r, c.g, c.b, sel ? 1f : (i == 0 ? 0.72f : 0.62f));
            }
            if (actionUnderlines[i] != null)
            {
                Color c = actionUnderlines[i].color;
                actionUnderlines[i].color = new Color(c.r, c.g, c.b, sel ? 0.9f : 0f);
            }
        }
        if (nextButtonBody != null)
            nextButtonBody.color = selectedActionIndex == 0 ? Color.white : new Color(0.72f, 0.72f, 0.75f, 1f);
    }

    private void RequestAction(Action action)
    {
        if (!gameObject.activeSelf) return;
        ActionRequested?.Invoke(action);
    }

    // =======================================================================
    //  ランキング登録フロー(SPEC §2.1/2.2)
    // =======================================================================

    private void StartRankingEntryFlow()
    {
        rankingFlowState = RankingFlowState.Initials;
        initialsColumn = 0;
        for (int i = 0; i < initialsCharIndex.Length; i++) initialsCharIndex[i] = 0;
        initialsIdleTimer = 0f;
        initialsUpRepeat = default;
        initialsDownRepeat = default;
        if (rankingOverlayRoot != null) rankingOverlayRoot.SetActive(true);
        rankingOverlayTarget = 1f;
        if (rankingOverlayHeading != null) rankingOverlayHeading.text = "ランキング登録";
        if (initialsGroup != null) initialsGroup.SetActive(true);
        if (boardGroup != null) boardGroup.SetActive(false);
        if (initialsScoreLine != null)
            initialsScoreLine.text = "SCORE  " + pendingRankScore.ToString("N0");
        RefreshInitialsSlots();
    }

    private void RefreshInitialsSlots()
    {
        for (int i = 0; i < initialsSlotTexts.Length; i++)
        {
            TMP_Text slot = initialsSlotTexts[i];
            if (slot == null) continue;
            slot.text = RankingStore.NameCharset[initialsCharIndex[i]].ToString();
            slot.color = i == initialsColumn ? GoldBright : new Color(SilverLabel.r, SilverLabel.g, SilverLabel.b, 0.6f);
        }
    }

    private void TickInitialsEntry(bool left, bool right, bool up, bool down, bool upEdge, bool downEdge,
        bool confirmEdge, bool backEdge)
    {
        float dt = Time.unscaledDeltaTime;
        initialsIdleTimer += dt;
        bool changed = false;

        if (initialsUpRepeat.Tick(upEdge, up, dt, 0.4f, 0.12f))
        {
            initialsCharIndex[initialsColumn] = (initialsCharIndex[initialsColumn] + 1) % RankingStore.NameCharset.Length;
            changed = true;
        }
        if (initialsDownRepeat.Tick(downEdge, down, dt, 0.4f, 0.12f))
        {
            initialsCharIndex[initialsColumn] = (initialsCharIndex[initialsColumn] - 1 + RankingStore.NameCharset.Length) % RankingStore.NameCharset.Length;
            changed = true;
        }
        if (right && initialsColumn < RankingStore.NameLength - 1) { initialsColumn++; changed = true; }
        if (left && initialsColumn > 0) { initialsColumn--; changed = true; }
        if (backEdge && initialsColumn > 0) { initialsColumn--; changed = true; }

        bool anyInput = left || right || upEdge || downEdge || confirmEdge || backEdge;
        if (anyInput) initialsIdleTimer = 0f;

        if (confirmEdge)
        {
            if (initialsColumn < RankingStore.NameLength - 1)
            {
                initialsColumn++;
                changed = true;
            }
            else
            {
                SubmitRankingEntry(BuildInitialsName());
                return;
            }
        }

        if (changed) RefreshInitialsSlots();

        if (initialsIdleTimer >= 10f)
        {
            SubmitRankingEntry("???");
        }
    }

    private string BuildInitialsName()
    {
        char[] chars = new char[RankingStore.NameLength];
        for (int i = 0; i < chars.Length; i++) chars[i] = RankingStore.NameCharset[initialsCharIndex[i]];
        return new string(chars);
    }

    private void SubmitRankingEntry(string name)
    {
        RankingStore.Entry entry = RankingStore.AddEntry(
            name, pendingRankScore, pendingRankStage, pendingRankDifficulty, pendingRankMode, System.DateTime.Now);
        pendingEntryId = entry.entryId;
        rankingSubmitted = true;
        rankingFlowState = RankingFlowState.Board;
        if (initialsGroup != null) initialsGroup.SetActive(false);
        if (boardGroup != null) boardGroup.SetActive(true);
        if (rankingOverlayHeading != null) rankingOverlayHeading.text = "TOP 10 圏内";
        RefreshRankingBoardView();
    }

    private void RefreshRankingBoardView()
    {
        List<RankingStore.Entry> top = RankingStore.GetTop(pendingRankStage, pendingRankDifficulty, pendingRankMode);
        if (boardHeaderText != null)
        {
            string stageName = TransferAchievements.StageDisplayName(pendingRankStage);
            string diffName = DifficultyUtility.GetDisplayName((Difficulty)pendingRankDifficulty);
            boardHeaderText.text = $"{stageName}  {diffName}  {pendingRankMode}";
        }
        int myPlace = 0;
        for (int i = 0; i < boardRowTexts.Length; i++)
        {
            TMP_Text row = boardRowTexts[i];
            if (row == null) continue;
            if (i < top.Count)
            {
                RankingStore.Entry e = top[i];
                bool mine = e.entryId == pendingEntryId;
                if (mine) myPlace = i + 1;
                row.text = $"{i + 1,2}   {e.name,-3}   {e.score,8:N0}";
                row.color = mine ? GoldBright : new Color(InkWhite.r, InkWhite.g, InkWhite.b, 0.85f);
            }
            else
            {
                row.text = $"{i + 1,2}   ---";
                row.color = new Color(1f, 1f, 1f, 0.3f);
            }
        }
        // 情報行の RANKING を実順位へ(未登録は「—」のまま)。
        if (rowValues[RowRanking] != null)
            rowValues[RowRanking].text = myPlace > 0 ? "#" + myPlace : "—";
    }

    // =======================================================================
    //  退出
    // =======================================================================

    public void HideImmediate()
    {
        StopEntranceRoutine();
        if (bgmSource != null) bgmSource.Stop();
        ReleaseBlur();
        gameObject.SetActive(false);
    }

    public async Task FadeOutBgmAsync(float duration)
    {
        if (bgmSource == null || !bgmSource.isPlaying) return;
        float startVol = bgmSource.volume;
        float start = Time.realtimeSinceStartup;
        while (true)
        {
            float el = Time.realtimeSinceStartup - start;
            float k = duration > 0f ? Mathf.Clamp01(el / duration) : 1f;
            if (bgmSource == null) return;
            bgmSource.volume = Mathf.Lerp(startVol, 0f, k);
            if (k >= 1f) break;
            await Task.Yield();
        }
        if (bgmSource != null) bgmSource.volume = 0f;
    }

    // =======================================================================
    //  スコア・ランク(ロジックは従来のまま。EditMode テストが参照する)
    // =======================================================================

    // スコア = クリア +500,000 / カウンター ×20,000 / 被弾 ×10,000（clamp [0,999999]）。
    public static int CalculateProvisionalScore(bool cleared, int hitCount, int counterCount,
        float elapsedSeconds, float endSeconds)
    {
        int score = cleared ? 500000 : 0;
        score += Mathf.Max(0, counterCount) * 20000;
        score -= Mathf.Max(0, hitCount) * 10000;
        return Mathf.Clamp(score, 0, 999999);
    }

    public static string EvaluateRank(bool cleared, int hitCount, int difficulty)
    {
        if (!cleared) return "F";
        if (hitCount <= 0) return "S";
        int aMax, bMax;
        switch (difficulty)
        {
            case 0: aMax = 2; bMax = 5; break;    // EASY
            case 2: aMax = 8; bMax = 15; break;   // LUNATIC
            default: aMax = 5; bMax = 10; break;  // NORMAL（HARD 相当もここへ）
        }
        if (hitCount <= aMax) return "A";
        if (hitCount <= bMax) return "B";
        return "C";
    }

    private static string DifficultyName(int difficulty)
    {
        // 2026-09-19 U7: 表示は難易度選択(v11)と同じ日本語に揃える。内部名は不変。
        switch (Mathf.Clamp(difficulty, 0, 2))
        {
            case 0: return "簡単";
            case 2: return "難しい";
            default: return "普通";
        }
    }

    // =======================================================================
    //  スプライトの焼き込み
    // =======================================================================
    // 焼き込みは表示寸法より大きな解像度で描いて縮小表示する(細線と菱形の縁が潰れない)。
    // 形と寸法の正は参考画像 Instructions/リザルト/ref/result_mockup_gpt_20260916.png。

    // ランキングオーバーレイの地(パネルより一段濃い紺 + 金の細枠)。
    private Sprite CreateOverlaySprite()
    {
        const int W = 128, H = 128, B = 24;
        Color32[] px = new Color32[W * H];
        float cx = (W - 1) * 0.5f, cy = (H - 1) * 0.5f;
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                float ax = Mathf.Abs(x - cx), ay = Mathf.Abs(y - cy);
                float d = Mathf.Max(ax - (W * 0.5f - 1f), ay - (H * 0.5f - 1f));
                float inside = Mathf.Clamp01(0.5f - d);
                Blend(px, W, H, x, y, new Color32(0x08, 0x0D, 0x1E, 0xFA), inside);
                Blend(px, W, H, x, y, TexGoldLine, Mathf.Clamp01(1.6f - Mathf.Abs(d + 0.9f)) * inside);
                Blend(px, W, H, x, y, TexGoldDim, Mathf.Clamp01(1.0f - Mathf.Abs(d + 6f)) * 0.6f * inside);
            }
        }
        Texture2D tex = MakeTexture(px, W, H, "ResultOverlayFill");
        Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, W, H), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(B, B, B, B));
        sprite.name = "ResultOverlayFill";
        generatedSprites.Add(sprite);
        return sprite;
    }

    // 主ボタン。角を斜めに切った八角形・暗い石目の板・二重の金の縁。
    private Sprite CreateButtonSprite()
    {
        const int SS = 3;
        int W = (int)ButtonW * SS, H = (int)ButtonH * SS;
        Color32[] px = new Color32[W * H];
        float cx = (W - 1) * 0.5f, cy = (H - 1) * 0.5f;
        float hw = W * 0.5f, hh = H * 0.5f;
        float chamfer = 17f * SS;
        for (int y = 0; y < H; y++)
        {
            float ty = y / (float)(H - 1);
            Color fill = (Color)Color32.Lerp(TexButtonBottom, TexButtonTop, ty);
            for (int x = 0; x < W; x++)
            {
                float ax = Mathf.Abs(x - cx), ay = Mathf.Abs(y - cy);
                float d = GoldPanelStyle.ChamferRect(ax, ay, hw - 1f, hh - 1f, chamfer);
                float inside = Mathf.Clamp01(0.5f - d);
                if (inside <= 0f) continue;

                // 石目: 低周波の粗いむら(±7/255)。
                float n = Mathf.Sin(x * 0.021f + Mathf.Cos(y * 0.017f) * 2.3f)
                        + Mathf.Sin(y * 0.013f + Mathf.Cos(x * 0.009f) * 1.7f) * 0.8f;
                float k = 1f + n * 0.055f;
                Color stone = new Color(fill.r * k, fill.g * k, fill.b * k * 1.02f, fill.a);
                Blend(px, W, H, x, y, stone, inside * fill.a);

                // 外の金線と 6px 内側の細い金線。
                Blend(px, W, H, x, y, TexGoldBright,
                    Mathf.Clamp01(2.3f * SS - Mathf.Abs(d + 1.2f * SS)) * inside);
                Blend(px, W, H, x, y, TexGoldDim,
                    Mathf.Clamp01(1.1f * SS - Mathf.Abs(d + 6.5f * SS)) * 0.8f * inside);
            }
        }
        // 左右の内端に小さな菱形。
        DrawDiamond(px, W, H, cx - hw + 22f * SS, cy, 4.5f * SS, TexGoldBright);
        DrawDiamond(px, W, H, cx + hw - 22f * SS, cy, 4.5f * SS, TexGoldBright);
        return MakeSprite(px, W, H, "ResultNextButton");
    }

    // ボタンの外側へにじむ光(白で焼いて Image.color で金に染める)。
    private Sprite CreateButtonGlowSprite()
    {
        const int SS = 2;
        int W = (int)(ButtonW + 22f) * SS, H = (int)(ButtonH + 22f) * SS;
        Color32[] px = new Color32[W * H];
        float cx = (W - 1) * 0.5f, cy = (H - 1) * 0.5f;
        float hw = ButtonW * 0.5f * SS, hh = ButtonH * 0.5f * SS;
        float chamfer = 17f * SS;
        float falloff = 4.6f * SS;
        for (int y = 0; y < H; y++)
        {
            float ay = Mathf.Abs(y - cy);
            for (int x = 0; x < W; x++)
            {
                float ax = Mathf.Abs(x - cx);
                float d = GoldPanelStyle.ChamferRect(ax, ay, hw, hh, chamfer);
                if (d <= 0f) continue;                       // 板の内側は板が描く
                float a = Mathf.Exp(-d / falloff);
                Blend(px, W, H, x, y, new Color32(0xFF, 0xFF, 0xFF, 0xFF), a * 0.85f);
            }
        }
        return MakeSprite(px, W, H, "ResultButtonGlow");
    }

    // 罫線(両端が細く消える横線)。白で焼いて Image.color で染める。幅方向へ伸ばす。
    private Sprite ruleSprite;

    private Sprite CreateRuleSprite()
    {
        if (ruleSprite != null) return ruleSprite;
        // 高さ 6px。表示は 3px なので、どのテクセル中心を拾っても線が出る比率にする
        // (H=12 で中央 2 行だけ塗ると、4px 表示のサンプル点が全部空行に当たって線が消える)。
        const int W = 1024, H = 6;
        float[] prof = { 0.10f, 0.55f, 1f, 1f, 0.55f, 0.10f };
        Color32[] px = new Color32[W * H];
        Color32 white = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
        for (int x = 0; x < W; x++)
        {
            float u = Mathf.Abs(x / (float)(W - 1) * 2f - 1f);        // 0=中央 1=端
            float a = Mathf.Clamp01(1f - u * u * u * u * u) * 0.98f;  // 端で静かに消える
            for (int y = 0; y < H; y++) Blend(px, W, H, x, y, white, a * prof[y]);
        }
        ruleSprite = MakeSprite(px, W, H, "ResultRule");
        return ruleSprite;
    }

    // 中身の詰まった菱形(白焼き)。
    private Sprite diamondSprite;

    private Sprite CreateDiamondSprite()
    {
        if (diamondSprite != null) return diamondSprite;
        const int S = 256;
        Color32[] px = new Color32[S * S];
        float c = (S - 1) * 0.5f;
        DrawDiamond(px, S, S, c, c, c * 0.96f, new Color32(0xFF, 0xFF, 0xFF, 0xFF));
        diamondSprite = MakeSprite(px, S, S, "ResultDiamond");
        return diamondSprite;
    }

    // 中空の菱形(参考画像の結び・頭飾りはどれも輪郭だけ)。
    private Sprite diamondRingSprite;

    private Sprite CreateDiamondRingSprite()
    {
        if (diamondRingSprite != null) return diamondRingSprite;
        const int S = 256;
        Color32[] px = new Color32[S * S];
        float c = (S - 1) * 0.5f;
        Color32 white = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
        float outer = c * 0.96f * 0.7071f;
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float m = (Mathf.Abs(x - c) + Mathf.Abs(y - c)) * 0.7071f;
                Blend(px, S, S, x, y, white, Mathf.Clamp01(13f - Mathf.Abs(m - outer)));
            }
        }
        diamondRingSprite = MakeSprite(px, S, S, "ResultDiamondRing");
        return diamondRingSprite;
    }

    // 舞台名の下の飾り罫。中央に中空の菱形、左右に矢羽根と細い線。
    private Sprite CreateOrnamentRuleSprite()
    {
        const int W = 1024, H = 85;
        Color32[] px = new Color32[W * H];
        float cx = (W - 1) * 0.5f, cy = (H - 1) * 0.5f;
        float half = 33f;
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                float m = (Mathf.Abs(x - cx) + Mathf.Abs(y - cy)) * 0.7071f;
                Blend(px, W, H, x, y, TexGoldBright, Mathf.Clamp01(4.5f - Mathf.Abs(m - half * 0.7071f)));
            }
        }
        for (int s = -1; s <= 1; s += 2)
        {
            // 細い線(外へ向かって消える)。
            for (int i = 0; i < 380; i++)
            {
                int x = (int)(cx + s * (60f + i));
                float a = Mathf.Clamp01(1f - Mathf.Pow(i / 380f, 3f)) * 0.95f;
                for (int y = 0; y < H; y++)
                {
                    float dy = Mathf.Abs(y - cy);
                    Blend(px, W, H, x, y, TexGoldBright, a * Mathf.Clamp01(3.0f - dy));
                }
            }
            // 矢羽根(外を向いた小さな山形)。
            float ax = cx + s * 92f;
            DrawLine(px, W, H, ax, cy, ax - s * 22f, cy - 15f, 3.6f, TexGoldBright);
            DrawLine(px, W, H, ax, cy, ax - s * 22f, cy + 15f, 3.6f, TexGoldBright);
        }
        return MakeSprite(px, W, H, "ResultOrnamentRule");
    }

    // ランク章の頭飾り(中空の小菱形 + 左右の短い線)。
    private Sprite CreateCrestSprite()
    {
        const int W = 512, H = 140;
        Color32[] px = new Color32[W * H];
        float cx = (W - 1) * 0.5f, cy = (H - 1) * 0.5f;
        float half = 52f;
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                float m = (Mathf.Abs(x - cx) + Mathf.Abs(y - cy)) * 0.7071f;
                Blend(px, W, H, x, y, TexGoldBright, Mathf.Clamp01(5.5f - Mathf.Abs(m - half * 0.7071f)));
            }
        }
        for (int s = -1; s <= 1; s += 2)
        {
            for (int i = 0; i < 130; i++)
            {
                int x = (int)(cx + s * (76f + i));
                float a = Mathf.Clamp01(1f - Mathf.Pow(i / 130f, 2.2f)) * 0.9f;
                for (int y = 0; y < H; y++)
                    Blend(px, W, H, x, y, TexGoldBright, a * Mathf.Clamp01(3.4f - Mathf.Abs(y - cy)));
            }
        }
        return MakeSprite(px, W, H, "ResultCrest");
    }

    // ランク字の背後の同心リング(薄い金の円 3 本)。
    private Sprite CreateRankRingsSprite()
    {
        const int S = 1024;
        Color32[] px = new Color32[S * S];
        float c = (S - 1) * 0.5f;
        // 半径は UI 換算 118 / 146 / 173(表示 400 → 1px = 2.5575 テクセル)。
        float[] radii = { 302f, 373f, 442f };
        float[] alphas = { 0.16f, 0.11f, 0.07f };
        for (int i = 0; i < radii.Length; i++)
        {
            float r = radii[i], a = alphas[i];
            int y0 = Mathf.Max(0, (int)(c - r - 6f)), y1 = Mathf.Min(S - 1, (int)(c + r + 6f));
            for (int y = y0; y <= y1; y++)
            {
                // 下端(情報行へ抜ける側)は静かに消す。
                float fade = Mathf.Clamp01((y - c * 0.30f) / (c * 0.45f));
                if (fade <= 0f) continue;
                for (int x = 0; x < S; x++)
                {
                    float dx = x - c, dy = y - c;
                    float d = Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - r);
                    if (d > 3f) continue;
                    Blend(px, S, S, x, y, TexGoldBright, Mathf.Clamp01(2.2f - d) * a * fade);
                }
            }
        }
        return MakeSprite(px, S, S, "ResultRankRings");
    }

    // 月桂樹の枝(左半分)。右側は localScale.x = -1 で反転して使う。
    // 参考画像の実測: 弧は φ=16°(下・半径 105) → 125°(先・半径 140)、葉は細長(長さ:幅 = 3:1)、
    // 色は根元の琥珀 #C48A3A から先の淡い金 #F6D68A へ。
    private Sprite laurelSprite;

    private Sprite CreateLaurelSprite()
    {
        if (laurelSprite != null) return laurelSprite;
        const int S = 1024;
        float k = S / LaurelBox;                 // UI → 焼き込み画素
        Color32[] px = new Color32[S * S];
        float c = (S - 1) * 0.5f;

        Color32 leafTip = new Color32(0xF0, 0xC6, 0x83, 0xFF);
        Color32 leafBase = new Color32(0xBE, 0x79, 0x33, 0xFF);
        Color32 stemCol = new Color32(0xCC, 0x93, 0x4B, 0xFF);

        // 枝(根元が太く先が細い)。
        float prevX = 0f, prevY = 0f;
        const int segs = 160;
        for (int i = 0; i <= segs; i++)
        {
            float u = i / (float)segs;
            float phi = Mathf.Lerp(16f, 125f, u) * Mathf.Deg2Rad;
            float R = Mathf.Lerp(105f, 140f, u) * k;
            float x = c - R * Mathf.Sin(phi);
            float y = c - R * Mathf.Cos(phi);
            if (i > 0)
                DrawLine(px, S, S, prevX, prevY, x, y, Mathf.Lerp(3.4f, 1.5f, u) * k, stemCol);
            prevX = x; prevY = y;
        }

        // 葉(外・内の対生。先へ向かって小さくする)。
        const int leaves = 9;
        for (int i = 0; i < leaves; i++)
        {
            float u = Mathf.Lerp(0.07f, 0.97f, i / (float)(leaves - 1));
            float phi = Mathf.Lerp(16f, 125f, u) * Mathf.Deg2Rad;
            float R = Mathf.Lerp(105f, 140f, u) * k;
            float bx = c - R * Mathf.Sin(phi);
            float by = c - R * Mathf.Cos(phi);
            float nx = -Mathf.Sin(phi), ny = -Mathf.Cos(phi);   // 外向き法線
            float tx = -Mathf.Cos(phi), ty = Mathf.Sin(phi);    // 先端方向の接線
            float len = Mathf.Lerp(34f, 19f, u) * k;
            Color32 col = Color32.Lerp(leafBase, leafTip, u);
            for (int side = 0; side < 2; side++)
            {
                float sgn = side == 0 ? 1f : -1f;               // 外 / 内
                float ox = bx + nx * 3.5f * k * sgn, oy = by + ny * 3.5f * k * sgn;
                float ang = Mathf.Atan2(ty, tx) + sgn * 40f * Mathf.Deg2Rad;
                Color32 lc = side == 0 ? col
                    : new Color32((byte)(col.r * 0.86f), (byte)(col.g * 0.86f), (byte)(col.b * 0.86f), 0xFF);
                DrawLeaf(px, S, S, ox, oy, ang, len, len / 5.8f, lc);
                // 中肋(葉の芯を 1 本)。
                Color32 vein = new Color32((byte)(lc.r * 0.72f), (byte)(lc.g * 0.68f), (byte)(lc.b * 0.60f), 0xFF);
                DrawLine(px, S, S, ox, oy,
                    ox + Mathf.Cos(ang) * len * 0.86f, oy + Mathf.Sin(ang) * len * 0.86f, 1.1f * k, vein);
            }
        }
        // 先端の 1 枚(枝の延長上)。
        {
            float phi = 125f * Mathf.Deg2Rad;
            float R = 140f * k;
            float bx = c - R * Mathf.Sin(phi), by = c - R * Mathf.Cos(phi);
            float ang = Mathf.Atan2(Mathf.Sin(phi), -Mathf.Cos(phi)) + 8f * Mathf.Deg2Rad;
            DrawLeaf(px, S, S, bx, by, ang, 22f * k, 22f * k / 5.4f, leafTip);
        }

        laurelSprite = MakeSprite(px, S, S, "ResultLaurel");
        return laurelSprite;
    }

    // 情報行の細線アイコン(照準 / 盾 / 剣 / 王冠)。線幅をそろえて白で焼く。
    // 情報行のアイコン(Highland UI v11: 星 / 照準 / 交差した剣 / トロフィー)。
    // SVG は ±16 の枠に線幅 1.55 で描かれているので、その比率のまま焼く。
    private Sprite[] CreateRowIconSprites()
    {
        const int S = 128;
        float k = S / 34f;                        // SVG の ±17 を S へ
        float c = (S - 1) * 0.5f;
        float T = 1.55f * k;
        System.Func<float, float> PX = v => c + v * k;
        System.Func<float, float> PY = v => c - v * k;
        Sprite[] result = new Sprite[4];

        // 0: 星(スコア)
        {
            Color32[] px = new Color32[S * S];
            float[] sx = { 0.000f, 3.468f, 12.364f, 5.611f, 7.641f, 0.000f, -7.641f, -5.611f, -12.364f, -3.468f };
            float[] sy = { -13.000f, -4.773f, -4.017f, 1.823f, 10.517f, 5.900f, 10.517f, 1.823f, -4.017f, -4.773f };
            for (int i = 0; i < sx.Length; i++)
            {
                int j = (i + 1) % sx.Length;
                HighlandUi.Line(px, S, S, PX(sx[i]), PY(sy[i]), PX(sx[j]), PY(sy[j]), T);
            }
            result[0] = MakeSprite(px, S, S, "ResultIconScoreV11");
        }
        // 1: 照準(当たった回数)
        {
            Color32[] px = new Color32[S * S];
            HighlandUi.Circle(px, S, S, c, c, 10.9f * k, 1.45f * k);
            HighlandUi.Circle(px, S, S, c, c, 5.2f * k, 1.25f * k);
            HighlandUi.Line(px, S, S, PX(0f), PY(-15f), PX(0f), PY(-7.5f), T);
            HighlandUi.Line(px, S, S, PX(0f), PY(7.5f), PX(0f), PY(15f), T);
            HighlandUi.Line(px, S, S, PX(-15f), PY(0f), PX(-7.5f), PY(0f), T);
            HighlandUi.Line(px, S, S, PX(7.5f), PY(0f), PX(15f), PY(0f), T);
            HighlandUi.Circle(px, S, S, c, c, 1.3f * k, 0f);
            result[1] = MakeSprite(px, S, S, "ResultIconHitsV11");
        }
        // 2: 交差した剣(難しさ)
        {
            Color32[] px = new Color32[S * S];
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                float rot = sgn * 43f * Mathf.Deg2Rad;
                System.Func<float, float, Vector2> R = (vx, vy) =>
                {
                    float rx = vx * Mathf.Cos(rot) - vy * Mathf.Sin(rot);
                    float ry = vx * Mathf.Sin(rot) + vy * Mathf.Cos(rot);
                    return new Vector2(PX(rx), PY(ry));
                };
                // 刀身の輪郭(塗りの代わりに輪郭 + 中心線で近似)。
                float[] bx = { 0f, 3f, 2f, -2f, -3f };
                float[] by = { -15f, -9f, 5f, 5f, -9f };
                for (int i = 0; i < bx.Length; i++)
                {
                    int j = (i + 1) % bx.Length;
                    Vector2 p0 = R(bx[i], by[i]), p1 = R(bx[j], by[j]);
                    HighlandUi.Line(px, S, S, p0.x, p0.y, p1.x, p1.y, 1.9f * k);
                }
                Vector2 g0 = R(-5.4f, 5.2f), g1 = R(5.4f, 5.2f);
                HighlandUi.Line(px, S, S, g0.x, g0.y, g1.x, g1.y, 1.8f * k);
                Vector2 h0 = R(0f, 5.6f), h1 = R(0f, 12.5f);
                HighlandUi.Line(px, S, S, h0.x, h0.y, h1.x, h1.y, 1.8f * k);
                Vector2 e0 = R(-2f, 12.5f), e1 = R(2f, 12.5f);
                HighlandUi.Line(px, S, S, e0.x, e0.y, e1.x, e1.y, 1.8f * k);
            }
            result[2] = MakeSprite(px, S, S, "ResultIconDifficultyV11");
        }
        // 3: トロフィー(ランキング)
        {
            Color32[] px = new Color32[S * S];
            HighlandUi.Line(px, S, S, PX(-8f), PY(-12f), PX(8f), PY(-12f), T);
            HighlandUi.Line(px, S, S, PX(-8f), PY(-12f), PX(-8f), PY(-5f), T);
            HighlandUi.Line(px, S, S, PX(8f), PY(-12f), PX(8f), PY(-5f), T);
            // 椀の底(-8,-5 → 0,5 → 8,-5 を 2 本の弧で)。
            for (int i = 0; i < 12; i++)
            {
                float u0 = i / 12f, u1 = (i + 1) / 12f;
                System.Func<float, Vector2> B = u => new Vector2(
                    PX(Mathf.Lerp(-8f, 0f, u)), PY(Mathf.Lerp(-5f, 5f, u * u * 0.55f + u * 0.45f)));
                Vector2 p0 = B(u0), p1 = B(u1);
                HighlandUi.Line(px, S, S, p0.x, p0.y, p1.x, p1.y, T);
                HighlandUi.Line(px, S, S, 2f * c - p0.x, p0.y, 2f * c - p1.x, p1.y, T);
            }
            // 取っ手。
            HighlandUi.Line(px, S, S, PX(-8f), PY(-9f), PX(-13f), PY(-9f), T);
            HighlandUi.Line(px, S, S, PX(-13f), PY(-9f), PX(-13f), PY(-5f), T);
            HighlandUi.Line(px, S, S, PX(-13f), PY(-5f), PX(-6f), PY(1f), T);
            HighlandUi.Line(px, S, S, PX(8f), PY(-9f), PX(13f), PY(-9f), T);
            HighlandUi.Line(px, S, S, PX(13f), PY(-9f), PX(13f), PY(-5f), T);
            HighlandUi.Line(px, S, S, PX(13f), PY(-5f), PX(6f), PY(1f), T);
            // 脚と台座。
            HighlandUi.Line(px, S, S, PX(0f), PY(5f), PX(0f), PY(11f), T);
            HighlandUi.Line(px, S, S, PX(-5f), PY(11f), PX(5f), PY(11f), T);
            HighlandUi.Line(px, S, S, PX(5f), PY(11f), PX(8f), PY(14f), T);
            HighlandUi.Line(px, S, S, PX(8f), PY(14f), PX(-8f), PY(14f), T);
            HighlandUi.Line(px, S, S, PX(-8f), PY(14f), PX(-5f), PY(11f), T);
            result[3] = MakeSprite(px, S, S, "ResultIconRankingV11");
        }
        return result;
    }

    // 画面全体の周辺減光(中央 0 → 端 0.6)。深い紺で沈ませる。
    private Sprite CreateVignetteSprite()
    {
        const int S = 256;
        Color32[] px = new Color32[S * S];
        for (int y = 0; y < S; y++)
        {
            float vy = (y / (float)(S - 1) - 0.5f) * 2f;
            for (int x = 0; x < S; x++)
            {
                float vx = (x / (float)(S - 1) - 0.5f) * 2f;
                float r = Mathf.Sqrt(vx * vx + vy * vy);
                float t = Mathf.Clamp01((r - 0.28f) / 1.02f);
                float a = t * t * 0.52f;
                Blend(px, S, S, x, y, new Color32(0x04, 0x06, 0x12, 0xFF), a);
            }
        }
        return MakeSprite(px, S, S, "ResultVignette");
    }

    // =======================================================================
    //  描画プリミティブ(実体は GoldPanelStyle と共用。ここは薄い転送)
    // =======================================================================

    private static void Blend(Color32[] buf, int w, int h, int x, int y, Color c, float cov)
        => GoldPanelStyle.Blend(buf, w, h, x, y, c, cov);

    private static void Blend(Color32[] buf, int w, int h, int x, int y, Color32 c, float cov)
        => GoldPanelStyle.Blend(buf, w, h, x, y, c, cov);

    private static void DrawLine(Color32[] buf, int w, int h,
        float x0, float y0, float x1, float y1, float thick, Color32 col)
        => GoldPanelStyle.DrawLine(buf, w, h, x0, y0, x1, y1, thick, col);

    private static void DrawDiamond(Color32[] buf, int w, int h, float cx, float cy, float half, Color32 col)
        => GoldPanelStyle.DrawDiamond(buf, w, h, cx, cy, half, col);

    // 先の尖った葉(長軸 len・半幅 wid・角度 ang)。根元 (cx,cy) から ang 方向へ伸びる。
    private static void DrawLeaf(Color32[] buf, int w, int h, float cx, float cy,
        float ang, float len, float wid, Color32 col)
    {
        float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
        float ex = cx + ca * len, ey = cy + sa * len;
        float mx = (cx + ex) * 0.5f, my = (cy + ey) * 0.5f;
        int r = Mathf.CeilToInt(len * 0.5f + wid) + 2;
        for (int y = (int)my - r; y <= (int)my + r; y++)
        {
            for (int x = (int)mx - r; x <= (int)mx + r; x++)
            {
                float dx = x - mx, dy = y - my;
                float u = (dx * ca + dy * sa) / (len * 0.5f);   // -1..1
                float v = -dx * sa + dy * ca;
                if (u < -1f || u > 1f) continue;
                float halfW = wid * (1f - u * u);               // 両端が尖る
                float d = Mathf.Abs(v) - halfW;
                Blend(buf, w, h, x, y, col, Mathf.Clamp01(0.5f - d));
            }
        }
    }

    private Texture2D MakeTexture(Color32[] px, int w, int h, string name)
        => GoldPanelStyle.MakeTexture(px, w, h, name, generatedTextures);

    private Sprite MakeSprite(Color32[] px, int w, int h, string name)
        => GoldPanelStyle.MakeSprite(px, w, h, name, generatedTextures, generatedSprites);


    // =======================================================================
    //  UI 生成ヘルパー
    // =======================================================================

    // 罫線 1 本(中央に菱形を置くかどうか)。罫は白で焼いてあるので色は Image.color で決める。
    private void AddRule(RectTransform parent, Vector2 pos, float width, bool withDiamond,
        Color? tint = null, float alpha = 0.85f)
    {
        Color c = tint ?? GoldDim;
        Image rule = NewImage("Rule", parent, new Color(c.r, c.g, c.b, alpha));
        rule.sprite = CreateRuleSprite();
        rule.type = Image.Type.Simple;
        SetRect(rule.rectTransform, pos, new Vector2(width, 3f));
        if (withDiamond) AddDiamond(parent, pos, 11f, GoldAccent);
    }

    private void AddDiamond(RectTransform parent, Vector2 pos, float size, Color color)
    {
        Image d = NewImage("Diamond", parent, color);
        d.sprite = CreateDiamondSprite();
        d.type = Image.Type.Simple;
        SetRect(d.rectTransform, pos, new Vector2(size, size));
    }

    // TMP のアンダーレイによる柔らかい発光(memory: ScaleRatioC=1 + UpdateMeshPadding が必須)。
    private static Material ApplyTextGlow(TMP_Text text, Color glow, float dilate, float softness)
    {
        if (text == null) return null;
        Material mat = text.fontMaterial;
        mat.EnableKeyword("UNDERLAY_ON");
        mat.SetFloat(ShaderUtilities.ID_ScaleRatio_C, 1f);
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, 0f);
        mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, dilate);
        mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, softness);
        mat.SetColor(ShaderUtilities.ID_UnderlayColor, glow);
        text.UpdateMeshPadding();
        return mat;
    }

    private TMP_Text NewText(string name, Transform parent, string value, float size,
        Color color, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        TMP_FontAsset use = mincho != null ? mincho : font;
        if (use != null) text.font = use;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }

    private static Image NewImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static T NewGraphic<T>(string name, Transform parent) where T : Graphic
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
        go.transform.SetParent(parent, false);
        T graphic = go.GetComponent<T>();
        graphic.raycastTarget = false;
        return graphic;
    }

    private static GameObject NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void AddTrigger(EventTrigger trigger, EventTriggerType type,
        UnityEngine.Events.UnityAction<BaseEventData> callback)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(callback);
        trigger.triggers.Add(entry);
    }

#if UNITY_EDITOR
    // MCP 検証用。EditorApplication.update では Game View が白/黒になるため、
    // Play 内で描画完了を数フレーム待ってから保存する。
    public void CaptureDebugScreenshot(string fileName)
    {
        StartCoroutine(CaptureDebugScreenshotRoutine(fileName));
    }

    private IEnumerator CaptureDebugScreenshotRoutine(string fileName)
    {
        for (int i = 0; i < 5; i++) yield return new WaitForEndOfFrame();
        string directory = Path.Combine(Application.dataPath, "Screenshots");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, fileName);
        ScreenCapture.CaptureScreenshot(path);
        Debug.Log($"[ResultScreen] Screenshot requested: {path}");
    }
#endif

    private void OnDestroy()
    {
        ReleaseBlur();
        foreach (Sprite sprite in generatedSprites)
        {
            if (sprite != null) Destroy(sprite);
        }
        foreach (Texture2D texture in generatedTextures)
        {
            if (texture != null) Destroy(texture);
        }
    }
}

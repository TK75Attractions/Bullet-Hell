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
    private static readonly Color32 TexButtonTop = new Color32(0x14, 0x1B, 0x2E, 0xF2);
    private static readonly Color32 TexButtonBottom = new Color32(0x0A, 0x10, 0x20, 0xF2);

    /// <summary>視覚 sRGB(0..255) → 頂点色用の pre-linear。</summary>
    private static Color Vis(int r, int g, int b, float a = 1f)
    {
        Color c = new Color(r / 255f, g / 255f, b / 255f, 1f).linear;
        c.a = a;
        return c;
    }

    private static readonly Color GoldAccent = Vis(0xE8, 0xB0, 0x64);   // 金(琥珀)のアクセント
    private static readonly Color GoldBright = Vis(0xF7, 0xD2, 0x95);   // 金のハイライト
    private static readonly Color GoldDim = Vis(0xB0, 0x8B, 0x4A);      // 罫線・アイコンの金
    private static readonly Color SilverLabel = Vis(0xB4, 0xBC, 0xC9);  // RESULT などの小見出し
    private static readonly Color InkWhite = Vis(0xF2, 0xF4, 0xF8);     // 舞台名の白
    private static readonly Color FailRed = Vis(0xE0, 0x6A, 0x74);      // STAGE FAILED

    // ---- 寸法(1080 基準) --------------------------------------------------
    private const float PanelW = 640f;
    private const float PanelH = 900f;
    private const float PanelTop = PanelH * 0.5f;
    private const float ScrimAlpha = 0.34f;

    private const float YResult = 390f;
    private const float YTopDiamond = 356f;
    private const float YVerdict = 300f;
    private const float YRule1 = 242f;
    private const float YStageTitle = 186f;
    private const float YRule2 = 130f;
    private const float YRankLabel = 112f;
    private const float YRank = -14f;
    private const float YRule3 = -120f;
    private const float YRow0 = -160f;
    private const float RowPitch = 48f;
    private const float YButton = -384f;
    private const float YLinks = -472f;
    private const float YTransfer = -502f;

    private const float RowHalfW = 236f;   // 情報行の左右端
    private const float RowValueRight = 236f;
    private const float RowSepX = 44f;     // 1P の縦罫の x

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
    private TMP_Text rankText;
    private TMP_Text rankText2;
    private TMP_Text p1RankTag;
    private TMP_Text p2RankTag;
    private Material rankGlowMat;
    private Material rankGlowMat2;
    private Image laurelLeft;
    private Image laurelRight;
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
    private readonly TMP_Text[] rowLabels = new TMP_Text[RowCount];
    private readonly TMP_Text[] rowValues = new TMP_Text[RowCount];
    private readonly TMP_Text[] rowValues2 = new TMP_Text[RowCount];
    private readonly TMP_Text[] rowSeparators = new TMP_Text[RowCount];
    private readonly Image[] rowIcons = new Image[RowCount];
    private TMP_Text columnTagP1;
    private TMP_Text columnTagP2;

    private TMP_Text nextLabel;
    private readonly RectTransform[] actionRects = new RectTransform[3];
    private readonly TMP_Text[] actionLabels = new TMP_Text[3];
    private readonly Image[] actionUnderlines = new Image[3];
    private readonly Action[] actionValues = new Action[3];
    private Image nextButtonBody;
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
        // 和文も欧文も明朝(ShipporiMincho)で通す。Oxanium 系はリザルトでは使わない。
        mincho = Resources.Load<TMP_FontAsset>("Fonts/ShipporiMincho-Regular SDF");

        // --- 背景のぼかし板(項目 4)と薄い暗幕 ---
        GameObject blurGo = new GameObject("BackdropBlur", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        blurGo.transform.SetParent(root, false);
        blurImage = blurGo.GetComponent<RawImage>();
        blurImage.raycastTarget = false;
        blurImage.color = new Color(1f, 1f, 1f, 0f);
        Stretch(blurImage.rectTransform);

        scrimImage = NewImage("Scrim", root, new Color(0f, 0.004f, 0.012f, 0f));
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

        Image plate = NewImage("Plate", panelRect, Color.white);
        plate.sprite = CreatePanelSprite();
        plate.type = Image.Type.Simple;
        Stretch(plate.rectTransform);

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

        TMP_Text resultLabel = NewText("ResultLabel", head, "RESULT", 26f, SilverLabel, TextAlignmentOptions.Center);
        resultLabel.characterSpacing = 22f;
        SetRect((RectTransform)resultLabel.transform, new Vector2(0f, YResult), new Vector2(420f, 36f));

        AddDiamond(head, new Vector2(0f, YTopDiamond), 13f, GoldAccent);

        verdictText = NewText("Verdict", head, "STAGE CLEAR", 64f, GoldAccent, TextAlignmentOptions.Center);
        verdictText.characterSpacing = 4f;
        SetRect((RectTransform)verdictText.transform, new Vector2(0f, YVerdict), new Vector2(600f, 110f));
        ApplyTextGlow(verdictText, new Color(0.85f, 0.55f, 0.18f, 0.5f), 0.055f, 0.5f);

        AddRule(head, new Vector2(0f, YRule1), 470f, false);

        GameObject titleGo = NewRect("TitleGroup", panel);
        RectTransform titleRect = (RectTransform)titleGo.transform;
        SetRect(titleRect, Vector2.zero, new Vector2(PanelW, PanelH));
        titleGroup = titleGo.AddComponent<CanvasGroup>();

        stageTitleText = NewText("StageTitle", titleRect, "", 58f, InkWhite, TextAlignmentOptions.Center);
        stageTitleText.characterSpacing = 6f;
        SetRect((RectTransform)stageTitleText.transform, new Vector2(0f, YStageTitle), new Vector2(560f, 86f));

        AddRule(titleRect, new Vector2(0f, YRule2), 470f, true);
    }

    // 中段: 月桂樹に囲まれた大きな金のランク字
    private void BuildRank(RectTransform panel)
    {
        GameObject go = NewRect("RankGroup", panel);
        rankGroupRect = (RectTransform)go.transform;
        SetRect(rankGroupRect, Vector2.zero, new Vector2(PanelW, PanelH));
        rankGroup = go.AddComponent<CanvasGroup>();

        rankHalo = NewGraphic<SoftCircleGraphic>("RankHalo", rankGroupRect);
        rankHalo.color = new Color(GoldAccent.r, GoldAccent.g, GoldAccent.b, 0.038f);
        SetRect(rankHalo.rectTransform, new Vector2(0f, YRank), new Vector2(360f, 360f));

        Sprite laurel = CreateLaurelSprite();
        laurelLeft = NewImage("LaurelL", rankGroupRect, new Color(GoldAccent.r, GoldAccent.g, GoldAccent.b, 0.95f));
        laurelLeft.sprite = laurel;
        SetRect(laurelLeft.rectTransform, new Vector2(0f, YRank), new Vector2(306f, 306f));
        laurelRight = NewImage("LaurelR", rankGroupRect, new Color(GoldAccent.r, GoldAccent.g, GoldAccent.b, 0.95f));
        laurelRight.sprite = laurel;
        SetRect(laurelRight.rectTransform, new Vector2(0f, YRank), new Vector2(306f, 306f));
        laurelRight.rectTransform.localScale = new Vector3(-1f, 1f, 1f);

        TMP_Text rankLabel = NewText("RankLabel", rankGroupRect, "RANK", 24f, new Color(GoldAccent.r, GoldAccent.g, GoldAccent.b, 0.92f), TextAlignmentOptions.Center);
        rankLabel.characterSpacing = 20f;
        SetRect((RectTransform)rankLabel.transform, new Vector2(0f, YRankLabel), new Vector2(300f, 34f));

        rankText = NewText("Rank", rankGroupRect, "A", 176f, GoldBright, TextAlignmentOptions.Center);
        SetRect((RectTransform)rankText.transform, new Vector2(0f, YRank), new Vector2(460f, 260f));
        rankGlowMat = ApplyTextGlow(rankText, new Color(0.92f, 0.62f, 0.22f, 0.55f), 0.075f, 0.45f);

        rankText2 = NewText("Rank2", rankGroupRect, "A", 176f, GoldBright, TextAlignmentOptions.Center);
        SetRect((RectTransform)rankText2.transform, new Vector2(0f, YRank), new Vector2(460f, 260f));
        rankGlowMat2 = ApplyTextGlow(rankText2, new Color(0.92f, 0.62f, 0.22f, 0.55f), 0.075f, 0.45f);
        rankText2.gameObject.SetActive(false);

        p1RankTag = NewText("P1Tag", rankGroupRect, "1P", 26f, GoldDim, TextAlignmentOptions.Center);
        p1RankTag.characterSpacing = 8f;
        SetRect((RectTransform)p1RankTag.transform, new Vector2(-126f, YRankLabel), new Vector2(120f, 34f));
        p1RankTag.gameObject.SetActive(false);

        p2RankTag = NewText("P2Tag", rankGroupRect, "2P", 26f, GoldDim, TextAlignmentOptions.Center);
        p2RankTag.characterSpacing = 8f;
        SetRect((RectTransform)p2RankTag.transform, new Vector2(126f, YRankLabel), new Vector2(120f, 34f));
        p2RankTag.gameObject.SetActive(false);

        AddRule(rankGroupRect, new Vector2(0f, YRule3), 470f, false);
    }

    private static readonly string[] RowLabelText = { "SCORE", "HITS", "DIFFICULTY", "RANKING" };
    private static readonly string[] RowIconName = { "result_icon_score", "result_icon_hit", "result_icon_counter", null };

    private void BuildRows(RectTransform panelParent)
    {
        // 情報行はまとめて 1 つの CanvasGroup に入れる(ランキングのパネルが出ている
        // あいだ、まとめて隠すため。TMP は独自マテリアルなので Image では隠せない)。
        GameObject host = NewRect("Rows", panelParent);
        RectTransform panel = (RectTransform)host.transform;
        SetRect(panel, Vector2.zero, new Vector2(PanelW, PanelH));
        rowsGroup = host.AddComponent<CanvasGroup>();

        Sprite podium = CreatePodiumIconSprite();
        for (int i = 0; i < RowCount; i++)
        {
            GameObject rowGo = NewRect("Row" + i, panel);
            RectTransform rect = (RectTransform)rowGo.transform;
            Vector2 home = new Vector2(0f, YRow0 - i * RowPitch);
            SetRect(rect, home, new Vector2(PanelW, RowPitch));
            rowRects[i] = rect;
            rowGroups[i] = rowGo.AddComponent<CanvasGroup>();

            Sprite iconSprite = RowIconName[i] != null
                ? Resources.Load<Sprite>("UI/" + RowIconName[i])
                : podium;
            if (iconSprite != null)
            {
                Image icon = NewImage("Icon", rect, new Color(SilverLabel.r, SilverLabel.g, SilverLabel.b, 0.85f));
                icon.sprite = iconSprite;
                icon.type = Image.Type.Simple;
                SetRect(icon.rectTransform, new Vector2(-RowHalfW + 16f, 0f), new Vector2(28f, 28f));
                rowIcons[i] = icon;
            }

            TMP_Text label = NewText("Label", rect, RowLabelText[i], 24f, SilverLabel, TextAlignmentOptions.Left);
            label.characterSpacing = 12f;
            SetRect((RectTransform)label.transform, new Vector2(-RowHalfW + 44f + 130f, 0f), new Vector2(260f, 34f));
            rowLabels[i] = label;

            TMP_Text sep = NewText("Sep", rect, "|", 26f, new Color(GoldDim.r, GoldDim.g, GoldDim.b, 0.8f),
                TextAlignmentOptions.Center);
            SetRect((RectTransform)sep.transform, new Vector2(RowSepX, 0f), new Vector2(30f, 34f));
            rowSeparators[i] = sep;

            TMP_Text value = NewText("Value", rect, "", 28f, GoldAccent, TextAlignmentOptions.Right);
            value.characterSpacing = 4f;
            SetRect((RectTransform)value.transform, new Vector2(RowValueRight - 150f, 0f), new Vector2(300f, 38f));
            rowValues[i] = value;

            TMP_Text value2 = NewText("Value2", rect, "", 28f, GoldAccent, TextAlignmentOptions.Right);
            value2.characterSpacing = 4f;
            SetRect((RectTransform)value2.transform, new Vector2(RowValueRight - 150f, 0f), new Vector2(300f, 38f));
            value2.gameObject.SetActive(false);
            rowValues2[i] = value2;
        }

        columnTagP1 = NewText("ColTagP1", panel, "1P", 18f, GoldDim, TextAlignmentOptions.Right);
        columnTagP1.characterSpacing = 8f;
        SetRect((RectTransform)columnTagP1.transform, new Vector2(96f - 90f, YRow0 + 34f), new Vector2(180f, 26f));
        columnTagP1.gameObject.SetActive(false);

        columnTagP2 = NewText("ColTagP2", panel, "2P", 18f, GoldDim, TextAlignmentOptions.Right);
        columnTagP2.characterSpacing = 8f;
        SetRect((RectTransform)columnTagP2.transform, new Vector2(RowValueRight - 90f, YRow0 + 34f), new Vector2(180f, 26f));
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
        SetRect(btnRect, new Vector2(0f, YButton), new Vector2(470f, 74f));
        actionRects[0] = btnRect;
        actionValues[0] = Action.StageSelect;

        nextButtonBody = NewImage("Body", btnRect, Color.white);
        nextButtonBody.sprite = CreateButtonSprite();
        nextButtonBody.type = Image.Type.Simple;
        Stretch(nextButtonBody.rectTransform);
        nextButtonBody.raycastTarget = true;

        nextLabel = NewText("Label", btnRect, "次へ", 32f, InkWhite, TextAlignmentOptions.Center);
        nextLabel.characterSpacing = 8f;
        Stretch((RectTransform)nextLabel.transform);
        TmpAlign.CenterInkVertically(nextLabel);
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

        // --- 文字リンク ---
        BuildLink(1, new Vector2(-92f, YLinks), "もう一度", Action.Retry);
        BuildLink(2, new Vector2(92f, YLinks), "タイトルへ", Action.Title);

        transferCodeLine = NewText("TransferCodeLine", buttonRect, "", 18f,
            new Color(GoldDim.r, GoldDim.g, GoldDim.b, 0.95f), TextAlignmentOptions.Center);
        SetRect((RectTransform)transferCodeLine.transform, new Vector2(0f, YTransfer), new Vector2(900f, 26f));
        transferCodeLine.gameObject.SetActive(false);

        selectedActionIndex = 0;
        RefreshActionSelection();
    }

    private void BuildLink(int slot, Vector2 pos, string labelText, Action action)
    {
        GameObject go = NewRect("Link" + slot, buttonRect);
        RectTransform rect = (RectTransform)go.transform;
        SetRect(rect, pos, new Vector2(170f, 38f));
        actionRects[slot] = rect;
        actionValues[slot] = action;

        TMP_Text label = NewText("Label", rect, labelText, 22f,
            new Color(SilverLabel.r, SilverLabel.g, SilverLabel.b, 0.8f), TextAlignmentOptions.Center);
        label.characterSpacing = 6f;
        Stretch((RectTransform)label.transform);
        TmpAlign.CenterInkVertically(label);
        actionLabels[slot] = label;

        Image underline = NewImage("Underline", rect, new Color(GoldAccent.r, GoldAccent.g, GoldAccent.b, 0f));
        SetRect(underline.rectTransform, new Vector2(0f, -18f), new Vector2(110f, 1.4f));
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
        const float overlayH = 600f;
        const float overlayY = -(PanelH * 0.5f) + overlayH * 0.5f + 10f;

        GameObject go = NewRect("RankingOverlay", panel);
        rankingOverlayRoot = go;
        RectTransform rect = (RectTransform)go.transform;
        SetRect(rect, new Vector2(0f, overlayY), new Vector2(PanelW - 24f, overlayH));
        rankingOverlayCG = go.AddComponent<CanvasGroup>();

        Image fill = NewImage("Fill", rect, Color.white);
        fill.sprite = CreateOverlaySprite();
        fill.type = Image.Type.Simple;
        Stretch(fill.rectTransform);

        rankingOverlayHeading = NewText("Heading", rect, "", 28f, GoldAccent, TextAlignmentOptions.Center);
        rankingOverlayHeading.characterSpacing = 6f;
        SetRect((RectTransform)rankingOverlayHeading.transform, new Vector2(0f, overlayH * 0.5f - 48f), new Vector2(520f, 40f));
        AddRule(rect, new Vector2(0f, overlayH * 0.5f - 78f), 420f, true);

        // --- イニシャル入力(3 文字) ---
        initialsGroup = NewRect("Initials", rect);
        SetRect((RectTransform)initialsGroup.transform, Vector2.zero, new Vector2(PanelW - 24f, overlayH));
        const float slotW = 82f;
        const float slotGap = 26f;
        float startX = -(slotW + slotGap) * (RankingStore.NameLength - 1) * 0.5f;
        for (int i = 0; i < RankingStore.NameLength; i++)
        {
            TMP_Text slot = NewText("Slot" + i, initialsGroup.transform, "A", 62f, GoldBright, TextAlignmentOptions.Center);
            SetRect((RectTransform)slot.transform, new Vector2(startX + i * (slotW + slotGap), 40f), new Vector2(slotW, 88f));
            initialsSlotTexts[i] = slot;
            Image bar = NewImage("Bar" + i, initialsGroup.transform, new Color(GoldDim.r, GoldDim.g, GoldDim.b, 0.6f));
            SetRect(bar.rectTransform, new Vector2(startX + i * (slotW + slotGap), -12f), new Vector2(slotW - 10f, 1.4f));
        }
        TMP_Text hint = NewText("Hint", initialsGroup.transform,
            "↑↓ 文字送り / ←→ 桁移動 / A 決定(3 桁目で登録) / B 戻る", 17f,
            new Color(SilverLabel.r, SilverLabel.g, SilverLabel.b, 0.72f), TextAlignmentOptions.Center);
        SetRect((RectTransform)hint.transform, new Vector2(0f, -110f), new Vector2(560f, 28f));

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
        verdictText.text = cleared ? "STAGE CLEAR" : "STAGE FAILED";
        verdictText.color = cleared ? GoldAccent : FailRed;

        // 舞台名(StageCityProfile の舞台名フィールド。無ければステージ名)。
        stageTitleText.text = StageCityProfile.StageTitleOf(stage);

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
            rankHaloBaseAlpha = cleared ? 0.030f : 0.045f;
            rankHalo.color = cleared
                ? new Color(GoldAccent.r, GoldAccent.g, GoldAccent.b, rankHaloBaseAlpha)
                : new Color(0.55f, 0.08f, 0.12f, rankHaloBaseAlpha);
        }
        Color laurelCol = cleared
            ? new Color(GoldAccent.r, GoldAccent.g, GoldAccent.b, 0.95f)
            : new Color(0.55f, 0.20f, 0.22f, 0.9f);
        if (laurelLeft != null) laurelLeft.color = laurelCol;
        if (laurelRight != null) laurelRight.color = laurelCol;

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
        const float rankOffset = 132f;
        // 月桂樹はランク 1 文字を囲む飾りなので、2 つ並ぶ 2P では出さない。
        if (laurelLeft != null) laurelLeft.gameObject.SetActive(false);
        if (laurelRight != null) laurelRight.gameObject.SetActive(false);
        string leftRank = p1OnLeft ? rank1 : rank2;
        string rightRank = p1OnLeft ? rank2 : rank1;

        rankText.text = leftRank;
        rankText.rectTransform.anchoredPosition = new Vector2(-rankOffset, YRank);
        rankText.rectTransform.localScale = Vector3.one * rankScale;
        rankText2.gameObject.SetActive(true);
        rankText2.text = rightRank;
        rankText2.rectTransform.anchoredPosition = new Vector2(rankOffset, YRank);
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
                SetRowValueRect(rowValues[i], 96f);
                SetRowValueRect(rowValues2[i], RowValueRight);
                rowValues[i].fontSize = 26f;
                rowValues2[i].fontSize = 26f;
            }
        }
        columnTagP1.gameObject.SetActive(true);
        columnTagP2.gameObject.SetActive(true);
        columnTagP1.text = p1OnLeft ? "1P" : "2P";
        columnTagP2.text = p1OnLeft ? "2P" : "1P";
    }

    private static void SetRowValueRect(TMP_Text text, float rightX)
    {
        RectTransform r = (RectTransform)text.transform;
        r.anchoredPosition = new Vector2(rightX - r.sizeDelta.x * 0.5f, 0f);
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
        rankText.rectTransform.anchoredPosition = new Vector2(0f, YRank);
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
            rowValues[i].fontSize = 28f;
            rowValues2[i].fontSize = 28f;
            SetRowValueRect(rowValues[i], RowValueRight);
            SetRowValueRect(rowValues2[i], RowValueRight);
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
        if (scrimImage != null) scrimImage.color = new Color(0f, 0.004f, 0.012f, ScrimAlpha * plate);
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
            rowRects[i].anchoredPosition = new Vector2(14f * (1f - p), YRow0 - i * RowPitch);
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
        if (selectedActionIndex != 0)
        {
            if (left && !navLeftPrev) SetActionSelection(1);
            else if (right && !navRightPrev) SetActionSelection(2);
        }
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
        if (rankingOverlayHeading != null) rankingOverlayHeading.text = "TOP 10 圏内";
        if (initialsGroup != null) initialsGroup.SetActive(true);
        if (boardGroup != null) boardGroup.SetActive(false);
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
        if (rankingOverlayHeading != null) rankingOverlayHeading.text = "ランキング登録";
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
        switch (Mathf.Clamp(difficulty, 0, 2))
        {
            case 0: return "EASY";
            case 2: return "LUNATIC";
            default: return "NORMAL";
        }
    }

    // =======================================================================
    //  スプライトの焼き込み
    // =======================================================================

    // パネル本体。縦グラデの紺 + 外周の金の細枠 + 10px 内側の金線(角は面取り) +
    // 四隅の明るい金のブラケット + 上下中央の菱形。
    private Sprite CreatePanelSprite()
    {
        const int W = (int)PanelW;
        const int H = (int)PanelH;
        Color32[] px = new Color32[W * H];
        float cx = (W - 1) * 0.5f, cy = (H - 1) * 0.5f;
        float hw = W * 0.5f, hh = H * 0.5f;
        const float innerInset = 11f;
        const float chamfer = 20f;

        for (int y = 0; y < H; y++)
        {
            float ty = y / (float)(H - 1);
            Color fill = (Color)Color32.Lerp(TexPanelBottom, TexPanelTop, ty * ty);
            for (int x = 0; x < W; x++)
            {
                float ax = Mathf.Abs(x - cx), ay = Mathf.Abs(y - cy);
                float dOuter = Mathf.Max(ax - (hw - 1f), ay - (hh - 1f));
                float inside = Mathf.Clamp01(0.5f - dOuter);
                Blend(px, W, H, x, y, fill, inside * fill.a);

                // 上端の淡い光(紙のような立ち上がり)。
                float glow = Mathf.Clamp01(1f - Mathf.Abs(ty - 0.86f) * 5.5f);
                Blend(px, W, H, x, y, new Color(0.10f, 0.16f, 0.32f), inside * glow * 0.20f);

                // 外周の金枠(1.6px)。
                Blend(px, W, H, x, y, TexGoldLine, Mathf.Clamp01(1.6f - Mathf.Abs(dOuter + 0.8f)) * inside);

                // 内側の金線(面取り角)。
                float ix = ax - (hw - innerInset);
                float iy = ay - (hh - innerInset);
                float dInner = Mathf.Max(ix, iy);
                dInner = Mathf.Max(dInner, (ix + iy + chamfer) * 0.7071f);
                Blend(px, W, H, x, y, TexGoldDim, Mathf.Clamp01(1.0f - Mathf.Abs(dInner)) * 0.9f);
            }
        }

        // 四隅の明るいブラケット(内側の線に沿って 54px)。
        for (int sx = -1; sx <= 1; sx += 2)
        {
            for (int sy = -1; sy <= 1; sy += 2)
            {
                float bx = cx + sx * (hw - innerInset);
                float by = cy + sy * (hh - innerInset);
                DrawLine(px, W, H, bx - sx * chamfer, by, bx - sx * chamfer - sx * 54f, by, 2.0f, TexGoldBright);
                DrawLine(px, W, H, bx, by - sy * chamfer, bx, by - sy * chamfer - sy * 54f, 2.0f, TexGoldBright);
                DrawLine(px, W, H, bx - sx * chamfer, by, bx, by - sy * chamfer, 2.0f, TexGoldBright);
            }
        }
        // 上下中央の菱形(枠に噛ませる)。
        DrawDiamond(px, W, H, cx, cy + hh - innerInset, 9f, TexGoldBright);
        DrawDiamond(px, W, H, cx, cy - hh + innerInset, 9f, TexGoldBright);

        return MakeSprite(px, W, H, "ResultPanel");
    }

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
                Blend(px, W, H, x, y, new Color32(0x07, 0x0C, 0x1C, 0xFA), inside);
                Blend(px, W, H, x, y, TexGoldDim, Mathf.Clamp01(1.3f - Mathf.Abs(d + 0.8f)) * inside);
            }
        }
        Texture2D tex = MakeTexture(px, W, H, "ResultOverlayFill");
        Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, W, H), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(B, B, B, B));
        sprite.name = "ResultOverlayFill";
        generatedSprites.Add(sprite);
        return sprite;
    }

    // 金縁の暗いボタン(470x74)。
    private Sprite CreateButtonSprite()
    {
        const int W = 470, H = 74;
        Color32[] px = new Color32[W * H];
        float cx = (W - 1) * 0.5f, cy = (H - 1) * 0.5f;
        float hw = W * 0.5f, hh = H * 0.5f;
        const float chamfer = 13f;
        for (int y = 0; y < H; y++)
        {
            float ty = y / (float)(H - 1);
            Color fill = (Color)Color32.Lerp(TexButtonBottom, TexButtonTop, ty);
            for (int x = 0; x < W; x++)
            {
                float ax = Mathf.Abs(x - cx), ay = Mathf.Abs(y - cy);
                float d = Mathf.Max(ax - (hw - 1f), ay - (hh - 1f));
                d = Mathf.Max(d, (ax + ay - (hw + hh - 2f - chamfer)) * 0.7071f);
                float inside = Mathf.Clamp01(0.5f - d);
                Blend(px, W, H, x, y, fill, inside * fill.a);
                Blend(px, W, H, x, y, TexGoldLine, Mathf.Clamp01(1.5f - Mathf.Abs(d + 0.8f)) * inside);
                // 内側の細い金線。
                Blend(px, W, H, x, y, TexGoldDim, Mathf.Clamp01(1.0f - Mathf.Abs(d + 6f)) * 0.75f * inside);
            }
        }
        DrawDiamond(px, W, H, cx - hw + 20f, cy, 5f, TexGoldBright);
        DrawDiamond(px, W, H, cx + hw - 20f, cy, 5f, TexGoldBright);
        return MakeSprite(px, W, H, "ResultNextButton");
    }

    // 罫線(両端がフェードする細い金の横線)。幅方向へ引き伸ばして使う。
    private Sprite ruleSprite;

    private Sprite CreateRuleSprite()
    {
        if (ruleSprite != null) return ruleSprite;
        const int W = 256, H = 4;
        Color32[] px = new Color32[W * H];
        for (int x = 0; x < W; x++)
        {
            float u = Mathf.Abs(x / (float)(W - 1) * 2f - 1f);        // 0=中央 1=端
            float a = Mathf.Clamp01(1f - u * u * u * u) * 0.95f;      // 端で静かに消える
            for (int y = 0; y < H; y++)
            {
                float cov = y == 1 || y == 2 ? 1f : 0.35f;
                Blend(px, W, H, x, y, TexGoldLine, a * cov);
            }
        }
        ruleSprite = MakeSprite(px, W, H, "ResultRule");
        return ruleSprite;
    }

    private Sprite diamondSprite;

    private Sprite CreateDiamondSprite()
    {
        if (diamondSprite != null) return diamondSprite;
        const int S = 64;
        Color32[] px = new Color32[S * S];
        float c = (S - 1) * 0.5f;
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float d = (Mathf.Abs(x - c) + Mathf.Abs(y - c)) * 0.7071f - c * 0.7071f;
                Blend(px, S, S, x, y, TexGoldBright, Mathf.Clamp01(0.5f - d));
            }
        }
        diamondSprite = MakeSprite(px, S, S, "ResultDiamond");
        return diamondSprite;
    }

    // 月桂樹の枝(左半分)。右側は localScale.x = -1 で反転して使う。
    private Sprite laurelSprite;

    private Sprite CreateLaurelSprite()
    {
        if (laurelSprite != null) return laurelSprite;
        const int S = 320;
        Color32[] px = new Color32[S * S];
        float c = (S - 1) * 0.5f;
        const float R = 118f;

        // 枝(細い弧)。
        int segs = 120;
        float prevX = 0f, prevY = 0f;
        for (int i = 0; i <= segs; i++)
        {
            float phi = Mathf.Lerp(40f, 150f, i / (float)segs) * Mathf.Deg2Rad;
            float x = c - R * Mathf.Sin(phi);
            float y = c - R * Mathf.Cos(phi);
            if (i > 0) DrawLine(px, S, S, prevX, prevY, x, y, 2.6f, TexGoldLine);
            prevX = x;
            prevY = y;
        }

        // 葉(外側・内側を交互に)。先端へ向かって少しずつ小さくする。
        const int leaves = 11;
        for (int i = 0; i < leaves; i++)
        {
            float u = i / (float)(leaves - 1);
            float phi = Mathf.Lerp(46f, 146f, u) * Mathf.Deg2Rad;
            float bx = c - R * Mathf.Sin(phi);
            float by = c - R * Mathf.Cos(phi);
            // 法線(外向き)。
            float nx = -Mathf.Sin(phi), ny = -Mathf.Cos(phi);
            // 接線(先端方向)。
            float tx = -Mathf.Cos(phi), ty = Mathf.Sin(phi);
            float taper = Mathf.Lerp(1f, 0.62f, Mathf.Abs(u - 0.5f) * 2f);
            for (int side = 0; side < 2; side++)
            {
                float sgn = side == 0 ? 1f : -1f;                       // 外/内
                float ox = bx + nx * 8f * sgn, oy = by + ny * 8f * sgn;
                // 葉の向き = 接線から外側へ 38° 開く。
                float ang = Mathf.Atan2(ty, tx) + sgn * 38f * Mathf.Deg2Rad;
                DrawLeaf(px, S, S, ox, oy, ang, 30f * taper, 9.5f * taper,
                    side == 0 ? TexGoldBright : TexGoldLine);
            }
        }
        laurelSprite = MakeSprite(px, S, S, "ResultLaurel");
        return laurelSprite;
    }

    // 順位アイコン(細線の表彰台)。Material Symbols に相当する図柄が無いので焼く。
    private Sprite CreatePodiumIconSprite()
    {
        const int S = 64;
        Color32[] px = new Color32[S * S];
        Color32 col = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
        // 3 本のバー(中央が高い)。
        DrawRectOutline(px, S, S, 6f, 14f, 20f, 34f, 3.2f, col);
        DrawRectOutline(px, S, S, 22f, 14f, 42f, 48f, 3.2f, col);
        DrawRectOutline(px, S, S, 44f, 14f, 58f, 40f, 3.2f, col);
        DrawLine(px, S, S, 4f, 13f, 60f, 13f, 3.2f, col);
        return MakeSprite(px, S, S, "ResultRankingIcon");
    }

    // =======================================================================
    //  描画プリミティブ
    // =======================================================================

    private static void Blend(Color32[] buf, int w, int h, int x, int y, Color c, float cov)
    {
        if (cov <= 0f || x < 0 || y < 0 || x >= w || y >= h) return;
        cov = Mathf.Clamp01(cov);
        int i = y * w + x;
        Color32 d = buf[i];
        float da = d.a / 255f;
        float outA = cov + da * (1f - cov);
        if (outA <= 0f) { buf[i] = new Color32(0, 0, 0, 0); return; }
        float r = (c.r * cov + d.r / 255f * da * (1f - cov)) / outA;
        float g = (c.g * cov + d.g / 255f * da * (1f - cov)) / outA;
        float b = (c.b * cov + d.b / 255f * da * (1f - cov)) / outA;
        buf[i] = new Color32((byte)(Mathf.Clamp01(r) * 255f), (byte)(Mathf.Clamp01(g) * 255f),
            (byte)(Mathf.Clamp01(b) * 255f), (byte)(Mathf.Clamp01(outA) * 255f));
    }

    private static void Blend(Color32[] buf, int w, int h, int x, int y, Color32 c, float cov)
    {
        Blend(buf, w, h, x, y, new Color(c.r / 255f, c.g / 255f, c.b / 255f, 1f), cov * (c.a / 255f));
    }

    private static void DrawLine(Color32[] buf, int w, int h,
        float x0, float y0, float x1, float y1, float thick, Color32 col)
    {
        float minX = Mathf.Min(x0, x1) - thick, maxX = Mathf.Max(x0, x1) + thick;
        float minY = Mathf.Min(y0, y1) - thick, maxY = Mathf.Max(y0, y1) + thick;
        float dx = x1 - x0, dy = y1 - y0;
        float len2 = Mathf.Max(1e-5f, dx * dx + dy * dy);
        for (int y = Mathf.Max(0, (int)minY); y <= Mathf.Min(h - 1, (int)maxY + 1); y++)
        {
            for (int x = Mathf.Max(0, (int)minX); x <= Mathf.Min(w - 1, (int)maxX + 1); x++)
            {
                float t = Mathf.Clamp01(((x - x0) * dx + (y - y0) * dy) / len2);
                float px = x0 + dx * t, py = y0 + dy * t;
                float d = Mathf.Sqrt((x - px) * (x - px) + (y - py) * (y - py)) - thick * 0.5f;
                Blend(buf, w, h, x, y, col, Mathf.Clamp01(0.5f - d));
            }
        }
    }

    private static void DrawDiamond(Color32[] buf, int w, int h, float cx, float cy, float half, Color32 col)
    {
        int r = Mathf.CeilToInt(half) + 2;
        for (int y = (int)cy - r; y <= (int)cy + r; y++)
        {
            for (int x = (int)cx - r; x <= (int)cx + r; x++)
            {
                float d = (Mathf.Abs(x - cx) + Mathf.Abs(y - cy)) * 0.7071f - half * 0.7071f;
                Blend(buf, w, h, x, y, col, Mathf.Clamp01(0.5f - d));
            }
        }
    }

    // 先の尖った葉(長軸 len・半幅 wid・角度 ang)。
    private static void DrawLeaf(Color32[] buf, int w, int h, float cx, float cy,
        float ang, float len, float wid, Color32 col)
    {
        float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
        float ex = cx + ca * len * 0.5f, ey = cy + sa * len * 0.5f;   // 中心は根元と先の中点
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

    private static void DrawRectOutline(Color32[] buf, int w, int h,
        float x0, float y0, float x1, float y1, float thick, Color32 col)
    {
        DrawLine(buf, w, h, x0, y0, x1, y0, thick, col);
        DrawLine(buf, w, h, x0, y1, x1, y1, thick, col);
        DrawLine(buf, w, h, x0, y0, x0, y1, thick, col);
        DrawLine(buf, w, h, x1, y0, x1, y1, thick, col);
    }

    private Texture2D MakeTexture(Color32[] px, int w, int h, string name)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.name = name;
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.SetPixels32(px);
        tex.Apply();
        generatedTextures.Add(tex);
        return tex;
    }

    private Sprite MakeSprite(Color32[] px, int w, int h, string name)
    {
        Texture2D tex = MakeTexture(px, w, h, name);
        Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = name;
        generatedSprites.Add(sprite);
        return sprite;
    }

    // =======================================================================
    //  UI 生成ヘルパー
    // =======================================================================

    // 罫線 1 本(中央に菱形を置くかどうか)。
    private void AddRule(RectTransform parent, Vector2 pos, float width, bool withDiamond)
    {
        Image rule = NewImage("Rule", parent, new Color(1f, 1f, 1f, 0.85f));
        rule.sprite = CreateRuleSprite();
        rule.type = Image.Type.Simple;
        SetRect(rule.rectTransform, pos, new Vector2(width, 4f));
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

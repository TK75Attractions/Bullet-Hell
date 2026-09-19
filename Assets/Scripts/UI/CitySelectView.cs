using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;

/// <summary>
/// ステージ選択「城壁の街」スタイル(PlayerPrefs stageSelectStyle = 2)の前景ウィジェット。
///
/// 街そのものは <see cref="CityMapController"/> が専用カメラ→RenderTexture で描く。ここは
/// その RT を貼る最背面の RawImage と、選択中の区画の真上に出る▼＋明朝ラベル、右下の情報
/// パネル(曲名・説明・長さ・プレビュー動画)だけを持つ。
///
/// カルーセル(style 1)の資産はそのまま残してあり、<see cref="JsabStageSelect"/> が
/// スタイルに応じてどちらを見せるか切り替える。上部バー(タイマー)・難易度モーダル・
/// 下部ヒントバーは style 1 のものをそのまま共有する。
/// </summary>
public class CitySelectView : MonoBehaviour
{
    // ---- ▼とラベル(タイトルの部屋と同じ様式) --------------------------------
    private const string MinchoFontResource = "Fonts/ShipporiMincho-Regular SDF";
    // ▼はタイトルと同じ 12x8 ドットの三角を Point で 2 倍に拡大したドット絵。
    // 第 U8 便(2026-09-19 指示)でドット絵の▼をやめ、滑らかな三角へ。
    // タイトルの▼と同じ 0.75 倍(24x16 → 18x12)。
    private const float MarkerArrowW = 18f;
    private const float MarkerArrowH = 12f;
    private const float MarkerFloatPx = 4f;
    private const float MarkerLabelGap = 46f;
    private const float MarkerLabelH = 46f;
    private const float MarkerLabelFont = 32f;
    private const float MarkerLabelBoxW = 520f;
    private const float MarkerLabelSpacing = 4f;
    private const float MarkerFadeSpeed = 1f / 0.15f;
    // ▼は枠なしの真っ白。縁取り(背後の暗い三角)は置かない(タイトルと同じ様式)。
    private static readonly Color MarkerArrowInk = new Color(1f, 1f, 1f, 1f);
    private static readonly Color MarkerLabelInk = new Color(0.976f, 0.961f, 0.918f, 1f);
    // ラベルは縁なし・右下 2px の落ち影 1 枚だけ。
    private static readonly Color MarkerLabelShadow = new Color(0f, 0f, 0f, 0.60f);
    private static readonly Vector2 MarkerLabelShadowOffset = new Vector2(2f, -2f);

    // ---- 情報パネル ----------------------------------------------------------
    // 第 14 便: 右下の小パネルは廃止(右半分へ既存の JSAB カードを移したため)。
    // 生成コードはそのまま残してあり、この定数を true にすれば元どおり出る。
    private static readonly bool ShowLegacyInfoPanel = false;
    private const float PanelW = 640f;
    private const float PanelH = 400f;
    private const float PanelInsetX = 96f;   // 19° の斜辺を避ける左右の余白
    private static readonly Color Cyan = new Color(0.22f, 0.76f, 0.878f, 1f);

    private RectTransform root;
    // 残り時間のストップウォッチ(Highland Timer v14・2026-09-19)。
    private readonly StageTimerWidget timer = new StageTimerWidget();
    private TMP_FontAsset uiFont;
    private TMP_FontAsset minchoFont;
    private bool minchoTried;

    private RawImage cityView;
    // cityView へ最後に入れたマテリアル(null=既定)。ドット風の色数減衰でだけ使う。
    private Material appliedViewMaterial;
    private RectTransform markerRoot;
    private Image markerArrow;
    private CanvasGroup markerLabelCG;
    private TMP_Text markerLabel;
    private TMP_Text[] markerLabelShadows;
    private bool markerInkCentered;

    private CanvasGroup panelCG;
    private TMP_Text panelName;
    private TMP_Text panelDesc;
    private TMP_Text panelMeta;
    private CanvasGroup previewCG;
    private RawImage previewImage;
    private Image previewFallback;
    private VideoPlayer previewVideo;
    private RenderTexture previewRT;

    private Sprite arrowSprite;
    private readonly System.Collections.Generic.List<Texture2D> ownedTextures = new System.Collections.Generic.List<Texture2D>();
    private readonly System.Collections.Generic.List<Sprite> ownedSprites = new System.Collections.Generic.List<Sprite>();

    // ---- 右パネル(2026-09-19 U7 便で「Highland UI v11」へ差し替え) ------------
    // 出典は Instructions/UI/from_user_20260919/v11/。編集マスターの SVG は 1672x941 なので、
    // 位置・寸法はすべて SVG 座標で書き、HighlandUi.X/Y/L で 1920x1080 へ読み替える
    // (一律 1920/1672 = 1.1483 倍)。
    // 旧 U3/U5 の「紺と金の額装パネル(GoldPanelStyle)」は街モードでは使わない。

    // 右側の暗幕(StageBackdrop)。
    private const float ScrimCx = 1316f, ScrimCy = 470.5f, ScrimW = 712f, ScrimH = 941f;
    // 見出し。
    private const float HeadNumberY = 91f, HeadRuleY = 119f, HeadNameY = 232f, HeadGoldRuleY = 261f;
    private const float ColCx = 1367f;
    // サムネ(v11 は 509x175 の横長。CG は 16:9 なので中央を切り出して収める)。
    // サムネ枠(第 U8 便・2026-09-19 指示「サムネの枠を 16:9 に」)。
    // v11 の絵は 509x175(約 2.9:1)で、16:9 の CG の上下 39% を切っていた。
    // 幅はそのまま、高さを 509*9/16 = 286.3 → 286 にして 16:9(実 1.7797)にし、
    // 上へ 12px ずらして(上端 324→312)情報行を下へ詰めた。
    private const float ThumbCx = 1367.5f, ThumbCy = 455f, ThumbW = 509f, ThumbH = 286f;
    // 情報行。
    private const float RowLabelX = 1210f, RowValueX = 1404f, RowIconX = 1170f, RowRuleX = 1367f;
    // サムネを 16:9 に伸ばしたぶん、情報行を下へ詰めた(第 U8 便)。
    // 旧: 622 / 684。決定ボタンの上端(779)との隙間は 56px 残る。
    private const float LengthRowY = 648f, StatusRowY = 706f;
    private const float StatusGemX0 = 1414f, StatusGemPitch = 42f;
    // 難易度一覧。
    private const float DiffHeadY = 357f, DiffHeadRuleY = 349f;
    private const float DiffRowCx = 1367.5f, DiffRowW = 509f, DiffRowH = 74f;
    private static readonly float[] DiffRowCy = { 431f, 522f, 613f };
    private static readonly float[] DiffNameBaseline = { 445f, 536f, 627f };
    private const float DiffNameX = 1140f, DiffBestX = 1595f, DiffGemX = 1380f;
    private const float DiffAccentX = 1119.5f, DiffAccentW = 3f, DiffAccentH = 48f;
    private const float DiffLeverX = 1308.6f, DiffLeverY = 705f, DiffGuideX = 1338.6f, DiffGuideY = 713f;
    // 仕切り罫とボタン。
    private const float FooterRuleY = 740f;
    private const float ButtonCx = 1367f, ButtonCy = 820.5f, ButtonW = 376f, ButtonH = 83f;
    private const float ButtonIconCx = 1334f, ButtonIconCy = 829.5f, ButtonIconSize = 51.6f;
    private const float ButtonTextX = 1367.68f, ButtonTextY = 837f;

    // 難易度の色(v11 の D_*_Accent)。0=簡単 / 1=普通 / 2=難しい。
    private static readonly Color[] DiffAccentColors =
    {
        new Color(0.584f, 0.784f, 0.592f, 1f),   // #95C897
        new Color(1.000f, 0.882f, 0.416f, 1f),   // #FFE16A
        new Color(0.894f, 0.533f, 0.569f, 1f),   // #E48891
    };
    private static readonly string[] DiffNameMarkup = { "[簡単|かんたん]", "[普通|ふつう]", "[難|むずか]しい" };

    private CanvasGroup stagePanelCG;
    private RectTransform stagePanelRect;
    private TMP_Text stageNumberText;
    private HighlandUi.RubyText stageNameRuby;
    private RawImage thumbImage;
    private Image thumbPlate;
    private TMP_Text lengthValue;
    // STATUS 行: 難易度ごとの菱形。index 0=EASY / 1=NORMAL / 2=LUNATIC。
    private Image[] statusGems;
    private Texture2D thumbTexture;
    private string thumbDir;

    // 本文(サムネ+情報行)と難易度一覧は同じパネルの中で差し替える。
    private CanvasGroup infoBodyCG;
    private CanvasGroup diffBodyCG;
    private Image[] diffRowPlate;      // 非選択の地
    private Image[] diffRowSelected;   // 選択中の地(金枠)
    private Image[] diffRowAccent;
    private Image[] diffRowGem;
    private HighlandUi.RubyText[] diffRowName;
    private TMP_Text[] diffRowBestLabel;
    private TMP_Text[] diffRowBestValue;
    private Sprite gemFilled, gemHollow;
    private int diffIndex = 1;
    private bool[] diffEnabled = { true, true, true };
    private bool diffMode;
    private float diffBlend;           // 0 = 本文 / 1 = 難易度一覧
    private StageData currentStage;
    private bool twoPlayerBest;

    // ふりがなの実測合わせ(表示後の初回に 1 度だけ効く)。
    private readonly System.Collections.Generic.List<HighlandUi.RubyText> rubies
        = new System.Collections.Generic.List<HighlandUi.RubyText>();
    private bool rubiesPlaced;

    // 残り 10 秒を切ったときだけ画面下端へ出す明朝の残り秒数。
    private TMP_Text timeLeftText;
    private TMP_Text timeLeftShadow;
    private float timeLeftAlpha;

    private CityMapController map;
    private int district;
    private float markerAlpha;
    private float animTime;
    private string videoUrl;

    public CityMapController Map => map;

    // ---- 生成 ----------------------------------------------------------------

    public static CitySelectView Create(RectTransform parent, TMP_FontAsset font)
    {
        GameObject go = new GameObject("CitySelectView", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        CitySelectView view = go.AddComponent<CitySelectView>();
        view.root = rt;
        view.uiFont = font;
        view.Build();
        return view;
    }

    private void Build()
    {
        // 街の描画役(3D)は Canvas のスケールを受けないようシーンのルートへ置く。
        GameObject rig = new GameObject("CityMapRig");
        map = rig.AddComponent<CityMapController>();
        map.cityPrefab = Resources.Load<GameObject>("CityCG/CityMap_v6b");
        map.targetTexture = Resources.Load<RenderTexture>("CityCG/CityMapRT");
        map.rendererIndex = 1;

        // 最背面の表示板。
        GameObject viewGO = new GameObject("CityView", typeof(RectTransform));
        viewGO.transform.SetParent(root, false);
        cityView = viewGO.AddComponent<RawImage>();
        cityView.texture = map.Texture;
        cityView.material = appliedViewMaterial = map.ViewMaterial;
        cityView.raycastTarget = false;
        Stretch(cityView.rectTransform);

        BuildMarker();
        BuildPanel();
        BuildStagePanel();
        BuildTimeLeft();
    }

    // SVG 座標(中心)と画面 px の寸法で Graphic を置く(罫線など、縦だけ 1:1 で焼くもの用)。
    private static void PlacePx(Graphic g, float svgCx, float svgCy, float pxW, float pxH)
    {
        RectTransform rt = g.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(HighlandUi.X(svgCx), HighlandUi.Y(svgCy));
        rt.sizeDelta = new Vector2(pxW, pxH);
    }

    private HighlandUi.RubyText Ruby(string name, Transform parent, string markup, float svgSize,
        Color color, TextAlignmentOptions align, bool bold = false, float tracking = 0f)
    {
        HighlandUi.RubyText r = HighlandUi.Ruby(name, parent, markup, svgSize, color, align, bold, tracking);
        rubies.Add(r);
        rubiesPlaced = false;
        return r;
    }

    /// <summary>
    /// 右パネル(Highland UI v11)。画面右の暗幕の上に、上から
    /// 1) STAGE 番号 2) ステージ名(ふりがな) 3) CG サムネ 4) 情報行 5) 仕切り 6) 決定ボタン。
    /// 3〜4 は「決定」で難易度一覧と差し替わる(<see cref="SetDifficultyMode"/>)。
    /// </summary>
    private void BuildStagePanel()
    {
        GameObject root2 = new GameObject("V11Panel", typeof(RectTransform), typeof(CanvasGroup));
        root2.transform.SetParent(root, false);
        stagePanelRect = (RectTransform)root2.transform;
        stagePanelRect.anchorMin = stagePanelRect.anchorMax = new Vector2(0.5f, 0.5f);
        stagePanelRect.pivot = new Vector2(0.5f, 0.5f);
        stagePanelRect.anchoredPosition = Vector2.zero;
        stagePanelRect.sizeDelta = new Vector2(1920f, 1080f);
        stagePanelCG = root2.GetComponent<CanvasGroup>();
        stagePanelCG.blocksRaycasts = false;
        stagePanelCG.alpha = 0f;

        BuildV11Scrim();
        BuildV11Header();
        BuildV11InfoBody();
        BuildV11DifficultyBody();
        BuildV11Footer();
        ApplyDifficultyVisual();
    }

    private void BuildV11Scrim()
    {
        Image scrim = HighlandUi.NewImage("Scrim", stagePanelRect,
            HighlandUi.HorizontalScrim(ownedTextures, ownedSprites), Color.white);
        HighlandUi.Place(scrim, ScrimCx, ScrimCy, ScrimW, ScrimH);
    }

    private void BuildV11Header()
    {
        // STAGE 番号。
        stageNumberText = HighlandUi.Text("StageNumber", stagePanelRect, "", 25f,
            new Color(0.949f, 0.949f, 0.949f, 0.88f), TextAlignmentOptions.Center, false, 9.5f);
        HighlandUi.PlaceCentered(stageNumberText, ColCx, HeadNumberY, 520f, 25f);

        // 銀の細罫(両側)+ 中空の菱形。
        Sprite silver = HighlandUi.FlatRule(Mathf.RoundToInt(HighlandUi.L(82f)), 10, 1.35f * HighlandUi.S,
            new Color32(0xC5, 0xC5, 0xC5, 0xFF), ownedTextures, ownedSprites, "V11HeadRule");
        foreach (float cx in new[] { (1270f + 1352f) * 0.5f, (1382f + 1464f) * 0.5f })
        {
            Image r = HighlandUi.NewImage("HeadRule", stagePanelRect, silver, new Color(1f, 1f, 1f, 0.75f));
            PlacePx(r, cx, HeadRuleY, HighlandUi.L(82f), 10f);
        }
        AddDiamondPx("HeadDiamond", stagePanelRect, ColCx, HeadRuleY, 12.6f, 16f, 1.9f,
            new Color(0.867f, 0.867f, 0.867f, 1f));

        // ステージ名(ふりがな付き)。
        stageNameRuby = Ruby("StageName", stagePanelRect, "", 70.5f, HighlandUi.Ink,
            TextAlignmentOptions.Center, true, 11.1f);
        HighlandUi.PlaceCentered(stageNameRuby.Body, ColCx, HeadNameY, 640f, 70.5f);

        // 金の飾り罫(両端が消える)+ 琥珀のにじみ + 中空の菱形。
        Image halo = HighlandUi.NewImage("TitleHalo", stagePanelRect,
            HighlandUi.Halo(ownedTextures, ownedSprites), Color.white);
        HighlandUi.Place(halo, ColCx, HeadGoldRuleY, 57.0f, 57.6f);
        Sprite gold = HighlandUi.FadeRule(Mathf.RoundToInt(HighlandUi.L(410f)), 12, 1.1f * HighlandUi.S,
            HighlandUi.L(28f),
            new[] { 0f, 0.22f, 0.5f, 0.78f, 1f },
            new[] { Col(0xBC, 0xAA, 0x4E), Col(0xBC, 0xAA, 0x4E), Col(0xFF, 0xE1, 0x6A),
                    Col(0xBC, 0xAA, 0x4E), Col(0xBC, 0xAA, 0x4E) },
            new[] { 0f, 0.30f, 0.95f, 0.30f, 0f },
            ownedTextures, ownedSprites, "V11TitleRule");
        Image gr = HighlandUi.NewImage("TitleRule", stagePanelRect, gold, Color.white);
        PlacePx(gr, ColCx, HeadGoldRuleY, HighlandUi.L(410f), 12f);
        AddDiamondPx("TitleDiamond", stagePanelRect, ColCx, HeadGoldRuleY, 12.4f, 16f, 1.55f,
            new Color(1f, 0.894f, 0.439f, 1f));
    }

    private void BuildV11InfoBody()
    {
        GameObject go = new GameObject("InfoBody", typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(stagePanelRect, false);
        RectTransform rt = (RectTransform)go.transform;
        Stretch(rt);
        infoBodyCG = go.GetComponent<CanvasGroup>();
        infoBodyCG.blocksRaycasts = false;

        // サムネ: 地 → CG → 外枠 → 内枠(枠を CG で隠さないよう別スプライトで重ねる)。
        int tw = Mathf.RoundToInt(HighlandUi.L(ThumbW)), th = Mathf.RoundToInt(HighlandUi.L(ThumbH));
        thumbPlate = HighlandUi.NewImage("ThumbPlate", rt,
            HighlandUi.NotchFlat(tw, th, HighlandUi.L(4f), Col(0x16, 0x16, 0x2D), 1f,
                Col(0, 0, 0), 0f, 0f, ownedTextures, ownedSprites, "V11ThumbPlate"), Color.white);
        HighlandUi.Place(thumbPlate, ThumbCx, ThumbCy, ThumbW, ThumbH);

        thumbImage = new GameObject("ThumbImage", typeof(RectTransform)).AddComponent<RawImage>();
        thumbImage.transform.SetParent(rt, false);
        thumbImage.raycastTarget = false;
        thumbImage.enabled = false;
        HighlandUi.Place(thumbImage, ThumbCx, ThumbCy, ThumbW - 2f, ThumbH - 2f);
        // 16:9 の CG を 509:175 の枠へ中央で切り出す。
        float uvH = (16f / 9f) / (ThumbW / ThumbH);
        thumbImage.uvRect = new Rect(0f, (1f - uvH) * 0.5f, 1f, uvH);

        Image outer = HighlandUi.NewImage("ThumbFrame", rt,
            HighlandUi.NotchFlat(tw, th, HighlandUi.L(4f), Col(0, 0, 0), 0f,
                Col(0xBC, 0xBC, 0xBC), 1.2f * HighlandUi.S, 0.69f,
                ownedTextures, ownedSprites, "V11ThumbFrame"), Color.white);
        HighlandUi.Place(outer, ThumbCx, ThumbCy, ThumbW, ThumbH);

        Image inner = HighlandUi.NewImage("ThumbInner", rt,
            HighlandUi.NotchFlat(Mathf.RoundToInt(HighlandUi.L(ThumbW - 6f)),
                Mathf.RoundToInt(HighlandUi.L(ThumbH - 6f)), HighlandUi.L(7f), Col(0, 0, 0), 0f,
                Col(0x8A, 0x8A, 0x8A), 0.55f * HighlandUi.S, 0.38f,
                ownedTextures, ownedSprites, "V11ThumbInner"), Color.white);
        HighlandUi.Place(inner, ThumbCx, ThumbCy, ThumbW - 6f, ThumbH - 6f);

        // 情報行 1: プレイ時間。
        AddRowIcon(rt, HighlandUi.ClockIcon(34, ownedTextures, ownedSprites), LengthRowY);
        HighlandUi.RubyText lengthLabel = Ruby("LengthLabel", rt, "プレイ[時間|じかん]", 20.5f,
            HighlandUi.InkSoft, TextAlignmentOptions.Left, false, 1.1f);
        HighlandUi.PlaceLeft(lengthLabel.Body, RowLabelX, LengthRowY + 6.6f, 260f, 20.5f);
        AddRowRule(rt, LengthRowY);
        lengthValue = HighlandUi.Text("LengthValue", rt, "--:--", 25f, HighlandUi.Ink,
            TextAlignmentOptions.Left, false, 2.1f);
        HighlandUi.PlaceLeft(lengthValue, RowValueX, LengthRowY + 9.2f, 220f, 25f);

        // 情報行 2: 状態(難易度ごとの踏破を菱形 3 つで。2026-09-16 U5 の踏襲)。
        AddRowIcon(rt, HighlandUi.FlagIcon(34, ownedTextures, ownedSprites), StatusRowY);
        HighlandUi.RubyText statusLabel = Ruby("StatusLabel", rt, "[状態|じょうたい]", 20.5f,
            HighlandUi.InkSoft, TextAlignmentOptions.Left, false, 1.1f);
        HighlandUi.PlaceLeft(statusLabel.Body, RowLabelX, StatusRowY + 6.6f, 260f, 20.5f);
        AddRowRule(rt, StatusRowY);

        gemFilled = HighlandUi.Gem(19, 22, true, 0f, ownedTextures, ownedSprites, "V11GemFill");
        gemHollow = HighlandUi.Gem(19, 22, false, 1.7f, ownedTextures, ownedSprites, "V11GemHollow");
        statusGems = new Image[StageDifficultyProgress.DifficultyCount];
        for (int i = 0; i < statusGems.Length; i++)
        {
            Image g = HighlandUi.NewImage("StatusGem" + i, rt, gemFilled, HighlandUi.Accent);
            HighlandUi.Place(g, StatusGemX0 + StatusGemPitch * i, StatusRowY, 19f, 22f);
            statusGems[i] = g;
        }
    }

    private void AddRowIcon(Transform parent, Sprite icon, float svgCy)
    {
        Image img = HighlandUi.NewImage("RowIcon", parent, icon, new Color(0.929f, 0.929f, 0.929f, 1f));
        HighlandUi.Place(img, RowIconX, svgCy, 34f, 34f);
    }

    private void AddRowRule(Transform parent, float svgCy)
    {
        Image img = HighlandUi.NewImage("RowRule", parent,
            HighlandUi.SolidBar(ownedTextures, ownedSprites), new Color(0.749f, 0.749f, 0.749f, 0.62f));
        HighlandUi.Place(img, RowRuleX, svgCy, 1f, 26f);
    }

    private void BuildV11DifficultyBody()
    {
        GameObject go = new GameObject("DifficultyBody", typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(stagePanelRect, false);
        RectTransform rt = (RectTransform)go.transform;
        Stretch(rt);
        diffBodyCG = go.GetComponent<CanvasGroup>();
        diffBodyCG.blocksRaycasts = false;
        diffBodyCG.alpha = 0f;
        go.SetActive(false);

        // 見出し「難しさ」と両側の細罫。
        Sprite headRule = HighlandUi.FlatRule(Mathf.RoundToInt(HighlandUi.L(117f)), 10,
            0.85f * HighlandUi.S, Col(0xC8, 0xC8, 0xC8), ownedTextures, ownedSprites, "V11DiffHeadRule");
        foreach (float cx in new[] { (1116f + 1233f) * 0.5f, (1501f + 1618f) * 0.5f })
        {
            Image r = HighlandUi.NewImage("DiffHeadRule", rt, headRule, new Color(1f, 1f, 1f, 0.52f));
            PlacePx(r, cx, DiffHeadRuleY, HighlandUi.L(117f), 10f);
        }
        HighlandUi.RubyText head = Ruby("DiffHeading", rt, "[難|むずか]しさ", 23.5f,
            HighlandUi.InkSoft, TextAlignmentOptions.Center, false, 3f);
        HighlandUi.PlaceCentered(head.Body, ColCx, DiffHeadY, 360f, 23.5f);

        int rw = Mathf.RoundToInt(HighlandUi.L(DiffRowW)), rh = Mathf.RoundToInt(HighlandUi.L(DiffRowH));
        Sprite plate = HighlandUi.NotchFlat(rw, rh, HighlandUi.L(5f), Col(0x16, 0x16, 0x2D), 0.55f,
            Col(0x9D, 0x9D, 0xA8), 0.85f * HighlandUi.S, 0.70f, ownedTextures, ownedSprites, "V11DiffPlate");
        Sprite chosen = HighlandUi.NotchPanel(rw, rh, HighlandUi.L(5f), true,
            ownedTextures, ownedSprites, "V11DiffChosen",
            HighlandUi.L(4f), 1.7f * HighlandUi.S, 0.7f * HighlandUi.S);

        int n = DiffRowCy.Length;
        diffRowPlate = new Image[n];
        diffRowSelected = new Image[n];
        diffRowAccent = new Image[n];
        diffRowGem = new Image[n];
        diffRowName = new HighlandUi.RubyText[n];
        diffRowBestLabel = new TMP_Text[n];
        diffRowBestValue = new TMP_Text[n];

        for (int i = 0; i < n; i++)
        {
            float cy = DiffRowCy[i];
            diffRowPlate[i] = HighlandUi.NewImage("DiffPlate" + i, rt, plate, Color.white);
            HighlandUi.Place(diffRowPlate[i], DiffRowCx, cy, DiffRowW, DiffRowH);
            diffRowSelected[i] = HighlandUi.NewImage("DiffChosen" + i, rt, chosen, Color.white);
            HighlandUi.Place(diffRowSelected[i], DiffRowCx, cy, DiffRowW, DiffRowH);

            diffRowAccent[i] = HighlandUi.NewImage("DiffAccent" + i, rt,
                HighlandUi.SolidBar(ownedTextures, ownedSprites), DiffAccentColors[i]);
            HighlandUi.Place(diffRowAccent[i], DiffAccentX, cy, DiffAccentW, DiffAccentH);

            diffRowGem[i] = HighlandUi.NewImage("DiffGem" + i, rt, gemFilled, HighlandUi.Accent);
            HighlandUi.Place(diffRowGem[i], DiffGemX, cy, 19f, 22f);

            diffRowName[i] = Ruby("DiffName" + i, rt, DiffNameMarkup[i], 23f,
                HighlandUi.InkSoft, TextAlignmentOptions.Left, false, 1.2f);
            HighlandUi.PlaceLeft(diffRowName[i].Body, DiffNameX, DiffNameBaseline[i], 220f, 23f);

            diffRowBestLabel[i] = HighlandUi.Text("DiffBestLabel" + i, rt, "ベスト", 15.5f,
                HighlandUi.InkSoft, TextAlignmentOptions.Right, false, 1.2f);
            HighlandUi.PlaceRight(diffRowBestLabel[i], DiffBestX, DiffNameBaseline[i] - 28f, 160f, 15.5f);
            diffRowBestValue[i] = HighlandUi.Text("DiffBestValue" + i, rt, "—", 21f,
                HighlandUi.InkSoft, TextAlignmentOptions.Right, false, 1f);
            HighlandUi.PlaceRight(diffRowBestValue[i], DiffBestX, DiffNameBaseline[i] + 2f, 220f, 21f);
        }

        // 操作ヒント「上下で選ぶ」(レバーの絵 + 明朝)。
        Image lever = HighlandUi.NewImage("LeverIcon", rt, HighlandUi.Icon("lever_white"), Color.white);
        // 第 U8 便(2026-09-19 指示): レバーを 1.4 倍(33 → 46)にして〇✕ と重さを揃える。
        HighlandUi.Place(lever, DiffLeverX, DiffLeverY, 46f, 46f);
        HighlandUi.RubyText guide = Ruby("DiffGuide", rt, "[上下|じょうげ]で[選|えら]ぶ", 20f,
            HighlandUi.InkSoft, TextAlignmentOptions.Left, false, 0.7f);
        HighlandUi.PlaceLeft(guide.Body, DiffGuideX, DiffGuideY, 260f, 20f);
    }

    private void BuildV11Footer()
    {
        Sprite footer = HighlandUi.FadeRule(Mathf.RoundToInt(HighlandUi.L(526f)), 12, 1.1f * HighlandUi.S,
            HighlandUi.L(30f),
            new[] { 0f, 0.18f, 0.5f, 0.82f, 1f },
            new[] { Col(0xD6, 0xD6, 0xD6), Col(0xAD, 0xAD, 0xAD), Col(0xD0, 0xD0, 0xD0),
                    Col(0xAD, 0xAD, 0xAD), Col(0xD6, 0xD6, 0xD6) },
            new[] { 0f, 0.45f, 0.72f, 0.45f, 0f },
            ownedTextures, ownedSprites, "V11FooterRule");
        Image fr = HighlandUi.NewImage("FooterRule", stagePanelRect, footer, Color.white);
        PlacePx(fr, ColCx, FooterRuleY, HighlandUi.L(526f), 12f);
        AddDiamondPx("FooterDiamond", stagePanelRect, ColCx, FooterRuleY, 12f, 15f, 1.55f,
            new Color(0.871f, 0.871f, 0.871f, 1f));

        Image btn = HighlandUi.NewImage("TravelButton", stagePanelRect,
            HighlandUi.NotchPanel(Mathf.RoundToInt(HighlandUi.L(ButtonW)),
                Mathf.RoundToInt(HighlandUi.L(ButtonH)), HighlandUi.L(9.5f), true,
                ownedTextures, ownedSprites, "V11TravelButton",
                HighlandUi.L(4.5f), 1.8f * HighlandUi.S, 0.7f * HighlandUi.S), Color.white);
        HighlandUi.Place(btn, ButtonCx, ButtonCy, ButtonW, ButtonH);

        Image icon = HighlandUi.NewImage("ButtonIcon", stagePanelRect, HighlandUi.Icon("circle"), Color.white);
        HighlandUi.Place(icon, ButtonIconCx, ButtonIconCy, ButtonIconSize, ButtonIconSize);

        HighlandUi.RubyText label = Ruby("ButtonText", stagePanelRect, "[決定|けってい]", 26f,
            HighlandUi.Ink, TextAlignmentOptions.Left, true, 1f);
        HighlandUi.PlaceLeft(label.Body, ButtonTextX, ButtonTextY, 200f, 26f);
    }

    private void AddDiamondPx(string name, Transform parent, float svgCx, float svgCy,
        float svgW, float svgH, float strokeSvg, Color color)
    {
        int w = Mathf.RoundToInt(HighlandUi.L(svgW)), h = Mathf.RoundToInt(HighlandUi.L(svgH));
        Image img = HighlandUi.NewImage(name, parent,
            HighlandUi.DiamondRect(w, h, false, strokeSvg * HighlandUi.S,
                ownedTextures, ownedSprites, "V11Dia" + name), color);
        HighlandUi.Place(img, svgCx, svgCy, svgW, svgH);
    }

    private static Color32 Col(byte r, byte g, byte b) { return new Color32(r, g, b, 0xFF); }

    // 残り時間の最小表示(上部バーは街モードでは隠すため)。
    private void BuildTimeLeft()
    {
        // 残り 10 秒を切ったときだけ画面下端に小さく出す(上部バーは街モードでは隠す)。
        // 明るい石壁の上でも読めるよう、▼のラベルと同じ右下 2px の落ち影を 1 枚敷く。
        timeLeftShadow = NewText("TimeLeftShadow", root, "", 26f, MarkerLabelShadow, TextAlignmentOptions.Center);
        timeLeftText = NewText("TimeLeft", root, "", 26f, new Color(0.90f, 0.88f, 0.84f, 1f), TextAlignmentOptions.Center);
        foreach (TMP_Text t in new[] { timeLeftShadow, timeLeftText })
        {
            RectTransform tr = (RectTransform)t.transform;
            tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0f);
            tr.pivot = new Vector2(0.5f, 0f);
            tr.sizeDelta = new Vector2(400f, 40f);
            tr.anchoredPosition = new Vector2(0f, 26f)
                + (t == timeLeftShadow ? MarkerLabelShadowOffset : Vector2.zero);
            StyleLabel(t);
            t.characterSpacing = 2f;
            t.alpha = 0f;
        }
    }

    /// <summary>右パネルの表示(難易度モーダルのあいだは下ろす)。</summary>
    public void SetRightInfoVisible(bool on)
    {
        if (stagePanelRect != null && stagePanelRect.gameObject.activeSelf != on)
            stagePanelRect.gameObject.SetActive(on);
    }

    /// <summary>残り時間。街モードではストップウォッチ型タイマー(Highland Timer v14)を
    /// 画面の左下へ出し、旧「のこり N」の小さな表示は隠す(2026-09-19 指示)。</summary>
    public void SetRemainingTime(float seconds, float total, bool cityMode)
    {
        float dt = Time.unscaledDeltaTime;
        if (timer != null)
        {
            if (!timer.Built) timer.Build(root);
            timer.Tick(seconds, total, cityMode, dt);
        }
        if (timeLeftText == null) return;
        // 新タイマーが出ているあいだ旧表示は使わない。
        bool show = cityMode && timer == null && seconds <= 10.5f && seconds > 0.05f;
        if (show)
        {
            string label = string.Format("のこり {0}", Mathf.CeilToInt(seconds));
            timeLeftText.text = label;
            if (timeLeftShadow != null) timeLeftShadow.text = label;
        }
        timeLeftAlpha = Mathf.MoveTowards(timeLeftAlpha, show ? 1f : 0f, dt / 0.2f);
        timeLeftText.alpha = timeLeftAlpha;
        if (timeLeftShadow != null) timeLeftShadow.alpha = timeLeftAlpha * MarkerLabelShadow.a;
    }

    private void BuildMarker()
    {
        arrowSprite = TitleManager.CreateSmoothDownTriangleSprite();

        GameObject markerObj = new GameObject("DistrictMarker", typeof(RectTransform));
        markerObj.transform.SetParent(root, false);
        markerRoot = (RectTransform)markerObj.transform;
        markerRoot.anchorMin = markerRoot.anchorMax = new Vector2(0.5f, 0.5f);
        markerRoot.pivot = new Vector2(0.5f, 0.5f);
        markerRoot.sizeDelta = Vector2.zero;

        markerArrow = NewImage("Arrow", markerRoot, MarkerArrowInk);
        markerArrow.sprite = arrowSprite;
        markerArrow.rectTransform.sizeDelta = new Vector2(MarkerArrowW, MarkerArrowH);

        GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(CanvasGroup));
        labelObj.transform.SetParent(markerRoot, false);
        RectTransform label = (RectTransform)labelObj.transform;
        label.anchorMin = label.anchorMax = new Vector2(0.5f, 0.5f);
        label.pivot = new Vector2(0.5f, 0.5f);
        label.anchoredPosition = new Vector2(0f, MarkerLabelGap);
        label.sizeDelta = new Vector2(MarkerLabelBoxW, MarkerLabelH);
        markerLabelCG = labelObj.GetComponent<CanvasGroup>();
        markerLabelCG.alpha = 0f;
        markerLabelCG.blocksRaycasts = false;

        markerLabelShadows = new TMP_Text[1];
        {
            TMP_Text shadow = NewText("Shadow", label, "", MarkerLabelFont, MarkerLabelShadow, TextAlignmentOptions.Center);
            RectTransform sr = (RectTransform)shadow.transform;
            sr.sizeDelta = new Vector2(MarkerLabelBoxW, MarkerLabelH);
            sr.anchoredPosition = MarkerLabelShadowOffset;
            StyleLabel(shadow);
            markerLabelShadows[0] = shadow;
        }
        markerLabel = NewText("Text", label, "", MarkerLabelFont, MarkerLabelInk, TextAlignmentOptions.Center);
        ((RectTransform)markerLabel.transform).sizeDelta = new Vector2(MarkerLabelBoxW, MarkerLabelH);
        StyleLabel(markerLabel);
    }

    private void BuildPanel()
    {
        GameObject panelGO = new GameObject("InfoPanel", typeof(RectTransform), typeof(CanvasGroup));
        panelGO.transform.SetParent(root, false);
        RectTransform panel = (RectTransform)panelGO.transform;
        panel.anchorMin = panel.anchorMax = new Vector2(1f, 0f);
        panel.pivot = new Vector2(1f, 0f);
        // 下部ヒントバー(高さ 76)の上へ置く。
        panel.anchoredPosition = new Vector2(-40f, 96f);
        panel.sizeDelta = new Vector2(PanelW, PanelH);
        panelCG = panelGO.GetComponent<CanvasGroup>();
        panelCG.blocksRaycasts = false;
        if (!ShowLegacyInfoPanel) panelGO.SetActive(false);

        // 平行四辺形(19°)の統一様式パネル。街(明るい面もある)の上に乗るので、
        // 同じスプライトを暗く着色したものを 1 枚下に敷いて地を締める(文字の可読性)。
        Sprite panelSprite = UiButtonStyle.CreateHudPanelSprite((int)PanelW, (int)PanelH, ownedTextures, ownedSprites, "CityInfoPanel");
        // 1 枚では下の街が透ける(スプライトのフィル alpha が 0.60)ので 2 枚重ねる。
        for (int i = 0; i < 2; i++)
        {
            Image shade = NewImage("PanelShade" + i, panel, new Color(0.012f, 0.024f, 0.045f, 1f));
            Stretch(shade.rectTransform);
            shade.sprite = panelSprite;
        }
        Image bg = NewImage("PanelBg", panel, Color.white);
        Stretch(bg.rectTransform);
        bg.sprite = panelSprite;

        float innerW = PanelW - PanelInsetX * 2f;

        panelName = NewText("Name", panel, "", 34f, Color.white, TextAlignmentOptions.Left);
        RectTransform nr = (RectTransform)panelName.transform;
        nr.anchorMin = nr.anchorMax = new Vector2(0.5f, 1f);
        nr.pivot = new Vector2(0.5f, 1f);
        nr.sizeDelta = new Vector2(innerW, 44f);
        nr.anchoredPosition = new Vector2(0f, -26f);

        panelDesc = NewText("Desc", panel, "", 20f, new Color(0.78f, 0.86f, 0.92f, 1f), TextAlignmentOptions.TopLeft);
        RectTransform dr = (RectTransform)panelDesc.transform;
        dr.anchorMin = dr.anchorMax = new Vector2(0.5f, 1f);
        dr.pivot = new Vector2(0.5f, 1f);
        dr.sizeDelta = new Vector2(innerW, 54f);
        dr.anchoredPosition = new Vector2(0f, -74f);
        panelDesc.textWrappingMode = TextWrappingModes.Normal;

        panelMeta = NewText("Meta", panel, "", 20f, Cyan, TextAlignmentOptions.Left);
        RectTransform mr = (RectTransform)panelMeta.transform;
        mr.anchorMin = mr.anchorMax = new Vector2(0.5f, 0f);
        mr.pivot = new Vector2(0.5f, 0f);
        mr.sizeDelta = new Vector2(innerW, 28f);
        mr.anchoredPosition = new Vector2(0f, 22f);

        // プレビュー動画(区画へ寄り切ってからフェードインする)。
        GameObject prevGO = new GameObject("Preview", typeof(RectTransform), typeof(CanvasGroup));
        prevGO.transform.SetParent(panel, false);
        RectTransform pr = (RectTransform)prevGO.transform;
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0f);
        pr.pivot = new Vector2(0.5f, 0f);
        pr.sizeDelta = new Vector2(384f, 216f);
        pr.anchoredPosition = new Vector2(0f, 58f);
        previewCG = prevGO.GetComponent<CanvasGroup>();
        previewCG.alpha = 0f;
        previewCG.blocksRaycasts = false;

        previewFallback = NewImage("PreviewFallback", pr, new Color(0.02f, 0.05f, 0.09f, 1f));
        Stretch(previewFallback.rectTransform);

        previewRT = new RenderTexture(384, 216, 0) { name = "CityPreviewRT" };
        previewImage = new GameObject("PreviewVideo", typeof(RectTransform)).AddComponent<RawImage>();
        previewImage.transform.SetParent(pr, false);
        previewImage.texture = previewRT;
        previewImage.raycastTarget = false;
        Stretch(previewImage.rectTransform);

        Image[] rim = BuildRim(pr, new Color(0.75f, 0.82f, 0.88f, 0.42f), 2f);
        if (rim != null && rim.Length > 0) { /* 枠は生成しっぱなしで良い */ }

        GameObject vpGO = new GameObject("PreviewVideoPlayer");
        vpGO.transform.SetParent(pr, false);
        previewVideo = vpGO.AddComponent<VideoPlayer>();
        previewVideo.playOnAwake = false;
        previewVideo.source = VideoSource.Url;
        previewVideo.renderMode = VideoRenderMode.RenderTexture;
        previewVideo.targetTexture = previewRT;
        previewVideo.isLooping = true;
        previewVideo.aspectRatio = VideoAspectRatio.FitInside;
        previewVideo.waitForFirstFrame = true;
        previewVideo.audioOutputMode = VideoAudioOutputMode.None;
        previewVideo.skipOnDrop = true;
    }

    // ---- 公開 API ------------------------------------------------------------

    /// <summary>city スタイルの表示/非表示。街のカメラとライトもここで止める。</summary>
    public void SetVisible(bool visible)
    {
        if (root != null) root.gameObject.SetActive(visible);
        if (map != null) map.SetCityActive(visible);
        if (previewVideo != null)
        {
            if (!visible && previewVideo.isPlaying) previewVideo.Pause();
            else if (visible && !string.IsNullOrEmpty(previewVideo.url) && !previewVideo.isPlaying) previewVideo.Play();
        }
    }

    /// <summary>区画割当のある(選択できる)ステージを街へ伝える。</summary>
    public void SetAvailableDistricts(bool[] flags)
    {
        if (map != null) map.SetAvailableDistricts(flags);
    }

    /// <summary>選択中のステージ。district が 0 なら区画未割当。
    /// <paramref name="stageNumber"/> は StageDataBase の並び順(1 始まり)＝ STAGE 01〜。</summary>
    public void SetStage(StageData data, int districtNumber, bool animate, int stageNumber = 0)
    {
        district = districtNumber;
        if (map != null) map.SelectDistrict(districtNumber, animate);

        // ▼のラベルは日本語の表示名(艦長は StageData 側が "Captain" のままなので profile で補う)。
        string stageName = StageCityProfile.DisplayNameOf(data);
        if (markerLabel != null) markerLabel.text = stageName;
        if (markerLabelShadows != null)
            foreach (TMP_Text t in markerLabelShadows) if (t != null) t.text = stageName;
        markerInkCentered = false;

        // 説明は仮文(英語のプレースホルダ)なら空欄にする。
        string desc = StageCityProfile.DescriptionOf(data);
        string meta = "";
        if (data != null && data.audioClip != null)
        {
            int len = (int)data.audioClip.length;
            meta = string.Format("プレイ時間 {0}:{1:00}", len / 60, len % 60);
        }
        if (panelName != null) panelName.text = stageName;
        if (panelDesc != null) panelDesc.text = desc;
        if (panelMeta != null) panelMeta.text = meta;
        // 廃止した右下パネルの動画は回さない。
        if (ShowLegacyInfoPanel) UpdatePreviewClip(data);

        ApplyStagePanel(data, stageNumber);
    }

    // ---- 右パネルの中身 ------------------------------------------------------

    private void ApplyStagePanel(StageData data, int stageNumber)
    {
        if (stagePanelRect == null) return;

        currentStage = data;
        if (stageNumberText != null)
            stageNumberText.text = stageNumber >= 1 ? string.Format("STAGE {0:00}", stageNumber) : "STAGE";
        // 見出しは既存のステージ名(石工 / 放浪者 / 艦長 / 浮浪者)。
        // StageCityProfile.stageTitle(舞台名の仮置き)は使わない(2026-09-16 ユーザー判定)。
        if (stageNameRuby != null)
        {
            stageNameRuby.Apply(StageCityProfile.ReadingMarkupOf(data));
            rubiesPlaced = false;
        }

        // 曲の長さ。BGM クリップ長が取れないときは endTime で代用する。
        if (lengthValue != null) lengthValue.text = FormatLength(data);

        // 状態 = 難易度ごとの踏破状況(2026-09-16 U5 指示を v11 の菱形で)。
        ApplyStatusRow(data);
        ApplyDifficultyVisual();

        UpdateThumbnail(data);
    }

    // 難易度ごとの菱形を現在のステージの記録に合わせる。
    //   クリア済み      : 塗りの黄
    //   挑戦済み未クリア: 枠だけの黄
    //   未プレイ        : 枠だけの銀
    private void ApplyStatusRow(StageData data)
    {
        if (statusGems == null) return;
        string dir = data != null ? data.stageDirectoryName : null;
        for (int i = 0; i < statusGems.Length; i++)
        {
            Image gem = statusGems[i];
            if (gem == null) continue;
            SetGemState(gem, dir, i);
        }
    }

    private void SetGemState(Image gem, string dir, int difficulty)
    {
        bool cleared = StageDifficultyProgress.HasCleared(dir, difficulty);
        bool played = cleared || StageDifficultyProgress.HasPlayed(dir, difficulty);
        gem.sprite = cleared ? gemFilled : gemHollow;
        gem.color = cleared ? HighlandUi.Accent
            : played ? HighlandUi.Accent
            : new Color(HighlandUi.Silver.r, HighlandUi.Silver.g, HighlandUi.Silver.b, 0.75f);
    }

    /// <summary>
    /// 曲の長さ(m:ss)。実際に遊ぶ長さ = <c>endTime</c> を秒で切り捨てる
    /// (石工 147.0 → 2:27 / 放浪者 156.8 → 2:36。ユーザー提示の実値と一致する)。
    /// endTime を持たないステージ(mirror)だけ BGM クリップ長で代用する。
    /// </summary>
    private static string FormatLength(StageData data)
    {
        if (data == null) return "--:--";
        float sec = Mathf.Max(0f, data.endTime);
        if (sec <= 0.01f && data.audioClip != null) sec = data.audioClip.length;
        if (sec <= 0.01f) return "--:--";
        int total = Mathf.FloorToInt(sec);
        return string.Format("{0}:{1:00}", total / 60, total % 60);
    }

    /// <summary>ステージ CG のサムネ(Tools/Bullet Hell/Stage Select/Render CG Thumbnails の生成物)。</summary>
    private void UpdateThumbnail(StageData data)
    {
        string dir = data != null ? data.stageDirectoryName : null;
        if (dir == thumbDir) return;
        thumbDir = dir;

        if (thumbTexture != null) { Destroy(thumbTexture); thumbTexture = null; }
        string path = ThumbnailPath(dir);
        if (path != null)
        {
            byte[] bytes = File.ReadAllBytes(path);
            // mipmap 無し・Bilinear。パネルでは 1280x720 → 596x335 の縮小表示なので
            // Point 拡大(街やステージ CG のドット風)とは別扱いにする。
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (tex.LoadImage(bytes))
            {
                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;
                thumbTexture = tex;
            }
            else Destroy(tex);
        }
        if (thumbImage != null)
        {
            thumbImage.texture = thumbTexture;
            thumbImage.enabled = thumbTexture != null;
        }
    }

    // ---- 難易度一覧(パネル内差し替え。2026-09-19 U7) --------------------------

    /// <summary>右パネルの本文を難易度一覧へ差し替える(<c>false</c> で本文へ戻す)。</summary>
    public void SetDifficultyMode(bool on)
    {
        if (diffMode == on) return;
        diffMode = on;
        if (on && diffBodyCG != null) diffBodyCG.gameObject.SetActive(true);
        rubiesPlaced = false;        // 表に出た側のふりがなを置き直す
        ApplyDifficultyVisual();
    }

    public bool DifficultyMode => diffMode;
    public int DifficultyIndex => diffIndex;

    /// <summary>選択できる難易度。無効な行は暗く沈める。</summary>
    public void SetDifficultyEnabled(bool easy, bool normal, bool lunatic)
    {
        diffEnabled[0] = easy; diffEnabled[1] = normal; diffEnabled[2] = lunatic;
        ApplyDifficultyVisual();
    }

    public bool IsDifficultyEnabled(int index)
    {
        return index >= 0 && index < diffEnabled.Length && diffEnabled[index];
    }

    /// <summary>選択行を設定する(無効な行は飛ばさずそのまま置く。確定側で弾く)。</summary>
    public void SetDifficultyIndex(int index)
    {
        diffIndex = Mathf.Clamp(index, 0, DiffRowCy.Length - 1);
        ApplyDifficultyVisual();
    }

    /// <summary>上下で 1 段動かす(端で止まる。既存の DefficultyBar と同じ)。</summary>
    public void MoveDifficulty(int dir)
    {
        SetDifficultyIndex(diffIndex + (dir > 0 ? 1 : -1));
    }

    /// <summary>ベストスコアの出典(1P/2P)。</summary>
    public void SetTwoPlayer(bool on)
    {
        if (twoPlayerBest == on) return;
        twoPlayerBest = on;
        ApplyDifficultyVisual();
    }

    private void ApplyDifficultyVisual()
    {
        if (diffRowPlate == null) return;
        string dir = currentStage != null ? currentStage.stageDirectoryName : null;
        for (int i = 0; i < diffRowPlate.Length; i++)
        {
            bool sel = i == diffIndex;
            bool on = diffEnabled[i];
            if (diffRowSelected[i] != null) diffRowSelected[i].enabled = sel;
            if (diffRowPlate[i] != null) diffRowPlate[i].enabled = !sel;

            float dim = on ? 1f : 0.42f;
            if (diffRowAccent[i] != null)
            {
                Color c = DiffAccentColors[i];
                diffRowAccent[i].color = new Color(c.r, c.g, c.b, dim);
            }
            if (diffRowName[i] != null)
            {
                Color c = sel ? HighlandUi.Ink : HighlandUi.InkSoft;
                diffRowName[i].Body.color = new Color(c.r, c.g, c.b, dim);
                diffRowName[i].SetAlpha(dim);
            }
            if (diffRowGem[i] != null)
            {
                SetGemState(diffRowGem[i], dir, i);
                Color c = diffRowGem[i].color;
                diffRowGem[i].color = new Color(c.r, c.g, c.b, c.a * dim);
            }
            if (diffRowBestLabel[i] != null)
                diffRowBestLabel[i].color = new Color(HighlandUi.InkSoft.r, HighlandUi.InkSoft.g,
                    HighlandUi.InkSoft.b, dim * 0.9f);
            if (diffRowBestValue[i] != null)
            {
                diffRowBestValue[i].text = BestScoreText(dir, i);
                Color c = sel ? HighlandUi.Ink : HighlandUi.InkSoft;
                diffRowBestValue[i].color = new Color(c.r, c.g, c.b, dim);
            }
        }
    }

    // その難易度のベストスコア。記録が無ければ絵と同じ長音符を出す。
    private string BestScoreText(string dir, int difficulty)
    {
        if (string.IsNullOrEmpty(dir)) return "—";
        System.Collections.Generic.List<RankingStore.Entry> top =
            RankingStore.GetTop(dir, difficulty, twoPlayerBest ? "2P" : "1P", 1);
        return top != null && top.Count > 0 ? top[0].score.ToString("N0") : "—";
    }

    // 本文 ⇄ 難易度一覧のクロスフェード(0.25 秒)。
    private void TickDifficultyBlend(float dt)
    {
        if (infoBodyCG == null || diffBodyCG == null) return;
        float want = diffMode ? 1f : 0f;
        if (Mathf.Approximately(diffBlend, want))
        {
            if (!diffMode && diffBodyCG.gameObject.activeSelf && diffBlend <= 0f)
                diffBodyCG.gameObject.SetActive(false);
            return;
        }
        diffBlend = Mathf.MoveTowards(diffBlend, want, dt / 0.25f);
        infoBodyCG.alpha = 1f - diffBlend;
        diffBodyCG.alpha = diffBlend;
        infoBodyCG.gameObject.SetActive(diffBlend < 1f);
        if (diffBlend <= 0f && !diffMode) diffBodyCG.gameObject.SetActive(false);
    }

    /// <summary>CG サムネの場所。無ければ null。</summary>
    public static string ThumbnailPath(string stageDirectoryName)
    {
        if (string.IsNullOrEmpty(stageDirectoryName)) return null;
        string path = Path.Combine(Application.dataPath, "StageData", stageDirectoryName, "cg_thumb.png");
        return File.Exists(path) ? path : null;
    }

    private void UpdatePreviewClip(StageData data)
    {
        string path = VideoPath(data);
        if (previewVideo == null) return;
        if (path == null)
        {
            videoUrl = null;
            previewVideo.Stop();
            previewVideo.url = string.Empty;
            if (previewImage != null) previewImage.enabled = false;
            return;
        }
        if (videoUrl == path) return;
        videoUrl = path;
        previewVideo.Stop();
        previewVideo.url = path;
        if (previewImage != null) previewImage.enabled = true;
        previewVideo.Play();
    }

    private static string VideoPath(StageData data)
    {
        string dir = data != null ? data.stageDirectoryName : null;
        if (string.IsNullOrEmpty(dir)) return null;
        string path = Path.Combine(Application.dataPath, "StageData", dir, dir + ".mp4");
        return File.Exists(path) ? path : null;
    }

    /// <summary>選択画面へ入ったときの入場。タイトルの「地図へ寄る」の続きとして、
    /// 全景より引いた位置から選択中の区画まで止まらずに寄る(2026-09-15 指示。
    /// 旧: 全景で 0.55 秒静止してから 0.5 秒で区画へ動き出す)。</summary>
    public void PlayEntrance()
    {
        if (map == null) return;
        map.PlayEntranceSweep(district, EntranceSweepDuration);
    }

    // 入場スイープの尺(秒)。クロスフェード開始(決定から 0.05 秒)に始まり、
    // 決定から約 1.15 秒で区画に着く。
    private const float EntranceSweepDuration = 1.1f;

    /// <summary>タイトルへ戻るときの退場。入場スイープの逆で、区画から全景より
    /// 引いた位置まで戻す(2026-09-16 指示)。</summary>
    public void PlayExit()
    {
        if (map == null) return;
        map.PlayExitSweep(EntranceSweepDuration);
    }

    /// <summary>決定で区画へさらに寄る / 戻す。</summary>
    public void SetCloseUp(bool on)
    {
        if (map != null) map.SetCloseUp(on);
    }

    public void Tick(float dt)
    {
        if (map == null || !map.CityVisible) return;
        animTime += dt;
        map.Tick(dt);

        // ドット風表示を切り替えると街の描画先 RT ごと差し替わるので、表示板を追従させる。
        // RawImage.material は null を入れると既定マテリアルを返すので、
        // 最後に入れた値を覚えて変化したときだけ差し替える。
        if (cityView != null)
        {
            if (cityView.texture != map.Texture) cityView.texture = map.Texture;
            if (!ReferenceEquals(appliedViewMaterial, map.ViewMaterial))
            {
                appliedViewMaterial = map.ViewMaterial;
                cityView.material = appliedViewMaterial;
            }
        }

        // 和文ラベルの光学中央合わせは、メッシュ生成後(表示後の初回)でないと空振る。
        if (!markerInkCentered)
        {
            bool all = markerLabel == null || TmpAlign.CenterInkVertically(markerLabel);
            if (markerLabelShadows != null)
                foreach (TMP_Text t in markerLabelShadows) if (t != null) all &= TmpAlign.CenterInkVertically(t);
            markerInkCentered = all;
        }

        // ▼とラベルを区画の中心の上へ。カメラが動いても毎フレーム投影し直すので追従する。
        Rect canvas = root.rect;
        Vector2 vp = Vector2.zero;
        bool show = district >= 1 && map.TryGetMarkerViewport(district, out vp);
        // 決定で寄り切る直前に消す(難易度モーダルの上に▼を残さない)。
        float zoomFade = 1f - Mathf.Clamp01((map.ZoomAmount - 0.55f) / 0.45f);
        if (show)
        {
            // 浮遊は滑らかな正弦(第 U8 便でドット格子のステップ移動をやめた)。
            float floatY = Mathf.Sin(animTime * 1.9f) * MarkerFloatPx;
            markerRoot.anchoredPosition = new Vector2(
                (vp.x - 0.5f) * canvas.width,
                (vp.y - 0.5f) * canvas.height + floatY);
        }
        markerAlpha = Mathf.MoveTowards(markerAlpha, show ? 1f : 0f, dt * MarkerFadeSpeed);
        // 消えるときはフェードし切ってから落とす(即切りだと画面外へ出入りする瞬間に
        // ▼がパッと消えて見える)。
        bool alive = show || markerAlpha > 0.001f;
        if (markerRoot.gameObject.activeSelf != alive) markerRoot.gameObject.SetActive(alive);
        if (markerLabelCG != null) markerLabelCG.alpha = markerAlpha * zoomFade;
        if (markerArrow != null)
        {
            Color c = MarkerArrowInk;
            c.a = markerAlpha * zoomFade;
            markerArrow.color = c;
            // 第 U8 便でドット格子への吸着はやめた(滑らかな三角になったので不要)。
            markerArrow.rectTransform.anchoredPosition = Vector2.zero;
        }

        // 右パネル: 区画が選ばれているあいだ出す。難易度は同じパネルの中で本文と
        // 差し替わる(2026-09-19 U7)ので、寄り込みでは消さない。
        if (stagePanelCG != null)
        {
            float want = district >= 1
                ? 1f - Mathf.Clamp01((map.ZoomAmount - 0.55f) / 0.45f) : 0f;
            stagePanelCG.alpha = Mathf.MoveTowards(stagePanelCG.alpha, want, dt / 0.25f);
        }
        TickDifficultyBlend(dt);

        // ふりがなの漢字直上合わせは、メッシュ生成後(表示後の初回)でないと空振りする。
        if (!rubiesPlaced && stagePanelCG != null && stagePanelCG.alpha > 0.01f)
        {
            bool all = true;
            foreach (HighlandUi.RubyText r in rubies)
            {
                if (r == null || r.Body == null) continue;
                // 非表示のあいだは TMP の実測が空振りするので、表に出たときに置き直す。
                if (!r.Body.gameObject.activeInHierarchy) continue;
                all &= r.EnsurePlaced();
            }
            rubiesPlaced = all;
        }

        // 情報パネル: 区画へ寄り切ってからプレビューを出す。
        if (!ShowLegacyInfoPanel) return;
        float previewWant = map.Arrived && district >= 1 ? 1f : 0f;
        if (previewCG != null)
            previewCG.alpha = Mathf.MoveTowards(previewCG.alpha, previewWant, dt / 0.25f);
        if (panelCG != null)
            panelCG.alpha = Mathf.MoveTowards(panelCG.alpha, 1f - Mathf.Clamp01((map.ZoomAmount - 0.4f) / 0.6f), dt / 0.25f);
        if (previewVideo != null && previewCG != null && previewCG.alpha > 0f
            && !previewVideo.isPlaying && !string.IsNullOrEmpty(previewVideo.url))
        {
            previewVideo.Play();
        }
    }

    /// <summary>検証用。</summary>
    public string DebugState()
    {
        return string.Format("district={0} markerA={1:F2} preview={2:F2} | {3}",
            district, markerAlpha, previewCG != null ? previewCG.alpha : -1f,
            map != null ? map.DebugState() : "map=null");
    }

    // ---- 小物 ----------------------------------------------------------------

    private void StyleLabel(TMP_Text label)
    {
        if (label == null) return;
        TMP_FontAsset mincho = MinchoFont;
        if (mincho != null) label.font = mincho;
        label.characterSpacing = MarkerLabelSpacing;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
    }

    private TMP_FontAsset MinchoFont
    {
        get
        {
            if (!minchoTried)
            {
                minchoTried = true;
                minchoFont = Resources.Load<TMP_FontAsset>(MinchoFontResource);
                if (minchoFont == null)
                    Debug.LogWarning("CitySelectView: 明朝フォントが見つかりません: " + MinchoFontResource);
            }
            return minchoFont;
        }
    }

    private Image NewImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        RectTransform rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        return img;
    }

    private TMP_Text NewText(string name, Transform parent, string content, float size, Color color, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (uiFont != null) t.font = uiFont;
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        return t;
    }

    private Image[] BuildRim(RectTransform parent, Color color, float thickness)
    {
        Image[] rim = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            Image s = NewImage("Rim" + i, parent, color);
            RectTransform rt = s.rectTransform;
            switch (i)
            {
                case 0: rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f); break;
                case 1: rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f); rt.pivot = new Vector2(0.5f, 0f); break;
                case 2: rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 0.5f); break;
                default: rt.anchorMin = new Vector2(1f, 0f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(1f, 0.5f); break;
            }
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = i < 2 ? new Vector2(0f, thickness) : new Vector2(thickness, -2f * thickness);
            rim[i] = s;
        }
        return rim;
    }

    private static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    private void OnDestroy()
    {
        if (previewVideo != null) previewVideo.Stop();
        if (previewRT != null) { previewRT.Release(); Destroy(previewRT); }
        if (thumbTexture != null) { Destroy(thumbTexture); thumbTexture = null; }
        foreach (Texture2D t in ownedTextures) if (t != null) Destroy(t);
        foreach (Sprite s in ownedSprites) if (s != null) Destroy(s);
        if (map != null) Destroy(map.gameObject);
    }
}

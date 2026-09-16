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
    private const int MarkerArrowDotW = 12;
    private const int MarkerArrowDotH = 8;
    private const float MarkerArrowPixel = 2f;   // 第 15 便: 3→2px/ドット(タイトルと同じ)
    private const float MarkerArrowW = MarkerArrowDotW * MarkerArrowPixel;
    private const float MarkerArrowH = MarkerArrowDotH * MarkerArrowPixel;
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

    // ---- 右パネル(2026-09-16 U3 便で作り直し) --------------------------------
    // リザルト新様式「夜の紺と金」(Docs/result-design-language.md §10)に揃えた
    // 紺の半透明板。上から STAGE 番号 / 舞台名 / ステージ CG のサムネ / 情報行。
    // 旧: 既存 JSAB カード(プレビュー動画)+ ステージ名 + 説明文。
    private const float StagePanelW = 680f;
    private const float StagePanelH = 860f;
    private static readonly Vector2 StagePanelPos = new Vector2(496f, 10f);
    private const float ThumbW = 600f;
    private const float ThumbH = 338f;      // 16:9
    private const float ThumbCenterY = 6f;
    // STATUS 行の難易度マーク(EASY/NORMAL/LUNATIC)の並び。縦罫(x=44)より右、
    // LENGTH の値の右端(x≒248)とおおむね揃う位置に 3 つ置く。
    private const float DiffMarkX0 = 92f;
    private const float DiffMarkPitch = 76f;
    private const float DiffMarkFont = 13f;

    private CanvasGroup stagePanelCG;
    private RectTransform stagePanelRect;
    private TMP_Text stageNumberText;
    private TMP_Text stageTitleText;
    private RawImage thumbImage;
    private Image thumbFallback;
    private TMP_Text lengthValue;
    private Image statusIcon;
    // STATUS 行: 難易度ごとの菱形と英字(2026-09-16 U5)。index 0=EASY / 1=NORMAL / 2=LUNATIC。
    private Image[] diffMarks;
    private TMP_Text[] diffMarkLabels;
    private Sprite diffMarkFilled;
    private Sprite diffMarkHollow;
    private Texture2D thumbTexture;
    private string thumbDir;

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
        map.cityPrefab = Resources.Load<GameObject>("CityCG/CityMap_v5");
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

    /// <summary>
    /// 右パネル。リザルト新様式「夜の紺と金」(§10)と同じ紺の半透明板・金の罫線と菱形・
    /// 四隅の金ブラケット・白い明朝で、上から
    /// 1) STAGE 番号 2) ステージ名 3) ステージ CG のサムネ 4) 情報行(LENGTH / 難易度別 STATUS)。
    /// </summary>
    private void BuildStagePanel()
    {
        GameObject go = new GameObject("StagePanel", typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(root, false);
        stagePanelRect = (RectTransform)go.transform;
        stagePanelRect.anchorMin = stagePanelRect.anchorMax = new Vector2(0.5f, 0.5f);
        stagePanelRect.pivot = new Vector2(0.5f, 0.5f);
        stagePanelRect.anchoredPosition = StagePanelPos;
        stagePanelRect.sizeDelta = new Vector2(StagePanelW, StagePanelH);
        stagePanelCG = go.GetComponent<CanvasGroup>();
        stagePanelCG.blocksRaycasts = false;
        stagePanelCG.alpha = 0f;

        Image plate = NewImage("Plate", stagePanelRect, Color.white);
        Stretch(plate.rectTransform);
        plate.sprite = GoldPanelStyle.CreatePanelSprite((int)StagePanelW, (int)StagePanelH,
            ownedTextures, ownedSprites, "CityStagePanel");

        ruleSprite = GoldPanelStyle.CreateRuleSprite(ownedTextures, ownedSprites);
        diamondSprite = GoldPanelStyle.CreateDiamondSprite(ownedTextures, ownedSprites);

        // 1) STAGE 番号(小さい英字・字間広め・銀灰)。
        stageNumberText = NewText("StageNumber", stagePanelRect, "", 24f,
            GoldPanelStyle.SilverLabel, TextAlignmentOptions.Center);
        SetRect((RectTransform)stageNumberText.transform, new Vector2(0f, 352f), new Vector2(560f, 34f));
        StyleMincho(stageNumberText, 22f);

        // STAGE 番号の下の小さな菱形(リザルトの RESULT → 菱形 → 見出しと同じ運び)。
        AddDiamond(stagePanelRect, new Vector2(0f, 318f), 13f, GoldPanelStyle.GoldAccent);

        // 2) ステージ名(大きい明朝・白)。2026-09-16 ユーザー判定により既存の
        //    ステージ名(石工 / 放浪者 / 艦長 / 浮浪者)をそのまま出す(舞台名の仮置きは使わない)。
        stageTitleText = NewText("StageTitle", stagePanelRect, "", 58f,
            GoldPanelStyle.InkWhite, TextAlignmentOptions.Center);
        SetRect((RectTransform)stageTitleText.transform, new Vector2(0f, 262f), new Vector2(600f, 78f));
        StyleMincho(stageTitleText, 6f);
        GoldPanelStyle.ApplyTextGlow(stageTitleText,
            new Color(GoldPanelStyle.GoldAccent.r, GoldPanelStyle.GoldAccent.g, GoldPanelStyle.GoldAccent.b, 0.55f),
            0.055f, 0.5f);

        // 名前の下に飾り罫と琥珀の菱形。
        AddRule(stagePanelRect, new Vector2(0f, 206f), 520f, true);

        // 4) サムネ(角丸なし・細い枠。ステージ CG のレンダリング)。
        GameObject thumbGO = new GameObject("Thumb", typeof(RectTransform));
        thumbGO.transform.SetParent(stagePanelRect, false);
        RectTransform tr = (RectTransform)thumbGO.transform;
        tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f);
        tr.pivot = new Vector2(0.5f, 0.5f);
        tr.anchoredPosition = new Vector2(0f, ThumbCenterY);
        tr.sizeDelta = new Vector2(ThumbW, ThumbH);

        thumbFallback = NewImage("ThumbFallback", tr, new Color(0.020f, 0.035f, 0.070f, 1f));
        Stretch(thumbFallback.rectTransform);

        thumbImage = new GameObject("ThumbImage", typeof(RectTransform)).AddComponent<RawImage>();
        thumbImage.transform.SetParent(tr, false);
        thumbImage.raycastTarget = false;
        thumbImage.enabled = false;
        Stretch(thumbImage.rectTransform);

        Image thumbFrame = NewImage("ThumbFrame", tr, Color.white);
        Stretch(thumbFrame.rectTransform);
        thumbFrame.sprite = GoldPanelStyle.CreateThinFrameSprite(ownedTextures, ownedSprites);
        thumbFrame.type = Image.Type.Sliced;

        // 6) 情報行。AREA は不要(2026-09-16 ユーザー決定)、DIFFICULTY は曲の長さへ。
        //    STATUS は難易度ごとの踏破状況(2026-09-16 U5 指示)なので 2 段ぶんの高さを取る。
        AddRule(stagePanelRect, new Vector2(0f, -220f), 520f, false);
        lengthValue = AddInfoRow(stagePanelRect, -272f, "LENGTH",
            Resources.Load<Sprite>("UI/result_icon_time"), out _);
        BuildStatusRow(stagePanelRect, -344f);
    }

    // STATUS 行。ラベルの右に EASY / NORMAL / LUNATIC を小さく並べ、その上に菱形を置く。
    // 菱形: クリア済み=金の塗り / 挑戦済み未クリア=金の枠だけ / 未プレイ=暗い枠だけ。
    private void BuildStatusRow(RectTransform parent, float y)
    {
        AddInfoRow(parent, y, "STATUS", CreateFlagIconSprite(), out statusIcon).gameObject.SetActive(false);

        diffMarkFilled = CreateDiffMarkSprite(true);
        diffMarkHollow = CreateDiffMarkSprite(false);
        diffMarks = new Image[StageDifficultyProgress.DifficultyCount];
        diffMarkLabels = new TMP_Text[StageDifficultyProgress.DifficultyCount];

        string[] names = { "EASY", "NORMAL", "LUNATIC" };
        for (int i = 0; i < diffMarks.Length; i++)
        {
            float cx = DiffMarkX0 + DiffMarkPitch * i;

            Image mark = NewImage("DiffMark" + i, parent, GoldPanelStyle.GoldAccent);
            mark.sprite = diffMarkFilled;
            mark.type = Image.Type.Simple;
            mark.preserveAspect = true;
            SetRect(mark.rectTransform, new Vector2(cx, y + 14f), new Vector2(16f, 16f));
            diffMarks[i] = mark;

            TMP_Text lab = NewText("DiffName" + i, parent, names[i], DiffMarkFont,
                GoldPanelStyle.SilverLabel, TextAlignmentOptions.Center);
            SetRect((RectTransform)lab.transform, new Vector2(cx, y - 15f), new Vector2(DiffMarkPitch, 22f));
            StyleMincho(lab, 1f);
            diffMarkLabels[i] = lab;
        }
    }

    // 菱形のマーク。塗り(クリア済み)と枠だけ(未クリア)の 2 種を焼く。
    // 表示 16px に対し 48px で焼くので、枠線は縮小しても 1.5px ぶん残る。
    private Sprite CreateDiffMarkSprite(bool filled)
    {
        const int S = 48;
        Color32[] px = new Color32[S * S];
        Color32 white = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
        float c = (S - 1) * 0.5f;
        float ring = c * 0.92f;
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float m = (Mathf.Abs(x - c) + Mathf.Abs(y - c)) * 0.7071f;
                float cov = filled
                    ? Mathf.Clamp01(ring * 0.7071f - m + 0.5f)
                    : Mathf.Clamp01(1.6f - Mathf.Abs(m - ring * 0.7071f));
                GoldPanelStyle.Blend(px, S, S, x, y, white, cov);
            }
        }
        return GoldPanelStyle.MakeSprite(px, S, S,
            filled ? "CityDiffMarkFilled" : "CityDiffMarkHollow", ownedTextures, ownedSprites);
    }

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

    /// <summary>残り時間の最小表示。10 秒を切ったときだけ出す。</summary>
    public void SetRemainingTime(float seconds, bool cityMode)
    {
        if (timeLeftText == null) return;
        bool show = cityMode && seconds <= 10.5f && seconds > 0.05f;
        if (show)
        {
            string label = string.Format("のこり {0}", Mathf.CeilToInt(seconds));
            timeLeftText.text = label;
            if (timeLeftShadow != null) timeLeftShadow.text = label;
        }
        timeLeftAlpha = Mathf.MoveTowards(timeLeftAlpha, show ? 1f : 0f, Time.unscaledDeltaTime / 0.2f);
        timeLeftText.alpha = timeLeftAlpha;
        if (timeLeftShadow != null) timeLeftShadow.alpha = timeLeftAlpha * MarkerLabelShadow.a;
    }

    private void BuildMarker()
    {
        arrowSprite = TitleManager.CreatePixelDownTriangleSprite(MarkerArrowDotW, MarkerArrowDotH);

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

        if (stageNumberText != null)
            stageNumberText.text = stageNumber >= 1 ? string.Format("STAGE {0:00}", stageNumber) : "STAGE";
        // 見出しは既存のステージ名(石工 / 放浪者 / 艦長 / 浮浪者)。
        // StageCityProfile.stageTitle(舞台名の仮置き)は使わない(2026-09-16 ユーザー判定)。
        if (stageTitleText != null) stageTitleText.text = StageCityProfile.DisplayNameOf(data);

        // 曲の長さ。BGM クリップ長が取れないときは endTime で代用する。
        if (lengthValue != null) lengthValue.text = FormatLength(data);

        // STATUS = 難易度ごとの踏破状況(2026-09-16 U5 指示)。
        ApplyStatusRow(data);

        UpdateThumbnail(data);
    }

    // 難易度ごとの菱形を現在のステージの記録に合わせる。
    //   クリア済み  : 金の塗り菱形 + 明るい英字
    //   挑戦済み未クリア: 金の枠だけの菱形 + 中間の英字
    //   未プレイ    : 暗い枠だけの菱形 + 沈んだ英字
    private void ApplyStatusRow(StageData data)
    {
        if (diffMarks == null) return;
        string dir = data != null ? data.stageDirectoryName : null;
        bool any = false;
        for (int i = 0; i < diffMarks.Length; i++)
        {
            bool cleared = StageDifficultyProgress.HasCleared(dir, i);
            bool played = cleared || StageDifficultyProgress.HasPlayed(dir, i);
            any |= cleared;

            Image mark = diffMarks[i];
            if (mark != null)
            {
                mark.sprite = cleared ? diffMarkFilled : diffMarkHollow;
                Color c = cleared ? GoldPanelStyle.GoldAccent
                    : played ? GoldPanelStyle.GoldDim : GoldPanelStyle.SilverLabel;
                mark.color = new Color(c.r, c.g, c.b, cleared ? 1f : played ? 0.95f : 0.30f);
            }

            TMP_Text lab = diffMarkLabels[i];
            if (lab != null)
            {
                Color c = cleared ? GoldPanelStyle.GoldAccent : GoldPanelStyle.SilverLabel;
                lab.color = new Color(c.r, c.g, c.b, cleared ? 0.95f : played ? 0.72f : 0.34f);
            }
        }

        if (statusIcon != null)
        {
            Color c = any ? GoldPanelStyle.GoldAccent : GoldPanelStyle.SilverLabel;
            statusIcon.color = new Color(c.r, c.g, c.b, any ? 0.90f : 0.55f);
        }
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
        if (thumbFallback != null) thumbFallback.enabled = true;   // 枠の中の地は常に敷く
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
            // 浮遊は 2px 単位のステップ移動(ドットが滑らかに滑らないようにする)。
            float floatY = Mathf.Round(Mathf.Sin(animTime * 1.9f)
                * MarkerFloatPx / MarkerArrowPixel) * MarkerArrowPixel;
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
            // ▼だけ画面のドット格子(2px)へ吸着させる。ラベルは滑らかなまま。
            Vector2 mp = markerRoot.anchoredPosition;
            markerArrow.rectTransform.anchoredPosition = new Vector2(
                Mathf.Round(mp.x / MarkerArrowPixel) * MarkerArrowPixel - mp.x,
                Mathf.Round(mp.y / MarkerArrowPixel) * MarkerArrowPixel - mp.y);
        }

        // 右パネル: 区画が選ばれているあいだ出し、決定の寄り込み(難易度モーダル)で消す。
        // 入場スイープ中も出したままにする(パネルの中身は左右キーで即差し替わる)。
        if (stagePanelCG != null)
        {
            float want = district >= 1
                ? 1f - Mathf.Clamp01((map.ZoomAmount - 0.2f) / 0.5f) : 0f;
            stagePanelCG.alpha = Mathf.MoveTowards(stagePanelCG.alpha, want, dt / 0.25f);
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

    // ---- 右パネルの小物 ------------------------------------------------------

    private Sprite ruleSprite;
    private Sprite diamondSprite;

    // 明朝(ShipporiMincho)＋字間。リザルト新様式に合わせ、英字も明朝で字間を広げる。
    private void StyleMincho(TMP_Text text, float spacing)
    {
        if (text == null) return;
        TMP_FontAsset m = MinchoFont;
        if (m != null) text.font = m;
        text.characterSpacing = spacing;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
    }

    private static void SetRect(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    // 金の細い罫線 1 本(中央に琥珀の菱形を置くかどうか)。
    private void AddRule(RectTransform parent, Vector2 pos, float width, bool withDiamond)
    {
        Image rule = NewImage("Rule", parent, new Color(1f, 1f, 1f, 0.85f));
        rule.sprite = ruleSprite;
        rule.type = Image.Type.Simple;
        SetRect(rule.rectTransform, pos, new Vector2(width, 16f));
        if (withDiamond) AddDiamond(parent, pos, 16f, GoldPanelStyle.GoldAccent);
    }

    private void AddDiamond(RectTransform parent, Vector2 pos, float size, Color color)
    {
        Image d = NewImage("Diamond", parent, color);
        d.sprite = diamondSprite;
        d.type = Image.Type.Simple;
        SetRect(d.rectTransform, pos, new Vector2(size, size));
    }

    // 情報行(アイコン + ラベル英字 + 縦罫 + 値)。戻り値は値の TMP。
    private TMP_Text AddInfoRow(RectTransform parent, float y, string label, Sprite icon, out Image iconImage)
    {
        iconImage = null;
        if (icon != null)
        {
            iconImage = NewImage("Icon", parent, new Color(GoldPanelStyle.SilverLabel.r,
                GoldPanelStyle.SilverLabel.g, GoldPanelStyle.SilverLabel.b, 0.85f));
            iconImage.sprite = icon;
            iconImage.type = Image.Type.Simple;
            iconImage.preserveAspect = true;
            SetRect(iconImage.rectTransform, new Vector2(-228f, y), new Vector2(28f, 28f));
        }

        TMP_Text lab = NewText("Label", parent, label, 24f, GoldPanelStyle.SilverLabel, TextAlignmentOptions.Left);
        SetRect((RectTransform)lab.transform, new Vector2(-88f, y), new Vector2(216f, 32f));
        StyleMincho(lab, 12f);

        TMP_Text bar = NewText("Bar", parent, "|", 24f,
            new Color(GoldPanelStyle.GoldDim.r, GoldPanelStyle.GoldDim.g, GoldPanelStyle.GoldDim.b, 0.75f),
            TextAlignmentOptions.Center);
        SetRect((RectTransform)bar.transform, new Vector2(44f, y), new Vector2(20f, 32f));
        StyleMincho(bar, 0f);

        TMP_Text value = NewText("Value", parent, "", 28f, GoldPanelStyle.GoldAccent, TextAlignmentOptions.Right);
        SetRect((RectTransform)value.transform, new Vector2(128f, y), new Vector2(240f, 34f));
        StyleMincho(value, 4f);
        return value;
    }

    // STATUS 行の旗アイコン(踏破の目印)。既存の Resources/UI には無いのでここで焼く。
    private Sprite CreateFlagIconSprite()
    {
        const int S = 64;
        Color32[] px = new Color32[S * S];
        Color32 white = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
        // 旗竿。
        GoldPanelStyle.DrawLine(px, S, S, 18f, 6f, 18f, 58f, 4f, white);
        // 三角の旗(竿の上半分から右へ)。
        for (int y = 32; y <= 56; y++)
        {
            float t = (56 - y) / 24f;                 // 上で長く、下で短い
            float x1 = 20f + 26f * t;
            for (int x = 20; x <= (int)x1; x++)
                GoldPanelStyle.Blend(px, S, S, x, y, white, Mathf.Clamp01(x1 - x + 0.5f));
        }
        return GoldPanelStyle.MakeSprite(px, S, S, "CityStatusFlag", ownedTextures, ownedSprites);
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

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ステージ終了後に表示する、JSAB 風のリザルト画面。
// シーンを肥大化させないよう、既存の JsabStageSelect と同じく実行時に組み立てる。
public sealed class ResultScreen : MonoBehaviour
{
    public enum Action
    {
        Retry,
        StageSelect,
    }

    // Linear Color Space 上でモックアップの sRGB 色へ見えるよう調整した値。
    private static readonly Color Cyan = new Color(0.04f, 0.54f, 0.75f, 1f);       // visual #38C2E0
    private static readonly Color BrandBlue = new Color(0.055f, 0.28f, 0.708f, 1f); // visual #4290DB
    private static readonly Color DeepNavy = new Color(0.001f, 0.003f, 0.008f, 1f);
    private static readonly Color PanelNavy = new Color(0.003f, 0.012f, 0.03f, 1f);
    private static readonly Color DimText = new Color(0.29f, 0.48f, 0.64f, 0.9f);

    // モックアップ実測に合わせた枠線・装飾色（いずれも linear 値）。
    private static readonly Color OutlineTan = new Color(0.205f, 0.185f, 0.125f, 0.98f); // 細い暖色グレー枠 (#6b6656)
    private static readonly Color BracketWhite = new Color(0.85f, 0.90f, 1f, 1f);        // 四隅の明るい白ブラケット
    private static readonly Color AccentBlue = new Color(0.020f, 0.225f, 0.780f, 1f);    // 太い青アクセント (#1E7CE0)
    private static readonly Color IconBlue = new Color(0.030f, 0.285f, 0.750f, 1f);      // チップ内アイコン (#2E90E0)
    private static readonly Color DividerTan = new Color(0.100f, 0.093f, 0.066f, 0.9f);  // 区切り線本体
    private static readonly Color DividerBright = new Color(0.32f, 0.35f, 0.38f, 0.95f); // 区切り線のノード/両端

    // DefficultyBar / TitleMenu と同じ青系（同一値を同一 UI パイプラインへ通して
    // 見た目を一致させる）。ボタンを既存 UI と同じ様式にするために使う。
    private static readonly Color MenuBarBlue = new Color(0.055f, 0.525f, 0.91f, 1f);
    private static readonly Color MenuTextBase = new Color(0.85f, 0.93f, 1f, 1f);

    private enum StatIcon { Crosshair, Shield, Swords, Clock }

    private TMP_FontAsset font;
    private CanvasGroup contentGroup;
    private RectTransform contentRect;
    private TMP_Text stageNameText;
    private TMP_Text difficultyText;
    private TMP_Text verdictText;
    private TMP_Text rankText;
    private TMP_Text scoreText;
    private TMP_Text hitText;
    private TMP_Text counterText;
    private TMP_Text timeText;
    private readonly Image[] actionBodies = new Image[2];
    private readonly TMP_Text[] actionLabels = new TMP_Text[2];
    private readonly RectTransform[] actionRects = new RectTransform[2];
    private readonly List<Texture2D> generatedTextures = new List<Texture2D>();
    private readonly List<Sprite> generatedSprites = new List<Sprite>();
    private Sprite cardFillSprite;
    private Sprite cardSpriteLeft;
    private Sprite cardSpriteRight;
    private Sprite hexChipSprite;
    private Sprite diamondRingSprite;
    private Sprite flareSprite;
    private Sprite bannerSprite;
    private Sprite slashSprite;
    private readonly Sprite[] statIconSprites = new Sprite[4];
    private SoftCircleGraphic rankAuraOuter;
    private SoftCircleGraphic rankAuraInner;
    private Image rankFlareBlue;
    private Image rankFlareCyan;
    private readonly Image[] actionSlashL = new Image[2];
    private readonly Image[] actionSlashR = new Image[2];
    private int selectedAction;
    private bool inputArmed;
    private bool entering;

    public event System.Action<Action> ActionRequested;

    public bool Visible => gameObject.activeSelf;

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
        cardFillSprite = CreateOctagonSprite("ResultCardOct", 44f);
        cardSpriteLeft = CreateHexCardSprite(true);
        cardSpriteRight = CreateHexCardSprite(false);
        hexChipSprite = CreateHexChipSprite();
        diamondRingSprite = CreateDiamondRingSprite();
        flareSprite = CreateFlareSprite();
        bannerSprite = CreateBannerSprite();
        slashSprite = CreateSlashSprite();
        statIconSprites[(int)StatIcon.Crosshair] = CreateStatIconSprite(StatIcon.Crosshair);
        statIconSprites[(int)StatIcon.Shield] = CreateStatIconSprite(StatIcon.Shield);
        statIconSprites[(int)StatIcon.Swords] = CreateStatIconSprite(StatIcon.Swords);
        statIconSprites[(int)StatIcon.Clock] = CreateStatIconSprite(StatIcon.Clock);

        Image background = NewImage("Background", root, DeepNavy);
        Stretch(background.rectTransform);
        BuildBackgroundDecor(root);

        GameObject content = NewRect("Content", root);
        contentRect = (RectTransform)content.transform;
        Stretch(contentRect);
        contentGroup = content.AddComponent<CanvasGroup>();

        BuildHeader(contentRect);
        BuildStageHeading(contentRect);
        BuildEvaluation(contentRect);
        BuildStats(contentRect);
        BuildActions(contentRect);
    }

    private void BuildBackgroundDecor(RectTransform root)
    {
        SoftCircleGraphic glow = NewGraphic<SoftCircleGraphic>("CentralGlow", root);
        glow.color = new Color(0.015f, 0.12f, 0.32f, 0.07f);
        SetRect(glow.rectTransform, Vector2.zero, new Vector2(900f, 900f));

        GameObject shapes = NewRect("BackgroundGeometry", root);
        RectTransform shapesRect = (RectTransform)shapes.transform;
        Stretch(shapesRect);
        Vector2[] positions =
        {
            new Vector2(-790f, 280f), new Vector2(780f, 260f),
            new Vector2(-820f, -320f), new Vector2(810f, -300f),
            new Vector2(-310f, -430f), new Vector2(350f, 410f),
        };
        float[] sizes = { 250f, 320f, 390f, 290f, 170f, 130f };
        for (int i = 0; i < positions.Length; i++)
        {
            Image diamond = NewImage("DriftingDiamond", shapesRect,
                new Color(0.008f, 0.10f, 0.32f, i < 4 ? 0.022f : 0.032f));
            SetRect(diamond.rectTransform, positions[i], Vector2.one * sizes[i]);
            diamond.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }
        shapes.AddComponent<ShapeDrifter>();

        // 画面端の縦線群。モックアップの建築シルエットを、プロジェクトの
        // 幾何学デザイン言語に合わせた抽象的なスカイラインへ置き換える。
        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < 7; i++)
            {
                float height = 210f + i * 55f;
                Image tower = NewImage("EdgeSpire", root,
                    new Color(0.004f, 0.07f, 0.24f, 0.055f));
                RectTransform tr = tower.rectTransform;
                tr.anchorMin = tr.anchorMax = new Vector2(side < 0 ? 0f : 1f, 0f);
                tr.pivot = new Vector2(side < 0 ? 0f : 1f, 0f);
                tr.anchoredPosition = new Vector2(side * (18f + i * 26f), 0f);
                tr.sizeDelta = new Vector2(5f, height);
                tr.localRotation = Quaternion.Euler(0f, 0f, side * (i % 2 == 0 ? -5f : 4f));
            }
        }
    }

    private void BuildHeader(RectTransform root)
    {
        // 底のシアン区切り線（全幅）と、その線からわずかに立ち上る青の微光。
        // モックアップは「ソリッド青バナー無し・線から昇る薄いにじみ」なので、
        // 帯ではなく線の直上に低アルファの細いグローだけを敷く（oracle 指摘）。
        Image underglow = NewImage("HeaderUnderglow", root,
            new Color(BrandBlue.r, BrandBlue.g, BrandBlue.b, 0.05f));
        RectTransform ug = underglow.rectTransform;
        ug.anchorMin = new Vector2(0f, 1f);
        ug.anchorMax = new Vector2(1f, 1f);
        ug.pivot = new Vector2(0.5f, 1f);
        ug.anchoredPosition = new Vector2(0f, -76f);
        ug.sizeDelta = new Vector2(0f, 22f);

        Image line = NewImage("HeaderLine", root, new Color(Cyan.r, Cyan.g, Cyan.b, 0.75f));
        RectTransform lineRect = line.rectTransform;
        lineRect.anchorMin = new Vector2(0f, 1f);
        lineRect.anchorMax = new Vector2(1f, 1f);
        lineRect.pivot = new Vector2(0.5f, 1f);
        lineRect.anchoredPosition = new Vector2(0f, -88f);
        lineRect.sizeDelta = new Vector2(0f, 2f);

        Image slashA = NewImage("HeaderSlashA", root, Color.white);
        SetTopLeftSlash(slashA.rectTransform, new Vector2(22f, -44f), 4f, 66f);
        Image slashB = NewImage("HeaderSlashB", root, new Color(Cyan.r, Cyan.g, Cyan.b, 1f));
        SetTopLeftSlash(slashB.rectTransform, new Vector2(35f, -44f), 4f, 66f);

        // 装飾的な十字アイコン（モックアップ準拠。先端に菱形フィニアル）。
        BuildCrossIcon(root, new Vector2(80f, -38f));

        TMP_Text title = NewText("HeaderTitle", root, "結果  /  RESULT", 40f, Color.white,
            TextAlignmentOptions.MidlineLeft);
        RectTransform titleRect = (RectTransform)title.transform;
        titleRect.anchorMin = titleRect.anchorMax = new Vector2(0f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.anchoredPosition = new Vector2(118f, -13f);
        titleRect.sizeDelta = new Vector2(520f, 64f);

        Image rightSlash = NewImage("HeaderRightSlash", root, Color.white);
        RectTransform rs = rightSlash.rectTransform;
        rs.anchorMin = rs.anchorMax = new Vector2(1f, 1f);
        rs.pivot = new Vector2(0.5f, 0.5f);
        rs.anchoredPosition = new Vector2(-28f, -45f);
        rs.sizeDelta = new Vector2(5f, 70f);
        rs.localRotation = Quaternion.Euler(0f, 0f, -42f);
    }

    // 装飾十字（ラテン十字・横木は上寄り）。縦木/横木＋4先端の菱形フィニアル＋
    // 中央のシアン宝石。pivot は横木と縦木の交点。
    private void BuildCrossIcon(RectTransform root, Vector2 pivot)
    {
        Color silver = new Color(0.92f, 0.95f, 1f, 1f);
        Color silverDim = new Color(0.66f, 0.74f, 0.88f, 1f);
        const float up = 22f, down = 34f, halfW = 20f, thick = 6.5f, dia = 12f;
        AddQuad(root, "CrossV", silver, pivot + new Vector2(0f, (up - down) * 0.5f),
            new Vector2(thick, up + down), 0f);
        AddQuad(root, "CrossH", silver, pivot, new Vector2(halfW * 2f, thick), 0f);
        AddQuad(root, "CrossTip", silverDim, pivot + new Vector2(0f, up), new Vector2(dia, dia), 45f);
        AddQuad(root, "CrossTip", silverDim, pivot + new Vector2(0f, -down), new Vector2(dia, dia), 45f);
        AddQuad(root, "CrossTip", silverDim, pivot + new Vector2(-halfW, 0f), new Vector2(dia, dia), 45f);
        AddQuad(root, "CrossTip", silverDim, pivot + new Vector2(halfW, 0f), new Vector2(dia, dia), 45f);
        AddQuad(root, "CrossGem", new Color(Cyan.r, Cyan.g, Cyan.b, 0.95f), pivot,
            new Vector2(8.5f, 8.5f), 45f);
    }

    private void BuildStageHeading(RectTransform root)
    {
        stageNameText = NewText("StageName", root, "STAGE", 30f, Color.white,
            TextAlignmentOptions.Center);
        SetRect((RectTransform)stageNameText.transform, new Vector2(0f, 413f), new Vector2(820f, 50f));

        difficultyText = NewText("Difficulty", root, "LUNATIC", 17f, Cyan,
            TextAlignmentOptions.Center);
        SetRect((RectTransform)difficultyText.transform, new Vector2(0f, 378f), new Vector2(240f, 32f));

        Image dashL = NewImage("StageDashL", root, new Color(Cyan.r, Cyan.g, Cyan.b, 0.55f));
        SetRect(dashL.rectTransform, new Vector2(-185f, 392f), new Vector2(150f, 2f));
        Image dashR = NewImage("StageDashR", root, new Color(Cyan.r, Cyan.g, Cyan.b, 0.55f));
        SetRect(dashR.rectTransform, new Vector2(185f, 392f), new Vector2(150f, 2f));
    }

    private void BuildEvaluation(RectTransform root)
    {
        // ランク文字を包む青系のソフトグロー（外=青 / 内=シアンで層にする）。
        // モックアップの紫グローに替えてユーザー指定の青フレアにする。
        rankAuraOuter = NewGraphic<SoftCircleGraphic>("RankAuraOuter", root);
        rankAuraOuter.color = new Color(BrandBlue.r, BrandBlue.g, BrandBlue.b, 0.09f);
        SetRect(rankAuraOuter.rectTransform, new Vector2(0f, 30f), new Vector2(400f, 400f));

        rankAuraInner = NewGraphic<SoftCircleGraphic>("RankAuraInner", root);
        rankAuraInner.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.13f);
        SetRect(rankAuraInner.rectTransform, new Vector2(0f, 30f), new Vector2(250f, 250f));

        // 中央判定を囲む多重の菱形ライン（細いリングを層にして、外ほど暗く沈め、
        // 内ほどシアンで明るくする。紫はユーザー指定の青系テーマに合わせて廃止）。
        Vector2 rankCenter = new Vector2(0f, 38f);
        float[] ringSizes = { 600f, 516f, 452f, 404f, 360f };
        Color[] ringColors =
        {
            new Color(0.070f, 0.085f, 0.120f, 0.20f),  // 最外・暗い鋼で沈める
            new Color(0.050f, 0.130f, 0.280f, 0.26f),
            new Color(0.050f, 0.165f, 0.345f, 0.32f),
            new Color(Cyan.r, Cyan.g, Cyan.b, 0.28f),
            new Color(Cyan.r, Cyan.g, Cyan.b, 0.42f),  // 内・シアンで明るく
        };
        for (int i = 0; i < ringSizes.Length; i++)
        {
            Image ring = NewImage("RankRing", root, ringColors[i]);
            ring.sprite = diamondRingSprite;
            ring.type = Image.Type.Simple;
            SetRect(ring.rectTransform, rankCenter, Vector2.one * ringSizes[i]);
        }

        // S の背後を薄く沈める菱形（不透明にすると重いので低アルファで浮かせる）。
        Image backdrop = NewImage("RankBackdrop", root, new Color(0.003f, 0.010f, 0.030f, 0.42f));
        backdrop.sprite = cardFillSprite;
        backdrop.type = Image.Type.Sliced;
        SetRect(backdrop.rectTransform, rankCenter, Vector2.one * 250f);
        backdrop.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

        // 評価エリア外周の白ブラケット（横キャップ＋斜めカット＋シアン差し色）。
        const float bx = 345f, by = 225f, arm = 44f, thick = 3.4f;
        Color evalCyan = new Color(Cyan.r, Cyan.g, Cyan.b, 0.95f);
        for (int sxi = -1; sxi <= 1; sxi += 2)
        {
            for (int syi = -1; syi <= 1; syi += 2)
            {
                Vector2 corner = rankCenter + new Vector2(sxi * bx, syi * by);
                float diagRot = (sxi * syi > 0f) ? 45f : -45f;
                AddQuad(root, "EvalBracketH", BracketWhite,
                    corner + new Vector2(-sxi * arm * 0.5f, 0f), new Vector2(arm, thick), 0f);
                AddQuad(root, "EvalBracketDiag", BracketWhite,
                    corner + new Vector2(-sxi * 14f, -syi * 14f), new Vector2(44f, thick), diagRot);
                AddQuad(root, "EvalBracketDot", evalCyan,
                    corner + new Vector2(-sxi * 26f, -syi * 26f), new Vector2(7f, 7f), 45f);
            }
        }

        // 上下中央のノードマーカー（中空菱形＋小さな芯。モックの◇準拠）と、
        // 左右のシェブロン（oracle 指摘で 22px 内側へ寄せる）。
        Color nodeRing = new Color(0.52f, 0.58f, 0.70f, 0.8f);
        Color nodeCore = new Color(0.72f, 0.80f, 0.92f, 0.95f);
        for (int s = -1; s <= 1; s += 2)
        {
            Vector2 np = rankCenter + new Vector2(0f, s * 252f);
            Image nring = NewImage("RankNodeRing", root, nodeRing);
            nring.sprite = diamondRingSprite;
            nring.type = Image.Type.Simple;
            SetRect(nring.rectTransform, np, new Vector2(22f, 22f));
            AddQuad(root, "RankNodeCore", nodeCore, np, new Vector2(6f, 6f), 45f);
        }
        AddChevron(root, -1f, rankCenter + new Vector2(-278f, 0f));
        AddChevron(root, 1f, rankCenter + new Vector2(278f, 0f));

        // 交点付近の tech 装飾（左=マゼンタ、右=青の小菱形）。
        Color techMagenta = new Color(0.55f, 0.10f, 0.60f, 0.5f);
        Color techBlue = new Color(0.10f, 0.35f, 0.85f, 0.5f);
        Vector2[] techPos =
        {
            new Vector2(-215f, 58f), new Vector2(-238f, 18f), new Vector2(-200f, -28f),
            new Vector2(215f, 58f), new Vector2(238f, 18f), new Vector2(200f, -28f),
        };
        for (int i = 0; i < techPos.Length; i++)
        {
            Color tc = techPos[i].x < 0f ? techMagenta : techBlue;
            float sz = (i % 3 == 1) ? 9f : 6f;
            AddQuad(root, "TechDot", tc, rankCenter + techPos[i], new Vector2(sz, sz), 45f);
        }

        verdictText = NewText("Verdict", root, "総合判定\n<size=18><color=#38C2E0>OVERALL EVALUATION</color></size>",
            32f, Color.white, TextAlignmentOptions.Center);
        SetRect((RectTransform)verdictText.transform, new Vector2(0f, 258f), new Vector2(500f, 90f));

        // ランク文字の背後に青系フレア（8方向の光条＋コア）。グレースケールの
        // スターバーストを大=青・小=シアンで二重に敷き、シアン→青のグラデにする。
        Vector2 flareCenter = new Vector2(0f, 26f);
        rankFlareBlue = NewImage("RankFlareBlue", root, new Color(BrandBlue.r, BrandBlue.g, BrandBlue.b, 0.8f));
        rankFlareBlue.sprite = flareSprite;
        rankFlareBlue.type = Image.Type.Simple;
        SetRect(rankFlareBlue.rectTransform, flareCenter, new Vector2(700f, 700f));

        rankFlareCyan = NewImage("RankFlareCyan", root, new Color(Cyan.r, Cyan.g, Cyan.b, 0.78f));
        rankFlareCyan.sprite = flareSprite;
        rankFlareCyan.type = Image.Type.Simple;
        SetRect(rankFlareCyan.rectTransform, flareCenter, new Vector2(430f, 430f));

        rankText = NewText("Rank", root, "S", 360f, Color.white, TextAlignmentOptions.Center);
        rankText.fontStyle = FontStyles.Bold;
        rankText.enableAutoSizing = true;
        rankText.fontSizeMin = 210f;
        rankText.fontSizeMax = 384f;
        rankText.outlineColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.74f);
        rankText.outlineWidth = 0.09f;
        SetRect((RectTransform)rankText.transform, new Vector2(0f, 26f), new Vector2(460f, 410f));
        // モックの端正なセリフに寄せる近似: 高さを保ったまま横幅を約8%細くする
        // （プロジェクトにセリフ TMP 資産が無いため字形はスケールで近似）。oracle 指摘。
        rankText.rectTransform.localScale = new Vector3(0.92f, 1f, 1f);
    }

    private void BuildStats(RectTransform root)
    {
        scoreText = BuildStatCard(root, "Score", new Vector2(-610f, 150f), "スコア", "SCORE", "000,000", StatIcon.Crosshair);
        hitText = BuildStatCard(root, "Hit", new Vector2(610f, 150f), "被弾回数", "HIT COUNT", "00", StatIcon.Shield);
        counterText = BuildStatCard(root, "Counter", new Vector2(-610f, -115f), "カウンター回数", "COUNTER COUNT", "00", StatIcon.Swords);
        timeText = BuildStatCard(root, "Time", new Vector2(610f, -115f), "時間", "TIME", "00:00", StatIcon.Clock);
    }

    private TMP_Text BuildStatCard(RectTransform root, string name, Vector2 pos,
        string jp, string en, string value, StatIcon icon)
    {
        GameObject card = NewRect(name + "Card", root);
        RectTransform rect = (RectTransform)card.transform;
        Vector2 size = new Vector2(470f, 205f);
        SetRect(rect, pos, size);

        // 非対称八角形（外側=画面端の面取りが大きく、内側=中央寄りが小さい）を
        // 塗り・細枠・内側下辺の青アクセント・外側2隅の白ブラケットまで一体で
        // 焼き込んだ専用スプライト。左右でミラー。
        Image bg = NewImage("CardBg", rect, Color.white);
        bg.sprite = pos.x < 0f ? cardSpriteLeft : cardSpriteRight;
        bg.type = Image.Type.Simple;
        Stretch(bg.rectTransform);

        // 左の六角アイコンチップ＋図形アイコン（大きい外側面取りを避けて配置）。
        Image chip = NewImage("Chip", rect, Color.white);
        chip.sprite = hexChipSprite;
        chip.type = Image.Type.Simple;
        SetRect(chip.rectTransform, new Vector2(-133f, 38f), new Vector2(86f, 96f));

        Image iconImg = NewImage("Icon", rect, Color.white);
        iconImg.sprite = statIconSprites[(int)icon];
        iconImg.type = Image.Type.Simple;
        SetRect(iconImg.rectTransform, new Vector2(-133f, 38f), new Vector2(52f, 52f));

        TMP_Text label = NewText("Label", rect,
            jp + "\n<size=15><color=#38C2E0>" + en + "</color></size>",
            27f, Color.white, TextAlignmentOptions.MidlineLeft);
        SetRect((RectTransform)label.transform, new Vector2(58f, 40f), new Vector2(290f, 76f));

        // 見出し下の細い区切り線（中央ノード＋両端ターミナル付き）。
        BuildDivider(rect, new Vector2(0f, 3f), 360f);

        TMP_Text valueText = NewText("Value", rect, value, 48f, Color.white, TextAlignmentOptions.Center);
        valueText.characterSpacing = 4f;
        SetRect((RectTransform)valueText.transform, new Vector2(0f, -48f), new Vector2(390f, 70f));
        return valueText;
    }

    private void BuildDivider(RectTransform card, Vector2 pos, float width)
    {
        Image line = NewImage("Rule", card, DividerTan);
        SetRect(line.rectTransform, pos, new Vector2(width, 1.6f));
        AddQuad(card, "RuleNode", DividerBright, pos, new Vector2(8f, 8f), 45f);
        AddQuad(card, "RuleEndL", DividerBright, new Vector2(pos.x - width * 0.5f, pos.y), new Vector2(5f, 5f), 45f);
        AddQuad(card, "RuleEndR", DividerBright, new Vector2(pos.x + width * 0.5f, pos.y), new Vector2(5f, 5f), 45f);
    }

    private static Image AddQuad(RectTransform parent, string name, Color color,
        Vector2 pos, Vector2 size, float rotDeg)
    {
        Image img = NewImage(name, parent, color);
        RectTransform r = img.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta = size;
        r.localRotation = Quaternion.Euler(0f, 0f, rotDeg);
        return img;
    }

    private void BuildActions(RectTransform root)
    {
        string[] labels = { "もう一度", "ステージ選択へ" };
        for (int i = 0; i < labels.Length; i++)
        {
            GameObject buttonGo = NewRect("Action" + i, root);
            RectTransform rect = (RectTransform)buttonGo.transform;
            SetRect(rect, new Vector2(i == 0 ? -270f : 270f, -405f), new Vector2(470f, 92f));
            actionRects[i] = rect;

            // 斜めバナー本体（DefficultyBar の SimpleBar 準拠の平行四辺形・青ティント）。
            Image body = NewImage("Body", rect, MenuBarBlue);
            body.sprite = bannerSprite;
            body.type = Image.Type.Simple;
            Stretch(body.rectTransform);
            body.raycastTarget = true;
            actionBodies[i] = body;

            // 上辺の薄いハイライト（バナーのグロス感）。
            Image gloss = NewImage("Gloss", rect, new Color(1f, 1f, 1f, 0.12f));
            RectTransform gl = gloss.rectTransform;
            gl.anchorMin = new Vector2(0f, 1f);
            gl.anchorMax = new Vector2(1f, 1f);
            gl.pivot = new Vector2(0.5f, 1f);
            gl.anchoredPosition = new Vector2(0f, -12f);
            gl.sizeDelta = new Vector2(-104f, 14f);

            TMP_Text label = NewText("Label", rect, labels[i], 34f, MenuTextBase, TextAlignmentOptions.Center);
            Stretch((RectTransform)label.transform);
            actionLabels[i] = label;

            // 選択マーカーの白スラッシュ（StageBar_White 準拠）を左右端に。選択時のみ点灯。
            Image slashL = NewImage("SlashL", rect, Color.white);
            slashL.sprite = slashSprite;
            slashL.type = Image.Type.Simple;
            SetRect(slashL.rectTransform, new Vector2(-238f, 0f), new Vector2(72f, 112f));
            actionSlashL[i] = slashL;
            Image slashR = NewImage("SlashR", rect, Color.white);
            slashR.sprite = slashSprite;
            slashR.type = Image.Type.Simple;
            SetRect(slashR.rectTransform, new Vector2(238f, 0f), new Vector2(72f, 112f));
            actionSlashR[i] = slashR;

            int captured = i;
            Button button = buttonGo.AddComponent<Button>();
            button.targetGraphic = body;
            button.transition = Selectable.Transition.None;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            button.onClick.AddListener(() => RequestAction(captured));

            EventTrigger trigger = buttonGo.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerEnter, _ => Select(captured));
            AddTrigger(trigger, EventTriggerType.PointerDown, _ => Select(captured));
        }

        TMP_Text guide = NewText("InputGuide", root,
            "← → / A D  選択     SPACE  決定     ESC  戻る",
            16f, DimText, TextAlignmentOptions.Center);
        SetRect((RectTransform)guide.transform, new Vector2(0f, -475f), new Vector2(850f, 34f));
    }

    public void Prepare(StageData stage, int difficulty, bool cleared, int hitCount,
        int counterCount, float elapsedSeconds, float endSeconds)
    {
        gameObject.SetActive(true);
        selectedAction = 0;
        inputArmed = false;
        entering = false;

        string stageName = stage != null && !string.IsNullOrWhiteSpace(stage.stageName)
            ? stage.stageName
            : "UNKNOWN STAGE";
        stageNameText.text = stageName;
        difficultyText.text = DifficultyName(difficulty);
        verdictText.text = cleared
            ? "総合判定\n<size=16><color=#38C2E0>OVERALL EVALUATION</color></size>"
            : "攻略失敗\n<size=16><color=#FF6C8B>STAGE FAILED</color></size>";

        string rank = EvaluateRank(cleared, hitCount);
        rankText.text = rank;
        rankText.color = cleared ? Color.white : new Color(1f, 0.52f, 0.65f, 1f);
        rankText.outlineColor = cleared
            ? new Color(Cyan.r, Cyan.g, Cyan.b, 0.74f)
            : new Color(0.85f, 0.08f, 0.22f, 0.78f);

        // クリア＝青系フレア、失敗＝赤系に切り替える（グレースケールのフレア
        // スプライトをティントで着色）。
        Color flareBlue = cleared ? new Color(BrandBlue.r, BrandBlue.g, BrandBlue.b, 0.8f)
                                  : new Color(0.72f, 0.12f, 0.20f, 0.78f);
        Color flareCyan = cleared ? new Color(Cyan.r, Cyan.g, Cyan.b, 0.78f)
                                  : new Color(0.95f, 0.30f, 0.38f, 0.72f);
        if (rankFlareBlue != null) rankFlareBlue.color = flareBlue;
        if (rankFlareCyan != null) rankFlareCyan.color = flareCyan;
        if (rankAuraOuter != null)
            rankAuraOuter.color = cleared ? new Color(BrandBlue.r, BrandBlue.g, BrandBlue.b, 0.09f)
                                          : new Color(0.6f, 0.08f, 0.14f, 0.10f);
        if (rankAuraInner != null)
            rankAuraInner.color = cleared ? new Color(Cyan.r, Cyan.g, Cyan.b, 0.13f)
                                          : new Color(0.9f, 0.2f, 0.28f, 0.13f);

        // TODO: 専用のスコア加算系が実装されたら、その確定値へ差し替える。
        // 現状はクリア状況・被弾・カウンター・到達率という実データから暫定算出する。
        int provisionalScore = CalculateProvisionalScore(
            cleared, hitCount, counterCount, elapsedSeconds, endSeconds);
        scoreText.text = provisionalScore.ToString("N0");
        hitText.text = Mathf.Max(0, hitCount).ToString("00");
        counterText.text = Mathf.Max(0, counterCount).ToString("00");
        timeText.text = FormatTime(elapsedSeconds);

        contentGroup.alpha = 0f;
        contentRect.anchoredPosition = new Vector2(0f, -18f);
        contentRect.localScale = Vector3.one * 0.975f;
        RefreshActionVisuals();
    }

    public async void PlayEntrance()
    {
        if (!gameObject.activeSelf || entering) return;
        entering = true;
        const float duration = 0.34f;
        float elapsed = 0f;
        while (elapsed < duration && this != null && gameObject.activeSelf)
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            float p = Mathf.Clamp01(elapsed / duration);
            float ease = 1f - Mathf.Pow(1f - p, 3f);
            contentGroup.alpha = ease;
            contentRect.anchoredPosition = Vector2.Lerp(new Vector2(0f, -18f), Vector2.zero, ease);
            contentRect.localScale = Vector3.one * Mathf.Lerp(0.975f, 1f, ease);
            await Task.Yield();
        }

        if (this == null) return;
        contentGroup.alpha = 1f;
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.localScale = Vector3.one;
        entering = false;
    }

    public void Tick(bool left, bool right, bool buttonHeld, bool buttonPressed, bool backPressed)
    {
        if (!gameObject.activeSelf || entering) return;

        if (backPressed)
        {
            RequestAction((int)Action.StageSelect);
            return;
        }

        if (!inputArmed)
        {
            if (!buttonHeld) inputArmed = true;
            return;
        }

        if (left) Select(selectedAction - 1);
        else if (right) Select(selectedAction + 1);
        else if (buttonPressed) RequestAction(selectedAction);
    }

    public void HideImmediate()
    {
        entering = false;
        gameObject.SetActive(false);
    }

    public static int CalculateProvisionalScore(bool cleared, int hitCount, int counterCount,
        float elapsedSeconds, float endSeconds)
    {
        float progress = endSeconds > 0.001f
            ? Mathf.Clamp01(elapsedSeconds / endSeconds)
            : (cleared ? 1f : 0f);
        int score = Mathf.RoundToInt(progress * 700000f);
        if (cleared) score += 200000;
        score += Mathf.Max(0, counterCount) * 7500;
        score -= Mathf.Max(0, hitCount) * 40000;
        return Mathf.Clamp(score, 0, 999999);
    }

    public static string EvaluateRank(bool cleared, int hitCount)
    {
        if (!cleared) return "F";
        if (hitCount <= 0) return "S";
        if (hitCount <= 2) return "A";
        if (hitCount <= 5) return "B";
        return "C";
    }

    private void Select(int index)
    {
        selectedAction = (index % actionRects.Length + actionRects.Length) % actionRects.Length;
        RefreshActionVisuals();
    }

    private void RefreshActionVisuals()
    {
        for (int i = 0; i < actionRects.Length; i++)
        {
            if (actionRects[i] == null || actionBodies[i] == null || actionLabels[i] == null)
            {
                Transform action = contentRect != null ? contentRect.Find("Action" + i) : null;
                if (action == null) continue;
                actionRects[i] = action as RectTransform;
                actionBodies[i] = action.Find("Body")?.GetComponent<Image>();
                actionLabels[i] = action.Find("Label")?.GetComponent<TMP_Text>();
                actionSlashL[i] = action.Find("SlashL")?.GetComponent<Image>();
                actionSlashR[i] = action.Find("SlashR")?.GetComponent<Image>();
                if (actionRects[i] == null || actionBodies[i] == null || actionLabels[i] == null) continue;
            }

            // DefficultyBar の選択挙動を踏襲: 選択でバナーを明るく・少し拡大し、白
            // スラッシュを点灯。非選択はバナーを沈め（alpha↓＝背景が透けて減光）・
            // 文字を控えめにし、スラッシュを消す。
            bool selected = i == selectedAction;
            Color bar = MenuBarBlue;
            bar.a = selected ? 1f : 0.5f;
            actionBodies[i].color = bar;
            actionLabels[i].color = selected
                ? Color.white
                : new Color(MenuTextBase.r, MenuTextBase.g, MenuTextBase.b, 0.72f);
            float slashA = selected ? 1f : 0f;
            if (actionSlashL[i] != null) actionSlashL[i].color = new Color(1f, 1f, 1f, slashA);
            if (actionSlashR[i] != null) actionSlashR[i].color = new Color(1f, 1f, 1f, slashA);
            actionRects[i].localScale = Vector3.one * (selected ? 1.03f : 0.965f);
        }
    }

    private void RequestAction(int index)
    {
        if (!gameObject.activeSelf) return;
        Select(index);
        ActionRequested?.Invoke((Action)selectedAction);
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

    private static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return string.Format("{0:00}:{1:00}", total / 60, total % 60);
    }

    private void AddChevron(RectTransform root, float sign, Vector2 center)
    {
        const float ax = 12f, ay = 18f;
        Vector2 point = center + new Vector2(sign * ax, 0f);
        AddChevronArm(root, point, center + new Vector2(-sign * ax, ay));
        AddChevronArm(root, point, center + new Vector2(-sign * ax, -ay));
    }

    private void AddChevronArm(RectTransform root, Vector2 a, Vector2 b)
    {
        Vector2 mid = (a + b) * 0.5f;
        float len = Vector2.Distance(a, b);
        float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
        AddQuad(root, "Chevron", AccentBlue, mid, new Vector2(len, 4f), ang);
    }

    // 面取り八角形の 9-slice fill（大きめの面取りで、カード・中央菱形の土台に使う）。
    private Sprite CreateOctagonSprite(string spriteName, float chamfer)
    {
        const int size = 256;
        const int border = 60;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = spriteName + "Texture";
        texture.filterMode = FilterMode.Bilinear;
        Color32[] pixels = new Color32[size * size];
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
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        generatedTextures.Add(texture);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
            new Vector4(border, border, border, border));
        sprite.name = spriteName;
        generatedSprites.Add(sprite);
        return sprite;
    }

    // カード背景（非対称八角形）を塗り・細枠・青アクセント・白ブラケットまで
    // 一体で焼き込んだ固定サイズスプライト。outerLeft=true で大面取りが左側。
    private Sprite CreateHexCardSprite(bool outerLeft)
    {
        const int S = 2;                 // 2x で焼いて縮小 AA を稼ぐ
        const int refW = 470, refH = 205;
        int TW = refW * S, TH = refH * S;
        // 面取り脚長（ref 単位）: 外側大・内側小。
        float cOT = 60f, cOB = 60f, cIT = 24f, cIB = 44f;
        float hw = refW * 0.5f * S, hh = refH * 0.5f * S;
        Vector2[] v = HexCardVerts(outerLeft, hw, hh, cOT * S, cOB * S, cIT * S, cIB * S);

        Texture2D texture = new Texture2D(TW, TH, TextureFormat.RGBA32, false);
        texture.name = "ResultHexCard_" + (outerLeft ? "L" : "R");
        texture.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[TW * TH];
        Color fillCol = new Color(0.016f, 0.032f, 0.068f, 0.95f);
        Color innerGold = new Color(0.095f, 0.082f, 0.052f, 1f); // 内側の鈍い金の細線
        Color innerGlow = new Color(0.085f, 0.20f, 0.42f);       // 内側の微かな明色（青）
        float cx = (TW - 1) * 0.5f, cy = (TH - 1) * 0.5f;
        float outlineHalf = 1.6f * S;
        float innerOffset = 10f * S;
        for (int y = 0; y < TH; y++)
        {
            for (int x = 0; x < TW; x++)
            {
                Vector2 p = new Vector2(x - cx, y - cy);
                float sdf = ConvexSdf(v, p);         // +外側 / -内側（tex px）
                float inside = Mathf.Clamp01(0.5f - sdf);
                Blend(px, TW, TH, x, y, fillCol, inside * fillCol.a);
                // カード内側の微かな明色グロー（上中央が最も明るく、外へ減衰）。
                float gx = (x - cx) / hw;
                float gy = (y - cy) / hh;
                float gd = Mathf.Sqrt(gx * gx * 0.6f + (gy - 0.28f) * (gy - 0.28f));
                float glow = Mathf.Clamp01(1f - gd);
                Blend(px, TW, TH, x, y, innerGlow, inside * glow * glow * 0.5f);
                Blend(px, TW, TH, x, y, OutlineTan, Mathf.Clamp01(outlineHalf - Mathf.Abs(sdf) + 0.5f));
                Blend(px, TW, TH, x, y, innerGold, Mathf.Clamp01(1.0f - Mathf.Abs(sdf + innerOffset)) * 0.7f);
            }
        }

        // 内側下辺の面取りに沿った太い青アクセント（少し内側へ詰めて外周の枠を残す）。
        Vector2 a0, a1;
        if (outerLeft) { a0 = v[3]; a1 = v[4]; }   // 右下面取り
        else { a0 = v[5]; a1 = v[6]; }             // 左下面取り
        Vector2 am = (a0 + a1) * 0.5f;
        a0 = Vector2.Lerp(a0, am, 0.07f);
        a1 = Vector2.Lerp(a1, am, 0.07f);
        DrawLine(px, TW, TH, a0.x + cx, a0.y + cy, a1.x + cx, a1.y + cy, 12f * S, AccentBlue);

        // 外側 2 隅の明るい白ブラケット（面取り＋隣接辺の短いキャップ）。
        if (outerLeft)
        {
            DrawBracket(px, TW, TH, cx, cy, v[7], v[0], v[6], v[1], S); // 左上
            DrawBracket(px, TW, TH, cx, cy, v[5], v[6], v[4], v[7], S); // 左下
        }
        else
        {
            DrawBracket(px, TW, TH, cx, cy, v[1], v[2], v[0], v[3], S); // 右上
            DrawBracket(px, TW, TH, cx, cy, v[3], v[4], v[2], v[5], S); // 右下
        }

        texture.SetPixels32(px);
        texture.Apply();
        generatedTextures.Add(texture);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, TW, TH),
            new Vector2(0.5f, 0.5f), 100f * S);
        sprite.name = texture.name;
        generatedSprites.Add(sprite);
        return sprite;
    }

    // 8 頂点（中央原点・CW・tex px）。outerLeft で大面取りを左へ。
    private static Vector2[] HexCardVerts(bool outerLeft, float hw, float hh,
        float cOT, float cOB, float cIT, float cIB)
    {
        if (outerLeft)
        {
            return new[]
            {
                new Vector2(-hw + cOT, hh),   // 0 上辺左
                new Vector2( hw - cIT, hh),   // 1 上辺右
                new Vector2( hw, hh - cIT),   // 2 右辺上
                new Vector2( hw, -hh + cIB),  // 3 右辺下
                new Vector2( hw - cIB, -hh),  // 4 下辺右
                new Vector2(-hw + cOB, -hh),  // 5 下辺左
                new Vector2(-hw, -hh + cOB),  // 6 左辺下
                new Vector2(-hw, hh - cOT),   // 7 左辺上
            };
        }
        return new[]
        {
            new Vector2(-hw + cIT, hh),   // 0 上辺左
            new Vector2( hw - cOT, hh),   // 1 上辺右
            new Vector2( hw, hh - cOT),   // 2 右辺上
            new Vector2( hw, -hh + cOB),  // 3 右辺下
            new Vector2( hw - cOB, -hh),  // 4 下辺右
            new Vector2(-hw + cIB, -hh),  // 5 下辺左
            new Vector2(-hw, -hh + cIB),  // 6 左辺下
            new Vector2(-hw, hh - cIT),   // 7 左辺上
        };
    }

    // 凸多角形の符号付き距離（+外側）。原点は内部にある前提。
    private static float ConvexSdf(Vector2[] v, Vector2 p)
    {
        float d = -1e9f;
        int n = v.Length;
        for (int i = 0; i < n; i++)
        {
            Vector2 a = v[i];
            Vector2 b = v[(i + 1) % n];
            Vector2 e = b - a;
            Vector2 nrm = new Vector2(e.y, -e.x);
            if (Vector2.Dot(nrm, (a + b) * 0.5f) < 0f) nrm = -nrm;
            nrm.Normalize();
            d = Mathf.Max(d, Vector2.Dot(nrm, p - a));
        }
        return d;
    }

    // 白ブラケット: 面取り辺(a->b)＋a,b から隣接頂点方向への短いキャップ。
    private static void DrawBracket(Color32[] px, int w, int h, float cx, float cy,
        Vector2 a, Vector2 b, Vector2 aToward, Vector2 bToward, int scale)
    {
        float width = 3.4f * scale;
        float cap = 17f * scale;
        Vector2 ac = a + Vector2.ClampMagnitude(aToward - a, cap);
        Vector2 bc = b + Vector2.ClampMagnitude(bToward - b, cap);
        DrawLine(px, w, h, a.x + cx, a.y + cy, b.x + cx, b.y + cy, width, BracketWhite);
        DrawLine(px, w, h, a.x + cx, a.y + cy, ac.x + cx, ac.y + cy, width, BracketWhite);
        DrawLine(px, w, h, b.x + cx, b.y + cy, bc.x + cx, bc.y + cy, width, BracketWhite);
    }

    // 縦長六角形（pointy-top）のアイコンチップ。淡い青塗り＋細い外周。
    private Sprite CreateHexChipSprite()
    {
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "ResultHexChipTexture";
        texture.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[size * size];
        float half = (size - 1) * 0.5f;
        float radius = half - 7f;
        const float k = 0.8660254f;   // sqrt(3)/2 -> 縦辺の半幅
        const float s3 = 1.7320508f;  // sqrt(3)
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float ax = Mathf.Abs(x - half);
                float ay = Mathf.Abs(y - half);
                float dSide = ax - radius * k;
                float dDiag = (ax + s3 * ay - s3 * radius) * 0.5f;
                float d = Mathf.Max(dSide, dDiag);
                float fill = Mathf.Clamp01(-d + 0.5f);
                float ty = Mathf.Clamp01((y - half) / radius * 0.5f + 0.5f);
                Color fillColor = Color.Lerp(new Color(0.010f, 0.045f, 0.115f),
                    new Color(0.030f, 0.110f, 0.240f), ty);
                Blend(px, size, size, x, y, fillColor, fill * 0.62f);
                float ring = Mathf.Clamp01(1.5f - Mathf.Abs(d));
                Blend(px, size, size, x, y, new Color(0.050f, 0.430f, 0.860f), ring * 0.75f);
            }
        }
        texture.SetPixels32(px);
        texture.Apply();
        generatedTextures.Add(texture);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f), 100f);
        sprite.name = "ResultHexChip";
        generatedSprites.Add(sprite);
        return sprite;
    }

    // 細い菱形の輪郭リング（中央の多重ラインに使う。頂点はテクスチャ辺の中点）。
    private Sprite CreateDiamondRingSprite()
    {
        const int size = 512;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "ResultDiamondRingTexture";
        texture.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[size * size];
        float half = (size - 1) * 0.5f;
        float edge = half - 4f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float ax = Mathf.Abs(x - half);
                float ay = Mathf.Abs(y - half);
                float dist = Mathf.Abs((ax + ay) - edge) * 0.7071f;
                float a = Mathf.Clamp01(1.2f - dist);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        texture.SetPixels32(px);
        texture.Apply();
        generatedTextures.Add(texture);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f), 100f);
        sprite.name = "ResultDiamondRing";
        generatedSprites.Add(sprite);
        return sprite;
    }

    // ランク文字の背後に敷く 8 方向スターバースト（グレースケール）。中心のソフト
    // コア＋主光条(水平/垂直・長)＋斜め光条(短)。色は Image ティントで与える。
    private Sprite CreateFlareSprite()
    {
        const int size = 512;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "ResultFlareTexture";
        texture.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[size * size];
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - c) / c;   // -1..1
                float dy = (y - c) / c;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                // コア（中央の拡散光）は控えめにし、光条を主役にする。拡散が
                // 強いと中央が青いフォグになり細い菱形装飾が埋まる（oracle 指摘）。
                float core = Mathf.Exp(-r * 5.6f) * 0.5f;
                float h = RayGlow(dx, dy, 0.045f, 1.0f);
                float v = RayGlow(dy, dx, 0.045f, 1.0f);
                float u1 = (dx + dy) * 0.70711f, w1 = (dx - dy) * 0.70711f;
                float u2 = (dx - dy) * 0.70711f, w2 = (dx + dy) * 0.70711f;
                float d1 = RayGlow(u1, w1, 0.028f, 0.58f);
                float d2 = RayGlow(u2, w2, 0.028f, 0.58f);
                float rays = Mathf.Max(Mathf.Max(h, v), Mathf.Max(d1, d2) * 0.72f);
                float a = Mathf.Clamp01(core + rays);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        texture.SetPixels32(px);
        texture.Apply();
        generatedTextures.Add(texture);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f), 100f);
        sprite.name = "ResultFlare";
        generatedSprites.Add(sprite);
        return sprite;
    }

    // 光条1本の強度。u=光条方向 / v=直交方向。中央で最大、両端でテーパー。
    private static float RayGlow(float u, float v, float halfThick, float len)
    {
        float across = Mathf.Clamp01(1f - Mathf.Abs(v) / halfThick);
        across *= across;
        float along = Mathf.Clamp01(1f - Mathf.Abs(u) / len);
        along *= along * along;
        return across * along;
    }

    // DefficultyBar の SimpleBar 準拠: 斜めの平行四辺形バナー（白＋縦グラデ）。
    // 青ティントで既存 UI と同じ斜めバナーになる。Simple 描画でボタン矩形へ伸縮。
    private Sprite CreateBannerSprite()
    {
        const int W = 512, H = 100;
        const float lean = 34f;   // 約20°(縦から)。上へ行くほど右へずれる "/" 傾き。
        Texture2D texture = new Texture2D(W, H, TextureFormat.RGBA32, false);
        texture.name = "ResultBannerTexture";
        texture.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[W * H];
        for (int y = 0; y < H; y++)
        {
            float ty = y / (float)(H - 1);         // 0 下端 .. 1 上端
            float leftEdge = lean * ty;
            float rightEdge = (W - 1) - lean + lean * ty;
            float shade = Mathf.Lerp(0.60f, 0.98f, ty);   // 下=暗 / 上=明
            if (ty < 0.10f) shade = Mathf.Lerp(0.42f, shade, ty / 0.10f); // 下辺の締め
            shade = Mathf.Clamp01(shade);
            for (int x = 0; x < W; x++)
            {
                float cov = Mathf.Clamp01(Mathf.Min(x - leftEdge, rightEdge - x) + 0.5f);
                px[y * W + x] = cov <= 0f
                    ? new Color(0f, 0f, 0f, 0f)
                    : new Color(shade, shade, shade, cov);
            }
        }
        texture.SetPixels32(px);
        texture.Apply();
        generatedTextures.Add(texture);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, W, H), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = "ResultBanner";
        generatedSprites.Add(sprite);
        return sprite;
    }

    // DefficultyBar の StageBar_White 準拠: 白い斜めスラッシュ "/"（縦長平行四辺形）。
    // 選択中ボタンの左右端に据えるブラケット。
    private Sprite CreateSlashSprite()
    {
        const int W = 96, H = 126;
        const float lean = 46f;   // バナーと同じ約20°傾き。
        Texture2D texture = new Texture2D(W, H, TextureFormat.RGBA32, false);
        texture.name = "ResultSlashTexture";
        texture.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[W * H];
        for (int y = 0; y < H; y++)
        {
            float ty = y / (float)(H - 1);
            float leftEdge = lean * ty;
            float rightEdge = (W - 1) - lean + lean * ty;
            for (int x = 0; x < W; x++)
            {
                float cov = Mathf.Clamp01(Mathf.Min(x - leftEdge, rightEdge - x) + 0.5f);
                px[y * W + x] = cov <= 0f
                    ? new Color(0f, 0f, 0f, 0f)
                    : new Color(1f, 1f, 1f, cov);
            }
        }
        texture.SetPixels32(px);
        texture.Apply();
        generatedTextures.Add(texture);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, W, H), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = "ResultSlash";
        generatedSprites.Add(sprite);
        return sprite;
    }

    // カード種別ごとの簡易ラインアイコン（クロスヘア/シールド/双剣/時計）。
    private Sprite CreateStatIconSprite(StatIcon kind)
    {
        const int size = 96;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "ResultStatIcon_" + kind;
        texture.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[size * size];
        float c = (size - 1) * 0.5f;
        Color col = IconBlue;
        switch (kind)
        {
            case StatIcon.Crosshair:
                DrawRing(px, size, size, c, c, 22f, 2.6f, col);
                DrawLine(px, size, size, c, c + 15f, c, c + 31f, 2.0f, col);
                DrawLine(px, size, size, c, c - 15f, c, c - 31f, 2.0f, col);
                DrawLine(px, size, size, c + 15f, c, c + 31f, c, 2.0f, col);
                DrawLine(px, size, size, c - 15f, c, c - 31f, c, 2.0f, col);
                DrawDisc(px, size, size, c, c, 3f, col);
                break;
            case StatIcon.Clock:
                DrawRing(px, size, size, c, c, 24f, 2.6f, col);
                DrawLine(px, size, size, c, c, c, c + 17f, 2.4f, col);
                DrawLine(px, size, size, c, c, c + 12f, c + 8f, 2.4f, col);
                DrawDisc(px, size, size, c, c, 3f, col);
                break;
            case StatIcon.Swords:
                DrawLine(px, size, size, c - 20f, c - 22f, c + 22f, c + 24f, 2.8f, col);
                DrawLine(px, size, size, c + 20f, c - 22f, c - 22f, c + 24f, 2.8f, col);
                DrawLine(px, size, size, c - 27f, c - 12f, c - 11f, c - 25f, 2.2f, col);
                DrawLine(px, size, size, c + 27f, c - 12f, c + 11f, c - 25f, 2.2f, col);
                DrawDisc(px, size, size, c - 21f, c - 24f, 2.6f, col);
                DrawDisc(px, size, size, c + 21f, c - 24f, 2.6f, col);
                break;
            case StatIcon.Shield:
                Vector2[] v =
                {
                    new Vector2(c - 19f, c + 24f), new Vector2(c + 19f, c + 24f),
                    new Vector2(c + 19f, c + 2f), new Vector2(c, c - 26f),
                    new Vector2(c - 19f, c + 2f),
                };
                for (int i = 0; i < v.Length; i++)
                {
                    Vector2 p = v[i];
                    Vector2 q = v[(i + 1) % v.Length];
                    DrawLine(px, size, size, p.x, p.y, q.x, q.y, 2.6f, col);
                }
                DrawRing(px, size, size, c, c + 4f, 7f, 2.0f, col);
                DrawDisc(px, size, size, c, c + 4f, 2.0f, col);
                break;
        }
        texture.SetPixels32(px);
        texture.Apply();
        generatedTextures.Add(texture);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f), 100f);
        sprite.name = "ResultStatIcon_" + kind;
        generatedSprites.Add(sprite);
        return sprite;
    }

    // --- 手続き描画の最小プリミティブ（Color32 バッファへ AA 付きでブレンド）---

    private static void Blend(Color32[] buf, int w, int h, int x, int y, Color c, float cov)
    {
        if (x < 0 || x >= w || y < 0 || y >= h) return;
        cov = Mathf.Clamp01(cov);
        if (cov <= 0f) return;
        int i = y * w + x;
        Color32 d = buf[i];
        float da = d.a / 255f;
        float outA = cov + da * (1f - cov);
        if (outA <= 0.0001f) { buf[i] = new Color32(0, 0, 0, 0); return; }
        float r = (c.r * cov + (d.r / 255f) * da * (1f - cov)) / outA;
        float g = (c.g * cov + (d.g / 255f) * da * (1f - cov)) / outA;
        float b = (c.b * cov + (d.b / 255f) * da * (1f - cov)) / outA;
        buf[i] = new Color(r, g, b, outA);
    }

    private static void DrawLine(Color32[] buf, int w, int h,
        float x0, float y0, float x1, float y1, float width, Color col)
    {
        float ht = width * 0.5f;
        int minx = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(x0, x1) - ht - 1f));
        int maxx = Mathf.Min(w - 1, Mathf.CeilToInt(Mathf.Max(x0, x1) + ht + 1f));
        int miny = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(y0, y1) - ht - 1f));
        int maxy = Mathf.Min(h - 1, Mathf.CeilToInt(Mathf.Max(y0, y1) + ht + 1f));
        float dx = x1 - x0, dy = y1 - y0;
        float len2 = dx * dx + dy * dy;
        for (int y = miny; y <= maxy; y++)
        {
            for (int x = minx; x <= maxx; x++)
            {
                float t = len2 > 1e-4f ? Mathf.Clamp01(((x - x0) * dx + (y - y0) * dy) / len2) : 0f;
                float px = x0 + t * dx, py = y0 + t * dy;
                float dist = Mathf.Sqrt((x - px) * (x - px) + (y - py) * (y - py));
                Blend(buf, w, h, x, y, col, ht - dist + 0.5f);
            }
        }
    }

    private static void DrawRing(Color32[] buf, int w, int h,
        float cx, float cy, float radius, float width, Color col)
    {
        float ht = width * 0.5f;
        int minx = Mathf.Max(0, Mathf.FloorToInt(cx - radius - ht - 1f));
        int maxx = Mathf.Min(w - 1, Mathf.CeilToInt(cx + radius + ht + 1f));
        int miny = Mathf.Max(0, Mathf.FloorToInt(cy - radius - ht - 1f));
        int maxy = Mathf.Min(h - 1, Mathf.CeilToInt(cy + radius + ht + 1f));
        for (int y = miny; y <= maxy; y++)
        {
            for (int x = minx; x <= maxx; x++)
            {
                float dist = Mathf.Abs(Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - radius);
                Blend(buf, w, h, x, y, col, ht - dist + 0.5f);
            }
        }
    }

    private static void DrawDisc(Color32[] buf, int w, int h,
        float cx, float cy, float radius, Color col)
    {
        int minx = Mathf.Max(0, Mathf.FloorToInt(cx - radius - 1f));
        int maxx = Mathf.Min(w - 1, Mathf.CeilToInt(cx + radius + 1f));
        int miny = Mathf.Max(0, Mathf.FloorToInt(cy - radius - 1f));
        int maxy = Mathf.Min(h - 1, Mathf.CeilToInt(cy + radius + 1f));
        for (int y = miny; y <= maxy; y++)
        {
            for (int x = minx; x <= maxx; x++)
            {
                float dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                Blend(buf, w, h, x, y, col, radius - dist + 0.5f);
            }
        }
    }

    private TMP_Text NewText(string name, Transform parent, string value, float size,
        Color color, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
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

    private static void SetTopLeftSlash(RectTransform rect, Vector2 pos, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(width, height);
        rect.localRotation = Quaternion.Euler(0f, 0f, -42f);
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

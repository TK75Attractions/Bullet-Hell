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

    // 頂点色（Image.color / AddQuad）用の linear 値。表示時に sRGB エンコードされる。
    private static readonly Color BracketWhite = new Color(0.85f, 0.90f, 1f, 1f);        // 明るい白（枠・ブラケット）
    private static readonly Color AccentBlue = new Color(0.020f, 0.225f, 0.780f, 1f);    // 鮮やかな青（シェブロン等）
    private static readonly Color DividerTan = new Color(0.046f, 0.040f, 0.043f, 0.9f);  // 区切り線本体（モック実測 #3C3739）
    private static readonly Color DividerBright = new Color(0.32f, 0.35f, 0.38f, 0.95f); // 区切り線のノード/両端

    // 焼き込みテクスチャ用の視覚（sRGB）値。生成 Texture2D は sRGB サンプル→linear→
    // 表示 sRGB エンコードで書いた値がそのまま画面に出るため、頂点色と違い
    // pre-linear 化せず「モックアップで実測した見た目の値」をそのまま書く。
    // （v7 まで頂点用の linear 値を流用して枠線が実測 (52,47,32) と暗すぎた反省）
    private static readonly Color TexOutlineTan = new Color(0.357f, 0.318f, 0.290f, 0.98f); // カード枠（実測 (91,81,74)）
    private static readonly Color TexInnerGold = new Color(0.235f, 0.204f, 0.133f, 1f);     // 内側の鈍い金線
    private static readonly Color TexFillNavy = new Color(0.010f, 0.024f, 0.060f, 0.95f);   // カード塗り（実測 (0,5,14)）
    private static readonly Color TexInnerGlow = new Color(0.05f, 0.13f, 0.30f);            // カード内側の微光
    private static readonly Color TexAccentBlue = new Color(0.12f, 0.43f, 0.90f, 1f);       // 青アクセント帯
    private static readonly Color TexBracketWhite = new Color(0.85f, 0.90f, 1f, 1f);        // 白ブラケット
    private static readonly Color TexIconBlue = new Color(0.16f, 0.51f, 0.94f, 1f);         // チップ内アイコン

    private enum StatIcon { Crosshair, Shield, Swords, Clock }

    private TMP_FontAsset font;
    private TMP_FontAsset rankFont; // ランク文字専用のセリフ体（Playfair Display）
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
    private readonly List<Texture2D> generatedTextures = new List<Texture2D>();
    private readonly List<Sprite> generatedSprites = new List<Sprite>();
    private Sprite cardFillSprite;
    private Sprite cardSpriteLeft;
    private Sprite cardSpriteRight;
    private Sprite hexChipSprite;
    private Sprite diamondRingSprite;
    private Sprite flareSprite;
    private Sprite exitButtonSprite;
    private readonly Sprite[] statIconSprites = new Sprite[4];
    private SoftCircleGraphic rankAuraOuter;
    private SoftCircleGraphic rankAuraInner;
    private Image rankFlareBlue;
    private Image rankFlareCyan;
    private RectTransform exitRect;
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
        // ランク文字用のセリフ体（Playfair Display Medium・SIL OFL）。
        // 資産が無い環境では既存フォントへフォールバックする。
        rankFont = Resources.Load<TMP_FontAsset>("Fonts/PlayfairDisplay-Medium SDF");

        cardFillSprite = CreateOctagonSprite("ResultCardOct", 44f);
        cardSpriteLeft = CreateHexCardSprite(true);
        cardSpriteRight = CreateHexCardSprite(false);
        hexChipSprite = CreateHexChipSprite();
        diamondRingSprite = CreateDiamondRingSprite();
        flareSprite = CreateFlareSprite();
        exitButtonSprite = CreateExitButtonSprite();
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
        BuildEvaluation(contentRect);
        BuildStats(contentRect);
        BuildExitButton(contentRect);
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
        // 全幅の暗い青グラデ帯（モック実測: 左上が僅かに明るく右下・下端へ沈む。
        // 実測値は左上でも (0,7,18) 程度と非常に淡い）。焼き込みテクスチャで敷く。
        Image band = NewImage("HeaderBand", root, Color.white);
        band.sprite = CreateHeaderBandSprite();
        band.type = Image.Type.Simple;
        RectTransform bandRect = band.rectTransform;
        bandRect.anchorMin = new Vector2(0f, 1f);
        bandRect.anchorMax = new Vector2(1f, 1f);
        bandRect.pivot = new Vector2(0.5f, 1f);
        bandRect.anchoredPosition = Vector2.zero;
        bandRect.sizeDelta = new Vector2(0f, 93f);

        // 底のシアン区切り線（全幅・モック実測 y=93）。
        Image line = NewImage("HeaderLine", root, new Color(Cyan.r, Cyan.g, Cyan.b, 0.75f));
        RectTransform lineRect = line.rectTransform;
        lineRect.anchorMin = new Vector2(0f, 1f);
        lineRect.anchorMax = new Vector2(1f, 1f);
        lineRect.pivot = new Vector2(0.5f, 1f);
        lineRect.anchoredPosition = new Vector2(0f, -92f);
        lineRect.sizeDelta = new Vector2(0f, 2f);

        // 左端の二重スラッシュ（モック: 白は短く上、シアンは長く左下）。
        Image slashA = NewImage("HeaderSlashA", root, Color.white);
        SetTopLeftSlash(slashA.rectTransform, new Vector2(58f, -30f), 4f, 44f);
        Image slashB = NewImage("HeaderSlashB", root, new Color(Cyan.r, Cyan.g, Cyan.b, 1f));
        SetTopLeftSlash(slashB.rectTransform, new Vector2(40f, -52f), 4f, 70f);

        // 装飾的な十字アイコン（モックアップ準拠。先端に菱形フィニアル）。
        BuildCrossIcon(root, new Vector2(102f, -50f));

        TMP_Text title = NewText("HeaderTitle", root, "結果  /  RESULT", 44f, Color.white,
            TextAlignmentOptions.MidlineLeft);
        RectTransform titleRect = (RectTransform)title.transform;
        titleRect.anchorMin = titleRect.anchorMax = new Vector2(0f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.anchoredPosition = new Vector2(145f, -25f);
        titleRect.sizeDelta = new Vector2(560f, 64f);

        Image rightSlash = NewImage("HeaderRightSlash", root, Color.white);
        RectTransform rs = rightSlash.rectTransform;
        rs.anchorMin = rs.anchorMax = new Vector2(1f, 1f);
        rs.pivot = new Vector2(0.5f, 0.5f);
        rs.anchoredPosition = new Vector2(-28f, -45f);
        rs.sizeDelta = new Vector2(5f, 70f);
        rs.localRotation = Quaternion.Euler(0f, 0f, -42f);

        // ステージ名と難易度はヘッダー右側へ（モックアップの中央領域は判定専用の
        // ため空けておく。機能情報として右寄せで残す）。
        stageNameText = NewText("StageName", root, "STAGE", 26f, Color.white,
            TextAlignmentOptions.MidlineRight);
        RectTransform sn = (RectTransform)stageNameText.transform;
        sn.anchorMin = sn.anchorMax = new Vector2(1f, 1f);
        sn.pivot = new Vector2(1f, 1f);
        sn.anchoredPosition = new Vector2(-72f, -16f);
        sn.sizeDelta = new Vector2(600f, 40f);

        difficultyText = NewText("Difficulty", root, "LUNATIC", 15f, Cyan,
            TextAlignmentOptions.MidlineRight);
        RectTransform df = (RectTransform)difficultyText.transform;
        df.anchorMin = df.anchorMax = new Vector2(1f, 1f);
        df.pivot = new Vector2(1f, 1f);
        df.anchoredPosition = new Vector2(-72f, -56f);
        df.sizeDelta = new Vector2(300f, 24f);
    }

    // 装飾十字（ラテン十字・横木は上寄り）。縦木/横木＋4先端の菱形フィニアル＋
    // 中央のシアン宝石。pivot は横木と縦木の交点。
    private void BuildCrossIcon(RectTransform root, Vector2 pivot)
    {
        Color silver = new Color(0.92f, 0.95f, 1f, 1f);
        Color silverDim = new Color(0.66f, 0.74f, 0.88f, 1f);
        const float up = 26f, down = 40f, halfW = 22f, thick = 7f, dia = 13f;
        AddQuad(root, "CrossV", silver, pivot + new Vector2(0f, (up - down) * 0.5f),
            new Vector2(thick, up + down), 0f);
        AddQuad(root, "CrossH", silver, pivot, new Vector2(halfW * 2f, thick), 0f);
        AddQuad(root, "CrossTip", silverDim, pivot + new Vector2(0f, up), new Vector2(dia, dia), 45f);
        AddQuad(root, "CrossTip", silverDim, pivot + new Vector2(0f, -down), new Vector2(dia, dia), 45f);
        AddQuad(root, "CrossTip", silverDim, pivot + new Vector2(-halfW, 0f), new Vector2(dia, dia), 45f);
        AddQuad(root, "CrossTip", silverDim, pivot + new Vector2(halfW, 0f), new Vector2(dia, dia), 45f);
        AddQuad(root, "CrossGem", new Color(Cyan.r, Cyan.g, Cyan.b, 0.95f), pivot,
            new Vector2(9f, 9f), 45f);
    }

    private void BuildEvaluation(RectTransform root)
    {
        // ランク文字を包む青系のソフトグロー（外=青 / 内=シアンで層にする）。
        // モックアップの紫グローに替えてユーザー指定の青フレアにする。
        // S はモック実測どおり画面中心 (0,0) に置く。
        rankAuraOuter = NewGraphic<SoftCircleGraphic>("RankAuraOuter", root);
        rankAuraOuter.color = new Color(BrandBlue.r, BrandBlue.g, BrandBlue.b, 0.09f);
        SetRect(rankAuraOuter.rectTransform, Vector2.zero, new Vector2(400f, 400f));

        rankAuraInner = NewGraphic<SoftCircleGraphic>("RankAuraInner", root);
        rankAuraInner.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.13f);
        SetRect(rankAuraInner.rectTransform, Vector2.zero, new Vector2(250f, 250f));

        // 中央判定を囲む多重の菱形ライン（細いリングを層にして、外ほど暗く沈め、
        // 内ほどシアンで明るくする。サイズはモック実測の最大リング≈450 に合わせる）。
        Vector2 ringCenter = new Vector2(0f, 25f);
        float[] ringSizes = { 448f, 396f, 348f, 306f, 270f };
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
            SetRect(ring.rectTransform, ringCenter, Vector2.one * ringSizes[i]);
        }

        // S の背後を薄く沈める菱形（不透明にすると重いので低アルファで浮かせる）。
        Image backdrop = NewImage("RankBackdrop", root, new Color(0.003f, 0.010f, 0.030f, 0.32f));
        backdrop.sprite = cardFillSprite;
        backdrop.type = Image.Type.Sliced;
        SetRect(backdrop.rectTransform, new Vector2(0f, 10f), Vector2.one * 280f);
        backdrop.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

        // 評価エリアの八角形フレーム（モック実測: 幅546 x 高さ604・中心(0,42)・
        // 面取り脚58・左右辺はシェブロン部で途切れる）。
        BuildOctagonFrame(root, new Vector2(0f, 42f), 273f, 302f, 58f);

        // 左右の二重シェブロン（外向き。フレーム側辺の切れ目に置く）。
        AddChevron(root, -1f, new Vector2(-273f, 30f));
        AddChevron(root, -1f, new Vector2(-253f, 30f));
        AddChevron(root, 1f, new Vector2(273f, 30f));
        AddChevron(root, 1f, new Vector2(253f, 30f));

        // S 下の小さなノードクラスタ（モック準拠: 中空菱形＋芯）。
        Color nodeRing = new Color(0.52f, 0.58f, 0.70f, 0.8f);
        Color nodeCore = new Color(0.72f, 0.80f, 0.92f, 0.95f);
        Image bring = NewImage("RankNodeRing", root, nodeRing);
        bring.sprite = diamondRingSprite;
        bring.type = Image.Type.Simple;
        SetRect(bring.rectTransform, new Vector2(0f, -190f), new Vector2(20f, 20f));
        AddQuad(root, "RankNodeCore", nodeCore, new Vector2(0f, -214f), new Vector2(7f, 7f), 45f);
        AddQuad(root, "RankNodeCore", nodeCore, new Vector2(0f, -230f), new Vector2(5f, 5f), 45f);

        // 交点付近の tech 装飾（左=マゼンタ、右=青の小菱形。リング径に合わせ内寄せ）。
        Color techMagenta = new Color(0.55f, 0.10f, 0.60f, 0.5f);
        Color techBlue = new Color(0.10f, 0.35f, 0.85f, 0.5f);
        Vector2[] techPos =
        {
            new Vector2(-152f, 63f), new Vector2(-183f, 25f), new Vector2(-160f, -33f),
            new Vector2(152f, 63f), new Vector2(183f, 25f), new Vector2(160f, -33f),
        };
        for (int i = 0; i < techPos.Length; i++)
        {
            Color tc = techPos[i].x < 0f ? techMagenta : techBlue;
            float sz = (i % 3 == 1) ? 9f : 6f;
            AddQuad(root, "TechDot", tc, ringCenter + techPos[i], new Vector2(sz, sz), 45f);
        }

        verdictText = NewText("Verdict", root, "総合判定\n<size=19><color=#38C2E0>OVERALL EVALUATION</color></size>",
            34f, Color.white, TextAlignmentOptions.Center);
        SetRect((RectTransform)verdictText.transform, new Vector2(0f, 296f), new Vector2(500f, 96f));

        // ランク文字の背後に青系フレア（8方向の光条＋コア）。グレースケールの
        // スターバーストを大=青・小=シアンで二重に敷き、シアン→青のグラデにする。
        rankFlareBlue = NewImage("RankFlareBlue", root, new Color(BrandBlue.r, BrandBlue.g, BrandBlue.b, 0.8f));
        rankFlareBlue.sprite = flareSprite;
        rankFlareBlue.type = Image.Type.Simple;
        SetRect(rankFlareBlue.rectTransform, Vector2.zero, new Vector2(700f, 700f));

        rankFlareCyan = NewImage("RankFlareCyan", root, new Color(Cyan.r, Cyan.g, Cyan.b, 0.78f));
        rankFlareCyan.sprite = flareSprite;
        rankFlareCyan.type = Image.Type.Simple;
        SetRect(rankFlareCyan.rectTransform, Vector2.zero, new Vector2(430f, 430f));

        // ランク文字。セリフ体（Playfair Display）でモック実測の
        // 字高 337px（キャップハイト比≈0.71 → フォントサイズ≈475）に合わせる。
        rankText = NewText("Rank", root, "S", 475f, Color.white, TextAlignmentOptions.Center);
        if (rankFont != null)
        {
            rankText.font = rankFont;
            rankText.fontStyle = FontStyles.Normal;
        }
        else
        {
            // セリフ資産が無い場合は従来の近似（太字＋横幅圧縮）。
            rankText.fontStyle = FontStyles.Bold;
            rankText.rectTransform.localScale = new Vector3(0.92f, 1f, 1f);
        }
        rankText.enableAutoSizing = false;
        rankText.outlineColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.6f);
        rankText.outlineWidth = 0.05f;
        SetRect((RectTransform)rankText.transform, Vector2.zero, new Vector2(560f, 480f));
    }

    // 評価エリアを囲む八角形の細いフレーム。上下辺＋45°面取り＋左右辺
    // （左右辺は中央のシェブロン部で途切れる）。面取り内側に青アクセントと
    // ノード菱形、上辺中央に中空菱形ノードを添える（モックアップ準拠）。
    private void BuildOctagonFrame(RectTransform root, Vector2 c, float hw, float hh, float leg)
    {
        Color frameCol = new Color(BracketWhite.r, BracketWhite.g, BracketWhite.b, 0.92f);
        const float thick = 2.6f;
        float topY = c.y + hh, botY = c.y - hh;
        float edgeHalf = hw - leg;                  // 上下辺の半長
        float sideTop = topY - leg, sideBot = botY + leg;

        // 上下の水平辺。
        AddQuad(root, "OctFrameTop", frameCol, new Vector2(c.x, topY), new Vector2(edgeHalf * 2f, thick), 0f);
        AddQuad(root, "OctFrameBottom", frameCol, new Vector2(c.x, botY), new Vector2(edgeHalf * 2f, thick), 0f);

        float diagLen = leg * 1.41421f;
        for (int s = -1; s <= 1; s += 2)
        {
            // 面取り（上=45°/-45°、下=-45°/45°）。
            AddQuad(root, "OctFrameChamfer", frameCol,
                new Vector2(c.x + s * (edgeHalf + leg * 0.5f), topY - leg * 0.5f),
                new Vector2(diagLen, thick), s * -45f);
            AddQuad(root, "OctFrameChamfer", frameCol,
                new Vector2(c.x + s * (edgeHalf + leg * 0.5f), botY + leg * 0.5f),
                new Vector2(diagLen, thick), s * 45f);

            // 左右辺（シェブロン部 y=c.y±[?] に切れ目）。
            float gapTop = c.y + 90f, gapBot = c.y - 90f;
            AddQuad(root, "OctFrameSideU", frameCol,
                new Vector2(c.x + s * hw, (sideTop + gapTop) * 0.5f),
                new Vector2(thick, sideTop - gapTop), 0f);
            AddQuad(root, "OctFrameSideL", frameCol,
                new Vector2(c.x + s * hw, (gapBot + sideBot) * 0.5f),
                new Vector2(thick, gapBot - sideBot), 0f);

            // 面取り内側の青アクセント（平行線＋端ノード）。
            Color accent = new Color(AccentBlue.r, AccentBlue.g, AccentBlue.b, 0.9f);
            Vector2 inset = new Vector2(s * -13f, -13f);
            Vector2 chamMid = new Vector2(c.x + s * (edgeHalf + leg * 0.5f), topY - leg * 0.5f);
            AddQuad(root, "OctAccent", accent, chamMid + inset, new Vector2(diagLen * 0.9f, 3.2f), s * -45f);
            AddQuad(root, "OctAccentNode", accent, chamMid + inset * 2.1f, new Vector2(7f, 7f), 45f);
            Vector2 insetB = new Vector2(s * -13f, 13f);
            Vector2 chamMidB = new Vector2(c.x + s * (edgeHalf + leg * 0.5f), botY + leg * 0.5f);
            AddQuad(root, "OctAccent", accent, chamMidB + insetB, new Vector2(diagLen * 0.7f, 3.2f), s * 45f);
        }

        // 上辺中央のノード（中空菱形＋直下に小さな芯）。
        Color nodeRing = new Color(0.52f, 0.58f, 0.70f, 0.9f);
        Image tring = NewImage("OctTopNode", root, nodeRing);
        tring.sprite = diamondRingSprite;
        tring.type = Image.Type.Simple;
        SetRect(tring.rectTransform, new Vector2(c.x, topY), new Vector2(18f, 18f));
        AddQuad(root, "OctTopNodeCore", new Color(0.72f, 0.80f, 0.92f, 0.9f),
            new Vector2(c.x, topY - 22f), new Vector2(6f, 6f), 45f);
    }

    private void BuildStats(RectTransform root)
    {
        // 位置・サイズはモックアップ実測（1080ref: 430x248・中心 ±487 / +185 / -175）。
        scoreText = BuildStatCard(root, "Score", new Vector2(-487f, 185f), "スコア", "SCORE", "000,000", StatIcon.Crosshair);
        hitText = BuildStatCard(root, "Hit", new Vector2(487f, 185f), "被弾回数", "HIT COUNT", "00", StatIcon.Shield);
        counterText = BuildStatCard(root, "Counter", new Vector2(-487f, -175f), "カウンター回数", "COUNTER COUNT", "00", StatIcon.Swords);
        timeText = BuildStatCard(root, "Time", new Vector2(487f, -175f), "時間", "TIME", "00:00", StatIcon.Clock);
    }

    private TMP_Text BuildStatCard(RectTransform root, string name, Vector2 pos,
        string jp, string en, string value, StatIcon icon)
    {
        GameObject card = NewRect(name + "Card", root);
        RectTransform rect = (RectTransform)card.transform;
        Vector2 size = new Vector2(430f, 248f);
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
        SetRect(chip.rectTransform, new Vector2(-114f, 53f), new Vector2(68f, 78f));

        Image iconImg = NewImage("Icon", rect, Color.white);
        iconImg.sprite = statIconSprites[(int)icon];
        iconImg.type = Image.Type.Simple;
        SetRect(iconImg.rectTransform, new Vector2(-114f, 53f), new Vector2(46f, 46f));

        // ラベルはチップ右の領域で中央揃え（モック実測: スコア/カウンター回数とも
        // ブロック中心が card 中心から +11 付近）。
        TMP_Text label = NewText("Label", rect,
            jp + "\n<size=20><color=#38C2E0>" + en + "</color></size>",
            36f, Color.white, TextAlignmentOptions.Center);
        SetRect((RectTransform)label.transform, new Vector2(11f, 44f), new Vector2(340f, 84f));

        // 見出し下の細い区切り線（中央ノード＋両端ターミナル付き）。
        BuildDivider(rect, new Vector2(0f, -16f), 250f);

        TMP_Text valueText = NewText("Value", rect, value, 58f, Color.white, TextAlignmentOptions.Center);
        valueText.characterSpacing = 4f;
        SetRect((RectTransform)valueText.transform, new Vector2(-14f, -66f), new Vector2(390f, 74f));
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

    // モックアップどおりの単一ボタン「プレイを終わる」（ステージ選択へ戻る）。
    // 青の縦グラデ本体＋銀の外枠＋両端ブラケットを一体で焼き込んだスプライト。
    private void BuildExitButton(RectTransform root)
    {
        GameObject buttonGo = NewRect("ExitAction", root);
        RectTransform rect = (RectTransform)buttonGo.transform;
        SetRect(rect, new Vector2(0f, -433f), new Vector2(660f, 120f));
        exitRect = rect;

        // ボタン背後の淡い青グロー（モックの発光感）。
        SoftCircleGraphic glow = NewGraphic<SoftCircleGraphic>("ExitGlow", rect);
        glow.color = new Color(BrandBlue.r, BrandBlue.g, BrandBlue.b, 0.10f);
        SetRect(glow.rectTransform, Vector2.zero, new Vector2(760f, 190f));

        Image body = NewImage("Body", rect, Color.white);
        body.sprite = exitButtonSprite;
        body.type = Image.Type.Simple;
        Stretch(body.rectTransform);
        body.raycastTarget = true;

        TMP_Text label = NewText("Label", rect, "プレイを終わる", 40f, Color.white,
            TextAlignmentOptions.Center);
        Stretch((RectTransform)label.transform);

        Button button = buttonGo.AddComponent<Button>();
        button.targetGraphic = body;
        button.transition = Selectable.Transition.None;
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;
        button.onClick.AddListener(RequestExit);

        EventTrigger trigger = buttonGo.AddComponent<EventTrigger>();
        AddTrigger(trigger, EventTriggerType.PointerEnter, _ => SetExitHover(true));
        AddTrigger(trigger, EventTriggerType.PointerExit, _ => SetExitHover(false));
    }

    private void SetExitHover(bool hover)
    {
        if (exitRect != null)
            exitRect.localScale = Vector3.one * (hover ? 1.025f : 1f);
    }

    public void Prepare(StageData stage, int difficulty, bool cleared, int hitCount,
        int counterCount, float elapsedSeconds, float endSeconds)
    {
        gameObject.SetActive(true);
        inputArmed = false;
        entering = false;
        SetExitHover(false);

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

    // 単一ボタンのため左右選択は無い。決定/戻るのどちらでもステージ選択へ戻る。
    public void Tick(bool left, bool right, bool buttonHeld, bool buttonPressed, bool backPressed)
    {
        if (!gameObject.activeSelf || entering) return;

        if (backPressed)
        {
            RequestExit();
            return;
        }

        if (!inputArmed)
        {
            if (!buttonHeld) inputArmed = true;
            return;
        }

        if (buttonPressed) RequestExit();
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

    private void RequestExit()
    {
        if (!gameObject.activeSelf) return;
        ActionRequested?.Invoke(Action.StageSelect);
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
        const int refW = 430, refH = 248;
        int TW = refW * S, TH = refH * S;
        // 面取り脚長（ref 単位）: 外側大・内側小。
        float cOT = 56f, cOB = 56f, cIT = 22f, cIB = 42f;
        float hw = refW * 0.5f * S, hh = refH * 0.5f * S;
        Vector2[] v = HexCardVerts(outerLeft, hw, hh, cOT * S, cOB * S, cIT * S, cIB * S);

        Texture2D texture = new Texture2D(TW, TH, TextureFormat.RGBA32, false);
        texture.name = "ResultHexCard_" + (outerLeft ? "L" : "R");
        texture.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[TW * TH];
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
                Blend(px, TW, TH, x, y, TexFillNavy, inside * TexFillNavy.a);
                // カード内側の微かな明色グロー（上中央が最も明るく、外へ減衰。
                // モック実測ではごく淡いので係数は控えめ）。
                float gx = (x - cx) / hw;
                float gy = (y - cy) / hh;
                float gd = Mathf.Sqrt(gx * gx * 0.6f + (gy - 0.28f) * (gy - 0.28f));
                float glow = Mathf.Clamp01(1f - gd);
                Blend(px, TW, TH, x, y, TexInnerGlow, inside * glow * glow * 0.22f);
                Blend(px, TW, TH, x, y, TexOutlineTan, Mathf.Clamp01(outlineHalf - Mathf.Abs(sdf) + 0.5f));
                Blend(px, TW, TH, x, y, TexInnerGold, Mathf.Clamp01(1.0f - Mathf.Abs(sdf + innerOffset)) * 0.7f);
            }
        }

        // 内側下辺の面取りに沿った太い青アクセント（少し内側へ詰めて外周の枠を残す）。
        Vector2 a0, a1;
        if (outerLeft) { a0 = v[3]; a1 = v[4]; }   // 右下面取り
        else { a0 = v[5]; a1 = v[6]; }             // 左下面取り
        Vector2 am = (a0 + a1) * 0.5f;
        a0 = Vector2.Lerp(a0, am, 0.07f);
        a1 = Vector2.Lerp(a1, am, 0.07f);
        DrawLine(px, TW, TH, a0.x + cx, a0.y + cy, a1.x + cx, a1.y + cy, 12f * S, TexAccentBlue);

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
        DrawLine(px, w, h, a.x + cx, a.y + cy, b.x + cx, b.y + cy, width, TexBracketWhite);
        DrawLine(px, w, h, a.x + cx, a.y + cy, ac.x + cx, ac.y + cy, width, TexBracketWhite);
        DrawLine(px, w, h, b.x + cx, b.y + cy, bc.x + cx, bc.y + cy, width, TexBracketWhite);
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
                // 視覚(sRGB)値で焼く（モック実測: 塗り (0,19,49)・枠 (1,86,174)）。
                Color fillColor = Color.Lerp(new Color(0.00f, 0.055f, 0.150f),
                    new Color(0.02f, 0.105f, 0.235f), ty);
                Blend(px, size, size, x, y, fillColor, fill * 0.95f);
                float ring = Mathf.Clamp01(1.5f - Mathf.Abs(d));
                Blend(px, size, size, x, y, new Color(0.004f, 0.34f, 0.68f), ring * 0.9f);
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

    // ヘッダーの暗い青グラデ帯。モック実測はごく淡く（左上でも (0,7,18) 程度）、
    // 上端ほど・左ほど僅かに明るい。視覚(sRGB)値で焼く。
    private Sprite CreateHeaderBandSprite()
    {
        const int W = 512, H = 96;
        Texture2D texture = new Texture2D(W, H, TextureFormat.RGBA32, false);
        texture.name = "ResultHeaderBandTexture";
        texture.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[W * H];
        for (int y = 0; y < H; y++)
        {
            float ty = y / (float)(H - 1);          // 0 下端 .. 1 上端
            float vert = 0.35f + 0.65f * ty;        // 上ほど明るい
            for (int x = 0; x < W; x++)
            {
                float tx = x / (float)(W - 1);
                float horiz = 1f - 0.55f * tx;      // 左ほど明るい
                float k = vert * horiz;
                px[y * W + x] = new Color(0.004f * k, 0.030f * k, 0.078f * k, 0.95f);
            }
        }
        texture.SetPixels32(px);
        texture.Apply();
        generatedTextures.Add(texture);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, W, H), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = "ResultHeaderBand";
        generatedSprites.Add(sprite);
        return sprite;
    }

    // モックアップのボタンを一体で焼き込む: 青の縦グラデ本体（下辺ほど明るく、
    // 最下辺にシアンの滲み）＋面取り付き銀枠（上下辺は青く発光）＋両端ブラケット。
    // 色はモック実測の視覚(sRGB)値。
    private Sprite CreateExitButtonSprite()
    {
        const int S = 2;
        const int refW = 660, refH = 120;
        int TW = refW * S, TH = refH * S;
        Texture2D texture = new Texture2D(TW, TH, TextureFormat.RGBA32, false);
        texture.name = "ResultExitButtonTexture";
        texture.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[TW * TH];
        float cx = (TW - 1) * 0.5f, cy = (TH - 1) * 0.5f;

        // 枠八角形（横 616 x 縦 98・面取り 16）と本体（内側へ 7）。
        Vector2[] frame = OctagonVerts(616f * 0.5f * S, 98f * 0.5f * S, 16f * S);
        Vector2[] body = OctagonVerts((616f * 0.5f - 7f) * S, (98f * 0.5f - 7f) * S, 12f * S);

        // 本体の縦グラデ（モック実測: 上 (0,38,90) → 下 (0,63,150)、最下辺 (0,79,182)）。
        Color topCol = new Color(0.000f, 0.149f, 0.353f);
        Color botCol = new Color(0.000f, 0.247f, 0.588f);
        Color rimCol = new Color(0.000f, 0.310f, 0.714f);
        Color bloomCol = new Color(0.157f, 0.471f, 0.863f);
        float bodyHalfH = (98f * 0.5f - 7f) * S;
        for (int y = 0; y < TH; y++)
        {
            for (int x = 0; x < TW; x++)
            {
                Vector2 p = new Vector2(x - cx, y - cy);
                float sdfB = ConvexSdf(body, p);
                float inside = Mathf.Clamp01(0.5f - sdfB);
                if (inside > 0f)
                {
                    float ty = Mathf.Clamp01(0.5f - p.y / (bodyHalfH * 2f)); // 0 上 .. 1 下
                    Color grad = Color.Lerp(topCol, botCol, ty * ty);
                    Blend(px, TW, TH, x, y, grad, inside);
                    // 最下辺のシアン寄りの滲み（高さ 8ref 分）と上辺の細いハイライト。
                    float rim = Mathf.Clamp01((p.y + bodyHalfH) / (8f * S));
                    if (p.y < -bodyHalfH + 8f * S)
                        Blend(px, TW, TH, x, y, rimCol, inside * (1f - rim) * 0.85f);
                    if (p.y > bodyHalfH - 4f * S)
                        Blend(px, TW, TH, x, y, new Color(0.24f, 0.47f, 0.78f), inside * 0.35f);
                    // 中央下寄りの淡いブルーム。
                    float bd = Mathf.Sqrt((p.x / (250f * S)) * (p.x / (250f * S))
                        + ((p.y + bodyHalfH * 0.4f) / (70f * S)) * ((p.y + bodyHalfH * 0.4f) / (70f * S)));
                    Blend(px, TW, TH, x, y, bloomCol, inside * Mathf.Clamp01(1f - bd) * 0.18f);
                }
                // 枠線（銀）。上辺・下辺は青の発光色を重ねる。
                float sdfF = ConvexSdf(frame, p);
                float line = Mathf.Clamp01(1.2f * S - Mathf.Abs(sdfF) + 0.5f);
                if (line > 0f)
                {
                    Blend(px, TW, TH, x, y, new Color(0.412f, 0.400f, 0.447f), line * 0.9f);
                    float frameHalfH = 98f * 0.5f * S;
                    if (p.y > frameHalfH - 14f * S)
                        Blend(px, TW, TH, x, y, new Color(0.000f, 0.220f, 0.565f), line * 0.9f);
                    else if (p.y < -frameHalfH + 14f * S)
                        Blend(px, TW, TH, x, y, new Color(0.000f, 0.298f, 0.714f), line * 0.9f);
                }
            }
        }

        // 両端のブラケット（タン＋白の二重角マーク）。枠の左右頂点から外へ。
        float fx = 616f * 0.5f * S;      // 枠の半幅
        float bh = 98f * 0.5f * S - 16f * S;
        Color tan = new Color(0.588f, 0.510f, 0.392f);
        for (int s = -1; s <= 1; s += 2)
        {
            float ox = s * (fx + 8f * S);
            // タンの角マーク（frame 面取りに沿う「〈」型）。
            DrawLine(px, TW, TH, cx + ox, cy + bh + 10f * S, cx + ox + s * 12f * S, cy + bh - 6f * S, 2.6f * S, tan);
            DrawLine(px, TW, TH, cx + ox, cy - bh - 10f * S, cx + ox + s * 12f * S, cy - bh + 6f * S, 2.6f * S, tan);
            DrawLine(px, TW, TH, cx + ox, cy + bh + 10f * S, cx + ox, cy - bh - 10f * S, 2.4f * S,
                new Color(0.75f, 0.80f, 0.90f));
            // 白の内側マーク。
            float ix = s * (fx - 4f * S);
            DrawLine(px, TW, TH, cx + ix, cy + bh + 4f * S, cx + ix, cy - bh - 4f * S, 2.0f * S,
                new Color(0.88f, 0.92f, 1f, 0.85f));
        }

        texture.SetPixels32(px);
        texture.Apply();
        generatedTextures.Add(texture);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, TW, TH),
            new Vector2(0.5f, 0.5f), 100f * S);
        sprite.name = "ResultExitButton";
        generatedSprites.Add(sprite);
        return sprite;
    }

    // 中央原点の横長八角形（4隅を 45°面取り）。CW。
    private static Vector2[] OctagonVerts(float hw, float hh, float ch)
    {
        return new[]
        {
            new Vector2(-hw + ch, hh),
            new Vector2( hw - ch, hh),
            new Vector2( hw, hh - ch),
            new Vector2( hw, -hh + ch),
            new Vector2( hw - ch, -hh),
            new Vector2(-hw + ch, -hh),
            new Vector2(-hw, -hh + ch),
            new Vector2(-hw, hh - ch),
        };
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
        Color col = TexIconBlue;
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

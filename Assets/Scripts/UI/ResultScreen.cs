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
        Title,
    }

    // Linear Color Space 上でモックアップの sRGB 色へ見えるよう調整した値。
    private static readonly Color Cyan = new Color(0.04f, 0.54f, 0.75f, 1f);       // visual #38C2E0
    private static readonly Color BrandBlue = new Color(0.055f, 0.28f, 0.708f, 1f); // visual #4290DB
    private static readonly Color DeepNavy = new Color(0.001f, 0.003f, 0.008f, 1f);
    private static readonly Color PanelNavy = new Color(0.003f, 0.012f, 0.03f, 1f);

    // 頂点色（Image.color / AddQuad）用の linear 値。表示時に sRGB エンコードされる。
    private static readonly Color BracketWhite = new Color(0.85f, 0.90f, 1f, 1f);        // 明るい白（枠・ブラケット）
    private static readonly Color AccentBlue = new Color(0.020f, 0.225f, 0.780f, 1f);    // 鮮やかな青（シェブロン等）
    private static readonly Color DividerTan = new Color(0.092f, 0.086f, 0.080f, 0.95f); // 区切り線本体（縮小表示でも読める明度に）
    private static readonly Color DividerBright = new Color(0.32f, 0.35f, 0.38f, 0.95f); // 区切り線のノード/両端

    // 焼き込みテクスチャ用の視覚（sRGB）値。生成 Texture2D は sRGB サンプル→linear→
    // 表示 sRGB エンコードで書いた値がそのまま画面に出るため、頂点色と違い
    // pre-linear 化せず「モックアップで実測した見た目の値」をそのまま書く。
    // （v7 まで頂点用の linear 値を流用して枠線が実測 (52,47,32) と暗すぎた反省）
    private static readonly Color TexOutlineTan = new Color(0.314f, 0.280f, 0.255f, 0.98f); // カード枠（長辺は実測より12%沈め、角の白と差を付ける）
    private static readonly Color TexInnerGold = new Color(0.235f, 0.204f, 0.133f, 1f);     // 内側の鈍い金線
    private static readonly Color TexFillNavy = new Color(0.010f, 0.024f, 0.060f, 0.95f);   // カード塗り（実測 (0,5,14)）
    private static readonly Color TexInnerGlow = new Color(0.05f, 0.13f, 0.30f);            // カード内側の微光
    private static readonly Color TexAccentBlue = new Color(0.12f, 0.43f, 0.90f, 1f);       // 青アクセント帯
    private static readonly Color TexBracketWhite = new Color(0.85f, 0.90f, 1f, 1f);        // 白ブラケット
    private static readonly Color TexChipGold = new Color(0.765f, 0.643f, 0.40f, 1f);       // チップ縁の金(視覚sRGB ≈ #C3A466)

    // アイコンのティント(頂点色=linear)。明るい白金(視覚 ≈ #EEF1F4)。
    // 金縁との明度差を確保し「金縁+白金シルエット」の二素材構成に見せる
    // (oracle 指摘: 当初の暖色寄り #EDE3C1 では縁と一体化して金一色に見えた)。
    private static readonly Color IconWarmWhite = new Color(0.86f, 0.88f, 0.91f, 1f);

    private enum StatIcon { Crosshair, Shield, Swords, Clock }

    private TMP_FontAsset font;
    private TMP_FontAsset rankFont; // ランク文字専用のセリフ体（Playfair Display）
    private CanvasGroup contentGroup;
    private RectTransform contentRect;
    private TMP_Text stageNameText;
    private TMP_Text difficultyText;
    private TMP_Text verdictText;
    private TMP_Text verdictRubyText;
    private TMP_Text rankText;
    private TMP_Text scoreText;
    private TMP_Text hitText;
    private TMP_Text counterText;
    private TMP_Text timeText;
    // ルビ(振り仮名)の実測配置バインディング。各ルビを対応本文の指定語範囲の
    // 漢字グリフ実測中心へ Prepare で配置する(TmpAlign.PlaceRubyOverKanji)。
    // 従来は等幅全角前提の算術 x で置いていたため CJK フォールバックの実アドバンス
    // と数 px ずれた。start/len は本文文字列先頭からの語範囲(漢字だけに自動で絞る)。
    private struct RubyBind { public TMP_Text body; public RectTransform ruby; public int start; public int len; }
    private readonly List<RubyBind> rubyBinds = new List<RubyBind>();
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
    private Material rankGlowMat;
    private RectTransform exitRect;
    private TMP_Text exitLabel;
    // 2 択(左=ステージ選択 / 右=プレイを終わる→タイトル)のボタン群と選択状態。
    // スティック左右で選択、ボタンで決定する(2026-07-13 指摘: リザルト→タイトル導線)。
    private readonly RectTransform[] actionRects = new RectTransform[2];
    private readonly CanvasGroup[] actionGroups = new CanvasGroup[2];
    private readonly Action[] actionValues = new Action[2];
    private int selectedActionIndex;
    private bool navLeftPrev;
    private bool navRightPrev;
    private bool inputArmed;
    private bool entering;

    // リザルト BGM(Killing Party の 1:50 以降)。画面ローカルの AudioSource で
    // ループ再生する。入場アニメ(溜め→開放)と同時に再生を開始し、clip の頭に
    // ある build-up が「溜め」、1.44s 後のドロップが「開放(ランクスタンプ着地)」と
    // 一致するようトリム済み(clip t=0=原曲 110.113s)。画面の SetActive(false) で
    // 切れないよう、ソースは兄弟 GO に常駐させる。素材: DOVA-SYNDROME。
    private AudioSource bgmSource;
    private AudioClip resultBgm;

    // --- 入場演出（溜め→開放のシーケンス）---
    // ヘッダー降下→カード左右スライド→数値カウントアップ→中央装飾→
    // ランクのスタンプ着地（一閃＋波紋＋揺れ）→ボタン。決定/戻るでスキップ可。
    private const float EnterHeaderDur = 0.30f;
    private const float EnterCardStart = 0.12f;
    private const float EnterCardStagger = 0.09f;
    private const float EnterCardDur = 0.34f;
    // カウント開始はカード整定の後ろへ 60ms、スタンプはカウント完了後に
    // 80ms の溜めを置き、巨大状態の滞在を短縮（oracle 動画レビュー反映）。
    private const float EnterCountStart = 0.48f;
    private const float EnterCountDur = 0.60f;
    private const float EnterEvalStart = 0.55f;
    private const float EnterEvalDur = 0.35f;
    private const float EnterStampStartClear = 1.16f;
    private const float EnterStampDurClear = 0.28f;
    private const float EnterStampStartFail = 1.16f;
    private const float EnterStampDurFail = 0.50f;
    private const float EnterFlashDur = 0.38f;
    private const float EnterRippleDur = 0.55f;
    private const float EnterButtonDelay = 0.16f;   // スタンプ着地からの遅延
    private const float EnterButtonDur = 0.32f;
    private const float EnterTail = 0.95f;          // 着地後に波紋・ボタンを収める残り尺

    // リザルト BGM 音量。Killing Party(1:50 以降 実測 -9.8LUFS)を、ステージ
    // BGM 帯(stone -10.7LUFS が最大)より僅かに下の -12LUFS へ揃える減衰
    // (-2.2dB)。全体は AudioListener.volume でさらに減衰する。
    // 2026-07-13 指摘「もう一段下げる」で 0.78→0.52(約 -3.5dB / -15.5LUFS 相当)。
    private const float ResultBgmVolume = 0.52f;
    // PlayScheduled のわずかな先読み(DSP バッファ境界で確実にスケジュールするため)。
    // この間だけ入場は t=0 で保持され、その後 dspTime に追従する。
    private const double BgmScheduleLead = 0.08d;

    private CanvasGroup headerGroup;
    private RectTransform headerGroupRect;
    private CanvasGroup evalGroup;
    private RectTransform evalGroupRect;
    private CanvasGroup rankGroup;
    private RectTransform rankGroupRect;
    private CanvasGroup buttonGroup;
    private readonly RectTransform[] cardRects = new RectTransform[4];
    private readonly CanvasGroup[] cardGroups = new CanvasGroup[4];
    private readonly Vector2[] cardHomes = new Vector2[4];
    // BuildStats の初期配置(1P・±515/±500)。2P で中央寄せへ動かした後、
    // 同一インスタンスで 1P へ戻す防御経路で確実に復元するため保持。
    private readonly Vector2[] cardHomeOriginal = new Vector2[4];
    private int builtCardCount;
    private Image rippleA;
    private Image rippleB;
    private Coroutine entranceRoutine;
    private bool resultCleared = true;
    private int finalScore;
    private int finalHit;
    private int finalCounter;
    private float finalElapsed;
    private Color flareBlueBase;
    private Color flareCyanBase;
    private Vector2 exitHome;

    // 2P(その2): 左右分割リザルト。中央に P1/P2 の 2 ランク、左列=P1・右列=P2 の
    // スコア/被弾。すべて既存ウィジェットの「上書き」として実装し、1P では一切
    // 実行されない(twoPlayerResult=false のまま)=現行リザルト byte/実測不変。
    private bool twoPlayerResult;
    private bool twoPlayerLayoutApplied;   // 一度でも 2P 上書きしたら真(1P 復元の要否判定)
    private int finalScore2;
    private int finalHit2;
    private bool twoPlayerSidesReversed;

    private TMP_Text rankText2;        // P2 のランク字(既定は非アクティブ)
    private Material rankGlowMat2;
    private TMP_Text p1RankTag;        // 「1P」見出し(ランク上)
    private TMP_Text p2RankTag;        // 「2P」見出し
    // 4 枚のステータスカードのラベル/ルビ/アイコン参照(2P で再ラベル・再アイコン
    // するために保持)。索引は生成順: 0=スコア 1=被弾 2=カウンター 3=時間。
    // statLabelOriginal は 1P レイアウトへ確実に戻すための元ラベル文字列。
    private readonly TMP_Text[] statLabels = new TMP_Text[4];
    private readonly TMP_Text[] statRubies = new TMP_Text[4];
    private readonly Image[] statIcons = new Image[4];
    private readonly string[] statLabelOriginal = new string[4];
    // カード生成順に対応する既定アイコン(1P 復元用)。
    private static readonly StatIcon[] StatCardIcons =
        { StatIcon.Crosshair, StatIcon.Shield, StatIcon.Swords, StatIcon.Clock };

    // ランク文字のアンダーレイグロー色（クリア=青 / 失敗=赤）。
    private static readonly Color RankGlowBlue = new Color(0.20f, 0.60f, 1f, 0.55f);
    private static readonly Color RankGlowRed = new Color(1f, 0.25f, 0.35f, 0.45f);

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
        // パラメータアイコンは Google Material Symbols(Apache 2.0)の白シルエット
        // (Assets/Resources/UI/result_icon_*.png)。資産が無い環境では従来の
        // 手続き生成ラインアイコンへフォールバックする。
        statIconSprites[(int)StatIcon.Crosshair] = LoadStatIconSprite("result_icon_score", StatIcon.Crosshair);
        statIconSprites[(int)StatIcon.Shield] = LoadStatIconSprite("result_icon_hit", StatIcon.Shield);
        statIconSprites[(int)StatIcon.Swords] = LoadStatIconSprite("result_icon_counter", StatIcon.Swords);
        statIconSprites[(int)StatIcon.Clock] = LoadStatIconSprite("result_icon_time", StatIcon.Clock);

        Image background = NewImage("Background", root, DeepNavy);
        Stretch(background.rectTransform);
        BuildBackgroundDecor(root);

        GameObject content = NewRect("Content", root);
        contentRect = (RectTransform)content.transform;
        Stretch(contentRect);
        contentGroup = content.AddComponent<CanvasGroup>();

        // 入場演出でまとめて動かす単位ごとに全面コンテナへ分ける
        // （カードとボタンは個別に動かすので直下のまま）。
        headerGroup = NewGroup("HeaderGroup", contentRect, out headerGroupRect);
        BuildHeader(headerGroupRect);
        evalGroup = NewGroup("EvalGroup", contentRect, out evalGroupRect);
        BuildEvaluation(evalGroupRect);
        BuildStats(contentRect);
        BuildActionButtons(contentRect);
        BuildAudio();
    }

    // リザルト BGM 用の常駐 AudioSource(兄弟 GO)とクリップの読み込み。クリップが
    // 無い環境では無音になるだけで他の動作には影響しない。入場開始と同時に
    // 再生できるよう、ここで LoadAudioData を要求して先読みしておく。
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
            resultBgm.LoadAudioData();   // 入場と同フレームで Play できるよう先読み
        }
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
        BuildHeaderBanner(root);
        BuildHeaderTexts(root);
    }

    // ステージ選択の Head バー直系の構図（2026-07-10 の A/B 静止モック比較で採用）。
    // 全幅を「ブランド青の主帯（横グラデ・右端が斜め）→太い白スラッシュ仕切り→
    // 濃紺の副帯（ステージ名/難易度）」に分割する。質感側の持ち込みとして、
    // 主帯上辺に銀のハイライト・下辺に沈み色を敷く。バー高 106・横グラデ・
    // 濃紺副帯は oracle レビュー(8.9→9.0 の最小修正セット)反映。
    private const float HeaderBandH = 106f;

    private void BuildHeaderBanner(RectTransform root)
    {
        const float bandH = HeaderBandH;

        // 主帯。左端は画面外へ逃がして垂直に見せ、右端の斜めだけを出す。
        // 参照元(ステージ選択)と同じく左の濃青→右の鮮青の横グラデを焼き込む。
        Image main = NewImage("HeaderMain", root, Color.white);
        main.sprite = CreateHeaderMainSprite();
        main.type = Image.Type.Simple;
        SetTopBand(main.rectTransform, -60f, 0f, 1310f, bandH);

        // 白スラッシュ仕切り（ステージ選択 Head の太い白パラレログラム）。
        ParallelogramGraphic slash = NewGraphic<ParallelogramGraphic>("HeaderSlash", root);
        slash.SlantRightEdge = true;
        slash.color = Color.white;
        SetTopBand(slash.rectTransform, 1262f, 0f, 70f, bandH);

        // 副帯（右側・ステージ名/難易度の受け）。ユーザー指摘(2026-07-10)の
        // 「石工/LUNATIC 側にもバーを」反映: v9 の濃紺ベタ(ほぼ黒で背景に沈む)を
        // やめ、主帯と同じ処理＝横グラデ焼き込み＋斜め端＋金属エッジを
        // 一段暗いトーンで敷く。白スラッシュ仕切りは好評につき維持。
        // 白スラッシュ両側の黒ギャップが対称(12px)になる位置に置く
        // （主帯右端 1250/1216・スラッシュ 1262..1332/1296..1298 に対し 1310）。
        Image sub = NewImage("HeaderSub", root, Color.white);
        sub.sprite = CreateHeaderSubSprite();
        sub.type = Image.Type.Simple;
        SetTopBand(sub.rectTransform, 1310f, 0f, 640f, bandH);

        // 金属質感: 主帯・副帯の上辺ハイライト（銀）と下辺の沈み。
        Image topEdge = NewImage("HeaderTopEdge", root, new Color(0.55f, 0.60f, 0.70f, 0.85f));
        SetTopBand(topEdge.rectTransform, -60f, 0f, 1298f, 2.5f);
        Image botEdge = NewImage("HeaderBotEdge", root, new Color(0.004f, 0.03f, 0.09f, 0.85f));
        SetTopBand(botEdge.rectTransform, -60f, -(bandH - 3f), 1264f, 3f);
        Image subTopEdge = NewImage("HeaderSubTopEdge", root, new Color(0.55f, 0.60f, 0.70f, 0.7f));
        SetTopBand(subTopEdge.rectTransform, 1344f, 0f, 576f, 2.5f);
        Image subBotEdge = NewImage("HeaderSubBotEdge", root, new Color(0.004f, 0.03f, 0.09f, 0.7f));
        SetTopBand(subBotEdge.rectTransform, 1310f, -(bandH - 3f), 610f, 3f);

        // 装飾的な十字アイコン＋タイトル（青帯の上に白・バー中心に合わせる）。
        // ユーザー指摘(2026-07-10): 小さく・縦中央・白単色。十字は非対称
        // (up18/down29・フィニアル込み全高57)なので、バウンディング中心が
        // バー中央 y=-53 に来るよう交点を +5.5 上げる。
        BuildCrossIcon(root, new Vector2(102f, -47.5f));
        BuildHeaderTitle(root);
    }

    // 主帯の横グラデ焼き込み（右端 34px 斜め・視覚 sRGB 値 #014190 → #026CDB）。
    private Sprite CreateHeaderMainSprite()
    {
        const int W = 1310, H = 106;
        const float skew = 34f;
        Texture2D texture = new Texture2D(W, H, TextureFormat.RGBA32, false);
        texture.name = "ResultHeaderMainTexture";
        texture.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[W * H];
        Color left = new Color(0.004f, 0.255f, 0.565f);
        Color right = new Color(0.008f, 0.424f, 0.859f);
        for (int y = 0; y < H; y++)
        {
            float t = y / (float)(H - 1);              // 0 下端 .. 1 上端
            float edge = (W - skew) + skew * t;        // 右端の斜め境界
            for (int x = 0; x < W; x++)
            {
                float a = Mathf.Clamp01(edge - x);
                if (a <= 0f) continue;
                Color c = Color.Lerp(left, right, x / (float)(W - 1));
                px[y * W + x] = new Color(c.r, c.g, c.b, a);
            }
        }
        texture.SetPixels32(px);
        texture.Apply();
        generatedTextures.Add(texture);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, W, H), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = "ResultHeaderMain";
        generatedSprites.Add(sprite);
        return sprite;
    }

    // 副帯の横グラデ焼き込み（左端 34px 斜め＝白スラッシュと平行・
    // 視覚 sRGB 値 #01356E → #011835。主帯より一段暗い青で主従を保ちつつ、
    // 右端でも「石工/LUNATIC」の背後が帯として読める明度を確保する）。
    private Sprite CreateHeaderSubSprite()
    {
        const int W = 640, H = 106;
        const float skew = 34f;
        Texture2D texture = new Texture2D(W, H, TextureFormat.RGBA32, false);
        texture.name = "ResultHeaderSubTexture";
        texture.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[W * H];
        Color left = new Color(0.004f, 0.208f, 0.431f);
        Color right = new Color(0.005f, 0.095f, 0.208f);
        for (int y = 0; y < H; y++)
        {
            float t = y / (float)(H - 1);              // 0 下端 .. 1 上端
            float edge = skew * t;                     // 左端の斜め境界
            for (int x = 0; x < W; x++)
            {
                float a = Mathf.Clamp01(x - edge);
                if (a <= 0f) continue;
                Color c = Color.Lerp(left, right, x / (float)(W - 1));
                px[y * W + x] = new Color(c.r, c.g, c.b, a);
            }
        }
        texture.SetPixels32(px);
        texture.Apply();
        generatedTextures.Add(texture);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, W, H), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = "ResultHeaderSub";
        generatedSprites.Add(sprite);
        return sprite;
    }

    private void BuildHeaderTitle(RectTransform root)
    {
        TMP_Text title = NewText("HeaderTitle", root, "結果  /  RESULT", 42f, Color.white,
            TextAlignmentOptions.MidlineLeft);
        RectTransform titleRect = (RectTransform)title.transform;
        titleRect.anchorMin = titleRect.anchorMax = new Vector2(0f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.anchoredPosition = new Vector2(145f, -27f);
        titleRect.sizeDelta = new Vector2(560f, 64f);

        // 「結果」のルビ（曲選択ヘッダー「きょく/せんたく」と同じ様式:
        // 白 α0.85・中央揃え・漢字ブロックの直上・本文比 ≈0.37）。
        // x は算術（145+42=187）を初期値に置き、Prepare の PlaceAllRubies で
        // 「結果」グリフの実測中心へ精密化する（CJK 実アドバンスのズレ補正）。
        TMP_Text ruby = NewText("HeaderRuby", root, "けっか", 15f,
            new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Center);
        RectTransform rubyRect = (RectTransform)ruby.transform;
        rubyRect.anchorMin = rubyRect.anchorMax = new Vector2(0f, 1f);
        rubyRect.pivot = new Vector2(0.5f, 0.5f);
        rubyRect.anchoredPosition = new Vector2(187f, -28f);
        rubyRect.sizeDelta = new Vector2(160f, 20f);
        BindRuby(title, ruby, 0, 2); // 「結果」
    }

    // ステージ名と難易度はヘッダー右側の副帯へ（モックアップの中央領域は判定専用
    // のため空けておく）。ユーザー指摘(2026-07-10): 2行積みをやめ
    // 「石工 LUNATIC」の横並び1行にして文字を大きく。行は帯(高さ106)の
    // 縦中央 y=-53。横位置は難易度語(EASY/NORMAL/LUNATIC)の描画幅で変わる
    // ため LayoutHeaderStageRow(Prepare 内)で確定する。
    private const float HeaderStageRowRight = -104f;  // 難易度の右端 x
    private const float HeaderStageRowGap = 22f;      // ステージ名と難易度の間隔

    private void BuildHeaderTexts(RectTransform root)
    {
        stageNameText = NewText("StageName", root, "STAGE", 44f, new Color(1f, 1f, 1f, 0.85f),
            TextAlignmentOptions.MidlineRight);
        RectTransform sn = (RectTransform)stageNameText.transform;
        sn.anchorMin = sn.anchorMax = new Vector2(1f, 1f);
        sn.pivot = new Vector2(1f, 0.5f);
        sn.anchoredPosition = new Vector2(-260f, -53f);
        sn.sizeDelta = new Vector2(600f, 62f);

        difficultyText = NewText("Difficulty", root, "LUNATIC", 32f, Cyan,
            TextAlignmentOptions.MidlineRight);
        RectTransform df = (RectTransform)difficultyText.transform;
        df.anchorMin = df.anchorMax = new Vector2(1f, 1f);
        df.pivot = new Vector2(1f, 0.5f);
        df.anchoredPosition = new Vector2(HeaderStageRowRight, -53f);
        df.sizeDelta = new Vector2(400f, 50f);
    }

    // 「石工 LUNATIC」1行の横組みを確定する。難易度の実測幅からステージ名の
    // 右端を決め、和文が CJK フォールバックの行メトリクスで上に乗る分は
    // 両ラベルともインク実測で帯の縦中央へ補正する(TmpAlign は冪等)。
    private void LayoutHeaderStageRow()
    {
        float difficultyWidth = difficultyText.GetPreferredValues(difficultyText.text).x;
        RectTransform sn = (RectTransform)stageNameText.transform;
        sn.anchoredPosition = new Vector2(
            HeaderStageRowRight - difficultyWidth - HeaderStageRowGap,
            sn.anchoredPosition.y);
        TmpAlign.CenterInkVertically(stageNameText);
        TmpAlign.CenterInkVertically(difficultyText);
    }

    // ヘッダー帯セグメント用: 画面左上アンカーで x 位置と幅・高さを与える。
    private static void SetTopBand(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    // 装飾十字（ラテン十字・横木は上寄り）。縦木/横木＋4先端の菱形フィニアル。
    // pos はヘッダー左上基準（AddQuad は中央アンカーのため、左上アンカーの
    // コンテナを挟んで交点位置を固定する。v7 まで中央基準で解釈され
    // ヘッダーに出ていなかった不具合の修正）。
    // ユーザー指摘(2026-07-10)で全体を約2割縮小し、銀2色＋シアン宝石を
    // やめて白の単色に統一（宝石は同色化で無意味になるため廃止）。
    private void BuildCrossIcon(RectTransform root, Vector2 pos)
    {
        GameObject holder = NewRect("CrossIcon", root);
        RectTransform hr = (RectTransform)holder.transform;
        hr.anchorMin = hr.anchorMax = new Vector2(0f, 1f);
        hr.pivot = new Vector2(0.5f, 0.5f);
        hr.anchoredPosition = pos;
        hr.sizeDelta = Vector2.zero;

        const float up = 18f, down = 29f, halfW = 16f, thick = 5f, dia = 10f;
        AddQuad(hr, "CrossV", Color.white, new Vector2(0f, (up - down) * 0.5f),
            new Vector2(thick, up + down), 0f);
        AddQuad(hr, "CrossH", Color.white, Vector2.zero, new Vector2(halfW * 2f, thick), 0f);
        AddQuad(hr, "CrossTip", Color.white, new Vector2(0f, up), new Vector2(dia, dia), 45f);
        AddQuad(hr, "CrossTip", Color.white, new Vector2(0f, -down), new Vector2(dia, dia), 45f);
        AddQuad(hr, "CrossTip", Color.white, new Vector2(-halfW, 0f), new Vector2(dia, dia), 45f);
        AddQuad(hr, "CrossTip", Color.white, new Vector2(halfW, 0f), new Vector2(dia, dia), 45f);
    }

    private void BuildEvaluation(RectTransform root)
    {
        // ランク文字を包む青系のソフトグロー（外=青 / 内=シアンで層にする）。
        // モックアップの紫グローに替えてユーザー指定の青フレアにする。
        // S はモック実測どおり画面中心 (0,0) に置く。
        rankAuraOuter = NewGraphic<SoftCircleGraphic>("RankAuraOuter", root);
        rankAuraOuter.color = new Color(BrandBlue.r, BrandBlue.g, BrandBlue.b, 0.07f);
        SetRect(rankAuraOuter.rectTransform, Vector2.zero, new Vector2(400f, 400f));

        rankAuraInner = NewGraphic<SoftCircleGraphic>("RankAuraInner", root);
        rankAuraInner.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.10f);
        SetRect(rankAuraInner.rectTransform, Vector2.zero, new Vector2(250f, 250f));

        // 中央判定を囲む多重の菱形ライン（細いリングを層にして、外ほど暗く沈め、
        // 内ほどシアンで明るくする。サイズはモック実測の最大リング≈450 に合わせる）。
        // 主リング（392）をモックの主菱形（実測≈390）に対応させる。白フレームより
        // 目立たない明度に抑え、外側に暗いリングを重ねて奥行きを出す（oracle 指摘）。
        Vector2 ringCenter = new Vector2(0f, 25f);
        float[] ringSizes = { 500f, 448f, 392f, 344f, 300f, 268f };
        Color[] ringColors =
        {
            new Color(0.070f, 0.085f, 0.120f, 0.12f),  // 最外・ごく暗く
            new Color(0.070f, 0.085f, 0.120f, 0.22f),
            new Color(Cyan.r, Cyan.g, Cyan.b, 0.36f),  // 主リング
            new Color(Cyan.r, Cyan.g, Cyan.b, 0.30f),
            new Color(0.050f, 0.165f, 0.345f, 0.30f),
            new Color(0.070f, 0.085f, 0.120f, 0.26f),
        };
        for (int i = 0; i < ringSizes.Length; i++)
        {
            Image ring = NewImage("RankRing", root, ringColors[i]);
            ring.sprite = diamondRingSprite;
            ring.type = Image.Type.Simple;
            SetRect(ring.rectTransform, ringCenter, Vector2.one * ringSizes[i]);
        }
        // 内リングの左右頂点に小さな菱形ノード（モックのリング頂点装飾）。
        Color ringNode = new Color(Cyan.r, Cyan.g, Cyan.b, 0.7f);
        AddQuad(root, "RingNode", ringNode, ringCenter + new Vector2(-135f, 0f), new Vector2(6f, 6f), 45f);
        AddQuad(root, "RingNode", ringNode, ringCenter + new Vector2(135f, 0f), new Vector2(6f, 6f), 45f);

        // S の背後を薄く沈める菱形（不透明にすると重いので低アルファで浮かせる）。
        Image backdrop = NewImage("RankBackdrop", root, new Color(0.003f, 0.010f, 0.030f, 0.32f));
        backdrop.sprite = cardFillSprite;
        backdrop.type = Image.Type.Sliced;
        SetRect(backdrop.rectTransform, new Vector2(0f, 10f), Vector2.one * 280f);
        backdrop.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

        // 評価エリアの八角形フレーム（モック再実測 2026-07-10: 側辺 ±278・高さ604・
        // 中心(0,42)・面取り脚58・各辺とも中央部で途切れる開放構造）。
        BuildOctagonFrame(root, new Vector2(0f, 42f), 278f, 302f, 58f);

        // 左右のシェブロン（モック実測: 細い白の開放シェブロン＋内側に青いソリッド）。
        BuildSideChevrons(root, -1f, 25f);
        BuildSideChevrons(root, 1f, 25f);

        // S 下の小さなノードクラスタ（モック準拠: 中空菱形＋芯）。
        Color nodeRing = new Color(0.52f, 0.58f, 0.70f, 0.8f);
        Color nodeCore = new Color(0.72f, 0.80f, 0.92f, 0.95f);
        Image bring = NewImage("RankNodeRing", root, nodeRing);
        bring.sprite = diamondRingSprite;
        bring.type = Image.Type.Simple;
        SetRect(bring.rectTransform, new Vector2(0f, -190f), new Vector2(20f, 20f));
        AddQuad(root, "RankNodeCore", nodeCore, new Vector2(0f, -214f), new Vector2(7f, 7f), 45f);
        AddQuad(root, "RankNodeCore", nodeCore, new Vector2(0f, -230f), new Vector2(5f, 5f), 45f);

        // シェブロン内側の tech 装飾（モック実測: 中空の正方形クラスタ。
        // 左=マゼンタ、右=青。サイズ違いを散らす）。菱形リングスプライトを
        // 45°回して軸平行の正方形アウトラインとして使う。
        Color techMagenta = new Color(0.55f, 0.10f, 0.60f, 0.8f);
        Color techBlue = new Color(0.10f, 0.35f, 0.85f, 0.8f);
        Vector4[] techSq =   // x, y, サイズ, 塗り(0=中空/1=ソリッド)
        {
            new Vector4(-238f, 55f, 10f, 0f), new Vector4(-215f, 28f, 16f, 0f),
            new Vector4(-241f, 2f, 8f, 1f), new Vector4(-212f, -22f, 12f, 0f),
            new Vector4(238f, 55f, 10f, 0f), new Vector4(215f, 28f, 16f, 0f),
            new Vector4(241f, 2f, 8f, 1f), new Vector4(212f, -22f, 12f, 0f),
        };
        for (int i = 0; i < techSq.Length; i++)
        {
            Color tc = techSq[i].x < 0f ? techMagenta : techBlue;
            Vector2 pos = ringCenter + new Vector2(techSq[i].x, techSq[i].y);
            if (techSq[i].w > 0.5f)
            {
                AddQuad(root, "TechSq", tc, pos, Vector2.one * techSq[i].z, 0f);
            }
            else
            {
                Image sq = NewImage("TechSq", root, tc);
                sq.sprite = diamondRingSprite;
                sq.type = Image.Type.Simple;
                SetRect(sq.rectTransform, pos, Vector2.one * (techSq[i].z * 1.41f));
                sq.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }
        }

        verdictText = NewText("Verdict", root, "総合判定\n<size=19><color=#38C2E0>OVERALL EVALUATION</color></size>",
            34f, Color.white, TextAlignmentOptions.Center);
        SetRect((RectTransform)verdictText.transform, new Vector2(0f, 296f), new Vector2(500f, 96f));

        // 見出し漢字のルビ（曲選択様式）。読みはクリア/失敗で Prepare が差し替える。
        // 漢字行のインク実測(中心 y≈313・34px)の直上に置く。
        verdictRubyText = NewText("VerdictRuby", root, "そうごうはんてい", 13f,
            new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Center);
        SetRect((RectTransform)verdictRubyText.transform, new Vector2(0f, 338f), new Vector2(300f, 18f));
        BindRuby(verdictText, verdictRubyText, 0, 4); // 「総合判定」/「攻略失敗」

        // スタンプ着地時に外へ広がる波紋リング（通常時は透明）。
        rippleA = NewImage("StampRippleA", root, Color.clear);
        rippleA.sprite = diamondRingSprite;
        rippleA.type = Image.Type.Simple;
        SetRect(rippleA.rectTransform, ringCenter, Vector2.one * 320f);
        rippleB = NewImage("StampRippleB", root, Color.clear);
        rippleB.sprite = diamondRingSprite;
        rippleB.type = Image.Type.Simple;
        SetRect(rippleB.rectTransform, ringCenter, Vector2.one * 320f);

        // ランク文字とフレアは入場のスタンプ演出でまとめて拡大→着地させる。
        rankGroup = NewGroup("RankGroup", root, out rankGroupRect);

        // ランク文字の背後に青系フレア（8方向の光条＋コア）。グレースケールの
        // スターバーストを大=青・小=シアンで二重に敷き、シアン→青のグラデにする。
        // 背後の菱形リングが埋もれないよう控えめに（codex 指摘反映）。
        rankFlareBlue = NewImage("RankFlareBlue", rankGroupRect, new Color(BrandBlue.r, BrandBlue.g, BrandBlue.b, 0.45f));
        rankFlareBlue.sprite = flareSprite;
        rankFlareBlue.type = Image.Type.Simple;
        SetRect(rankFlareBlue.rectTransform, Vector2.zero, new Vector2(640f, 640f));

        rankFlareCyan = NewImage("RankFlareCyan", rankGroupRect, new Color(Cyan.r, Cyan.g, Cyan.b, 0.52f));
        rankFlareCyan.sprite = flareSprite;
        rankFlareCyan.type = Image.Type.Simple;
        SetRect(rankFlareCyan.rectTransform, Vector2.zero, new Vector2(400f, 400f));

        // ランク文字。セリフ体（Playfair Display）でモック実測（字幅230 x 字高336・
        // 画面中心）に一致させる。Play 内レンダ実測でキャリブレーション済み:
        // fontSize 469 → h=340、素の Playfair はモックよりわずかに細いため x1.09。
        rankText = NewText("Rank", rankGroupRect, "S", 469f, Color.white, TextAlignmentOptions.Center);
        if (rankFont != null)
        {
            rankText.font = rankFont;
            rankText.fontStyle = FontStyles.Normal;
            rankText.rectTransform.localScale = new Vector3(1.09f, 1f, 1f);
            // モックの発光する文字面を TMP アンダーレイで再現（青のソフトグロー。
            // ScaleRatioC を明示しないと巨大サイズでアンダーレイが破綻する）。
            rankGlowMat = rankText.fontMaterial;
            rankGlowMat.EnableKeyword("UNDERLAY_ON");
            rankGlowMat.SetFloat(ShaderUtilities.ID_ScaleRatio_C, 1f);
            rankGlowMat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
            rankGlowMat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, 0f);
            rankGlowMat.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.08f);
            rankGlowMat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.42f);
            rankGlowMat.SetColor(ShaderUtilities.ID_UnderlayColor, RankGlowBlue);
            rankText.UpdateMeshPadding();
        }
        else
        {
            // セリフ資産が無い場合は従来の近似（太字＋横幅圧縮）。
            rankText.fontStyle = FontStyles.Bold;
            rankText.rectTransform.localScale = new Vector3(0.92f, 1f, 1f);
        }
        rankText.enableAutoSizing = false;
        rankText.outlineColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.5f);
        rankText.outlineWidth = 0.03f;
        // 塗りは純白一色でなく、下端をごく淡いシアン寄りに（モックの質感）。
        rankText.enableVertexGradient = true;
        rankText.colorGradient = new VertexGradient(
            Color.white, Color.white,
            new Color(0.86f, 0.94f, 1f, 1f), new Color(0.86f, 0.94f, 1f, 1f));
        SetRect((RectTransform)rankText.transform, new Vector2(0f, 23f), new Vector2(560f, 480f));

        // 2P(その2): 2 人分のランクを中央に並べるための 2 つ目のランク字と P1/P2 タグ。
        // いずれも既定は非アクティブ=1P では一切描画されず現行と完全一致。位置/縮小/
        // 色は 2P の Prepare(ApplyTwoPlayerResult)で確定させる。
        rankText2 = NewText("Rank2", rankGroupRect, "S", 469f, Color.white, TextAlignmentOptions.Center);
        if (rankFont != null)
        {
            rankText2.font = rankFont;
            rankText2.fontStyle = FontStyles.Normal;
            rankGlowMat2 = rankText2.fontMaterial;
            rankGlowMat2.EnableKeyword("UNDERLAY_ON");
            rankGlowMat2.SetFloat(ShaderUtilities.ID_ScaleRatio_C, 1f);
            rankGlowMat2.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
            rankGlowMat2.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, 0f);
            rankGlowMat2.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.08f);
            rankGlowMat2.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.42f);
            rankGlowMat2.SetColor(ShaderUtilities.ID_UnderlayColor, RankGlowBlue);
            rankText2.UpdateMeshPadding();
        }
        else
        {
            rankText2.fontStyle = FontStyles.Bold;
            rankText2.rectTransform.localScale = new Vector3(0.92f, 1f, 1f);
        }
        rankText2.enableAutoSizing = false;
        rankText2.outlineColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.5f);
        rankText2.outlineWidth = 0.03f;
        rankText2.enableVertexGradient = true;
        rankText2.colorGradient = new VertexGradient(
            Color.white, Color.white,
            new Color(0.86f, 0.94f, 1f, 1f), new Color(0.86f, 0.94f, 1f, 1f));
        SetRect((RectTransform)rankText2.transform, new Vector2(0f, 23f), new Vector2(560f, 480f));
        rankText2.gameObject.SetActive(false);

        // P1/P2 見出し(各ランクの上・トーン色。HUD と同じ 温色/シアン)。
        p1RankTag = NewText("P1Tag", rankGroupRect, "1P", 44f,
            new Color(1f, 0.80f, 0.40f, 1f), TextAlignmentOptions.Center);
        SetRect((RectTransform)p1RankTag.transform, new Vector2(-225f, 158f), new Vector2(180f, 56f));
        p1RankTag.gameObject.SetActive(false);
        p2RankTag = NewText("P2Tag", rankGroupRect, "2P", 44f,
            new Color(0.45f, 0.85f, 1f, 1f), TextAlignmentOptions.Center);
        SetRect((RectTransform)p2RankTag.transform, new Vector2(225f, 158f), new Vector2(180f, 56f));
        p2RankTag.gameObject.SetActive(false);
    }

    // 評価エリアを囲む八角形の細いフレーム。上下辺＋45°面取り＋左右辺
    // （左右辺は中央のシェブロン部で途切れる）。面取り内側に青アクセントと
    // ノード菱形、上辺中央に中空菱形ノードを添える（モックアップ準拠）。
    private void BuildOctagonFrame(RectTransform root, Vector2 c, float hw, float hh, float leg)
    {
        Color frameCol = new Color(BracketWhite.r, BracketWhite.g, BracketWhite.b, 0.9f);
        // 左右辺は淡く（モックは中央へ向けて消え込む。カードとの分離感も出る）。
        Color sideCol = new Color(BracketWhite.r, BracketWhite.g, BracketWhite.b, 0.42f);
        const float thick = 2.2f;
        float topY = c.y + hh, botY = c.y - hh;
        float edgeHalf = hw - leg;                  // 上下辺の半長
        float sideTop = topY - leg, sideBot = botY + leg;

        // 上下の水平辺はモック実測どおり中央に大きな切れ目を置く
        // （上辺は ±88..192、下辺は ±115..185 のみ。中央はノード菱形が浮かぶ）。
        for (int s = -1; s <= 1; s += 2)
        {
            AddQuad(root, "OctFrameTop", frameCol,
                new Vector2(c.x + s * 140f, topY), new Vector2(104f, thick), 0f);
            AddQuad(root, "OctFrameBottom", frameCol,
                new Vector2(c.x + s * 150f, botY), new Vector2(70f, thick), 0f);
        }

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

            // 左右辺（シェブロン部 y=c.y±110 に切れ目・淡色）。
            float gapTop = c.y + 110f, gapBot = c.y - 110f;
            AddQuad(root, "OctFrameSideU", sideCol,
                new Vector2(c.x + s * hw, (sideTop + gapTop) * 0.5f),
                new Vector2(thick, sideTop - gapTop), 0f);
            AddQuad(root, "OctFrameSideL", sideCol,
                new Vector2(c.x + s * hw, (gapBot + sideBot) * 0.5f),
                new Vector2(thick, gapBot - sideBot), 0f);

            // 面取り内側の青アクセント（平行線＋端ノード。白フレームより控えめに）。
            Color accent = new Color(AccentBlue.r, AccentBlue.g, AccentBlue.b, 0.65f);
            Vector2 inset = new Vector2(s * -13f, -13f);
            Vector2 chamMid = new Vector2(c.x + s * (edgeHalf + leg * 0.5f), topY - leg * 0.5f);
            AddQuad(root, "OctAccent", accent, chamMid + inset, new Vector2(diagLen * 0.9f, 3.2f), s * -45f);
            AddQuad(root, "OctAccentNode", accent, chamMid + inset * 2.1f, new Vector2(7f, 7f), 45f);
            Vector2 insetB = new Vector2(s * -13f, 13f);
            Vector2 chamMidB = new Vector2(c.x + s * (edgeHalf + leg * 0.5f), botY + leg * 0.5f);
            AddQuad(root, "OctAccent", accent, chamMidB + insetB, new Vector2(diagLen * 0.7f, 3.2f), s * 45f);
        }

        // 上辺中央のノード（中空菱形＋小さな芯）。芯は v9 まで直下(topY-22)にあり
        // 「総合判定」の漢字行と重なっていた（ユーザー指摘 2026-07-10）。ルビも
        // 入るため、クラスタごと上辺の上側へ逃がして文字域から完全に外す。
        Color nodeRing = new Color(0.52f, 0.58f, 0.70f, 0.9f);
        Image tring = NewImage("OctTopNode", root, nodeRing);
        tring.sprite = diamondRingSprite;
        tring.type = Image.Type.Simple;
        SetRect(tring.rectTransform, new Vector2(c.x, topY + 18f), new Vector2(18f, 18f));
        AddQuad(root, "OctTopNodeCore", new Color(0.72f, 0.80f, 0.92f, 0.9f),
            new Vector2(c.x, topY + 40f), new Vector2(6f, 6f), 45f);

        // 上下辺セグメントの内側端に置く小菱形（開放端のアクセント。oracle 指摘）。
        Color endNode = new Color(0.52f, 0.58f, 0.70f, 0.7f);
        for (int s = -1; s <= 1; s += 2)
        {
            AddQuad(root, "OctEdgeEnd", endNode, new Vector2(c.x + s * 82f, topY), new Vector2(5f, 5f), 45f);
            AddQuad(root, "OctEdgeEnd", endNode, new Vector2(c.x + s * 109f, botY), new Vector2(5f, 5f), 45f);
        }
    }

    private void BuildStats(RectTransform root)
    {
        // 位置・サイズは 2026-07-10 のモック再実測（1080ref）:
        //   上段カード外周 x=-697..-333（幅366）→ 中心 ±515、
        //   下段はモック実測では約31px 中央寄り（±484）だが、ユーザー指摘
        //   (2026-07-10「下段を中央からもう少し離す」)で ±500 へ +16px 外出し。
        //   中央フレーム側辺 ±278 に対し内側間隔は上段 54 / 下段 39。
        // 旧値(430x248・±495)は v8 の実測ミスで幅が広く、間隔が 7px しかなかった。
        // ルビは曲選択ヘッダーの様式（白 α0.85・漢字ブロック直上・本文比 ≈0.37）。
        scoreText = BuildStatCard(root, "Score", new Vector2(-515f, 185f), "スコア", "SCORE", "000,000", StatIcon.Crosshair, null, 0f);
        hitText = BuildStatCard(root, "Hit", new Vector2(515f, 185f), "被弾回数", "HIT COUNT", "00", StatIcon.Shield, "ひだんかいすう", 50f);
        counterText = BuildStatCard(root, "Counter", new Vector2(-500f, -175f), "カウンター回数", "COUNTER COUNT", "00", StatIcon.Swords, "かいすう", 110f);
        timeText = BuildStatCard(root, "Time", new Vector2(500f, -175f), "時間", "TIME", "00:00", StatIcon.Clock, "じかん", 50f);
    }

    private TMP_Text BuildStatCard(RectTransform root, string name, Vector2 pos,
        string jp, string en, string value, StatIcon icon, string ruby, float rubyX)
    {
        GameObject card = NewRect(name + "Card", root);
        RectTransform rect = (RectTransform)card.transform;
        Vector2 size = new Vector2(366f, 248f);
        SetRect(rect, pos, size);

        // 入場演出用: カード単位でフェード＋スライドできるよう控えておく。
        int cardIndex = builtCardCount;
        if (builtCardCount < cardRects.Length)
        {
            cardRects[builtCardCount] = rect;
            cardGroups[builtCardCount] = card.AddComponent<CanvasGroup>();
            cardHomes[builtCardCount] = pos;
            cardHomeOriginal[builtCardCount] = pos;
            builtCardCount++;
        }

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
        SetRect(chip.rectTransform, new Vector2(-100f, 53f), new Vector2(68f, 78f));

        Image iconImg = NewImage("Icon", rect, IconWarmWhite);
        iconImg.sprite = statIconSprites[(int)icon];
        iconImg.type = Image.Type.Simple;
        // 双剣だけ線密度が高く一段強く見えるため 6% 縮小（oracle 指摘）。
        float iconSize = icon == StatIcon.Swords ? 43f : 46f;
        SetRect(iconImg.rectTransform, new Vector2(-100f, 53f), new Vector2(iconSize, iconSize));
        if (cardIndex >= 0 && cardIndex < statIcons.Length) statIcons[cardIndex] = iconImg;

        // ラベルはチップ右の領域で中央揃え。カード幅 366 化に伴いフォントを
        // モック実測（jp≈30・en≈17）へ。最長「カウンター回数」(7字×30=210)が
        // チップ右端(-66)と内側面取り(+170)の間に収まる中心 +50 に置く
        // （旧 430 幅ではラベル先頭がチップに重なっていた欠陥も同時に解消）。
        TMP_Text label = NewText("Label", rect,
            jp + "\n<size=17><color=#38C2E0>" + en + "</color></size>",
            30f, new Color(0.78f, 0.80f, 0.84f, 1f), TextAlignmentOptions.Center);
        SetRect((RectTransform)label.transform, new Vector2(50f, 44f), new Vector2(240f, 84f));

        // 漢字部分のルビ。rubyX は算術による初期 x（フォールバック）。実配置は
        // Prepare の PlaceAllRubies で label の JP 語範囲 [0, jp.Length) の漢字
        // グリフ実測中心へ精密化する（「カウンター回数」は「回数」だけに乗る）。
        TMP_Text rubyText = null;
        if (!string.IsNullOrEmpty(ruby))
        {
            rubyText = NewText("Ruby", rect, ruby, 11f,
                new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Center);
            SetRect((RectTransform)rubyText.transform, new Vector2(rubyX, 79f), new Vector2(220f, 16f));
            BindRuby(label, rubyText, 0, jp.Length);
        }

        // 2P の再ラベル用にラベル/ルビ参照と元ラベル文字列を控える(1P では未使用=描画不変)。
        if (cardIndex >= 0 && cardIndex < statLabels.Length)
        {
            statLabels[cardIndex] = label;
            statRubies[cardIndex] = rubyText;
            statLabelOriginal[cardIndex] = label.text;
        }

        // 見出し下の細い区切り線（中央ノード＋両端ターミナル付き）。
        BuildDivider(rect, new Vector2(0f, -16f), 220f);

        TMP_Text valueText = NewText("Value", rect, value, 50f, Color.white, TextAlignmentOptions.Center);
        valueText.characterSpacing = 4f;
        SetRect((RectTransform)valueText.transform, new Vector2(-2f, -66f), new Vector2(330f, 74f));
        return valueText;
    }

    private void BuildDivider(RectTransform card, Vector2 pos, float width)
    {
        Image line = NewImage("Rule", card, DividerTan);
        SetRect(line.rectTransform, pos, new Vector2(width, 2f));
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

    // リザルト下部の 2 択ボタン。左=「ステージ選択」(曲選択へ戻る=従来挙動) /
    // 右=「プレイを終わる」(タイトルへ)。スティック左右で選択・ボタンで決定する
    // (2026-07-13 指摘: リザルト→タイトル導線)。入場演出は親 ActionRow をまとめて
    // フェード/スライドさせる(exitRect/buttonGroup がその親)。ボタンの見た目・
    // 焼き込みスプライト(660x120)・白スラッシュ様式は従来のまま(横に 2 枚並べる)。
    private void BuildActionButtons(RectTransform root)
    {
        GameObject rowGo = NewRect("ActionRow", root);
        RectTransform rowRect = (RectTransform)rowGo.transform;
        SetRect(rowRect, new Vector2(0f, -433f), new Vector2(1500f, 140f));
        exitRect = rowRect;                              // 入場スライドの単位(親ごと動かす)
        exitHome = rowRect.anchoredPosition;
        buttonGroup = rowGo.AddComponent<CanvasGroup>(); // 2 ボタンをまとめてフェード

        BuildActionButton(0, rowRect, new Vector2(-352f, 0f), "ステージ選択",
            Action.StageSelect, null, 0, 0);
        BuildActionButton(1, rowRect, new Vector2(352f, 0f), "プレイを終わる",
            Action.Title, "お", 0, 7);   // 漢字「終」にルビ

        selectedActionIndex = 0;
        RefreshActionSelection();
    }

    private void BuildActionButton(int slot, RectTransform row, Vector2 pos, string labelText,
        Action action, string rubyText, int rubyStart, int rubyLen)
    {
        GameObject buttonGo = NewRect($"Action{slot}", row);
        RectTransform rect = (RectTransform)buttonGo.transform;
        SetRect(rect, pos, new Vector2(660f, 120f));
        actionRects[slot] = rect;
        actionValues[slot] = action;
        CanvasGroup cg = buttonGo.AddComponent<CanvasGroup>();   // 選択状態の減光用
        actionGroups[slot] = cg;

        // ボタン背後の淡い青グロー（モックの発光感。中央ランクと競合しないよう
        // oracle 指摘で 25% 減光）。
        SoftCircleGraphic glow = NewGraphic<SoftCircleGraphic>("Glow", rect);
        glow.color = new Color(BrandBlue.r, BrandBlue.g, BrandBlue.b, 0.075f);
        SetRect(glow.rectTransform, Vector2.zero, new Vector2(760f, 190f));

        Image body = NewImage("Body", rect, Color.white);
        body.sprite = exitButtonSprite;
        body.type = Image.Type.Simple;
        Stretch(body.rectTransform);
        body.raycastTarget = true;

        // 白スラッシュ（難易度選択/タイトルの White マーカーと同じ語彙。
        // 左右対称・4本とも 19° で平行）。見た目の正は Docs/result-design-language.md。
        UiButtonStyle.AddSlashPair(rect, 660f, 120f);

        TMP_Text label = NewText("Label", rect, labelText,
            UiButtonStyle.LabelSizeResult, Color.white, TextAlignmentOptions.Center);
        Stretch((RectTransform)label.transform);

        if (rubyText != null)
        {
            // ルビ（曲選択様式）。label の子にして光学中央補正へ追従させる。x は算術を
            // 初期値に、Prepare の PlaceAllRubies で漢字グリフ実測中心へ精密化する。
            TMP_Text ruby = NewText("Ruby", (RectTransform)label.transform, rubyText, 14f,
                new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Center);
            SetRect((RectTransform)ruby.transform, new Vector2(38f, 28f), new Vector2(40f, 18f));
            BindRuby(label, ruby, rubyStart, rubyLen);
        }
        if (slot == 1) exitLabel = label;   // Prepare の再インク中央補正の代表

        // 日本語は CJK フォールバックの行メトリクスで上に乗るため、インク実測で
        // 光学中央へ補正する。ビルド時(非アクティブ)は空振りし得るので Prepare でも再実行。
        TmpAlign.CenterInkVertically(label);

        Button button = buttonGo.AddComponent<Button>();
        button.targetGraphic = body;
        button.transition = Selectable.Transition.None;
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;
        int captured = slot;
        button.onClick.AddListener(() => { SetActionSelection(captured); RequestAction(actionValues[captured]); });

        EventTrigger trigger = buttonGo.AddComponent<EventTrigger>();
        AddTrigger(trigger, EventTriggerType.PointerEnter, _ => SetActionSelection(captured));
    }

    // 選択中のボタンを拡大＋明るく、非選択を縮小＋減光する。
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
                actionRects[i].localScale = Vector3.one * (sel ? 1.03f : 0.965f);
            if (actionGroups[i] != null)
                actionGroups[i].alpha = sel ? 1f : 0.5f;
        }
    }

    public void Prepare(StageData stage, int difficulty, bool cleared, int hitCount,
        int counterCount, float elapsedSeconds, float endSeconds,
        bool twoPlayer = false, int hitCount2 = 0)
    {
        gameObject.SetActive(true);
        inputArmed = false;
        entering = false;
        // 2 択の初期選択は左(ステージ選択)。左右のエッジ検出もリセット。
        selectedActionIndex = 0;
        navLeftPrev = false;
        navRightPrev = false;
        RefreshActionSelection();
        // 表示確定後の再実行(冪等)。非アクティブ時の空振り対策(既知の TMP の罠)。
        TmpAlign.CenterInkVertically(exitLabel);

        string stageName = stage != null && !string.IsNullOrWhiteSpace(stage.stageName)
            ? stage.stageName
            : "UNKNOWN STAGE";
        stageNameText.text = stageName;
        difficultyText.text = DifficultyName(difficulty);
        LayoutHeaderStageRow();
        verdictText.text = cleared
            ? "総合判定\n<size=16><color=#38C2E0>OVERALL EVALUATION</color></size>"
            : "攻略失敗\n<size=16><color=#FF6C8B>STAGE FAILED</color></size>";
        if (verdictRubyText != null)
            verdictRubyText.text = cleared ? "そうごうはんてい" : "こうりゃくしっぱい";

        // 全ルビを対応漢字グリフの実測中心へ精密配置(表示確定後・冪等)。
        // 本文の text/サイズ確定後に呼ぶ必要があるためここで実行する。
        PlaceAllRubies();

        string rank = EvaluateRank(cleared, hitCount, difficulty);
        rankText.text = rank;
        // F は字形の質量が左に寄るため、光学中心へ +12px 右へ寄せる（oracle 指摘）。
        rankText.rectTransform.anchoredPosition = new Vector2(rank == "F" ? 12f : 0f, 23f);
        rankText.color = cleared ? Color.white : new Color(1f, 0.52f, 0.65f, 1f);
        rankText.outlineColor = cleared
            ? new Color(Cyan.r, Cyan.g, Cyan.b, 0.74f)
            : new Color(0.85f, 0.08f, 0.22f, 0.78f);

        // クリア＝青系フレア、失敗＝赤系に切り替える（グレースケールのフレア
        // スプライトをティントで着色）。
        Color flareBlue = cleared ? new Color(BrandBlue.r, BrandBlue.g, BrandBlue.b, 0.45f)
                                  : new Color(0.72f, 0.12f, 0.20f, 0.45f);
        Color flareCyan = cleared ? new Color(Cyan.r, Cyan.g, Cyan.b, 0.52f)
                                  : new Color(0.95f, 0.30f, 0.38f, 0.5f);
        if (rankFlareBlue != null) rankFlareBlue.color = flareBlue;
        if (rankFlareCyan != null) rankFlareCyan.color = flareCyan;
        if (rankGlowMat != null)
            rankGlowMat.SetColor(ShaderUtilities.ID_UnderlayColor,
                cleared ? RankGlowBlue : RankGlowRed);
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

        // 入場演出用に確定値と基準色を控え、全グループを隠した初期状態にする
        // （PlayEntrance が t=0 から段階的に出す。背景装飾は contentGroup 外なので
        // ピクセル遷移の開け中も見えている）。
        resultCleared = cleared;
        finalScore = provisionalScore;
        finalHit = Mathf.Max(0, hitCount);
        finalCounter = Mathf.Max(0, counterCount);
        finalElapsed = elapsedSeconds;
        flareBlueBase = flareBlue;
        flareCyanBase = flareCyan;

        // 2P: 左右分割へ上書き(中央 2 ランク+左=P1/右=P2 のスコア・被弾)。
        // 1P では実行せず twoPlayerResult=false=現行リザルトのまま。
        if (twoPlayer)
            ApplyTwoPlayerResult(cleared, hitCount, hitCount2,
                counterCount, elapsedSeconds, endSeconds, provisionalScore, difficulty);
        else if (twoPlayerLayoutApplied)
            RestoreOnePlayerLayout();     // 2P 表示後に 1P へ戻す経路のみ(純 1P は不変)
        else
            twoPlayerResult = false;

        contentGroup.alpha = 1f;
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.localScale = Vector3.one;
        StopEntranceRoutine();
        ApplyEntranceFrame(0f);
    }

    // 2P: リザルトを左右分割へ上書きする(1P では呼ばれない)。中央に P1/P2 の 2 ランク、
    // 左列(既存 Score/Counter カード)=P1 のスコア/被弾、右列(既存 Hit/Time カード)=
    // P2 のスコア/被弾。値のカウントアップは ApplyEntranceFrame の twoPlayerResult 分岐、
    // ラベルはここで再設定する。既存ウィジェットを流用するため額装・銀枠の様式は不変。
    private void ApplyTwoPlayerResult(bool cleared, int hit1, int hit2,
        int counterCount, float elapsedSeconds, float endSeconds, int score1, int difficulty)
    {
        twoPlayerResult = true;
        twoPlayerLayoutApplied = true;
        finalScore2 = CalculateProvisionalScore(cleared, hit2, counterCount, elapsedSeconds, endSeconds);
        finalHit2 = Mathf.Max(0, hit2);
        twoPlayerSidesReversed = GManager.Control != null && GManager.Control.PlayerSidesReversed;
        bool p1OnLeft = PlayerIndexForResultSide(false, twoPlayerSidesReversed) == 0;

        // --- 中央: 2 人分のランク(P1=左 / P2=右) ---
        // ランクを十分な大きさに保ちながら左右へ分け、1P/2P タグとの所属関係を示す。
        string rank1 = EvaluateRank(cleared, hit1, difficulty);
        string rank2 = EvaluateRank(cleared, hit2, difficulty);
        const float rankScale = 0.34f;
        const float rankOffset = 110f;
        string leftRank = p1OnLeft ? rank1 : rank2;
        string rightRank = p1OnLeft ? rank2 : rank1;
        rankText.text = leftRank;
        rankText.rectTransform.anchoredPosition = new Vector2(-rankOffset + (leftRank == "F" ? 4f : 0f), -6f);
        rankText.rectTransform.localScale = new Vector3(1.09f * rankScale, rankScale, 1f);
        if (rankText2 != null)
        {
            rankText2.gameObject.SetActive(true);
            rankText2.text = rightRank;
            rankText2.rectTransform.anchoredPosition = new Vector2(rankOffset + (rightRank == "F" ? 4f : 0f), -6f);
            rankText2.rectTransform.localScale = new Vector3(1.09f * rankScale, rankScale, 1f);
            rankText2.color = cleared ? Color.white : new Color(1f, 0.52f, 0.65f, 1f);
            rankText2.outlineColor = cleared
                ? new Color(Cyan.r, Cyan.g, Cyan.b, 0.74f)
                : new Color(0.85f, 0.08f, 0.22f, 0.78f);
            if (rankGlowMat2 != null)
                rankGlowMat2.SetColor(ShaderUtilities.ID_UnderlayColor, cleared ? RankGlowBlue : RankGlowRed);
        }
        if (p1RankTag != null) p1RankTag.gameObject.SetActive(true);
        if (p2RankTag != null) p2RankTag.gameObject.SetActive(true);
        if (p1RankTag != null)
        {
            p1RankTag.fontSize = 32f;
            ((RectTransform)p1RankTag.transform).anchoredPosition =
                new Vector2(p1OnLeft ? -rankOffset : rankOffset, 108f);
        }
        if (p2RankTag != null)
        {
            p2RankTag.fontSize = 32f;
            ((RectTransform)p2RankTag.transform).anchoredPosition =
                new Vector2(p1OnLeft ? rankOffset : -rankOffset, 108f);
        }

        // --- 左右カードの再ラベル+アイコン(左列=P1 / 右列=P2) ---
        // カードは物理的に流用するため、ラベルに合うアイコン(スコア=照準/被弾=盾)へ
        // 差し替える(既定のカウンター=双剣・時間=時計のままだと不一致になる)。
        SetTwoPlayerColumnLabels(0, 2, p1OnLeft ? 0 : 1);
        SetTwoPlayerColumnLabels(1, 3, p1OnLeft ? 1 : 0);
        SetCardIcon(0, StatIcon.Crosshair);
        SetCardIcon(2, StatIcon.Shield);
        SetCardIcon(1, StatIcon.Crosshair);
        SetCardIcon(3, StatIcon.Shield);

        // --- 2P はスコア/被弾カードを左右へ再配置 ---
        // 中央のランク枠と重ならないよう、1P と同じ外側の基準位置を使う。
        const float twoPTopX = 515f;
        const float twoPBottomX = 500f;
        SetTwoPlayerCardX(0, -twoPTopX);    // P1 スコア(左上)
        SetTwoPlayerCardX(2, -twoPBottomX); // P1 被弾(左下)
        SetTwoPlayerCardX(1, twoPTopX);     // P2 スコア(右上)
        SetTwoPlayerCardX(3, twoPBottomX);  // P2 被弾(右下)

        // --- 値の即時反映(カウントアップの最終状態と一致させる) ---
        int leftScore = p1OnLeft ? Mathf.Max(0, score1) : finalScore2;
        int leftHit = p1OnLeft ? Mathf.Max(0, hit1) : finalHit2;
        int rightScore = p1OnLeft ? finalScore2 : Mathf.Max(0, score1);
        int rightHit = p1OnLeft ? finalHit2 : Mathf.Max(0, hit1);
        scoreText.text = leftScore.ToString("N0");
        counterText.text = leftHit.ToString("00");
        hitText.text = rightScore.ToString("N0");
        timeText.text = rightHit.ToString("00");
    }

    // 2P: カードの水平位置を調整する(入場アニメの静止位置 cardHomes と実位置の
    // 両方を更新)。符号=スライド方向は保持されるため x の符号は呼び出し側で維持する。
    private void SetTwoPlayerCardX(int idx, float x)
    {
        if (idx < 0 || idx >= cardRects.Length || cardRects[idx] == null) return;
        cardHomes[idx].x = x;
        Vector2 pos = cardRects[idx].anchoredPosition;
        pos.x = x;
        cardRects[idx].anchoredPosition = pos;
    }

    public static int PlayerIndexForResultSide(bool rightSide, bool reversed)
    {
        return (rightSide ? 1 : 0) ^ (reversed ? 1 : 0);
    }

    private void SetTwoPlayerColumnLabels(int topIndex, int bottomIndex, int playerIndex)
    {
        bool p1 = playerIndex == 0;
        string tone = p1 ? "#FFCC66" : "#73D9FF";
        string tag = p1 ? "P1" : "P2";
        SetStatLabel(topIndex, tone, tag, "スコア", "SCORE");
        SetStatLabel(bottomIndex, tone, tag, "被弾", "HIT");
    }

    // カードラベルを「<tag> <jp>\n<en>」形式へ差し替え、対応するルビを隠す。
    private void SetStatLabel(int idx, string toneHex, string tag, string jp, string en)
    {
        if (idx < 0 || idx >= statLabels.Length) return;
        if (statRubies[idx] != null) statRubies[idx].gameObject.SetActive(false);
        if (statLabels[idx] == null) return;
        statLabels[idx].text = "<color=" + toneHex + ">" + tag + "</color> " + jp
            + "\n<size=17><color=#38C2E0>" + en + "</color></size>";
    }

    // カードのアイコンスプライトを差し替える(双剣のみ 6% 縮小の既定を踏襲)。
    private void SetCardIcon(int idx, StatIcon icon)
    {
        if (idx < 0 || idx >= statIcons.Length || statIcons[idx] == null) return;
        statIcons[idx].sprite = statIconSprites[(int)icon];
        float size = icon == StatIcon.Swords ? 43f : 46f;
        statIcons[idx].rectTransform.sizeDelta = new Vector2(size, size);
    }

    // 1P レイアウトへ確実に戻す(冪等)。純 1P セッションでは元と同一のため無変化=
    // 現行リザルト不変。2P 表示の後に同一インスタンスで 1P を出す経路(通常プレイでは
    // 発生しないが防御的に)でも正しく単独ランク+元ラベルへ復帰させる。
    private void RestoreOnePlayerLayout()
    {
        twoPlayerResult = false;
        if (rankText2 != null) rankText2.gameObject.SetActive(false);
        if (p1RankTag != null) p1RankTag.gameObject.SetActive(false);
        if (p2RankTag != null) p2RankTag.gameObject.SetActive(false);
        // ランク字は中央・原寸へ(位置は 1P Prepare が別途確定済み)。
        rankText.rectTransform.localScale = new Vector3(1.09f, 1f, 1f);
        // 2P で中央寄せしたカードを元の 1P 配置(±515/±500)へ戻す。
        for (int i = 0; i < cardRects.Length; i++)
        {
            if (cardRects[i] == null) continue;
            cardHomes[i] = cardHomeOriginal[i];
            cardRects[i].anchoredPosition = cardHomeOriginal[i];
        }
        for (int i = 0; i < statLabels.Length; i++)
        {
            if (statLabels[i] != null && statLabelOriginal[i] != null)
                statLabels[i].text = statLabelOriginal[i];
            if (statRubies[i] != null) statRubies[i].gameObject.SetActive(true);
            SetCardIcon(i, StatCardIcons[i]);
        }
        // ラベル/ルビを元へ戻したので実測配置を再実行(冪等)。
        PlaceAllRubies();
    }

    public void PlayEntrance()
    {
        if (!gameObject.activeSelf || entering) return;
        StopEntranceRoutine();
        entranceRoutine = StartCoroutine(EntranceRoutine());
    }

    // 録画デモ等の外部からスキップ可否を判定するための公開状態。
    public bool Entering => entering;

    private void StopEntranceRoutine()
    {
        if (entranceRoutine != null)
        {
            StopCoroutine(entranceRoutine);
            entranceRoutine = null;
        }
        entering = false;
    }

    // 入場中に決定/戻るが押されたときのスキップ。演出を省略して最終状態へ。
    // 鳴っている途中の演出音(カウント駆動音など)も映像に合わせて打ち切る。
    private void FinishEntranceImmediate()
    {
        StopEntranceRoutine();
        // BGM は止めない(映像だけスキップ、音楽は流し続ける)。
        ApplyEntranceFrame(9999f);
    }

    private float StampImpactTime()
    {
        return resultCleared
            ? EnterStampStartClear + EnterStampDurClear
            : EnterStampStartFail + EnterStampDurFail;
    }

    private IEnumerator EntranceRoutine()
    {
        entering = true;
        float total = StampImpactTime() + EnterTail;
        float t = 0f;
        ApplyEntranceFrame(0f);
        // 音ハメ: リザルト BGM(Killing Party 1:50〜)を DSP 時刻でスケジュール再生し、
        // 入場アニメの時刻を AudioSettings.dspTime に合わせて進める。ステージの
        // 弾幕が dspTime に同期するのと同じ流儀。描画クロック(captureFramerate や
        // フレーム落ち)に依らず、スタンプ着地(t=1.44)が曲の強アクセント(clip 1.44s)に
        // フレーム精度で一致し、Recorder が DSP 時計で取り込む音声とも録画上でずれない。
        bool useDspClock = bgmSource != null && resultBgm != null;
        double startDsp = 0d;
        if (useDspClock)
        {
            // 退出時のフェードアウトで 0 のままになっている場合があるので毎回戻す。
            bgmSource.volume = ResultBgmVolume;
            bgmSource.time = 0f;
            startDsp = AudioSettings.dspTime + BgmScheduleLead;
            bgmSource.PlayScheduled(startDsp);
        }
        while (t < total)
        {
            yield return null;
            if (useDspClock)
            {
                double elapsed = AudioSettings.dspTime - startDsp;
                t = elapsed > 0d ? (float)elapsed : 0f;   // 発音前はフレーム0で保持
            }
            else
            {
                t += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            }
            ApplyEntranceFrame(t);
        }
        ApplyEntranceFrame(9999f);
        entering = false;
        entranceRoutine = null;
    }

    // 入場シーケンスの時刻 t（秒）における全要素の状態を決める純関数。
    // スキップは大きな t を渡すだけで最終状態になる（コルーチン停止と併用）。
    private void ApplyEntranceFrame(float t)
    {
        bool cleared = resultCleared;
        float stampStart = cleared ? EnterStampStartClear : EnterStampStartFail;
        float stampDur = cleared ? EnterStampDurClear : EnterStampDurFail;
        float impact = stampStart + stampDur;
        float postT = t - impact;

        // (1) ヘッダー: 上から降りながらフェードイン。
        float hp = EaseOutCubic(t / EnterHeaderDur);
        headerGroup.alpha = hp;
        headerGroupRect.anchoredPosition = new Vector2(0f, 26f * (1f - hp));

        // (2) 情報カード: 左列は左から・右列は右から時差スライドイン。
        for (int i = 0; i < builtCardCount; i++)
        {
            float cp = EaseOutCubic((t - (EnterCardStart + i * EnterCardStagger)) / EnterCardDur);
            cardGroups[i].alpha = cp;
            float dir = cardHomes[i].x < 0f ? -1f : 1f;
            cardRects[i].anchoredPosition = cardHomes[i] + new Vector2(dir * 90f * (1f - cp), 0f);
        }

        // (3) 数値カウントアップ（下位桁が回って見えるよう毎フレーム再計算）。
        float np = Mathf.Clamp01((t - EnterCountStart) / EnterCountDur);
        float ne = 1f - (1f - np) * (1f - np);
        if (twoPlayerResult)
        {
            // 左列=P1(score/hit) / 右列=P2(score/hit)。カード割当:
            // scoreText=P1スコア, counterText=P1被弾, hitText=P2スコア, timeText=P2被弾。
            int leftScore = twoPlayerSidesReversed ? finalScore2 : finalScore;
            int leftHit = twoPlayerSidesReversed ? finalHit2 : finalHit;
            int rightScore = twoPlayerSidesReversed ? finalScore : finalScore2;
            int rightHit = twoPlayerSidesReversed ? finalHit : finalHit2;
            scoreText.text = Mathf.RoundToInt(leftScore * ne).ToString("N0");
            counterText.text = Mathf.RoundToInt(leftHit * ne).ToString("00");
            hitText.text = Mathf.RoundToInt(rightScore * ne).ToString("N0");
            timeText.text = Mathf.RoundToInt(rightHit * ne).ToString("00");
        }
        else
        {
            scoreText.text = Mathf.RoundToInt(finalScore * ne).ToString("N0");
            hitText.text = Mathf.RoundToInt(finalHit * ne).ToString("00");
            counterText.text = Mathf.RoundToInt(finalCounter * ne).ToString("00");
            timeText.text = FormatTime(finalElapsed * ne);
        }
        float pulse = ValuePulse(t - (EnterCountStart + EnterCountDur));
        scoreText.rectTransform.localScale = Vector3.one * pulse;
        hitText.rectTransform.localScale = Vector3.one * pulse;
        counterText.rectTransform.localScale = Vector3.one * pulse;
        timeText.rectTransform.localScale = Vector3.one * pulse;

        // (4) 中央装飾: わずかに縮みながらフェードイン。
        float ep = EaseOutCubic((t - EnterEvalStart) / EnterEvalDur);
        evalGroup.alpha = ep;
        evalGroupRect.localScale = Vector3.one * Mathf.Lerp(1.06f, 1f, ep);

        // (5) ランクのスタンプ: 大きく淡い状態から加速して着地。
        //     失敗時は開始倍率を抑え、時間を掛けて重く落とす。
        float sp = Mathf.Clamp01((t - stampStart) / stampDur);
        float se = cleared ? sp * sp * sp : sp * sp * sp * sp;
        // 接地の瞬間に最大発光が来るよう、落下中はやや暗く抑えて着地で 1.0 に
        // 引き上げる（oracle 指摘: 発光ピークと接地感の分散を防ぐ）。
        float appear = Mathf.Clamp01(sp / 0.30f);
        rankGroup.alpha = postT >= 0f ? 1f : appear * 0.88f;
        float stampScale = Mathf.Lerp(cleared ? 2.4f : 1.9f, 1f, se);
        Vector3 stampScaleVec;
        if (cleared)
        {
            // クリア: 着地直後に小さな沈み込み→復帰（紙へ押し込む感触）。
            if (postT > 0f && postT < 0.18f)
                stampScale = 1f - 0.015f * Mathf.Sin(Mathf.PI * postT / 0.18f);
            stampScaleVec = Vector3.one * stampScale;
        }
        else
        {
            // 失敗: 反発なし。接触の 0.1 秒だけ縦に潰れて戻る（重い着地）。
            stampScaleVec = Vector3.one * stampScale;
            if (postT > 0f && postT < 0.10f)
            {
                float sq = 1f - postT / 0.10f;
                stampScaleVec = new Vector3(
                    stampScale * (1f + 0.04f * sq),
                    stampScale * (1f - 0.08f * sq), 1f);
            }
        }
        rankGroupRect.localScale = stampScaleVec;

        // (6) 着地の一閃: フレアのアルファを持ち上げて減衰させる。
        float boost = 0f;
        if (postT > 0f)
        {
            float flashP = Mathf.Clamp01(postT / EnterFlashDur);
            boost = (1f - flashP) * (1f - flashP);
        }
        float flashGain = cleared ? 1.1f : 1.4f;
        rankFlareBlue.color = WithAlpha(flareBlueBase,
            Mathf.Min(1f, flareBlueBase.a * (1f + flashGain * boost)));
        rankFlareCyan.color = WithAlpha(flareCyanBase,
            Mathf.Min(1f, flareCyanBase.a * (1f + flashGain * boost)));

        // (7) 波紋リング: クリアはシアン2重、失敗は暗い赤1重で控えめに。
        Color rippleCol = cleared
            ? new Color(Cyan.r, Cyan.g, Cyan.b, 1f)
            : new Color(0.75f, 0.16f, 0.24f, 1f);
        // 既存の多重菱形（最大500）より十分外まで広げて波紋を認識しやすく。
        float rippleMax = cleared ? 920f : 680f;
        float rippleAlpha = cleared ? 0.50f : 0.30f;
        ApplyRipple(rippleA, postT, rippleCol, rippleMax, rippleAlpha);
        ApplyRipple(rippleB, cleared ? postT - 0.12f : -1f, rippleCol, rippleMax, rippleAlpha * 0.8f);

        // (8) 着地の揺れ（クリアは短く軽く、失敗は長く重く）。
        float shakeDur = cleared ? 0.30f : 0.42f;
        float shakeAmp = cleared ? 7f : 13f;
        float qp = postT / shakeDur;
        if (qp >= 0f && qp < 1f)
        {
            float decay = (1f - qp) * (1f - qp);
            contentRect.anchoredPosition = new Vector2(
                shakeAmp * decay * Mathf.Sin(qp * 43f),
                shakeAmp * decay * Mathf.Sin(qp * 57f + 1.7f) * 0.8f);
        }
        else
        {
            contentRect.anchoredPosition = Vector2.zero;
        }

        // (9) ボタン: スタンプが落ち着いてから下からフェードイン。
        float bp = EaseOutCubic((t - (impact + EnterButtonDelay)) / EnterButtonDur);
        buttonGroup.alpha = bp;
        exitRect.anchoredPosition = exitHome + new Vector2(0f, -14f * (1f - bp));
        bool interactive = bp >= 0.999f;
        buttonGroup.interactable = interactive;
        buttonGroup.blocksRaycasts = interactive;
    }

    private static void ApplyRipple(Image img, float time, Color col, float maxSize, float baseAlpha)
    {
        if (img == null) return;
        float p = time / EnterRippleDur;
        if (p < 0f || p >= 1f)
        {
            img.color = Color.clear;
            return;
        }
        img.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(320f, maxSize, EaseOutCubic(p));
        img.color = new Color(col.r, col.g, col.b, baseAlpha * (1f - p));
    }

    private static float EaseOutCubic(float p)
    {
        p = Mathf.Clamp01(p);
        return 1f - (1f - p) * (1f - p) * (1f - p);
    }

    // カウントアップ完了時のパルス（1→1.10→1。oracle 指摘で増幅）。
    private static float ValuePulse(float time)
    {
        const float dur = 0.16f;
        if (time <= 0f || time >= dur) return 1f;
        return 1f + 0.10f * Mathf.Sin(Mathf.PI * time / dur);
    }

    private static Color WithAlpha(Color c, float a)
    {
        return new Color(c.r, c.g, c.b, a);
    }

    // 2 択(左=ステージ選択 / 右=プレイを終わる→タイトル)。スティック左右で選択、
    // ボタンで決定。戻る(あれば)は即ステージ選択へ抜ける近道。left/right は押しっぱなし
    // 状態なので、立ち上がりエッジで 1 回だけ選択を動かす。
    public void Tick(bool left, bool right, bool buttonHeld, bool buttonPressed, bool backPressed)
    {
        if (!gameObject.activeSelf) return;

        // 入場中は決定/戻るで演出をスキップして最終状態へ（押し直すまで
        // 決定は発火させない）。他の入力は受けない。
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

        if (backPressed)
        {
            // 戻るは近道: 迷わずステージ選択へ(次の人がすぐ曲を選べる)。
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

        // 左右のエッジで選択移動(2 択なので端で止める)。
        if (left && !navLeftPrev) SetActionSelection(selectedActionIndex - 1);
        else if (right && !navRightPrev) SetActionSelection(selectedActionIndex + 1);
        navLeftPrev = left;
        navRightPrev = right;

        if (buttonPressed) RequestAction(actionValues[selectedActionIndex]);
    }

    public void HideImmediate()
    {
        StopEntranceRoutine();
        // リザルトを閉じるときは BGM も止める(タイトル/選択画面へ持ち越さない)。
        // 退出前に FadeOutBgmAsync を await していれば既に無音まで下がっている。
        if (bgmSource != null) bgmSource.Stop();
        gameObject.SetActive(false);
    }

    // リザルト BGM を duration 秒でフェードアウトする(退出前に await して使う)。
    // ぶつ切りを避けつつ、実際の停止は直後の HideImmediate が担う。
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

    // スコア = クリア +500,000 / カウンター ×20,000 / 被弾 ×10,000（clamp [0,999999]）。
    // 進行率係数(elapsedSeconds/endSeconds × 700,000)はユーザー指定で廃止。
    // elapsedSeconds/endSeconds は既存呼び出し互換のため残すが、この関数では未使用。
    public static int CalculateProvisionalScore(bool cleared, int hitCount, int counterCount,
        float elapsedSeconds, float endSeconds)
    {
        int score = cleared ? 500000 : 0;
        score += Mathf.Max(0, counterCount) * 20000;
        score -= Mathf.Max(0, hitCount) * 10000;
        return Mathf.Clamp(score, 0, 999999);
    }

    // 総合ランク。被弾のみで決めていたが、難易度別しきい値を導入し高難易度ほど被弾を
    // 許容する。S は全難易度で 0 被弾、未クリアは全難易度で F 固定。
    // difficulty は 0=EASY / 1=NORMAL / 2=LUNATIC（DifficultyName と同じ割当。HARD は
    // 存在しないため switch の default=NORMAL 相当へフォールバック）。
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

    private void RequestAction(Action action)
    {
        if (!gameObject.activeSelf) return;
        ActionRequested?.Invoke(action);
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

    // モック実測のシェブロン: 外側=細い白の開放「〈」（高さ82・線3.2）、
    // 内側=青いソリッドの「◀」型シェブロン（高さ50・腕厚13）。sign=-1 が左。
    // 頂点はモック再実測(2026-07-10)の ±305（フレーム側辺 278 より外・
    // 上段カード内側エッジ 333 との間）。
    private void BuildSideChevrons(RectTransform root, float sign, float cy)
    {
        Color white = new Color(0.95f, 0.97f, 1f, 0.92f);
        Vector2 wApex = new Vector2(sign * 305f, cy);
        AddChevronArm(root, wApex, wApex + new Vector2(-sign * 38f, 41f), 3.2f, white);
        AddChevronArm(root, wApex, wApex + new Vector2(-sign * 38f, -41f), 3.2f, white);

        Color blue = new Color(AccentBlue.r, AccentBlue.g, AccentBlue.b, 0.95f);
        Vector2 bApex = new Vector2(sign * 304f, cy);
        AddChevronArm(root, bApex, bApex + new Vector2(-sign * 26f, 25f), 13f, blue);
        AddChevronArm(root, bApex, bApex + new Vector2(-sign * 26f, -25f), 13f, blue);
    }

    private void AddChevronArm(RectTransform root, Vector2 a, Vector2 b, float thick, Color col)
    {
        Vector2 mid = (a + b) * 0.5f;
        float len = Vector2.Distance(a, b);
        float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
        AddQuad(root, "Chevron", col, mid, new Vector2(len, thick), ang);
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
        const int refW = 366, refH = 248;   // モック再実測(2026-07-10): 幅366
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
                // カード内側のネイビー面勾配（中央上寄りが明るく外周へ沈む。
                // oracle 指摘: 均一な黒だと平面的に見える）。
                float gx = (x - cx) / hw;
                float gy = (y - cy) / hh;
                float gd = Mathf.Sqrt(gx * gx * 0.6f + (gy - 0.28f) * (gy - 0.28f));
                float glow = Mathf.Clamp01(1f - gd * 0.9f);
                Blend(px, TW, TH, x, y, new Color(0.035f, 0.085f, 0.160f), inside * glow * glow * 0.45f);
                // 枠は均一色でなく、上辺へ向けて明るい銀へ寄せる（モックの金属感）。
                float topT = Mathf.Clamp01((y - cy) / hh);
                Color outCol = Color.Lerp(TexOutlineTan,
                    new Color(0.64f, 0.66f, 0.70f, 0.98f), topT * topT * 0.55f);
                Blend(px, TW, TH, x, y, outCol, Mathf.Clamp01(outlineHalf - Mathf.Abs(sdf) + 0.5f));
                Blend(px, TW, TH, x, y, TexInnerGold, Mathf.Clamp01(1.0f - Mathf.Abs(sdf + innerOffset)) * 0.7f);
            }
        }

        // 内側下辺の面取りに沿った太い青アクセント（背後に淡い青の滲みを敷く）。
        Vector2 a0, a1;
        if (outerLeft) { a0 = v[3]; a1 = v[4]; }   // 右下面取り
        else { a0 = v[5]; a1 = v[6]; }             // 左下面取り
        Vector2 am = (a0 + a1) * 0.5f;
        a0 = Vector2.Lerp(a0, am, 0.02f);
        a1 = Vector2.Lerp(a1, am, 0.02f);
        DrawLine(px, TW, TH, a0.x + cx, a0.y + cy, a1.x + cx, a1.y + cy, 26f * S,
            new Color(TexAccentBlue.r, TexAccentBlue.g, TexAccentBlue.b, 0.16f));
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
                // 視覚(sRGB)値で焼く（塗りはモック実測 (0,19,49) 系の紺）。
                Color fillColor = Color.Lerp(new Color(0.00f, 0.055f, 0.150f),
                    new Color(0.02f, 0.105f, 0.235f), ty);
                Blend(px, size, size, x, y, fillColor, fill * 0.95f);
                // 縁は金（ユーザー指定の「金縁チップ」。上辺ほど明るくして金属感）。
                float ring = Mathf.Clamp01(1.5f - Mathf.Abs(d));
                Color rimColor = Color.Lerp(TexChipGold,
                    new Color(0.85f, 0.75f, 0.52f, 1f), ty * 0.7f);
                Blend(px, size, size, x, y, rimColor, ring * 0.9f);
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

    // ボタン本体の焼き込み（青縦グラデ+銀枠+シアンリム）は UiButtonStyle に
    // 共通化（統一便・全画面で同一ソースから同じ見た目を得る）。
    // 見た目の正は Docs/result-design-language.md。
    private Sprite CreateExitButtonSprite()
    {
        return UiButtonStyle.CreateBodySprite(660, 120,
            generatedTextures, generatedSprites, "ResultExitButton");
    }

    // Material Symbols のスプライト読込。無ければ手続き生成へフォールバック。
    // どちらも白シルエットで、色は Image.color(IconWarmWhite)で与える。
    private Sprite LoadStatIconSprite(string resourceName, StatIcon fallback)
    {
        Sprite loaded = Resources.Load<Sprite>("UI/" + resourceName);
        return loaded != null ? loaded : CreateStatIconSprite(fallback);
    }

    // カード種別ごとの簡易ラインアイコン（クロスヘア/シールド/双剣/時計）。
    // Material Symbols 資産が無い環境向けのフォールバック(白ベーク)。
    private Sprite CreateStatIconSprite(StatIcon kind)
    {
        const int size = 96;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "ResultStatIcon_" + kind;
        texture.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[size * size];
        float c = (size - 1) * 0.5f;
        Color col = Color.white;
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

    // ルビと本文(漢字語範囲)を登録。実配置は PlaceAllRubies(Prepare)で行う。
    private void BindRuby(TMP_Text body, TMP_Text ruby, int start, int len)
    {
        if (body == null || ruby == null) return;
        rubyBinds.Add(new RubyBind
        {
            body = body,
            ruby = (RectTransform)ruby.transform,
            start = start,
            len = len,
        });
    }

    // 全ルビを対応漢字グリフの実測中心へ配置(表示確定後に呼ぶ)。
    private void PlaceAllRubies()
    {
        for (int i = 0; i < rubyBinds.Count; i++)
        {
            RubyBind b = rubyBinds[i];
            TmpAlign.PlaceRubyOverKanji(b.body, b.ruby, b.start, b.len);
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

    // 入場演出でまとめてフェード/スライドさせる全面コンテナ。
    private static CanvasGroup NewGroup(string name, RectTransform parent, out RectTransform rect)
    {
        GameObject go = NewRect(name, parent);
        rect = (RectTransform)go.transform;
        Stretch(rect);
        return go.AddComponent<CanvasGroup>();
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

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// プレイ中 HUD の駆動役。シーンの PlayHUD(曲名+装飾バー)に実行時 AddComponent され、
/// 自身の Update で GManager.state==Playing のときだけ:
///   (b) 既存の上部バー(BarBack)を曲の再生位置に連動させる
///   (c) 上部帯の左に被弾/スコアのミニカードを表示する
/// を行う。曲位置は StageReader.CurrentTime/EndTime、被弾は GManager.playerHitCount、
/// スコアは ResultScreen.CalculateProvisionalScore の暫定値。
///
/// 見た目は 2026-07-11 の再設計(ユーザー指摘「統一感がない」対応):
/// 上端の薄い半透明帯(高さ104)の中に、リザルトのデザイン言語
/// (平行四辺形+銀枠+シアンリム+青ボトムリム+白スラッシュ仕切り)で
/// 全要素をひとつの意匠として並べる。左=被弾/スコアカード、中央=曲進捗
/// (平行四辺形トラック)、右=曲名パネル。
/// スタイルの正は Docs/result-design-language.md。テクスチャ焼き込みは
/// 視覚(sRGB)値・頂点色(Image.color)は pre-linear 値(混同注意)。
///
/// 2026-07-12 額装(REVIEW-NOTES「弾幕との被り」①): プレイ中は
/// FreezeAspectRate.SetPlayFrame でカメラをズームアウトし、フィールド全体を
/// 帯の下へ縮小表示する。ズームアウトで画面に入るフィールド外(弾の生存域)を
/// 隠すため、StageCanvas 直下に不透明の額縁(PlayFrame)を敷く。帯の裏にも
/// 不透明フィルが入るので、落下前ブロック等が帯越しに見えることはない。
/// </summary>
public class PlayHudController : MonoBehaviour
{
    // リザルト様式の色(pre-linear 頂点色)。
    private static readonly Color CyanBright = new Color(0.12f, 0.78f, 0.95f);
    private static readonly Color ValueWhite = new Color(1f, 1f, 1f, 0.95f);
    private static readonly Color FillBlue = new Color(0.051f, 0.549f, 0.949f, 1f);
    // ラベル/アイコンは帯の暗さに沈まないよう明るめ(oracle レビュー指摘)。
    private static readonly Color LabelGray = new Color(0.88f, 0.91f, 0.96f, 1f);
    private static readonly Color IconWarm = new Color(1f, 0.97f, 0.90f, 1f);
    // 曲名は純白から一段抑えて左の数値と明度階層を揃える(視覚 #E6E9EF 相当)。
    private static readonly Color SongWhite = new Color(0.797f, 0.820f, 0.867f, 1f);
    // 帯の地色(pre-linear)。控えめ不透明度で下の弾幕を透かす。
    private static readonly Color BandNavy = new Color(0.010f, 0.028f, 0.055f, 0.45f);
    // 帯下辺の銀エッジ(視覚(0.55,0.60,0.70)相当の pre-linear)。
    private static readonly Color BandEdgeSilver = new Color(0.268f, 0.325f, 0.456f, 0.9f);
    // プレイ領域の左右縦エッジ(額縁)。moracle レビュー(edge-compare)採用案 D:
    // 帯の銀エッジと同じ材質だがグロー/シアン無しの静かな銀 1 本。帯銀より一段暗く
    // 低アルファにし、非発光・低コントラストで弾のブルームより常に暗く保つ
    // (画面端の弾の視認性を優先。発光する縦線は青系の弾・予告・残光と競合する指摘)。
    private static readonly Color FrameEdgeSilver = new Color(0.193f, 0.234f, 0.328f, 0.55f);
    // 額縁の地色(pre-linear・不透明必須)。フィールド外の弾を隠しつつ、
    // リザルト背景 DeepNavy より一段だけ明るい紺でエッジ線が立つ暗さにする。
    private static readonly Color FrameNavy = new Color(0.006f, 0.014f, 0.030f, 1f);

    // レイアウト定数(1080p ref・キャンバス中心原点)。全要素は帯の中心線に乗せる。
    private const float BandH = 104f;
    private const float RowY = 490f;            // 540(上端) - v11 の進捗バー中心 y=50
    // 登場時に HUD 全体(帯+曲名バー)を上から滑り込ませる距離(canvas px)。
    // AnimateHUDIn の 70px を継ぎ、額縁フェード/カメラズームと同じ eased 値で降ろす。
    private const float HudSlideY = 56f;
    private const float CardH = 72f;           // v11: y 14..86
    private const float HitCardW = 244f;       // v11: x 28..272
    private const float ScoreCardW = 300f;     // v11: x 300..600
    private const float TrackW = 818f;         // v11: x 658..1476
    // 2P のバー幅。P1/P2 のスコア札(中心 ±510・幅 300)の内側へ 30px 空けて収める。
    private const float TrackW2 = 660f;
    private const float TrackH = 34f;          // v11: y 33..67
    // 曲名パネルは右端 930 を保ったまま左へ延長し、バーとの空白を詰める
    // (oracle レビュー「右側の空白が広い」)。
    private const float SongPanelW = 368f;      // v11: x 1524..1892
    private const float SongPanelCenterX = 748f; // 中心 1708 - 960

    private TMP_FontAsset font;

    // 生成テクスチャ/スプライトの破棄用(アイコンは Resources 資産なので含めない)。
    private readonly List<Texture2D> ownedTextures = new List<Texture2D>();
    private readonly List<Sprite> ownedSprites = new List<Sprite>();

    // 帯(バンド)ルート。プレイ中のみ表示。
    private RectTransform bandRoot;

    // (b) 進捗バー
    private RectTransform barBack;
    private ParallelogramGraphic barFillPara;   // 旧 19° フィル(v11 では使わない)
    private Image barFill;          // v11 の水平な黄色フィル
    private Image barFillGlow;
    private TMP_Text barTimeText;   // 0:54 / 1:22
    private float fillSkew;
    private float fillMaxInk;

    // (c) スコア/被弾ミニカード
    private TMP_Text scoreValue;
    private TMP_Text hitValue;
    // P1 カードのラベル(2P 化時に P1 タグを付ける・1P では触らない)。
    private TMP_Text hitLabel;
    private TMP_Text scoreLabel;
    // BuildStatCard が最後に生成したラベル(呼び出し直後に捕捉する)。
    private TMP_Text lastBuiltLabel;

    // 2P(その2): 右側の P2 被弾/スコアカードと、中央へ移す曲名。1P では一切生成せず
    // 現行レイアウトを完全維持する。HUD は scene 開始時(タイトルの人数選択より前)に
    // 組まれ twoPlayer が未確定なので、Playing かつ twoPlayer を最初に検出した Update で
    // 1 度だけ遅延構築する(twoPlayerBuilt)。
    private bool twoPlayerBuilt;
    private TMP_Text scoreValue2;
    private TMP_Text hitValue2;
    // 1P の右セパレータ(548)。2P では隠して +352 の鏡像に置き換える。
    private ParallelogramGraphic sepRightA;
    private ParallelogramGraphic sepRightB;

    // 右端の曲名パネル(曲名は中央揃え、♪アイコンはインク幅に追従)
    private Image songBg;
    private TMP_Text songNameText;      // シーン既定のテキスト(v11 では非表示)
    private TMP_Text songTitleText;     // v11 の曲名(ふりがな付き)
    private HighlandUi.RubyText songRuby;
    private RectTransform songIconRect;
    private string lastSongText;

    private bool built;

    // 和文ラベルは TMP の Middle(行ボックス)整列だと CJK フォールバックの
    // 非対称メトリクスで数 px 上ずれする(再発指摘)。TmpAlign のインク実測で
    // 光学センターへ補正する。ビルド時(帯が非表示)は canvas 未初期化で測れず
    // 無言で空振りすることがあるため、プレイ中の Update で全ラベルの補正が
    // 成功するまで再試行する(bool 戻り値で確定)。
    private readonly List<TMP_Text> inkCenterLabels = new List<TMP_Text>();
    private bool inkCentered;

    // 額装(プレイ領域フレーム)。カメラのズームアウトは FreezeAspectRate が担い、
    // ここはフィールド外を覆う不透明フィル+エッジ線の UI とフェード同期を持つ。
    // インセット値は FreezeAspectRate(単一ソース)から導出する。
    private FreezeAspectRate cameraRig;
    // playHUD 自身の CanvasGroup / RectTransform。帯・曲名バーを一括で
    // フェード+スライドさせ、額縁(frameGroup)・カメラズームと同じ eased 値で
    // 一本の登場モーションに揃える(帯だけ即時ポップしていた退行の解消)。
    private CanvasGroup hudGroup;
    private RectTransform hudRect;
    private RectTransform frameRoot;
    private CanvasGroup frameGroup;
    private RectTransform frameTopFill, frameLeftFill, frameRightFill, frameBottomFill;
    private RectTransform frameEdgeSilverL, frameEdgeSilverR;
    private RectTransform frameEdgeSilverB, frameEdgeBlueB, frameEdgeKeyB;
    private float frameAppliedTop = -1f;
    private float frameAppliedBottom = -1f;
    // 装飾枠線の表示ポリシー(2026-07-13 指摘「プレイ画面の枠は左右だけに」):
    //  - 左右の縦エッジ(EdgeSilverL/R)= 通常ステージで表示。frameSideStrokeVisible で制御。
    //  - 下辺3層(EdgeSilverB/BlueB/KeyB)= 全ステージで恒久非表示。復活は
    //    BottomStrokeEnabled=true の 1 フラグで戻せる。
    // 機能フィル(TopFill/LeftFill/RightFill/BottomFill=不透明・画面外弾の遮蔽)は常時維持。
    //
    // 石工(2d2ced9): 下部コンベア帯が下端を示すため、純黒背景では左右縦線が
    // 画面枠でなくステージ内の縦線に見え、下辺と直角の明るいL字を作る指摘があった。
    // 下辺3層を全ステージで消した本便では L字の原因(下辺)自体が無くなるが、確定済みの
    // ユーザー修正を尊重し既定では石工も左右エッジを非表示のまま保つ。石工も他ステージと
    // 同じ左右エッジに揃えたいときは StoneShowSideEdges=true にするだけで戻せる。
    private bool frameSideStrokeVisible = true;
    // 下辺3層(額縁を閉じる横ライン)の恒久スイッチ。true で従来の下辺装飾が復活。
    private const bool BottomStrokeEnabled = false;
    // 石工で左右の縦エッジも表示するか。2026-07-13 指摘「石工も他ステージと同じ
    // 左右エッジ表示に揃える」で true(下辺3層は BottomStrokeEnabled=false のまま
    // なので、2d2ced9 で問題になった下辺との直角L字は発生しない=縦エッジのみ)。
    private const bool StoneShowSideEdges = true;

    private void Awake()
    {
        Build();
    }

    private void OnDestroy()
    {
        // 額縁は PlayHUD の兄弟(StageCanvas 直下)なので自前で破棄する。
        if (frameRoot != null) Destroy(frameRoot.gameObject);
        foreach (Sprite s in ownedSprites) if (s != null) Destroy(s);
        foreach (Texture2D t in ownedTextures) if (t != null) Destroy(t);
        ownedSprites.Clear();
        ownedTextures.Clear();
    }

    private void Build()
    {
        if (built) return;

        // playHUD 自身(帯+曲名バーの受け皿)の CanvasGroup / RectTransform。
        // 登場アニメを額縁と同期させるため保持する。CanvasGroup が無ければ足す。
        hudRect = (RectTransform)transform;
        hudGroup = GetComponent<CanvasGroup>();
        if (hudGroup == null) hudGroup = gameObject.AddComponent<CanvasGroup>();

        // フォントはシーンの曲名テキストから拝借(シーン既定 TMP)。
        Transform songName = transform.Find("SongName");
        if (songName != null)
        {
            songNameText = songName.GetComponent<TMP_Text>();
            if (songNameText != null) font = songNameText.font;
        }
        Transform songIcon = transform.Find("SongIcon");
        if (songIcon != null) songIconRect = (RectTransform)songIcon;

        // ---- 帯(全要素の受け皿)。最背面に敷く ----
        GameObject bandGo = new GameObject("HudBand", typeof(RectTransform));
        bandGo.layer = gameObject.layer;
        bandRoot = (RectTransform)bandGo.transform;
        bandRoot.SetParent(transform, false);
        bandRoot.SetAsFirstSibling();
        bandRoot.anchorMin = new Vector2(0f, 1f);
        bandRoot.anchorMax = new Vector2(1f, 1f);
        bandRoot.pivot = new Vector2(0.5f, 1f);
        bandRoot.anchoredPosition = Vector2.zero;
        bandRoot.sizeDelta = new Vector2(0f, BandH);

        Image bandBg = NewImage("BandBg", bandRoot, BandNavy);
        StretchFull(bandBg.rectTransform);

        // 帯下辺(v11): 金の細罫を中央で切り、白い中空の菱形を置く。
        Image bottomRule = NewImage("BandBottomRule", bandRoot, new Color(1f, 1f, 1f, 0.95f));
        bottomRule.sprite = HighlandUi.FlatRule(1920, 10, 1.3f, new Color32(0xF3, 0xDC, 0x6B, 0xFF),
            ownedTextures, ownedSprites, "HudBottomRule");
        SetBand(bottomRule.rectTransform, 960f, 99f, 1920f, 10f);
        Image bottomGap = NewImage("BandBottomGap", bandRoot, new Color(0f, 0f, 0f, 0f));
        SetBand(bottomGap.rectTransform, 960f, 99f, 22f, 10f);
        Image bottomGem = NewImage("BandBottomGem", bandRoot, Color.white);
        bottomGem.sprite = HighlandUi.DiamondRect(9, 8, false, 1.45f,
            ownedTextures, ownedSprites, "HudBottomGem");
        SetBand(bottomGem.rectTransform, 960f, 99f, 9f, 8f);

        // ---- 左: 被弾/スコアのミニカード ----
        Sprite hitPanel = V11Plate((int)HitCardW, "HudHitPanel");
        Sprite scorePanel = V11Plate((int)ScoreCardW, "HudScorePanel");
        float hitCx = 150f - 960f;              // v11: x 28..272
        float scoreCx = 450f - 960f;            // v11: x 300..600
        hitValue = BuildStatCard("HitCard", hitPanel, hitCx, HitCardW,
            "[当|あ]たった[回数|かいすう]", 18.5f, 0.8f);
        hitLabel = lastBuiltLabel;
        scoreValue = BuildStatCard("ScoreCard", scorePanel, scoreCx, ScoreCardW,
            "スコア", 20.5f, 2f);
        scoreLabel = lastBuiltLabel;

        // v11 は 19° のスラッシュ仕切りを持たない(各札が枠で分かれている)。

        // ---- 中央: 曲進捗バー(平行四辺形トラック+フィル) ----
        Transform bb = transform.Find("BarBack");
        if (bb != null)
        {
            barBack = (RectTransform)bb;
            // シーン直下(キャンバス中心アンカー)なので y はキャンバス座標で与える。
            // v11: x 658..1476 の中心 = 1067 → キャンバス中心基準で +107。
            barBack.anchoredPosition = new Vector2(107f, RowY);
            barBack.sizeDelta = new Vector2(TrackW, TrackH);
            Image bbImg = bb.GetComponent<Image>();
            if (bbImg != null)
            {
                bbImg.sprite = HighlandUi.NotchPanel((int)TrackW, (int)TrackH, 5f, true,
                    ownedTextures, ownedSprites, "HudTrackPanelV11", 4f, 1.1f, 0.7f, 0.2f, 2);
                bbImg.type = Image.Type.Simple;
                bbImg.color = Color.white;
            }
            // 旧フィル(矩形 Image)と 19° の斜辺はやめ、v11 の水平な黄色バーにする。
            Transform bf = bb.Find("BarFill");
            if (bf != null) bf.gameObject.SetActive(false);
            const float fillH = 16f;
            fillSkew = 0f;
            fillMaxInk = 786f;                       // v11: x 674..1460
            Image track = NewImage("TrackBase", barBack, new Color(1f, 1f, 1f, 0.10f));
            track.rectTransform.anchorMin = track.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            track.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            track.rectTransform.sizeDelta = new Vector2(fillMaxInk, fillH);
            barFillPara = null;
            barFill = NewImage("Fill", barBack, Color.white);
            barFill.sprite = HighlandUi.HorizontalBar(ownedTextures, ownedSprites);
            barFill.rectTransform.anchorMin = barFill.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            barFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            barFill.rectTransform.anchoredPosition = new Vector2(16f, 0f);
            barFill.rectTransform.sizeDelta = new Vector2(0f, fillH);
            barFillGlow = null;

            // 経過/全体の時刻テキスト(バー右端の外側。v11 の絵には無いが残す)。
            barTimeText = NewText("BarTime", barBack, "0:00 / 0:00", 18f,
                new Color(0.88f, 0.88f, 0.88f, 0.85f), TextAlignmentOptions.Left);
            // v11 の絵には無い項目なので、バーの右上(枠の外)へ小さく逃がす
            // (右の曲名札と重ねない。2026-09-19 U7 の既定)。
            barTimeText.alignment = TextAlignmentOptions.Right;
            RectTransform tr = (RectTransform)barTimeText.transform;
            tr.anchorMin = tr.anchorMax = new Vector2(1f, 1f);
            tr.pivot = new Vector2(1f, 0f);
            tr.anchoredPosition = new Vector2(-2f, 1f);
            tr.sizeDelta = new Vector2(220f, 24f);
            barTimeText.fontSize = 15f;
        }

        // ---- 右: 曲名パネル(カードと同型のパネルに ♪+曲名を中央配置) ----
        Sprite songPanel = V11Plate((int)SongPanelW, "HudSongPanel");
        songBg = NewImage("SongPanel", bandRoot, Color.white);
        songBg.sprite = songPanel;
        songBg.type = Image.Type.Simple;
        songBg.rectTransform.anchorMin = songBg.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        songBg.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        songBg.rectTransform.anchoredPosition = new Vector2(SongPanelCenterX, 0f);
        songBg.rectTransform.sizeDelta = new Vector2(SongPanelW, CardH);
        // 札の中の細罫 + 菱形(左右のカードと同じ)。
        {
            Image rule = NewImage("Rule", songBg.rectTransform, Color.white);
            rule.sprite = HighlandUi.FadeRule((int)(SongPanelW - 52f), 10, 0.75f, 16f,
                new[] { 0f, 0.25f, 0.5f, 0.75f, 1f },
                new[] { new Color32(0xFF, 0xE1, 0x6A, 0xFF), new Color32(0xFF, 0xE1, 0x6A, 0xFF),
                        new Color32(0xFF, 0xE1, 0x6A, 0xFF), new Color32(0xFF, 0xE1, 0x6A, 0xFF),
                        new Color32(0xFF, 0xE1, 0x6A, 0xFF) },
                new[] { 0f, 0.4f, 0.55f, 0.4f, 0f },
                ownedTextures, ownedSprites, "HudSongRule");
            SetLocal(rule.rectTransform, 0f, -23f, SongPanelW - 52f, 10f);
            Image gem = NewImage("Gem", songBg.rectTransform, new Color(1f, 0.882f, 0.416f, 1f));
            gem.sprite = HighlandUi.DiamondRect(10, 13, false, 1.5f, ownedTextures, ownedSprites, "HudSongGem");
            SetLocal(gem.rectTransform, 0f, -23f, 9.6f, 12.4f);
        }

        if (songNameText != null)
        {
            // v11: 曲名は札の中央・25px の明朝(太)+ ふりがな。
            songNameText.gameObject.SetActive(false);
            songRuby = HudRuby("SongName", bandRoot, "", 25f, HighlandUi.InkSoft,
                TextAlignmentOptions.Center, 3f, true);
            songTitleText = songRuby.Body;
            SetBand((RectTransform)songTitleText.transform, 1735f, 46.5f, 300f, 40f);
        }
        // ♪ アイコン(v11: x 1549..1582 / y 29..67)。SongIcon は帯ではなく
        // PlayHUD 直下(キャンバス座標)にあるので、そちらの系で置く。
        if (songIconRect != null)
        {
            songIconRect.anchorMin = songIconRect.anchorMax = new Vector2(0.5f, 0.5f);
            songIconRect.pivot = new Vector2(0.5f, 0.5f);
            songIconRect.anchoredPosition = new Vector2(1563f - 960f, 540f - 48f);
            songIconRect.sizeDelta = new Vector2(30f, 38f);
        }

        // ---- 額装(プレイ領域フレーム) ----
        cameraRig = Object.FindFirstObjectByType<FreezeAspectRate>();
        if (cameraRig != null) cameraRig.playFrameTopPx = BandH; // 帯高さと機械同期(d=0)
        BuildPlayFrame();

        built = true;
    }

    // 額縁: StageCanvas 直下(PlayHUD の直前=下のレイヤ)に置き、AnimateHUDIn の
    // 70px スライドとは独立させる。フィルは不透明必須(ズームアウトで画面に入る
    // フィールド外の弾・落下前ブロックを隠す)。エッジ線は帯下辺の
    // 銀+青アクセントと同じ語彙をフィールドの左右(+下)に回す。
    private void BuildPlayFrame()
    {
        GameObject go = new GameObject("PlayFrame", typeof(RectTransform), typeof(CanvasGroup));
        go.layer = gameObject.layer;
        frameRoot = (RectTransform)go.transform;
        frameRoot.SetParent(transform.parent, false);
        frameRoot.SetSiblingIndex(transform.GetSiblingIndex());
        StretchFull(frameRoot);
        frameGroup = go.GetComponent<CanvasGroup>();
        frameGroup.alpha = 0f;
        frameGroup.blocksRaycasts = false;
        frameGroup.interactable = false;

        frameTopFill = NewImage("TopFill", frameRoot, FrameNavy).rectTransform;
        frameLeftFill = NewImage("LeftFill", frameRoot, FrameNavy).rectTransform;
        frameRightFill = NewImage("RightFill", frameRoot, FrameNavy).rectTransform;
        frameBottomFill = NewImage("BottomFill", frameRoot, FrameNavy).rectTransform;

        // 左右の縦エッジは静かな銀 1 本のみ(採用案 D)。以前の「銀+シアンリム+
        // 暗キーライン」の 3 層はシアンが強く『左右のガイド線/当たり判定境界』に
        // 見える指摘(ユーザー+moracle)を受けて撤去。下辺は額縁を閉じるため、
        // 帯下辺と同じ銀+青アクセント+暗キーラインの 3 層を残す。
        Color accentBlue = new Color(FillBlue.r, FillBlue.g, FillBlue.b, 0.28f);
        Color keyline = new Color(0f, 0.004f, 0.010f, 0.9f);
        frameEdgeSilverL = NewImage("EdgeSilverL", frameRoot, FrameEdgeSilver).rectTransform;
        frameEdgeSilverR = NewImage("EdgeSilverR", frameRoot, FrameEdgeSilver).rectTransform;
        frameEdgeBlueB = NewImage("EdgeBlueB", frameRoot, accentBlue).rectTransform;
        frameEdgeSilverB = NewImage("EdgeSilverB", frameRoot, BandEdgeSilver).rectTransform;
        frameEdgeKeyB = NewImage("EdgeKeyB", frameRoot, keyline).rectTransform;

        LayoutPlayFrame();
        go.SetActive(false);
    }

    // 1080p ref・キャンバス中心原点。FreezeAspectRate と同じ式で内寸を出す
    // (連動要素の同一ソース導出)。インセットが変わったフレームだけ組み直す。
    private void LayoutPlayFrame()
    {
        if (frameRoot == null) return;
        float top = cameraRig != null ? cameraRig.playFrameTopPx : BandH;
        float bot = cameraRig != null ? cameraRig.playFrameBottomPx : 20f;
        if (top == frameAppliedTop && bot == frameAppliedBottom) return;
        frameAppliedTop = top;
        frameAppliedBottom = bot;

        float s = Mathf.Max(0.05f, 1f - (top + bot) / 1080f);
        float halfW = 960f * s;              // フィールド半幅(canvas px)
        float fieldTop = 540f - top;
        float fieldBot = -540f + bot;
        float sideW = 960f - halfW;
        float sideH = 1080f - top;
        float fieldH = fieldTop - fieldBot;
        float edgeCy = (fieldTop + fieldBot) * 0.5f;
        bool hasBottom = bot > 0.5f;

        PlaceRect(frameTopFill, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1920f, top));
        PlaceRect(frameLeftFill, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(sideW, sideH));
        PlaceRect(frameRightFill, new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(sideW, sideH));
        frameBottomFill.gameObject.SetActive(hasBottom);
        PlaceRect(frameBottomFill, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(1920f, Mathf.Max(1f, bot)));

        // 左右の縦エッジ = 静かな銀 1 本(1.5px、採用案 D)。フィールド際に置く。
        // 下辺は帯下辺と同じ 3 層で額縁を閉じ、横ラインを左右の外側まで伸ばす。
        Vector2 center = new Vector2(0.5f, 0.5f);
        const float edgeOut = 4.5f;
        const float sideEdgeW = 1.5f;
        // 左右の縦エッジは frameSideStrokeVisible(石工=false)で表示制御。機能フィルは常時。
        frameEdgeSilverL.gameObject.SetActive(frameSideStrokeVisible);
        frameEdgeSilverR.gameObject.SetActive(frameSideStrokeVisible);
        PlaceRect(frameEdgeSilverL, center, new Vector2(1f, 0.5f), new Vector2(-halfW, edgeCy), new Vector2(sideEdgeW, fieldH));
        PlaceRect(frameEdgeSilverR, center, new Vector2(0f, 0.5f), new Vector2(halfW, edgeCy), new Vector2(sideEdgeW, fieldH));
        // 下辺3層は「枠は左右だけに」で恒久非表示(BottomStrokeEnabled=true で復活)。
        bool bottomStroke = hasBottom && BottomStrokeEnabled && frameSideStrokeVisible;
        frameEdgeSilverB.gameObject.SetActive(bottomStroke);
        frameEdgeBlueB.gameObject.SetActive(bottomStroke);
        frameEdgeKeyB.gameObject.SetActive(bottomStroke);
        float bottomW = halfW * 2f + edgeOut * 2f;
        PlaceRect(frameEdgeSilverB, center, new Vector2(0.5f, 1f), new Vector2(0f, fieldBot), new Vector2(bottomW, 2f));
        PlaceRect(frameEdgeBlueB, center, new Vector2(0.5f, 1f), new Vector2(0f, fieldBot - 2f), new Vector2(bottomW, 1.5f));
        PlaceRect(frameEdgeKeyB, center, new Vector2(0.5f, 1f), new Vector2(0f, fieldBot - 3.5f), new Vector2(bottomW, 1f));
    }

    // 左右の縦エッジの表示を切り替える。石工では false(2d2ced9 の縦線指摘を尊重)。
    // 下辺3層は BottomStrokeEnabled で恒久制御、機能フィルは触らない。
    // 変化時のみ次フレームで再レイアウトさせる。
    private void SetFrameSideStrokeVisible(bool v)
    {
        if (v == frameSideStrokeVisible) return;
        frameSideStrokeVisible = v;
        frameAppliedTop = -999f; // LayoutPlayFrame の早期 return を外して active を反映
        LayoutPlayFrame();
    }

    private static void PlaceRect(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
    }

    // v11 の札: 地は無く、四隅をえぐった金の枠 + 内側の銀線。中にふりがな付きの
    // ラベル(左)と白い数値(右)、下寄りに両端が消える金の細罫と中空の菱形。
    private Sprite V11Plate(int width, string name)
    {
        return HighlandUi.NotchPanel(width, (int)CardH, 6f, true,
            ownedTextures, ownedSprites, name, 4f, 1.15f, 0.7f, 0f, 3);
    }

    private TMP_Text BuildStatCard(string name, Sprite panel, float centerX, float width,
        string labelMarkup, float labelSvgSize, float labelTracking)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(bandRoot, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(centerX, 0f);
        rect.sizeDelta = new Vector2(width, CardH);
        Image bg = go.GetComponent<Image>();
        bg.sprite = panel;
        bg.type = Image.Type.Simple;
        bg.color = Color.white;
        bg.raycastTarget = false;

        // 札の中の細罫(両端が消える金)+ 中空の菱形。y=75 は帯座標で -23。
        Image rule = NewImage("Rule", rect, Color.white);
        rule.sprite = HighlandUi.FadeRule((int)(width - 52f), 10, 0.75f, 16f,
            new[] { 0f, 0.25f, 0.5f, 0.75f, 1f },
            new[] { new Color32(0xFF, 0xE1, 0x6A, 0xFF), new Color32(0xFF, 0xE1, 0x6A, 0xFF),
                    new Color32(0xFF, 0xE1, 0x6A, 0xFF), new Color32(0xFF, 0xE1, 0x6A, 0xFF),
                    new Color32(0xFF, 0xE1, 0x6A, 0xFF) },
            new[] { 0f, 0.4f, 0.55f, 0.4f, 0f },
            ownedTextures, ownedSprites, name + "Rule");
        SetLocal(rule.rectTransform, 0f, -23f, width - 52f, 10f);
        Image gem = NewImage("Gem", rect, new Color(1f, 0.882f, 0.416f, 1f));
        gem.sprite = HighlandUi.DiamondRect(10, 13, false, 1.5f, ownedTextures, ownedSprites, name + "Gem");
        SetLocal(gem.rectTransform, 0f, -23f, 9.6f, 12.4f);

        // ラベル(ふりがな付き・左寄せ)。
        HighlandUi.RubyText labelRuby = HudRuby(name + "Label", rect, labelMarkup, labelSvgSize,
            HighlandUi.Ink, TextAlignmentOptions.Left, labelTracking);
        TMP_Text label = labelRuby.Body;
        RectTransform lr = (RectTransform)label.transform;
        lr.anchorMin = lr.anchorMax = new Vector2(0f, 0.5f);
        lr.pivot = new Vector2(0f, 0.5f);
        lr.sizeDelta = new Vector2(width - 60f, labelSvgSize * 1.6f);
        lr.anchoredPosition = new Vector2(25f, 5f);

        // 数値(白・右寄せ)。
        TMP_Text value = NewText("Value", rect, "0", 24f, HighlandUi.InkSoft, TextAlignmentOptions.Right);
        RectTransform vr = (RectTransform)value.transform;
        vr.anchorMin = vr.anchorMax = new Vector2(1f, 0.5f);
        vr.pivot = new Vector2(1f, 0.5f);
        vr.anchoredPosition = new Vector2(-27f, -8f);
        vr.sizeDelta = new Vector2(170f, 36f);
        value.characterSpacing = 1.5f / 24f * 100f;

        lastBuiltLabel = label;
        return value;
    }

    // 帯(1920x104)の SVG 座標で置く。
    private static void SetBand(RectTransform rt, float svgCx, float svgCy, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(svgCx - 960f, 52f - svgCy);
        rt.sizeDelta = new Vector2(w, h);
    }

    private static void SetLocal(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    // HUD は 1920x104 の等倍なので、HighlandUi の 1.1483 倍を打ち消して使う。
    private readonly List<HighlandUi.RubyText> rubies = new List<HighlandUi.RubyText>();
    private bool rubiesPlaced;

    private HighlandUi.RubyText HudRuby(string name, Transform parent, string markup, float svgSize,
        Color color, TextAlignmentOptions align, float tracking, bool bold = false)
    {
        HighlandUi.RubyText r = HighlandUi.Ruby(name, parent, markup, svgSize / HighlandUi.S,
            color, align, bold, tracking / HighlandUi.S);
        rubies.Add(r);
        rubiesPlaced = false;
        return r;
    }

    // 2P レイアウトの遅延構築(Playing かつ twoPlayer を最初に検出した Update から 1 度)。
    // 左=P1(現行)の鏡像として右に P2 の被弾/スコアを組み、両側のラベルへ P1/P2 タグを
    // 付ける。曲名は右パネルを畳んで中央の進捗バー直上の小さな見出しへ移し、バー時刻は
    // バー直下(中央)へ寄せて P2 カードとの重なりを避ける。1P では決して呼ばれない。
    private void BuildTwoPlayerLayout()
    {
        if (twoPlayerBuilt) return;
        twoPlayerBuilt = true;

        // ---- 右: P2 の被弾/スコア(左 P1 の鏡像・外=被弾/内=スコア) ----
        Sprite hitPanel2 = V11Plate((int)HitCardW, "HudHitPanel2");
        Sprite scorePanel2 = V11Plate((int)ScoreCardW, "HudScorePanel2");
        float hitCx2 = 960f - 150f;             // 左 P1 の鏡像
        float scoreCx2 = 960f - 450f;
        scoreValue2 = BuildStatCard("ScoreCard2", scorePanel2, scoreCx2, ScoreCardW,
            "スコア", 20.5f, 2f);
        TMP_Text scoreLabel2 = lastBuiltLabel;
        hitValue2 = BuildStatCard("HitCard2", hitPanel2, hitCx2, HitCardW,
            "[当|あ]たった[回数|かいすう]", 18.5f, 0.8f);
        TMP_Text hitLabel2 = lastBuiltLabel;

        // プレイヤータグ(1P=温色 / 2P=シアン)。
        ApplyPlayerTag(hitLabel, "P1", true);
        ApplyPlayerTag(scoreLabel, "P1", true);
        ApplyPlayerTag(hitLabel2, "P2", false);
        ApplyPlayerTag(scoreLabel2, "P2", false);

        // ---- 進捗バー: 2P は左右とも札が来るので細くして画面中央へ ----
        // 1P は x 658..1476(中心 +107・幅 818)。2P だと右端 +525 が P2 スコア札の
        // 左端 +360 に 165px 食い込んでいた(第 U9 便の実フレームで確認)。
        // P1 スコア札の右端 -360 と P2 スコア札の左端 +360 の間へ、左右 30px 空けて収める。
        if (barBack != null)
        {
            barBack.anchoredPosition = new Vector2(0f, RowY);
            barBack.sizeDelta = new Vector2(TrackW2, TrackH);
            Image bbImg2 = barBack.GetComponent<Image>();
            if (bbImg2 != null)
                bbImg2.sprite = HighlandUi.NotchPanel((int)TrackW2, (int)TrackH, 5f, true,
                    ownedTextures, ownedSprites, "HudTrackPanelV11_2P", 4f, 1.1f, 0.7f, 0.2f, 2);
            fillMaxInk = TrackW2 - 32f;          // 1P と同じ左右 16px の余白
            Transform tb = barBack.Find("TrackBase");
            if (tb != null)
                ((RectTransform)tb).sizeDelta = new Vector2(fillMaxInk, ((RectTransform)tb).sizeDelta.y);
        }

        // ---- 曲名: 右パネルを畳み、中央バー直上の小見出しへ ----
        if (songBg != null) songBg.gameObject.SetActive(false);
        if (songIconRect != null) songIconRect.gameObject.SetActive(false);
        if (songNameText != null)
        {
            // oracle 指摘: 曲名が小さく進捗バーの飾り文字に近い。中央軸としての存在感を
            // 少し戻すため一段拡大(22→25)。バー幅・位置は不変。
            // v11: 2P では曲名札を畳んで、進捗バーの直上へ小さく置く。
            if (songTitleText != null)
            {
                // 第 U9 便: 20 → 16.5。ふりがなの上端が帯の外へはみ出していた
                // (帯 104px に「曲名+ふりがな / バー / 経過時刻」を積むため)。
                songTitleText.fontSize = 16.5f;
                // ふりがなの大きさと本文からの距離は RubyText の svgSize で決まるので、
                // 本文を縮めたぶんこちらも合わせる(16.5px ÷ 1.1483 = 14.4 SVG)。
                // 合わせないと読みが大きいまま上へ離れ、帯の上端で切れる。
                if (songRuby != null)
                {
                    songRuby.SetSvgSize(14.4f);
                    lastSongText = null;      // 次の Update で読みを作り直させる
                }
                RectTransform nr = (RectTransform)songTitleText.transform;
                nr.anchorMin = nr.anchorMax = new Vector2(0.5f, 0.5f);
                nr.pivot = new Vector2(0.5f, 0.5f);
                nr.anchoredPosition = new Vector2(0f, 30f);
                nr.sizeDelta = new Vector2(TrackW2, 26f);
                rubiesPlaced = false;
            }
        }

        // ---- バー時刻: バー直下の中央へ(1P では右外だが 2P は P2 カードと被る) ----
        if (barTimeText != null)
        {
            barTimeText.alignment = TextAlignmentOptions.Center;
            RectTransform tr = (RectTransform)barTimeText.transform;
            tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0f);
            tr.pivot = new Vector2(0.5f, 1f);
            tr.anchoredPosition = new Vector2(0f, -6f);
            tr.sizeDelta = new Vector2(240f, 26f);
        }

        // 新規/変更ラベルをインク中央補正で再確定させる。
        inkCentered = false;
    }

    // ラベルの上に小さく P1/P2 のタグを出す(v11 の絵には無い。2P 用の既定)。
    private void ApplyPlayerTag(TMP_Text label, string tag, bool isP1)
    {
        if (label == null) return;
        TMP_Text t = HighlandUi.Text(tag, label.transform.parent, tag, 13f / HighlandUi.S,
            isP1 ? new Color(1f, 0.80f, 0.40f, 1f) : new Color(0.45f, 0.85f, 1f, 1f),
            TextAlignmentOptions.Right, false, 0f);
        RectTransform rt = (RectTransform)t.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.sizeDelta = new Vector2(60f, 20f);
        rt.anchoredPosition = new Vector2(-27f, 16f);
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // 帯の下端に沿う横一杯のライン。bottomOffset は帯下端からの持ち上げ。
    private static void AnchorBottomStretch(RectTransform rect, float bottomOffset, float height)
    {
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, bottomOffset);
        rect.sizeDelta = new Vector2(0f, height);
    }

    private void Update()
    {
        GManager gm = GManager.Control;
        if (gm == null) return;
        bool playing = gm.state == GManager.GameState.Playing;

        // 額装(カメラズームアウト)の適用度。HUD帯・曲名バー・額縁フェードを
        // すべて同じ eased 値に乗せ、登場を一本のモーション(上からスライドイン+
        // フェードで着地)に揃える。プレイ中は 1 へ、外れると 0 へ 0.35s で補間。
        if (cameraRig != null) cameraRig.SetPlayFrame(playing);
        float eased = cameraRig != null ? cameraRig.PlayFrameEased : (playing ? 1f : 0f);
        bool frameVisible = playing || eased > 0.001f;

        // 帯・曲名バー(playHUD 全体)を eased でフェード+上からスライドイン。
        // 帯だけ SetActive で瞬間ポップし、額縁は 0.35s フェード…という非対称を解消。
        if (hudGroup != null) hudGroup.alpha = eased;
        if (hudRect != null) hudRect.anchoredPosition = new Vector2(0f, HudSlideY * (1f - eased));
        if (bandRoot != null && bandRoot.gameObject.activeSelf != frameVisible)
            bandRoot.gameObject.SetActive(frameVisible);

        // 額縁はズームで露出するフィールド外を覆う。帯と同じ eased でフェード。
        if (frameRoot != null)
        {
            if (frameRoot.gameObject.activeSelf != frameVisible)
                frameRoot.gameObject.SetActive(frameVisible);
            if (frameVisible && cameraRig != null)
            {
                frameGroup.alpha = eased;
                LayoutPlayFrame();
            }
        }

        if (!playing) return;
        StageReader sr = gm.SReader;
        if (sr == null || !sr.IsReady) return;

        // 2P(その2): 2 人プレイなら右側 P2 カード+中央曲名レイアウトを 1 度だけ足す。
        // ink-center パスの前に組み、新ラベルも同フレームで中央補正の対象にする。
        if (gm.twoPlayer && !twoPlayerBuilt) BuildTwoPlayerLayout();

        // 石工のみ左右の縦エッジも消す(純黒背景で縦線がステージ内の線に見える指摘)。
        // StoneShowSideEdges=true にすれば石工も他ステージと同じ左右エッジになる。
        bool stoneStage = sr.CurrentStage != null && sr.CurrentStage.stageDirectoryName == "stone";
        SetFrameSideStrokeVisible(!stoneStage || StoneShowSideEdges);

        // 帯が表示された後の初回に、全ラベルをインク実測で縦センターへ確定させる。
        if (!inkCentered)
        {
            bool all = true;
            for (int i = 0; i < inkCenterLabels.Count; i++)
                if (inkCenterLabels[i] != null) all &= TmpAlign.CenterInkVertically(inkCenterLabels[i]);
            inkCentered = all;
        }

        // 曲名(v11): シーン既定のテキストが差し替わったら、ふりがな付きへ写す。
        if (songNameText != null && songRuby != null && songNameText.text != lastSongText)
        {
            lastSongText = songNameText.text;
            songRuby.Apply(StageCityProfile.ReadingMarkup(lastSongText));
            rubiesPlaced = false;
        }

        // ふりがなの実測合わせ(帯が出た後の初回)。
        if (!rubiesPlaced)
        {
            bool allRuby = true;
            for (int i = 0; i < rubies.Count; i++)
            {
                if (rubies[i] == null || rubies[i].Body == null) continue;
                if (!rubies[i].Body.gameObject.activeInHierarchy) continue;
                allRuby &= rubies[i].EnsurePlaced();
            }
            rubiesPlaced = allRuby;
        }

        // (b) 進捗バー。フィルはトラック斜辺に平行な平行四辺形を幅で伸ばす。
        float end = sr.EndTime;
        float cur = sr.CurrentTime;
        float progress = end > 0.001f ? Mathf.Clamp01(cur / end) : 0f;
        if (barBack != null && barFill != null)
        {
            float ink = fillMaxInk * progress;
            RectTransform fr = barFill.rectTransform;
            fr.sizeDelta = new Vector2(Mathf.Max(0.1f, ink), fr.sizeDelta.y);
            if (barTimeText != null)
                barTimeText.text = FormatTime(cur) + " / " + FormatTime(end);
        }

        // (c) スコア/被弾。
        int hit = gm.playerHitCount;
        int counter = gm.counterHitBossCount;
        int score = ResultScreen.CalculateProvisionalScore(false, hit, counter, cur, end);
        if (scoreValue != null) scoreValue.text = score.ToString("000,000");
        if (hitValue != null) hitValue.text = hit.ToString("00");

        // 2P: P2 の被弾/スコア(ボスカウンターは共有・被弾は playerHitCount2)。
        if (gm.twoPlayer)
        {
            int hit2 = gm.playerHitCount2;
            int score2 = ResultScreen.CalculateProvisionalScore(false, hit2, counter, cur, end);
            if (scoreValue2 != null) scoreValue2.text = score2.ToString("000,000");
            if (hitValue2 != null) hitValue2.text = hit2.ToString("00");
        }
    }

    private static string FormatTime(float seconds)
    {
        if (seconds < 0f) seconds = 0f;
        int m = (int)(seconds / 60f);
        int s = (int)(seconds % 60f);
        return m + ":" + s.ToString("00");
    }

    private Image NewImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private TMP_Text NewText(string name, Transform parent, string value, float size, Color color, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = value;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }
}

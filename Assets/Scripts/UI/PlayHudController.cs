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
    private const float RowY = 488f;            // 540(上端) - BandH/2
    // 登場時に HUD 全体(帯+曲名バー)を上から滑り込ませる距離(canvas px)。
    // AnimateHUDIn の 70px を継ぎ、額縁フェード/カメラズームと同じ eased 値で降ろす。
    private const float HudSlideY = 56f;
    private const float CardH = 72f;
    private const float HitCardW = 220f;
    private const float ScoreCardW = 320f;
    private const float TrackW = 620f;
    private const float TrackH = 26f;
    // 曲名パネルは右端 930 を保ったまま左へ延長し、バーとの空白を詰める
    // (oracle レビュー「右側の空白が広い」)。
    private const float SongPanelW = 340f;
    private const float SongPanelCenterX = 760f; // 右端 930 - W/2

    private TMP_FontAsset font;

    // 生成テクスチャ/スプライトの破棄用(アイコンは Resources 資産なので含めない)。
    private readonly List<Texture2D> ownedTextures = new List<Texture2D>();
    private readonly List<Sprite> ownedSprites = new List<Sprite>();

    // 帯(バンド)ルート。プレイ中のみ表示。
    private RectTransform bandRoot;

    // (b) 進捗バー
    private RectTransform barBack;
    private ParallelogramGraphic barFillPara;
    private Image barFillGlow;      // フィル先端のシアン発光
    private TMP_Text barTimeText;   // 0:54 / 1:22
    private float fillSkew;
    private float fillMaxInk;

    // (c) スコア/被弾ミニカード
    private TMP_Text scoreValue;
    private TMP_Text hitValue;

    // 右端の曲名パネル(曲名は中央揃え、♪アイコンはインク幅に追従)
    private TMP_Text songNameText;
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

        // 帯下辺: 銀エッジ+その上の青アクセント(リザルトのヘッダー帯の金属
        // エッジ+ボタン下辺発光の語彙)。
        Image edgeBlue = NewImage("BandEdgeBlue", bandRoot, new Color(FillBlue.r, FillBlue.g, FillBlue.b, 0.40f));
        AnchorBottomStretch(edgeBlue.rectTransform, 2f, 1.5f);
        Image edgeSilver = NewImage("BandEdgeSilver", bandRoot, BandEdgeSilver);
        AnchorBottomStretch(edgeSilver.rectTransform, 0f, 2f);

        // ---- 左: 被弾/スコアのミニカード ----
        Sprite hitPanel = UiButtonStyle.CreateHudPanelSprite((int)HitCardW, (int)CardH,
            ownedTextures, ownedSprites, "HudHitPanel");
        Sprite scorePanel = UiButtonStyle.CreateHudPanelSprite((int)ScoreCardW, (int)CardH,
            ownedTextures, ownedSprites, "HudScorePanel");
        float hitCx = -960f + 28f + HitCardW * 0.5f;
        float scoreCx = -960f + 28f + HitCardW + 12f + ScoreCardW * 0.5f;
        hitValue = BuildStatCard("HitCard", hitPanel, hitCx, HitCardW,
            "被弾", "HIT", "UI/result_icon_hit");
        scoreValue = BuildStatCard("ScoreCard", scorePanel, scoreCx, ScoreCardW,
            "スコア", "SCORE", "UI/result_icon_score");

        // ---- 仕切りのスラッシュ対(カード群/バー間、バー/曲名間) ----
        AddSeparator(-352f);
        AddSeparator(548f);

        // ---- 中央: 曲進捗バー(平行四辺形トラック+フィル) ----
        Transform bb = transform.Find("BarBack");
        if (bb != null)
        {
            barBack = (RectTransform)bb;
            // シーン直下(キャンバス中心アンカー)なので y はキャンバス座標で与える。
            barBack.anchoredPosition = new Vector2(-10f, RowY);
            barBack.sizeDelta = new Vector2(TrackW, TrackH);
            Image bbImg = bb.GetComponent<Image>();
            if (bbImg != null)
            {
                bbImg.sprite = UiButtonStyle.CreateHudPanelSprite((int)TrackW, (int)TrackH,
                    ownedTextures, ownedSprites, "HudTrackPanel");
                bbImg.type = Image.Type.Simple;
                bbImg.color = Color.white;
            }
            // 旧フィル(矩形 Image)は使わず、トラックの斜辺に平行な
            // ParallelogramGraphic で左から伸ばす。
            Transform bf = bb.Find("BarFill");
            if (bf != null) bf.gameObject.SetActive(false);
            const float fillH = 16f;
            fillSkew = fillH * Mathf.Tan(UiButtonStyle.SlashAngleDeg * Mathf.Deg2Rad);
            fillMaxInk = TrackW - 12f - fillSkew;
            barFillPara = UiButtonStyle.AddSlash(barBack, "Fill", FillBlue, 0f, 0.1f, fillH);
            RectTransform fr = barFillPara.rectTransform;
            fr.anchorMin = fr.anchorMax = new Vector2(0f, 0.5f);
            fr.pivot = new Vector2(0f, 0.5f);
            fr.anchoredPosition = new Vector2(6f, 0f);
            // フィル先端のシアン発光(斜辺に合わせて 19° 傾ける)。
            barFillGlow = NewImage("FillGlow", barBack, new Color(CyanBright.r, CyanBright.g, CyanBright.b, 0.9f));
            barFillGlow.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            barFillGlow.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            barFillGlow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            barFillGlow.rectTransform.sizeDelta = new Vector2(3f, fillH + 4f);
            barFillGlow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -UiButtonStyle.SlashAngleDeg);

            // 経過/全体の時刻テキスト(バー右端の外側)。
            barTimeText = NewText("BarTime", barBack, "0:00 / 0:00", 20f,
                new Color(0.72f, 0.86f, 0.95f, 0.9f), TextAlignmentOptions.Left);
            RectTransform tr = (RectTransform)barTimeText.transform;
            tr.anchorMin = new Vector2(1f, 0.5f);
            tr.anchorMax = new Vector2(1f, 0.5f);
            tr.pivot = new Vector2(0f, 0.5f);
            tr.anchoredPosition = new Vector2(14f, 0f);
            tr.sizeDelta = new Vector2(220f, 30f);
            inkCenterLabels.Add(barTimeText);
        }

        // ---- 右: 曲名パネル(カードと同型のパネルに ♪+曲名を中央配置) ----
        Sprite songPanel = UiButtonStyle.CreateHudPanelSprite((int)SongPanelW, (int)CardH,
            ownedTextures, ownedSprites, "HudSongPanel");
        Image songBg = NewImage("SongPanel", bandRoot, Color.white);
        songBg.sprite = songPanel;
        songBg.type = Image.Type.Simple;
        songBg.rectTransform.anchorMin = songBg.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        songBg.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        songBg.rectTransform.anchoredPosition = new Vector2(SongPanelCenterX, 0f);
        songBg.rectTransform.sizeDelta = new Vector2(SongPanelW, CardH);
        if (songNameText != null)
        {
            songNameText.alignment = TextAlignmentOptions.Center;
            // 曲名だけ1段大きく明るく見える不均衡を抑える(48→40px・白を一段減光)。
            songNameText.fontSize = 40f;
            songNameText.color = SongWhite;
            RectTransform nr = (RectTransform)songNameText.transform;
            nr.anchoredPosition = new Vector2(SongPanelCenterX + 12f, RowY);
            // 曲名は和文になり得る。基準 y(RowY=帯中心線)を確定させた後に登録。
            inkCenterLabels.Add(songNameText);
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
        PlaceRect(frameEdgeSilverL, center, new Vector2(1f, 0.5f), new Vector2(-halfW, edgeCy), new Vector2(sideEdgeW, fieldH));
        PlaceRect(frameEdgeSilverR, center, new Vector2(0f, 0.5f), new Vector2(halfW, edgeCy), new Vector2(sideEdgeW, fieldH));
        frameEdgeSilverB.gameObject.SetActive(hasBottom);
        frameEdgeBlueB.gameObject.SetActive(hasBottom);
        frameEdgeKeyB.gameObject.SetActive(hasBottom);
        float bottomW = halfW * 2f + edgeOut * 2f;
        PlaceRect(frameEdgeSilverB, center, new Vector2(0.5f, 1f), new Vector2(0f, fieldBot), new Vector2(bottomW, 2f));
        PlaceRect(frameEdgeBlueB, center, new Vector2(0.5f, 1f), new Vector2(0f, fieldBot - 2f), new Vector2(bottomW, 1.5f));
        PlaceRect(frameEdgeKeyB, center, new Vector2(0.5f, 1f), new Vector2(0f, fieldBot - 3.5f), new Vector2(bottomW, 1f));
    }

    private static void PlaceRect(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
    }

    // ステータスミニカード: 平行四辺形パネル(銀枠+半透明紺)+左のアイコン+
    // 和文ラベル(シアン英字添え)+右寄せの白数値。リザルトカードの帯内縮小版。
    private TMP_Text BuildStatCard(string name, Sprite panel, float centerX, float width,
        string jp, string en, string iconResource)
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

        // 左のアイコン(リザルトカードと同じ Material Symbols 資産)。
        Sprite iconSprite = Resources.Load<Sprite>(iconResource);
        float labelX = -width * 0.5f + 30f;
        if (iconSprite != null)
        {
            Image icon = NewImage("Icon", rect, IconWarm);
            icon.sprite = iconSprite;
            icon.type = Image.Type.Simple;
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            icon.rectTransform.anchoredPosition = new Vector2(38f, 0f);
            icon.rectTransform.sizeDelta = new Vector2(26f, 26f);
            labelX = -width * 0.5f + 58f;
        }

        // ラベル(JP 白グレー + シアン英字)。英字はリザルトの #38C2E0 より一段
        // 明るい #42E4FF(輝度+約17%)。12px と小さく弾幕上の帯に載るため、同値だと
        // リザルトより暗い階層に見える(oracle 提案 2026-07-12)。
        TMP_Text label = NewText("Label", rect, jp + " <size=12><color=#42E4FF>" + en + "</color></size>",
            19f, LabelGray, TextAlignmentOptions.Left);
        RectTransform lr = (RectTransform)label.transform;
        lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0.5f);
        lr.pivot = new Vector2(0f, 0.5f);
        lr.anchoredPosition = new Vector2(labelX, 0f);
        lr.sizeDelta = new Vector2(width - 150f, 30f);

        // 数値(白・右寄せ。斜辺を避けて右マージン 24)。
        TMP_Text value = NewText("Value", rect, "0", 29f, ValueWhite, TextAlignmentOptions.Right);
        RectTransform vr = (RectTransform)value.transform;
        vr.anchorMin = vr.anchorMax = new Vector2(1f, 0.5f);
        vr.pivot = new Vector2(1f, 0.5f);
        vr.anchoredPosition = new Vector2(-24f, 0f);
        vr.sizeDelta = new Vector2(170f, 36f);
        value.characterSpacing = 2f;

        inkCenterLabels.Add(label);
        inkCenterLabels.Add(value);
        return value;
    }

    // 仕切りのスラッシュ対(白の主線+シアンの細い補助線、19°、帯の中心線に配置)。
    // リザルトの「白主線+青アクセント」の語彙(oracle レビューで補助線を
    // 白→シアンへ)。
    private void AddSeparator(float centerX)
    {
        const float h = 44f;
        UiButtonStyle.AddSlash(bandRoot, "SepSlashA", new Color(1f, 1f, 1f, 0.9f), centerX, 6f, h);
        UiButtonStyle.AddSlash(bandRoot, "SepSlashB",
            new Color(CyanBright.r, CyanBright.g, CyanBright.b, 0.55f), centerX + 16f, 2.5f, h);
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

        // 帯が表示された後の初回に、全ラベルをインク実測で縦センターへ確定させる。
        if (!inkCentered)
        {
            bool all = true;
            for (int i = 0; i < inkCenterLabels.Count; i++)
                if (inkCenterLabels[i] != null) all &= TmpAlign.CenterInkVertically(inkCenterLabels[i]);
            inkCentered = all;
        }

        // 曲名パネル内: ♪アイコンをインク幅に追従させて文字の左に置く。
        if (songNameText != null && songIconRect != null && songNameText.text != lastSongText)
        {
            lastSongText = songNameText.text;
            // 曲名が変わったらインク中心も変わる(和文/欧文混在)ので再補正。
            TmpAlign.CenterInkVertically(songNameText);
            songNameText.ForceMeshUpdate();
            songIconRect.anchoredPosition = new Vector2(
                SongPanelCenterX + 12f - songNameText.preferredWidth * 0.5f - 26f,
                RowY);
        }

        // (b) 進捗バー。フィルはトラック斜辺に平行な平行四辺形を幅で伸ばす。
        float end = sr.EndTime;
        float cur = sr.CurrentTime;
        float progress = end > 0.001f ? Mathf.Clamp01(cur / end) : 0f;
        if (barBack != null && barFillPara != null)
        {
            float ink = fillMaxInk * progress;
            RectTransform fr = barFillPara.rectTransform;
            fr.sizeDelta = new Vector2(Mathf.Max(0.1f, ink) + fillSkew, fr.sizeDelta.y);
            if (barFillGlow != null)
                barFillGlow.rectTransform.anchoredPosition = new Vector2(6f + ink + fillSkew * 0.5f, 0f);
            if (barTimeText != null)
                barTimeText.text = FormatTime(cur) + " / " + FormatTime(end);
        }

        // (c) スコア/被弾。
        int hit = gm.playerHitCount;
        int counter = gm.counterHitBossCount;
        int score = ResultScreen.CalculateProvisionalScore(false, hit, counter, cur, end);
        if (scoreValue != null) scoreValue.text = score.ToString("000,000");
        if (hitValue != null) hitValue.text = hit.ToString("00");
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

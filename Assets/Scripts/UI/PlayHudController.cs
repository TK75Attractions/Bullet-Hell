using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// プレイ中 HUD の駆動役。シーンの PlayHUD(曲名+装飾バー)に実行時 AddComponent され、
/// 自身の Update で GManager.state==Playing のときだけ:
///   (b) 既存の上部バー(BarBack/BarFill)を曲の再生位置に連動させる
///       (従来は BarFill が固定幅で進捗を表していなかった)
///   (c) 上部帯の左に被弾/スコアのミニカードを表示する(リザルトのカード様式)
/// を行う。曲位置は StageReader.CurrentTime/EndTime、被弾は GManager.playerHitCount、
/// スコアは ResultScreen.CalculateProvisionalScore の暫定値。
///
/// レイアウトはユーザー確定(2026-07-11): ステータス系は上部帯に集約し、
/// 左上=被弾数+スコア、中央=曲の進捗バー、右端=曲名(右揃え+♪アイコン)。
/// スタイルの正は Docs/result-design-language.md。色は頂点色(pre-linear)で与える。
/// </summary>
public class PlayHudController : MonoBehaviour
{
    // リザルト様式の色(pre-linear 頂点色)。
    private static readonly Color Cyan = new Color(0.04f, 0.54f, 0.75f);
    private static readonly Color CyanBright = new Color(0.12f, 0.78f, 0.95f);
    private static readonly Color PanelNavy = new Color(0.010f, 0.028f, 0.055f, 0.82f);
    private static readonly Color BracketWhite = new Color(0.85f, 0.90f, 1f, 0.9f);
    private static readonly Color ValueWhite = new Color(1f, 1f, 1f, 0.95f);
    private static readonly Color TrackGray = new Color(0.32f, 0.32f, 0.34f, 1f);
    private static readonly Color FillBlue = new Color(0.051f, 0.549f, 0.949f, 1f);

    private TMP_FontAsset font;

    // (b) 進捗バー
    private RectTransform barBack;
    private RectTransform barFill;
    private Image barFillGlow;      // フィル先端のシアン発光
    private TMP_Text barTimeText;   // 0:54 / 1:22

    // (c) スコア/被弾ミニカード
    private RectTransform scoreCard;
    private RectTransform hitCard;
    private TMP_Text scoreValue;
    private TMP_Text hitValue;

    // 右端の曲名グループ(曲名は右揃え、♪アイコンは曲名のインク幅に追従)
    private TMP_Text songNameText;
    private RectTransform songIconRect;
    private string lastSongText;

    // 曲名の右端 x(キャンバス中心基準)。時計等の他 UI と揃える基準線。
    private const float SongRightEdgeX = 930f;

    private bool built;

    private void Awake()
    {
        Build();
    }

    private void Build()
    {
        if (built) return;

        // 既存バーを掴む。BarFill を左端起点に張り替えて進捗で伸ばせるようにする。
        Transform bb = transform.Find("BarBack");
        if (bb != null)
        {
            barBack = (RectTransform)bb;
            // ユーザー確定レイアウト: 進捗バーは上部帯の中央へ。左のステータス
            // カード・右の曲名と干渉しない幅に詰める(シーンは左寄り1030幅)。
            barBack.anchoredPosition = new Vector2(0f, 470f);
            barBack.sizeDelta = new Vector2(700f, barBack.sizeDelta.y);
            Transform bf = bb.Find("BarFill");
            if (bf != null)
            {
                barFill = (RectTransform)bf;
                // 左端固定でスケールでなく幅で伸ばす。
                barFill.anchorMin = new Vector2(0f, 0.5f);
                barFill.anchorMax = new Vector2(0f, 0.5f);
                barFill.pivot = new Vector2(0f, 0.5f);
                barFill.anchoredPosition = new Vector2(0f, 0f);
                Image bfImg = barFill.GetComponent<Image>();
                if (bfImg != null) bfImg.color = FillBlue;
                // 先端のシアン発光(縦線)。
                barFillGlow = NewImage("BarFillGlow", barBack, CyanBright);
                barFillGlow.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                barFillGlow.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                barFillGlow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                barFillGlow.rectTransform.sizeDelta = new Vector2(4f, barBack.sizeDelta.y + 6f);
                Color g = CyanBright; g.a = 0.9f; barFillGlow.color = g;
            }
            Image bbImg = bb.GetComponent<Image>();
            if (bbImg != null) bbImg.color = TrackGray;

            // 経過/全体の時刻テキスト(バー右端の外側)。
            barTimeText = NewText("BarTime", barBack, "0:00 / 0:00", 20f, new Color(0.72f, 0.86f, 0.95f, 0.9f), TextAlignmentOptions.Left);
            RectTransform tr = (RectTransform)barTimeText.transform;
            tr.anchorMin = new Vector2(1f, 0.5f);
            tr.anchorMax = new Vector2(1f, 0.5f);
            tr.pivot = new Vector2(0f, 0.5f);
            tr.anchoredPosition = new Vector2(14f, 0f);
            tr.sizeDelta = new Vector2(220f, 30f);
        }

        // 右端の曲名グループ。曲名は右揃えで基準線 SongRightEdgeX に合わせ、
        // ♪アイコンは曲名のインク幅に追従して文字の左に付く(曲名は可変長のため。
        // 追従は Update で行う)。フォントもここから拝借(シーン既定 TMP)。
        Transform songName = transform.Find("SongName");
        if (songName != null)
        {
            songNameText = songName.GetComponent<TMP_Text>();
            if (songNameText != null)
            {
                font = songNameText.font;
                songNameText.alignment = TextAlignmentOptions.Right;
                RectTransform nr = (RectTransform)songName.transform;
                nr.anchoredPosition = new Vector2(SongRightEdgeX - nr.sizeDelta.x * 0.5f, 468f);
            }
        }
        Transform songIcon = transform.Find("SongIcon");
        if (songIcon != null) songIconRect = (RectTransform)songIcon;

        // (c) 被弾/スコアのミニカード(上部帯の左)。
        scoreCard = BuildStatCard("ScoreCard", "スコア", "SCORE", out scoreValue);
        hitCard = BuildStatCard("HitCard", "被弾", "HIT", out hitValue);
        ApplyLayout();

        built = true;
    }

    // ステータスミニカード: 半透明の濃紺板 + 上辺シアンリム + 2隅の白ブラケット +
    // シアンの英字ラベル + 白の数値(リザルトカードの控えめ縮小版)。
    private RectTransform BuildStatCard(string name, string jp, string en, out TMP_Text value)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(CanvasGroup), typeof(Image));
        go.layer = gameObject.layer;
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(transform, false);
        rect.sizeDelta = new Vector2(240f, 66f);
        Image bg = go.GetComponent<Image>();
        bg.color = PanelNavy;
        bg.raycastTarget = false;
        CanvasGroup cg = go.GetComponent<CanvasGroup>();
        cg.alpha = 0.9f; // 上部帯の上に載るため弾との重なりは少ない。わずかに透かして帯と馴染ませる

        // 上辺シアンリム。
        Image rim = NewImage("Rim", rect, Cyan);
        rim.rectTransform.anchorMin = new Vector2(0f, 1f);
        rim.rectTransform.anchorMax = new Vector2(1f, 1f);
        rim.rectTransform.pivot = new Vector2(0.5f, 1f);
        rim.rectTransform.sizeDelta = new Vector2(0f, 2.5f);
        rim.rectTransform.anchoredPosition = Vector2.zero;
        Color rc = Cyan; rc.a = 0.9f; rim.color = rc;

        // 2隅(左上・右下)の白ブラケット。
        AddBracket(rect, new Vector2(0f, 1f));
        AddBracket(rect, new Vector2(1f, 0f));

        // ラベル(JP 白グレー + シアン英字)。
        TMP_Text label = NewText("Label", rect, jp + "  <size=13><color=#38C2E0>" + en + "</color></size>",
            18f, new Color(0.80f, 0.83f, 0.88f, 1f), TextAlignmentOptions.Left);
        RectTransform lr = (RectTransform)label.transform;
        lr.anchorMin = lr.anchorMax = new Vector2(0f, 1f);
        lr.pivot = new Vector2(0f, 1f);
        lr.anchoredPosition = new Vector2(14f, -8f);
        lr.sizeDelta = new Vector2(220f, 22f);

        // 数値(白・やや大きめ)。
        value = NewText("Value", rect, "0", 26f, ValueWhite, TextAlignmentOptions.Right);
        RectTransform vr = (RectTransform)value.transform;
        vr.anchorMin = vr.anchorMax = new Vector2(1f, 0f);
        vr.pivot = new Vector2(1f, 0f);
        vr.anchoredPosition = new Vector2(-14f, 6f);
        vr.sizeDelta = new Vector2(220f, 34f);
        value.characterSpacing = 2f;

        return rect;
    }

    // ユーザー確定レイアウト: 上部帯(y 420..540)の左に 被弾(左端)→スコア を横並び。
    // カード66px は帯の縦中央(上から27px)に収める。
    public void ApplyLayout()
    {
        if (scoreCard == null || hitCard == null) return;
        AnchorCorner(hitCard, new Vector2(0f, 1f), new Vector2(30f, -27f));
        AnchorCorner(scoreCard, new Vector2(0f, 1f), new Vector2(30f + 240f + 14f, -27f));
    }

    private static void AnchorCorner(RectTransform rect, Vector2 anchor, Vector2 offset)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        // pivot を隅に合わせ、offset で内側へ寄せる。
        rect.pivot = new Vector2(anchor.x, anchor.y);
        rect.anchoredPosition = offset;
    }

    private void AddBracket(RectTransform parent, Vector2 corner)
    {
        float sx = corner.x < 0.5f ? 1f : -1f;
        float sy = corner.y < 0.5f ? 1f : -1f;
        Image h = NewImage("BracketH", parent, BracketWhite);
        h.rectTransform.anchorMin = h.rectTransform.anchorMax = corner;
        h.rectTransform.pivot = new Vector2(corner.x, corner.y);
        h.rectTransform.sizeDelta = new Vector2(14f, 2.5f);
        h.rectTransform.anchoredPosition = new Vector2(sx * 3f, sy * 3f);
        Image v = NewImage("BracketV", parent, BracketWhite);
        v.rectTransform.anchorMin = v.rectTransform.anchorMax = corner;
        v.rectTransform.pivot = new Vector2(corner.x, corner.y);
        v.rectTransform.sizeDelta = new Vector2(2.5f, 14f);
        v.rectTransform.anchoredPosition = new Vector2(sx * 3f, sy * 3f);
    }

    private void Update()
    {
        GManager gm = GManager.Control;
        if (gm == null) return;
        bool playing = gm.state == GManager.GameState.Playing;

        // スコア/被弾カードはプレイ中のみ表示(それ以外は隠す)。
        if (scoreCard != null) scoreCard.gameObject.SetActive(playing);
        if (hitCard != null) hitCard.gameObject.SetActive(playing);

        if (!playing) return;
        StageReader sr = gm.SReader;
        if (sr == null || !sr.IsReady) return;

        // 曲名は右揃え(右端基準線)。♪アイコンをインク幅に追従させて文字の左に置く。
        if (songNameText != null && songIconRect != null && songNameText.text != lastSongText)
        {
            lastSongText = songNameText.text;
            songNameText.ForceMeshUpdate();
            songIconRect.anchoredPosition = new Vector2(
                SongRightEdgeX - songNameText.preferredWidth - 46f, 470f);
        }

        // (b) 進捗バー。
        float end = sr.EndTime;
        float cur = sr.CurrentTime;
        float progress = end > 0.001f ? Mathf.Clamp01(cur / end) : 0f;
        if (barBack != null && barFill != null)
        {
            float trackW = barBack.rect.width;
            float w = trackW * progress;
            barFill.sizeDelta = new Vector2(w, barFill.sizeDelta.y);
            if (barFillGlow != null)
                barFillGlow.rectTransform.anchoredPosition = new Vector2(w, 0f);
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

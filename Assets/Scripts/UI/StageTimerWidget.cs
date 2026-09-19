using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ステージ選択・難易度選択の「残り時間」を出すストップウォッチ型タイマー
/// (Highland Timer v14。2026-09-19 にユーザーが Astra Pro へ依頼した素材)。
///
/// 出典 <c>Instructions/UI/from_user_20260919/timer_v14/Highland_Timer_v14/</c>。
/// 値の正は同梱の <c>specification.json</c>:
/// ・タイマーのキャンバスは 820x336。時計の中心 (146,182)・半径 107・外周 5.8
/// ・「TIME」Noto Serif 28px・字間 7・#D7B66A・(332,78) 左揃え
/// ・数字 212px・字間 6・#FFE393・基線 (315,266) 左揃え・<b>2 桁固定</b>
/// ・単位「s」112px・(565.436,267)
/// ・残り 5 秒からコーラル #FF7962 で 1 秒周期の控えめな明滅(全消灯しない)
/// ・針は総秒数で 1 回転
/// ・画面(1672x941)の左下 x=42 / y=757.88 / scale 0.42
///
/// 絵は SVG をラスタライズせず、同じ実値からここで焼く
/// (フォントの無い環境で崩れないようにする流儀は <see cref="HighlandUi"/> と同じ)。
/// 針だけ別スプライトにして回す。数字・TIME・s は TMP。
///
/// 街の 3D の上に直接載るので、背後に「左下の隅へ向かって濃くなる暗幕」を敷き、
/// 時計と文字には柔らかいドロップシャドウを付ける(視認性の指示)。
/// </summary>
public class StageTimerWidget
{
    // ---- v14 の実値 --------------------------------------------------------
    private const float CanvasW = 820f, CanvasH = 336f;
    private const float StageX = 42f, StageY = 757.88f, StageScale = 0.42f;
    private const float DialCx = 146f, DialCy = 182f, DialR = 107f;
    private const float WarningSeconds = 5f;
    private static readonly Color ColorNormal = new Color(1f, 0.890f, 0.576f, 1f);   // #FFE393
    private static readonly Color ColorWarning = new Color(1f, 0.475f, 0.384f, 1f);  // #FF7962
    private static readonly Color LabelGold = new Color(0.843f, 0.714f, 0.416f, 1f); // #D7B66A

    // タイマー座標 → 画面(1672x941)の SVG 座標。
    private static float Tx(float v) { return StageX + v * StageScale; }
    private static float Ty(float v) { return StageY + v * StageScale; }
    private static float Tl(float v) { return v * StageScale; }

    private readonly List<Texture2D> ownedTextures = new List<Texture2D>();
    private readonly List<Sprite> ownedSprites = new List<Sprite>();

    private RectTransform root;
    private CanvasGroup group;
    private RectTransform needleRect;
    private TMP_Text valueText, unitText, labelText;
    private Image dialImage, pivotImage;
    private float shownAlpha;

    public bool Built { get { return root != null; } }

    /// <summary>親(選択画面のルート)の下に組む。1 度だけ呼ぶ。</summary>
    public void Build(Transform parent)
    {
        if (root != null) return;
        GameObject go = new GameObject("StageTimer", typeof(RectTransform), typeof(CanvasGroup));
        go.layer = parent.gameObject.layer;
        root = (RectTransform)go.transform;
        root.SetParent(parent, false);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = Vector2.zero;
        root.sizeDelta = Vector2.zero;
        group = go.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        // --- 背後の暗幕(左下の隅へ向かって濃くなる) ---
        Image scrim = NewImage("Scrim", root, Color.white);
        scrim.sprite = CornerScrimSprite();
        RectTransform sr = scrim.rectTransform;
        sr.anchorMin = sr.anchorMax = new Vector2(0f, 0f);
        sr.pivot = new Vector2(0f, 0f);
        sr.anchoredPosition = Vector2.zero;
        sr.sizeDelta = new Vector2(HighlandUi.L(560f), HighlandUi.L(360f));

        // --- 時計(針より奥) ---
        // 焼き込み範囲はタイマー座標の x[30,258] y[28,298]。
        const float artX0 = 30f, artY0 = 28f, artX1 = 258f, artY1 = 298f;
        float artW = artX1 - artX0, artH = artY1 - artY0;
        int pxW = Mathf.RoundToInt(HighlandUi.L(Tl(artW)));
        int pxH = Mathf.RoundToInt(HighlandUi.L(Tl(artH)));
        dialImage = NewImage("Dial", root, Color.white);
        dialImage.sprite = DialSprite(pxW * 2, pxH * 2, artX0, artY0, artW, artH);
        Place(dialImage.rectTransform, Tx((artX0 + artX1) * 0.5f), Ty((artY0 + artY1) * 0.5f),
            Tl(artW), Tl(artH));

        // --- 針(中心で回す) ---
        // 針の形は x[-9,9] y[-75,9](原点 = 文字盤の中心)。
        const float nx0 = -9f, ny0 = -75f, nx1 = 9f, ny1 = 9f;
        float nW = nx1 - nx0, nH = ny1 - ny0;
        Image needle = NewImage("Needle", root, Color.white);
        needle.sprite = NeedleSprite(
            Mathf.RoundToInt(HighlandUi.L(Tl(nW)) * 3f), Mathf.RoundToInt(HighlandUi.L(Tl(nH)) * 3f));
        needleRect = needle.rectTransform;
        // 回転の中心(= 文字盤の中心)がスプライトのどこにあるかを pivot で指す。
        needleRect.pivot = new Vector2((0f - nx0) / nW, (ny1 - 0f) / nH);
        needleRect.anchoredPosition = new Vector2(HighlandUi.X(Tx(DialCx)), HighlandUi.Y(Ty(DialCy)));
        needleRect.sizeDelta = new Vector2(HighlandUi.L(Tl(nW)), HighlandUi.L(Tl(nH)));

        // --- 中心の軸(針より手前) ---
        pivotImage = NewImage("Pivot", root, Color.white);
        int ps = Mathf.RoundToInt(HighlandUi.L(Tl(36f)) * 3f);
        pivotImage.sprite = PivotSprite(ps);
        Place(pivotImage.rectTransform, Tx(DialCx), Ty(DialCy), Tl(36f), Tl(36f));

        // --- 見出し「TIME」と飾り罫 + 菱形 ---
        labelText = HighlandUi.Text("TimerLabel", root, "TIME", Tl(28f), LabelGold,
            TextAlignmentOptions.Left, false, Tl(7f));
        HighlandUi.PlaceLeft(labelText, Tx(332f), Ty(78f), Tl(220f), Tl(28f));
        AddRule(Tx(449f), Tx(513f), Ty(68f), 0.12f, 0.85f, "TimerRuleL");
        AddRule(Tx(558f), Tx(779f), Ty(68f), 0.88f, 0f, "TimerRuleR");
        Image gem = NewImage("TimerGem", root, new Color(1f, 0.914f, 0.569f, 1f));
        gem.sprite = HighlandUi.DiamondRect(
            Mathf.RoundToInt(HighlandUi.L(Tl(18.4f)) * 3f),
            Mathf.RoundToInt(HighlandUi.L(Tl(25f)) * 3f), false, 3.5f * StageScale * HighlandUi.S,
            ownedTextures, ownedSprites, "TimerGem");
        Place(gem.rectTransform, Tx(536f), Ty(68f), Tl(18.4f), Tl(25f));

        // --- 数字と単位 ---
        valueText = HighlandUi.Text("TimerValue", root, "30", Tl(212f), ColorNormal,
            TextAlignmentOptions.Left, false, Tl(6f));
        HighlandUi.PlaceLeft(valueText, Tx(315f), Ty(266f), Tl(320f), Tl(212f));
        unitText = HighlandUi.Text("TimerUnit", root, "s", Tl(112f), ColorNormal,
            TextAlignmentOptions.Left, false, 0f);
        HighlandUi.PlaceLeft(unitText, Tx(565.436f), Ty(267f), Tl(140f), Tl(112f));

        // 文字のドロップシャドウ(街の明るい区画の上でも読めるように)。
        AddShadow(labelText, 0.55f);
        AddShadow(valueText, 0.75f);
        AddShadow(unitText, 0.75f);

        root.gameObject.SetActive(false);
    }

    /// <summary>残り秒数を反映する。<paramref name="show"/> が false ならフェードで消す。</summary>
    public void Tick(float remaining, float total, bool show, float dt)
    {
        if (root == null) return;
        shownAlpha = Mathf.MoveTowards(shownAlpha, show ? 1f : 0f, dt / 0.25f);
        bool alive = shownAlpha > 0.002f;
        if (root.gameObject.activeSelf != alive) root.gameObject.SetActive(alive);
        if (!alive) return;
        group.alpha = shownAlpha;

        float sec = Mathf.Max(0f, remaining);
        // 表示は切り上げの整数 2 桁(30 → 00)。
        int shown = Mathf.Clamp(Mathf.CeilToInt(sec - 0.0001f), 0, 99);
        string s = shown.ToString("00");
        if (valueText.text != s) valueText.text = s;

        // 針は「残り / 総秒数」に比例。0 で真上へ戻らず 1 回転して止まる。
        float t = total > 0.01f ? Mathf.Clamp01(sec / total) : 0f;
        needleRect.localEulerAngles = new Vector3(0f, 0f, -360f * (1f - t));

        // 残り 5 秒からコーラルで 1 秒周期の明滅(全消灯しない)。
        Color c = ColorNormal;
        if (sec <= WarningSeconds && sec > 0f)
        {
            float pulse = 0.72f + 0.28f * (0.5f + 0.5f * Mathf.Cos(sec * Mathf.PI * 2f));
            c = ColorWarning * pulse;
            c.a = 1f;
        }
        else if (sec <= 0f)
        {
            c = ColorWarning;
        }
        valueText.color = c;
        unitText.color = c;
    }

    public void Release()
    {
        foreach (Sprite sp in ownedSprites) if (sp != null) Object.Destroy(sp);
        foreach (Texture2D tex in ownedTextures) if (tex != null) Object.Destroy(tex);
        ownedSprites.Clear();
        ownedTextures.Clear();
    }

    // ---- 組み立ての小道具 --------------------------------------------------

    private static Image NewImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = parent.gameObject.layer;
        Image img = go.GetComponent<Image>();
        img.rectTransform.SetParent(parent, false);
        img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static void Place(RectTransform rt, float svgCx, float svgCy, float svgW, float svgH)
    {
        rt.anchoredPosition = new Vector2(HighlandUi.X(svgCx), HighlandUi.Y(svgCy));
        rt.sizeDelta = new Vector2(HighlandUi.L(svgW), HighlandUi.L(svgH));
    }

    private void AddRule(float svgX0, float svgX1, float svgY, float a0, float a1, string name)
    {
        int w = Mathf.Max(8, Mathf.RoundToInt(HighlandUi.L(svgX1 - svgX0)));
        Image img = NewImage(name, root, Color.white);
        img.sprite = HighlandUi.FadeRule(w, 8, 1.5f * StageScale * HighlandUi.S, 0f,
            new[] { 0f, 1f },
            new[] { new Color32(0xFF, 0xD8, 0x70, 0xFF), new Color32(0xFF, 0xD8, 0x70, 0xFF) },
            new[] { a0, a1 }, ownedTextures, ownedSprites, name);
        img.rectTransform.anchoredPosition = new Vector2(
            HighlandUi.X((svgX0 + svgX1) * 0.5f), HighlandUi.Y(svgY));
        img.rectTransform.sizeDelta = new Vector2(w, 8f);
    }

    // TMP のアンダーレイで柔らかい落ち影を付ける(街の 3D の上でも文字が浮く)。
    private static void AddShadow(TMP_Text text, float alpha)
    {
        if (text == null) return;
        Material mat = text.fontMaterial;
        if (mat == null) return;
        mat.EnableKeyword("UNDERLAY_ON");
        mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, alpha));
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.55f);
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.55f);
        mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.18f);
        mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.32f);
        if (mat.HasProperty(ShaderUtilities.ID_ScaleRatio_C))
            mat.SetFloat(ShaderUtilities.ID_ScaleRatio_C, 1f);
        text.UpdateMeshPadding();
    }

    // ---- 焼き込み ----------------------------------------------------------

    private static Color32 Lerp32(Color32 a, Color32 b, float t)
    {
        return Color32.Lerp(a, b, Mathf.Clamp01(t));
    }

    // Rim_Gold のグラデ(左上→右下)。
    private static readonly Color32[] RimStops =
    {
        new Color32(0xFF, 0xF4, 0xB7, 0xFF), new Color32(0xFF, 0xE6, 0x83, 0xFF),
        new Color32(0xE4, 0xBE, 0x5D, 0xFF), new Color32(0xCC, 0xA6, 0x4C, 0xFF),
        new Color32(0xE7, 0xC6, 0x63, 0xFF),
    };
    private static readonly float[] RimOffsets = { 0f, 0.25f, 0.57f, 0.82f, 1f };

    private static Color32 RimGold(float t)
    {
        t = Mathf.Clamp01(t);
        for (int i = 1; i < RimOffsets.Length; i++)
            if (t <= RimOffsets[i])
                return Lerp32(RimStops[i - 1], RimStops[i],
                    Mathf.InverseLerp(RimOffsets[i - 1], RimOffsets[i], t));
        return RimStops[RimStops.Length - 1];
    }

    private Sprite DialSprite(int W, int H, float ox, float oy, float w, float h)
    {
        Color32[] px = new Color32[W * H];
        float k = W / w;                       // タイマー座標 → 焼き込み画素
        System.Func<float, float> U = v => (v - ox) * k;
        System.Func<float, float> V = v => H - 1f - (v - oy) * k;   // y 反転(UI は上が +)

        float cx = U(DialCx), cy = V(DialCy);

        // 文字盤の地(中心が少し明るい紺)。
        Ring(px, W, H, cx, cy, 0f, DialR * k - 3f * k, (d, ang) =>
        {
            float t = Mathf.Clamp01(d / (DialR * k));
            Color32 c = t < 0.6f
                ? Lerp32(new Color32(0x1D, 0x20, 0x39, 0xFF), new Color32(0x13, 0x15, 0x2B, 0xFF), t / 0.6f)
                : Lerp32(new Color32(0x13, 0x15, 0x2B, 0xFF), new Color32(0x0C, 0x0F, 0x23, 0xFF), (t - 0.6f) / 0.4f);
            // 見本は紺地の上なので半透明(0.56〜0.88)だが、ここは街の 3D の上に直接
            // 載るので、数字と針が読めるようほぼ不透明にする(視認性の指示)。
            return new Color(c.r / 255f, c.g / 255f, c.b / 255f, Mathf.Lerp(0.93f, 0.985f, t));
        });

        // 冠(上の押しボタン)と軸。文字盤より先に描いて後ろへ回す。
        StrokeRect(px, W, H, U(133f), V(77f), U(159f), V(58f), 2f * k, RimGold(0.3f), 1f,
            new Color(0.082f, 0.086f, 0.165f, 1f));
        StrokeRect(px, W, H, U(120f), V(59f), U(172f), V(36f), 3.4f * k, RimGold(0.15f), 1f,
            new Color(0.106f, 0.106f, 0.180f, 0.78f));
        // 斜めの押しボタン(45 度・タイマー座標 (233,91) が中心)。
        StrokeRotRect(px, W, H, U(233f), V(91f), 27f * k, 17f * k, 45f, 2.8f * k,
            RimGold(0.4f), new Color(0.090f, 0.098f, 0.165f, 0.55f));

        // 外周の二重リング。
        Ring(px, W, H, cx, cy, (DialR - 2.9f) * k, (DialR + 2.9f) * k,
            (d, ang) => RimAt(ang, 1f));
        Ring(px, W, H, cx, cy, (95.1f - 0.8f) * k, (95.1f + 0.8f) * k,
            (d, ang) => RimAt(ang, 0.74f));
        // 上側のハイライト。
        Arc(px, W, H, cx, cy, DialR * k, 90f, 180f, 1.1f * k,
            new Color(1f, 0.961f, 0.788f, 0.70f));

        // 12/3/6/9 の菱形。
        Diamond(px, W, H, U(146f), V(117f), 7.2f * k, 18f * k, RimAt(0.2f, 1f));
        Diamond(px, W, H, U(224f), V(182f), 8.5f * k, 4.2f * k, RimAt(0.5f, 1f));
        Diamond(px, W, H, U(146f), V(257f), 4.2f * k, 8.6f * k, RimAt(0.7f, 1f));
        Diamond(px, W, H, U(68f), V(182f), 8.5f * k, 4.2f * k, RimAt(0.5f, 1f));

        return MakeSprite(px, W, H, "HighlandTimerDial");
    }

    private static Color RimAt(float t, float alpha)
    {
        Color32 c = RimGold(t);
        return new Color(c.r / 255f, c.g / 255f, c.b / 255f, alpha);
    }

    private Sprite NeedleSprite(int W, int H)
    {
        Color32[] px = new Color32[W * H];
        // 形は (0,-74) - (8,-4) - (0,8) - (-8,-4)。x[-9,9] y[-75,9] を W x H に写す。
        float k = W / 18f;
        System.Func<float, float> U = v => (v + 9f) * k;
        System.Func<float, float> V = v => H - 1f - (v + 75f) * k;
        Vector2 a = new Vector2(U(0f), V(-74f));
        Vector2 b = new Vector2(U(8f), V(-4f));
        Vector2 c = new Vector2(U(0f), V(8f));
        Vector2 d = new Vector2(U(-8f), V(-4f));
        Tri(px, W, H, a, b, c, new Color(0.890f, 0.694f, 0.357f, 0.72f));
        Tri(px, W, H, a, c, d, new Color(1f, 0.973f, 0.859f, 1f));
        return MakeSprite(px, W, H, "HighlandTimerNeedle");
    }

    private Sprite PivotSprite(int S)
    {
        Color32[] px = new Color32[S * S];
        float c = (S - 1) * 0.5f;
        float k = S / 36f;
        Ring(px, S, S, c, c, 0f, 16f * k, (d, ang) => RimAt(Mathf.Clamp01(d / (16f * k)), 1f));
        Ring(px, S, S, c, c, 15f * k, 16.6f * k, (d, ang) => new Color(1f, 0.941f, 0.686f, 1f));
        Ring(px, S, S, c, c, 0f, 4.1f * k, (d, ang) => new Color(0.612f, 0.454f, 0.235f, 1f));
        return MakeSprite(px, S, S, "HighlandTimerPivot");
    }

    // 左下の隅へ向かって濃くなる暗幕(最大 α 0.55)。
    private Sprite CornerScrimSprite()
    {
        const int W = 128, H = 96;
        Color32[] px = new Color32[W * H];
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                float u = x / (W - 1f);
                float v = 1f - y / (H - 1f);      // 下が 0
                float d = Mathf.Clamp01(Mathf.Sqrt(u * u * 0.72f + v * v));
                float a = 0.55f * Mathf.Pow(1f - d, 1.6f);
                px[y * W + x] = new Color32(2, 4, 12, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
            }
        }
        return MakeSprite(px, W, H, "HighlandTimerScrim", FilterMode.Bilinear);
    }

    // ---- 画素の道具 --------------------------------------------------------

    private Sprite MakeSprite(Color32[] px, int w, int h, string name,
        FilterMode filter = FilterMode.Bilinear)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = name,
            hideFlags = HideFlags.DontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = filter,
        };
        tex.SetPixels32(px);
        tex.Apply(false);
        Sprite sp = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f);
        sp.name = name;
        sp.hideFlags = HideFlags.DontSave;
        ownedTextures.Add(tex);
        ownedSprites.Add(sp);
        return sp;
    }

    private static void Blend(Color32[] px, int W, int H, int x, int y, Color c, float a)
    {
        if (a <= 0f || x < 0 || y < 0 || x >= W || y >= H) return;
        a = Mathf.Clamp01(a) * c.a;
        int i = y * W + x;
        Color32 dst = px[i];
        float da = dst.a / 255f;
        float outA = a + da * (1f - a);
        if (outA <= 0.0001f) { px[i] = new Color32(0, 0, 0, 0); return; }
        float r = (c.r * a + dst.r / 255f * da * (1f - a)) / outA;
        float g = (c.g * a + dst.g / 255f * da * (1f - a)) / outA;
        float b = (c.b * a + dst.b / 255f * da * (1f - a)) / outA;
        px[i] = new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(r) * 255f),
            (byte)Mathf.RoundToInt(Mathf.Clamp01(g) * 255f),
            (byte)Mathf.RoundToInt(Mathf.Clamp01(b) * 255f),
            (byte)Mathf.RoundToInt(Mathf.Clamp01(outA) * 255f));
    }

    // 半径 r0..r1 のリング(r0=0 なら円盤)。色は距離と角度(0..1)から決める。
    private static void Ring(Color32[] px, int W, int H, float cx, float cy,
        float r0, float r1, System.Func<float, float, Color> shade)
    {
        int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - r1 - 2f)), x1 = Mathf.Min(W - 1, Mathf.CeilToInt(cx + r1 + 2f));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - r1 - 2f)), y1 = Mathf.Min(H - 1, Mathf.CeilToInt(cy + r1 + 2f));
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                float dx = x - cx, dy = y - cy;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float cov = Mathf.Min(Mathf.Clamp01(r1 - d + 0.5f),
                    r0 <= 0f ? 1f : Mathf.Clamp01(d - r0 + 0.5f));
                if (cov <= 0f) continue;
                // 左上→右下を 0→1 に。
                float t = Mathf.Clamp01(((dx / Mathf.Max(1f, r1)) + (-dy / Mathf.Max(1f, r1))) * 0.5f + 0.5f);
                Blend(px, W, H, x, y, shade(d, 1f - t), cov);
            }
        }
    }

    private static void Arc(Color32[] px, int W, int H, float cx, float cy, float r,
        float degFrom, float degTo, float width, Color col)
    {
        for (float a = degFrom; a <= degTo; a += 0.35f)
        {
            float rad = a * Mathf.Deg2Rad;
            float x = cx + r * Mathf.Cos(rad), y = cy - r * Mathf.Sin(rad);
            Dot(px, W, H, x, y, width * 0.5f, col);
        }
    }

    private static void Dot(Color32[] px, int W, int H, float cx, float cy, float r, Color col)
    {
        int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - r - 1)), x1 = Mathf.Min(W - 1, Mathf.CeilToInt(cx + r + 1));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - r - 1)), y1 = Mathf.Min(H - 1, Mathf.CeilToInt(cy + r + 1));
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                Blend(px, W, H, x, y, col, Mathf.Clamp01(r - d + 0.5f));
            }
    }

    private static void Diamond(Color32[] px, int W, int H, float cx, float cy,
        float hw, float hh, Color col)
    {
        int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - hw - 1)), x1 = Mathf.Min(W - 1, Mathf.CeilToInt(cx + hw + 1));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - hh - 1)), y1 = Mathf.Min(H - 1, Mathf.CeilToInt(cy + hh + 1));
        float norm = 1f / Mathf.Sqrt(1f / (hw * hw) + 1f / (hh * hh));
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float m = Mathf.Abs(x - cx) / hw + Mathf.Abs(y - cy) / hh - 1f;
                Blend(px, W, H, x, y, col, Mathf.Clamp01(0.5f - m * norm));
            }
    }

    private static void Tri(Color32[] px, int W, int H, Vector2 a, Vector2 b, Vector2 c, Color col)
    {
        int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)) - 1));
        int x1 = Mathf.Min(W - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)) + 1));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y)) - 1));
        int y1 = Mathf.Min(H - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y)) + 1));
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                // 4x4 のサブサンプルで被覆率を出す。
                int hit = 0;
                for (int sy = 0; sy < 4; sy++)
                    for (int sx = 0; sx < 4; sx++)
                    {
                        Vector2 p = new Vector2(x + (sx + 0.5f) / 4f, y + (sy + 0.5f) / 4f);
                        if (Inside(p, a, b, c)) hit++;
                    }
                if (hit > 0) Blend(px, W, H, x, y, col, hit / 16f);
            }
    }

    private static bool Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(p, a, b), d2 = Cross(p, b, c), d3 = Cross(p, c, a);
        bool neg = d1 < 0f || d2 < 0f || d3 < 0f;
        bool pos = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(neg && pos);
    }

    private static float Cross(Vector2 p, Vector2 a, Vector2 b)
    {
        return (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
    }

    // 枠線つきの矩形(y は既に画素座標)。
    private static void StrokeRect(Color32[] px, int W, int H, float x0, float y0,
        float x1, float y1, float stroke, Color edge, float edgeAlpha, Color fill)
    {
        float ax0 = Mathf.Min(x0, x1), ax1 = Mathf.Max(x0, x1);
        float ay0 = Mathf.Min(y0, y1), ay1 = Mathf.Max(y0, y1);
        for (int y = Mathf.Max(0, (int)(ay0 - stroke - 1)); y <= Mathf.Min(H - 1, (int)(ay1 + stroke + 1)); y++)
            for (int x = Mathf.Max(0, (int)(ax0 - stroke - 1)); x <= Mathf.Min(W - 1, (int)(ax1 + stroke + 1)); x++)
            {
                float d = Mathf.Max(Mathf.Max(ax0 - x, x - ax1), Mathf.Max(ay0 - y, y - ay1));
                Blend(px, W, H, x, y, fill, Mathf.Clamp01(0.5f - d));
                Blend(px, W, H, x, y, edge,
                    Mathf.Clamp01(stroke * 0.5f - Mathf.Abs(d)) * edgeAlpha);
            }
    }

    // 回転した矩形(中心 cx,cy・幅 w・高さ h・角度 deg)。
    private static void StrokeRotRect(Color32[] px, int W, int H, float cx, float cy,
        float w, float h, float deg, float stroke, Color edge, Color fill)
    {
        float cs = Mathf.Cos(deg * Mathf.Deg2Rad), sn = Mathf.Sin(deg * Mathf.Deg2Rad);
        float r = Mathf.Max(w, h);
        for (int y = Mathf.Max(0, (int)(cy - r)); y <= Mathf.Min(H - 1, (int)(cy + r)); y++)
            for (int x = Mathf.Max(0, (int)(cx - r)); x <= Mathf.Min(W - 1, (int)(cx + r)); x++)
            {
                float dx = x - cx, dy = y - cy;
                float u = dx * cs + dy * sn, v = -dx * sn + dy * cs;
                float d = Mathf.Max(Mathf.Abs(u) - w * 0.5f, Mathf.Abs(v) - h * 0.5f);
                Blend(px, W, H, x, y, fill, Mathf.Clamp01(0.5f - d));
                Blend(px, W, H, x, y, edge, Mathf.Clamp01(stroke * 0.5f - Mathf.Abs(d)));
            }
    }
}

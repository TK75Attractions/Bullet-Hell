using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 「Highland UI v11」(2026-09-19 にユーザーと Astra Pro が作った新 UI)の共通部品。
///
/// 出典は <c>Instructions/UI/from_user_20260919/v11/Highland_UI_Editable_v11/</c>。
/// SVG の編集マスターは 1672x941、ゲームは 1920x1080 なので、座標は一律
/// <see cref="S"/> = 1920/1672 = 1.1483 倍で読み替える(HUD だけ等倍)。
///
/// 文字は Unity(TMP)が描く。枠・罫・菱形・アイコンはここで焼き込みスプライトにする
/// (SVG をそのままラスタライズするとフォントの無い環境で崩れるため)。
/// 色は<b>視覚 sRGB 値をそのまま</b>頂点色・テクスチャへ渡す
/// (<c>Docs/result-design-language.md</c> §10 の訂正と同じ流儀)。
///
/// v11 の様式の要点(旧「夜の紺と金」§10 との違い):
/// ・枠は<b>四隅を円弧でえぐった角</b>(concave notch)の二重枠。外が金のグラデ 1.45px、
///   内が銀 0.9px。四隅のブラケット・翼形の曲線は無い。
/// ・上下中央に中空の菱形(crest)。
/// ・区切り罫は両端が消えるグラデで、中央に菱形ぶんの隙間を空ける。
/// ・アクセントはオレンジ寄りではない明るい黄 #FFE16A。
/// </summary>
public static class HighlandUi
{
    /// <summary>SVG マスター(1672x941) → 画面(1920x1080)の倍率。</summary>
    public const float S = 1920f / 1672f;

    /// <summary>SVG の x(0..1672) → Canvas 中央基準の x。</summary>
    public static float X(float svgX) { return (svgX - 836f) * S; }

    /// <summary>SVG の y(0..941・下向き) → Canvas 中央基準の y(上向き)。</summary>
    public static float Y(float svgY) { return (470.5f - svgY) * S; }

    /// <summary>SVG の長さ → 画面の長さ。</summary>
    public static float L(float len) { return len * S; }

    // =======================================================================
    //  色(視覚 sRGB)
    // =======================================================================

    private static Color C(int r, int g, int b, float a = 1f)
    {
        return new Color(r / 255f, g / 255f, b / 255f, a);
    }

    /// <summary>アクセントの明るい黄(palette_v11.json の accent_yellow)。</summary>
    public static readonly Color Accent = C(0xFF, 0xE1, 0x6A);
    public static readonly Color AccentBright = C(0xFF, 0xE4, 0x70);
    public static readonly Color AccentDim = C(0xBC, 0xAA, 0x4E);
    /// <summary>白(本文・値)。</summary>
    public static readonly Color Ink = C(0xFF, 0xFF, 0xFF);
    /// <summary>やや落とした白(ラベル)。</summary>
    public static readonly Color InkSoft = C(0xF2, 0xF2, 0xF2);
    /// <summary>さらに落とした灰(日付など)。</summary>
    public static readonly Color InkFaint = C(0xD6, 0xD6, 0xD6);
    public static readonly Color Silver = C(0xC5, 0xC5, 0xC5);
    public static readonly Color SilverDim = C(0x8A, 0x8A, 0x8A);

    // 焼き込み用(テクスチャに書く sRGB)。
    private static readonly Color32 TexPanelA = new Color32(0x13, 0x13, 0x29, 0xF7);
    private static readonly Color32 TexPanelB = new Color32(0x0D, 0x0E, 0x22, 0xF6);
    private static readonly Color32 TexPanelC = new Color32(0x10, 0x10, 0x24, 0xF7);
    private static readonly Color32 TexPanelD = new Color32(0x13, 0x16, 0x22, 0xF5);
    private static readonly Color32 TexBtnA = new Color32(0x24, 0x27, 0x35, 0xF7);
    private static readonly Color32 TexBtnB = new Color32(0x19, 0x1D, 0x29, 0xF7);
    private static readonly Color32 TexBtnC = new Color32(0x2B, 0x2F, 0x3C, 0xF7);
    // BorderGold のグラデ(左上→右下)。
    private static readonly Color32[] BorderGoldStops =
    {
        new Color32(0xFF, 0xE5, 0x75, 0xFF), new Color32(0xB9, 0xA3, 0x40, 0xFF),
        new Color32(0xE7, 0xCE, 0x57, 0xFF), new Color32(0xF4, 0xDF, 0x73, 0xFF),
        new Color32(0xB4, 0xA0, 0x3F, 0xFF),
    };
    private static readonly float[] BorderGoldOffsets = { 0f, 0.19f, 0.44f, 0.77f, 1f };
    private static readonly Color32 TexInnerSilver = new Color32(0xC8, 0xC8, 0xC8, 0xFF);

    // =======================================================================
    //  フォント
    // =======================================================================

    private static TMP_FontAsset serif;
    private static TMP_FontAsset serifBold;
    private static bool serifTried, serifBoldTried;

    /// <summary>Noto Serif JP Regular(和文・欧文とも)。</summary>
    public static TMP_FontAsset Serif
    {
        get
        {
            if (serif == null && !serifTried)
            {
                serifTried = true;
                serif = Resources.Load<TMP_FontAsset>("Fonts/NotoSerifJP-Regular SDF");
            }
            return serif;
        }
    }

    /// <summary>Noto Serif JP Bold(見出し・ステージ名)。</summary>
    public static TMP_FontAsset SerifBold
    {
        get
        {
            if (serifBold == null && !serifBoldTried)
            {
                serifBoldTried = true;
                serifBold = Resources.Load<TMP_FontAsset>("Fonts/NotoSerifJP-Bold SDF");
            }
            return serifBold ?? Serif;
        }
    }

    // =======================================================================
    //  形(四隅を円弧でえぐった角)
    // =======================================================================

    /// <summary>
    /// 四隅を半径 <paramref name="r"/> の円弧でえぐった矩形の符号付き距離。
    /// 中心 (0,0)・半幅 <paramref name="hw"/>・半高 <paramref name="hh"/>。負が内側。
    /// </summary>
    public static float NotchSdf(float ax, float ay, float hw, float hh, float r)
    {
        float cx = hw - r, cy = hh - r;
        if (ax > cx && ay > cy)
        {
            float dx = ax - cx, dy = ay - cy;
            return Mathf.Sqrt(dx * dx + dy * dy) - r;
        }
        return Mathf.Max(ax - hw, ay - hh);
    }

    private static Color32 SampleBorderGold(float t)
    {
        t = Mathf.Clamp01(t);
        for (int i = 1; i < BorderGoldOffsets.Length; i++)
        {
            if (t <= BorderGoldOffsets[i])
            {
                float k = Mathf.InverseLerp(BorderGoldOffsets[i - 1], BorderGoldOffsets[i], t);
                return Color32.Lerp(BorderGoldStops[i - 1], BorderGoldStops[i], k);
            }
        }
        return BorderGoldStops[BorderGoldStops.Length - 1];
    }

    // =======================================================================
    //  スプライト
    // =======================================================================

    /// <summary>
    /// v11 のパネル/ボタン共通の板。四隅をえぐった二重枠(外=金のグラデ・内=銀の細線)と
    /// 斜めの紺グラデ地、上下のほのかな光。<paramref name="ss"/> は焼き込み倍率。
    /// </summary>
    /// <param name="w">表示幅(px)</param>
    /// <param name="h">表示高さ(px)</param>
    /// <param name="notch">四隅の円弧半径(表示 px)</param>
    /// <param name="button">true = ボタンの地(やや明るい灰紺)</param>
    /// <param name="innerInset">内側の銀線の内寄せ(表示 px・0 で線なし)</param>
    public static Sprite NotchPanel(int w, int h, float notch, bool button,
        List<Texture2D> ownedTex, List<Sprite> ownedSpr, string name,
        float innerInset = 5.2f, float outerWidth = 1.45f, float innerWidth = 0.9f,
        float fillAlpha = 1f, int ss = 0)
    {
        if (ss <= 0) ss = Mathf.Clamp(Mathf.CeilToInt(1200f / Mathf.Max(w, h)), 2, 4);
        int W = w * ss, H = h * ss;
        Color32[] px = new Color32[W * H];
        float hw = W * 0.5f, hh = H * 0.5f;
        float cx = (W - 1) * 0.5f, cy = (H - 1) * 0.5f;
        float rN = notch * ss;
        float rInner = (notch + innerInset) * ss;
        float tOuter = outerWidth * ss;
        float tInner = innerWidth * ss;
        float inset = innerInset * ss;

        Color32 a = button ? TexBtnA : TexPanelA;
        Color32 b = button ? TexBtnB : TexPanelB;
        Color32 c2 = button ? TexBtnB : TexPanelC;
        Color32 d = button ? TexBtnC : TexPanelD;

        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                float ax = Mathf.Abs(x - cx), ay = Mathf.Abs(y - cy);
                float dOut = NotchSdf(ax, ay, hw - 0.5f, hh - 0.5f, rN);
                float inside = Mathf.Clamp01(0.5f - dOut);
                if (inside <= 0f) continue;

                // 地(ボタンは縦・パネルは斜めのグラデ)。
                float t = button
                    ? 1f - y / (float)(H - 1)
                    : (x / (float)(W - 1) + (1f - y / (float)(H - 1))) * 0.5f;
                Color32 fill;
                if (t < 0.35f) fill = Color32.Lerp(a, b, t / 0.35f);
                else if (t < 0.70f) fill = Color32.Lerp(b, c2, (t - 0.35f) / 0.35f);
                else fill = Color32.Lerp(c2, d, (t - 0.70f) / 0.30f);
                Blend(px, W, H, x, y, fill, inside * fillAlpha);

                // 上下のほのかな光(v11 の Sheen)。
                float ny = y / (float)(H - 1);
                float nx = (x - cx) / hw;
                float sheenTop = Mathf.Clamp01(1f - Mathf.Sqrt(nx * nx * 0.6f
                    + (ny - 0.88f) * (ny - 0.88f) * 9f));
                Blend(px, W, H, x, y, new Color(0.22f, 0.25f, 0.32f), inside * sheenTop * 0.17f);
                float sheenBottom = Mathf.Clamp01(1f - Mathf.Sqrt(nx * nx * 0.55f
                    + (ny - 0.07f) * (ny - 0.07f) * 13f));
                Blend(px, W, H, x, y, Color.white, inside * sheenBottom * 0.10f);

                // 外周の金枠(左上→右下のグラデ)。
                float gt = (x / (float)(W - 1) + y / (float)(H - 1)) * 0.5f;
                Blend(px, W, H, x, y, SampleBorderGold(1f - gt),
                    Mathf.Clamp01(tOuter * 0.5f - Mathf.Abs(dOut + tOuter * 0.5f)) * inside);

                // 内側の銀の細線。
                if (innerInset > 0f)
                {
                    float dIn = NotchSdf(ax, ay, hw - 0.5f - inset, hh - 0.5f - inset, rInner);
                    Blend(px, W, H, x, y, TexInnerSilver,
                        Mathf.Clamp01(tInner * 0.5f - Mathf.Abs(dIn) + 0.35f) * 0.68f * inside);
                }
            }
        }
        return MakeSprite(px, W, H, name, ownedTex, ownedSpr);
    }

    /// <summary>
    /// 枠だけ(地なし)の細い二重枠。CG サムネの縁などに使う。
    /// </summary>
    public static Sprite NotchFrame(int w, int h, float notch,
        List<Texture2D> ownedTex, List<Sprite> ownedSpr, string name,
        Color32 outer, float outerWidth = 1.2f, float outerAlpha = 0.69f,
        float innerInset = 3f, float innerAlpha = 0.38f, int ss = 0)
    {
        if (ss <= 0) ss = Mathf.Clamp(Mathf.CeilToInt(1200f / Mathf.Max(w, h)), 2, 4);
        int W = w * ss, H = h * ss;
        Color32[] px = new Color32[W * H];
        float hw = W * 0.5f, hh = H * 0.5f;
        float cx = (W - 1) * 0.5f, cy = (H - 1) * 0.5f;
        float rN = notch * ss, tOut = outerWidth * ss;
        float inset = innerInset * ss, rIn = (notch + innerInset) * ss;

        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                float ax = Mathf.Abs(x - cx), ay = Mathf.Abs(y - cy);
                float dOut = NotchSdf(ax, ay, hw - 0.5f, hh - 0.5f, rN);
                Blend(px, W, H, x, y, outer,
                    Mathf.Clamp01(tOut * 0.5f - Mathf.Abs(dOut + tOut * 0.5f)) * outerAlpha);
                if (innerInset > 0f)
                {
                    float dIn = NotchSdf(ax, ay, hw - 0.5f - inset, hh - 0.5f - inset, rIn);
                    Blend(px, W, H, x, y, outer,
                        Mathf.Clamp01(0.6f * ss - Mathf.Abs(dIn)) * innerAlpha);
                }
            }
        }
        return MakeSprite(px, W, H, name, ownedTex, ownedSpr);
    }

    /// <summary>
    /// 区切り罫。SVG の linearGradient の停止点(offset 0..1・色・不透明度)をそのまま
    /// 渡して焼く。中央には菱形ぶんの隙間 <paramref name="gapPx"/> を空ける。
    ///
    /// 縦方向は<b>表示寸法と 1:1</b>(高さ <paramref name="hPx"/>)で焼き、線を副画素の
    /// カバレッジで置く。2 倍などで焼いて縮小すると、細い横線はテクセル整列で
    /// 丸ごと消えることがある(§10.3.1 の落とし穴)。
    /// </summary>
    public static Sprite FadeRule(int wPx, int hPx, float lineWidthPx, float gapPx,
        float[] offsets, Color32[] colors, float[] alphas,
        List<Texture2D> ownedTex, List<Sprite> ownedSpr, string name)
    {
        int W = Mathf.Max(8, wPx);
        int H = Mathf.Max(4, hPx);
        Color32[] px = new Color32[W * H];
        float cyf = (H - 1) * 0.5f;
        float half = lineWidthPx * 0.5f;
        float cxf = (W - 1) * 0.5f;
        for (int x = 0; x < W; x++)
        {
            float t = x / (float)(W - 1);
            Color32 col = colors[colors.Length - 1];
            float a = alphas[alphas.Length - 1];
            for (int i = 1; i < offsets.Length; i++)
            {
                if (t <= offsets[i])
                {
                    float k = Mathf.InverseLerp(offsets[i - 1], offsets[i], t);
                    col = Color32.Lerp(colors[i - 1], colors[i], k);
                    a = Mathf.Lerp(alphas[i - 1], alphas[i], k);
                    break;
                }
            }
            // 中央の隙間(菱形が入る)。
            float dx = Mathf.Abs(x - cxf);
            if (dx < gapPx * 0.5f) continue;
            a *= Mathf.Clamp01(dx - gapPx * 0.5f);
            if (a <= 0f) continue;
            for (int y = 0; y < H; y++)
            {
                float cov = Mathf.Clamp01(half - Mathf.Abs(y - cyf) + 0.5f);
                Blend(px, W, H, x, y, col, cov * a);
            }
        }
        return MakeSprite(px, W, H, name, ownedTex, ownedSpr);
    }

    /// <summary>単色の細い横罫(ヘッダーの銀線など)。縦は表示と 1:1 で焼く。</summary>
    public static Sprite FlatRule(int wPx, int hPx, float lineWidthPx, Color32 col,
        List<Texture2D> ownedTex, List<Sprite> ownedSpr, string name)
    {
        float[] offs = { 0f, 1f };
        Color32[] cols = { col, col };
        float[] al = { 1f, 1f };
        return FadeRule(wPx, hPx, lineWidthPx, 0f, offs, cols, al, ownedTex, ownedSpr, name);
    }

    /// <summary>
    /// 四隅をえぐった角の「地 1 色 + 枠 1 本」の板。難易度の非選択行・サムネの地など。
    /// </summary>
    public static Sprite NotchFlat(int w, int h, float notch,
        Color32 fill, float fillAlpha, Color32 stroke, float strokeWidth, float strokeAlpha,
        List<Texture2D> ownedTex, List<Sprite> ownedSpr, string name, int ss = 0)
    {
        if (ss <= 0) ss = Mathf.Clamp(Mathf.CeilToInt(1200f / Mathf.Max(w, h)), 2, 4);
        int W = w * ss, H = h * ss;
        Color32[] px = new Color32[W * H];
        float hw = W * 0.5f, hh = H * 0.5f;
        float cx = (W - 1) * 0.5f, cy = (H - 1) * 0.5f;
        float rN = notch * ss, tOut = strokeWidth * ss;
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                float ax = Mathf.Abs(x - cx), ay = Mathf.Abs(y - cy);
                float d = NotchSdf(ax, ay, hw - 0.5f, hh - 0.5f, rN);
                float inside = Mathf.Clamp01(0.5f - d);
                if (inside > 0f && fillAlpha > 0f) Blend(px, W, H, x, y, fill, inside * fillAlpha);
                if (strokeAlpha > 0f)
                    Blend(px, W, H, x, y, stroke,
                        Mathf.Clamp01(tOut * 0.5f - Mathf.Abs(d + tOut * 0.5f)) * strokeAlpha);
            }
        }
        return MakeSprite(px, W, H, name, ownedTex, ownedSpr);
    }

    /// <summary>縦横比の違う菱形(中空/塗り)。表示寸法の 4 倍で焼く。</summary>
    public static Sprite DiamondRect(int w, int h, bool filled, float strokePx,
        List<Texture2D> ownedTex, List<Sprite> ownedSpr, string name)
    {
        const int ss = 4;
        int W = Mathf.Max(4, w) * ss, H = Mathf.Max(4, h) * ss;
        Color32[] px = new Color32[W * H];
        float cx = (W - 1) * 0.5f, cy = (H - 1) * 0.5f;
        float hw = W * 0.5f - 0.5f, hh = H * 0.5f - 0.5f;
        // 菱形 |x|/hw + |y|/hh = 1 の、画素距離へ直した符号付き距離。
        float norm = 1f / Mathf.Sqrt(1f / (hw * hw) + 1f / (hh * hh));
        float st = strokePx * ss;
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                float m = Mathf.Abs(x - cx) / hw + Mathf.Abs(y - cy) / hh - 1f;
                float d = m * norm;
                float cov = filled
                    ? Mathf.Clamp01(0.5f - d)
                    : Mathf.Clamp01(st * 0.5f - Mathf.Abs(d) + 0.5f);
                Blend(px, W, H, x, y, Color.white, cov);
            }
        }
        return MakeSprite(px, W, H, name, ownedTex, ownedSpr);
    }

    /// <summary>
    /// 難易度行の宝石(菱形)。塗りは右上を白 23%・左下を紺 15% で陰影づける
    /// (v11 の D_*_Badge と同じ)。色は Image.color で着ける。
    /// </summary>
    public static Sprite Gem(int w, int h, bool filled, float strokePx,
        List<Texture2D> ownedTex, List<Sprite> ownedSpr, string name)
    {
        const int ss = 4;
        int W = Mathf.Max(4, w) * ss, H = Mathf.Max(4, h) * ss;
        Color32[] px = new Color32[W * H];
        float cx = (W - 1) * 0.5f, cy = (H - 1) * 0.5f;
        float hw = W * 0.5f - 0.5f, hh = H * 0.5f - 0.5f;
        float norm = 1f / Mathf.Sqrt(1f / (hw * hw) + 1f / (hh * hh));
        float st = strokePx * ss;
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                float dx = x - cx, dy = y - cy;
                float m = Mathf.Abs(dx) / hw + Mathf.Abs(dy) / hh - 1f;
                float d = m * norm;
                if (filled)
                {
                    float cov = Mathf.Clamp01(0.5f - d);
                    if (cov <= 0f) continue;
                    Blend(px, W, H, x, y, Color.white, cov);
                    // 上半分(y が大きい側 = 画面の上)の右寄りを白く、下半分の左寄りを暗く。
                    if (dy > 0f && dx > 0f) Blend(px, W, H, x, y, Color.white, cov * 0.23f);
                    else if (dy < 0f && dx < 0f)
                        Blend(px, W, H, x, y, new Color(0.047f, 0.063f, 0.125f), cov * 0.15f);
                }
                else
                {
                    Blend(px, W, H, x, y, Color.white,
                        Mathf.Clamp01(st * 0.5f - Mathf.Abs(d) + 0.5f));
                }
            }
        }
        return MakeSprite(px, W, H, name, ownedTex, ownedSpr);
    }

    // ---- 線画アイコン -------------------------------------------------------

    /// <summary>太さ <paramref name="wide"/> の線分を白で引く(端は丸)。</summary>
    public static void Line(Color32[] buf, int w, int h, float x0, float y0, float x1, float y1,
        float wide, float alpha = 1f)
    {
        float dx = x1 - x0, dy = y1 - y0;
        float len2 = dx * dx + dy * dy;
        float r = wide * 0.5f;
        int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(x0, x1) - r - 1f));
        int maxX = Mathf.Min(w - 1, Mathf.CeilToInt(Mathf.Max(x0, x1) + r + 1f));
        int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(y0, y1) - r - 1f));
        int maxY = Mathf.Min(h - 1, Mathf.CeilToInt(Mathf.Max(y0, y1) + r + 1f));
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float t = len2 > 0.0001f ? Mathf.Clamp01(((x - x0) * dx + (y - y0) * dy) / len2) : 0f;
                float qx = x0 + dx * t, qy = y0 + dy * t;
                float d = Mathf.Sqrt((x - qx) * (x - qx) + (y - qy) * (y - qy));
                Blend(buf, w, h, x, y, Color.white, Mathf.Clamp01(r - d + 0.5f) * alpha);
            }
        }
    }

    /// <summary>白の円(<paramref name="stroke"/> が 0 なら塗り)。</summary>
    public static void Circle(Color32[] buf, int w, int h, float cx, float cy, float r,
        float stroke, float alpha = 1f)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(cx - r - stroke - 1f));
        int maxX = Mathf.Min(w - 1, Mathf.CeilToInt(cx + r + stroke + 1f));
        int minY = Mathf.Max(0, Mathf.FloorToInt(cy - r - stroke - 1f));
        int maxY = Mathf.Min(h - 1, Mathf.CeilToInt(cy + r + stroke + 1f));
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;
                float cov = stroke > 0f
                    ? Mathf.Clamp01(stroke * 0.5f - Mathf.Abs(d) + 0.5f)
                    : Mathf.Clamp01(0.5f - d);
                Blend(buf, w, h, x, y, Color.white, cov * alpha);
            }
        }
    }

    /// <summary>
    /// 情報行の時計アイコン(v11 の S_Info_Icon_LENGTH)。表示 <paramref name="size"/> px の
    /// 正方形で焼く(中身は SVG の ±16 の枠)。色は Image.color で着ける。
    /// </summary>
    public static Sprite ClockIcon(int size, List<Texture2D> ownedTex, List<Sprite> ownedSpr,
        string name = "V11Clock")
    {
        int S2 = size * 3;
        Color32[] px = new Color32[S2 * S2];
        float k = S2 / 34f;                       // SVG 原寸(竜頭込みで ±17)
        float cx = (S2 - 1) * 0.5f, cy = (S2 - 1) * 0.5f;
        // 画面は y 上向き・SVG は y 下向きなので符号を反転して置く。
        System.Func<float, float> PX = v => cx + v * k;
        System.Func<float, float> PY = v => cy - v * k;
        Circle(px, S2, S2, cx, cy, 12.2f * k, 1.6f * k);
        Line(px, S2, S2, PX(10f), PY(0f), PX(11.8f), PY(0f), 1.3f * k);
        Line(px, S2, S2, PX(-10f), PY(0f), PX(-11.8f), PY(0f), 1.3f * k);
        Line(px, S2, S2, PX(0f), PY(10f), PX(0f), PY(11.8f), 1.3f * k);
        Line(px, S2, S2, PX(0f), PY(-10f), PX(0f), PY(-11.8f), 1.3f * k);
        Line(px, S2, S2, PX(0f), PY(-7.8f), PX(0f), PY(0f), 1.55f * k);
        Line(px, S2, S2, PX(0f), PY(0f), PX(5f), PY(3.3f), 1.55f * k);
        Circle(px, S2, S2, cx, cy, 1.15f * k, 0f);
        // 上の竜頭。
        Line(px, S2, S2, PX(-2f), PY(-13f), PX(-2f), PY(-15f), 1.55f * k);
        Line(px, S2, S2, PX(-2f), PY(-15f), PX(2f), PY(-15f), 1.55f * k);
        Line(px, S2, S2, PX(2f), PY(-15f), PX(2f), PY(-13f), 1.55f * k);
        Line(px, S2, S2, PX(7f), PY(-10f), PX(9f), PY(-12f), 1.55f * k);
        return MakeSprite(px, S2, S2, name, ownedTex, ownedSpr);
    }

    /// <summary>情報行の旗アイコン(v11 の S_Info_Icon_STATUS)。</summary>
    public static Sprite FlagIcon(int size, List<Texture2D> ownedTex, List<Sprite> ownedSpr,
        string name = "V11Flag")
    {
        int S2 = size * 3;
        Color32[] px = new Color32[S2 * S2];
        float k = S2 / 32f;
        float cx = (S2 - 1) * 0.5f, cy = (S2 - 1) * 0.5f;
        System.Func<float, float> PX = v => cx + v * k;
        System.Func<float, float> PY = v => cy - v * k;
        Line(px, S2, S2, PX(-9f), PY(13f), PX(-9f), PY(-13f), 2f * k);
        Circle(px, S2, S2, PX(-9f), PY(-13f), 1.45f * k, 0f);
        Line(px, S2, S2, PX(-11f), PY(13f), PX(-7f), PY(13f), 1.1f * k);
        // 旗(SVG の C 曲線を、上辺 y=-14・下辺 y=+2 の吹き流しで近似)。
        for (int y = 0; y < S2; y++)
        {
            float sy = (cy - y) / k;             // SVG の y
            if (sy < -14f || sy > 2f) continue;
            float t = Mathf.InverseLerp(-14f, 2f, sy);
            float left = -7f;
            float right = Mathf.Lerp(13f, 8.5f, t) - 5f * Mathf.Sin(t * Mathf.PI);
            for (int x = 0; x < S2; x++)
            {
                float sx = (x - cx) / k;
                if (sx < left - 0.5f || sx > right + 0.5f) continue;
                float cov = Mathf.Min(Mathf.Clamp01(sx - left + 0.5f), Mathf.Clamp01(right - sx + 0.5f));
                cov *= Mathf.Min(Mathf.Clamp01((sy + 14f) * 2f), Mathf.Clamp01((2f - sy) * 2f));
                Blend(px, S2, S2, x, y, Color.white, cov);
            }
        }
        return MakeSprite(px, S2, S2, name, ownedTex, ownedSpr);
    }

    // ---- 取り込み済みスプライト ---------------------------------------------

    private static readonly Dictionary<string, Sprite> loadedIcons = new Dictionary<string, Sprite>();

    /// <summary>
    /// <c>Assets/Resources/UI/v11/</c> に取り込んだ操作アイコンの透過 PNG。
    /// (v11 素材 <c>components/</c> の 192x192。文字は含まない。)
    /// </summary>
    public static Sprite Icon(string resourceName)
    {
        Sprite sp;
        if (loadedIcons.TryGetValue(resourceName, out sp) && sp != null) return sp;
        sp = Resources.Load<Sprite>("UI/v11/" + resourceName);
        if (sp == null)
        {
            Texture2D tex = Resources.Load<Texture2D>("UI/v11/" + resourceName);
            if (tex != null)
                sp = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }
        loadedIcons[resourceName] = sp;
        return sp;
    }

    /// <summary>中空(枠だけ)または塗りの菱形。表示寸法の 3 倍で焼く。</summary>
    public static Sprite Diamond(bool filled, List<Texture2D> ownedTex, List<Sprite> ownedSpr,
        string name, float strokeRatio = 0.10f)
    {
        const int S2 = 96;
        Color32[] px = new Color32[S2 * S2];
        float c = (S2 - 1) * 0.5f;
        float half = c * 0.94f;
        float stroke = S2 * strokeRatio;
        for (int y = 0; y < S2; y++)
        {
            for (int x = 0; x < S2; x++)
            {
                float m = Mathf.Abs(x - c) + Mathf.Abs(y - c);
                float d = (m - half) * 0.7071f;          // 菱形の符号付き距離
                float cov = filled
                    ? Mathf.Clamp01(0.5f - d)
                    : Mathf.Clamp01(stroke * 0.5f * 0.7071f - Mathf.Abs(d) + 0.5f);
                Blend(px, S2, S2, x, y, Color.white, cov);
            }
        }
        return MakeSprite(px, S2, S2, name, ownedTex, ownedSpr);
    }

    /// <summary>琥珀のにじむ光(AmberHalo)。楕円に伸ばして使う。</summary>
    public static Sprite Halo(List<Texture2D> ownedTex, List<Sprite> ownedSpr,
        string name = "V11Halo", float peak = 0.2448f)
    {
        const int S2 = 128;
        Color32[] px = new Color32[S2 * S2];
        float c = (S2 - 1) * 0.5f;
        Color col = Accent;
        for (int y = 0; y < S2; y++)
        {
            for (int x = 0; x < S2; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                if (r >= 1f) continue;
                // v11 の停止点(0/24/54/77/100% → 1/0.8/0.34/0.085/0)。
                float a;
                if (r < 0.24f) a = Mathf.Lerp(1f, 0.8f, r / 0.24f);
                else if (r < 0.54f) a = Mathf.Lerp(0.8f, 0.34f, (r - 0.24f) / 0.30f);
                else if (r < 0.77f) a = Mathf.Lerp(0.34f, 0.085f, (r - 0.54f) / 0.23f);
                else a = Mathf.Lerp(0.085f, 0f, (r - 0.77f) / 0.23f);
                Blend(px, S2, S2, x, y, col, a * peak);
            }
        }
        return MakeSprite(px, S2, S2, name, ownedTex, ownedSpr);
    }

    /// <summary>
    /// 画面右側の暗幕(StageBackdrop)。左端が透明・右へ向かって濃紺になる横グラデ。
    /// 9-slice せず横 1 枚で使う。
    /// </summary>
    public static Sprite HorizontalScrim(List<Texture2D> ownedTex, List<Sprite> ownedSpr,
        string name = "V11Scrim")
    {
        const int W = 256, H = 4;
        Color32[] px = new Color32[W * H];
        Color col = C(0x11, 0x11, 0x29);
        for (int x = 0; x < W; x++)
        {
            float t = x / (float)(W - 1);
            float a;
            if (t < 0.15f) a = Mathf.Lerp(0f, 0.23f, t / 0.15f);
            else if (t < 0.33f) a = Mathf.Lerp(0.23f, 0.66f, (t - 0.15f) / 0.18f);
            else if (t < 0.53f) a = Mathf.Lerp(0.66f, 0.88f, (t - 0.33f) / 0.20f);
            else a = Mathf.Lerp(0.88f, 0.96f, (t - 0.53f) / 0.47f);
            for (int y = 0; y < H; y++) Blend(px, W, H, x, y, col, a);
        }
        return MakeSprite(px, W, H, name, ownedTex, ownedSpr);
    }

    /// <summary>難易度の左端に出す細い縦帯(1 色のベタ)。</summary>
    public static Sprite SolidBar(List<Texture2D> ownedTex, List<Sprite> ownedSpr,
        string name = "V11Solid")
    {
        Color32[] px = new Color32[16];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
        return MakeSprite(px, 4, 4, name, ownedTex, ownedSpr);
    }

    // =======================================================================
    //  テキスト
    // =======================================================================

    /// <summary>v11 のテキスト 1 つ。<paramref name="svgSize"/> は SVG(1672 幅)基準の px。</summary>
    public static TMP_Text Text(string name, Transform parent, string content, float svgSize,
        Color color, TextAlignmentOptions align, bool bold = false, float trackingPx = 0f)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TMP_Text t = go.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset f = bold ? SerifBold : Serif;
        if (f != null) t.font = f;
        t.text = content;
        t.fontSize = svgSize * S;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Overflow;
        // TMP の characterSpacing は em 単位(1/100 em)。SVG の tracking(px)を換算する。
        if (trackingPx != 0f) t.characterSpacing = trackingPx / svgSize * 100f;
        return t;
    }

    /// <summary>SVG 座標(中心 x, ベースライン y)でテキスト枠を置く。</summary>
    public static void PlaceCentered(TMP_Text t, float svgCx, float svgBaselineY,
        float svgW, float svgSize)
    {
        RectTransform rt = (RectTransform)t.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(L(svgW), L(svgSize * 1.6f));
        // ベースライン → 字の視覚中心はおよそ 0.34em 上。
        rt.anchoredPosition = new Vector2(X(svgCx), Y(svgBaselineY - svgSize * 0.34f));
    }

    /// <summary>SVG 座標(左端 x, ベースライン y)でテキスト枠を置く(左揃え)。</summary>
    public static void PlaceLeft(TMP_Text t, float svgLeftX, float svgBaselineY,
        float svgW, float svgSize)
    {
        RectTransform rt = (RectTransform)t.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = new Vector2(L(svgW), L(svgSize * 1.6f));
        rt.anchoredPosition = new Vector2(X(svgLeftX), Y(svgBaselineY - svgSize * 0.34f));
    }

    /// <summary>SVG 座標(右端 x, ベースライン y)でテキスト枠を置く(右揃え)。</summary>
    public static void PlaceRight(TMP_Text t, float svgRightX, float svgBaselineY,
        float svgW, float svgSize)
    {
        RectTransform rt = (RectTransform)t.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.sizeDelta = new Vector2(L(svgW), L(svgSize * 1.6f));
        rt.anchoredPosition = new Vector2(X(svgRightX), Y(svgBaselineY - svgSize * 0.34f));
    }

    // ---- ふりがな -----------------------------------------------------------

    /// <summary>ふりがな付きの見出し 1 本分。<see cref="Apply"/> で本文と読みを差し替える。</summary>
    public class RubyText
    {
        public TMP_Text Body;
        private readonly List<TMP_Text> rubies = new List<TMP_Text>();
        private readonly Transform parent;
        private readonly Color rubyColor;
        private float bodySvgSize;
        private bool placed;

        public RubyText(TMP_Text body, Transform rubyParent, float svgSize, Color color)
        {
            Body = body;
            parent = rubyParent;
            bodySvgSize = svgSize;
            rubyColor = color;
        }

        public void SetSvgSize(float svgSize) { bodySvgSize = svgSize; }

        /// <summary>
        /// 「プレイ[時間|じかん]」のように、<c>[漢字|よみ]</c> で読みを付けた書式を渡す。
        /// 本文には角括弧を外した文字列が入り、読みは漢字の真上へ置かれる。
        /// </summary>
        public void Apply(string markup)
        {
            string plain;
            List<(int start, int len, string reading)> spans = Parse(markup, out plain);
            Body.text = plain;

            // ふりがなの大きさと高さは v11 の text_manifest.json の実値に合わせた近似式。
            float rubySize = Mathf.Min(bodySvgSize * 0.56f, 7.5f + 0.185f * bodySvgSize);
            for (int i = 0; i < spans.Count; i++)
            {
                TMP_Text r;
                if (i < rubies.Count) { r = rubies[i]; r.gameObject.SetActive(true); }
                else
                {
                    r = Text("Ruby" + i, parent, "", rubySize, rubyColor,
                        TextAlignmentOptions.Center);
                    RectTransform rt = (RectTransform)r.transform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rubies.Add(r);
                }
                r.fontSize = rubySize * S;
                r.text = spans[i].reading;
                RectTransform rr = (RectTransform)r.transform;
                rr.sizeDelta = new Vector2(L(rubySize * 8f), L(rubySize * 1.6f));
            }
            for (int i = spans.Count; i < rubies.Count; i++) rubies[i].gameObject.SetActive(false);

            lastSpans = spans;
            placed = false;
            Layout();
        }

        /// <summary>
        /// 本文の<b>いまの位置</b>からふりがなを置き直す。本文を置いたあと(PlaceLeft 等)や
        /// 表示直後に呼ぶ。非アクティブのあいだは TMP の実測が空振りするので false を返す。
        /// </summary>
        public bool EnsurePlaced()
        {
            if (placed) return true;
            placed = Layout();
            return placed;
        }

        private List<(int start, int len, string reading)> lastSpans;

        private bool Layout()
        {
            if (lastSpans == null) return false;
            float rubySize = Mathf.Min(bodySvgSize * 0.56f, 7.5f + 0.185f * bodySvgSize);
            float dy = L(bodySvgSize * 0.66f + 3.5f);
            Vector2 bodyPos = ((RectTransform)Body.transform).anchoredPosition;
            bool all = true;
            for (int i = 0; i < lastSpans.Count && i < rubies.Count; i++)
            {
                RectTransform rr = (RectTransform)rubies[i].transform;
                rr.anchoredPosition = new Vector2(bodyPos.x, bodyPos.y + dy);
                all &= TmpAlign.PlaceRubyOverKanji(Body, rr, lastSpans[i].start, lastSpans[i].len);
            }
            return all;
        }

        public void SetAlpha(float a)
        {
            Body.alpha = a;
            foreach (TMP_Text r in rubies) r.alpha = a;
        }

        public static List<(int start, int len, string reading)> Parse(string markup, out string plain)
        {
            var spans = new List<(int, int, string)>();
            var sb = new System.Text.StringBuilder();
            int i = 0;
            while (i < markup.Length)
            {
                char ch = markup[i];
                if (ch == '[')
                {
                    int bar = markup.IndexOf('|', i);
                    int end = markup.IndexOf(']', i);
                    if (bar > i && end > bar)
                    {
                        string word = markup.Substring(i + 1, bar - i - 1);
                        string reading = markup.Substring(bar + 1, end - bar - 1);
                        spans.Add((sb.Length, word.Length, reading));
                        sb.Append(word);
                        i = end + 1;
                        continue;
                    }
                }
                sb.Append(ch);
                i++;
            }
            plain = sb.ToString();
            return spans;
        }
    }

    /// <summary>ふりがな付きテキストを作る。</summary>
    public static RubyText Ruby(string name, Transform parent, string markup, float svgSize,
        Color color, TextAlignmentOptions align, bool bold = false, float trackingPx = 0f,
        Color? rubyColor = null)
    {
        TMP_Text body = Text(name, parent, "", svgSize, color, align, bold, trackingPx);
        RubyText rt = new RubyText(body, parent, svgSize, rubyColor ?? color);
        rt.Apply(markup);
        return rt;
    }

    // =======================================================================
    //  画素ユーティリティ
    // =======================================================================

    public static void Blend(Color32[] buf, int w, int h, int x, int y, Color c, float cov)
    {
        if (cov <= 0f || x < 0 || y < 0 || x >= w || y >= h) return;
        cov = Mathf.Clamp01(cov) * c.a;
        int i = y * w + x;
        Color32 dst = buf[i];
        float da = dst.a / 255f;
        float outA = cov + da * (1f - cov);
        if (outA <= 0f) { buf[i] = new Color32(0, 0, 0, 0); return; }
        float r = (c.r * cov + dst.r / 255f * da * (1f - cov)) / outA;
        float g = (c.g * cov + dst.g / 255f * da * (1f - cov)) / outA;
        float b = (c.b * cov + dst.b / 255f * da * (1f - cov)) / outA;
        buf[i] = new Color32((byte)(Mathf.Clamp01(r) * 255f), (byte)(Mathf.Clamp01(g) * 255f),
            (byte)(Mathf.Clamp01(b) * 255f), (byte)(Mathf.Clamp01(outA) * 255f));
    }

    public static void Blend(Color32[] buf, int w, int h, int x, int y, Color32 c, float cov)
    {
        Blend(buf, w, h, x, y, (Color)c, cov);
    }

    public static Sprite MakeSprite(Color32[] px, int w, int h, string name,
        List<Texture2D> ownedTex, List<Sprite> ownedSpr)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
        tex.name = name;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.SetPixels32(px);
        tex.Apply(false, false);
        if (ownedTex != null) ownedTex.Add(tex);
        Sprite sp = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f,
            0, SpriteMeshType.FullRect);
        sp.name = name;
        if (ownedSpr != null) ownedSpr.Add(sp);
        return sp;
    }

    /// <summary>Image を 1 つ足す(親の中央基準・レイキャスト無し)。</summary>
    public static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        RectTransform rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        return img;
    }

    /// <summary>SVG 座標系で Image を置く(中心 x,y・幅高さ)。</summary>
    public static void Place(Graphic g, float svgCx, float svgCy, float svgW, float svgH)
    {
        RectTransform rt = g.rectTransform;
        rt.anchoredPosition = new Vector2(X(svgCx), Y(svgCy));
        rt.sizeDelta = new Vector2(L(svgW), L(svgH));
    }
}

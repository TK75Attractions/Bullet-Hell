using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// リザルト画面で確立したボタン様式（銀枠+シアンリム+青縦グラデの平行四辺形
// +左右対称の白スラッシュ）を全画面で共有するベーカー。見た目の正は
// Docs/result-design-language.md。焼き込み色は視覚(sRGB)値で与える。
// 斜辺・スラッシュは全て同一角 19°（互いに平行）。
public static class UiButtonStyle
{
    public const float SlashAngleDeg = 19f;
    private static readonly float SlashTan = Mathf.Tan(SlashAngleDeg * Mathf.Deg2Rad);

    private static readonly Color ThinSlashWhite = new Color(1f, 1f, 1f, 0.5f);

    // ボタン本体を一体で焼き込む: 青の縦グラデ本体（下辺ほど明るく、最下辺に
    // シアンの滲み）＋銀枠（上下辺は青く発光）。色はモック実測の視覚(sRGB)値を
    // ブランド青 #4290DB へ 30% 寄せたもの（リザルト v11 で確定した配合）。
    // refW/refH は表示サイズ。枠は左右22/上下11 内側、本体はさらに 8 内側で
    // 斜辺は平行。texOwner/spriteOwner が非 null なら生成物を登録して
    // 呼び出し側のライフサイクルで破棄できるようにする。
    public static Sprite CreateBodySprite(int refW, int refH,
        List<Texture2D> texOwner, List<Sprite> spriteOwner, string spriteName)
    {
        return BakeBody(refW, refH, StandardBluePalette(), texOwner, spriteOwner, spriteName);
    }

    // 難易度色などアクセント色違いの本体。標準パレット(ブランド青)の各色を
    // HSV 差分(色相=差、彩度/明度=比)で accent へ寄せて焼く。銀枠は共通のまま、
    // 本体グラデ・リム・ブルーム・枠の発光だけが accent の色相に乗る。
    public static Sprite CreateBodySpriteTinted(int refW, int refH, Color accent,
        List<Texture2D> texOwner, List<Sprite> spriteOwner, string spriteName)
    {
        Color brand = new Color(0.259f, 0.565f, 0.859f);
        BodyPalette std = StandardBluePalette();
        BodyPalette pal = new BodyPalette
        {
            topCol = Retint(std.topCol, brand, accent),
            botCol = Retint(std.botCol, brand, accent),
            rimCol = Retint(std.rimCol, brand, accent),
            bloomCol = Retint(std.bloomCol, brand, accent),
            topHi = Retint(std.topHi, brand, accent),
            rail = Retint(std.rail, brand, accent),
            glowTop = Retint(std.glowTop, brand, accent),
            glowBot = Retint(std.glowBot, brand, accent),
        };
        return BakeBody(refW, refH, pal, texOwner, spriteOwner, spriteName);
    }

    private struct BodyPalette
    {
        public Color topCol;   // 本体グラデ上端
        public Color botCol;   // 本体グラデ下端
        public Color rimCol;   // 最下辺の滲み
        public Color bloomCol; // 中央下寄りのブルーム
        public Color topHi;    // 上辺の細いハイライト
        public Color rail;     // 本体エッジ内側の細線
        public Color glowTop;  // 銀枠上辺の発光
        public Color glowBot;  // 銀枠下辺の発光
    }

    // リザルト v11 で確定した標準(ブランド青)パレット。CreateBodySprite の
    // 出力を従来とビット同一に保つため、値はここに集約して変更しない。
    private static BodyPalette StandardBluePalette()
    {
        Color brand = new Color(0.259f, 0.565f, 0.859f);   // #4290DB(視覚)
        return new BodyPalette
        {
            topCol = Color.Lerp(new Color(0.000f, 0.149f, 0.353f), brand, 0.30f),
            botCol = Color.Lerp(new Color(0.000f, 0.247f, 0.588f), brand, 0.30f),
            rimCol = new Color(0.000f, 0.310f, 0.714f),
            bloomCol = new Color(0.157f, 0.471f, 0.863f),
            topHi = new Color(0.30f, 0.68f, 0.88f),
            rail = new Color(0.22f, 0.76f, 0.88f),
            glowTop = new Color(0.000f, 0.220f, 0.565f),
            glowBot = new Color(0.000f, 0.298f, 0.714f),
        };
    }

    // 基準色 from→to の HSV 差分(色相は差、彩度/明度は比)を c に適用する。
    private static Color Retint(Color c, Color from, Color to)
    {
        Color.RGBToHSV(c, out float ch, out float cs, out float cv);
        Color.RGBToHSV(from, out float fh, out float fs, out float fv);
        Color.RGBToHSV(to, out float th, out float ts, out float tv);
        float h = Mathf.Repeat(ch + (th - fh), 1f);
        float s = Mathf.Clamp01(cs * (fs <= 0.001f ? 1f : ts / fs));
        float v = Mathf.Clamp01(cv * (fv <= 0.001f ? 1f : tv / fv));
        Color result = Color.HSVToRGB(h, s, v);
        result.a = c.a;
        return result;
    }

    private static Sprite BakeBody(int refW, int refH, BodyPalette pal,
        List<Texture2D> texOwner, List<Sprite> spriteOwner, string spriteName)
    {
        const int S = 2;   // 2x スーパーサンプル
        int TW = refW * S, TH = refH * S;
        Texture2D texture = new Texture2D(TW, TH, TextureFormat.RGBA32, false);
        texture.name = spriteName + "Texture";
        texture.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[TW * TH];
        float cx = (TW - 1) * 0.5f, cy = (TH - 1) * 0.5f;

        float frameHW = (refW * 0.5f - 22f) * S;
        float frameHH = (refH * 0.5f - 11f) * S;
        float bodyHW = frameHW - 8f * S;
        float bodyHH = frameHH - 8f * S;
        Vector2[] frame = ParallelogramVerts(frameHW, frameHH, 2f * frameHH * SlashTan);
        Vector2[] body = ParallelogramVerts(bodyHW, bodyHH, 2f * bodyHH * SlashTan);

        float bloomRx = refW * (250f / 660f) * S;
        float bloomRy = refH * (70f / 120f) * S;
        for (int y = 0; y < TH; y++)
        {
            for (int x = 0; x < TW; x++)
            {
                Vector2 p = new Vector2(x - cx, y - cy);
                float sdfB = ConvexSdf(body, p);
                float inside = Mathf.Clamp01(0.5f - sdfB);
                if (inside > 0f)
                {
                    float ty = Mathf.Clamp01(0.5f - p.y / (bodyHH * 2f)); // 0 上 .. 1 下
                    Color grad = Color.Lerp(pal.topCol, pal.botCol, ty * ty);
                    Blend(px, TW, TH, x, y, grad, inside);
                    // 最下辺のシアン寄りの滲み（高さ 8ref 分）と上辺の細いハイライト。
                    float rim = Mathf.Clamp01((p.y + bodyHH) / (8f * S));
                    if (p.y < -bodyHH + 8f * S)
                        Blend(px, TW, TH, x, y, pal.rimCol, inside * (1f - rim) * 0.85f);
                    if (p.y > bodyHH - 3f * S)
                        Blend(px, TW, TH, x, y, pal.topHi, inside * 0.45f);
                    // 中央下寄りの淡いブルーム（ガラス感）。
                    float bd = Mathf.Sqrt((p.x / bloomRx) * (p.x / bloomRx)
                        + ((p.y + bodyHH * 0.4f) / bloomRy) * ((p.y + bodyHH * 0.4f) / bloomRy));
                    Blend(px, TW, TH, x, y, pal.bloomCol, inside * Mathf.Clamp01(1f - bd) * 0.18f);
                    // 本体エッジ内側のシアン細線（三段構造の中間レール）。
                    float railLine = Mathf.Clamp01(0.8f * S - Mathf.Abs(sdfB + 4f * S) + 0.5f);
                    Blend(px, TW, TH, x, y, pal.rail, railLine * 0.4f);
                }
                // 枠線（銀）。上辺・下辺は発光色を重ねる。
                float sdfF = ConvexSdf(frame, p);
                float line = Mathf.Clamp01(1.2f * S - Mathf.Abs(sdfF) + 0.5f);
                if (line > 0f)
                {
                    Blend(px, TW, TH, x, y, new Color(0.412f, 0.400f, 0.447f), line * 0.9f);
                    if (p.y > frameHH - 14f * S)
                        Blend(px, TW, TH, x, y, pal.glowTop, line * 0.9f);
                    else if (p.y < -frameHH + 14f * S)
                        Blend(px, TW, TH, x, y, pal.glowBot, line * 0.9f);
                }
            }
        }

        texture.SetPixels32(px);
        texture.Apply();
        texOwner?.Add(texture);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, TW, TH),
            new Vector2(0.5f, 0.5f), 100f * S);
        sprite.name = spriteName;
        spriteOwner?.Add(sprite);
        return sprite;
    }

    // スラッシュの標準高。CreateBodySprite の枠線は rect の上下 11px 内側に
    // 焼かれるため、buttonH - 22 で枠線中心とスラッシュの上下端が一致する
    // (2026-07-11 ユーザー指摘「線の上下端をボタンの上下辺と一致させる」)。
    public static float SlashHeight(float buttonH) => buttonH - 22f;

    // スラッシュの標準中心 x（ボタン中心基準）。焼き込み枠線(W/2-22)を挟んで
    // 外9px=太、内9px=細で、枠・スラッシュ3本が平行に並ぶ密着レイアウト。
    // ボタンから離すとリザルトの距離感と揃わない
    // (2026-07-11 ユーザー指摘「白い線がボタンから離れすぎ」)。
    public static float ThickSlashX(float buttonW) => buttonW * 0.5f - 13f;
    public static float ThinSlashX(float buttonW) => buttonW * 0.5f - 31f;

    // ボタン内ラベルの標準サイズ(2026-07-11 指摘「余白をもっと広く」の一括調整点)。
    // リザルト 660x120=38 はユーザー承認済みの錨。タイトル 583x109=40 と
    // 確認ダイアログ 260x86=25 は oracle レビュー(ui-unify-followup-review)の
    // 推奨帯(38〜42 / 24〜26)で手調整した値。ボタンの役割(主導線かどうか)で
    // 最適比が変わるため単一比率式にはしない。新規ボタンはここへ追記して使う。
    public const float LabelSizeResult = 38f;
    public const float LabelSizeTitleMenu = 40f;
    public const float LabelSizeConfirm = 25f;

    // ボタン両脇の白スラッシュ4本(外=太11px 白、内=細2.5px 白α0.5、19°、
    // 左右対称)。高さは SlashHeight でボタンの上下辺に揃える。
    // buttonW/buttonH は表示サイズ。
    public static void AddSlashPair(RectTransform parent, float buttonW, float buttonH)
    {
        float h = SlashHeight(buttonH);
        float thickX = ThickSlashX(buttonW);
        float thinX = ThinSlashX(buttonW);
        AddSlash(parent, "ButtonSlashA", Color.white, -thickX, 11f, h);
        AddSlash(parent, "ButtonSlashB", ThinSlashWhite, -thinX, 2.5f, h);
        AddSlash(parent, "ButtonSlashC", ThinSlashWhite, thinX, 2.5f, h);
        AddSlash(parent, "ButtonSlashD", Color.white, thickX, 11f, h);
    }

    // 上下辺が水平な真の平行四辺形スラッシュ1本。rect 幅は「線幅 + skew」で、
    // 視覚上の線幅が lineW になる（ParallelogramGraphic は skew ≤ width まで有効）。
    public static ParallelogramGraphic AddSlash(RectTransform parent, string name,
        Color color, float centerX, float lineW, float height)
    {
        float skew = height * SlashTan;
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(ParallelogramGraphic));
        go.layer = parent.gameObject.layer;
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(centerX, 0f);
        rect.sizeDelta = new Vector2(lineW + skew, height);
        ParallelogramGraphic slash = go.GetComponent<ParallelogramGraphic>();
        slash.Slant = skew;
        slash.SlantRightEdge = true;
        slash.color = color;
        slash.raycastTarget = false;
        return slash;
    }

    // リザルトのヘッダー帯（ブランド青の横グラデ主帯→白スラッシュ仕切り→濃紺
    // 副帯+上下金属エッジ）を任意サイズで1枚に焼き込む。幾何は帯高 106 基準の
    // 比率（斜辺 34/106）で、主帯右端/副帯左端は slashCenterX からの距離を H 比で
    // 縮尺する（リザルト実測: 主帯 -41.5・副帯 +11.5 @H=106）。白スラッシュ本体は
    // 別描画（AddHeaderSlash）。色は視覚 sRGB 値（OptionMenu RestyleHeaderBand と
    // 同値）。slashCenterX はテクスチャ左端基準。
    public static Sprite CreateHeaderBandSprite(int W, int H, float slashCenterX,
        List<Texture2D> texOwner, List<Sprite> spriteOwner, string spriteName)
    {
        Texture2D texture = new Texture2D(W, H, TextureFormat.RGBA32, false);
        texture.name = spriteName + "Texture";
        texture.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[W * H];
        float k = H / 106f;
        float skew = 34f * k;
        float mainTopRight = slashCenterX - 41.5f * k;
        float subBottomLeft = slashCenterX + 11.5f * k;
        Color mainL = new Color(0.004f, 0.255f, 0.565f);
        Color mainR = new Color(0.008f, 0.424f, 0.859f);
        Color subL = new Color(0.004f, 0.208f, 0.431f);
        Color subR = new Color(0.005f, 0.095f, 0.208f);
        Color edgeHi = new Color(0.55f, 0.60f, 0.70f);
        Color edgeLo = new Color(0.004f, 0.03f, 0.09f);
        float edgeH = Mathf.Max(2f, 3f * k);
        for (int y = 0; y < H; y++)
        {
            float t = y / (float)(H - 1);
            float edgeMain = (mainTopRight - skew) + skew * t;
            float edgeSub = subBottomLeft + skew * t;
            for (int x = 0; x < W; x++)
            {
                float a;
                Color c;
                if (x < edgeMain + 1f)
                {
                    a = Mathf.Clamp01(edgeMain - x);
                    c = Color.Lerp(mainL, mainR, Mathf.Clamp01(x / mainTopRight));
                }
                else if (x > edgeSub - 1f)
                {
                    a = Mathf.Clamp01(x - edgeSub);
                    c = Color.Lerp(subL, subR, Mathf.Clamp01((x - subBottomLeft) / (W - subBottomLeft)));
                }
                else continue;
                if (a <= 0f) continue;
                if (y >= H - edgeH) c = Color.Lerp(c, edgeHi, 0.85f);
                else if (y < edgeH) c = Color.Lerp(c, edgeLo, 0.85f);
                px[y * W + x] = new Color(c.r, c.g, c.b, a);
            }
        }
        texture.SetPixels32(px);
        texture.Apply();
        texOwner?.Add(texture);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, W, H), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = spriteName;
        spriteOwner?.Add(sprite);
        return sprite;
    }

    // ヘッダー帯の白スラッシュ仕切り（帯全高・角度 atan(34/106)=約17.8°。
    // ボタン脇の 19° スラッシュとは別規格で、リザルト/設定のヘッダー帯と同一）。
    // slashCenterX は帯左端基準（CreateHeaderBandSprite と同じ座標系）。
    public static ParallelogramGraphic AddHeaderSlash(RectTransform band, float bandW, float bandH, float slashCenterX)
    {
        float k = bandH / 106f;
        float skew = 34f * k;
        float lineW = 36f * k;
        GameObject go = new GameObject("HeaderSlash", typeof(RectTransform), typeof(CanvasRenderer), typeof(ParallelogramGraphic));
        go.layer = band.gameObject.layer;
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(band, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(slashCenterX - bandW * 0.5f, 0f);
        rect.sizeDelta = new Vector2(lineW + skew, bandH);
        ParallelogramGraphic slash = go.GetComponent<ParallelogramGraphic>();
        slash.Slant = skew;
        slash.SlantRightEdge = true;
        slash.color = Color.white;
        slash.raycastTarget = false;
        return slash;
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

    // 中央原点の平行四辺形（上辺を skew だけ右へずらす。ParallelogramGraphic の
    // slantRightEdge=true と同じ向き）。CW。
    private static Vector2[] ParallelogramVerts(float hw, float hh, float skew)
    {
        return new[]
        {
            new Vector2(-hw + skew, hh),
            new Vector2( hw, hh),
            new Vector2( hw - skew, -hh),
            new Vector2(-hw, -hh),
        };
    }

    // 半透明どうしの over 合成（straight alpha）。
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
}

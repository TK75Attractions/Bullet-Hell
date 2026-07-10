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

        Color brand = new Color(0.259f, 0.565f, 0.859f);   // #4290DB(視覚)
        Color topCol = Color.Lerp(new Color(0.000f, 0.149f, 0.353f), brand, 0.30f);
        Color botCol = Color.Lerp(new Color(0.000f, 0.247f, 0.588f), brand, 0.30f);
        Color rimCol = new Color(0.000f, 0.310f, 0.714f);
        Color bloomCol = new Color(0.157f, 0.471f, 0.863f);
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
                    Color grad = Color.Lerp(topCol, botCol, ty * ty);
                    Blend(px, TW, TH, x, y, grad, inside);
                    // 最下辺のシアン寄りの滲み（高さ 8ref 分）と上辺の細いハイライト。
                    float rim = Mathf.Clamp01((p.y + bodyHH) / (8f * S));
                    if (p.y < -bodyHH + 8f * S)
                        Blend(px, TW, TH, x, y, rimCol, inside * (1f - rim) * 0.85f);
                    if (p.y > bodyHH - 3f * S)
                        Blend(px, TW, TH, x, y, new Color(0.30f, 0.68f, 0.88f), inside * 0.45f);
                    // 中央下寄りの淡いブルーム（ガラス感）。
                    float bd = Mathf.Sqrt((p.x / bloomRx) * (p.x / bloomRx)
                        + ((p.y + bodyHH * 0.4f) / bloomRy) * ((p.y + bodyHH * 0.4f) / bloomRy));
                    Blend(px, TW, TH, x, y, bloomCol, inside * Mathf.Clamp01(1f - bd) * 0.18f);
                    // 本体エッジ内側のシアン細線（三段構造の中間レール）。
                    float railLine = Mathf.Clamp01(0.8f * S - Mathf.Abs(sdfB + 4f * S) + 0.5f);
                    Blend(px, TW, TH, x, y, new Color(0.22f, 0.76f, 0.88f), railLine * 0.4f);
                }
                // 枠線（銀）。上辺・下辺は青の発光色を重ねる。
                float sdfF = ConvexSdf(frame, p);
                float line = Mathf.Clamp01(1.2f * S - Mathf.Abs(sdfF) + 0.5f);
                if (line > 0f)
                {
                    Blend(px, TW, TH, x, y, new Color(0.412f, 0.400f, 0.447f), line * 0.9f);
                    if (p.y > frameHH - 14f * S)
                        Blend(px, TW, TH, x, y, new Color(0.000f, 0.220f, 0.565f), line * 0.9f);
                    else if (p.y < -frameHH + 14f * S)
                        Blend(px, TW, TH, x, y, new Color(0.000f, 0.298f, 0.714f), line * 0.9f);
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

    // ボタン両脇の白スラッシュ4本（外=太11px 白、内=細2.5px 白α0.5、19°、
    // 高さはボタン高の105%、左右対称）。buttonW/buttonH は表示サイズ。
    public static void AddSlashPair(RectTransform parent, float buttonW, float buttonH)
    {
        float h = buttonH * 1.05f;
        float thickX = buttonW * 0.5f - 13f;
        float thinX = buttonW * 0.5f - 31f;
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

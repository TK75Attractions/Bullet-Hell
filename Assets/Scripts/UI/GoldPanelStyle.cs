using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// リザルト新様式「夜の紺と金」(<c>Docs/result-design-language.md</c> §10)の色とスプライトを、
/// リザルト以外の画面でも使えるようにまとめた焼き込みユーティリティ。
///
/// 2026-09-16 の U3 便(ステージ選択の右パネル)で追加。<see cref="ResultScreen"/> 側の実装は
/// 一切触っていない(動いている画面を作り直さないため)。値の正は同じ §10 で、こちらは
/// 「紺の半透明板・金の外枠と内側の罫線・四隅の金ブラケット・罫線・菱形」だけを持つ。
///
/// 焼き込みテクスチャは視覚(sRGB)値をそのまま書き、頂点色(Image.color / TMP.color)は
/// <see cref="Vis"/> で pre-linear へ落とす(memory: texture_vs_vertex_colorspace)。
/// </summary>
public static class GoldPanelStyle
{
    // ---- 焼き込み用(視覚 sRGB) ----------------------------------------------
    public static readonly Color32 TexPanelTop = new Color32(0x12, 0x1B, 0x35, 0xE0);
    public static readonly Color32 TexPanelBottom = new Color32(0x06, 0x0B, 0x1A, 0xEE);
    public static readonly Color32 TexGoldLine = new Color32(0xA8, 0x83, 0x45, 0xFF);
    public static readonly Color32 TexGoldBright = new Color32(0xE9, 0xB9, 0x6E, 0xFF);
    public static readonly Color32 TexGoldDim = new Color32(0x6E, 0x57, 0x2C, 0xFF);

    /// <summary>視覚 sRGB(0..255) → 頂点色用の pre-linear。</summary>
    public static Color Vis(int r, int g, int b, float a = 1f)
    {
        Color c = new Color(r / 255f, g / 255f, b / 255f, 1f).linear;
        c.a = a;
        return c;
    }

    // ---- 頂点色(pre-linear) --------------------------------------------------
    public static readonly Color GoldAccent = Vis(0xE8, 0xB0, 0x64);   // 菱形・値のアクセント
    public static readonly Color GoldBright = Vis(0xF7, 0xD2, 0x95);   // ブラケット・ハイライト
    public static readonly Color GoldDim = Vis(0xB0, 0x8B, 0x4A);      // 罫線
    public static readonly Color SilverLabel = Vis(0xB4, 0xBC, 0xC9);  // 小見出し・ラベル
    public static readonly Color InkWhite = Vis(0xF2, 0xF4, 0xF8);     // 舞台名の白

    // =======================================================================
    //  スプライト
    // =======================================================================

    /// <summary>
    /// パネル本体。縦グラデの紺 + 周辺減光 + 三重の金の枠(外周の太線 / 面取りの内線 /
    /// さらに内側の細線) + 四隅の明るいブラケットと小菱形 + 上下中央の菱形。
    ///
    /// <paramref name="ss"/> は焼き込みの倍率。表示寸法(<paramref name="w"/>x<paramref name="h"/>)より
    /// 大きな解像度で描いて縮小表示するので、細い線と菱形の縁が潰れない
    /// (2026-09-16 指摘「四隅・菱形・罫線が簡素で粗い」への対応。1024px 以上で焼く)。
    /// 線の太さ・余白はすべて ss 倍するので、見た目の寸法は ss を変えても同じ。
    /// </summary>
    public static Sprite CreatePanelSprite(int w, int h, List<Texture2D> ownedTextures,
        List<Sprite> ownedSprites, string name = "GoldPanel", float bracketLength = 70f, int ss = 0)
    {
        if (ss <= 0) ss = Mathf.Max(2, Mathf.CeilToInt(1024f / Mathf.Max(w, h)));
        int W = w * ss, H = h * ss;
        Color32[] px = new Color32[W * H];
        float cx = (W - 1) * 0.5f, cy = (H - 1) * 0.5f;
        float hw = W * 0.5f, hh = H * 0.5f;
        float innerInset = 13f * ss;     // 外枠 → 内側の面取り線
        float thirdInset = 20f * ss;     // さらに内側の細線
        float chamfer = 22f * ss;
        float outerThick = 2.2f * ss;
        float bracket = bracketLength * ss;

        for (int y = 0; y < H; y++)
        {
            float ty = y / (float)(H - 1);
            Color fill = (Color)Color32.Lerp(TexPanelBottom, TexPanelTop, ty * ty);
            for (int x = 0; x < W; x++)
            {
                float ax = Mathf.Abs(x - cx), ay = Mathf.Abs(y - cy);
                float dOuter = Mathf.Max(ax - (hw - 1f), ay - (hh - 1f));
                float inside = Mathf.Clamp01(0.5f - dOuter);

                // 周辺減光: 板の四隅ほど沈ませる(中央は素のまま)。
                float vx = ax / hw, vy = ay / hh;
                float vig = Mathf.Clamp01(vx * vx * 0.55f + vy * vy * 0.45f);
                Color fillV = new Color(fill.r * (1f - vig * 0.45f), fill.g * (1f - vig * 0.45f),
                    fill.b * (1f - vig * 0.45f), fill.a);
                Blend(px, W, H, x, y, fillV, inside * fill.a);

                // 上端の淡い光(紙のような立ち上がり)。
                float glow = Mathf.Clamp01(1f - Mathf.Abs(ty - 0.88f) * 5.5f);
                Blend(px, W, H, x, y, new Color(0.10f, 0.16f, 0.32f), inside * glow * 0.22f);

                // 外周の金枠。
                Blend(px, W, H, x, y, TexGoldLine,
                    Mathf.Clamp01(outerThick - Mathf.Abs(dOuter + outerThick * 0.5f)) * inside);

                // 内側の金線(面取り角)。
                float ix = ax - (hw - innerInset);
                float iy = ay - (hh - innerInset);
                float dInner = Mathf.Max(ix, iy);
                dInner = Mathf.Max(dInner, (ix + iy + chamfer) * 0.7071f);
                Blend(px, W, H, x, y, TexGoldDim, Mathf.Clamp01(1.3f * ss - Mathf.Abs(dInner)) * 0.95f);

                // さらに内側の細線(三重目。うっすら)。
                float jx = ax - (hw - thirdInset);
                float jy = ay - (hh - thirdInset);
                float dThird = Mathf.Max(jx, jy);
                dThird = Mathf.Max(dThird, (jx + jy + chamfer) * 0.7071f);
                Blend(px, W, H, x, y, TexGoldDim, Mathf.Clamp01(0.9f * ss - Mathf.Abs(dThird)) * 0.35f);
            }
        }

        // 四隅の明るいブラケット(内側の線に沿って)+ 直交する短い返し + 小菱形。
        for (int sx = -1; sx <= 1; sx += 2)
        {
            for (int sy = -1; sy <= 1; sy += 2)
            {
                float bx = cx + sx * (hw - innerInset);
                float by = cy + sy * (hh - innerInset);
                float t = 2.4f * ss;
                DrawLine(px, W, H, bx - sx * chamfer, by, bx - sx * chamfer - sx * bracket, by, t, TexGoldBright);
                DrawLine(px, W, H, bx, by - sy * chamfer, bx, by - sy * chamfer - sy * bracket, t, TexGoldBright);
                DrawLine(px, W, H, bx - sx * chamfer, by, bx, by - sy * chamfer, t, TexGoldBright);
                // 返し(ブラケットの先から内側へ短く折る)。
                DrawLine(px, W, H, bx - sx * chamfer - sx * bracket, by,
                    bx - sx * chamfer - sx * bracket, by - sy * 9f * ss, 1.8f * ss, TexGoldBright);
                DrawLine(px, W, H, bx, by - sy * chamfer - sy * bracket,
                    bx - sx * 9f * ss, by - sy * chamfer - sy * bracket, 1.8f * ss, TexGoldBright);
                // 面取り辺の中央に小さな菱形。
                DrawDiamond(px, W, H, bx - sx * chamfer * 0.5f, by - sy * chamfer * 0.5f, 4.5f * ss, TexGoldBright);
            }
        }
        // 上下中央の菱形(枠に噛ませる)。二重にして密度を上げる。
        foreach (float sign in new[] { 1f, -1f })
        {
            float dy = cy + sign * (hh - innerInset);
            DrawDiamond(px, W, H, cx, dy, 11f * ss, TexGoldBright);
            DrawDiamond(px, W, H, cx, dy, 5f * ss, TexPanelBottom);
            DrawDiamond(px, W, H, cx - 26f * ss, dy, 4f * ss, TexGoldLine);
            DrawDiamond(px, W, H, cx + 26f * ss, dy, 4f * ss, TexGoldLine);
        }

        return MakeSprite(px, W, H, name, ownedTextures, ownedSprites);
    }

    /// <summary>両端がフェードする飾り罫(太い主線 + その上下の髪の毛線)。幅方向へ伸ばして使う。</summary>
    public static Sprite CreateRuleSprite(List<Texture2D> ownedTextures, List<Sprite> ownedSprites)
    {
        const int W = 1024, H = 16;
        Color32[] px = new Color32[W * H];
        float cy = (H - 1) * 0.5f;
        for (int x = 0; x < W; x++)
        {
            float u = Mathf.Abs(x / (float)(W - 1) * 2f - 1f);        // 0=中央 1=端
            float a = Mathf.Clamp01(1f - u * u * u * u) * 0.95f;      // 端で静かに消える
            for (int y = 0; y < H; y++)
            {
                float dy = Mathf.Abs(y - cy);
                // 主線(中央 2px)。
                float main = Mathf.Clamp01(1.4f - Mathf.Abs(dy));
                // 髪の毛線(±5px。端へ行くほど早く消える)。
                float hair = Mathf.Clamp01(0.9f - Mathf.Abs(dy - 5f)) * Mathf.Clamp01(1f - u * u) * 0.45f;
                Blend(px, W, H, x, y, TexGoldLine, a * Mathf.Max(main, hair));
            }
        }
        return MakeSprite(px, W, H, "GoldRule", ownedTextures, ownedSprites);
    }

    /// <summary>菱形(外に細い輪郭を持つ二重菱形。色は Image.color で決める)。</summary>
    public static Sprite CreateDiamondSprite(List<Texture2D> ownedTextures, List<Sprite> ownedSprites)
    {
        const int S = 256;
        Color32[] px = new Color32[S * S];
        float c = (S - 1) * 0.5f;
        float half = c * 0.62f;
        float ring = c * 0.94f;
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float m = (Mathf.Abs(x - c) + Mathf.Abs(y - c)) * 0.7071f;
                // 本体。
                Blend(px, S, S, x, y, TexGoldBright, Mathf.Clamp01(half * 0.7071f - m + 0.5f));
                // 外側の細い輪郭(1.5px 相当)。
                Blend(px, S, S, x, y, TexGoldBright,
                    Mathf.Clamp01(3f - Mathf.Abs(m - ring * 0.7071f)) * 0.75f);
            }
        }
        return MakeSprite(px, S, S, "GoldDiamond", ownedTextures, ownedSprites);
    }

    /// <summary>サムネの枠(外に細い金の二重線。中は空)。9-slice で任意の大きさに伸ばす。</summary>
    public static Sprite CreateThinFrameSprite(List<Texture2D> ownedTextures, List<Sprite> ownedSprites)
    {
        const int S = 256, B = 40;
        Color32[] px = new Color32[S * S];
        float cx = (S - 1) * 0.5f, cy = (S - 1) * 0.5f;
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float ax = Mathf.Abs(x - cx), ay = Mathf.Abs(y - cy);
                float d = Mathf.Max(ax - (S * 0.5f - 1f), ay - (S * 0.5f - 1f));
                // 外周の金線(2px)+ 5px 内側の細線。中は透明のまま(サムネが透ける)。
                Blend(px, S, S, x, y, TexGoldLine, Mathf.Clamp01(2f - Mathf.Abs(d + 1f)));
                Blend(px, S, S, x, y, TexGoldDim, Mathf.Clamp01(1f - Mathf.Abs(d + 6f)) * 0.7f);
            }
        }
        // 四隅の明るい小さな返し。
        foreach (int sx in new[] { -1, 1 })
        {
            foreach (int sy in new[] { -1, 1 })
            {
                float bx = cx + sx * (S * 0.5f - 2f);
                float by = cy + sy * (S * 0.5f - 2f);
                DrawLine(px, S, S, bx, by, bx - sx * 22f, by, 2.4f, TexGoldBright);
                DrawLine(px, S, S, bx, by, bx, by - sy * 22f, 2.4f, TexGoldBright);
            }
        }
        Texture2D tex = MakeTexture(px, S, S, "GoldThinFrame", ownedTextures);
        Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, S, S), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(B, B, B, B));
        sprite.name = "GoldThinFrame";
        ownedSprites?.Add(sprite);
        return sprite;
    }

    /// <summary>面取り角の距離場(0 = 輪郭上、負 = 内側)。</summary>
    public static float ChamferRect(float ax, float ay, float hw, float hh, float chamfer)
    {
        float d = Mathf.Max(ax - hw, ay - hh);
        return Mathf.Max(d, (ax + ay - (hw + hh - chamfer)) * 0.7071f);
    }

    /// <summary>円弧をポリラインで引く(角度は度・時計回りでも反時計回りでもよい)。</summary>
    public static void DrawArc(Color32[] buf, int w, int h, float cx, float cy, float radius,
        float deg0, float deg1, float thick, Color32 col, int segments = 48)
    {
        float px = 0f, py = 0f;
        for (int i = 0; i <= segments; i++)
        {
            float a = Mathf.Lerp(deg0, deg1, i / (float)segments) * Mathf.Deg2Rad;
            float x = cx + Mathf.Cos(a) * radius;
            float y = cy + Mathf.Sin(a) * radius;
            if (i > 0) DrawLine(buf, w, h, px, py, x, y, thick, col);
            px = x; py = y;
        }
    }

    /// <summary>
    /// 参考画像(Instructions/リザルト/ref/result_mockup_gpt_20260916.png)の額縁を写した板。
    /// 面取り角の <b>二重の金枠</b>(外の太線 + 10px 内側の細線 + さらに内側の髪の毛線)、
    /// 四隅の <b>弧と点の飾り</b>、上下中央の <b>中空の菱形と翼形の曲線</b>、
    /// 深い紺のグラデーション(既定 #0B1022 前後・不透明度 0.86)と中央のわずかな放射状の明るみ。
    ///
    /// <paramref name="ss"/> は焼き込みの倍率(既定は最長辺が 1400px を超えるまで上げる)。
    /// 線の太さ・余白はすべて ss 倍するので、見た目の寸法は ss を変えても同じ。
    /// </summary>
    public static Sprite CreateOrnatePanelSprite(int w, int h, List<Texture2D> ownedTextures,
        List<Sprite> ownedSprites, string name = "OrnatePanel", int ss = 0)
    {
        if (ss <= 0) ss = Mathf.Max(2, Mathf.CeilToInt(1400f / Mathf.Max(w, h)));
        int W = w * ss, H = h * ss;
        Color32[] px = new Color32[W * H];
        float cx = (W - 1) * 0.5f, cy = (H - 1) * 0.5f;
        float hw = W * 0.5f, hh = H * 0.5f;

        float chamfer = 26f * ss;      // 面取りの深さ
        float inset2 = 10f * ss;       // 二重目の枠
        float inset3 = 17f * ss;       // 三重目(髪の毛線)
        float outerThick = 2.2f * ss;

        Color32 plateTop = new Color32(0x14, 0x1A, 0x33, 0xDC);
        Color32 plateBottom = new Color32(0x09, 0x0D, 0x1E, 0xE8);

        for (int y = 0; y < H; y++)
        {
            float ty = y / (float)(H - 1);
            Color fill = (Color)Color32.Lerp(plateBottom, plateTop, Mathf.Sqrt(ty));
            float ay = Mathf.Abs(y - cy);
            for (int x = 0; x < W; x++)
            {
                float ax = Mathf.Abs(x - cx);
                float dOuter = ChamferRect(ax, ay, hw - 1f, hh - 1f, chamfer);
                float inside = Mathf.Clamp01(0.5f - dOuter);
                if (inside <= 0f) continue;

                // 板。中央のわずかな放射状の明るみ(参考画像の「内側の明るみ」)。
                float rx = (x - cx) / hw, ry = (y - cy) / hh;
                float rad = Mathf.Sqrt(rx * rx * 0.85f + ry * ry);
                float lift = Mathf.Clamp01(1f - rad * 1.25f);
                Color fillL = Color.Lerp(fill, new Color(0.115f, 0.135f, 0.235f), lift * lift * 0.5f);
                Blend(px, W, H, x, y, fillL, inside * fill.a);

                // 外周の金枠。
                Blend(px, W, H, x, y, TexGoldLine,
                    Mathf.Clamp01(outerThick - Mathf.Abs(dOuter + outerThick * 0.5f)) * inside);

                // 二重目の枠(こちらが主線)。
                float d2 = ChamferRect(ax, ay, hw - inset2, hh - inset2, chamfer);
                Blend(px, W, H, x, y, TexGoldLine,
                    Mathf.Clamp01(1.5f * ss - Mathf.Abs(d2)) * 0.95f);

                // 三重目の髪の毛線。
                float d3 = ChamferRect(ax, ay, hw - inset3, hh - inset3, chamfer);
                Blend(px, W, H, x, y, TexGoldDim, Mathf.Clamp01(0.9f * ss - Mathf.Abs(d3)) * 0.40f);
            }
        }

        // ---- 四隅の飾り(弧 + 点 + 段差) ----
        for (int sx = -1; sx <= 1; sx += 2)
        {
            for (int sy = -1; sy <= 1; sy += 2)
            {
                // 面取り辺の両端(二重目の枠の上)。
                float ex = cx + sx * (hw - inset2);
                float ey = cy + sy * (hh - inset2 - chamfer);
                float fx = cx + sx * (hw - inset2 - chamfer);
                float fy = cy + sy * (hh - inset2);

                // 面取り辺から内側へ 1 段下げた小さな段差(参考画像の階段状の返し)。
                float mx = (ex + fx) * 0.5f, my = (ey + fy) * 0.5f;
                float stepX = mx - sx * 7f * ss, stepY = my - sy * 7f * ss;
                DrawLine(px, W, H, mx - sx * 9f * ss - (-sx) * 0f, my + sy * 9f * ss,
                    stepX - sx * 9f * ss, stepY + sy * 9f * ss, 1.7f * ss, TexGoldBright);
                DrawLine(px, W, H, ex, ey, ex - sx * 8f * ss, ey + sy * 8f * ss, 1.7f * ss, TexGoldBright);
                DrawLine(px, W, H, ex - sx * 8f * ss, ey + sy * 8f * ss,
                    ex - sx * 8f * ss, ey + sy * 22f * ss, 1.7f * ss, TexGoldBright);
                DrawLine(px, W, H, fx, fy, fx + sx * 8f * ss, fy - sy * 8f * ss, 1.7f * ss, TexGoldBright);
                DrawLine(px, W, H, fx + sx * 8f * ss, fy - sy * 8f * ss,
                    fx + sx * 22f * ss, fy - sy * 8f * ss, 1.7f * ss, TexGoldBright);

                // 内側の小さな弧(渦の返し)と点 2 つ。
                float ax0 = mx - sx * 26f * ss, ay0 = my - sy * 26f * ss;
                float baseDeg = sx > 0 ? (sy > 0 ? 225f : 135f) : (sy > 0 ? 315f : 45f);
                DrawArc(px, W, H, ax0, ay0, 11f * ss, baseDeg - 100f, baseDeg + 60f, 1.5f * ss, TexGoldLine, 28);
                DrawDiamond(px, W, H, mx - sx * 14f * ss, my - sy * 14f * ss, 3.2f * ss, TexGoldBright);
                DrawDiamond(px, W, H, ax0 + sx * 2f * ss, ay0 + sy * 2f * ss, 2.2f * ss, TexGoldBright);
            }
        }

        // ---- 上下中央の中空菱形 + 翼形の曲線 ----
        foreach (float sign in new[] { 1f, -1f })
        {
            float dy = cy + sign * (hh - inset2);
            DrawDiamond(px, W, H, cx, dy, 13f * ss, TexGoldBright);
            DrawDiamond(px, W, H, cx, dy, 7.5f * ss, new Color32(0x0B, 0x10, 0x22, 0xFF));
            for (int s = -1; s <= 1; s += 2)
            {
                // 菱形の脇から外へ、浅く反り返る翼。
                float x0 = cx + s * 16f * ss;
                float prevX = x0, prevY = dy;
                for (int i = 1; i <= 24; i++)
                {
                    float u = i / 24f;
                    float x = x0 + s * u * 42f * ss;
                    float y = dy - sign * Mathf.Sin(u * Mathf.PI) * 7f * ss * (1f - u * 0.45f);
                    DrawLine(px, W, H, prevX, prevY, x, y, 1.4f * ss * (1f - u * 0.5f), TexGoldLine);
                    prevX = x; prevY = y;
                }
                DrawLine(px, W, H, cx + s * 60f * ss, dy, cx + s * 96f * ss, dy, 1.3f * ss, TexGoldLine);
            }
        }

        return MakeSprite(px, W, H, name, ownedTextures, ownedSprites);
    }

    // =======================================================================
    //  UI 生成ヘルパー
    // =======================================================================

    /// <summary>TMP のアンダーレイによる柔らかい発光
    /// (memory: ScaleRatioC=1 + UpdateMeshPadding が必須。怠ると全面ベタ矩形)。</summary>
    public static void ApplyTextGlow(TMP_Text text, Color glow, float dilate, float softness)
    {
        if (text == null) return;
        Material mat = text.fontMaterial;
        mat.EnableKeyword("UNDERLAY_ON");
        mat.SetFloat(ShaderUtilities.ID_ScaleRatio_C, 1f);
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, 0f);
        mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, dilate);
        mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, softness);
        mat.SetColor(ShaderUtilities.ID_UnderlayColor, glow);
        text.UpdateMeshPadding();
    }

    // =======================================================================
    //  描画プリミティブ(ResultScreen と同じ式)
    // =======================================================================

    public static void Blend(Color32[] buf, int w, int h, int x, int y, Color c, float cov)
    {
        if (cov <= 0f || x < 0 || y < 0 || x >= w || y >= h) return;
        cov = Mathf.Clamp01(cov);
        int i = y * w + x;
        Color32 d = buf[i];
        float da = d.a / 255f;
        float outA = cov + da * (1f - cov);
        if (outA <= 0f) { buf[i] = new Color32(0, 0, 0, 0); return; }
        float r = (c.r * cov + d.r / 255f * da * (1f - cov)) / outA;
        float g = (c.g * cov + d.g / 255f * da * (1f - cov)) / outA;
        float b = (c.b * cov + d.b / 255f * da * (1f - cov)) / outA;
        buf[i] = new Color32((byte)(Mathf.Clamp01(r) * 255f), (byte)(Mathf.Clamp01(g) * 255f),
            (byte)(Mathf.Clamp01(b) * 255f), (byte)(Mathf.Clamp01(outA) * 255f));
    }

    public static void Blend(Color32[] buf, int w, int h, int x, int y, Color32 c, float cov)
    {
        Blend(buf, w, h, x, y, new Color(c.r / 255f, c.g / 255f, c.b / 255f, 1f), cov * (c.a / 255f));
    }

    public static void DrawLine(Color32[] buf, int w, int h,
        float x0, float y0, float x1, float y1, float thick, Color32 col)
    {
        float minX = Mathf.Min(x0, x1) - thick, maxX = Mathf.Max(x0, x1) + thick;
        float minY = Mathf.Min(y0, y1) - thick, maxY = Mathf.Max(y0, y1) + thick;
        float dx = x1 - x0, dy = y1 - y0;
        float len2 = Mathf.Max(1e-5f, dx * dx + dy * dy);
        for (int y = Mathf.Max(0, (int)minY); y <= Mathf.Min(h - 1, (int)maxY + 1); y++)
        {
            for (int x = Mathf.Max(0, (int)minX); x <= Mathf.Min(w - 1, (int)maxX + 1); x++)
            {
                float t = Mathf.Clamp01(((x - x0) * dx + (y - y0) * dy) / len2);
                float qx = x0 + dx * t, qy = y0 + dy * t;
                float d = Mathf.Sqrt((x - qx) * (x - qx) + (y - qy) * (y - qy)) - thick * 0.5f;
                Blend(buf, w, h, x, y, col, Mathf.Clamp01(0.5f - d));
            }
        }
    }

    public static void DrawDiamond(Color32[] buf, int w, int h, float cx, float cy, float half, Color32 col)
    {
        int r = Mathf.CeilToInt(half) + 2;
        for (int y = (int)cy - r; y <= (int)cy + r; y++)
        {
            for (int x = (int)cx - r; x <= (int)cx + r; x++)
            {
                float d = (Mathf.Abs(x - cx) + Mathf.Abs(y - cy)) * 0.7071f - half * 0.7071f;
                Blend(buf, w, h, x, y, col, Mathf.Clamp01(0.5f - d));
            }
        }
    }

    public static Texture2D MakeTexture(Color32[] px, int w, int h, string name, List<Texture2D> owned)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.name = name;
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.SetPixels32(px);
        tex.Apply();
        owned?.Add(tex);
        return tex;
    }

    public static Sprite MakeSprite(Color32[] px, int w, int h, string name,
        List<Texture2D> ownedTextures, List<Sprite> ownedSprites)
    {
        Texture2D tex = MakeTexture(px, w, h, name, ownedTextures);
        Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = name;
        ownedSprites?.Add(sprite);
        return sprite;
    }
}

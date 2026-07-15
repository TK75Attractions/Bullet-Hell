using UnityEngine;

// ランタイム生成のコントローラー用アイコン(スティック左右 / ボタン)。
// プロジェクトには実アイコン画像が無いため、他の UI 手続き生成テクスチャと同じ
// 流儀で白の線画スプライトをベイクし、使用側で Image.color により文脈色へ tint する
// (白背景のキーキャップ上では濃紺、暗い帯の上ではシアン/白 等)。
// 生成物はキャッシュして重複ベイクを避ける。
public static class UiIconFactory
{
    private static Sprite stickLeftRight;
    private static Sprite button;

    // 上下対称の丸ベース(真上視点のアナログスティック)+ 中央のノブ +
    // 左右の三角矢印。「スティックを左右に動かす」ことを示すアイコン。
    public static Sprite StickLeftRight()
    {
        if (stickLeftRight != null) return stickLeftRight;
        const int w = 112, h = 64;
        float[,] cov = new float[w, h];
        Vector2 c = new Vector2(56f, 32f);
        AddRing(cov, w, h, c, 15f, 4.5f);   // ベース(リング)
        AddDisc(cov, w, h, c, 7.5f);        // ノブ
        // 左矢印(先端左)
        AddTriangle(cov, w, h, new Vector2(14f, 32f), new Vector2(38f, 21f), new Vector2(38f, 43f));
        // 右矢印(先端右)
        AddTriangle(cov, w, h, new Vector2(98f, 32f), new Vector2(74f, 21f), new Vector2(74f, 43f));
        stickLeftRight = ToSprite(cov, w, h, "UiIcon_StickLR");
        return stickLeftRight;
    }

    // 丸いフェイスボタン(外リング+ノブ)。ダッシュ等のボタン操作を示す。
    public static Sprite Button()
    {
        if (button != null) return button;
        const int w = 64, h = 64;
        float[,] cov = new float[w, h];
        Vector2 c = new Vector2(32f, 32f);
        AddRing(cov, w, h, c, 22f, 5f);
        AddDisc(cov, w, h, c, 12f);
        button = ToSprite(cov, w, h, "UiIcon_Button");
        return button;
    }

    // 以降は 4x スーパーサンプリングのカバレッジ加算(union=max)でエッジを滑らかに。
    private const int SS = 4;

    private static void AddDisc(float[,] cov, int w, int h, Vector2 c, float r)
    {
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int inside = 0;
                for (int sy = 0; sy < SS; sy++)
                    for (int sx = 0; sx < SS; sx++)
                    {
                        float px = x + (sx + 0.5f) / SS;
                        float py = y + (sy + 0.5f) / SS;
                        if ((new Vector2(px, py) - c).sqrMagnitude <= r * r) inside++;
                    }
                float a = inside / (float)(SS * SS);
                if (a > cov[x, y]) cov[x, y] = a;
            }
    }

    private static void AddRing(float[,] cov, int w, int h, Vector2 c, float r, float thick)
    {
        float half = thick * 0.5f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int inside = 0;
                for (int sy = 0; sy < SS; sy++)
                    for (int sx = 0; sx < SS; sx++)
                    {
                        float px = x + (sx + 0.5f) / SS;
                        float py = y + (sy + 0.5f) / SS;
                        float d = (new Vector2(px, py) - c).magnitude;
                        if (Mathf.Abs(d - r) <= half) inside++;
                    }
                float a = inside / (float)(SS * SS);
                if (a > cov[x, y]) cov[x, y] = a;
            }
    }

    private static void AddTriangle(float[,] cov, int w, int h, Vector2 a, Vector2 b, Vector2 cc)
    {
        // バウンディングボックスだけ走査
        int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, cc.x))));
        int maxX = Mathf.Min(w - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, cc.x))));
        int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, cc.y))));
        int maxY = Mathf.Min(h - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, cc.y))));
        for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                int inside = 0;
                for (int sy = 0; sy < SS; sy++)
                    for (int sx = 0; sx < SS; sx++)
                    {
                        float px = x + (sx + 0.5f) / SS;
                        float py = y + (sy + 0.5f) / SS;
                        if (PointInTriangle(new Vector2(px, py), a, b, cc)) inside++;
                    }
                float av = inside / (float)(SS * SS);
                if (av > cov[x, y]) cov[x, y] = av;
            }
    }

    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(p, a, b);
        float d2 = Cross(p, b, c);
        float d3 = Cross(p, c, a);
        bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
        bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(hasNeg && hasPos); // 全て同符号なら内部(巻き向き非依存)
    }

    private static float Cross(Vector2 p, Vector2 a, Vector2 b)
    {
        return (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
    }

    private static Sprite ToSprite(float[,] cov, int w, int h, string name)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = name,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        Color32[] px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(cov[x, y]) * 255f);
                // 白の線画。RGB を白にしておくと Image.color でそのまま任意色に tint できる。
                px[y * w + x] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(px);
        tex.Apply(false, false);
        Sprite sp = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        sp.name = name;
        return sp;
    }
}

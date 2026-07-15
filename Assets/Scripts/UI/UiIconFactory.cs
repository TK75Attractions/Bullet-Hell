using UnityEngine;
using UnityEngine.UI;

// ランタイム生成のアーケード操作アイコン(スティック / 丸ボタン)。
// 添付イメージを基にした白いシルエットをベイクし、使用側の Image.color で
// 白背景では濃紺、暗い帯ではシアンへ tint する。
// 生成物はキャッシュして重複ベイクを避ける。
public static class UiIconFactory
{
    public enum IconKind { Stick, Button }

    private static Sprite stickLeftRight;
    private static Sprite button;
    private static Sprite stickBase;
    private static Sprite stickHandle;
    private static Sprite buttonBase;
    private static Sprite buttonCap;

    // 台座と操作部を別 Image として組み立てる。動く部品だけを
    // ControlIconMotion に渡すことで、シルエットの部品関係を崩さずアニメーションする。
    public static RectTransform CreateIcon(Transform parent, string objectName, IconKind kind,
        Vector2 position, Vector2 size, Color tint)
    {
        GameObject rootObject = new GameObject(objectName, typeof(RectTransform));
        rootObject.layer = parent.gameObject.layer;
        RectTransform root = (RectTransform)rootObject.transform;
        root.SetParent(parent, false);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = position;
        root.sizeDelta = size;

        CreateLayer(root, "Base", kind == IconKind.Button ? ButtonBase() : StickBase(), tint,
            new Vector2(0.5f, 0.5f));
        RectTransform movingPart = CreateLayer(root, kind == IconKind.Button ? "Cap" : "Handle",
            kind == IconKind.Button ? ButtonCap() : StickHandle(), tint, new Vector2(0.5f, 0.30f));

        rootObject.AddComponent<ControlIconMotion>().Configure(movingPart, kind == IconKind.Button);
        return root;
    }

    private static RectTransform CreateLayer(RectTransform parent, string objectName, Sprite sprite,
        Color tint, Vector2 pivot)
    {
        GameObject layerObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        layerObject.layer = parent.gameObject.layer;
        RectTransform rect = (RectTransform)layerObject.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = pivot;

        Image image = layerObject.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.color = tint;
        image.raycastTarget = false;
        return rect;
    }

    private static Sprite StickBase()
    {
        if (stickBase != null) return stickBase;
        const int w = 128, h = 96;
        float[,] cov = new float[w, h];
        AddTriangle(cov, w, h, new Vector2(12f, 10f), new Vector2(116f, 10f), new Vector2(103f, 34f));
        AddTriangle(cov, w, h, new Vector2(12f, 10f), new Vector2(103f, 34f), new Vector2(25f, 34f));
        stickBase = ToSprite(cov, w, h, "UiIcon_StickBase");
        return stickBase;
    }

    private static Sprite StickHandle()
    {
        if (stickHandle != null) return stickHandle;
        const int w = 128, h = 96;
        float[,] cov = new float[w, h];
        AddRoundedRect(cov, w, h, new Rect(58f, 29f, 12f, 42f), 6f);
        AddEllipse(cov, w, h, new Vector2(64f, 76f), 20f, 13f);
        stickHandle = ToSprite(cov, w, h, "UiIcon_StickHandle");
        return stickHandle;
    }

    private static Sprite ButtonBase()
    {
        if (buttonBase != null) return buttonBase;
        const int w = 128, h = 80;
        float[,] cov = new float[w, h];
        AddRoundedRect(cov, w, h, new Rect(15f, 10f, 98f, 25f), 10f);
        buttonBase = ToSprite(cov, w, h, "UiIcon_ButtonBase");
        return buttonBase;
    }

    private static Sprite ButtonCap()
    {
        if (buttonCap != null) return buttonCap;
        const int w = 128, h = 80;
        float[,] cov = new float[w, h];
        AddEllipse(cov, w, h, new Vector2(64f, 55f), 37f, 17f);
        buttonCap = ToSprite(cov, w, h, "UiIcon_ButtonCap");
        return buttonCap;
    }

    // 広い台座・軸・丸ノブで構成する、横から見たスティックのシルエット。
    public static Sprite StickLeftRight()
    {
        if (stickLeftRight != null) return stickLeftRight;
        const int w = 128, h = 96;
        float[,] cov = new float[w, h];

        // 添付イメージに合わせた横から見たアーケードスティックのシルエット。
        // 広い台座、細い軸、丸いノブの3要素だけで小サイズでも判別できる形にする。
        AddTriangle(cov, w, h, new Vector2(12f, 10f), new Vector2(116f, 10f), new Vector2(103f, 34f));
        AddTriangle(cov, w, h, new Vector2(12f, 10f), new Vector2(103f, 34f), new Vector2(25f, 34f));
        AddRoundedRect(cov, w, h, new Rect(58f, 29f, 12f, 42f), 6f);
        AddEllipse(cov, w, h, new Vector2(64f, 76f), 20f, 13f);

        stickLeftRight = ToSprite(cov, w, h, "UiIcon_StickSilhouette");
        return stickLeftRight;
    }

    // 低い台座と大きな丸い天面で、押しボタン操作を示すシルエット。
    public static Sprite Button()
    {
        if (button != null) return button;
        const int w = 128, h = 80;
        float[,] cov = new float[w, h];

        // 低い台座の上に大きな丸ボタンが乗る、横から見たシルエット。
        // 台座とボタンの間に細い空きを残し、単色でも部品が読み分けられる。
        AddRoundedRect(cov, w, h, new Rect(15f, 10f, 98f, 25f), 10f);
        AddEllipse(cov, w, h, new Vector2(64f, 55f), 37f, 17f);

        button = ToSprite(cov, w, h, "UiIcon_ButtonSilhouette");
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


    private static void AddEllipse(float[,] cov, int w, int h, Vector2 center, float radiusX, float radiusY)
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
                        float dx = (px - center.x) / radiusX;
                        float dy = (py - center.y) / radiusY;
                        if (dx * dx + dy * dy <= 1f) inside++;
                    }
                float alpha = inside / (float)(SS * SS);
                if (alpha > cov[x, y]) cov[x, y] = alpha;
            }
    }

    private static void AddRoundedRect(float[,] cov, int w, int h, Rect rect, float radius)
    {
        Vector2 center = rect.center;
        Vector2 half = rect.size * 0.5f;
        radius = Mathf.Min(radius, Mathf.Min(half.x, half.y));
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int inside = 0;
                for (int sy = 0; sy < SS; sy++)
                    for (int sx = 0; sx < SS; sx++)
                    {
                        Vector2 point = new Vector2(x + (sx + 0.5f) / SS, y + (sy + 0.5f) / SS);
                        Vector2 q = new Vector2(Mathf.Abs(point.x - center.x), Mathf.Abs(point.y - center.y))
                            - (half - Vector2.one * radius);
                        float distance = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude
                            + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
                        if (distance <= 0f) inside++;
                    }
                float alpha = inside / (float)(SS * SS);
                if (alpha > cov[x, y]) cov[x, y] = alpha;
            }
    }
}

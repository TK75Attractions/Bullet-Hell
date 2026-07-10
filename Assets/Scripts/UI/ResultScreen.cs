using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ステージ終了後に表示する、JSAB 風のリザルト画面。
// シーンを肥大化させないよう、既存の JsabStageSelect と同じく実行時に組み立てる。
public sealed class ResultScreen : MonoBehaviour
{
    public enum Action
    {
        Retry,
        StageSelect,
    }

    // Linear Color Space 上でモックアップの sRGB 色へ見えるよう調整した値。
    private static readonly Color Cyan = new Color(0.04f, 0.54f, 0.75f, 1f);       // visual #38C2E0
    private static readonly Color BrandBlue = new Color(0.055f, 0.28f, 0.708f, 1f); // visual #4290DB
    private static readonly Color DeepNavy = new Color(0.001f, 0.003f, 0.008f, 1f);
    private static readonly Color PanelNavy = new Color(0.003f, 0.012f, 0.03f, 1f);
    private static readonly Color Rim = new Color(0.20f, 0.45f, 0.65f, 0.52f);
    private static readonly Color DimText = new Color(0.29f, 0.48f, 0.64f, 0.9f);
    private static readonly Color Violet = new Color(0.35f, 0.05f, 1f, 1f);

    private TMP_FontAsset font;
    private CanvasGroup contentGroup;
    private RectTransform contentRect;
    private TMP_Text stageNameText;
    private TMP_Text difficultyText;
    private TMP_Text verdictText;
    private TMP_Text rankText;
    private TMP_Text scoreText;
    private TMP_Text hitText;
    private TMP_Text counterText;
    private TMP_Text timeText;
    private readonly Image[] actionBodies = new Image[2];
    private readonly Image[] actionRims = new Image[2];
    private readonly TMP_Text[] actionLabels = new TMP_Text[2];
    private readonly RectTransform[] actionRects = new RectTransform[2];
    private readonly List<Texture2D> generatedTextures = new List<Texture2D>();
    private readonly List<Sprite> generatedSprites = new List<Sprite>();
    private Sprite chamferSprite;
    private Sprite buttonGradientSprite;
    private int selectedAction;
    private bool inputArmed;
    private bool entering;

    public event System.Action<Action> ActionRequested;

    public bool Visible => gameObject.activeSelf;

    public static ResultScreen Create(Transform parent, TMP_FontAsset uiFont)
    {
        GameObject go = new GameObject("ResultScreenCanvas", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 15;

        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();

        ResultScreen screen = go.AddComponent<ResultScreen>();
        screen.font = uiFont;
        screen.Build((RectTransform)go.transform);
        go.SetActive(false);
        return screen;
    }

    private void Build(RectTransform root)
    {
        chamferSprite = CreateChamferSprite("ResultChamfer", false);
        buttonGradientSprite = CreateChamferSprite("ResultButtonGradient", true);

        Image background = NewImage("Background", root, DeepNavy);
        Stretch(background.rectTransform);
        BuildBackgroundDecor(root);

        GameObject content = NewRect("Content", root);
        contentRect = (RectTransform)content.transform;
        Stretch(contentRect);
        contentGroup = content.AddComponent<CanvasGroup>();

        BuildHeader(contentRect);
        BuildStageHeading(contentRect);
        BuildEvaluation(contentRect);
        BuildStats(contentRect);
        BuildActions(contentRect);
    }

    private void BuildBackgroundDecor(RectTransform root)
    {
        SoftCircleGraphic glow = NewGraphic<SoftCircleGraphic>("CentralGlow", root);
        glow.color = new Color(0.015f, 0.12f, 0.32f, 0.07f);
        SetRect(glow.rectTransform, Vector2.zero, new Vector2(900f, 900f));

        GameObject shapes = NewRect("BackgroundGeometry", root);
        RectTransform shapesRect = (RectTransform)shapes.transform;
        Stretch(shapesRect);
        Vector2[] positions =
        {
            new Vector2(-790f, 280f), new Vector2(780f, 260f),
            new Vector2(-820f, -320f), new Vector2(810f, -300f),
            new Vector2(-310f, -430f), new Vector2(350f, 410f),
        };
        float[] sizes = { 250f, 320f, 390f, 290f, 170f, 130f };
        for (int i = 0; i < positions.Length; i++)
        {
            Image diamond = NewImage("DriftingDiamond", shapesRect,
                new Color(0.008f, 0.10f, 0.32f, i < 4 ? 0.022f : 0.032f));
            SetRect(diamond.rectTransform, positions[i], Vector2.one * sizes[i]);
            diamond.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }
        shapes.AddComponent<ShapeDrifter>();

        // 画面端の縦線群。モックアップの建築シルエットを、プロジェクトの
        // 幾何学デザイン言語に合わせた抽象的なスカイラインへ置き換える。
        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < 7; i++)
            {
                float height = 210f + i * 55f;
                Image tower = NewImage("EdgeSpire", root,
                    new Color(0.004f, 0.07f, 0.24f, 0.055f));
                RectTransform tr = tower.rectTransform;
                tr.anchorMin = tr.anchorMax = new Vector2(side < 0 ? 0f : 1f, 0f);
                tr.pivot = new Vector2(side < 0 ? 0f : 1f, 0f);
                tr.anchoredPosition = new Vector2(side * (18f + i * 26f), 0f);
                tr.sizeDelta = new Vector2(5f, height);
                tr.localRotation = Quaternion.Euler(0f, 0f, side * (i % 2 == 0 ? -5f : 4f));
            }
        }
    }

    private void BuildHeader(RectTransform root)
    {
        Image line = NewImage("HeaderLine", root, new Color(Cyan.r, Cyan.g, Cyan.b, 0.75f));
        RectTransform lineRect = line.rectTransform;
        lineRect.anchorMin = new Vector2(0f, 1f);
        lineRect.anchorMax = new Vector2(1f, 1f);
        lineRect.pivot = new Vector2(0.5f, 1f);
        lineRect.anchoredPosition = new Vector2(0f, -88f);
        lineRect.sizeDelta = new Vector2(0f, 2f);

        Image brand = NewImage("BrandBanner", root, BrandBlue);
        RectTransform brandRect = brand.rectTransform;
        brandRect.anchorMin = brandRect.anchorMax = new Vector2(0f, 1f);
        brandRect.pivot = new Vector2(0f, 1f);
        brandRect.anchoredPosition = new Vector2(0f, 0f);
        brandRect.sizeDelta = new Vector2(620f, 88f);

        Image slashA = NewImage("HeaderSlashA", root, Color.white);
        SetTopLeftSlash(slashA.rectTransform, new Vector2(20f, -44f), 4f, 70f);
        Image slashB = NewImage("HeaderSlashB", root, new Color(Cyan.r, Cyan.g, Cyan.b, 1f));
        SetTopLeftSlash(slashB.rectTransform, new Vector2(33f, -44f), 4f, 70f);

        TMP_Text title = NewText("HeaderTitle", root, "戦績  /  RESULT", 40f, Color.white,
            TextAlignmentOptions.MidlineLeft);
        RectTransform titleRect = (RectTransform)title.transform;
        titleRect.anchorMin = titleRect.anchorMax = new Vector2(0f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.anchoredPosition = new Vector2(82f, -13f);
        titleRect.sizeDelta = new Vector2(500f, 64f);

        Image rightSlash = NewImage("HeaderRightSlash", root, Color.white);
        RectTransform rs = rightSlash.rectTransform;
        rs.anchorMin = rs.anchorMax = new Vector2(1f, 1f);
        rs.pivot = new Vector2(0.5f, 0.5f);
        rs.anchoredPosition = new Vector2(-28f, -45f);
        rs.sizeDelta = new Vector2(5f, 70f);
        rs.localRotation = Quaternion.Euler(0f, 0f, -42f);
    }

    private void BuildStageHeading(RectTransform root)
    {
        stageNameText = NewText("StageName", root, "STAGE", 30f, Color.white,
            TextAlignmentOptions.Center);
        SetRect((RectTransform)stageNameText.transform, new Vector2(0f, 413f), new Vector2(820f, 50f));

        difficultyText = NewText("Difficulty", root, "LUNATIC", 17f, Cyan,
            TextAlignmentOptions.Center);
        SetRect((RectTransform)difficultyText.transform, new Vector2(0f, 378f), new Vector2(240f, 32f));

        Image dashL = NewImage("StageDashL", root, new Color(Cyan.r, Cyan.g, Cyan.b, 0.55f));
        SetRect(dashL.rectTransform, new Vector2(-185f, 392f), new Vector2(150f, 2f));
        Image dashR = NewImage("StageDashR", root, new Color(Cyan.r, Cyan.g, Cyan.b, 0.55f));
        SetRect(dashR.rectTransform, new Vector2(185f, 392f), new Vector2(150f, 2f));
    }

    private void BuildEvaluation(RectTransform root)
    {
        SoftCircleGraphic aura = NewGraphic<SoftCircleGraphic>("RankAura", root);
        aura.color = new Color(Violet.r, Violet.g, Violet.b, 0.13f);
        SetRect(aura.rectTransform, new Vector2(0f, 38f), new Vector2(430f, 430f));

        for (int i = 0; i < 3; i++)
        {
            Image diamond = NewImage("RankDiamond", root,
                i == 0 ? new Color(0.002f, 0.012f, 0.035f, 0.99f) : new Color(0f, 0f, 0f, 0f));
            diamond.sprite = chamferSprite;
            diamond.type = Image.Type.Sliced;
            SetRect(diamond.rectTransform, new Vector2(0f, 38f), Vector2.one * (390f - i * 58f));
            diamond.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            if (i > 0)
            {
                // 透明な面に細い外周を重ねる代わりに、少し大きい色面と内側の
                // 暗色面を対にして、解像度に依存しない幾何学リムを作る。
                Image inner = NewImage("RankDiamondInner", root, DeepNavy);
                inner.sprite = chamferSprite;
                inner.type = Image.Type.Sliced;
                SetRect(inner.rectTransform, new Vector2(0f, 38f), Vector2.one * (384f - i * 58f));
                inner.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                diamond.color = i == 1
                    ? new Color(Cyan.r, Cyan.g, Cyan.b, 0.28f)
                    : new Color(Violet.r, Violet.g, Violet.b, 0.34f);
            }
        }

        verdictText = NewText("Verdict", root, "総合判定\n<size=18><color=#38C2E0>OVERALL EVALUATION</color></size>",
            32f, Color.white, TextAlignmentOptions.Center);
        SetRect((RectTransform)verdictText.transform, new Vector2(0f, 258f), new Vector2(500f, 90f));

        rankText = NewText("Rank", root, "S", 250f, Color.white, TextAlignmentOptions.Center);
        rankText.fontStyle = FontStyles.Bold;
        rankText.enableAutoSizing = true;
        rankText.fontSizeMin = 130f;
        rankText.fontSizeMax = 250f;
        rankText.outlineColor = new Color(Violet.r, Violet.g, Violet.b, 0.9f);
        rankText.outlineWidth = 0.2f;
        SetRect((RectTransform)rankText.transform, new Vector2(0f, 22f), new Vector2(330f, 310f));
    }

    private void BuildStats(RectTransform root)
    {
        scoreText = BuildStatCard(root, "Score", new Vector2(-610f, 150f), "スコア", "SCORE", "000,000", "01");
        hitText = BuildStatCard(root, "Hit", new Vector2(610f, 150f), "被弾回数", "HIT COUNT", "00", "02");
        counterText = BuildStatCard(root, "Counter", new Vector2(-610f, -115f), "カウンター回数", "COUNTER COUNT", "00", "03");
        timeText = BuildStatCard(root, "Time", new Vector2(610f, -115f), "時間", "TIME", "00:00", "04");
    }

    private TMP_Text BuildStatCard(RectTransform root, string name, Vector2 pos,
        string jp, string en, string value, string index)
    {
        GameObject card = NewRect(name + "Card", root);
        RectTransform rect = (RectTransform)card.transform;
        SetRect(rect, pos, new Vector2(470f, 205f));

        Image outer = NewImage("Rim", rect, Rim);
        outer.sprite = chamferSprite;
        outer.type = Image.Type.Sliced;
        Stretch(outer.rectTransform);

        Image body = NewImage("Body", rect, PanelNavy);
        body.sprite = chamferSprite;
        body.type = Image.Type.Sliced;
        Stretch(body.rectTransform);
        body.rectTransform.offsetMin = new Vector2(3f, 3f);
        body.rectTransform.offsetMax = new Vector2(-3f, -3f);

        Image accent = NewImage("Accent", rect, BrandBlue);
        RectTransform ar = accent.rectTransform;
        ar.anchorMin = ar.anchorMax = new Vector2(pos.x < 0f ? 1f : 0f, 0f);
        ar.pivot = new Vector2(0.5f, 0f);
        ar.anchoredPosition = new Vector2(pos.x < 0f ? -31f : 31f, 4f);
        ar.sizeDelta = new Vector2(12f, 76f);
        ar.localRotation = Quaternion.Euler(0f, 0f, pos.x < 0f ? -30f : 30f);

        TMP_Text number = NewText("Index", rect, index, 25f, Cyan, TextAlignmentOptions.Center);
        SetRect((RectTransform)number.transform, new Vector2(-170f, 42f), new Vector2(64f, 54f));

        TMP_Text label = NewText("Label", rect,
            jp + "\n<size=15><color=#38C2E0>" + en + "</color></size>",
            27f, Color.white, TextAlignmentOptions.MidlineLeft);
        SetRect((RectTransform)label.transform, new Vector2(28f, 43f), new Vector2(300f, 76f));

        Image rule = NewImage("Rule", rect, new Color(Rim.r, Rim.g, Rim.b, 0.42f));
        SetRect(rule.rectTransform, new Vector2(0f, 3f), new Vector2(330f, 2f));

        TMP_Text valueText = NewText("Value", rect, value, 48f, Color.white, TextAlignmentOptions.Center);
        valueText.characterSpacing = 4f;
        SetRect((RectTransform)valueText.transform, new Vector2(0f, -48f), new Vector2(390f, 70f));
        return valueText;
    }

    private void BuildActions(RectTransform root)
    {
        string[] labels = { "もう一度", "ステージ選択へ" };
        for (int i = 0; i < labels.Length; i++)
        {
            GameObject buttonGo = NewRect("Action" + i, root);
            RectTransform rect = (RectTransform)buttonGo.transform;
            SetRect(rect, new Vector2(i == 0 ? -270f : 270f, -405f), new Vector2(470f, 92f));
            actionRects[i] = rect;

            Image rim = NewImage("Rim", rect, Cyan);
            rim.sprite = chamferSprite;
            rim.type = Image.Type.Sliced;
            Stretch(rim.rectTransform);
            actionRims[i] = rim;

            Image body = NewImage("Body", rect, Color.white);
            body.sprite = buttonGradientSprite;
            body.type = Image.Type.Sliced;
            Stretch(body.rectTransform);
            body.rectTransform.offsetMin = new Vector2(4f, 4f);
            body.rectTransform.offsetMax = new Vector2(-4f, -4f);
            body.raycastTarget = true;
            actionBodies[i] = body;

            TMP_Text label = NewText("Label", rect, labels[i], 34f, Color.white, TextAlignmentOptions.Center);
            Stretch((RectTransform)label.transform);
            actionLabels[i] = label;

            int captured = i;
            Button button = buttonGo.AddComponent<Button>();
            button.targetGraphic = body;
            button.transition = Selectable.Transition.None;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            button.onClick.AddListener(() => RequestAction(captured));

            EventTrigger trigger = buttonGo.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerEnter, _ => Select(captured));
            AddTrigger(trigger, EventTriggerType.PointerDown, _ => Select(captured));
        }

        TMP_Text guide = NewText("InputGuide", root,
            "← → / A D  選択     SPACE  決定     ESC  戻る",
            16f, DimText, TextAlignmentOptions.Center);
        SetRect((RectTransform)guide.transform, new Vector2(0f, -475f), new Vector2(850f, 34f));
    }

    public void Prepare(StageData stage, int difficulty, bool cleared, int hitCount,
        int counterCount, float elapsedSeconds, float endSeconds)
    {
        gameObject.SetActive(true);
        selectedAction = 0;
        inputArmed = false;
        entering = false;

        string stageName = stage != null && !string.IsNullOrWhiteSpace(stage.stageName)
            ? stage.stageName
            : "UNKNOWN STAGE";
        stageNameText.text = stageName;
        difficultyText.text = DifficultyName(difficulty);
        verdictText.text = cleared
            ? "総合判定\n<size=16><color=#38C2E0>OVERALL EVALUATION</color></size>"
            : "攻略失敗\n<size=16><color=#FF6C8B>STAGE FAILED</color></size>";

        string rank = EvaluateRank(cleared, hitCount);
        rankText.text = rank;
        rankText.color = cleared ? Color.white : new Color(1f, 0.52f, 0.65f, 1f);
        rankText.outlineColor = cleared
            ? new Color(Violet.r, Violet.g, Violet.b, 0.9f)
            : new Color(0.85f, 0.08f, 0.22f, 0.9f);

        // TODO: 専用のスコア加算系が実装されたら、その確定値へ差し替える。
        // 現状はクリア状況・被弾・カウンター・到達率という実データから暫定算出する。
        int provisionalScore = CalculateProvisionalScore(
            cleared, hitCount, counterCount, elapsedSeconds, endSeconds);
        scoreText.text = provisionalScore.ToString("N0");
        hitText.text = Mathf.Max(0, hitCount).ToString("00");
        counterText.text = Mathf.Max(0, counterCount).ToString("00");
        timeText.text = FormatTime(elapsedSeconds);

        contentGroup.alpha = 0f;
        contentRect.anchoredPosition = new Vector2(0f, -18f);
        contentRect.localScale = Vector3.one * 0.975f;
        RefreshActionVisuals();
    }

    public async void PlayEntrance()
    {
        if (!gameObject.activeSelf || entering) return;
        entering = true;
        const float duration = 0.34f;
        float elapsed = 0f;
        while (elapsed < duration && this != null && gameObject.activeSelf)
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            float p = Mathf.Clamp01(elapsed / duration);
            float ease = 1f - Mathf.Pow(1f - p, 3f);
            contentGroup.alpha = ease;
            contentRect.anchoredPosition = Vector2.Lerp(new Vector2(0f, -18f), Vector2.zero, ease);
            contentRect.localScale = Vector3.one * Mathf.Lerp(0.975f, 1f, ease);
            await Task.Yield();
        }

        if (this == null) return;
        contentGroup.alpha = 1f;
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.localScale = Vector3.one;
        entering = false;
    }

    public void Tick(bool left, bool right, bool buttonHeld, bool buttonPressed, bool backPressed)
    {
        if (!gameObject.activeSelf || entering) return;

        if (backPressed)
        {
            RequestAction((int)Action.StageSelect);
            return;
        }

        if (!inputArmed)
        {
            if (!buttonHeld) inputArmed = true;
            return;
        }

        if (left) Select(selectedAction - 1);
        else if (right) Select(selectedAction + 1);
        else if (buttonPressed) RequestAction(selectedAction);
    }

    public void HideImmediate()
    {
        entering = false;
        gameObject.SetActive(false);
    }

    public static int CalculateProvisionalScore(bool cleared, int hitCount, int counterCount,
        float elapsedSeconds, float endSeconds)
    {
        float progress = endSeconds > 0.001f
            ? Mathf.Clamp01(elapsedSeconds / endSeconds)
            : (cleared ? 1f : 0f);
        int score = Mathf.RoundToInt(progress * 700000f);
        if (cleared) score += 200000;
        score += Mathf.Max(0, counterCount) * 7500;
        score -= Mathf.Max(0, hitCount) * 40000;
        return Mathf.Clamp(score, 0, 999999);
    }

    public static string EvaluateRank(bool cleared, int hitCount)
    {
        if (!cleared) return "F";
        if (hitCount <= 0) return "S";
        if (hitCount <= 2) return "A";
        if (hitCount <= 5) return "B";
        return "C";
    }

    private void Select(int index)
    {
        selectedAction = (index % actionRects.Length + actionRects.Length) % actionRects.Length;
        RefreshActionVisuals();
    }

    private void RefreshActionVisuals()
    {
        for (int i = 0; i < actionRects.Length; i++)
        {
            if (actionRects[i] == null || actionRims[i] == null ||
                actionBodies[i] == null || actionLabels[i] == null)
            {
                Transform action = contentRect != null ? contentRect.Find("Action" + i) : null;
                if (action == null) continue;
                actionRects[i] = action as RectTransform;
                actionRims[i] = action.Find("Rim")?.GetComponent<Image>();
                actionBodies[i] = action.Find("Body")?.GetComponent<Image>();
                actionLabels[i] = action.Find("Label")?.GetComponent<TMP_Text>();
                if (actionRects[i] == null || actionRims[i] == null ||
                    actionBodies[i] == null || actionLabels[i] == null) continue;
            }

            bool selected = i == selectedAction;
            actionRims[i].color = selected ? Color.white : new Color(Cyan.r, Cyan.g, Cyan.b, 0.55f);
            actionBodies[i].color = selected ? Color.white : new Color(0.006f, 0.035f, 0.09f, 0.9f);
            actionLabels[i].color = selected ? Color.white : new Color(0.68f, 0.82f, 0.91f, 0.9f);
            actionRects[i].localScale = Vector3.one * (selected ? 1.035f : 1f);
        }
    }

    private void RequestAction(int index)
    {
        if (!gameObject.activeSelf) return;
        Select(index);
        ActionRequested?.Invoke((Action)selectedAction);
    }

    private static string DifficultyName(int difficulty)
    {
        switch (Mathf.Clamp(difficulty, 0, 2))
        {
            case 0: return "EASY";
            case 2: return "LUNATIC";
            default: return "NORMAL";
        }
    }

    private static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return string.Format("{0:00}:{1:00}", total / 60, total % 60);
    }

    private Sprite CreateChamferSprite(string spriteName, bool gradient)
    {
        const int width = 192;
        const int height = 80;
        const int chamfer = 22;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = spriteName + "Texture";
        texture.filterMode = FilterMode.Bilinear;
        Color32[] pixels = new Color32[width * height];
        Color left = gradient ? new Color(0.055f, 0.28f, 0.708f, 1f) : Color.white;
        Color right = gradient ? new Color(0.04f, 0.54f, 0.75f, 1f) : Color.white;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int edgeX = Mathf.Min(x, width - 1 - x);
                int edgeY = Mathf.Min(y, height - 1 - y);
                bool inside = edgeX + edgeY >= chamfer;
                Color color = Color.Lerp(left, right, x / (float)(width - 1));
                if (!inside) color.a = 0f;
                pixels[y * width + x] = color;
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        generatedTextures.Add(texture);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
            new Vector4(34f, 34f, 34f, 34f));
        sprite.name = spriteName;
        generatedSprites.Add(sprite);
        return sprite;
    }

    private TMP_Text NewText(string name, Transform parent, string value, float size,
        Color color, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }

    private static Image NewImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static T NewGraphic<T>(string name, Transform parent) where T : Graphic
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
        go.transform.SetParent(parent, false);
        T graphic = go.GetComponent<T>();
        graphic.raycastTarget = false;
        return graphic;
    }

    private static GameObject NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetTopLeftSlash(RectTransform rect, Vector2 pos, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(width, height);
        rect.localRotation = Quaternion.Euler(0f, 0f, -42f);
    }

    private static void AddTrigger(EventTrigger trigger, EventTriggerType type,
        UnityEngine.Events.UnityAction<BaseEventData> callback)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(callback);
        trigger.triggers.Add(entry);
    }

#if UNITY_EDITOR
    // MCP 検証用。EditorApplication.update では Game View が白/黒になるため、
    // Play 内で描画完了を数フレーム待ってから保存する。
    public void CaptureDebugScreenshot(string fileName)
    {
        StartCoroutine(CaptureDebugScreenshotRoutine(fileName));
    }

    private IEnumerator CaptureDebugScreenshotRoutine(string fileName)
    {
        for (int i = 0; i < 5; i++) yield return new WaitForEndOfFrame();
        string directory = Path.Combine(Application.dataPath, "Screenshots");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, fileName);
        ScreenCapture.CaptureScreenshot(path);
        Debug.Log($"[ResultScreen] Screenshot requested: {path}");
    }
#endif

    private void OnDestroy()
    {
        foreach (Sprite sprite in generatedSprites)
        {
            if (sprite != null) Destroy(sprite);
        }
        foreach (Texture2D texture in generatedTextures)
        {
            if (texture != null) Destroy(texture);
        }
    }
}

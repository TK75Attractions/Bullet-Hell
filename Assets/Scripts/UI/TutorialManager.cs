using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Pre-stage tutorial, start callout and song intro. The tutorial visuals are
// generated at runtime so they stay aligned with the existing 1920x1080 canvas
// without adding scene-only references.
public class TutorialManager : MonoBehaviour
{
    private static readonly Color easyColor = new Color(0.56f, 0.72f, 0.91f);
    private static readonly Color normalColor = new Color(0.3f, 0.65f, 1f);
    private static readonly Color lunaticColor = new Color(0.95f, 0.45f, 0.6f);
    private static readonly Color cyan = new Color(0.02f, 0.78f, 1f);
    private static readonly Color panelColor = new Color(0.018f, 0.028f, 0.075f, 0.97f);
    private static readonly Color keyColor = new Color(0.92f, 0.94f, 0.97f, 0.98f);
    // 白いキーキャップ上でアイコン線画を濃紺に tint(ラベルと同系で可読性確保)。
    private static readonly Color iconTint = new Color(0.03f, 0.12f, 0.28f, 1f);

    private TMP_Text tutorialText;
    private RectTransform tutorialRect;
    private TMP_Text startText;
    private RectTransform startRect;
    private TMP_Text introName;
    private RectTransform introNameRect;
    private TMP_Text introDiff;
    private RectTransform introDiffRect;
    private float introNameBaseX;
    private float introDiffBaseX;

    private RectTransform cardRect;
    private CanvasGroup cardGroup;
    private RectTransform moveIconRoot;
    private CanvasGroup moveIconGroup;
    private RectTransform dashIconRoot;
    private CanvasGroup dashIconGroup;
    private TMP_Text dashNote;
    private readonly List<KeyVisual> moveKeys = new List<KeyVisual>();
    private readonly List<KeyVisual> dashKeys = new List<KeyVisual>();
    private Vector2 cardBasePosition;
    private bool initialized;

    private sealed class KeyVisual
    {
        public RectTransform rect;
        public Image image;
        public Vector2 basePosition;
    }

    private void Awake()
    {
        EnsureInit();
    }

    private void EnsureInit()
    {
        if (initialized) return;
        initialized = true;

        Transform t = transform.Find("TutorialText");
        if (t != null) { tutorialText = t.GetComponent<TMP_Text>(); tutorialRect = t as RectTransform; }
        Transform s = transform.Find("StartText");
        if (s != null) { startText = s.GetComponent<TMP_Text>(); startRect = s as RectTransform; }
        Transform n = transform.Find("IntroName");
        if (n != null) { introName = n.GetComponent<TMP_Text>(); introNameRect = n as RectTransform; introNameBaseX = introNameRect.anchoredPosition.x; }
        Transform d = transform.Find("IntroDiff");
        if (d != null) { introDiff = d.GetComponent<TMP_Text>(); introDiffRect = d as RectTransform; introDiffBaseX = introDiffRect.anchoredPosition.x; }

        NormalizeUnsupportedPunctuation(tutorialText);
        NormalizeUnsupportedPunctuation(startText);
        NormalizeUnsupportedPunctuation(introName);
        NormalizeUnsupportedPunctuation(introDiff);

        if (tutorialText != null)
        {
            tutorialText.alpha = 0f;
            BuildTutorialVisuals();
        }
        if (startText != null) startText.alpha = 0f;
        if (introName != null) introName.alpha = 0f;
        if (introDiff != null) introDiff.alpha = 0f;
    }

    private static void NormalizeUnsupportedPunctuation(TMP_Text text)
    {
        if (text == null || string.IsNullOrEmpty(text.text)) return;
        text.text = text.text.Replace('\uFF01', '!');
    }

    // ---- Highland UI v11 のチュートリアルカード(2026-09-19 U7) ----------------
    // 出典 07_tutorial_move / states/08_tutorial_dash_CIRCLE_ONLY。
    // 左に操作アイコンの札(20fps のスプライトシート再生)、縦の仕切りを挟んで
    // 右にふりがな付きの本文 2 行。
    private const float CardPanelCx = 836f, CardPanelCy = 273f, CardPanelW = 1400f, CardPanelH = 310f;

    private readonly List<Texture2D> ownedTextures = new List<Texture2D>();
    private readonly List<Sprite> ownedSprites = new List<Sprite>();
    private RawImage moveIconImage, dashIconImage;
    private float animTime;
    private HighlandUi.RubyText moveLine1, moveLine2, dashLine1, dashLine2;
    private RectTransform moveDivider, dashDivider;
    private TMP_Text progressText;

    private void BuildTutorialVisuals()
    {
        GameObject card = new GameObject("TutorialCard", typeof(RectTransform));
        card.layer = gameObject.layer;
        cardRect = (RectTransform)card.transform;
        cardRect.SetParent(transform, false);
        cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardBasePosition = new Vector2(HighlandUi.X(CardPanelCx), HighlandUi.Y(CardPanelCy));
        cardRect.anchoredPosition = cardBasePosition;
        cardRect.sizeDelta = new Vector2(HighlandUi.L(CardPanelW), HighlandUi.L(CardPanelH));
        cardRect.SetAsFirstSibling();
        cardGroup = card.AddComponent<CanvasGroup>();
        cardGroup.alpha = 0f;

        // 板(四隅をえぐった二重枠)。
        Image plate = NewCardImage("Panel", cardRect, Color.white);
        plate.sprite = HighlandUi.NotchPanel((int)HighlandUi.L(CardPanelW), (int)HighlandUi.L(CardPanelH),
            HighlandUi.L(27f), false, ownedTextures, ownedSprites, "TutPanel",
            HighlandUi.L(11f), 1.9f * HighlandUi.S, 0.75f * HighlandUi.S);
        plate.rectTransform.sizeDelta = cardRect.sizeDelta;

        // 左右の中空菱形(x=159 / 1513, y=279)。
        foreach (float sx in new[] { 159f, 1513f })
        {
            Image d = NewCardImage("SideGem", cardRect, new Color(0.784f, 0.784f, 0.784f, 0.6f));
            d.sprite = HighlandUi.DiamondRect(17, 20, false, 1.4f * HighlandUi.S,
                ownedTextures, ownedSprites, "TutSideGem");
            PlaceInCard(d.rectTransform, sx, 279f, 14.4f, 17.6f);
        }

        // 縦の仕切り(中央に菱形ぶんの隙間)。移動は x=725 / ダッシュは x=810。
        moveDivider = BuildDivider(725f);
        dashDivider = BuildDivider(810f);

        // 左: 操作アイコンの札 + 20fps の再生。
        moveIconRoot = CreateIconRoot("MoveIcons", cardRect);
        moveIconGroup = moveIconRoot.gameObject.AddComponent<CanvasGroup>();
        moveIconImage = BuildKeyTag(moveIconRoot, 535f, 273f, 194f, 194f, 13.5f, "anim_lever", 150f);

        dashIconRoot = CreateIconRoot("DashIcons", cardRect);
        dashIconGroup = dashIconRoot.gameObject.AddComponent<CanvasGroup>();
        dashIconImage = BuildKeyTag(dashIconRoot, 535f, 272f, 180f, 188f, 12.5f, "anim_circle", 140f);

        // 右: 本文 2 行(ふりがな付き)。シーンの TutorialText は使わない。
        tutorialText.alpha = 0f;
        tutorialText.gameObject.SetActive(false);
        moveLine1 = BuildLine("MoveLine1", 834.32f, 263f, "レバーで [上|うえ]・[下|した]・[左|ひだり]・[右|みぎ]に");
        moveLine2 = BuildLine("MoveLine2", 946.25f, 330f, "[移動|いどう]しよう！");
        dashLine1 = BuildLine("DashLine1", 975.55f, 263f, "[押|お]すとダッシュ！");
        dashLine2 = BuildLine("DashLine2", 957.7f, 330f, "ダッシュ[中|ちゅう]は[無敵|むてき]！");

        // 2P の「1P OK / 2P --」表示(v11 の絵には無い項目。既定で本文の下へ)。
        progressText = HighlandUi.Text("Progress", cardRect, "", 20f, HighlandUi.InkSoft,
            TextAlignmentOptions.Center, false, 1f);
        PlaceTextInCard(progressText, 1110f, 386f, 520f, 20f, TextAlignmentOptions.Center);
        progressText.gameObject.SetActive(false);

        SetLineGroup(false);
        moveIconRoot.gameObject.SetActive(false);
        dashIconRoot.gameObject.SetActive(false);
    }

    private RectTransform BuildDivider(float svgX)
    {
        GameObject go = new GameObject("Divider", typeof(RectTransform));
        go.layer = gameObject.layer;
        RectTransform root = (RectTransform)go.transform;
        root.SetParent(cardRect, false);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = Vector2.zero;
        root.sizeDelta = Vector2.zero;

        foreach (float cy in new[] { 215.5f, 330.5f })
        {
            Image line = NewCardImage("Line", root, new Color(0.788f, 0.788f, 0.788f, 0.45f));
            line.sprite = HighlandUi.SolidBar(ownedTextures, ownedSprites);
            PlaceInCard(line.rectTransform, svgX, cy, 1.05f, 87f);
        }
        Image gem = NewCardImage("Gem", root, new Color(0.788f, 0.788f, 0.788f, 0.74f));
        gem.sprite = HighlandUi.DiamondRect(15, 18, false, 1.7f * HighlandUi.S,
            ownedTextures, ownedSprites, "TutDivGem");
        PlaceInCard(gem.rectTransform, svgX, 273f, 12.6f, 15.4f);
        return root;
    }

    // 白い札 + その上で 20fps 再生する操作アイコン。
    private RawImage BuildKeyTag(Transform parent, float svgCx, float svgCy,
        float svgW, float svgH, float notch, string animResource, float iconSvgSize)
    {
        Image tag = NewCardImage("Tag", parent, Color.white);
        tag.sprite = HighlandUi.NotchFlat((int)HighlandUi.L(svgW), (int)HighlandUi.L(svgH),
            HighlandUi.L(notch), new Color32(0xF5, 0xF5, 0xF5, 0xFF), 1f,
            new Color32(0xF0, 0xD7, 0x5B, 0xFF), 1.8f * HighlandUi.S, 1f,
            ownedTextures, ownedSprites, "TutTag" + animResource);
        PlaceInCard(tag.rectTransform, svgCx, svgCy, svgW, svgH);

        GameObject go = new GameObject("Anim", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        go.layer = gameObject.layer;
        RawImage img = go.GetComponent<RawImage>();
        img.rectTransform.SetParent(parent, false);
        img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        img.raycastTarget = false;
        img.texture = Resources.Load<Texture2D>("UI/v11/" + animResource);
        PlaceInCard(img.rectTransform, svgCx, svgCy, iconSvgSize, iconSvgSize);
        return img;
    }

    private HighlandUi.RubyText BuildLine(string name, float svgX, float svgBaseline, string markup)
    {
        TMP_Text body = HighlandUi.Text(name, cardRect, "", 35f, HighlandUi.InkSoft,
            TextAlignmentOptions.Left, false, 0.7f);
        HighlandUi.RubyText r = new HighlandUi.RubyText(body, cardRect, 35f, HighlandUi.InkSoft);
        r.Apply(markup);
        PlaceTextInCard(body, svgX, svgBaseline, 700f, 35f, TextAlignmentOptions.Left);
        rubies.Add(r);
        return r;
    }

    private readonly List<HighlandUi.RubyText> rubies = new List<HighlandUi.RubyText>();
    private bool rubiesPlaced;

    // カード(中心 = SVG の CardPanelCx/Cy)の中へ SVG 座標で置く。
    private void PlaceInCard(RectTransform rt, float svgCx, float svgCy, float svgW, float svgH)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(
            HighlandUi.L(svgCx - CardPanelCx), HighlandUi.L(CardPanelCy - svgCy));
        rt.sizeDelta = new Vector2(HighlandUi.L(svgW), HighlandUi.L(svgH));
    }

    private void PlaceTextInCard(TMP_Text t, float svgX, float svgBaseline, float svgW, float svgSize,
        TextAlignmentOptions align)
    {
        RectTransform rt = (RectTransform)t.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(align == TextAlignmentOptions.Center ? 0.5f : 0f, 0.5f);
        t.alignment = align;
        rt.sizeDelta = new Vector2(HighlandUi.L(svgW), HighlandUi.L(svgSize * 1.6f));
        rt.anchoredPosition = new Vector2(
            HighlandUi.L(svgX - CardPanelCx),
            HighlandUi.L(CardPanelCy - (svgBaseline - svgSize * 0.34f)));
    }

    private Image NewCardImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;
        Image img = go.GetComponent<Image>();
        img.rectTransform.SetParent(parent, false);
        img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // 表示中の本文だけ出す。
    private void SetLineGroup(bool dash)
    {
        if (moveLine1 == null) return;
        moveLine1.SetVisible(!dash);
        moveLine2.SetVisible(!dash);
        dashLine1.SetVisible(dash);
        dashLine2.SetVisible(dash);
        if (moveDivider != null) moveDivider.gameObject.SetActive(!dash);
        if (dashDivider != null) dashDivider.gameObject.SetActive(dash);
        rubiesPlaced = false;
    }

    // 20fps のスプライトシート再生(〇=64 コマ 8x8 / レバー=128 コマ 8x16)。
    private void Update()
    {
        if (cardGroup == null || cardGroup.alpha <= 0.001f) return;
        animTime += Time.unscaledDeltaTime;
        StepSheet(moveIconImage, 8, 16, 128);
        StepSheet(dashIconImage, 8, 8, 64);

        if (!rubiesPlaced)
        {
            bool all = true;
            foreach (HighlandUi.RubyText r in rubies)
            {
                if (r == null || r.Body == null) continue;
                if (!r.Body.gameObject.activeInHierarchy) continue;
                all &= r.EnsurePlaced();
            }
            rubiesPlaced = all;
        }
    }

    private void StepSheet(RawImage img, int cols, int rows, int frames)
    {
        if (img == null || img.texture == null || !img.gameObject.activeInHierarchy) return;
        int f = Mathf.FloorToInt(animTime * 20f) % frames;
        int cx = f % cols, cy = f / cols;
        float w = 1f / cols, h = 1f / rows;
        // スプライトシートの原点は左上、UV は左下始まりなので行を反転する。
        img.uvRect = new Rect(cx * w, 1f - (cy + 1) * h, w, h);
    }

    private void OnDestroy()
    {
        foreach (Sprite sp in ownedSprites) if (sp != null) Destroy(sp);
        foreach (Texture2D tx in ownedTextures) if (tx != null) Destroy(tx);
        ownedSprites.Clear();
        ownedTextures.Clear();
    }

    private RectTransform CreateIconRoot(string objectName, Transform parent)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.layer = gameObject.layer;
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = cardRect.sizeDelta;
        return rect;
    }

    private GameObject CreateImageObject(string objectName, Transform parent, Color color)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return go;
    }

    private TMP_Text CreateLabel(string objectName, Transform parent, Vector2 position, Vector2 size, float fontSize)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.layer = gameObject.layer;
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        label.font = tutorialText.font;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    // Step 1: hold movement, Step 2: dash. Keycaps stay visually stable while
    // pressed; input only advances the tutorial.
    public async Task RunTutorial(InputManager input, bool twoPlayer = false)
    {
        EnsureInit();
        if (tutorialText == null || input == null) return;

        bool p1Complete = false;
        bool p2Complete = !twoPlayer;
        string moveMessage = twoPlayer ? "P1 / P2  スティックで移動" : "スティックで移動";
        await ShowTutorialStep(moveMessage, false);
        while (!IsTutorialStepComplete(twoPlayer, p1Complete, p2Complete))
        {
            p1Complete |= input.upPressed || input.downPressed || input.leftPressed || input.rightPressed;
            p2Complete |= input.p2Up || input.p2Down || input.p2Left || input.p2Right;
            SetTutorialProgress(moveMessage, twoPlayer, p1Complete, p2Complete);
            await Task.Yield();
            if (this == null) return;
        }
        SetTutorialProgress(moveMessage, twoPlayer, true, true);
        await CompleteTutorialStep(false);

        p1Complete = false;
        p2Complete = !twoPlayer;
        string dashMessage = twoPlayer ? "P1 / P2  移動しながらダッシュ" : "移動しながらダッシュ";
        await ShowTutorialStep(dashMessage, true);
        while (!IsTutorialStepComplete(twoPlayer, p1Complete, p2Complete))
        {
            p1Complete |= input.buttonPressedThisFrame &&
                (input.upPressed || input.downPressed || input.leftPressed || input.rightPressed);
            p2Complete |= input.p2ButtonPressedThisFrame &&
                (input.p2Up || input.p2Down || input.p2Left || input.p2Right);
            SetTutorialProgress(dashMessage, twoPlayer, p1Complete, p2Complete);
            await Task.Yield();
            if (this == null) return;
        }
        SetTutorialProgress(dashMessage, twoPlayer, true, true);
        await CompleteTutorialStep(true);
    }

    // 1P では P1 の完了だけ、2P では両者の完了が揃ったときだけ次へ進む。
    public static bool IsTutorialStepComplete(bool twoPlayer, bool p1Complete, bool p2Complete)
    {
        return p1Complete && (!twoPlayer || p2Complete);
    }

    private void SetTutorialProgress(string message, bool twoPlayer, bool p1Complete, bool p2Complete)
    {
        if (progressText == null) return;
        progressText.gameObject.SetActive(twoPlayer);
        if (!twoPlayer) return;
        progressText.text = "<color=#FFCC66>1P " + (p1Complete ? "OK" : "--")
            + "</color>   <color=#73D9FF>2P " + (p2Complete ? "OK" : "--") + "</color>";
    }

    private async Task ShowTutorialStep(string message, bool dash)
    {
        List<KeyVisual> keys = dash ? dashKeys : moveKeys;
        moveIconRoot.gameObject.SetActive(!dash);
        dashIconRoot.gameObject.SetActive(dash);
        CanvasGroup icons = dash ? dashIconGroup : moveIconGroup;

        SetLineGroup(dash);

        cardGroup.alpha = 0f;
        cardRect.anchoredPosition = cardBasePosition;
        cardRect.localScale = Vector3.one;
        cardRect.localEulerAngles = Vector3.zero;
        icons.alpha = 0f;
        ResetKeys(keys);

        float time = 0f;
        const float duration = 0.2f;
        while (time < duration)
        {
            time += Time.deltaTime;
            float p = Mathf.Clamp01(time / duration);
            float iconFade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((p - 0.2f) / 0.8f));
            cardGroup.alpha = p;
            icons.alpha = iconFade;

            await Task.Yield();
            if (this == null) return;
        }

        cardGroup.alpha = 1f;
        cardRect.anchoredPosition = cardBasePosition;
        cardRect.localScale = Vector3.one;
        cardRect.localEulerAngles = Vector3.zero;
        icons.alpha = 1f;
        SnapKeys(keys);
    }

    private async Task CompleteTutorialStep(bool dash)
    {
        List<KeyVisual> keys = dash ? dashKeys : moveKeys;
        float time = 0f;
        const float duration = 0.25f;
        while (time < duration)
        {
            time += Time.deltaTime;
            float p = Mathf.Clamp01(time / duration);
            float fade = 1f - p;
            cardGroup.alpha = fade;
            (dash ? dashIconGroup : moveIconGroup).alpha = fade * fade;

            await Task.Yield();
            if (this == null) return;
        }
        cardGroup.alpha = 0f;
        (dash ? dashIconGroup : moveIconGroup).alpha = 0f;
        cardRect.localScale = Vector3.one;
        cardRect.localEulerAngles = Vector3.zero;
        ResetKeys(keys);
    }

    private void ResetKeys(List<KeyVisual> keys)
    {
        foreach (KeyVisual key in keys)
        {
            key.rect.anchoredPosition = key.basePosition;            key.rect.localScale = Vector3.one;
            key.rect.localEulerAngles = Vector3.zero;
            key.image.color = keyColor;
        }
    }

    private void SnapKeys(List<KeyVisual> keys)
    {
        foreach (KeyVisual key in keys)
        {
            key.rect.anchoredPosition = key.basePosition;
            key.rect.localScale = Vector3.one;
            key.rect.localEulerAngles = Vector3.zero;
            key.image.color = keyColor;
        }
    }

    private static float EaseOutBack(float p)
    {
        float x = p - 1f;
        return 1f + 2.7f * x * x * x + 1.7f * x * x;
    }

    // Short, quiet start cue before control passes to the stage.
    public async Task ShowStartText()
    {
        EnsureInit();
        if (startText == null) return;

        startText.text = "はじまるよ!";
        startText.color = Color.white;
        startText.alpha = 1f;
        startRect.localScale = Vector3.one * 1.65f;
        startRect.localEulerAngles = Vector3.zero;

        float time = 0f;
        const float inTime = 0.32f;
        while (time < inTime)
        {
            time += Time.deltaTime;
            float p = Mathf.Clamp01(time / inTime);
            startText.alpha = 1f;
            startRect.localScale = Vector3.one * Mathf.LerpUnclamped(1.65f, 1f, EaseOutBack(p));
            await Task.Yield();
            if (this == null) return;
        }

        startRect.localScale = Vector3.one;
        float hold = 0.85f;
        while (hold > 0f)
        {
            hold -= Time.deltaTime;
            await Task.Yield();
            if (this == null) return;
        }

        time = 0f;
        const float outTime = 0.38f;
        while (time < outTime)
        {
            time += Time.deltaTime;
            float p = Mathf.Clamp01(time / outTime);
            float fade = p * p;
            startText.alpha = 1f - fade;
            startRect.localScale = Vector3.one * Mathf.Lerp(1f, 0.97f, fade);
            await Task.Yield();
            if (this == null) return;
        }
        startText.alpha = 0f;
        startRect.localScale = Vector3.one;
        startRect.localEulerAngles = Vector3.zero;
    }

    // Big song title + difficulty sliding in from the right, holding, then fading.
    public async Task ShowSongIntro(string songName, string difficultyName, int difficultyIndex)
    {
        EnsureInit();
        if (introName == null || introDiff == null) return;
        introName.text = songName;
        introDiff.text = difficultyName;
        introDiff.color = difficultyIndex == 0 ? easyColor : difficultyIndex == 2 ? lunaticColor : normalColor;

        const float slide = 420f;
        float time = 0f;
        const float inTime = 0.45f;
        while (time < inTime)
        {
            time += Time.deltaTime;
            float p = Mathf.Clamp01(time / inTime);
            float ease = 1f - Mathf.Pow(1f - p, 3f);
            introNameRect.anchoredPosition = new Vector2(introNameBaseX + slide * (1f - ease), introNameRect.anchoredPosition.y);
            introDiffRect.anchoredPosition = new Vector2(introDiffBaseX + slide * (1f - ease), introDiffRect.anchoredPosition.y);
            introNameRect.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, EaseOutBack(p));
            introDiffRect.localScale = introNameRect.localScale;
            introName.alpha = ease;
            introDiff.alpha = ease;
            await Task.Yield();
            if (this == null) return;
        }

        float hold = 2.2f;
        while (hold > 0f) { hold -= Time.deltaTime; await Task.Yield(); if (this == null) return; }

        time = 0f;
        const float outTime = 0.5f;
        while (time < outTime)
        {
            time += Time.deltaTime;
            float p = Mathf.Clamp01(time / outTime);
            float ease = p * p;
            introName.alpha = 1f - ease;
            introDiff.alpha = 1f - ease;
            await Task.Yield();
            if (this == null) return;
        }
        introName.alpha = 0f;
        introDiff.alpha = 0f;
        introNameRect.localScale = Vector3.one;
        introDiffRect.localScale = Vector3.one;
    }
}

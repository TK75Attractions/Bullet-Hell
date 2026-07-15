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

    private void BuildTutorialVisuals()
    {
        GameObject card = new GameObject("TutorialCard", typeof(RectTransform));
        card.layer = gameObject.layer;
        cardRect = (RectTransform)card.transform;
        cardRect.SetParent(transform, false);
        cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardBasePosition = new Vector2(0f, 345f);
        cardRect.anchoredPosition = cardBasePosition;
        cardRect.sizeDelta = new Vector2(900f, 230f);
        cardRect.SetAsFirstSibling();
        cardGroup = card.AddComponent<CanvasGroup>();
        cardGroup.alpha = 0f;
        // ユーザー確定様式(2026-07-11): 端の白スラッシュは付けず、
        // パネル上下辺に控えめな青ラインを沿わせるだけにする。
        CreateRibbonPanel("Panel", cardRect, Vector2.zero, new Vector2(780f, 194f), panelColor, 34f);
        CreateEdgeLine(cardRect, new Vector2(780f, 194f), 34f, true);
        CreateEdgeLine(cardRect, new Vector2(780f, 194f), 34f, false);

        tutorialRect.anchoredPosition = new Vector2(145f, cardBasePosition.y);
        tutorialRect.sizeDelta = new Vector2(410f, 92f);
        tutorialText.alignment = TextAlignmentOptions.MidlineLeft;
        tutorialText.fontStyle = FontStyles.Bold;
        tutorialText.enableAutoSizing = true;
        tutorialText.fontSizeMin = 27f;
        tutorialText.fontSizeMax = 46f;
        tutorialText.enableWordWrapping = false;

        // 実機はジョイスティック+ボタン想定(2026-07-13 指摘)。WASD の 4 キーの代わりに
        // 「スティック」1 枚、ダッシュは「ボタン」で案内する。各キーキャップには
        // ランタイム生成のアイコン(スティック左右 / ボタン)を添える(2026-07-14 要望)。
        moveIconRoot = CreateIconRoot("MoveIcons", cardRect);
        moveIconGroup = moveIconRoot.gameObject.AddComponent<CanvasGroup>();
        moveKeys.Add(CreateKey(moveIconRoot, "スティック", UiIconFactory.StickLeftRight(), Vector2.zero, new Vector2(248f, 62f)));

        // ダッシュは「移動しながらボタン」を示すため、スティックとボタンの2枚を縦に並べる。
        dashIconRoot = CreateIconRoot("DashIcons", cardRect);
        dashIconGroup = dashIconRoot.gameObject.AddComponent<CanvasGroup>();
        dashKeys.Add(CreateKey(dashIconRoot, "スティック", UiIconFactory.StickLeftRight(), new Vector2(0f, 34f), new Vector2(248f, 56f)));
        dashKeys.Add(CreateKey(dashIconRoot, "ボタン", UiIconFactory.Button(), new Vector2(0f, -34f), new Vector2(248f, 56f)));

        // 「ダッシュ中は無敵!」の注記(ダッシュステップのみ表示)。無敵は PlayerController の
        // invincible(dash>0 で true・TryHit を無効化)で実装済み=事実として明記する。
        // 濃紺パネル上なのでアクセントのシアンで強調(既存の強調色を踏襲)。card の子に
        // するので cardGroup のフェードに追従する。
        dashNote = CreateLabel("DashNote", cardRect, new Vector2(145f, -44f), new Vector2(440f, 46f), 26f);
        dashNote.text = "ダッシュ中は無敵!";
        dashNote.color = cyan;
        dashNote.fontStyle = FontStyles.Bold;
        dashNote.alignment = TextAlignmentOptions.MidlineLeft;
        dashNote.gameObject.SetActive(false);

        moveIconRoot.gameObject.SetActive(false);
        dashIconRoot.gameObject.SetActive(false);
    }

    private void CreateRibbonPanel(string objectName, Transform parent, Vector2 position, Vector2 size, Color color, float slant)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(ParallelogramGraphic));
        go.layer = gameObject.layer;
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        ParallelogramGraphic graphic = go.GetComponent<ParallelogramGraphic>();
        graphic.color = color;
        graphic.Slant = slant;
        graphic.SlantRightEdge = true;
        graphic.raycastTarget = false;
    }

    // パネルの上辺/下辺に沿う細い青ライン。パネルと同じ平行四辺形規約で作り、
    // 高さ h の帯の skew を panelSlant*h/panelH にすると端の斜めに正確に沿う。
    private void CreateEdgeLine(Transform parent, Vector2 panelSize, float panelSlant, bool top)
    {
        const float h = 3f;
        float skew = panelSlant * h / panelSize.y;
        GameObject go = new GameObject(top ? "EdgeLineTop" : "EdgeLineBottom",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(ParallelogramGraphic));
        go.layer = gameObject.layer;
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(panelSize.x - panelSlant + skew, h);
        float dirX = top ? 1f : -1f;
        rect.anchoredPosition = new Vector2(dirX * (panelSlant - skew) * 0.5f,
            dirX * (panelSize.y - h) * 0.5f);

        ParallelogramGraphic line = go.GetComponent<ParallelogramGraphic>();
        Color c = cyan; c.a = 0.55f;
        line.color = c;
        line.Slant = skew;
        line.SlantRightEdge = true;
        line.raycastTarget = false;
    }

    private RectTransform CreateIconRoot(string objectName, Transform parent)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.layer = gameObject.layer;
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(-210f, 0f);
        rect.sizeDelta = new Vector2(280f, 150f);
        return rect;
    }

    private KeyVisual CreateKey(Transform parent, string label, Sprite icon, Vector2 position, Vector2 size)
    {
        GameObject key = CreateImageObject("Key_" + label, parent, keyColor);
        RectTransform rect = (RectTransform)key.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Outline outline = key.AddComponent<Outline>();
        outline.effectColor = new Color(0.01f, 0.23f, 0.48f, 0.95f);
        outline.effectDistance = new Vector2(2f, -2f);

        GameObject accent = CreateImageObject("CyanEdge", rect, cyan);
        RectTransform accentRect = (RectTransform)accent.transform;
        accentRect.anchorMin = accentRect.anchorMax = new Vector2(0.5f, 0.5f);
        accentRect.anchoredPosition = new Vector2(0f, -size.y * 0.5f + 3f);
        accentRect.sizeDelta = new Vector2(size.x - 8f, 6f);

        // アイコンを左側に配置し、ラベルを右の残り領域へ中央寄せする。
        float textLeftPad = 0f;
        if (icon != null)
        {
            float iconH = 30f;
            float iconW = iconH * (icon.rect.width / icon.rect.height);
            GameObject ic = CreateImageObject("Icon", rect, iconTint);
            Image icImg = ic.GetComponent<Image>();
            icImg.sprite = icon;
            icImg.preserveAspect = true;
            RectTransform icr = (RectTransform)ic.transform;
            icr.anchorMin = icr.anchorMax = new Vector2(0.5f, 0.5f);
            icr.anchoredPosition = new Vector2(-size.x * 0.5f + 16f + iconW * 0.5f, 1f);
            icr.sizeDelta = new Vector2(iconW, iconH);
            textLeftPad = 16f + iconW + 8f;
        }

        float labelW = Mathf.Max(40f, size.x - textLeftPad - 12f);
        Vector2 labelPos = new Vector2((textLeftPad - 12f) * 0.5f, 2f);
        TMP_Text keyText = CreateLabel("Label", rect, labelPos, new Vector2(labelW, size.y), label.Length >= 4 ? 24f : 30f);
        keyText.text = label;
        keyText.fontStyle = FontStyles.Bold;
        keyText.color = new Color(0.035f, 0.055f, 0.09f);

        return new KeyVisual { rect = rect, image = key.GetComponent<Image>(), basePosition = position };
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
        if (tutorialText == null) return;
        tutorialRect.sizeDelta = new Vector2(410f, twoPlayer ? 120f : 92f);
        tutorialText.text = twoPlayer
            ? message + "\n<size=24><color=#FFCC66>1P " + (p1Complete ? "OK" : "--")
                + "</color>　<color=#73D9FF>2P " + (p2Complete ? "OK" : "--") + "</color></size>"
            : message;
    }

    private async Task ShowTutorialStep(string message, bool dash)
    {
        List<KeyVisual> keys = dash ? dashKeys : moveKeys;
        moveIconRoot.gameObject.SetActive(!dash);
        dashIconRoot.gameObject.SetActive(dash);
        CanvasGroup icons = dash ? dashIconGroup : moveIconGroup;

        // ダッシュステップは無敵注記を出す分、本文を少し上げて重なりを避ける。
        if (dashNote != null) dashNote.gameObject.SetActive(dash);
        tutorialRect.anchoredPosition = new Vector2(145f, cardBasePosition.y + (dash ? 26f : 0f));

        tutorialText.text = message;
        tutorialText.color = Color.white;

        cardGroup.alpha = 0f;
        cardRect.anchoredPosition = cardBasePosition;
        cardRect.localScale = Vector3.one;
        cardRect.localEulerAngles = Vector3.zero;
        tutorialText.alpha = 0f;
        tutorialRect.localScale = Vector3.one;
        tutorialRect.localEulerAngles = Vector3.zero;
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
            tutorialText.alpha = p;
            icons.alpha = iconFade;

            await Task.Yield();
            if (this == null) return;
        }

        cardGroup.alpha = 1f;
        cardRect.anchoredPosition = cardBasePosition;
        cardRect.localScale = Vector3.one;
        cardRect.localEulerAngles = Vector3.zero;
        tutorialText.alpha = 1f;
        tutorialRect.localScale = Vector3.one;
        tutorialRect.localEulerAngles = Vector3.zero;
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
            tutorialText.alpha = fade;
            (dash ? dashIconGroup : moveIconGroup).alpha = fade * fade;

            await Task.Yield();
            if (this == null) return;
        }
        cardGroup.alpha = 0f;
        tutorialText.alpha = 0f;
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

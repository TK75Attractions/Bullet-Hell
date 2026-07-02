using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TitleManager : MonoBehaviour
{
    [SerializeField] private float bpm = 128f;

    private CanvasGroup group;
    private TMP_Text promptText;
    private RectTransform logoRect;
    private float logoBaseY;
    private float animTime;
    private bool dismissed;
    private bool returnAnimating;
    private Image returnBackdrop;

    private float beatTimer;
    private float beatPulse;
    private Graphic[] shapeGraphics = new Graphic[0];
    private float[] shapeBaseAlphas = new float[0];

    // Big flat shapes drifting slowly behind the logo (Just Shapes & Beats vibe).
    private struct ShapeAnim
    {
        public RectTransform rect;
        public Vector2 basePos;
        public float phase;
        public float speedX;
        public float speedY;
        public float ampX;
        public float ampY;
        public float rotSpeed;
    }

    private ShapeAnim[] shapes = new ShapeAnim[0];

    // ---- Title menu + transfer panel (black / cyan / deep navy) -------------
    public enum TitleMenuAction { Start = 0, Options = 1, Transfer = 2 }

    private static readonly Color Cyan = new Color(0.219f, 0.761f, 0.878f);
    private static readonly Color CyanDim = new Color(0.11f, 0.34f, 0.40f);
    private static readonly Color Navy = new Color(0.03f, 0.05f, 0.11f, 1f);
    private static readonly Color NavyDeep = new Color(0.015f, 0.028f, 0.06f, 0.98f);
    private static readonly Color ErrorRed = new Color(0.96f, 0.46f, 0.52f);

    // Menu bars reuse the stage-select bar sprite and its color treatment so the
    // title menu matches the stage list: dim tinted bar + faint text when idle,
    // full bright bar + white text when selected (mirrors StageBox constants).
    private static readonly Color BarDim = new Color(0.42f, 0.55f, 0.72f);
    private static readonly Color BarSelected = Color.white;
    private static readonly Color MenuTextDim = new Color(0.78f, 0.88f, 1f);

    // How far above its scene-authored position the logo is lifted.
    private const float LogoRaiseOffset = 80f;

    private TMP_FontAsset uiFont;
    private RectTransform menuRoot;
    private TMP_Text[] menuItems = new TMP_Text[0];
    private RectTransform[] menuItemRects = new RectTransform[0];
    private Image[] menuBars = new Image[0];
    private Sprite barSprite;
    private float[] menuItemSel = new float[0];
    private int menuIndex;

    private GameObject transferRoot;
    private TMP_Text transferCodeText;
    private TMP_InputField transferInput;
    private TMP_Text transferMessageText;
    private Image applyButton;
    private bool transferOpen;

    public int MenuIndex => menuIndex;
    public TitleMenuAction CurrentAction => (TitleMenuAction)menuIndex;
    public bool IsTransferOpen => transferOpen;
    public bool IsTransferInputFocused => transferInput != null && transferInput.isFocused;

    public void Init()
    {
        animTime = 0f;
        beatTimer = 0f;
        beatPulse = 0f;
        returnAnimating = false;
        if (returnBackdrop != null) returnBackdrop.gameObject.SetActive(false);
        group = GetComponent<CanvasGroup>();
        Transform prompt = transform.Find("Prompt");
        if (prompt != null) promptText = prompt.GetComponent<TMP_Text>();
        Transform logo = transform.Find("Logo");
        if (logo != null)
        {
            logoRect = logo.GetComponent<RectTransform>();
            // Lift the logo above its scene-authored spot (float / beat pulse are
            // applied on top of this raised base position).
            logoBaseY = logoRect.anchoredPosition.y + LogoRaiseOffset;
            logoRect.anchoredPosition = new Vector2(logoRect.anchoredPosition.x, logoBaseY);
        }

        Transform shapesRoot = transform.Find("Shapes");
        if (shapesRoot != null)
        {
            shapes = new ShapeAnim[shapesRoot.childCount];
            shapeGraphics = new Graphic[shapesRoot.childCount];
            shapeBaseAlphas = new float[shapesRoot.childCount];
            for (int i = 0; i < shapesRoot.childCount; i++)
            {
                RectTransform rect = shapesRoot.GetChild(i) as RectTransform;
                shapeGraphics[i] = rect.GetComponent<Graphic>();
                shapeBaseAlphas[i] = shapeGraphics[i] != null ? shapeGraphics[i].color.a : 1f;
                shapes[i] = new ShapeAnim
                {
                    rect = rect,
                    basePos = rect.anchoredPosition,
                    phase = i * 1.7f,
                    speedX = 0.22f + 0.07f * (i % 3),
                    speedY = 0.17f + 0.06f * ((i + 1) % 4),
                    ampX = 70f + 30f * (i % 3),
                    ampY = 50f + 25f * ((i + 2) % 3),
                    rotSpeed = (i % 2 == 0 ? 1f : -1f) * (4f + 3f * (i % 3)),
                };
            }
        }

        EnsureUiBuilt();

        group.alpha = 1f;
        transform.localScale = Vector3.one;
        dismissed = false;
        gameObject.SetActive(true);
    }

    private void EnsureUiBuilt()
    {
        if (uiFont == null)
        {
            uiFont = promptText != null ? promptText.font : TMP_Settings.defaultFontAsset;
        }
        if (menuRoot == null) BuildMenu();
        if (transferRoot == null) BuildTransferPanel();

        menuIndex = 0;
        transferOpen = false;
        for (int i = 0; i < menuItemSel.Length; i++) menuItemSel[i] = i == 0 ? 1f : 0f;
        if (transferRoot != null) transferRoot.SetActive(false);
        if (menuRoot != null) menuRoot.gameObject.SetActive(true);

        // The scene-authored "PRESS ANY BUTTON" prompt is replaced by the menu.
        if (promptText != null)
        {
            promptText.gameObject.SetActive(false);
            promptText = null;
        }
    }

    // Returning from the option screen: the title rushes toward the viewer,
    // overshoots slightly, then settles as the pixel cover clears.
    public void PrepareReturnEntrance()
    {
        EnsureReturnBackdrop();
        returnBackdrop.gameObject.SetActive(true);
        int titleSiblingIndex = transform.GetSiblingIndex();
        if (returnBackdrop.transform.GetSiblingIndex() > titleSiblingIndex)
        {
            returnBackdrop.transform.SetSiblingIndex(titleSiblingIndex);
        }
        returnAnimating = true;
        group.alpha = 1f;
        transform.localScale = Vector3.one * 0.78f;
        for (int i = 0; i < shapes.Length; i++)
        {
            if (shapes[i].rect != null) shapes[i].rect.anchoredPosition = shapes[i].basePos;
        }
        if (logoRect != null)
        {
            logoRect.localScale = Vector3.one;
            logoRect.anchoredPosition = new Vector2(logoRect.anchoredPosition.x, logoBaseY);
        }
        if (promptText != null) promptText.alpha = 0f;
    }

    public async void PlayReturnEntrance()
    {
        if (!returnAnimating) PrepareReturnEntrance();
        const float delay = 0.01f;
        float wait = 0f;
        while (wait < delay)
        {
            wait += Time.unscaledDeltaTime;
            await Task.Yield();
            if (this == null) return;
        }

        const float duration = 0.30f;
        float time = 0f;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(time / duration);
            float q = p - 1f;
            float easeOutBack = 1f + 2.2f * q * q * q + 1.2f * q * q;
            transform.localScale = Vector3.one * Mathf.LerpUnclamped(0.78f, 1f, easeOutBack);
            if (promptText != null) promptText.alpha = Mathf.Clamp01((p - 0.45f) / 0.4f);
            await Task.Yield();
            if (this == null || group == null) return;
        }
        if (logoRect != null)
        {
            logoRect.localScale = Vector3.one;
            logoRect.anchoredPosition = new Vector2(logoRect.anchoredPosition.x, logoBaseY);
        }
        transform.localScale = Vector3.one;
        group.alpha = 1f;
        animTime = 0f;
        beatTimer = 0f;
        beatPulse = 0f;
        returnAnimating = false;
        if (returnBackdrop != null) returnBackdrop.gameObject.SetActive(false);
    }

    private void EnsureReturnBackdrop()
    {
        if (returnBackdrop != null) return;

        Transform parent = transform.parent;
        Transform existing = parent != null ? parent.Find("TitleReturnBackdrop") : null;
        if (existing != null)
        {
            returnBackdrop = existing.GetComponent<Image>();
            if (returnBackdrop != null) return;
        }

        GameObject backdrop = new GameObject(
            "TitleReturnBackdrop",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform rect = backdrop.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;

        returnBackdrop = backdrop.GetComponent<Image>();
        returnBackdrop.color = new Color(0.018f, 0.02f, 0.035f, 1f);
        returnBackdrop.raycastTarget = false;
    }

    // Idle animation: prompt blinks, logo floats and bounces on the beat,
    // background shapes drift, spin and flash slightly in time with the BPM.
    public void UpdateTitle(float dt)
    {
        if (dismissed || returnAnimating) return;
        animTime += dt;

        beatTimer += dt;
        float beatInterval = 60f / Mathf.Max(1f, bpm);
        if (beatTimer >= beatInterval)
        {
            beatTimer -= beatInterval;
            beatPulse = 1f;
        }
        beatPulse = Mathf.Max(0f, beatPulse - dt * 5f);

        if (promptText != null)
        {
            promptText.alpha = 0.35f + 0.55f * (0.5f + 0.5f * Mathf.Sin(animTime * 3.5f));
        }
        if (logoRect != null)
        {
            logoRect.anchoredPosition = new Vector2(logoRect.anchoredPosition.x, logoBaseY + Mathf.Sin(animTime * 1.2f) * 10f);
            logoRect.localScale = Vector3.one * (1f + 0.035f * beatPulse);
        }
        for (int i = 0; i < shapeGraphics.Length; i++)
        {
            if (shapeGraphics[i] == null) continue;
            Color c = shapeGraphics[i].color;
            c.a = Mathf.Min(1f, shapeBaseAlphas[i] * (1f + 0.65f * beatPulse));
            shapeGraphics[i].color = c;
        }
        for (int i = 0; i < shapes.Length; i++)
        {
            ShapeAnim s = shapes[i];
            if (s.rect == null) continue;
            s.rect.anchoredPosition = s.basePos + new Vector2(
                (Mathf.Sin(animTime * s.speedX + s.phase) - Mathf.Sin(s.phase)) * s.ampX,
                (Mathf.Cos(animTime * s.speedY + s.phase * 1.3f) - Mathf.Cos(s.phase * 1.3f)) * s.ampY);
            s.rect.Rotate(0f, 0f, s.rotSpeed * dt);
        }
    }

    // Dive "through" the title when the player presses the button: the camera
    // rushes in (cubic ease-in zoom) and the screen blows past the viewer,
    // fading only near the end so the acceleration reads clearly.
    public async void Dismiss()
    {
        if (dismissed) return;
        dismissed = true;
        const float duration = 0.38f;
        float d = duration;
        while (d > 0f)
        {
            d -= Time.deltaTime;
            float t = 1f - Mathf.Clamp01(d / duration);
            transform.localScale = Vector3.one * (1f + 2.2f * t * t * t);
            float fade = Mathf.Clamp01((t - 0.4f) / 0.45f);
            group.alpha = 1f - fade * fade;
            await Task.Yield();
            if (this == null || group == null) return;
        }
        group.alpha = 0f;
        gameObject.SetActive(false);
    }

    // ---- Menu -------------------------------------------------------------

    public void ShowMenu()
    {
        if (menuRoot != null) menuRoot.gameObject.SetActive(true);
    }

    public void HideMenu()
    {
        if (menuRoot != null) menuRoot.gameObject.SetActive(false);
    }

    // Navigate + animate the vertical menu. Selection pulses with the beat and
    // brightens toward cyan; unselected items sit in a dim teal.
    public void UpdateMenu(float dt, bool up, bool down)
    {
        if (transferOpen || menuItems.Length == 0) return;

        if (up) menuIndex = Mathf.Max(0, menuIndex - 1);
        else if (down) menuIndex = Mathf.Min(menuItems.Length - 1, menuIndex + 1);

        for (int i = 0; i < menuItems.Length; i++)
        {
            bool selected = i == menuIndex;
            menuItemSel[i] = Mathf.Lerp(menuItemSel[i], selected ? 1f : 0f, 1f - Mathf.Exp(-16f * dt));
            float pulse = selected ? 1f + 0.05f * beatPulse : 1f;
            float scale = Mathf.Lerp(0.94f, 1.06f, menuItemSel[i]) * pulse;
            if (menuItemRects[i] != null) menuItemRects[i].localScale = Vector3.one * scale;
            if (menuBars[i] != null) menuBars[i].color = Color.Lerp(BarDim, BarSelected, menuItemSel[i]);
            if (menuItems[i] != null) menuItems[i].color = Color.Lerp(MenuTextDim, Color.white, menuItemSel[i]);
        }
    }

    private void BuildMenu()
    {
        GameObject rootObj = new GameObject("Menu", typeof(RectTransform));
        rootObj.layer = gameObject.layer;
        menuRoot = (RectTransform)rootObj.transform;
        menuRoot.SetParent(transform, false);
        menuRoot.anchorMin = menuRoot.anchorMax = new Vector2(0.5f, 0.5f);
        menuRoot.anchoredPosition = Vector2.zero;
        menuRoot.sizeDelta = new Vector2(700f, 500f);

        barSprite = FindStageBarSprite();

        string[] labels = { "スタート", "設定", "引き継ぎ" };
        float[] rowY = { -178f, -303f, -428f };
        Vector2 barSize = new Vector2(600f, 104f);
        menuItems = new TMP_Text[labels.Length];
        menuItemRects = new RectTransform[labels.Length];
        menuBars = new Image[labels.Length];
        menuItemSel = new float[labels.Length];
        for (int i = 0; i < labels.Length; i++)
        {
            // Each row is a container so the bar and its label scale together on
            // selection / beat, exactly like a stage-select box.
            RectTransform row = CreateRow("Item" + i, menuRoot, new Vector2(0f, rowY[i]), barSize);

            Image bar = CreatePanel("Bar", row, Vector2.zero, barSize, i == 0 ? BarSelected : BarDim);
            if (barSprite != null)
            {
                bar.sprite = barSprite;
                bar.type = Image.Type.Simple;
                bar.preserveAspect = false;
            }
            menuBars[i] = bar;

            TMP_Text item = CreateText("Label", row, Vector2.zero, barSize, 52f,
                i == 0 ? Color.white : MenuTextDim, TextAlignmentOptions.Center);
            item.text = labels[i];
            item.fontStyle = FontStyles.Bold;
            menuItems[i] = item;
            menuItemRects[i] = row;
            menuItemSel[i] = i == 0 ? 1f : 0f;
        }
    }

    // The stage-select boxes are created before the title during startup, so we
    // can borrow the exact bar sprite they use (Box prefab's "StageBar" image)
    // without touching the scene or hard-coding an asset path.
    private Sprite FindStageBarSprite()
    {
        StageBox[] boxes = transform.root.GetComponentsInChildren<StageBox>(true);
        foreach (StageBox box in boxes)
        {
            Image[] images = box.GetComponentsInChildren<Image>(true);
            foreach (Image img in images)
            {
                if (img != null && img.sprite != null && img.gameObject.name == "StageBar")
                    return img.sprite;
            }
        }
        // Fallback: any sprited image on a box.
        foreach (StageBox box in boxes)
        {
            Image[] images = box.GetComponentsInChildren<Image>(true);
            foreach (Image img in images)
            {
                if (img != null && img.sprite != null) return img.sprite;
            }
        }
        return null;
    }

    private RectTransform CreateRow(string objectName, Transform parent, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.layer = gameObject.layer;
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        return rect;
    }

    // ---- Transfer panel ---------------------------------------------------

    public void OpenTransfer()
    {
        if (transferRoot == null) return;
        transferOpen = true;
        if (menuRoot != null) menuRoot.gameObject.SetActive(false);
        transferRoot.SetActive(true);
        transferRoot.transform.SetAsLastSibling();
        RefreshTransferCode();
        if (transferMessageText != null) transferMessageText.text = string.Empty;
        if (transferInput != null)
        {
            transferInput.text = string.Empty;
            transferInput.ActivateInputField();
        }
    }

    public void CloseTransfer()
    {
        transferOpen = false;
        if (transferInput != null) transferInput.DeactivateInputField();
        if (transferRoot != null) transferRoot.SetActive(false);
        if (menuRoot != null) menuRoot.gameObject.SetActive(true);
    }

    public void ApplyTransfer()
    {
        if (transferInput == null) return;
        if (PlayHistory.TryImportCode(transferInput.text, out string error))
        {
            if (transferMessageText != null)
            {
                transferMessageText.color = Cyan;
                transferMessageText.text =
                    $"引き継ぎました(プレイ {PlayHistory.TotalPlays} 回 / クリア {PlayHistory.TotalClears} 回)";
            }
            RefreshTransferCode();
            transferInput.text = string.Empty;
        }
        else
        {
            if (transferMessageText != null)
            {
                transferMessageText.color = ErrorRed;
                transferMessageText.text = error;
            }
        }
    }

    private void RefreshTransferCode()
    {
        if (transferCodeText == null) return;
        transferCodeText.text = PlayHistory.HasHistory
            ? PlayHistory.ExportCode()
            : "まだプレイ履歴がありません";
    }

    private void BuildTransferPanel()
    {
        GameObject rootObj = new GameObject("TransferPanel", typeof(RectTransform));
        rootObj.layer = gameObject.layer;
        transferRoot = rootObj;
        RectTransform rootRect = (RectTransform)rootObj.transform;
        rootRect.SetParent(transform, false);
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        CreatePanel("Scrim", rootRect, Vector2.zero, new Vector2(4000f, 4000f), new Color(0f, 0f, 0f, 0.86f));
        CreatePanel("Panel", rootRect, Vector2.zero, new Vector2(1120f, 760f), NavyDeep);
        CreatePanel("PanelEdge", rootRect, new Vector2(0f, 322f), new Vector2(1120f, 8f), Cyan);

        CreateText("Heading", rootRect, new Vector2(0f, 268f), new Vector2(1000f, 90f), 60f, Cyan, TextAlignmentOptions.Center)
            .fontStyle = FontStyles.Bold;
        CreateText("CodeLabel", rootRect, new Vector2(0f, 168f), new Vector2(1000f, 60f), 34f, CyanDim, TextAlignmentOptions.Center);

        transferCodeText = CreateText("Code", rootRect, new Vector2(0f, 78f), new Vector2(1040f, 110f), 70f, Cyan, TextAlignmentOptions.Center);
        transferCodeText.fontStyle = FontStyles.Bold;
        transferCodeText.characterSpacing = 6f;

        CreatePanel("Divider", rootRect, new Vector2(0f, 2f), new Vector2(920f, 3f), new Color(0.12f, 0.28f, 0.34f, 1f));
        CreateText("InputLabel", rootRect, new Vector2(0f, -66f), new Vector2(1000f, 60f), 34f, CyanDim, TextAlignmentOptions.Center);

        BuildInputField(rootRect, new Vector2(0f, -158f), new Vector2(880f, 104f));

        applyButton = CreatePanel("ApplyButton", rootRect, new Vector2(0f, -282f), new Vector2(300f, 84f), Cyan);
        CreateText("ApplyLabel", applyButton.rectTransform, Vector2.zero, new Vector2(300f, 84f), 38f, new Color(0.02f, 0.05f, 0.08f), TextAlignmentOptions.Center)
            .fontStyle = FontStyles.Bold;

        transferMessageText = CreateText("Message", rootRect, new Vector2(0f, -352f), new Vector2(1040f, 56f), 32f, Cyan, TextAlignmentOptions.Center);

        // Fill the label texts.
        SetChildText(rootRect, "Heading", "引き継ぎ");
        SetChildText(rootRect, "CodeLabel", "あなたの引き継ぎコード");
        SetChildText(rootRect, "InputLabel", "コードを入力");
        SetChildText(applyButton.rectTransform, "ApplyLabel", "適用");
        transferMessageText.text = string.Empty;

        transferRoot.SetActive(false);
    }

    private void BuildInputField(RectTransform parent, Vector2 pos, Vector2 size)
    {
        GameObject fieldObj = new GameObject("CodeInput", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TMP_InputField));
        fieldObj.layer = gameObject.layer;
        RectTransform fieldRect = (RectTransform)fieldObj.transform;
        fieldRect.SetParent(parent, false);
        fieldRect.anchorMin = fieldRect.anchorMax = new Vector2(0.5f, 0.5f);
        fieldRect.anchoredPosition = pos;
        fieldRect.sizeDelta = size;
        Image bg = fieldObj.GetComponent<Image>();
        bg.color = Navy;

        GameObject areaObj = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
        areaObj.layer = gameObject.layer;
        RectTransform areaRect = (RectTransform)areaObj.transform;
        areaRect.SetParent(fieldRect, false);
        areaRect.anchorMin = Vector2.zero;
        areaRect.anchorMax = Vector2.one;
        areaRect.offsetMin = new Vector2(24f, 8f);
        areaRect.offsetMax = new Vector2(-24f, -8f);

        TMP_Text placeholder = CreateText("Placeholder", areaRect, Vector2.zero, size, 46f, new Color(0.28f, 0.44f, 0.5f), TextAlignmentOptions.Center);
        StretchToParent(placeholder.rectTransform);
        placeholder.text = "XXXX-XXXX-XXXX-XXXX";

        TMP_Text textComp = CreateText("Text", areaRect, Vector2.zero, size, 46f, Cyan, TextAlignmentOptions.Center);
        StretchToParent(textComp.rectTransform);
        textComp.fontStyle = FontStyles.Bold;
        textComp.characterSpacing = 4f;

        transferInput = fieldObj.GetComponent<TMP_InputField>();
        transferInput.textViewport = areaRect;
        transferInput.textComponent = textComp;
        transferInput.placeholder = placeholder;
        transferInput.fontAsset = uiFont;
        transferInput.pointSize = 46f;
        transferInput.characterLimit = 19; // 16 symbols + 3 grouping hyphens
        transferInput.lineType = TMP_InputField.LineType.SingleLine;
        transferInput.richText = false;
        transferInput.onValidateInput += (string text, int pos, char ch) => char.ToUpperInvariant(ch);
        transferInput.onSubmit.AddListener(_ => ApplyTransfer());
    }

    // ---- UI helpers -------------------------------------------------------

    private TMP_Text CreateText(string objectName, Transform parent, Vector2 pos, Vector2 size, float fontSize, Color color, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.layer = gameObject.layer;
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        if (uiFont != null) label.font = uiFont;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = align;
        label.raycastTarget = false;
        return label;
    }

    private Image CreatePanel(string objectName, Transform parent, Vector2 pos, Vector2 size, Color color)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetChildText(Transform parent, string childName, string value)
    {
        Transform child = parent.Find(childName);
        if (child == null) return;
        TMP_Text text = child.GetComponent<TMP_Text>();
        if (text != null) text.text = value;
    }
}

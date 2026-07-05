using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.InputSystem;
using TMPro;

// Runtime-built "Just Shapes & Beats"-style horizontal carousel variation of the
// stage select. Everything here is created procedurally so the scene keeps no
// permanent JSAB objects. StageSelectManager owns the lifecycle and forwards the
// current stage / input to this overlay while the JSAB style is active.
public class JsabStageSelect : MonoBehaviour
{
    // Palette: cyan + black + navy. No heavy white use (per design).
    private static readonly Color Cyan = new Color(0.22f, 0.76f, 0.878f, 1f);       // #38C2E0
    private static readonly Color CyanDim = new Color(0.22f, 0.76f, 0.878f, 0.55f);
    private static readonly Color Navy = new Color(0.043f, 0.106f, 0.169f, 1f);      // #0B1B2B
    private static readonly Color NavyDeep = new Color(0.02f, 0.05f, 0.09f, 1f);
    private static readonly Color HeaderCyan = new Color(0.16f, 0.62f, 0.73f, 1f);
    private static readonly Color Ink = new Color(0.03f, 0.09f, 0.12f, 1f);

    private CanvasGroup rootCG;
    private TMP_FontAsset font;
    private Sprite playerSprite;

    // Center card
    private RectTransform cardRect;
    private RawImage cardVideo;
    private Image cardFallback;
    private TMP_Text cardFallbackName;
    private TMP_Text stageNameText;

    // Neighbours
    private TMP_Text leftName;
    private TMP_Text rightName;

    // Video
    private VideoPlayer videoPlayer;
    private RenderTexture videoRT;

    // State
    private int currentIndex = 0;
    private int totalStages = 1;
    private float pulseTime;
    private float slideTime = -1f;      // <0 == not animating
    private float slideFrom;            // starting x offset
    private const float slideDuration = 0.25f;
    private Vector2 cardBasePos;

    // --- In-screen difficulty overlay (built once, hidden until a stage is decided) ---
    private static readonly string[] DifficultyNames = { "EASY", "NORMAL", "LUNATIC" };
    private static readonly Color[] DifficultyTints =
    {
        new Color(0.42f, 0.85f, 0.55f, 1f),   // EASY: calm green
        new Color(0.22f, 0.76f, 0.878f, 1f),  // NORMAL: brand cyan
        new Color(0.95f, 0.36f, 0.55f, 1f),   // LUNATIC: hot magenta
    };
    private RectTransform diffRoot;      // container for blur + scrim + panel
    private RawImage diffBlur;           // frozen, downsampled snapshot of the carousel
    private Image diffScrim;             // dark wash for text contrast
    private RectTransform diffPanel;     // the buttons + title
    private readonly Image[] diffButtons = new Image[3];
    private readonly Image[] diffButtonBorders = new Image[3];
    private readonly TMP_Text[] diffButtonLabels = new TMP_Text[3];
    private static readonly Color SelectBorder = new Color(0.78f, 0.97f, 1f, 1f); // bright cyan-white
    private int diffIndex = 1;           // default NORMAL
    private bool difficultyOpen;
    private float diffOpenTime;           // unscaled time the modal opened (mouse debounce)
    private RenderTexture blurRT;
    private bool mouseConfirm;           // set when a difficulty button is left-clicked

    public bool DifficultyOpen => difficultyOpen;
    public int DifficultyIndex => diffIndex;

    // Returns (and clears) whether the mouse just clicked a difficulty button.
    public bool ConsumeMouseConfirm()
    {
        bool v = mouseConfirm;
        mouseConfirm = false;
        return v;
    }

    public bool Visible { get; private set; }

    public static JsabStageSelect Create(Transform parent, TMP_FontAsset font, Sprite playerSprite)
    {
        GameObject go = new GameObject("JsabStageSelectCanvas");
        if (parent != null) go.transform.SetParent(parent, false);

        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5; // above the (mostly 0/1) select canvases, opaque so it covers them

        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();

        JsabStageSelect jsab = go.AddComponent<JsabStageSelect>();
        jsab.font = font;
        jsab.playerSprite = playerSprite;
        jsab.rootCG = go.AddComponent<CanvasGroup>();
        jsab.rootCG.interactable = false;
        jsab.rootCG.blocksRaycasts = false;
        jsab.Build((RectTransform)go.transform);
        jsab.SetVisible(false);
        return jsab;
    }

    private void Build(RectTransform root)
    {
        // Opaque black backdrop covering the whole screen.
        Image bg = NewImage("Background", root, Color.black);
        Stretch(bg.rectTransform);

        // --- Header band ---
        Image header = NewImage("Header", root, HeaderCyan);
        RectTransform hr = header.rectTransform;
        hr.anchorMin = new Vector2(0f, 1f);
        hr.anchorMax = new Vector2(1f, 1f);
        hr.pivot = new Vector2(0.5f, 1f);
        hr.anchoredPosition = Vector2.zero;
        hr.sizeDelta = new Vector2(0f, 96f);
        TMP_Text headerText = NewText("HeaderText", header.rectTransform, "♪  ステージ選択", 48f, Ink, TextAlignmentOptions.Left);
        RectTransform htr = (RectTransform)headerText.transform;
        htr.anchorMin = new Vector2(0f, 0f);
        htr.anchorMax = new Vector2(1f, 1f);
        htr.offsetMin = new Vector2(60f, 0f);
        htr.offsetMax = new Vector2(-60f, 0f);
        // thin accent line under the header
        Image accent = NewImage("HeaderAccent", root, Cyan);
        RectTransform ar = accent.rectTransform;
        ar.anchorMin = new Vector2(0f, 1f);
        ar.anchorMax = new Vector2(1f, 1f);
        ar.pivot = new Vector2(0.5f, 1f);
        ar.anchoredPosition = new Vector2(0f, -96f);
        ar.sizeDelta = new Vector2(0f, 3f);

        // --- Neighbour cards (static, dim, partially off-screen) ---
        BuildNeighbour(root, -1);
        BuildNeighbour(root, 1);

        // --- Center card ---
        // Cyan border frame = a cyan rect slightly larger than the black card.
        Image border = NewImage("CardBorder", root, Cyan);
        cardRect = border.rectTransform;
        cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = new Vector2(0f, 70f);
        cardRect.sizeDelta = new Vector2(936f, 528f);
        cardBasePos = cardRect.anchoredPosition;

        Image cardBody = NewImage("CardBody", cardRect, NavyDeep);
        RectTransform cbr = cardBody.rectTransform;
        cbr.anchorMin = cbr.anchorMax = new Vector2(0.5f, 0.5f);
        cbr.pivot = new Vector2(0.5f, 0.5f);
        cbr.sizeDelta = new Vector2(924f, 516f);
        cbr.anchoredPosition = Vector2.zero;

        // Fallback (navy card + big name) shown when no video.
        cardFallback = NewImage("CardFallback", cbr, Navy);
        Stretch(cardFallback.rectTransform);
        cardFallbackName = NewText("CardFallbackName", cardFallback.rectTransform, "", 96f, Cyan, TextAlignmentOptions.Center);
        Stretch((RectTransform)cardFallbackName.transform);

        // Video surface.
        videoRT = new RenderTexture(768, 432, 0);
        videoRT.name = "JsabStageVideoRT";
        cardVideo = NewRawImage("CardVideo", cbr, videoRT);
        Stretch(cardVideo.rectTransform);

        GameObject vpGO = new GameObject("StageVideoPlayer");
        vpGO.transform.SetParent(cbr, false);
        videoPlayer = vpGO.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.source = VideoSource.Url;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = videoRT;
        videoPlayer.isLooping = true;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        videoPlayer.skipOnDrop = true;

        // Stage name above the card.
        stageNameText = NewText("StageName", root, "", 60f, Cyan, TextAlignmentOptions.Center);
        RectTransform snr = (RectTransform)stageNameText.transform;
        snr.anchorMin = snr.anchorMax = new Vector2(0.5f, 0.5f);
        snr.pivot = new Vector2(0.5f, 0.5f);
        snr.sizeDelta = new Vector2(1000f, 80f);
        snr.anchoredPosition = new Vector2(0f, 70f + 528f * 0.5f + 56f);

        // --- Progress indicator (player -> dashes -> ring) ---
        BuildProgressIndicator(root);

        // --- Bottom hint bar ---
        BuildHintBar(root);

        // --- In-screen difficulty overlay (hidden until a stage is decided) ---
        BuildDifficultyOverlay(root);
    }

    private void BuildNeighbour(RectTransform root, int side)
    {
        // side = -1 (left) or +1 (right)
        Image card = NewImage(side < 0 ? "LeftCard" : "RightCard", root, new Color(0.05f, 0.09f, 0.13f, 1f));
        RectTransform r = card.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(520f, 340f);
        // Sit just past the center card, mostly off-screen.
        float x = side * 820f;
        r.anchoredPosition = new Vector2(x, 70f);

        CanvasGroup cg = card.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0.35f;

        TMP_Text name = NewText(side < 0 ? "LeftName" : "RightName", r, "", 34f, CyanDim, TextAlignmentOptions.Center);
        RectTransform nr = (RectTransform)name.transform;
        nr.anchorMin = new Vector2(0f, 1f);
        nr.anchorMax = new Vector2(1f, 1f);
        nr.pivot = new Vector2(0.5f, 1f);
        nr.anchoredPosition = new Vector2(0f, 44f);
        nr.sizeDelta = new Vector2(0f, 44f);
        if (side < 0) leftName = name; else rightName = name;

        // Key hint centered on the neighbour card. The carousel is horizontal, so
        // left/right (A/D or arrows) move between stages.
        string hint = side < 0 ? "← / A" : "→ / D";
        TMP_Text keyHint = NewText(side < 0 ? "LeftHint" : "RightHint", r, hint, 30f, Cyan, TextAlignmentOptions.Center);
        RectTransform kr = (RectTransform)keyHint.transform;
        kr.anchorMin = kr.anchorMax = new Vector2(0.5f, 0.5f);
        kr.pivot = new Vector2(0.5f, 0.5f);
        kr.sizeDelta = new Vector2(300f, 40f);
        kr.anchoredPosition = new Vector2(0f, -40f);
    }

    private void BuildProgressIndicator(RectTransform root)
    {
        RectTransform row = new GameObject("Progress", typeof(RectTransform)).GetComponent<RectTransform>();
        row.SetParent(root, false);
        row.anchorMin = row.anchorMax = new Vector2(0.5f, 0.5f);
        row.pivot = new Vector2(0.5f, 0.5f);
        row.sizeDelta = new Vector2(360f, 80f);
        row.anchoredPosition = new Vector2(0f, 70f - 528f * 0.5f - 62f);

        // Player sprite (dot art, integer up-scaled).
        Image player = NewImage("Player", row, Color.white);
        if (playerSprite != null)
        {
            player.sprite = playerSprite;
            player.color = Color.white;
            player.preserveAspect = true;
            Rect sr = playerSprite.rect;
            // Dot-art players get a crisp integer up-scale; large illustrations are
            // fit to a small icon height so they read as a marker, not a portrait.
            const float targetHeight = 84f;
            float scale = sr.height <= 32f ? Mathf.Max(1f, Mathf.Floor(targetHeight / sr.height))
                                           : targetHeight / sr.height;
            player.rectTransform.sizeDelta = new Vector2(sr.width * scale, sr.height * scale);
        }
        else
        {
            player.color = Cyan;
            player.rectTransform.sizeDelta = new Vector2(44f, 44f);
        }
        player.rectTransform.anchorMin = player.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        player.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        player.rectTransform.anchoredPosition = new Vector2(-150f, 0f);

        // Dashed line = a row of small squares.
        int dashes = 6;
        for (int i = 0; i < dashes; i++)
        {
            Image dash = NewImage("Dash" + i, row, Cyan);
            dash.rectTransform.anchorMin = dash.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            dash.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            dash.rectTransform.sizeDelta = new Vector2(14f, 8f);
            dash.rectTransform.anchoredPosition = new Vector2(-96f + i * 30f, 0f);
        }

        // Goal ring.
        Image ring = NewImage("GoalRing", row, Cyan);
        ring.sprite = CreateRingSprite();
        ring.type = Image.Type.Simple;
        ring.rectTransform.anchorMin = ring.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        ring.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        ring.rectTransform.sizeDelta = new Vector2(40f, 40f);
        ring.rectTransform.anchoredPosition = new Vector2(150f, 0f);
    }

    private void BuildHintBar(RectTransform root)
    {
        Image bar = NewImage("HintBar", root, new Color(0.02f, 0.05f, 0.08f, 0.95f));
        RectTransform br = bar.rectTransform;
        br.anchorMin = new Vector2(0f, 0f);
        br.anchorMax = new Vector2(1f, 0f);
        br.pivot = new Vector2(0.5f, 0f);
        br.anchoredPosition = Vector2.zero;
        br.sizeDelta = new Vector2(0f, 76f);

        Image topLine = NewImage("HintBarLine", root, Cyan);
        RectTransform tlr = topLine.rectTransform;
        tlr.anchorMin = new Vector2(0f, 0f);
        tlr.anchorMax = new Vector2(1f, 0f);
        tlr.pivot = new Vector2(0.5f, 0f);
        tlr.anchoredPosition = new Vector2(0f, 76f);
        tlr.sizeDelta = new Vector2(0f, 2f);

        // Key-cap style hints (reference art): [chip] label, grouped and centered.
        BuildHintRow(br, 38f, new[]
        {
            new HintItem(new[] { "←", "→" }, "ステージ選択"),
            new HintItem(new[] { "SPACE" }, "決定"),
            new HintItem(new[] { "ESC" }, "戻る"),
            new HintItem(new[] { "V" }, "スタイル切替"),
        });
    }

    // ---- Key-cap hint rows (shared by the bottom bar and the difficulty overlay) ----

    private readonly struct HintItem
    {
        public readonly string[] Keys;
        public readonly string Label;
        public HintItem(string[] keys, string label) { Keys = keys; Label = label; }
    }

    // Lays out a centered horizontal row of "[key][key] label" groups using nested
    // layout groups so the whole row self-centers regardless of content width.
    private void BuildHintRow(RectTransform parent, float y, HintItem[] items)
    {
        RectTransform row = NewLayoutRow("HintRow", parent, 44f, 4f, TextAnchor.MiddleCenter);
        row.anchorMin = row.anchorMax = new Vector2(0.5f, 0.5f);
        row.pivot = new Vector2(0.5f, 0.5f);
        row.anchoredPosition = new Vector2(0f, y);
        HorizontalLayoutGroup outer = row.GetComponent<HorizontalLayoutGroup>();
        outer.spacing = 40f;

        foreach (HintItem item in items)
        {
            RectTransform group = NewLayoutRow("Hint_" + item.Label, row, 44f, 8f, TextAnchor.MiddleCenter);
            foreach (string key in item.Keys)
            {
                NewKeyCap(group, key, 40f, 26f);
            }
            TMP_Text label = NewText("Label", group, item.Label, 28f, Cyan, TextAlignmentOptions.Left);
            AddLayoutElement((RectTransform)label.transform, label.GetPreferredValues().x, 40f);
        }
    }

    // A dark, cyan-bordered key-cap chip with a centered label.
    private RectTransform NewKeyCap(RectTransform parent, string label, float height, float fontSize)
    {
        float width = Mathf.Max(height, 20f + label.Length * fontSize * 0.66f);
        Image border = NewImage("Key_" + label, parent, Cyan);
        RectTransform br = border.rectTransform;
        br.sizeDelta = new Vector2(width, height);
        AddLayoutElement(br, width, height);

        Image fill = NewImage("Fill", br, new Color(0.02f, 0.06f, 0.10f, 1f));
        RectTransform fr = fill.rectTransform;
        fr.anchorMin = Vector2.zero;
        fr.anchorMax = Vector2.one;
        fr.offsetMin = new Vector2(2f, 2f);
        fr.offsetMax = new Vector2(-2f, -2f);

        TMP_Text t = NewText("L", br, label, fontSize, Cyan, TextAlignmentOptions.Center);
        Stretch((RectTransform)t.transform);
        return br;
    }

    private RectTransform NewLayoutRow(string name, Transform parent, float height, float spacing, TextAnchor align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(0f, height);
        HorizontalLayoutGroup h = go.AddComponent<HorizontalLayoutGroup>();
        h.childAlignment = align;
        h.spacing = spacing;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;
        ContentSizeFitter fit = go.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        return rt;
    }

    private static void AddLayoutElement(RectTransform rt, float width, float height)
    {
        LayoutElement le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        le.minWidth = width;
        le.preferredHeight = height;
        le.minHeight = height;
    }

    // ---- In-screen difficulty overlay ----

    private void BuildDifficultyOverlay(RectTransform root)
    {
        GameObject rootGO = new GameObject("DifficultyOverlay", typeof(RectTransform));
        rootGO.transform.SetParent(root, false);
        diffRoot = (RectTransform)rootGO.transform;
        Stretch(diffRoot);

        // Frozen, blurred snapshot of the carousel behind the modal.
        diffBlur = NewRawImage("DiffBlur", diffRoot, null);
        Stretch(diffBlur.rectTransform);
        diffBlur.color = new Color(0.55f, 0.62f, 0.72f, 1f); // slight dim on the snapshot

        // Dark wash for text contrast.
        diffScrim = NewImage("DiffScrim", diffRoot, new Color(0.01f, 0.03f, 0.06f, 0.55f));
        Stretch(diffScrim.rectTransform);

        // Centered panel: a cyan border rectangle with an inset navy body. The body
        // is the first child (renders just above the cyan panel) so the title,
        // buttons and hints added afterwards sit on the navy body, not the border.
        Image panel = NewImage("DiffPanel", diffRoot, Cyan);
        diffPanel = panel.rectTransform;
        diffPanel.anchorMin = diffPanel.anchorMax = new Vector2(0.5f, 0.5f);
        diffPanel.pivot = new Vector2(0.5f, 0.5f);
        diffPanel.sizeDelta = new Vector2(880f, 420f);
        diffPanel.anchoredPosition = new Vector2(0f, 30f);

        Image body = NewImage("DiffPanelBody", diffPanel, new Color(0.03f, 0.08f, 0.13f, 0.99f));
        RectTransform bodyR = body.rectTransform;
        bodyR.anchorMin = Vector2.zero;
        bodyR.anchorMax = Vector2.one;
        bodyR.offsetMin = new Vector2(3f, 3f);
        bodyR.offsetMax = new Vector2(-3f, -3f);

        TMP_Text title = NewText("DiffTitle", diffPanel, "難易度を選択", 48f, Cyan, TextAlignmentOptions.Center);
        RectTransform tr = (RectTransform)title.transform;
        tr.anchorMin = new Vector2(0f, 1f);
        tr.anchorMax = new Vector2(1f, 1f);
        tr.pivot = new Vector2(0.5f, 1f);
        tr.sizeDelta = new Vector2(0f, 70f);
        tr.anchoredPosition = new Vector2(0f, -36f);

        // Three difficulty buttons in a centered row.
        const float bw = 240f;
        const float bh = 150f;
        const float bgap = 30f;
        float startX = -(bw + bgap);
        for (int i = 0; i < 3; i++)
        {
            Vector2 pos = new Vector2(startX + i * (bw + bgap), -30f);

            // Selection outline sits behind the button fill (added first) so it reads
            // as a bright border, shown only for the highlighted difficulty.
            Image border = NewImage("DiffBtnSel_" + DifficultyNames[i], diffPanel, SelectBorder);
            RectTransform bdr = border.rectTransform;
            bdr.anchorMin = bdr.anchorMax = new Vector2(0.5f, 0.5f);
            bdr.pivot = new Vector2(0.5f, 0.5f);
            bdr.sizeDelta = new Vector2(bw + 10f, bh + 10f);
            bdr.anchoredPosition = pos;
            diffButtonBorders[i] = border;

            Image btn = NewImage("DiffBtn_" + DifficultyNames[i], diffPanel, Navy);
            RectTransform btr = btn.rectTransform;
            btr.anchorMin = btr.anchorMax = new Vector2(0.5f, 0.5f);
            btr.pivot = new Vector2(0.5f, 0.5f);
            btr.sizeDelta = new Vector2(bw, bh);
            btr.anchoredPosition = pos;
            diffButtons[i] = btn;

            TMP_Text lbl = NewText("Lbl", btr, DifficultyNames[i], 40f, Cyan, TextAlignmentOptions.Center);
            Stretch((RectTransform)lbl.transform);
            diffButtonLabels[i] = lbl;
        }

        // Bottom hints inside the panel.
        BuildHintRow(diffPanel, -152f, new[]
        {
            new HintItem(new[] { "←", "→" }, "選択"),
            new HintItem(new[] { "SPACE" }, "決定"),
            new HintItem(new[] { "ESC" }, "戻る"),
        });

        diffRoot.gameObject.SetActive(false);
        RefreshDifficultyVisual();
    }

    // ---- Public difficulty API (driven by StageSelectManager) ----

    public void OpenDifficulty()
    {
        if (diffRoot == null) return;
        diffIndex = 1; // default NORMAL each time it opens
        difficultyOpen = true;
        diffOpenTime = Time.unscaledTime;
        mouseConfirm = false;
        diffRoot.gameObject.SetActive(true);
        RefreshDifficultyVisual();
        StartCoroutine(CaptureBlurBackground());
    }

    public void CloseDifficulty()
    {
        difficultyOpen = false;
        if (diffRoot != null) diffRoot.gameObject.SetActive(false);
    }

    public void MoveDifficulty(int dir)
    {
        if (!difficultyOpen) return;
        diffIndex = Mathf.Clamp(diffIndex + (dir > 0 ? 1 : -1), 0, DifficultyNames.Length - 1);
        RefreshDifficultyVisual();
    }

    private void RefreshDifficultyVisual()
    {
        for (int i = 0; i < diffButtons.Length; i++)
        {
            if (diffButtons[i] == null) continue;
            bool sel = i == diffIndex;
            Color tint = DifficultyTints[i];
            diffButtons[i].color = sel ? tint : new Color(0.04f, 0.10f, 0.15f, 1f);
            diffButtons[i].rectTransform.localScale = Vector3.one * (sel ? 1.06f : 0.94f);
            if (diffButtonBorders[i] != null)
            {
                diffButtonBorders[i].enabled = sel;
                diffButtonBorders[i].rectTransform.localScale = diffButtons[i].rectTransform.localScale;
            }
            if (diffButtonLabels[i] != null)
                diffButtonLabels[i].color = sel ? Ink : tint;
        }
    }

    // Freezes the current screen into a downsampled (thus blurred) snapshot behind
    // the modal. Captured with our own overlay children hidden so only the carousel
    // shows through.
    private IEnumerator CaptureBlurBackground()
    {
        if (diffBlur != null) diffBlur.enabled = false;
        if (diffScrim != null) diffScrim.enabled = false;
        if (diffPanel != null) diffPanel.gameObject.SetActive(false);

        yield return new WaitForEndOfFrame();

        Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
        int w = Mathf.Max(24, shot.width / 12);
        int h = Mathf.Max(14, shot.height / 12);
        if (blurRT != null) { blurRT.Release(); Destroy(blurRT); }
        blurRT = new RenderTexture(w, h, 0) { filterMode = FilterMode.Bilinear };
        // Two-step downsample smooths the box edges into a softer blur.
        RenderTexture mid = RenderTexture.GetTemporary(shot.width / 4, shot.height / 4, 0);
        mid.filterMode = FilterMode.Bilinear;
        Graphics.Blit(shot, mid);
        Graphics.Blit(mid, blurRT);
        RenderTexture.ReleaseTemporary(mid);
        Destroy(shot);

        if (diffBlur != null)
        {
            diffBlur.texture = blurRT;
            diffBlur.enabled = true;
        }
        if (diffScrim != null) diffScrim.enabled = true;
        if (diffPanel != null) diffPanel.gameObject.SetActive(true);
    }

    // ---- Public API used by StageSelectManager ----

    public void SetVisible(bool visible)
    {
        Visible = visible;
        if (rootCG != null)
        {
            rootCG.alpha = visible ? 1f : 0f;
            rootCG.blocksRaycasts = false;
        }
        gameObject.SetActive(true); // keep active so Tick/video run; alpha hides it
        if (visible)
        {
            if (videoPlayer != null && !string.IsNullOrEmpty(videoPlayer.url) && !videoPlayer.isPlaying)
            {
                videoPlayer.Play();
            }
        }
        else
        {
            if (videoPlayer != null && videoPlayer.isPlaying) videoPlayer.Pause();
        }
    }

    public void SetStage(int index, int total, bool animate)
    {
        totalStages = Mathf.Max(1, total);
        int newIndex = Mathf.Clamp(index, 0, totalStages - 1);
        int dir = newIndex == currentIndex ? 0 : (newIndex > currentIndex ? 1 : -1);
        currentIndex = newIndex;

        StageData cur = GetStage(currentIndex);
        string curName = cur != null && !string.IsNullOrWhiteSpace(cur.stageName) ? cur.stageName : ("Stage " + currentIndex);
        // Japanese stage names ride high under Middle alignment (Latin UI font +
        // CJK fallback metrics); optically center each by its ink bounds.
        if (stageNameText != null) { stageNameText.text = curName; TmpAlign.CenterInkVertically(stageNameText); }
        if (cardFallbackName != null) { cardFallbackName.text = curName; TmpAlign.CenterInkVertically(cardFallbackName); }

        StageData left = GetStage(currentIndex - 1);
        StageData right = GetStage(currentIndex + 1);
        if (leftName != null) { leftName.text = left != null ? SafeName(left) : ""; TmpAlign.CenterInkVertically(leftName); }
        if (rightName != null) { rightName.text = right != null ? SafeName(right) : ""; TmpAlign.CenterInkVertically(rightName); }

        UpdateVideo(cur);

        if (animate && dir != 0)
        {
            // Slide the card in from the direction we moved.
            slideFrom = dir > 0 ? 260f : -260f;
            slideTime = 0f;
        }
        else
        {
            slideTime = -1f;
            if (cardRect != null) cardRect.anchoredPosition = cardBasePos;
        }
    }

    public void Tick(float dt)
    {
        if (!Visible) return;

        // While the difficulty modal is open, the mouse can hover (to select) and
        // click (to confirm) any of the three buttons. Keyboard is driven by the
        // StageSelectManager; here we only translate pointer position/click.
        // Ignore the pointer for a beat after opening so the interaction that opened
        // the modal (or a focus click on the game view) cannot immediately confirm.
        if (difficultyOpen && Time.unscaledTime - diffOpenTime >= 0.2f)
        {
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 mp = mouse.position.ReadValue();
                int hover = -1;
                for (int i = 0; i < diffButtons.Length; i++)
                {
                    if (diffButtons[i] == null) continue;
                    if (RectTransformUtility.RectangleContainsScreenPoint(diffButtons[i].rectTransform, mp, null))
                    {
                        hover = i;
                        break;
                    }
                }
                if (hover >= 0 && hover != diffIndex)
                {
                    diffIndex = hover;
                    RefreshDifficultyVisual();
                }
                if (hover >= 0 && mouse.leftButton.wasPressedThisFrame)
                {
                    mouseConfirm = true;
                }
            }
        }

        pulseTime += dt;
        // Breathing pulse on the selected card (matches StageBox.SetPulse feel).
        float pulse = 1f + 0.02f * (0.5f + 0.5f * Mathf.Sin(pulseTime * 3f));

        if (slideTime >= 0f && cardRect != null)
        {
            slideTime += dt;
            float p = Mathf.Clamp01(slideTime / slideDuration);
            float ease = 1f - Mathf.Pow(1f - p, 3f);
            float x = Mathf.Lerp(slideFrom, 0f, ease);
            cardRect.anchoredPosition = cardBasePos + new Vector2(x, 0f);
            if (rootCG != null) rootCG.alpha = Mathf.Lerp(0.35f, 1f, ease);
            if (p >= 1f)
            {
                slideTime = -1f;
                cardRect.anchoredPosition = cardBasePos;
                if (rootCG != null) rootCG.alpha = 1f;
            }
        }

        if (cardRect != null) cardRect.localScale = Vector3.one * pulse;
    }

    // ---- internals ----

    private void UpdateVideo(StageData data)
    {
        string dir = data != null ? data.stageDirectoryName : null;
        string path = null;
        if (!string.IsNullOrEmpty(dir))
        {
            path = Path.Combine(Application.dataPath, "StageData", dir, dir + ".mp4");
        }

        bool hasVideo = !string.IsNullOrEmpty(path) && File.Exists(path);
        if (hasVideo)
        {
            if (cardVideo != null) cardVideo.enabled = true;
            if (cardFallback != null) cardFallback.gameObject.SetActive(false);
            if (videoPlayer != null)
            {
                videoPlayer.url = path;
                videoPlayer.Play();
            }
        }
        else
        {
            if (cardVideo != null) cardVideo.enabled = false;
            if (cardFallback != null) cardFallback.gameObject.SetActive(true);
            if (videoPlayer != null)
            {
                videoPlayer.Stop();
                videoPlayer.url = null;
            }
        }
    }

    private StageData GetStage(int index)
    {
        if (index < 0 || index >= totalStages) return null;
        return GManager.Control != null && GManager.Control.SDB != null ? GManager.Control.SDB.GetStage(index) : null;
    }

    private static string SafeName(StageData d)
    {
        return d != null && !string.IsNullOrWhiteSpace(d.stageName) ? d.stageName : "";
    }

    private Image NewImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private RawImage NewRawImage(string name, Transform parent, Texture tex)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RawImage img = go.AddComponent<RawImage>();
        img.texture = tex;
        img.raycastTarget = false;
        return img;
    }

    private TMP_Text NewText(string name, Transform parent, string content, float size, Color color, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    private static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    private Sprite CreateRingSprite()
    {
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Vector2 c = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float outer = size * 0.46f;
        float inner = size * 0.30f;
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c);
                float a = 0f;
                if (d <= outer && d >= inner)
                {
                    // soft edges
                    float edge = Mathf.Min(outer - d, d - inner);
                    a = Mathf.Clamp01(edge);
                }
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private void OnDestroy()
    {
        if (videoRT != null)
        {
            videoRT.Release();
            Destroy(videoRT);
        }
        if (blurRT != null)
        {
            blurRT.Release();
            Destroy(blurRT);
        }
    }
}

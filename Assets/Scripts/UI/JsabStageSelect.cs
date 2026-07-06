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

    private CanvasGroup rootCG;
    private TMP_FontAsset font;
    private Sprite playerSprite;

    // Center card
    private RectTransform cardRect;
    private RawImage cardVideo;
    private Image cardFallback;
    private TMP_Text cardFallbackName;
    private TMP_Text stageNameText;

    // Neighbours (dim thumbnail previews of the adjacent stages)
    private TMP_Text leftName;
    private TMP_Text rightName;
    private RawImage leftThumb;
    private RawImage rightThumb;
    private Image leftThumbFallback;
    private Image rightThumbFallback;
    private VideoPlayer leftVP;
    private VideoPlayer rightVP;
    private RenderTexture leftRT;
    private RenderTexture rightRT;

    // Page indicator (rebuilt when the stage count is known)
    private RectTransform progressRow;
    // Persistent player marker on the indicator; tweens between nodes.
    private RectTransform markerRect;
    private float markerToX;
    private float markerFromX;
    private float markerTweenTime = -1f;    // <0 == not animating
    private const float markerTweenDuration = 0.25f;

    // Cloned style-0 top bar pieces that must mirror the live originals
    // (StageSelectManager keeps updating them even while alpha-hidden).
    private TMP_Text topBarTimerText;
    private RectTransform topBarTimeDim;
    private TMP_Text origTimerText;
    private RectTransform origTimeDim;

    // Carousel band that slides together on stage change.
    private RectTransform stageNameRect;
    private Vector2 stageNameBasePos;
    private RectTransform leftCardRect;
    private Vector2 leftCardBasePos;
    private RectTransform rightCardRect;
    private Vector2 rightCardBasePos;

    // Vertical divider color between the center and side columns.
    private static readonly Color Divider = new Color(0.22f, 0.76f, 0.878f, 0.4f);

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
    private RectTransform diffRoot;      // container for blur + scrim + panel
    private RawImage diffBlur;           // frozen, downsampled snapshot of the carousel
    private Image diffScrim;             // dark wash for text contrast
    private RectTransform diffPanel;     // cloned style-0 difficulty column
    private DefficultyBar diffBar;       // the cloned component (style-0 look & animation)
    private readonly RectTransform[] diffBoxRects = new RectTransform[3]; // Easy/Normal/Lunatic bar rects (mouse hit areas)
    private bool difficultyOpen;
    private float diffOpenTime;           // unscaled time the modal opened (mouse debounce)
    private RenderTexture blurRT;
    private bool mouseConfirm;           // set when a difficulty button is left-clicked

    public bool DifficultyOpen => difficultyOpen;
    public int DifficultyIndex => diffBar != null ? diffBar.index : 1;

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

        // --- Top bar: clone of the default (style 0) header so both styles share
        // the exact same design. The timer text and the red time-dim panel mirror
        // the live originals every frame in Tick.
        CloneTopBar(root);

        // --- Thin vertical dividers between the center column and the side columns ---
        BuildDivider(root, -480f);
        BuildDivider(root, 480f);

        // --- Neighbour cards (dim thumbnail previews, fully on-screen) ---
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
        // The RenderTexture keeps the previous stage's frame while the next clip
        // prepares; visuals only swap once the new clip is ready (no flash).
        videoPlayer.prepareCompleted += OnMainVideoPrepared;

        // Stage name above the card.
        stageNameText = NewText("StageName", root, "", 60f, Cyan, TextAlignmentOptions.Center);
        RectTransform snr = (RectTransform)stageNameText.transform;
        snr.anchorMin = snr.anchorMax = new Vector2(0.5f, 0.5f);
        snr.pivot = new Vector2(0.5f, 0.5f);
        // +46f keeps the name clear of the 120px-tall top bar (bottom edge y=420).
        snr.sizeDelta = new Vector2(1000f, 80f);
        snr.anchoredPosition = new Vector2(0f, 70f + 528f * 0.5f + 46f);
        stageNameRect = snr;
        stageNameBasePos = snr.anchoredPosition;

        // --- Progress indicator (player -> dashes -> ring) ---
        BuildProgressIndicator(root);

        // --- Bottom hint bar ---
        BuildHintBar(root);

        // --- In-screen difficulty overlay (hidden until a stage is decided) ---
        BuildDifficultyOverlay(root);
    }

    // Clones the two scene "Head" subtrees (base graphics from StaticCanvas, text
    // layer from StageCanvas) into this canvas. The clones are passive visuals;
    // dynamic parts (timer text, TimeDim width) are mirrored from the originals.
    private void CloneTopBar(RectTransform root)
    {
        Transform canvases = transform.parent;
        if (canvases == null) return;

        Transform staticHead = canvases.Find("StaticCanvas/StageBoxParent/Head");
        if (staticHead != null)
        {
            GameObject clone = Instantiate(staticHead.gameObject, root);
            clone.name = "TopBarBase";
            clone.SetActive(true);
            CopyRect((RectTransform)clone.transform, (RectTransform)staticHead);
            topBarTimeDim = clone.transform.Find("TimeDim") as RectTransform;
            origTimeDim = staticHead.Find("TimeDim") as RectTransform;
        }

        Transform textHead = canvases.Find("StageCanvas/StageBoxParent/Head");
        if (textHead != null)
        {
            GameObject clone = Instantiate(textHead.gameObject, root);
            clone.name = "TopBarText";
            clone.SetActive(true);
            CopyRect((RectTransform)clone.transform, (RectTransform)textHead);
            // The clone must not react to state transitions; it is a static copy.
            Header dupHeader = clone.GetComponent<Header>();
            if (dupHeader != null) Destroy(dupHeader);
            Transform cloneTimer = clone.transform.Find("TimerText");
            topBarTimerText = cloneTimer != null ? cloneTimer.GetComponent<TMP_Text>() : null;
            Transform srcTimer = textHead.Find("TimerText");
            origTimerText = srcTimer != null ? srcTimer.GetComponent<TMP_Text>() : null;
        }
    }

    private static void CopyRect(RectTransform dst, RectTransform src)
    {
        dst.anchorMin = src.anchorMin;
        dst.anchorMax = src.anchorMax;
        dst.pivot = src.pivot;
        dst.anchoredPosition = src.anchoredPosition;
        dst.sizeDelta = src.sizeDelta;
        dst.localScale = src.localScale;
    }

    private void BuildDivider(RectTransform root, float x)
    {
        Image line = NewImage("Divider", root, Divider);
        RectTransform r = line.rectTransform;
        r.anchorMin = new Vector2(0.5f, 0f);
        r.anchorMax = new Vector2(0.5f, 1f);
        r.pivot = new Vector2(0.5f, 0.5f);
        // Span between the top bar (120px) and the bottom hint bar.
        r.offsetMin = new Vector2(x - 1f, 78f);
        r.offsetMax = new Vector2(x + 1f, -120f);
        r.sizeDelta = new Vector2(2f, r.sizeDelta.y);
    }

    private void BuildNeighbour(RectTransform root, int side)
    {
        // side = -1 (left) or +1 (right). A dim thumbnail preview of the adjacent
        // stage with the stage name above and a key chip in the corner.
        const float cw = 452f;
        const float ch = 300f;
        float x = side * 712f;

        // Thin cyan frame around the thumbnail.
        Image frame = NewImage(side < 0 ? "LeftCard" : "RightCard", root, CyanDim);
        RectTransform r = frame.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(cw + 4f, ch + 4f);
        r.anchoredPosition = new Vector2(x, 40f);

        Image body = NewImage("ThumbBody", r, new Color(0.02f, 0.05f, 0.08f, 1f));
        RectTransform bodyR = body.rectTransform;
        bodyR.anchorMin = Vector2.zero;
        bodyR.anchorMax = Vector2.one;
        bodyR.offsetMin = new Vector2(2f, 2f);
        bodyR.offsetMax = new Vector2(-2f, -2f);

        // Fallback tile (stage name on navy) when the stage has no preview video.
        Image fallback = NewImage("ThumbFallback", bodyR, new Color(0.04f, 0.09f, 0.14f, 1f));
        Stretch(fallback.rectTransform);
        TMP_Text fbName = NewText("ThumbFallbackName", fallback.rectTransform, "", 44f, CyanDim, TextAlignmentOptions.Center);
        Stretch((RectTransform)fbName.transform);

        // Dim thumbnail = the stage preview video rendered to a RenderTexture and
        // tinted down so the center card stays the focus.
        RenderTexture rt = new RenderTexture(384, 216, 0) { name = (side < 0 ? "JsabLeftRT" : "JsabRightRT") };
        RawImage thumb = NewRawImage("Thumb", bodyR, rt);
        Stretch(thumb.rectTransform);
        thumb.color = new Color(0.5f, 0.55f, 0.62f, 1f); // dim + slight desaturate feel
        thumb.enabled = false;

        // A subtle dark wash over the thumbnail so it reads as "dimmed / inactive".
        Image scrim = NewImage("ThumbScrim", bodyR, new Color(0.02f, 0.05f, 0.09f, 0.4f));
        Stretch(scrim.rectTransform);

        GameObject vpGO = new GameObject(side < 0 ? "LeftVideoPlayer" : "RightVideoPlayer");
        vpGO.transform.SetParent(bodyR, false);
        VideoPlayer vp = vpGO.AddComponent<VideoPlayer>();
        vp.playOnAwake = false;
        vp.source = VideoSource.Url;
        vp.renderMode = VideoRenderMode.RenderTexture;
        vp.targetTexture = rt;
        vp.isLooping = true;
        vp.waitForFirstFrame = true;
        vp.audioOutputMode = VideoAudioOutputMode.None;
        vp.skipOnDrop = true;
        // Show a still first frame instead of a busy looping clip.
        vp.prepareCompleted += v => { if (thumb != null) thumb.enabled = true; v.Pause(); };

        // Stage name above the thumbnail (dim, per reference).
        TMP_Text name = NewText(side < 0 ? "LeftName" : "RightName", r, "", 34f, CyanDim, TextAlignmentOptions.Center);
        RectTransform nr = (RectTransform)name.transform;
        nr.anchorMin = new Vector2(0f, 1f);
        nr.anchorMax = new Vector2(1f, 1f);
        nr.pivot = new Vector2(0.5f, 0f);
        nr.anchoredPosition = new Vector2(0f, 14f);
        nr.sizeDelta = new Vector2(0f, 48f);

        // Key chip in the top-inner corner (reference LB/RB placement).
        string keyLabel = side < 0 ? "← A" : "D →";
        RectTransform chip = NewKeyCap(r, keyLabel, 42f, 26f);
        chip.anchorMin = chip.anchorMax = new Vector2(side < 0 ? 0f : 1f, 1f);
        chip.pivot = new Vector2(side < 0 ? 0f : 1f, 1f);
        chip.anchoredPosition = new Vector2(side < 0 ? 12f : -12f, -12f);

        if (side < 0)
        {
            leftName = name; leftThumb = thumb; leftThumbFallback = fallback;
            leftVP = vp; leftRT = rt;
            leftCardRect = r; leftCardBasePos = r.anchoredPosition;
        }
        else
        {
            rightName = name; rightThumb = thumb; rightThumbFallback = fallback;
            rightVP = vp; rightRT = rt;
            rightCardRect = r; rightCardBasePos = r.anchoredPosition;
        }
    }

    private Sprite ringSprite;

    private void BuildProgressIndicator(RectTransform root)
    {
        // Container only; the dots are (re)built once the stage count is known so
        // the row can be centered under the card with one node per stage.
        progressRow = new GameObject("Progress", typeof(RectTransform)).GetComponent<RectTransform>();
        progressRow.SetParent(root, false);
        progressRow.anchorMin = progressRow.anchorMax = new Vector2(0.5f, 0.5f);
        progressRow.pivot = new Vector2(0.5f, 0.5f);
        progressRow.sizeDelta = new Vector2(0f, 60f);
        progressRow.anchoredPosition = new Vector2(0f, 70f - 528f * 0.5f - 60f);
        ringSprite = CreateRingSprite();

        // Persistent player marker; RefreshProgress never destroys it so its
        // position can tween smoothly between nodes.
        Image marker = NewImage("Marker", progressRow, Color.white);
        if (playerSprite != null)
        {
            marker.sprite = playerSprite;
            marker.preserveAspect = true;
            Rect sr = playerSprite.rect;
            float h = 40f;
            float scale = sr.height <= 32f ? Mathf.Max(1f, Mathf.Floor(h / sr.height)) : h / sr.height;
            marker.rectTransform.sizeDelta = new Vector2(sr.width * scale, sr.height * scale);
        }
        else
        {
            marker.color = Cyan;
            marker.rectTransform.sizeDelta = new Vector2(26f, 26f);
        }
        marker.rectTransform.anchorMin = marker.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        marker.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        markerRect = marker.rectTransform;
    }

    // Rebuilds the node/dash chain: hollow rings connected by dashes, one node per
    // stage. The current stage's ring is omitted; the persistent player marker
    // (tweened in Tick) sits/lands there instead.
    private void RefreshProgress(bool animate)
    {
        if (progressRow == null) return;
        for (int i = progressRow.childCount - 1; i >= 0; i--)
        {
            Transform child = progressRow.GetChild(i);
            if (markerRect != null && child == markerRect) continue;
            Destroy(child.gameObject);
        }

        int n = Mathf.Max(1, totalStages);
        const float nodeSize = 26f;
        const float dashGap = 66f;   // center-to-center spacing between nodes
        float totalW = (n - 1) * dashGap;
        float x0 = -totalW * 0.5f;

        for (int i = 0; i < n; i++)
        {
            float x = x0 + i * dashGap;

            // Dash segment to the next node (3 small ticks).
            if (i < n - 1)
            {
                for (int d = 0; d < 3; d++)
                {
                    Image dash = NewImage("Dash", progressRow, CyanDim);
                    dash.rectTransform.anchorMin = dash.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    dash.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    dash.rectTransform.sizeDelta = new Vector2(10f, 4f);
                    // 3 ticks evenly spread across the gap [x .. x+dashGap].
                    dash.rectTransform.anchoredPosition = new Vector2(x + dashGap * (d + 1) / 4f, 0f);
                }
            }

            if (i == currentIndex) continue; // marker lands here
            Image node = NewImage("Node", progressRow, Cyan);
            node.sprite = ringSprite;
            node.type = Image.Type.Simple;
            node.rectTransform.anchorMin = node.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            node.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            node.rectTransform.sizeDelta = new Vector2(nodeSize, nodeSize);
            node.rectTransform.anchoredPosition = new Vector2(x, 0f);
        }

        markerToX = x0 + currentIndex * dashGap;
        if (markerRect != null)
        {
            markerRect.SetAsLastSibling(); // draw above rings while sliding over them
            if (animate && !Mathf.Approximately(markerRect.anchoredPosition.x, markerToX))
            {
                markerFromX = markerRect.anchoredPosition.x;
                markerTweenTime = 0f;
            }
            else
            {
                markerTweenTime = -1f;
                markerRect.anchoredPosition = new Vector2(markerToX, 0f);
            }
        }
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

        // Reference layout: primary actions grouped on the left, "back" on the right.
        BuildHintRowAt(br, 0f, 0f, 48f, 0f, new[]
        {
            new HintItem(new[] { "←", "→" }, "選択"),
            new HintItem(new[] { "SPACE" }, "決定"),
            new HintItem(new[] { "V" }, "スタイル切替"),
        });
        BuildHintRowAt(br, 1f, 1f, -48f, 0f, new[]
        {
            new HintItem(new[] { "ESC" }, "戻る"),
        });
    }

    // ---- Key-cap hint rows (shared by the bottom bar and the difficulty overlay) ----

    private readonly struct HintItem
    {
        public readonly string[] Keys;
        public readonly string Label;
        public HintItem(string[] keys, string label) { Keys = keys; Label = label; }
    }

    // Lays out a horizontal row of "[key][key] label" groups using nested layout
    // groups so the row self-sizes regardless of content width.
    // anchorX/pivotX let the row hang from the left (0), center (0.5) or right (1).
    private void BuildHintRowAt(RectTransform parent, float anchorX, float pivotX, float x, float y, HintItem[] items)
    {
        RectTransform row = NewLayoutRow("HintRow", parent, 44f, 4f, TextAnchor.MiddleCenter);
        row.anchorMin = row.anchorMax = new Vector2(anchorX, 0.5f);
        row.pivot = new Vector2(pivotX, 0.5f);
        row.anchoredPosition = new Vector2(x, y);
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

        // The panel itself is a clone of the default (style 0) difficulty column so
        // the design and its animations (white brackets, staggered rows, blinking
        // prompt) are exactly the original component's. Only the backdrop (frozen
        // blur + scrim) is JSAB-specific.
        Transform canvases = transform.parent;
        Transform src = canvases != null ? canvases.Find("StageCanvas/StageBoxParent/DefficultyBar") : null;
        if (src != null)
        {
            GameObject clone = Instantiate(src.gameObject, diffRoot);
            clone.name = "DifficultyColumn";
            clone.SetActive(true);
            diffPanel = (RectTransform)clone.transform;
            diffPanel.anchorMin = diffPanel.anchorMax = new Vector2(0.5f, 0.5f);
            diffPanel.pivot = new Vector2(0.5f, 0.5f);
            diffPanel.anchoredPosition = new Vector2(0f, 30f);
            diffPanel.localScale = Vector3.one * 0.8f; // matches the original's on-screen scale
            diffBar = clone.GetComponent<DefficultyBar>();
            if (diffBar != null)
            {
                diffBar.Init();
                diffBar.SetAlpha(1f);
                diffBar.SetEntranceProgress(1f);
            }
            // Mouse hit areas = the visible bar sprites (583x109), not the tiny row roots.
            string[] rows = { "Easy", "Normal", "Lunatic" };
            for (int i = 0; i < rows.Length; i++)
                diffBoxRects[i] = clone.transform.Find("List/" + rows[i] + "/StageBar") as RectTransform;
        }

        diffRoot.gameObject.SetActive(false);
    }

    // ---- Public difficulty API (driven by StageSelectManager) ----

    public void OpenDifficulty()
    {
        if (diffRoot == null) return;
        difficultyOpen = true;
        diffOpenTime = Time.unscaledTime;
        mouseConfirm = false;
        if (diffBar != null) diffBar.ResetSelection(1); // default NORMAL each time it opens
        diffRoot.gameObject.SetActive(true);
        StartCoroutine(CaptureBlurBackground());
    }

    public void CloseDifficulty()
    {
        difficultyOpen = false;
        if (diffRoot != null) diffRoot.gameObject.SetActive(false);
    }

    public void MoveDifficulty(int dir)
    {
        if (!difficultyOpen || diffBar == null) return;
        if (dir > 0) diffBar.Down();
        else diffBar.Up();
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
        UpdateThumb(leftVP, leftThumb, leftThumbFallback, left);
        UpdateThumb(rightVP, rightThumb, rightThumbFallback, right);
        RefreshProgress(animate && dir != 0);

        if (animate && dir != 0)
        {
            // Slide the whole carousel band in from the direction we moved.
            slideFrom = dir > 0 ? 260f : -260f;
            slideTime = 0f;
        }
        else
        {
            slideTime = -1f;
            ApplySlideOffset(0f);
        }
    }

    // Offsets the carousel band (center card, stage name, both neighbour cards)
    // as one unit so the switch reads as a single smooth slide.
    private void ApplySlideOffset(float x)
    {
        Vector2 off = new Vector2(x, 0f);
        if (cardRect != null) cardRect.anchoredPosition = cardBasePos + off;
        if (stageNameRect != null) stageNameRect.anchoredPosition = stageNameBasePos + off;
        if (leftCardRect != null) leftCardRect.anchoredPosition = leftCardBasePos + off;
        if (rightCardRect != null) rightCardRect.anchoredPosition = rightCardBasePos + off;
    }

    public void Tick(float dt)
    {
        if (!Visible) return;

        // While the difficulty modal is open, the mouse can hover (to select) and
        // click (to confirm) any of the three buttons. Keyboard is driven by the
        // StageSelectManager; here we only translate pointer position/click.
        // Ignore the pointer for a beat after opening so the interaction that opened
        // the modal (or a focus click on the game view) cannot immediately confirm.
        if (difficultyOpen)
        {
            if (diffBar != null) diffBar.Tick(dt);
            if (Time.unscaledTime - diffOpenTime >= 0.2f)
            {
                Mouse mouse = Mouse.current;
                if (mouse != null)
                {
                    Vector2 mp = mouse.position.ReadValue();
                    int hover = -1;
                    for (int i = 0; i < diffBoxRects.Length; i++)
                    {
                        if (diffBoxRects[i] == null) continue;
                        if (RectTransformUtility.RectangleContainsScreenPoint(diffBoxRects[i], mp, null))
                        {
                            hover = i;
                            break;
                        }
                    }
                    if (hover >= 0 && diffBar != null)
                    {
                        // Route through Up/Down so the description/brackets update too.
                        while (diffBar.index > hover) diffBar.Up();
                        while (diffBar.index < hover) diffBar.Down();
                        if (mouse.leftButton.wasPressedThisFrame) mouseConfirm = true;
                    }
                }
            }
        }

        // Mirror the live (alpha-hidden) top bar originals: timer text incl. the
        // low-time warning pulse, and the red time-dim panel width.
        if (topBarTimerText != null && origTimerText != null)
        {
            topBarTimerText.text = origTimerText.text;
            topBarTimerText.color = origTimerText.color;
            topBarTimerText.rectTransform.localScale = origTimerText.rectTransform.localScale;
        }
        if (topBarTimeDim != null && origTimeDim != null)
        {
            topBarTimeDim.sizeDelta = origTimeDim.sizeDelta;
            topBarTimeDim.anchoredPosition = origTimeDim.anchoredPosition;
        }

        pulseTime += dt;
        // Breathing pulse on the selected card (matches StageBox.SetPulse feel).
        float pulse = 1f + 0.02f * (0.5f + 0.5f * Mathf.Sin(pulseTime * 3f));

        // Carousel band slide (ease-out). The root CanvasGroup alpha is left alone:
        // dipping it made the opaque overlay translucent for a few frames, which
        // read as a full-screen flash on every switch.
        if (slideTime >= 0f)
        {
            slideTime += dt;
            float p = Mathf.Clamp01(slideTime / slideDuration);
            float ease = 1f - Mathf.Pow(1f - p, 3f);
            ApplySlideOffset(p >= 1f ? 0f : Mathf.Lerp(slideFrom, 0f, ease));
            if (p >= 1f) slideTime = -1f;
        }

        // Page-indicator marker tween (ease-out between nodes).
        if (markerTweenTime >= 0f && markerRect != null)
        {
            markerTweenTime += dt;
            float p = Mathf.Clamp01(markerTweenTime / markerTweenDuration);
            float ease = 1f - Mathf.Pow(1f - p, 3f);
            markerRect.anchoredPosition = new Vector2(Mathf.Lerp(markerFromX, markerToX, ease), 0f);
            if (p >= 1f) markerTweenTime = -1f;
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
            if (videoPlayer == null) return;
            if (videoPlayer.url == path && videoPlayer.isPrepared)
            {
                if (!videoPlayer.isPlaying) videoPlayer.Play();
                return;
            }
            // Keep whatever is on the RenderTexture (the previous stage's frame)
            // visible while the new clip prepares; OnMainVideoPrepared swaps it in.
            // Only when there is no previous frame (first show, or coming from a
            // stage without video) does the fallback card cover the wait.
            if (cardVideo != null && !cardVideo.enabled && cardFallback != null)
                cardFallback.gameObject.SetActive(true);
            videoPlayer.url = path;
            videoPlayer.Prepare();
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

    private void OnMainVideoPrepared(VideoPlayer vp)
    {
        if (cardVideo != null) cardVideo.enabled = true;
        if (cardFallback != null) cardFallback.gameObject.SetActive(false);
        vp.Play();
    }

    // Prepares a neighbour's preview video into its RenderTexture (paused on the
    // first frame = a still, dim thumbnail). Falls back to a navy tile if missing.
    private void UpdateThumb(VideoPlayer vp, RawImage thumb, Image fallback, StageData data)
    {
        if (vp == null) return;
        string dir = data != null ? data.stageDirectoryName : null;
        string path = !string.IsNullOrEmpty(dir)
            ? Path.Combine(Application.dataPath, "StageData", dir, dir + ".mp4")
            : null;
        bool hasVideo = !string.IsNullOrEmpty(path) && File.Exists(path);

        if (hasVideo)
        {
            if (fallback != null) fallback.gameObject.SetActive(false);
            // thumb.enabled is flipped on in prepareCompleted so we never show a
            // stale texture from the previous stage.
            if (thumb != null) thumb.enabled = false;
            vp.url = path;
            vp.Prepare();
        }
        else
        {
            if (thumb != null) thumb.enabled = false;
            if (fallback != null) fallback.gameObject.SetActive(true);
            vp.Stop();
            vp.url = null;
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
        if (leftRT != null) { leftRT.Release(); Destroy(leftRT); }
        if (rightRT != null) { rightRT.Release(); Destroy(rightRT); }
    }
}

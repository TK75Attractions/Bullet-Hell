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

    // Carousel slots. Panels physically move between these on stage change:
    // the neighbour panel slides+grows into the center slot while the center
    // card slides+shrinks into the neighbour slot (true slide carousel).
    private const float BandYOffset = -60f;   // 全体を下げて上下の余白バランスを取る
    private const float SideSlotX = 712f;
    private const float OffSlotX = 1120f;     // 玉突きで画面外へ退場するパネルの x
    private const float SideSlotY = 40f + BandYOffset;
    private static readonly Vector2 CenterSlotPos = new Vector2(0f, 70f + BandYOffset);
    private static readonly Vector2 CenterSlotSize = new Vector2(936f, 528f);
    private static readonly Vector2 SideSlotSize = new Vector2(456f, 304f);
    private static readonly Color ThumbDim = new Color(0.5f, 0.55f, 0.62f, 1f);
    private static readonly Color ThumbScrimColor = new Color(0.02f, 0.05f, 0.09f, 0.4f);
    // CyanDim(alpha0.55)を黒背景に合成した不透明色。サイドパネルの枠帯用。
    // 半透明のままだと CanvasGroup フェード中に帯同士の重なりが濃く見える。
    private static readonly Color CyanDimRim = new Color(0.121f, 0.418f, 0.483f, 1f);

    private RectTransform stageNameRect;
    private RectTransform leftCardRect;
    private RectTransform rightCardRect;
    private CanvasGroup leftCG;
    private CanvasGroup rightCG;
    private CanvasGroup leftDecorCG;      // 隣パネルの名前+キーチップ(飛行中はフェードアウト)
    private CanvasGroup rightDecorCG;
    // 隣パネルの枠(飛行中に色を Cyan へ寄せる)。全面塗り+内側かぶせ方式だと
    // CanvasGroup フェード中に body が半透明になり、下の明るい塗りが中身全体
    // から透けて「フェード中だけ明るく光る」フラッシュになるため、4辺の帯で描く。
    private Image[] leftFrame;
    private Image[] rightFrame;
    private RectTransform leftBody;
    private RectTransform rightBody;
    private Image leftScrim;
    private Image rightScrim;
    private TMP_Text leftFbName;
    private TMP_Text rightFbName;
    private Image cardBorderImg;
    private RectTransform cardBodyRect;
    // 端のステージで存在しない側のパネルを隠すためのフェード目標値
    private float leftAlphaTarget = 1f;
    private float rightAlphaTarget = 1f;
    // サムネイル(or fallback)の準備が終わるまでフェードインを保留するフラグ。
    // 枠だけ先に出て、動画準備完了の瞬間に絵が瞬時ポップする「フラッシュ」を防ぐ。
    private bool leftContentReady = true;
    private bool rightContentReady = true;

    // Vertical divider color between the center and side columns.
    private static readonly Color Divider = new Color(0.22f, 0.76f, 0.878f, 0.4f);

    // Video
    private VideoPlayer videoPlayer;
    private RenderTexture videoRT;

    // State
    private int currentIndex = 0;
    private int totalStages = 1;
    private float pulseTime;
    // Carousel transition (panels flying between slots). <0 == not animating.
    private float transTime = -1f;
    private int transDir;
    private const float transDuration = 0.3f;
    private float exitStartAlpha;       // 退場パネルの開始アルファ(端では元々 0)
    private bool nameSwapped;           // ステージ名クロスフェードの差し替え済みフラグ

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
        cardBorderImg = border;
        cardRect = border.rectTransform;
        cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = CenterSlotPos;
        cardRect.sizeDelta = CenterSlotSize;

        // Body is stretch-anchored (6px border inset) so the whole card can be
        // resized via sizeDelta while flying between slots.
        Image cardBody = NewImage("CardBody", cardRect, NavyDeep);
        RectTransform cbr = cardBody.rectTransform;
        Stretch(cbr);
        SetInset(cbr, 6f);
        cardBodyRect = cbr;

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
        snr.anchoredPosition = new Vector2(0f, CenterSlotPos.y + CenterSlotSize.y * 0.5f + 46f);
        stageNameRect = snr;

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

        // Thin cyan frame around the thumbnail (4 rim strips, see leftFrame note).
        GameObject frameGO = new GameObject(side < 0 ? "LeftCard" : "RightCard", typeof(RectTransform));
        frameGO.transform.SetParent(root, false);
        RectTransform r = (RectTransform)frameGO.transform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = SideSlotSize;
        r.anchoredPosition = new Vector2(side * SideSlotX, SideSlotY);
        // 端のステージで丸ごとフェードアウトさせるための CanvasGroup。
        CanvasGroup cg = frameGO.AddComponent<CanvasGroup>();

        Image body = NewImage("ThumbBody", r, new Color(0.02f, 0.05f, 0.08f, 1f));
        RectTransform bodyR = body.rectTransform;
        Stretch(bodyR);
        SetInset(bodyR, 2f);

        Image[] rim = BuildRim(r);
        SetRim(rim, CyanDimRim, 2f);

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
        thumb.color = ThumbDim; // dim + slight desaturate feel
        thumb.enabled = false;

        // A subtle dark wash over the thumbnail so it reads as "dimmed / inactive".
        Image scrim = NewImage("ThumbScrim", bodyR, ThumbScrimColor);
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
        vp.prepareCompleted += v =>
        {
            if (thumb != null) thumb.enabled = true;
            v.Pause();
            SetThumbReady(side, true);
        };
        // 準備に失敗した場合も fallback タイルでフェードインを解放する。
        vp.errorReceived += (v, msg) =>
        {
            if (fallback != null) fallback.gameObject.SetActive(true);
            SetThumbReady(side, true);
        };

        // Decor (stage name + key chip) sits in its own CanvasGroup so it can
        // fade out while the panel flies into the center slot.
        GameObject decorGO = new GameObject("Decor", typeof(RectTransform));
        decorGO.transform.SetParent(r, false);
        RectTransform decorR = (RectTransform)decorGO.transform;
        Stretch(decorR);
        CanvasGroup decorCG = decorGO.AddComponent<CanvasGroup>();

        // Stage name above the thumbnail (dim, per reference).
        TMP_Text name = NewText(side < 0 ? "LeftName" : "RightName", decorR, "", 34f, CyanDim, TextAlignmentOptions.Center);
        RectTransform nr = (RectTransform)name.transform;
        nr.anchorMin = new Vector2(0f, 1f);
        nr.anchorMax = new Vector2(1f, 1f);
        nr.pivot = new Vector2(0.5f, 0f);
        nr.anchoredPosition = new Vector2(0f, 14f);
        nr.sizeDelta = new Vector2(0f, 48f);

        // 山括弧型の矢印(キーチップ廃止)。サムネイル外側の縁に上下中央で重ね、
        // カルーセルの進行方向を示す。decor 配下なので飛行中は名前ごとフェードする。
        // サイズ/位置/alpha は oracle レビュー反映(56pt・端から36px・alpha0.8)。
        TMP_Text arrow = NewText(side < 0 ? "LeftArrow" : "RightArrow", decorR,
            side < 0 ? "<" : ">", 56f, new Color(Cyan.r, Cyan.g, Cyan.b, 0.8f), TextAlignmentOptions.Center);
        RectTransform ar = (RectTransform)arrow.transform;
        ar.anchorMin = ar.anchorMax = new Vector2(side < 0 ? 0f : 1f, 0.5f);
        ar.pivot = new Vector2(0.5f, 0.5f);
        ar.anchoredPosition = new Vector2(side < 0 ? 36f : -36f, 0f);
        ar.sizeDelta = new Vector2(64f, 90f);
        TmpAlign.CenterInkVertically(arrow);

        if (side < 0)
        {
            leftName = name; leftThumb = thumb; leftThumbFallback = fallback;
            leftVP = vp; leftRT = rt;
            leftCardRect = r; leftCG = cg; leftDecorCG = decorCG;
            leftFrame = rim; leftBody = bodyR; leftScrim = scrim; leftFbName = fbName;
        }
        else
        {
            rightName = name; rightThumb = thumb; rightThumbFallback = fallback;
            rightVP = vp; rightRT = rt;
            rightCardRect = r; rightCG = cg; rightDecorCG = decorCG;
            rightFrame = rim; rightBody = bodyR; rightScrim = scrim; rightFbName = fbName;
        }
    }

    // 4辺の帯(上/下/左/右)でパネルの枠線を作る。面で塗らないので、
    // CanvasGroup の中間 alpha でも中身の下から枠色が透けない。
    private Image[] BuildRim(RectTransform parent)
    {
        Image[] rim = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            Image s = NewImage("Rim" + i, parent, CyanDimRim);
            RectTransform rt = s.rectTransform;
            switch (i)
            {
                case 0: rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f); break;   // top
                case 1: rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f); rt.pivot = new Vector2(0.5f, 0f); break;   // bottom
                case 2: rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 0.5f); break;   // left
                default: rt.anchorMin = new Vector2(1f, 0f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(1f, 0.5f); break;  // right
            }
            rt.anchoredPosition = Vector2.zero;
            rim[i] = s;
        }
        return rim;
    }

    // 帯4本の色と太さをまとめて更新する(遷移中の補間もここを通す)。
    private static void SetRim(Image[] rim, Color color, float thickness)
    {
        if (rim == null) return;
        for (int i = 0; i < rim.Length; i++)
        {
            if (rim[i] == null) continue;
            rim[i].color = color;
            RectTransform rt = rim[i].rectTransform;
            // 縦帯は上下の横帯ぶんを避けて角の二重描画を防ぐ。
            rt.sizeDelta = i < 2 ? new Vector2(0f, thickness) : new Vector2(thickness, -2f * thickness);
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
        progressRow.anchoredPosition = new Vector2(0f, CenterSlotPos.y - CenterSlotSize.y * 0.5f - 72f);
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
        if (transTime >= 0f)
        {
            // 飛行中の同一インデックス通知は無視。連打時は現在の遷移を即着地
            // させてから次の遷移を始める(状態の取りこぼし防止)。
            if (newIndex == currentIndex) return;
            FinishTransition();
        }
        int dir = newIndex == currentIndex ? 0 : (newIndex > currentIndex ? 1 : -1);
        currentIndex = newIndex;
        RefreshProgress(animate && dir != 0);

        if (animate && dir != 0)
        {
            BeginTransition(dir);
        }
        else
        {
            ApplyStageContent(false);
            ResetPanels(true);
        }
    }

    // Starts the slide-carousel transition: the neighbour panel on the moved-to
    // side flies into the center slot while the center card flies out to the
    // opposite neighbour slot. Content (names/thumbs) swaps only on landing.
    private void BeginTransition(int dir)
    {
        transDir = dir;
        transTime = 0f;
        nameSwapped = false;
        CanvasGroup exitCG = dir > 0 ? leftCG : rightCG;
        exitStartAlpha = exitCG != null ? exitCG.alpha : 1f;
        // 到着後すぐ再生に移れるよう、飛行中に新ステージの動画を裏で準備する。
        // url を差し替えた時点で旧クリップは止まり、RT は最後のフレームで凍結
        // するので、縮小しながら退く中央カードには旧ステージの静止画が残る。
        StageData cur = GetStage(currentIndex);
        string path = VideoPath(cur);
        if (videoPlayer != null && path != null && videoPlayer.url != path)
        {
            videoPlayer.url = path;
            videoPlayer.Prepare();
        }
        if (cardRect != null) cardRect.localScale = Vector3.one; // 飛行中はパルス停止
    }

    // Drives one frame of the transition. p is linear progress [0..1].
    private void ApplyTransition(float p)
    {
        float e = 1f - Mathf.Pow(1f - p, 3f); // ease-out cubic

        Vector2 fromSide = new Vector2(transDir * SideSlotX, SideSlotY);   // 到着パネルの出発点
        Vector2 toSide = new Vector2(-transDir * SideSlotX, SideSlotY);    // 中央カードの行き先
        Vector2 offSide = new Vector2(-transDir * OffSlotX, SideSlotY);    // 退場パネルの行き先

        // 中央カード: 中央スロット → 反対隣スロットへ、縮小しながら移動。
        if (cardRect != null)
        {
            cardRect.anchoredPosition = Vector2.Lerp(CenterSlotPos, toSide, e);
            cardRect.sizeDelta = Vector2.Lerp(CenterSlotSize, SideSlotSize, e);
        }
        if (cardBorderImg != null) cardBorderImg.color = Color.Lerp(Cyan, CyanDim, e);
        if (cardBodyRect != null) SetInset(cardBodyRect, Mathf.Lerp(6f, 2f, e));
        if (cardVideo != null) cardVideo.color = Color.Lerp(Color.white, ThumbDim, e);
        if (cardFallbackName != null) cardFallbackName.fontSize = Mathf.Lerp(96f, 44f, e);

        // 到着パネル: 隣スロット → 中央スロットへ、拡大しながら移動。
        // 装飾(名前/チップ)と減光は先行してフェードし、中央カードの見た目へ寄せる。
        RectTransform arrive = transDir > 0 ? rightCardRect : leftCardRect;
        Image[] arriveFrame = transDir > 0 ? rightFrame : leftFrame;
        RectTransform arriveBody = transDir > 0 ? rightBody : leftBody;
        RawImage arriveThumb = transDir > 0 ? rightThumb : leftThumb;
        Image arriveScrim = transDir > 0 ? rightScrim : leftScrim;
        CanvasGroup arriveDecor = transDir > 0 ? rightDecorCG : leftDecorCG;
        CanvasGroup arriveCG = transDir > 0 ? rightCG : leftCG;
        TMP_Text arriveFb = transDir > 0 ? rightFbName : leftFbName;
        if (arrive != null)
        {
            arrive.anchoredPosition = Vector2.Lerp(fromSide, CenterSlotPos, e);
            arrive.sizeDelta = Vector2.Lerp(SideSlotSize, CenterSlotSize, e);
        }
        SetRim(arriveFrame, Color.Lerp(CyanDimRim, Cyan, e), Mathf.Lerp(2f, 6f, e));
        if (arriveBody != null) SetInset(arriveBody, Mathf.Lerp(2f, 6f, e));
        if (arriveThumb != null) arriveThumb.color = Color.Lerp(ThumbDim, Color.white, e);
        if (arriveScrim != null)
        {
            Color sc = ThumbScrimColor;
            sc.a = Mathf.Lerp(sc.a, 0f, e);
            arriveScrim.color = sc;
        }
        if (arriveDecor != null) arriveDecor.alpha = 1f - Mathf.Clamp01(p * 3f);
        if (arriveCG != null) arriveCG.alpha = 1f;
        if (arriveFb != null) arriveFb.fontSize = Mathf.Lerp(44f, 96f, e);

        // 退場パネル: 隣スロット → 画面外へフェードアウトしながら移動。
        RectTransform exit = transDir > 0 ? leftCardRect : rightCardRect;
        CanvasGroup exitCG = transDir > 0 ? leftCG : rightCG;
        if (exit != null) exit.anchoredPosition = Vector2.Lerp(toSide, offSide, e);
        if (exitCG != null) exitCG.alpha = exitStartAlpha * (1f - p);

        // ステージ名: 旧名は序盤(0〜0.35)で消し切り、新名は終盤(0.55〜1.0)で出す。
        // 中間フレームに旧名が薄く残ると退場中か選択中か曖昧に見えるため。
        if (stageNameText != null)
        {
            if (!nameSwapped && p >= 0.45f)
            {
                nameSwapped = true;
                StageData cur = GetStage(currentIndex);
                string curName = cur != null && !string.IsNullOrWhiteSpace(cur.stageName) ? cur.stageName : ("Stage " + currentIndex);
                stageNameText.text = curName;
                TmpAlign.CenterInkVertically(stageNameText);
            }
            stageNameText.alpha = p < 0.35f ? 1f - p / 0.35f
                                : p < 0.55f ? 0f
                                : (p - 0.55f) / 0.45f;
        }
    }

    // Lands the transition: copies the arriving panel's still frame onto the
    // center RenderTexture (so the swap frame shows the exact same picture),
    // snaps every panel back to its home slot and applies the new content.
    private void FinishTransition()
    {
        if (transTime < 0f) return;
        transTime = -1f;

        RawImage arriveThumb = transDir > 0 ? rightThumb : leftThumb;
        RenderTexture arriveRT = transDir > 0 ? rightRT : leftRT;
        bool arriveHadFrame = arriveThumb != null && arriveThumb.enabled && arriveRT != null;
        if (arriveHadFrame && videoRT != null) Graphics.Blit(arriveRT, videoRT);

        ResetPanels(false);
        ApplyStageContent(arriveHadFrame);
        if (stageNameText != null) stageNameText.alpha = 1f;
    }

    // Puts every panel back to its home slot / home look. instantAlpha=true
    // snaps the side panels straight to their visibility target (initial show);
    // false hides them so Tick fades them back in over the content swap.
    private void ResetPanels(bool instantAlpha)
    {
        if (cardRect != null)
        {
            cardRect.anchoredPosition = CenterSlotPos;
            cardRect.sizeDelta = CenterSlotSize;
        }
        if (cardBorderImg != null) cardBorderImg.color = Cyan;
        if (cardBodyRect != null) SetInset(cardBodyRect, 6f);
        if (cardVideo != null) cardVideo.color = Color.white;
        if (cardFallbackName != null) cardFallbackName.fontSize = 96f;

        if (leftCardRect != null)
        {
            leftCardRect.anchoredPosition = new Vector2(-SideSlotX, SideSlotY);
            leftCardRect.sizeDelta = SideSlotSize;
        }
        if (rightCardRect != null)
        {
            rightCardRect.anchoredPosition = new Vector2(SideSlotX, SideSlotY);
            rightCardRect.sizeDelta = SideSlotSize;
        }
        SetRim(leftFrame, CyanDimRim, 2f);
        SetRim(rightFrame, CyanDimRim, 2f);
        if (leftBody != null) SetInset(leftBody, 2f);
        if (rightBody != null) SetInset(rightBody, 2f);
        if (leftThumb != null) leftThumb.color = ThumbDim;
        if (rightThumb != null) rightThumb.color = ThumbDim;
        if (leftScrim != null) leftScrim.color = ThumbScrimColor;
        if (rightScrim != null) rightScrim.color = ThumbScrimColor;
        if (leftDecorCG != null) leftDecorCG.alpha = 1f;
        if (rightDecorCG != null) rightDecorCG.alpha = 1f;
        if (leftFbName != null) leftFbName.fontSize = 44f;
        if (rightFbName != null) rightFbName.fontSize = 44f;

        // 端のステージでは存在しない側のパネルを隠す(ラップアラウンドなし)。
        leftAlphaTarget = GetStage(currentIndex - 1) != null ? 1f : 0f;
        rightAlphaTarget = GetStage(currentIndex + 1) != null ? 1f : 0f;
        if (instantAlpha)
        {
            // 初期表示でもサムネイル未準備なら 0 から始め、Tick 側で
            // 準備完了後にフェードインさせる(絵の瞬時ポップ防止)。
            if (leftCG != null) leftCG.alpha = leftContentReady ? leftAlphaTarget : 0f;
            if (rightCG != null) rightCG.alpha = rightContentReady ? rightAlphaTarget : 0f;
        }
        else
        {
            // 着地直後はサムネイル差し替え中の古い絵を隠し、Tick でフェードイン。
            if (leftCG != null) leftCG.alpha = 0f;
            if (rightCG != null) rightCG.alpha = 0f;
        }
    }

    // Applies the current stage's content to every panel (names, main video,
    // neighbour thumbnails). keepBlittedFrame=true means the center RT already
    // holds the arriving panel's still frame, so it can stay visible while the
    // main clip finishes preparing (no pop on the landing frame).
    private void ApplyStageContent(bool keepBlittedFrame)
    {
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

        UpdateVideo(cur, keepBlittedFrame);
        UpdateThumb(-1, leftVP, leftThumb, leftThumbFallback, left);
        UpdateThumb(1, rightVP, rightThumb, rightThumbFallback, right);
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

        // Slide-carousel transition (panels flying between slots, ease-out).
        // The root CanvasGroup alpha is left alone: dipping it made the opaque
        // overlay translucent for a few frames, which read as a full-screen flash.
        if (transTime >= 0f)
        {
            transTime += dt;
            float p = Mathf.Clamp01(transTime / transDuration);
            ApplyTransition(p);
            if (p >= 1f) FinishTransition();
        }
        else
        {
            // 端フェード / 着地後のフェードイン。存在しない側は 0 に向かう。
            // サムネイル未準備の側は 0 のまま待機し、準備完了後に枠+絵を
            // ひとかたまりで 0.15s フェードイン(絵だけ後からポップさせない)。
            // 動画 Prepare 直後は dt がスパイクするため 1/30s にクランプし、
            // フェードが1フレームに潰れて瞬時ポップに見えるのを防ぐ。
            float step = Mathf.Min(dt, 1f / 30f) / 0.15f;
            float lTarget = leftContentReady ? leftAlphaTarget : 0f;
            float rTarget = rightContentReady ? rightAlphaTarget : 0f;
            if (leftCG != null) leftCG.alpha = Mathf.MoveTowards(leftCG.alpha, lTarget, step);
            if (rightCG != null) rightCG.alpha = Mathf.MoveTowards(rightCG.alpha, rTarget, step);
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

        if (cardRect != null && transTime < 0f) cardRect.localScale = Vector3.one * pulse;
    }

    // ---- internals ----

    // Returns the preview-clip path for a stage, or null when it has none.
    private static string VideoPath(StageData data)
    {
        string dir = data != null ? data.stageDirectoryName : null;
        if (string.IsNullOrEmpty(dir)) return null;
        string path = Path.Combine(Application.dataPath, "StageData", dir, dir + ".mp4");
        return File.Exists(path) ? path : null;
    }

    private void UpdateVideo(StageData data, bool keepBlittedFrame)
    {
        string path = VideoPath(data);
        if (path != null)
        {
            if (videoPlayer == null) return;
            if (videoPlayer.url == path && videoPlayer.isPrepared)
            {
                if (cardVideo != null) cardVideo.enabled = true;
                if (cardFallback != null) cardFallback.gameObject.SetActive(false);
                if (!videoPlayer.isPlaying) videoPlayer.Play();
                return;
            }
            // 準備待ちの間の表示: 着地時に Blit した到着パネルの静止画があれば
            // それを見せ続ける。無い場合のみフォールバックカードで覆う。
            if (keepBlittedFrame)
            {
                if (cardVideo != null) cardVideo.enabled = true;
                if (cardFallback != null) cardFallback.gameObject.SetActive(false);
            }
            else if (cardVideo != null && !cardVideo.enabled && cardFallback != null)
            {
                cardFallback.gameObject.SetActive(true);
            }
            if (videoPlayer.url != path)
            {
                videoPlayer.url = path;
                videoPlayer.Prepare();
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

    private void OnMainVideoPrepared(VideoPlayer vp)
    {
        // 飛行中は退場する中央カードに旧ステージの静止画を残したいので、
        // 表示切り替えと再生開始は着地時(FinishTransition 経由)に任せる。
        if (transTime >= 0f) return;
        if (cardVideo != null) cardVideo.enabled = true;
        if (cardFallback != null) cardFallback.gameObject.SetActive(false);
        vp.Play();
    }

    private void SetThumbReady(int side, bool ready)
    {
        if (side < 0) leftContentReady = ready;
        else rightContentReady = ready;
    }

    // Prepares a neighbour's preview video into its RenderTexture (paused on the
    // first frame = a still, dim thumbnail). Falls back to a navy tile if missing.
    private void UpdateThumb(int side, VideoPlayer vp, RawImage thumb, Image fallback, StageData data)
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
            // 準備完了(prepareCompleted)までパネルのフェードインを保留する。
            SetThumbReady(side, false);
            vp.url = path;
            vp.Prepare();
        }
        else
        {
            if (thumb != null) thumb.enabled = false;
            if (fallback != null) fallback.gameObject.SetActive(true);
            vp.Stop();
            vp.url = null;
            SetThumbReady(side, true); // fallback タイルは即表示できる
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

    // Uniform inset for a stretch-anchored rect (border thickness of a panel).
    private static void SetInset(RectTransform r, float inset)
    {
        r.offsetMin = new Vector2(inset, inset);
        r.offsetMax = new Vector2(-inset, -inset);
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

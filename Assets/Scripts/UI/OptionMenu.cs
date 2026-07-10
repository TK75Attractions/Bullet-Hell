using System.Collections;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Pause / option menu operation. The menu builds a few small runtime visuals
// (blurred backdrop, volume knob, toggle and confirmation buttons) around the
// scene-authored typography so the layout remains easy to tune.
public class OptionMenu : MonoBehaviour
{
    private static readonly float[] rowY = { 170f, 10f, -130f, -280f };
    private const float itemBaseX = -410f;
    private const float sliderWidth = 660f;
    private const float selectedShiftX = 10f;
    private const float textBlockShiftY = -10f;
    private const float confirmButtonTextOffsetY = -7f;
    private static readonly Vector2 yesButtonPosition = new Vector2(-160f, -95f);
    private static readonly Vector2 noButtonPosition = new Vector2(160f, -95f);

    private static readonly Color unselectedColor = new Color(0.78f, 0.84f, 0.92f);
    private static readonly Color disabledGray = new Color(0.42f, 0.46f, 0.54f);
    private static readonly Color accentBlue = new Color(0.16f, 0.58f, 1f);

    private RectTransform selectBand;
    private readonly RectTransform[] badges = new RectTransform[4];
    private readonly float[] badgeBaseRotationZ = new float[4];
    private readonly float[] badgeRotationOffset = new float[4];
    private readonly TMP_Text[] items = new TMP_Text[4];
    private readonly RectTransform[] itemRects = new RectTransform[4];
    private readonly RectTransform[] rubyRects = new RectTransform[4];
    private readonly float[] rubyBaseX = new float[4];
    private readonly float[] rubyBaseY = new float[4];
    private readonly float[] itemVisualOffsetY = new float[4];
    private readonly float[] itemOffset = new float[4];
    private readonly RectTransform[] headerRects = new RectTransform[4];
    private readonly Vector2[] headerBasePositions = new Vector2[4];

    private RectTransform sliderFill;
    private RectTransform sliderKnob;
    private Graphic sliderKnobGraphic;
    private Graphic toggleTrack;
    private RectTransform toggleKnob;
    private Graphic toggleKnobGraphic;
    private TMP_Text toggleStateText;

    private CanvasGroup countdownGroup;
    private TMP_Text countdownText;

    private GameObject confirmGroup;
    private TMP_Text confirmTitle;
    private TMP_Text confirmDetail;
    private TMP_Text yesText;
    private TMP_Text noText;
    private Image yesButton;
    private Image noButton;
    // リザルト様式ボタン(UiButtonStyle)の選択状態は色スワップではなく
    // CanvasGroup の減光で表す(スラッシュ等の子要素ごと沈める)。
    private CanvasGroup yesButtonGroup;
    private CanvasGroup noButtonGroup;

    private RawImage confirmBackdrop;
    private RenderTexture confirmBlurRT;
    private Coroutine confirmCaptureRoutine;
    private CanvasGroup confirmCanvasGroup;
    private Coroutine confirmOpenAnimationRoutine;
    private float confirmContentScale = 1f;

    private RawImage menuBackdrop;
    private GameObject menuShade;
    private RenderTexture menuBlurRT;
    private Coroutine menuCaptureRoutine;
    private bool waitingForMenuCapture;

    private int index;
    private bool confirmOpen;
    private int confirmIndex = 1;
    private bool effectsOn = true;
    private bool initialized;
    private bool quitting;
    private bool closing;
    private float bandY;
    private CanvasGroup group;
    private float openAnimT = 1f;
    private float headerAnimT = 1f;

    // タイトルから開いた設定では「プレイを終了」行を隠し、「再開する」は
    // ポーズ解除(カウントダウン)ではなくタイトルへ戻る動作にする。
    private bool titleContext;
    private System.Action titleResumeRequest;

    private void EnsureInit()
    {
        if (initialized) return;
        initialized = true;

        group = GetComponent<CanvasGroup>();
        selectBand = transform.Find("SelectBand") as RectTransform;
        string[] headerNames = { "HeaderBar", "HeaderIcon", "HeaderText", "HeaderRuby" };
        for (int i = 0; i < headerNames.Length; i++)
        {
            headerRects[i] = transform.Find(headerNames[i]) as RectTransform;
            if (headerRects[i] != null) headerBasePositions[i] = headerRects[i].anchoredPosition;
        }
        for (int i = 0; i < 4; i++)
        {
            badges[i] = transform.Find("Badge" + i) as RectTransform;
            if (badges[i] != null) badgeBaseRotationZ[i] = badges[i].localEulerAngles.z;
            Transform item = transform.Find("Item" + i);
            items[i] = item.GetComponent<TMP_Text>();
            itemRects[i] = item as RectTransform;
            items[i].ForceMeshUpdate(true, true);
            itemVisualOffsetY[i] = Mathf.Clamp(-items[i].textBounds.center.y, -12f, 12f);

            Transform ruby = transform.Find("Ruby" + i);
            if (ruby != null)
            {
                rubyRects[i] = ruby as RectTransform;
                rubyBaseX[i] = rubyRects[i].anchoredPosition.x;
                rubyBaseY[i] = rubyRects[i].anchoredPosition.y;
            }
        }

        sliderFill = transform.Find("SliderBack/SliderFill") as RectTransform;
        Transform confirm = transform.Find("ConfirmGroup");
        confirmGroup = confirm.gameObject;
        confirmTitle = confirm.Find("ConfirmText1").GetComponent<TMP_Text>();
        confirmDetail = confirm.Find("ConfirmText2").GetComponent<TMP_Text>();
        yesText = confirm.Find("YesText").GetComponent<TMP_Text>();
        noText = confirm.Find("NoText").GetComponent<TMP_Text>();

        Transform on = transform.Find("OnText");
        Transform off = transform.Find("OffText");
        Transform border = transform.Find("EffectBorder");
        if (on != null) on.gameObject.SetActive(false);
        if (off != null) off.gameObject.SetActive(false);
        if (border != null) border.gameObject.SetActive(false);

        BuildFriendlyVisuals();
    }

    private void BuildFriendlyVisuals()
    {
        RestyleHeaderBand();

        // 背景ぼかしは難易度オーバーレイと同じダウンサンプルピラミッド方式
        // (BackdropBlurUtil)で作るため、シェーダマテリアルは使わない。RawImage は
        // 既定マテリアルで 1/4 解像度のぼかし RT をバイリニア拡大表示する。
        GameObject menuBlurObject = new GameObject("MenuBackdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        menuBlurObject.layer = gameObject.layer;
        RectTransform menuBlurRect = (RectTransform)menuBlurObject.transform;
        menuBlurRect.SetParent(transform, false);
        Stretch(menuBlurRect);
        menuBlurRect.SetAsFirstSibling();
        menuBackdrop = menuBlurObject.GetComponent<RawImage>();
        menuBackdrop.color = Color.white;
        menuBackdrop.raycastTarget = false;
        menuBackdrop.gameObject.SetActive(false);

        menuShade = CreateImage("MenuShade", transform, Vector2.zero, new Vector2(1920f, 1080f), new Color(0.005f, 0.012f, 0.03f, 0.52f));
        menuShade.transform.SetSiblingIndex(1);
        menuShade.SetActive(false);

        RectTransform sliderBack = sliderFill.parent as RectTransform;
        GameObject knobObject = new GameObject("SliderKnob", typeof(RectTransform), typeof(CanvasRenderer), typeof(CapsuleGraphic));
        knobObject.layer = gameObject.layer;
        sliderKnob = (RectTransform)knobObject.transform;
        sliderKnob.SetParent(sliderBack, false);
        sliderKnob.anchorMin = sliderKnob.anchorMax = new Vector2(0.5f, 0.5f);
        sliderKnob.sizeDelta = Vector2.one * 48f;
        sliderKnobGraphic = knobObject.GetComponent<CapsuleGraphic>();
        sliderKnobGraphic.color = new Color(0.05f, 0.06f, 0.10f, 1f);
        sliderKnobGraphic.raycastTarget = false;
        GameObject knobFillObject = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(CapsuleGraphic));
        knobFillObject.layer = gameObject.layer;
        RectTransform knobFill = (RectTransform)knobFillObject.transform;
        knobFill.SetParent(sliderKnob, false);
        knobFill.anchorMin = knobFill.anchorMax = new Vector2(0.5f, 0.5f);
        knobFill.sizeDelta = Vector2.one * 38f;
        CapsuleGraphic knobFillGraphic = knobFillObject.GetComponent<CapsuleGraphic>();
        knobFillGraphic.color = Color.white;
        knobFillGraphic.raycastTarget = false;

        GameObject toggleObject = new GameObject("EffectToggle", typeof(RectTransform), typeof(CanvasRenderer), typeof(CapsuleGraphic));
        toggleObject.layer = gameObject.layer;
        RectTransform toggleRect = (RectTransform)toggleObject.transform;
        toggleRect.SetParent(transform, false);
        toggleRect.anchorMin = toggleRect.anchorMax = new Vector2(0.5f, 0.5f);
        toggleRect.anchoredPosition = new Vector2(400f, rowY[2]);
        toggleRect.sizeDelta = new Vector2(154f, 52f);
        CapsuleGraphic toggleOutline = toggleObject.GetComponent<CapsuleGraphic>();
        toggleOutline.color = new Color(0.42f, 0.58f, 0.72f, 0.9f);
        toggleOutline.raycastTarget = false;

        GameObject trackObject = new GameObject("Track", typeof(RectTransform), typeof(CanvasRenderer), typeof(CapsuleGraphic));
        trackObject.layer = gameObject.layer;
        RectTransform trackRect = (RectTransform)trackObject.transform;
        trackRect.SetParent(toggleRect, false);
        trackRect.anchorMin = trackRect.anchorMax = new Vector2(0.5f, 0.5f);
        trackRect.sizeDelta = new Vector2(146f, 44f);
        toggleTrack = trackObject.GetComponent<CapsuleGraphic>();
        toggleTrack.raycastTarget = false;

        GameObject toggleKnobObject = new GameObject("Knob", typeof(RectTransform), typeof(CanvasRenderer), typeof(CapsuleGraphic));
        toggleKnobObject.layer = gameObject.layer;
        toggleKnob = (RectTransform)toggleKnobObject.transform;
        toggleKnob.SetParent(trackRect, false);
        toggleKnob.anchorMin = toggleKnob.anchorMax = new Vector2(0.5f, 0.5f);
        toggleKnob.sizeDelta = Vector2.one * 36f;
        toggleKnobGraphic = toggleKnobObject.GetComponent<CapsuleGraphic>();
        toggleKnobGraphic.raycastTarget = false;

        toggleStateText = CreateLabel("EffectState", transform, new Vector2(515f, rowY[2]), new Vector2(90f, 48f), 25f);
        toggleStateText.rectTransform.anchoredPosition = new Vector2(565f, rowY[2]);
        toggleStateText.rectTransform.sizeDelta = new Vector2(130f, 56f);
        toggleStateText.font = items[0].font;
        toggleStateText.fontSize = 38f;
        toggleStateText.fontStyle = FontStyles.Normal;

        GameObject countdownObject = new GameObject("ResumeCountdown", typeof(RectTransform), typeof(CanvasGroup));
        countdownObject.layer = gameObject.layer;
        RectTransform countdownRect = (RectTransform)countdownObject.transform;
        countdownRect.SetParent(transform.parent, false);
        Stretch(countdownRect);
        countdownRect.SetAsLastSibling();
        countdownGroup = countdownObject.GetComponent<CanvasGroup>();
        countdownGroup.alpha = 0f;
        countdownText = CreateLabel("Count", countdownRect, Vector2.zero, new Vector2(900f, 360f), 170f);
        countdownText.font = items[0].font;
        countdownText.fontStyle = FontStyles.Bold;
        countdownText.color = Color.white;
        TextMeshProUGUI countdownLabel = countdownText as TextMeshProUGUI;
        if (countdownLabel != null)
        {
            countdownLabel.outlineWidth = 0.16f;
            countdownLabel.outlineColor = new Color32(0, 10, 24, 220);
        }
        countdownObject.SetActive(false);

        confirmGroup.transform.SetAsLastSibling();
        confirmCanvasGroup = confirmGroup.GetComponent<CanvasGroup>();
        if (confirmCanvasGroup == null) confirmCanvasGroup = confirmGroup.AddComponent<CanvasGroup>();
        GameObject confirmBlurObject = new GameObject("ConfirmBlur", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        confirmBlurObject.layer = gameObject.layer;
        RectTransform confirmBlurRect = (RectTransform)confirmBlurObject.transform;
        confirmBlurRect.SetParent(confirmGroup.transform, false);
        Stretch(confirmBlurRect);
        confirmBlurRect.SetAsFirstSibling();
        confirmBackdrop = confirmBlurObject.GetComponent<RawImage>();
        confirmBackdrop.color = Color.white;
        confirmBackdrop.raycastTarget = false;

        GameObject shade = CreateImage("ConfirmShade", confirmGroup.transform, Vector2.zero, new Vector2(1920f, 1080f), new Color(0f, 0f, 0f, 0.94f));
        shade.transform.SetSiblingIndex(1);

        confirmTitle.text = "プレイを終了しますか";
        confirmTitle.rectTransform.anchoredPosition = new Vector2(0f, 72f);
        confirmTitle.rectTransform.sizeDelta = new Vector2(760f, 80f);
        confirmTitle.alignment = TextAlignmentOptions.Center;
        confirmTitle.fontSize = 48f;
        confirmDetail.text = string.Empty;
        confirmDetail.gameObject.SetActive(false);

        // リザルト画面で確立したボタン様式(銀枠+シアンリム+青縦グラデ+
        // 左右対称の白スラッシュ)へ統一(統一便)。サイズ・配置は従来のまま。
        Sprite confirmButtonSprite = UiButtonStyle.CreateBodySprite(260, 86, null, null, "OptionConfirmButton");
        yesButton = CreateImage("YesButton", confirmGroup.transform, yesButtonPosition, new Vector2(260f, 86f), Color.white).GetComponent<Image>();
        noButton = CreateImage("NoButton", confirmGroup.transform, noButtonPosition, new Vector2(260f, 86f), Color.white).GetComponent<Image>();
        yesButton.sprite = confirmButtonSprite;
        noButton.sprite = confirmButtonSprite;
        UiButtonStyle.AddSlashPair(yesButton.rectTransform, 260f, 86f);
        UiButtonStyle.AddSlashPair(noButton.rectTransform, 260f, 86f);
        yesButtonGroup = yesButton.gameObject.AddComponent<CanvasGroup>();
        noButtonGroup = noButton.gameObject.AddComponent<CanvasGroup>();
        yesText.text = "はい";
        noText.text = "いいえ";
        SetupButtonText(yesText, yesButtonPosition);
        SetupButtonText(noText, noButtonPosition);
        confirmTitle.transform.SetAsLastSibling();
        confirmDetail.transform.SetAsLastSibling();
        yesText.transform.SetAsLastSibling();
        noText.transform.SetAsLastSibling();
    }

    // fromTitle=true でタイトルから開いた設定として振る舞う(終了行を隠し、
    // 再開する=タイトルへ戻る)。onResume はタイトル文脈での「再開する」押下時に
    // 呼ばれる(GManager が設定画面を閉じる)。
    public void Open(bool fromTitle = false, System.Action onResume = null)
    {
        titleContext = fromTitle;
        titleResumeRequest = onResume;
        EnsureInit();
        ApplyContextVisibility();
        index = 0;
        confirmOpen = false;
        confirmIndex = 1;
        quitting = false;
        closing = false;
        for (int i = 0; i < itemOffset.Length; i++) itemOffset[i] = 0f;
        for (int i = 0; i < badgeRotationOffset.Length; i++)
        {
            badgeRotationOffset[i] = 0f;
            if (badges[i] != null) badges[i].localEulerAngles = new Vector3(0f, 0f, badgeBaseRotationZ[i]);
        }
        confirmGroup.SetActive(false);
        bandY = rowY[0];
        selectBand.anchoredPosition = new Vector2(0f, bandY);
        RefreshSlider();
        RefreshEffects();

        openAnimT = 0f;
        headerAnimT = 0f;
        ApplyHeaderEntrance(0f);
        if (group != null) group.alpha = 0f;
        transform.localScale = Vector3.one * 0.97f;

        if (menuCaptureRoutine != null) StopCoroutine(menuCaptureRoutine);
        BackdropBlurUtil.ReleaseRT(ref menuBlurRT);
        menuBackdrop.texture = null;
        menuBackdrop.gameObject.SetActive(false);
        menuShade.SetActive(false);
        waitingForMenuCapture = true;
        menuCaptureRoutine = StartCoroutine(CaptureMenuBackdrop());
    }

    // タイトル文脈では終了行(row 3: プレイを終了)を丸ごと隠す。
    private void ApplyContextVisibility()
    {
        bool showQuit = !titleContext;
        if (items[3] != null) items[3].gameObject.SetActive(showQuit);
        if (badges[3] != null) badges[3].gameObject.SetActive(showQuit);
        if (rubyRects[3] != null) rubyRects[3].gameObject.SetActive(showQuit);
    }

    public bool HandleBack()
    {
        if (quitting || closing) return true;
        if (!confirmOpen) return false;
        CloseConfirm();
        return true;
    }

    public void UpdateMenu(float dt, bool up, bool down, bool leftPress, bool rightPress, bool leftHeld, bool rightHeld, bool button)
    {
        EnsureInit();
        if (quitting || closing) return;
        if (waitingForMenuCapture) return;

        if (openAnimT < 1f && group != null)
        {
            openAnimT = Mathf.Min(1f, openAnimT + dt / 0.18f);
            float e = 1f - (1f - openAnimT) * (1f - openAnimT);
            group.alpha = e;
            transform.localScale = Vector3.one * (0.97f + 0.03f * e);
        }

        if (headerAnimT < 1f)
        {
            headerAnimT = Mathf.Min(1f, headerAnimT + dt / 0.32f);
            ApplyHeaderEntrance(headerAnimT);
        }

        if (confirmOpen)
        {
            if (leftPress || rightPress)
            {
                confirmIndex = confirmIndex == 0 ? 1 : 0;
                RefreshConfirm();
            }
            if (button)
            {
                if (confirmIndex == 0)
                {
                    quitting = true;
                    GManager.Control.QuitPlay();
                }
                else CloseConfirm();
            }
        }
        else
        {
            // タイトル文脈では終了行を隠しているので、選択は row 2 までに留める。
            int maxIndex = titleContext ? 2 : rowY.Length - 1;
            if (up && index > 0) index--;
            else if (down && index < maxIndex) index++;

            if (index == 1 && (leftHeld || rightHeld))
            {
                float direction = (rightHeld ? 1f : 0f) - (leftHeld ? 1f : 0f);
                AudioListener.volume = Mathf.Clamp01(AudioListener.volume + direction * 1.5f * dt);
                RefreshSlider();
            }
            else if (index == 2)
            {
                bool nextEffectsOn = effectsOn;
                if (leftPress) nextEffectsOn = false;
                else if (rightPress) nextEffectsOn = true;
                else if (button) nextEffectsOn = !effectsOn;

                if (nextEffectsOn != effectsOn)
                {
                    effectsOn = nextEffectsOn;
                    RefreshEffects();
                }
            }

            if (button)
            {
                if (index == 0)
                {
                    if (titleContext) titleResumeRequest?.Invoke();
                    else BeginResume();
                }
                else if (index == 3 && !titleContext) OpenConfirm();
            }
        }

        bandY = Mathf.Lerp(bandY, rowY[index], 1f - Mathf.Exp(-14f * dt));
        if (selectBand != null) selectBand.anchoredPosition = new Vector2(0f, bandY);
        for (int i = 0; i < 4; i++)
        {
            bool selected = i == index && !confirmOpen;
            if (badges[i] != null)
            {
                badges[i].sizeDelta = Vector2.one * (selected ? 24f : 16f);
                badges[i].localScale = Vector3.one;
                badgeRotationOffset[i] = Mathf.MoveTowards(
                    badgeRotationOffset[i], selected ? 90f : 0f, 540f * dt);
                badges[i].localEulerAngles = new Vector3(0f, 0f, badgeBaseRotationZ[i] + badgeRotationOffset[i]);
            }
            itemOffset[i] = Mathf.Lerp(itemOffset[i], selected ? selectedShiftX : 0f, 1f - Mathf.Exp(-12f * dt));
            if (itemRects[i] != null)
            {
                itemRects[i].anchoredPosition = new Vector2(itemBaseX + itemOffset[i], rowY[i] + itemVisualOffsetY[i] + textBlockShiftY);
            }
            if (rubyRects[i] != null)
            {
                rubyRects[i].anchoredPosition = new Vector2(rubyBaseX[i] + itemOffset[i], rubyBaseY[i] + textBlockShiftY);
            }
            if (items[i] != null) items[i].color = selected ? Color.white : unselectedColor;
        }

        bool volumeSelected = index == 1 && !confirmOpen;
        sliderKnob.sizeDelta = Vector2.one * (volumeSelected ? 54f : 48f);
        sliderKnobGraphic.color = new Color(0.05f, 0.06f, 0.10f, 1f);
    }

    private void OpenConfirm()
    {
        confirmOpen = true;
        confirmIndex = 1;
        // Lock the full-screen option root at its final size before capturing.
        // Otherwise the menu entrance scale can leave a visible seam at the
        // screen edges when the confirmation backdrop appears.
        openAnimT = 1f;
        if (group != null) group.alpha = 1f;
        transform.localScale = Vector3.one;
        headerAnimT = 1f;
        ApplyHeaderEntrance(1f);
        confirmGroup.SetActive(false);
        RefreshConfirm();
        if (confirmCaptureRoutine != null) StopCoroutine(confirmCaptureRoutine);
        confirmCaptureRoutine = StartCoroutine(CaptureConfirmBackdrop());
    }

    public async void BeginResume()
    {
        if (closing || quitting || confirmOpen) return;
        closing = true;

        const float closeDuration = 0.24f;
        float time = 0f;
        while (time < closeDuration)
        {
            time += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(time / closeDuration);
            float ease = p * p * (3f - 2f * p);
            if (group != null) group.alpha = 1f - ease;
            transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.985f, ease);
            await Task.Yield();
            if (this == null) return;
        }

        gameObject.SetActive(false);
        transform.localScale = Vector3.one;
        if (group != null) group.alpha = 1f;

        if (countdownGroup != null)
        {
            countdownGroup.gameObject.SetActive(true);
            for (int number = 3; number >= 1; number--)
            {
                await ShowCountdownNumber(number.ToString());
                if (this == null) return;
            }
            countdownGroup.alpha = 0f;
            countdownGroup.gameObject.SetActive(false);
        }

        closing = false;
        if (GManager.Control != null) GManager.Control.SetPaused(false);
    }

    // タイトル画面から開いた設定用のクローズ。BeginResume と違いカウントダウンや
    // SetPaused を伴わず、短いフェードアウトだけで閉じる(背後ではタイトルが動き
    // 続けているので、閉じた後の追加演出は不要)。
    public async void CloseForTitle(System.Action onClosed)
    {
        if (quitting) return;
        if (closing) { onClosed?.Invoke(); return; }
        EnsureInit();
        closing = true;

        const float closeDuration = 0.18f;
        float time = 0f;
        while (time < closeDuration)
        {
            time += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(time / closeDuration);
            float ease = p * p * (3f - 2f * p);
            if (group != null) group.alpha = 1f - ease;
            transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.985f, ease);
            await Task.Yield();
            if (this == null) return;
            // フェード中に再オープンされたら中断する(Open が状態を作り直す)。
            if (!closing) return;
        }

        closing = false;
        gameObject.SetActive(false);
        transform.localScale = Vector3.one;
        if (group != null) group.alpha = 1f;
        onClosed?.Invoke();
    }

    private async Task ShowCountdownNumber(string value)
    {
        countdownText.text = value;
        RectTransform rect = countdownText.rectTransform;
        float time = 0f;
        const float duration = 0.58f;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(time / duration);
            float appear = Mathf.Clamp01(p / 0.16f);
            float disappear = Mathf.Clamp01((1f - p) / 0.22f);
            countdownGroup.alpha = Mathf.Min(appear, disappear);
            float ease = 1f - Mathf.Pow(1f - p, 3f);
            rect.localScale = Vector3.one * Mathf.Lerp(1.38f, 1f, ease);
            await Task.Yield();
            if (this == null) return;
        }
        countdownGroup.alpha = 0f;
        rect.localScale = Vector3.one;
    }

    private IEnumerator CaptureMenuBackdrop()
    {
        // Capture the completed gameplay frame while the option CanvasGroup is
        // still fully transparent, then fade the full-resolution blur in.
        yield return new WaitForEndOfFrame();
        menuCaptureRoutine = null;
        if (!gameObject.activeInHierarchy) yield break;

        BackdropBlurUtil.ReleaseRT(ref menuBlurRT);
        menuBlurRT = BackdropBlurUtil.CapturePyramidBlur();
        menuBackdrop.texture = menuBlurRT;
        menuBackdrop.gameObject.SetActive(true);
        menuShade.SetActive(true);
        waitingForMenuCapture = false;
        openAnimT = 0f;
        if (group != null) group.alpha = 0f;
    }

    private IEnumerator CaptureConfirmBackdrop()
    {
        // Capture only after the settings frame has finished rendering. Doing
        // this synchronously during input handling can return a white buffer.
        yield return new WaitForEndOfFrame();
        confirmCaptureRoutine = null;
        if (!confirmOpen) yield break;

        BackdropBlurUtil.ReleaseRT(ref confirmBlurRT);
        confirmBlurRT = BackdropBlurUtil.CapturePyramidBlur();
        confirmBackdrop.texture = confirmBlurRT;
        confirmGroup.SetActive(true);
        confirmGroup.transform.SetAsLastSibling();
        confirmGroup.transform.localScale = Vector3.one;
        SetConfirmContentScale(0.94f);
        confirmCanvasGroup.alpha = 0f;
        RefreshConfirm();
        if (confirmOpenAnimationRoutine != null) StopCoroutine(confirmOpenAnimationRoutine);
        confirmOpenAnimationRoutine = StartCoroutine(AnimateConfirmOpen());
    }

    private IEnumerator AnimateConfirmOpen()
    {
        float time = 0f;
        const float duration = 0.18f;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(time / duration);
            float ease = 1f - Mathf.Pow(1f - p, 3f);
            confirmCanvasGroup.alpha = ease;
            SetConfirmContentScale(Mathf.Lerp(0.94f, 1f, ease));
            yield return null;
        }

        confirmCanvasGroup.alpha = 1f;
        confirmGroup.transform.localScale = Vector3.one;
        SetConfirmContentScale(1f);
        confirmOpenAnimationRoutine = null;
    }

    private void SetConfirmContentScale(float scale)
    {
        confirmContentScale = scale;
        if (confirmTitle != null) confirmTitle.rectTransform.localScale = Vector3.one * scale;
        ApplyConfirmSelectionScale();
    }

    // 統一便: シーン authored のベタ塗りヘッダー帯(1920x120)を、リザルト画面の
    // ヘッダー様式(ブランド青横グラデ主帯→白スラッシュ仕切り→濃紺副帯+
    // 金属エッジ)へ差し替える。帯 rect とアイコン/見出し/ルビの配置は現状維持。
    // 斜辺の角度(atan(34/106))とスラッシュ右側の対称12pxギャップはリザルトと
    // スクリーン座標で一致させる。
    private void RestyleHeaderBand()
    {
        RectTransform bar = headerRects[0];
        if (bar == null) return;
        Image barImage = bar.GetComponent<Image>();
        if (barImage == null) return;
        int barW = Mathf.RoundToInt(bar.sizeDelta.x);
        int barH = Mathf.RoundToInt(bar.sizeDelta.y);
        barImage.sprite = CreateHeaderBandSprite(barW, barH);
        barImage.color = Color.white;
        barImage.type = Image.Type.Simple;

        float skew = barH * (34f / 106f);
        float lineW = 36f * (barH / 106f);
        GameObject slashObj = new GameObject("HeaderSlash", typeof(RectTransform), typeof(CanvasRenderer), typeof(ParallelogramGraphic));
        slashObj.layer = bar.gameObject.layer;
        RectTransform slashRect = (RectTransform)slashObj.transform;
        slashRect.SetParent(bar, false);
        slashRect.anchorMin = slashRect.anchorMax = new Vector2(0.5f, 0.5f);
        slashRect.pivot = new Vector2(0.5f, 0.5f);
        slashRect.anchoredPosition = new Vector2(337f, 0f);   // 画面座標 1297 中心(リザルトと一致)
        slashRect.sizeDelta = new Vector2(lineW + skew, barH);
        ParallelogramGraphic slash = slashObj.GetComponent<ParallelogramGraphic>();
        slash.Slant = skew;
        slash.SlantRightEdge = true;
        slash.color = Color.white;
        slash.raycastTarget = false;
    }

    // リザルト BuildHeaderBanner の帯を1枚に焼き込む(視覚 sRGB 値)。
    // 主帯は左濃青→右鮮青(#014190→#026CDB)・右端斜め、副帯は一段暗い
    // (#01356E→#011835)・左端斜め。上辺銀/下辺沈みの金属エッジも帯内に焼く。
    private Sprite CreateHeaderBandSprite(int W, int H)
    {
        Texture2D texture = new Texture2D(W, H, TextureFormat.RGBA32, false);
        texture.name = "OptionHeaderBandTexture";
        texture.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[W * H];
        float skew = H * (34f / 106f);
        Color mainL = new Color(0.004f, 0.255f, 0.565f);
        Color mainR = new Color(0.008f, 0.424f, 0.859f);
        Color subL = new Color(0.004f, 0.208f, 0.431f);
        Color subR = new Color(0.005f, 0.095f, 0.208f);
        Color edgeHi = new Color(0.55f, 0.60f, 0.70f);
        Color edgeLo = new Color(0.004f, 0.03f, 0.09f);
        const float mainTopRight = 1250f;   // 主帯右端(上辺)の x。リザルトと一致
        const float subBottomLeft = 1310f;  // 副帯左端(下辺)の x。リザルトと一致
        for (int y = 0; y < H; y++)
        {
            float t = y / (float)(H - 1);              // 0 下端 .. 1 上端
            float edgeMain = (mainTopRight - skew) + skew * t;
            float edgeSub = subBottomLeft + skew * t;
            for (int x = 0; x < W; x++)
            {
                float a;
                Color c;
                if (x < edgeMain + 1f)
                {
                    a = Mathf.Clamp01(edgeMain - x);
                    c = Color.Lerp(mainL, mainR, Mathf.Clamp01(x / mainTopRight));
                }
                else if (x > edgeSub - 1f)
                {
                    a = Mathf.Clamp01(x - edgeSub);
                    c = Color.Lerp(subL, subR, Mathf.Clamp01((x - subBottomLeft) / (W - subBottomLeft)));
                }
                else continue;
                if (a <= 0f) continue;
                if (y >= H - 3) c = Color.Lerp(c, edgeHi, 0.85f);
                else if (y < 3) c = Color.Lerp(c, edgeLo, 0.85f);
                px[y * W + x] = new Color(c.r, c.g, c.b, a);
            }
        }
        texture.SetPixels32(px);
        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, W, H), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = "OptionHeaderBand";
        return sprite;
    }

    private void CloseConfirm()
    {
        confirmOpen = false;
        if (confirmOpenAnimationRoutine != null)
        {
            StopCoroutine(confirmOpenAnimationRoutine);
            confirmOpenAnimationRoutine = null;
        }
        if (confirmCaptureRoutine != null)
        {
            StopCoroutine(confirmCaptureRoutine);
            confirmCaptureRoutine = null;
        }
        confirmGroup.SetActive(false);
        BackdropBlurUtil.ReleaseRT(ref confirmBlurRT);
        confirmBackdrop.texture = null;
    }

    private void RefreshConfirm()
    {
        bool yes = confirmIndex == 0;
        yesButton.rectTransform.anchoredPosition = yesButtonPosition;
        noButton.rectTransform.anchoredPosition = noButtonPosition;
        yesText.rectTransform.anchoredPosition = yesButtonPosition + new Vector2(0f, confirmButtonTextOffsetY);
        noText.rectTransform.anchoredPosition = noButtonPosition + new Vector2(0f, confirmButtonTextOffsetY);
        // 選択=等倍表示、非選択=減光(スラッシュ・枠ごと沈める)。
        if (yesButtonGroup != null) yesButtonGroup.alpha = yes ? 1f : 0.5f;
        if (noButtonGroup != null) noButtonGroup.alpha = yes ? 0.5f : 1f;
        yesText.color = yes ? Color.white : unselectedColor;
        noText.color = yes ? unselectedColor : Color.white;
        ApplyConfirmSelectionScale();
    }

    private void ApplyConfirmSelectionScale()
    {
        float yesScale = confirmContentScale * (confirmIndex == 0 ? 1.12f : 0.96f);
        float noScale = confirmContentScale * (confirmIndex == 1 ? 1.12f : 0.96f);
        if (yesButton != null) yesButton.rectTransform.localScale = Vector3.one * yesScale;
        if (noButton != null) noButton.rectTransform.localScale = Vector3.one * noScale;
        if (yesText != null) yesText.rectTransform.localScale = Vector3.one * yesScale;
        if (noText != null) noText.rectTransform.localScale = Vector3.one * noScale;
    }

    private void RefreshSlider()
    {
        if (sliderFill == null) return;
        float volume = Mathf.Clamp01(AudioListener.volume);
        sliderFill.sizeDelta = new Vector2(sliderWidth * volume, sliderFill.sizeDelta.y);
        sliderKnob.anchoredPosition = new Vector2(-sliderWidth * 0.5f + sliderWidth * volume, 0f);
    }

    private void RefreshEffects()
    {
        toggleTrack.color = effectsOn
            ? new Color(0.04f, 0.27f, 0.47f, 1f)
            : new Color(0.035f, 0.055f, 0.085f, 1f);
        toggleKnob.anchoredPosition = new Vector2(effectsOn ? 49f : -49f, 0f);
        toggleKnob.localEulerAngles = Vector3.zero;
        toggleKnobGraphic.color = effectsOn ? Color.white : new Color(0.70f, 0.75f, 0.82f, 1f);
        toggleStateText.text = effectsOn ? "ON" : "OFF";
        toggleStateText.color = effectsOn ? new Color(0.62f, 0.88f, 1f) : new Color(0.86f, 0.90f, 0.96f);
    }

    private GameObject CreateImage(string objectName, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
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
        label.font = items[0].font;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    private static void SetupButtonText(TMP_Text text, Vector2 position)
    {
        text.rectTransform.anchoredPosition = position + new Vector2(0f, confirmButtonTextOffsetY);
        text.rectTransform.sizeDelta = new Vector2(260f, 86f);
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 32f;
        text.fontStyle = FontStyles.Bold;
    }

    private void ApplyHeaderEntrance(float normalizedTime)
    {
        for (int i = 0; i < headerRects.Length; i++)
        {
            if (headerRects[i] == null) continue;
            float p = Mathf.Clamp01(normalizedTime * 1.18f - i * 0.06f);
            float ease = 1f - Mathf.Pow(1f - p, 3f);
            headerRects[i].anchoredPosition = headerBasePositions[i] + Vector2.up * (150f * (1f - ease));
        }
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void OnDisable()
    {
        waitingForMenuCapture = false;
        if (confirmOpenAnimationRoutine != null)
        {
            StopCoroutine(confirmOpenAnimationRoutine);
            confirmOpenAnimationRoutine = null;
        }
        if (menuCaptureRoutine != null)
        {
            StopCoroutine(menuCaptureRoutine);
            menuCaptureRoutine = null;
        }
        if (confirmCaptureRoutine != null)
        {
            StopCoroutine(confirmCaptureRoutine);
            confirmCaptureRoutine = null;
        }
        BackdropBlurUtil.ReleaseRT(ref confirmBlurRT);
        if (confirmBackdrop != null) confirmBackdrop.texture = null;
        BackdropBlurUtil.ReleaseRT(ref menuBlurRT);
        if (menuBackdrop != null) menuBackdrop.texture = null;
    }

    private void OnDestroy()
    {
        BackdropBlurUtil.ReleaseRT(ref confirmBlurRT);
        BackdropBlurUtil.ReleaseRT(ref menuBlurRT);
    }
}

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
    // Highland UI v11 の実座標(SVG 1672x941 → 1920x1080 は 1.1483 倍)。
    // 行の中心 y: 続ける 323 / 音量 408 / エフェクト 493 / 終了する 577。
    private static readonly float[] rowY = { 169.4f, 71.8f, -25.8f, -122.3f };
    private const float itemBaseX = -318f;      // v11: ラベル左端 x=559
    private const float sliderWidth = 351.4f;   // v11: 306 * 1.1483
    private const float selectedShiftX = 10f;
    private const float textBlockShiftY = -10f;
    // CJK フォールバックの行メトリクスで上に乗る分の光学補正。フォントサイズに
    // 比例する(32px 時 -7 → 25px 時 -5.5)。
    private const float confirmButtonTextOffsetY = -5.5f;
    // v11 の終了確認(パネル中心からの相対)。
    private static readonly Vector2 yesButtonPosition = new Vector2(-124.6f, -45.4f);
    private static readonly Vector2 noButtonPosition = new Vector2(123.2f, -45.4f);
    private static readonly Vector2 confirmButtonSize = new Vector2(210f, 68f);

    private static readonly Color unselectedColor = new Color(0.949f, 0.949f, 0.949f);
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

    // v11 の部品(2026-09-19 U7)。
    private readonly System.Collections.Generic.List<Texture2D> ownedTextures
        = new System.Collections.Generic.List<Texture2D>();
    private readonly System.Collections.Generic.List<Sprite> ownedSprites
        = new System.Collections.Generic.List<Sprite>();
    private readonly HighlandUi.RubyText[] itemRuby = new HighlandUi.RubyText[4];
    private readonly Image[] rowPlates = new Image[4];
    private readonly Image[] rowGems = new Image[4];
    private Sprite rowPlateSprite, rowChosenSprite;
    private Image effectsOnPlate, effectsOffPlate;
    private Sprite effectsChosen, effectsPlain;
    private Sprite confirmChosen, confirmPlain;
    private TMP_Text effectsOnText, effectsOffText;
    private HighlandUi.RubyText confirmTitleRuby;

    private RectTransform sliderFill;
    private RectTransform sliderKnob;
    private Graphic sliderKnobGraphic;
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
    // 終了確認は装飾を抑えた単色ボタン。選択状態は明度と細い輪郭で示す。
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
    private string quitItemTextOriginal;
    private bool quitRubyActiveOriginal;

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

        quitItemTextOriginal = "ステージを [終了|しゅうりょう]する";
        quitRubyActiveOriginal = rubyRects[3] != null && rubyRects[3].gameObject.activeSelf;

        // v11: 行ラベルは自前のふりがな付きへ。シーンのルビは畳む。
        string[] markup =
        {
            "[続|つづ]ける", "[音量|おんりょう]", "エフェクト", "ステージを [終了|しゅうりょう]する",
        };
        for (int i = 0; i < 4; i++)
        {
            if (rubyRects[i] != null) rubyRects[i].gameObject.SetActive(false);
            if (badges[i] != null) badges[i].gameObject.SetActive(false);
            if (items[i] == null) continue;
            items[i].font = HighlandUi.Serif;
            items[i].fontSize = 25f * HighlandUi.S;
            items[i].characterSpacing = 1.1f / 25f * 100f;
            items[i].alignment = TextAlignmentOptions.Left;
            items[i].rectTransform.pivot = new Vector2(0f, 0.5f);
            items[i].rectTransform.sizeDelta = new Vector2(520f, 25f * HighlandUi.S * 1.6f);
            itemVisualOffsetY[i] = 0f;
            itemRuby[i] = new HighlandUi.RubyText(items[i], items[i].transform.parent,
                25f, HighlandUi.InkSoft);
            itemRuby[i].Apply(markup[i]);
        }
        if (selectBand != null) selectBand.gameObject.SetActive(false);
        for (int i = 0; i < headerRects.Length; i++)
            if (headerRects[i] != null) headerRects[i].gameObject.SetActive(false);

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

        BuildV11Frame();

        RectTransform sliderBack = sliderFill.parent as RectTransform;
        sliderBack.anchoredPosition = new Vector2(HighlandUi.X(950f), HighlandUi.Y(408.5f));
        sliderBack.sizeDelta = new Vector2(sliderWidth, 12f);
        Image sliderBackImg = sliderBack.GetComponent<Image>();
        if (sliderBackImg != null) sliderBackImg.color = new Color(0.8f, 0.8f, 0.8f, 0.3f);
        sliderFill.sizeDelta = new Vector2(sliderWidth, HighlandUi.L(3f));
        Image sliderFillImg = sliderFill.GetComponent<Image>();
        if (sliderFillImg != null) sliderFillImg.color = Color.white;
        // 溝は 3px の細線。背景の Image は薄い灰にして高さだけ合わせる。
        sliderBack.sizeDelta = new Vector2(sliderWidth, HighlandUi.L(3f));

        // 両端の小さな中空菱形(v11 の O_Volume_Min / Max)。
        foreach (float sx in new[] { 781f, 1118f })
        {
            Image d = NewV11Image("VolumeEnd", transform, new Color(0.867f, 0.769f, 0.318f, 1f));
            d.sprite = HighlandUi.DiamondRect(9, 11, false, 1f * HighlandUi.S,
                ownedTextures, ownedSprites, "OptVolEnd");
            SetV11(d.rectTransform, sx, 408.5f, 8f, 9.6f);
        }

        // つまみ(v11 の八角形)。
        GameObject knobObject = new GameObject("SliderKnob", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        knobObject.layer = gameObject.layer;
        sliderKnob = (RectTransform)knobObject.transform;
        sliderKnob.SetParent(sliderBack, false);
        sliderKnob.anchorMin = sliderKnob.anchorMax = new Vector2(0.5f, 0.5f);
        sliderKnob.sizeDelta = Vector2.one * HighlandUi.L(26f);
        Image knobImg = knobObject.GetComponent<Image>();
        knobImg.sprite = HighlandUi.NotchFlat(30, 30, 9f, new Color32(0x38, 0x3C, 0x48, 0xFF), 1f,
            new Color32(0xFF, 0xFF, 0xFF, 0xFF), 2f, 1f, ownedTextures, ownedSprites, "OptKnob");
        knobImg.raycastTarget = false;
        sliderKnobGraphic = knobImg;

        // エフェクトの「あり」「なし」(v11 は独立した 2 つのボタン)。
        effectsOnPlate = NewV11Image("EffectsOn", transform, Color.white);
        SetV11(effectsOnPlate.rectTransform, 947f, 492.5f, 98f, 49f);
        effectsOffPlate = NewV11Image("EffectsOff", transform, Color.white);
        SetV11(effectsOffPlate.rectTransform, 1070f, 492.5f, 98f, 49f);
        effectsChosen = HighlandUi.NotchPanel((int)HighlandUi.L(98f), (int)HighlandUi.L(49f),
            HighlandUi.L(4.5f), true, ownedTextures, ownedSprites, "OptEffOn",
            HighlandUi.L(3.5f), 1.5f * HighlandUi.S, 0.7f * HighlandUi.S);
        effectsPlain = HighlandUi.NotchFlat((int)HighlandUi.L(98f), (int)HighlandUi.L(49f),
            HighlandUi.L(4.5f), new Color32(0x11, 0x11, 0x29, 0xFF), 0.5f,
            new Color32(0xBB, 0xBB, 0xBB, 0xFF), 1.1f * HighlandUi.S, 0.58f,
            ownedTextures, ownedSprites, "OptEffOff");
        effectsOnText = HighlandUi.Text("EffectsOnText", transform, "あり", 20f,
            HighlandUi.Ink, TextAlignmentOptions.Center, false, 1.1f);
        HighlandUi.PlaceCentered(effectsOnText, 947f, 500f, 120f, 20f);
        effectsOffText = HighlandUi.Text("EffectsOffText", transform, "なし", 20f,
            HighlandUi.InkSoft, TextAlignmentOptions.Center, false, 1.1f);
        HighlandUi.PlaceCentered(effectsOffText, 1070f, 500f, 120f, 20f);
        toggleStateText = effectsOnText;

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

        // v11 の確認パネル(四隅をえぐった二重枠)。
        Image confirmPanel = NewV11Image("ConfirmPanel", confirmGroup.transform, Color.white);
        confirmPanel.sprite = HighlandUi.NotchPanel((int)HighlandUi.L(508f), (int)HighlandUi.L(198f),
            HighlandUi.L(13.5f), false, ownedTextures, ownedSprites, "OptConfirmPanel",
            HighlandUi.L(5.5f), 1.65f * HighlandUi.S, 0.7f * HighlandUi.S);
        confirmPanel.rectTransform.anchoredPosition = Vector2.zero;
        confirmPanel.rectTransform.sizeDelta = new Vector2(HighlandUi.L(508f), HighlandUi.L(198f));

        AddV11Rule(confirmGroup.transform, 20.7f, 380f, 34f, "OptConfirmRule");

        confirmTitle.font = HighlandUi.Serif;
        confirmTitle.rectTransform.anchoredPosition = new Vector2(0f, 56.8f);
        confirmTitle.rectTransform.sizeDelta = new Vector2(HighlandUi.L(460f), 25f * HighlandUi.S * 1.6f);
        confirmTitle.alignment = TextAlignmentOptions.Center;
        confirmTitle.fontSize = 25f * HighlandUi.S;
        confirmTitle.characterSpacing = 1.25f / 25f * 100f;
        confirmTitleRuby = new HighlandUi.RubyText(confirmTitle, confirmGroup.transform, 25f, HighlandUi.InkSoft);
        confirmTitleRuby.Apply("ステージを [終了|しゅうりょう]しますか？");
        confirmDetail.text = string.Empty;
        confirmDetail.gameObject.SetActive(false);

        yesButton = CreateImage("YesButton", confirmGroup.transform, yesButtonPosition,
            confirmButtonSize, Color.white).GetComponent<Image>();
        noButton = CreateImage("NoButton", confirmGroup.transform, noButtonPosition,
            confirmButtonSize, Color.white).GetComponent<Image>();
        confirmChosen = HighlandUi.NotchPanel((int)confirmButtonSize.x, (int)confirmButtonSize.y,
            HighlandUi.L(6f), true, ownedTextures, ownedSprites, "OptConfirmYes",
            HighlandUi.L(4f), 1.6f * HighlandUi.S, 0.7f * HighlandUi.S);
        confirmPlain = HighlandUi.NotchFlat((int)confirmButtonSize.x, (int)confirmButtonSize.y,
            HighlandUi.L(6f), new Color32(0x11, 0x11, 0x29, 0xFF), 0.5f,
            new Color32(0xB2, 0xB2, 0xB9, 0xFF), 1f * HighlandUi.S, 0.65f,
            ownedTextures, ownedSprites, "OptConfirmNo");
        yesButtonGroup = yesButton.gameObject.AddComponent<CanvasGroup>();
        noButtonGroup = noButton.gameObject.AddComponent<CanvasGroup>();

        // 〇 / ✕ の操作アイコン(✕ は「戻る」の記号)。
        Image yesIcon = NewV11Image("YesIcon", confirmGroup.transform, Color.white);
        yesIcon.sprite = HighlandUi.Icon("circle");
        yesIcon.rectTransform.anchoredPosition = yesButtonPosition + new Vector2(HighlandUi.L(-31.3f), HighlandUi.L(1.5f));
        yesIcon.rectTransform.sizeDelta = Vector2.one * HighlandUi.L(39.5f);
        Image noIcon = NewV11Image("NoIcon", confirmGroup.transform, Color.white);
        noIcon.sprite = HighlandUi.Icon("cross");
        noIcon.rectTransform.anchoredPosition = noButtonPosition + new Vector2(HighlandUi.L(-31.3f), HighlandUi.L(1.5f));
        noIcon.rectTransform.sizeDelta = Vector2.one * HighlandUi.L(39.5f);

        yesText.font = HighlandUi.Serif;
        noText.font = HighlandUi.Serif;
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
    // =======================================================================
    //  事前ビルド(第 U9 便)
    // =======================================================================
    // 設定画面は初回オープンで組み立てていたため、1 フレームが 2107ms 止まっていた
    // (v11 の板 1 枚が 200 万画素を超える)。タイトル表示中に、
    //   1) 板の画素をワーカースレッドで塗る(PrebakeV11Sprites)
    //   2) 塗り終わってから EnsureInit(転送と組み立てだけ)
    //   3) 見えない状態で 2 フレーム出して TMP のメッシュとマテリアルを暖める
    // の順で済ませておく。引数は BuildFriendlyVisuals / BuildV11Frame と同じ値。
    private bool prewarmed;

    public static void PrebakeV11Sprites()
    {
        float S = HighlandUi.S;
        // スライダーのつまみ
        HighlandUi.PrebakeNotchFlat(30, 30, 9f, new Color32(0x38, 0x3C, 0x48, 0xFF), 1f,
            new Color32(0xFF, 0xFF, 0xFF, 0xFF), 2f, 1f);
        // エフェクトの「あり」「なし」
        HighlandUi.PrebakeNotchPanel((int)HighlandUi.L(98f), (int)HighlandUi.L(49f),
            HighlandUi.L(4.5f), true, HighlandUi.L(3.5f), 1.5f * S, 0.7f * S);
        HighlandUi.PrebakeNotchFlat((int)HighlandUi.L(98f), (int)HighlandUi.L(49f),
            HighlandUi.L(4.5f), new Color32(0x11, 0x11, 0x29, 0xFF), 0.5f,
            new Color32(0xBB, 0xBB, 0xBB, 0xFF), 1.1f * S, 0.58f);
        // 終了確認のパネルとボタン
        HighlandUi.PrebakeNotchPanel((int)HighlandUi.L(508f), (int)HighlandUi.L(198f),
            HighlandUi.L(13.5f), false, HighlandUi.L(5.5f), 1.65f * S, 0.7f * S);
        HighlandUi.PrebakeNotchPanel((int)confirmButtonSize.x, (int)confirmButtonSize.y,
            HighlandUi.L(6f), true, HighlandUi.L(4f), 1.6f * S, 0.7f * S);
        HighlandUi.PrebakeNotchFlat((int)confirmButtonSize.x, (int)confirmButtonSize.y,
            HighlandUi.L(6f), new Color32(0x11, 0x11, 0x29, 0xFF), 0.5f,
            new Color32(0xB2, 0xB2, 0xB9, 0xFF), 1f * S, 0.65f);
        // 本体のパネルと行の板(いちばん重い 3 枚)
        HighlandUi.PrebakeNotchPanel((int)HighlandUi.L(765f), (int)HighlandUi.L(544f),
            HighlandUi.L(19f), false, HighlandUi.L(6f), 1.65f * S, 0.7f * S);
        HighlandUi.PrebakeNotchFlat((int)HighlandUi.L(642f), (int)HighlandUi.L(68f),
            HighlandUi.L(6f), new Color32(0x16, 0x16, 0x2D, 0xFF), 0.36f,
            new Color32(0xA4, 0xA4, 0xAE, 0xFF), 1f * S, 0.62f);
        HighlandUi.PrebakeNotchPanel((int)HighlandUi.L(642f), (int)HighlandUi.L(68f),
            HighlandUi.L(6f), true, HighlandUi.L(4f), 1.8f * S, 0.7f * S);
    }

    /// <summary>タイトル表示中に設定画面を作っておく(初回オープンのハング対策)。</summary>
    public IEnumerator PrewarmRoutine()
    {
        if (prewarmed) yield break;
        prewarmed = true;

        bool wasActive = gameObject.activeSelf;
        if (!initialized)
        {
            PrebakeV11Sprites();
            // 塗り終わるまでは主スレッドを空けておく(実測 529ms・待ち上限 8 秒)。
            float t = 0f;
            while (!HighlandUi.PrebakeDone() && t < 8f) { t += Time.unscaledDeltaTime; yield return null; }
            EnsureInit();   // 転送と組み立てだけなので実測 47ms
            yield return null;
        }

        // TMP のメッシュ・マテリアル・シェーダ変種を、見えない状態で暖める。
        CanvasGroup cg = group != null ? group : GetComponent<CanvasGroup>();
        float alpha0 = cg != null ? cg.alpha : 1f;
        if (cg != null) cg.alpha = 0f;
        gameObject.SetActive(true);
        yield return null;
        yield return new WaitForEndOfFrame();
        gameObject.SetActive(wasActive);
        if (cg != null) cg.alpha = alpha0;
    }

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
    // プレイヤー左右入れ替えはデバッグ用のため、通常の設定画面には露出させない。
    private void ApplyContextVisibility()
    {
        if (items[3] != null)
        {
            items[3].gameObject.SetActive(!titleContext);
            // v11: 文言は「ステージを 終了する」。ふりがなは RubyText が持つので
            // 本文を直接代入せず Apply で入れ直す(読みの位置が狂わないように)。
            if (itemRuby[3] != null) itemRuby[3].Apply(quitItemTextOriginal);
            else items[3].text = quitItemTextOriginal;
        }
        // 旧シーンの飾り(菱形・ルビ)は v11 では使わない。
        if (badges[3] != null) badges[3].gameObject.SetActive(false);
        if (rubyRects[3] != null) rubyRects[3].gameObject.SetActive(false);
        if (itemRuby[3] != null) itemRuby[3].SetAlpha(titleContext ? 0f : 1f);
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
            // タイトルから開くときは、カメラの寄りに重なるようフェードを伸ばす
            // (第 U8 便・2026-09-19 指示「寄りながら表示」)。プレイ中のポーズは従来どおり。
            // dt はクランプする。初回オープンは EnsureInit がスプライトを焼くぶん
            // 1 フレームが数百 ms 掛かり、その dt をそのまま足すとフェードが
            // 1 コマで終わってしまう(第 U8 便で実測)。
            float step = Mathf.Min(dt, 0.05f) / (titleContext ? TitleManager.PanelFadeIn : 0.18f);
            openAnimT = Mathf.Min(1f, openAnimT + step);
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
            int maxIndex = titleContext ? rowY.Length - 2 : rowY.Length - 1;
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
        RefreshV11Rows();

        bool volumeSelected = index == 1 && !confirmOpen;
        if (sliderKnob != null)
            sliderKnob.sizeDelta = Vector2.one * HighlandUi.L(volumeSelected ? 29f : 26f);
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
        // 撮影フレームだけ明部ウィジェットを隠す。ぼかしに写った白いスライダー
        // バー等はシェード(α0.94)越しでも知覚的に残り、「ダイアログの上に
        // スライダーが貫通表示」に見える(2026-07-11 指摘)。撮影後に戻す。
        SetControlWidgetsVisible(false);
        RefreshConfirm();
        if (confirmCaptureRoutine != null) StopCoroutine(confirmCaptureRoutine);
        confirmCaptureRoutine = StartCoroutine(CaptureConfirmBackdrop());
    }

    // 音量スライダーとエフェクトトグル(明るい前景ウィジェット)の表示切替。
    // 確認ダイアログの背景ぼかし撮影中だけ false にする。
    private void SetControlWidgetsVisible(bool visible)
    {
        if (sliderKnob != null && sliderKnob.parent != null)
            sliderKnob.parent.gameObject.SetActive(visible);
        if (effectsOnPlate != null) effectsOnPlate.gameObject.SetActive(visible);
        if (effectsOffPlate != null) effectsOffPlate.gameObject.SetActive(visible);
        if (effectsOnText != null) effectsOnText.gameObject.SetActive(visible);
        if (effectsOffText != null) effectsOffText.gameObject.SetActive(visible);
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

        float closeDuration = TitleManager.PanelFadeOut;   // 第 U8 便: 引きながら消える
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
        if (!confirmOpen)
        {
            SetControlWidgetsVisible(true);
            yield break;
        }

        BackdropBlurUtil.ReleaseRT(ref confirmBlurRT);
        confirmBlurRT = BackdropBlurUtil.CapturePyramidBlur();
        SetControlWidgetsVisible(true);
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
        yesText.rectTransform.anchoredPosition = yesButtonPosition + new Vector2(HighlandUi.L(-2.5f), confirmButtonTextOffsetY);
        noText.rectTransform.anchoredPosition = noButtonPosition + new Vector2(HighlandUi.L(-2.5f), confirmButtonTextOffsetY);
        if (yesButtonGroup != null) yesButtonGroup.alpha = 1f;
        if (noButtonGroup != null) noButtonGroup.alpha = 1f;
        yesButton.color = Color.white;
        noButton.color = Color.white;
        yesButton.sprite = yes ? confirmChosen : confirmPlain;
        noButton.sprite = yes ? confirmPlain : confirmChosen;
        yesText.color = yes ? Color.white : unselectedColor;
        noText.color = yes ? unselectedColor : Color.white;
        ApplyConfirmSelectionScale();
    }

    private void ApplyConfirmSelectionScale()
    {
        float yesScale = confirmContentScale * (confirmIndex == 0 ? 1.04f : 1f);
        float noScale = confirmContentScale * (confirmIndex == 1 ? 1.04f : 1f);
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
        if (sliderKnob != null)
            sliderKnob.anchoredPosition = new Vector2(-sliderWidth * 0.5f + sliderWidth * volume, 0f);
    }

    private void RefreshEffects()
    {
        if (effectsOnPlate != null) effectsOnPlate.sprite = effectsOn ? effectsChosen : effectsPlain;
        if (effectsOffPlate != null) effectsOffPlate.sprite = effectsOn ? effectsPlain : effectsChosen;
        if (effectsOnText != null) effectsOnText.color = effectsOn ? HighlandUi.Ink : HighlandUi.InkSoft;
        if (effectsOffText != null) effectsOffText.color = effectsOn ? HighlandUi.InkSoft : HighlandUi.Ink;
    }

    // =======================================================================
    //  Highland UI v11 の枠組み(2026-09-19 U7)
    // =======================================================================

    private Image NewV11Image(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;
        Image img = go.GetComponent<Image>();
        RectTransform rt = img.rectTransform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // SVG(1672x941)の中心座標と寸法で置く。
    private static void SetV11(RectTransform rt, float svgCx, float svgCy, float svgW, float svgH)
    {
        rt.anchoredPosition = new Vector2(HighlandUi.X(svgCx), HighlandUi.Y(svgCy));
        rt.sizeDelta = new Vector2(HighlandUi.L(svgW), HighlandUi.L(svgH));
    }

    // 両端が消える金の細罫 + 中央の中空菱形。localY は画面(px)。
    private void AddV11Rule(Transform parent, float localY, float svgW, float svgGap, string name)
    {
        int w = Mathf.RoundToInt(HighlandUi.L(svgW));
        Image rule = NewV11Image("Rule", parent, Color.white);
        rule.sprite = HighlandUi.FadeRule(w, 12, 1.05f * HighlandUi.S, HighlandUi.L(svgGap),
            new[] { 0f, 0.22f, 0.5f, 0.78f, 1f },
            new[] { new Color32(0xBC, 0xAA, 0x4E, 0xFF), new Color32(0xBC, 0xAA, 0x4E, 0xFF),
                    new Color32(0xFF, 0xE1, 0x6A, 0xFF), new Color32(0xBC, 0xAA, 0x4E, 0xFF),
                    new Color32(0xBC, 0xAA, 0x4E, 0xFF) },
            new[] { 0f, 0.30f, 0.95f, 0.30f, 0f },
            ownedTextures, ownedSprites, name);
        rule.rectTransform.anchoredPosition = new Vector2(0f, localY);
        rule.rectTransform.sizeDelta = new Vector2(w, 12f);

        Image gem = NewV11Image("RuleGem", parent, new Color(1f, 0.882f, 0.416f, 1f));
        gem.sprite = HighlandUi.DiamondRect(11, 14, false, 1.5f * HighlandUi.S,
            ownedTextures, ownedSprites, name + "Gem");
        gem.rectTransform.anchoredPosition = new Vector2(0f, localY);
        gem.rectTransform.sizeDelta = new Vector2(HighlandUi.L(9.6f), HighlandUi.L(12.4f));
    }

    // 設定パネルの地・見出し・行の板・操作ヒント。シーンの文字より背面へ回す。
    private void BuildV11Frame()
    {
        Transform root = transform;

        Image panel = NewV11Image("V11Panel", root, Color.white);
        panel.sprite = HighlandUi.NotchPanel((int)HighlandUi.L(765f), (int)HighlandUi.L(544f),
            HighlandUi.L(19f), false, ownedTextures, ownedSprites, "OptPanel",
            HighlandUi.L(6f), 1.65f * HighlandUi.S, 0.7f * HighlandUi.S);
        SetV11(panel.rectTransform, 835.5f, 435f, 765f, 544f);
        panel.transform.SetSiblingIndex(2);

        // 行の板(選択中は金枠)。文字より後ろへ回すため、パネルの直後に挿す。
        rowPlateSprite = HighlandUi.NotchFlat((int)HighlandUi.L(642f), (int)HighlandUi.L(68f),
            HighlandUi.L(6f), new Color32(0x16, 0x16, 0x2D, 0xFF), 0.36f,
            new Color32(0xA4, 0xA4, 0xAE, 0xFF), 1f * HighlandUi.S, 0.62f,
            ownedTextures, ownedSprites, "OptRowPlate");
        rowChosenSprite = HighlandUi.NotchPanel((int)HighlandUi.L(642f), (int)HighlandUi.L(68f),
            HighlandUi.L(6f), true, ownedTextures, ownedSprites, "OptRowChosen",
            HighlandUi.L(4f), 1.8f * HighlandUi.S, 0.7f * HighlandUi.S);
        float[] rowSvgCy = { 323f, 408f, 493f, 577f };
        for (int i = 0; i < 4; i++)
        {
            rowPlates[i] = NewV11Image("V11Row" + i, root, Color.white);
            rowPlates[i].sprite = rowPlateSprite;
            SetV11(rowPlates[i].rectTransform, 835f, rowSvgCy[i], 642f, 68f);
            rowPlates[i].transform.SetSiblingIndex(3 + i);

            rowGems[i] = NewV11Image("V11RowGem" + i, root, new Color(1f, 0.882f, 0.416f, 1f));
            rowGems[i].sprite = HighlandUi.Gem(15, 18, true, 0f, ownedTextures, ownedSprites, "OptRowGem");
            SetV11(rowGems[i].rectTransform, 537f, rowSvgCy[i], 17f, 20f);
            rowGems[i].transform.SetSiblingIndex(7 + i);
        }

        // 見出し「設定」+ その下の飾り罫。
        TMP_Text heading = HighlandUi.Text("V11Heading", root, "", 37f, HighlandUi.InkSoft,
            TextAlignmentOptions.Center, false, 5.2f);
        headingRuby = new HighlandUi.RubyText(heading, root, 37f, HighlandUi.InkSoft);
        headingRuby.Apply("[設定|せってい]");
        HighlandUi.PlaceCentered(heading, 836f, 230f, 460f, 37f);
        AddV11Rule(root, HighlandUi.Y(260f), 388f, 34f, "OptHeaderRule");

        // 下部の操作ヒント(レバー / 〇 決定 / ✕ 戻る)。
        Image lever = NewV11Image("HintLever", root, Color.white);
        lever.sprite = HighlandUi.Icon("lever_white");
        // 第 U8 便(2026-09-19 指示): レバーは〇✕ に比べて絵が小さく見づらいので
        // 1.4 倍(27.5 → 38.5)にする。中心は動かさない。
        SetV11(lever.rectTransform, 585.25f, 656f, 38.5f, 38.5f);
        hintLever = new HighlandUi.RubyText(
            HighlandUi.Text("HintLeverText", root, "", 19f, HighlandUi.InkSoft, TextAlignmentOptions.Left, false, 0.7f),
            root, 19f, HighlandUi.InkSoft);
        hintLever.Apply("[選|えら]ぶ・[変|か]える");
        HighlandUi.PlaceLeft(hintLever.Body, 615.25f, 664f, 260f, 19f);

        Image circle = NewV11Image("HintCircle", root, Color.white);
        circle.sprite = HighlandUi.Icon("circle");
        SetV11(circle.rectTransform, 841.65f, 658.5f, 36.2f, 36.2f);
        hintConfirm = new HighlandUi.RubyText(
            HighlandUi.Text("HintConfirmText", root, "", 19f, HighlandUi.InkSoft, TextAlignmentOptions.Left, false, 0.7f),
            root, 19f, HighlandUi.InkSoft);
        hintConfirm.Apply("[決定|けってい]");
        HighlandUi.PlaceLeft(hintConfirm.Body, 871.65f, 664f, 200f, 19f);

        Image cross = NewV11Image("HintCross", root, Color.white);
        cross.sprite = HighlandUi.Icon("cross");
        SetV11(cross.rectTransform, 1043.65f, 658.5f, 36.2f, 36.2f);
        hintBack = new HighlandUi.RubyText(
            HighlandUi.Text("HintBackText", root, "", 19f, HighlandUi.InkSoft, TextAlignmentOptions.Left, false, 0.7f),
            root, 19f, HighlandUi.InkSoft);
        hintBack.Apply("[戻|もど]る");
        HighlandUi.PlaceLeft(hintBack.Body, 1073.65f, 664f, 200f, 19f);
    }

    private HighlandUi.RubyText headingRuby, hintLever, hintConfirm, hintBack;

    // 行の選択状態と、ふりがなの追従。
    private void RefreshV11Rows()
    {
        for (int i = 0; i < 4; i++)
        {
            bool selected = i == index && !confirmOpen;
            bool shown = items[i] != null && items[i].gameObject.activeSelf;
            if (rowPlates[i] != null)
            {
                rowPlates[i].gameObject.SetActive(shown);
                rowPlates[i].sprite = selected ? rowChosenSprite : rowPlateSprite;
            }
            if (rowGems[i] != null) rowGems[i].gameObject.SetActive(shown && selected);
            if (itemRuby[i] != null) itemRuby[i].Layout();
        }
        if (headingRuby != null) headingRuby.EnsurePlaced();
        if (hintLever != null) hintLever.EnsurePlaced();
        if (hintConfirm != null) hintConfirm.EnsurePlaced();
        if (hintBack != null) hintBack.EnsurePlaced();
        if (confirmTitleRuby != null && confirmGroup != null && confirmGroup.activeSelf)
            confirmTitleRuby.EnsurePlaced();
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

    // v11: ボタンの中は「左に 〇 / ✕ のアイコン、右に文字」。文字は左寄せで置く。
    private static void SetupButtonText(TMP_Text text, Vector2 position)
    {
        text.rectTransform.pivot = new Vector2(0f, 0.5f);
        text.rectTransform.anchoredPosition = position
            + new Vector2(HighlandUi.L(-2.5f), confirmButtonTextOffsetY);
        text.rectTransform.sizeDelta = new Vector2(HighlandUi.L(120f), HighlandUi.L(40f));
        text.alignment = TextAlignmentOptions.Left;
        text.fontSize = 24f * HighlandUi.S;
        text.fontStyle = FontStyles.Normal;
        text.characterSpacing = 1.6f / 24f * 100f;
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
            // 撮影中に閉じられた場合、隠した明部ウィジェットを戻しておく。
            SetControlWidgetsVisible(true);
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
        foreach (Sprite sp in ownedSprites) if (sp != null) Destroy(sp);
        foreach (Texture2D tx in ownedTextures) if (tx != null) Destroy(tx);
        ownedSprites.Clear();
        ownedTextures.Clear();
    }


    private static void SetupSimpleConfirmOutline(Image button)
    {
        if (button == null) return;
        Outline outline = button.gameObject.AddComponent<Outline>();
        outline.effectDistance = new Vector2(2f, -2f);
        outline.effectColor = new Color(0.30f, 0.72f, 1f, 0.55f);
        outline.useGraphicAlpha = true;
    }
}

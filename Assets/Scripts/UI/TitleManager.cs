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
    // プレイ終了(シーン再読込)からの復帰専用のパンチイン演出の状態。設定画面を
    // 閉じたときには使わない(タイトルは背後で動き続けているため演出不要)。
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

    // The title menu clones the difficulty-select rows (DefficultyBar) so both
    // screens share one design language: slanted StageBar banner + StageName
    // label + gliding white slash brackets. Colors mirror the NORMAL row.
    private static readonly Color MenuBarBlue = new Color(0.055f, 0.525f, 0.91f);
    private static readonly Color MenuTextBase = new Color(0.85f, 0.93f, 1f);

    // How far above its scene-authored position the logo is lifted.
    private const float LogoRaiseOffset = 130f;

    // メニュー横の白スラッシュ(DefficultyBar クローン)のひと回り縮小率。
    // 等倍だとバナー高(109px)に対して159pxと主張が強すぎる。
    private const float MenuSlashScale = 0.78f;

    // スタート決定からステージ選択を重ね始めるまでの時間。GManager がこの時間
    // 経過後に state を切り替えて SSManager.PlayEntrance を呼ぶ(演出は総尺
    // StartExitTotal まで続き、選択画面のフェードインと交差する)。
    public const float StartExitCoverDelay = 0.30f;
    private const float StartExitTotal = 0.60f;

    private TMP_FontAsset uiFont;
    private RectTransform menuRoot;
    private TMP_Text[] menuItems = new TMP_Text[0];
    private RectTransform[] menuItemRects = new RectTransform[0];
    private CanvasGroup[] menuRowCG = new CanvasGroup[0];
    private Image[] menuRowBars = new Image[0];
    private float[] menuItemSel = new float[0];
    private float[] menuRowY = new float[0];
    private int menuIndex;

    // Cloned DefficultyBar "White" slash brackets; glide to the selected row.
    private RectTransform menuWhite;
    private float menuWhiteY;

    private GameObject transferRoot;
    private TMP_Text transferCodeText;          // 履歴なしメッセージ(コードはブロック表示)
    private TMP_InputField transferInput;
    private TMP_Text transferMessageText;
    private Image applyButton;
    private TMP_Text applyLabelText;
    private bool transferOpen;
    // oracle レビュー反映(第27便): コード4ブロック表示・入力フォーカス枠・
    // CTRL+C コピー・斜めバナーヘッダー/ボタン・操作ヒントバー。
    private GameObject transferCodeBlocksRoot;
    private TMP_Text[] transferCodeBlockTexts = new TMP_Text[0];
    private TMP_Text[] transferCodeBlockShadows = new TMP_Text[0];
    private Image transferInputBorder;
    private bool transferInputError;
    private Sprite bannerSprite;

    // キーチップ(青地+白文字)。JSAB ステージ選択の下部バーと同じ配色
    // (トップバーのグラデ青 #4290DB 実測値)で全画面のチップ意匠を揃える。
    // キーキャップはコードチップと競合しないよう一段暗く(oracle 第2周)。
    private static readonly Color KeyCapBlue = new Color(0.357f, 0.663f, 0.91f, 0.8f);
    private static readonly Color KeyCapEdge = new Color(0.741f, 0.933f, 1f, 0.75f);
    private static readonly Color HintLabelCyan = new Color(0.259f, 0.847f, 0.91f, 0.8f);
    private static readonly Color InputBorderIdle = new Color(0.118f, 0.812f, 0.878f, 0.65f);
    // 適用ボタンは入力が空だと沈み、入力があると点灯する(oracle 指摘: 常時
    // 最明度のボタンが主役のコード表示より目立っていた)。点灯時も主役の
    // コードチップよりわずかに彩度を抑える。
    private static readonly Color ApplyIdle = new Color(0.035f, 0.176f, 0.275f, 0.8f);
    private static readonly Color ApplyReady = new Color(0.086f, 0.561f, 0.784f, 0.92f);
    private static readonly Color ApplyLabelIdle = new Color(0.624f, 0.722f, 0.761f, 0.6f);
    private static readonly Color ApplyLabelReady = new Color(1f, 1f, 1f, 0.95f);

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
        for (int i = 0; i < menuItemSel.Length; i++)
        {
            menuItemSel[i] = i == 0 ? 1f : 0f;
            ApplyMenuRowState(i, menuItemSel[i]);
        }
        if (menuWhite != null && menuRowY.Length > 0)
        {
            menuWhiteY = menuRowY[0];
            menuWhite.anchoredPosition = new Vector2(0f, menuWhiteY);
        }
        if (transferRoot != null) transferRoot.SetActive(false);
        if (menuRoot != null) menuRoot.gameObject.SetActive(true);

        // The scene-authored "PRESS ANY BUTTON" prompt is replaced by the menu.
        if (promptText != null)
        {
            promptText.gameObject.SetActive(false);
            promptText = null;
        }
    }

    // Returning from a quit-play scene reload: the title rushes toward the
    // viewer, overshoots slightly, then settles as the pixel cover clears.
    // (PixelTransition drives this; the title-options close path does not.)
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

    // スタート決定の遷移演出: 選択バナーが白フラッシュ+小ポップ→行が右へ
    // 加速して飛び去り(選択行が先頭)、ロゴは上へ抜け、背景図形は加速する。
    // タイトルの背景は StartExitCoverDelay 経過後にステージ選択が重なって
    // くるまで残し、終盤で全体をフェードして交差させる(ハードカット防止)。
    public async void PlayStartExit()
    {
        if (dismissed) return;
        dismissed = true;

        const float flashDur = 0.14f;
        const float rowDur = 0.26f;
        const float slideDistance = 1500f;
        const float logoDelay = 0.10f;
        const float logoDur = 0.38f;
        const float fadeStart = 0.38f;

        int selected = Mathf.Clamp(menuIndex, 0, menuItemRects.Length > 0 ? menuItemRects.Length - 1 : 0);
        beatPulse = 1f; // 決定と同時に図形をひと光りさせる

        float time = 0f;
        while (time < StartExitTotal)
        {
            float dt = Time.deltaTime;
            time += dt;

            // 背景図形は加速しながら流れ続ける(dismissed 中は UpdateTitle が
            // 止まるので、同じ式をここで加速倍率付きで駆動する)。
            float speedMul = Mathf.Lerp(1f, 6f, Mathf.Clamp01(time / StartExitTotal));
            animTime += dt * speedMul;
            beatPulse = Mathf.Max(0f, beatPulse - dt * 5f);
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
                s.rect.Rotate(0f, 0f, s.rotSpeed * dt * speedMul);
            }

            // 選択バナーのフラッシュ(白→元色)+小ポップ(1→1.06→1)。文字は白地に
            // 飛ばないよう反転(ネイビー→白)させ、決定の一拍を読めるまま見せる。
            float flashP = Mathf.Clamp01(time / flashDur);
            if (selected < menuRowBars.Length && menuRowBars[selected] != null)
            {
                menuRowBars[selected].color = Color.Lerp(Color.white, MenuBarBlue, flashP * flashP);
            }
            if (selected < menuItems.Length && menuItems[selected] != null)
            {
                menuItems[selected].color = Color.Lerp(Navy, Color.white, flashP * flashP);
            }

            // 行スライドアウト: ease-in cubic で緩→急。選択行が先頭で飛び出す。
            for (int i = 0; i < menuItemRects.Length; i++)
            {
                if (menuItemRects[i] == null) continue;
                float delay = i == selected ? 0.10f : 0.17f + 0.05f * i;
                float p = Mathf.Clamp01((time - delay) / rowDur);
                float x = p * p * p * slideDistance;
                menuItemRects[i].anchoredPosition = new Vector2(x, menuRowY[i]);
                if (i == selected)
                {
                    float pop = Mathf.Sin(flashP * Mathf.PI) * 0.06f;
                    menuItemRects[i].localScale = Vector3.one * ((0.8f + 0.2f * menuItemSel[i]) + pop);
                    // 白ブラケットは選択行と一体で飛ぶ。
                    if (menuWhite != null) menuWhite.anchoredPosition = new Vector2(x, menuWhiteY);
                }
            }

            // ロゴは上へ加速して画面外に抜ける。
            if (logoRect != null)
            {
                float lp = Mathf.Clamp01((time - logoDelay) / logoDur);
                logoRect.anchoredPosition = new Vector2(
                    logoRect.anchoredPosition.x, logoBaseY + lp * lp * lp * 520f);
            }

            // 覆われ始めてから全体をフェード(選択画面側のフェードインと交差)。
            float fade = Mathf.Clamp01((time - fadeStart) / (StartExitTotal - fadeStart));
            group.alpha = 1f - fade * fade;

            await Task.Yield();
            if (this == null || group == null) return;
        }

        group.alpha = 0f;
        gameObject.SetActive(false);
        // 非表示中に退場前の配置へ戻し、次回 Init(再表示)を無傷にする。
        for (int i = 0; i < menuItemRects.Length; i++)
        {
            if (menuItemRects[i] == null) continue;
            menuItemRects[i].anchoredPosition = new Vector2(0f, menuRowY[i]);
            ApplyMenuRowState(i, menuItemSel[i]);
        }
        if (selected < menuRowBars.Length && menuRowBars[selected] != null)
        {
            menuRowBars[selected].color = MenuBarBlue;
        }
        if (menuWhite != null) menuWhite.anchoredPosition = new Vector2(0f, menuWhiteY);
        if (logoRect != null)
        {
            logoRect.anchoredPosition = new Vector2(logoRect.anchoredPosition.x, logoBaseY);
        }
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

    // Navigate + animate the vertical menu. Selection mirrors DefficultyBox
    // exactly (alpha 0.4→1, scale 0.8→1, text toward white) and the cloned white
    // slash brackets glide to the selected row like DefficultyBar.Tick.
    public void UpdateMenu(float dt, bool up, bool down)
    {
        if (transferOpen || menuItems.Length == 0) return;

        if (up) menuIndex = Mathf.Max(0, menuIndex - 1);
        else if (down) menuIndex = Mathf.Min(menuItems.Length - 1, menuIndex + 1);

        float follow = 1f - Mathf.Exp(-14f * dt);
        for (int i = 0; i < menuItems.Length; i++)
        {
            float target = i == menuIndex ? 1f : 0f;
            menuItemSel[i] = Mathf.Abs(target - menuItemSel[i]) < 0.001f
                ? target
                : Mathf.Lerp(menuItemSel[i], target, follow);
            ApplyMenuRowState(i, menuItemSel[i]);
        }

        if (menuWhite != null && menuRowY.Length > menuIndex)
        {
            float targetY = menuRowY[menuIndex];
            menuWhiteY = Mathf.Abs(targetY - menuWhiteY) < 0.5f
                ? targetY
                : Mathf.Lerp(menuWhiteY, targetY, 1f - Mathf.Exp(-16f * dt));
            menuWhite.anchoredPosition = new Vector2(0f, menuWhiteY);
        }
    }

    // Same visual state math as DefficultyBox.SetPosition.
    private void ApplyMenuRowState(int i, float progress)
    {
        if (menuRowCG[i] != null) menuRowCG[i].alpha = 0.4f + 0.6f * progress;
        if (menuItemRects[i] != null) menuItemRects[i].localScale = Vector3.one * (0.8f + 0.2f * progress);
        if (menuItems[i] != null) menuItems[i].color = Color.Lerp(MenuTextBase, Color.white, progress);
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

        string[] labels = { "スタート", "設定", "引き継ぎ" };
        float[] rowY = { -178f, -303f, -428f };

        // The real difficulty-select column lives next to the title in the same
        // canvas; clone its row (banner + label) and white brackets so the title
        // menu is literally the same parts, not a lookalike.
        Transform diffSrc = transform.parent != null ? transform.parent.Find("StageBoxParent/DefficultyBar") : null;
        Transform rowSrc = diffSrc != null ? diffSrc.Find("List/Normal") : null;
        Transform whiteSrc = diffSrc != null ? diffSrc.Find("White") : null;

        menuItems = new TMP_Text[labels.Length];
        menuItemRects = new RectTransform[labels.Length];
        menuRowCG = new CanvasGroup[labels.Length];
        menuRowBars = new Image[labels.Length];
        menuItemSel = new float[labels.Length];
        menuRowY = rowY;

        for (int i = 0; i < labels.Length; i++)
        {
            RectTransform row;
            TMP_Text label;
            if (rowSrc != null)
            {
                GameObject rowObj = Instantiate(rowSrc.gameObject, menuRoot);
                rowObj.name = "Item" + i;
                rowObj.SetActive(true);
                row = (RectTransform)rowObj.transform;
                Image bar = rowObj.transform.Find("StageBar")?.GetComponent<Image>();
                if (bar != null) bar.color = MenuBarBlue;
                menuRowBars[i] = bar;
                label = rowObj.transform.Find("StageName")?.GetComponent<TMP_Text>();
            }
            else
            {
                // Degraded fallback (scene layout changed): plain banner + label.
                row = new GameObject("Item" + i, typeof(RectTransform)).GetComponent<RectTransform>();
                row.SetParent(menuRoot, false);
                menuRowBars[i] = CreatePanel("StageBar", row, Vector2.zero, new Vector2(583f, 109f), MenuBarBlue);
                label = CreateText("StageName", row, Vector2.zero, new Vector2(583f, 109f), 52f, MenuTextBase, TextAlignmentOptions.Center);
            }

            row.anchorMin = row.anchorMax = new Vector2(0.5f, 0.5f);
            row.pivot = new Vector2(0.5f, 0.5f);
            row.anchoredPosition = new Vector2(0f, rowY[i]);

            CanvasGroup cg = row.GetComponent<CanvasGroup>();
            if (cg == null) cg = row.gameObject.AddComponent<CanvasGroup>();

            if (label != null)
            {
                label.text = labels[i];
                // Japanese labels ride high under Middle alignment (Latin UI font
                // + CJK fallback metrics); optically center them in the banner.
                TmpAlign.CenterInkVertically(label);
            }

            menuItems[i] = label;
            menuItemRects[i] = row;
            menuRowCG[i] = cg;
            menuItemSel[i] = i == 0 ? 1f : 0f;
            ApplyMenuRowState(i, menuItemSel[i]);
        }

        if (whiteSrc != null)
        {
            GameObject whiteObj = Instantiate(whiteSrc.gameObject, menuRoot);
            whiteObj.name = "White";
            whiteObj.SetActive(true);
            menuWhite = (RectTransform)whiteObj.transform;
            CanvasGroup whiteCG = whiteObj.GetComponent<CanvasGroup>();
            if (whiteCG != null) whiteCG.alpha = 1f;
            // 白スラッシュ本体だけ縮小(バナー上を掃く Shine はバナーサイズのまま)。
            // 縮小した分だけ内側に寄せ、バナー端との間隔を保つ。
            foreach (string slashName in new[] { "White_L", "White_R" })
            {
                RectTransform slash = menuWhite.Find(slashName) as RectTransform;
                if (slash == null) continue;
                slash.localScale = Vector3.one * MenuSlashScale;
                slash.anchoredPosition = new Vector2(slash.anchoredPosition.x * 0.96f, slash.anchoredPosition.y);
            }
            menuWhite.SetAsLastSibling();
            menuWhiteY = rowY[0];
            menuWhite.anchoredPosition = new Vector2(0f, menuWhiteY);
        }
    }

    // ---- Transfer panel ---------------------------------------------------

    public void OpenTransfer()
    {
        if (transferRoot == null) return;
        transferOpen = true;
        transferInputError = false;
        if (menuRoot != null) menuRoot.gameObject.SetActive(false);
        // スクリムを薄くした分、背面のロゴがパネルに透けて汚れるため隠す
        // (背景図形は演出として残す)。
        if (logoRect != null) logoRect.gameObject.SetActive(false);
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
        if (logoRect != null) logoRect.gameObject.SetActive(true);
    }

    public void ApplyTransfer()
    {
        if (transferInput == null) return;
        if (PlayHistory.TryImportCode(transferInput.text, out string error))
        {
            transferInputError = false;
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
            transferInputError = true;
            if (transferMessageText != null)
            {
                transferMessageText.color = ErrorRed;
                transferMessageText.text = error;
            }
        }
    }

    // 発行済みコードをクリップボードへ(CTRL+C。入力欄のフォーカスと衝突しない)。
    public bool CopyTransferCode()
    {
        if (!PlayHistory.HasHistory) return false;
        GUIUtility.systemCopyBuffer = PlayHistory.ExportCode();
        if (transferMessageText != null)
        {
            transferMessageText.color = Cyan;
            transferMessageText.text = "コードをコピーしました";
        }
        return true;
    }

    // 毎フレームの引き継ぎ画面装飾: 入力欄の枠色(フォーカス=シアン/エラー=赤)と
    // 適用ボタンの点灯(入力があるときだけ明るくなる)。
    public void TickTransfer()
    {
        if (!transferOpen || transferInputBorder == null || transferInput == null) return;
        transferInputBorder.color = transferInputError
            ? ErrorRed
            : (transferInput.isFocused ? Cyan : InputBorderIdle);
        if (applyButton != null)
        {
            bool ready = transferInput.text.Length > 0;
            applyButton.color = ready ? ApplyReady : ApplyIdle;
            if (applyLabelText != null) applyLabelText.color = ready ? ApplyLabelReady : ApplyLabelIdle;
        }
    }

    private void RefreshTransferCode()
    {
        bool has = PlayHistory.HasHistory;
        if (transferCodeBlocksRoot != null) transferCodeBlocksRoot.SetActive(has);
        if (transferCodeText != null)
        {
            transferCodeText.gameObject.SetActive(!has);
            if (!has)
            {
                transferCodeText.text = "まだプレイ履歴がありません";
                TmpAlign.CenterInkVertically(transferCodeText);
            }
        }
        if (!has) return;
        string[] parts = PlayHistory.ExportCode().Split('-');
        for (int i = 0; i < transferCodeBlockTexts.Length; i++)
        {
            string part = i < parts.Length ? parts[i] : "";
            if (transferCodeBlockTexts[i] != null)
            {
                transferCodeBlockTexts[i].text = part;
                TmpAlign.CenterInkVertically(transferCodeBlockTexts[i]);
            }
            if (i < transferCodeBlockShadows.Length && transferCodeBlockShadows[i] != null)
            {
                transferCodeBlockShadows[i].text = part;
                TmpAlign.CenterInkVertically(transferCodeBlockShadows[i]);
            }
        }
    }

    // 引き継ぎ画面(oracle レビュー反映版): 斜めバナーヘッダー、コード4ブロック、
    // 2カード構成(発行済み/入力)、斜めバナー適用ボタン、下部操作ヒントバー。
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

        // oracle 第28便レビュー反映: スクリムは薄く保ちつつ中央 UI の背面だけ
        // 暗いプレートで締める(背景図形は周縁でうっすら動き続ける)。
        CreatePanel("Scrim", rootRect, Vector2.zero, new Vector2(4000f, 4000f), new Color(0.008f, 0.027f, 0.075f, 0.45f));
        CreatePanel("Panel", rootRect, Vector2.zero, new Vector2(1120f, 780f), new Color(0.008f, 0.035f, 0.078f, 0.97f));
        CreatePanel("PanelEdge", rootRect, new Vector2(0f, 342f), new Vector2(1120f, 4f), Cyan);

        // ヘッダー: 斜めバナー+ずらし影+白スラッシュ(シアン影付き)。
        CreateBanner("HeaderShadow", rootRect, new Vector2(14f, 272f), new Vector2(720f, 82f), new Color(0.043f, 0.09f, 0.145f, 0.75f));
        CreateBanner("HeaderBanner", rootRect, new Vector2(0f, 282f), new Vector2(720f, 82f), new Color(0.024f, 0.169f, 0.286f, 0.96f));
        CreateSlash("HeaderSlashL", rootRect, new Vector2(-408f, 282f));
        CreateSlash("HeaderSlashR", rootRect, new Vector2(408f, 282f));
        TMP_Text heading = CreateText("Heading", rootRect, new Vector2(0f, 284f), new Vector2(700f, 70f), 56f, Cyan, TextAlignmentOptions.Center);
        heading.fontStyle = FontStyles.Bold;
        TMP_Text headingSub = CreateText("HeadingSub", rootRect, new Vector2(0f, 228f), new Vector2(700f, 30f), 20f, new Color(0.62f, 0.91f, 0.906f, 0.85f), TextAlignmentOptions.Center);
        headingSub.characterSpacing = 9f;
        headingSub.text = "TRANSFER CODE";

        // 主役カード: 発行済みコード(明るいシアン枠+内側の暗線+上辺の強調ライン)。
        CreateCard("CodeCard", rootRect, new Vector2(0f, 85f), new Vector2(1002f, 260f),
            new Color(0.157f, 0.918f, 0.949f, 0.9f), 3f, new Color(0.02f, 0.125f, 0.184f, 0.8f),
            new Color(0f, 0.09f, 0.133f, 0.75f));
        CreatePanel("CodeCardTopLine", rootRect, new Vector2(0f, 213f), new Vector2(1002f, 4f), new Color(0.216f, 0.937f, 1f, 0.95f));
        TMP_Text codeLabel = CreateText("CodeLabel", rootRect, new Vector2(-257f, 172f), new Vector2(440f, 40f), 24f, new Color(0.388f, 0.867f, 0.91f, 0.62f), TextAlignmentOptions.Left);
        codeLabel.characterSpacing = 3f;
        TMP_Text copyLabel = CreateText("CopyLabel", rootRect, new Vector2(300f, 178f), new Vector2(360f, 36f), 20f, new Color(0.345f, 0.863f, 0.922f, 0.8f), TextAlignmentOptions.Right);
        copyLabel.text = "CTRL+C でコピー";

        // コード4ブロック(チップ質感: シアン枠+内側の淡いヘアライン+濃い地+影付き文字)。
        transferCodeBlocksRoot = new GameObject("CodeBlocks", typeof(RectTransform));
        transferCodeBlocksRoot.layer = gameObject.layer;
        RectTransform blocksRect = (RectTransform)transferCodeBlocksRoot.transform;
        blocksRect.SetParent(rootRect, false);
        blocksRect.anchorMin = blocksRect.anchorMax = new Vector2(0.5f, 0.5f);
        blocksRect.anchoredPosition = new Vector2(0f, 55f);
        transferCodeBlockTexts = new TMP_Text[4];
        transferCodeBlockShadows = new TMP_Text[4];
        const float blockW = 200f;
        const float blockH = 84f;
        const float blockGap = 26f;
        float x0 = -(blockW * 3f + blockGap * 3f) * 0.5f;
        for (int i = 0; i < 4; i++)
        {
            float bx = x0 + i * (blockW + blockGap);
            Image border = CreatePanel("Block" + i, blocksRect, new Vector2(bx, 0f), new Vector2(blockW, blockH), new Color(0.192f, 0.945f, 0.965f, 0.9f));
            Image hairline = CreatePanel("Hairline", border.rectTransform, Vector2.zero, Vector2.zero, new Color(0.749f, 0.984f, 1f, 0.22f));
            hairline.rectTransform.anchorMin = Vector2.zero;
            hairline.rectTransform.anchorMax = Vector2.one;
            hairline.rectTransform.offsetMin = new Vector2(3f, 3f);
            hairline.rectTransform.offsetMax = new Vector2(-3f, -3f);
            Image fill = CreatePanel("Fill", border.rectTransform, Vector2.zero, Vector2.zero, new Color(0.016f, 0.098f, 0.145f, 0.96f));
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = new Vector2(4.5f, 4.5f);
            fill.rectTransform.offsetMax = new Vector2(-4.5f, -4.5f);
            // 部品感を出す上辺ハイライトと下辺の落ち影(oracle 第2周)。
            Image topHi = CreatePanel("TopHi", border.rectTransform, Vector2.zero, Vector2.zero, new Color(0.455f, 1f, 1f, 0.35f));
            topHi.rectTransform.anchorMin = new Vector2(0f, 1f);
            topHi.rectTransform.anchorMax = Vector2.one;
            topHi.rectTransform.offsetMin = new Vector2(4.5f, -6f);
            topHi.rectTransform.offsetMax = new Vector2(-4.5f, -4.5f);
            Image bottomSh = CreatePanel("BottomShadow", border.rectTransform, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.35f));
            bottomSh.rectTransform.anchorMin = Vector2.zero;
            bottomSh.rectTransform.anchorMax = new Vector2(1f, 0f);
            bottomSh.rectTransform.offsetMin = new Vector2(4.5f, 4.5f);
            bottomSh.rectTransform.offsetMax = new Vector2(-4.5f, 6f);
            TMP_Text sh = CreateText("Shadow", border.rectTransform, new Vector2(3f, -3f), new Vector2(blockW, blockH), 60f, new Color(0f, 0.227f, 0.29f, 0.75f), TextAlignmentOptions.Center);
            sh.fontStyle = FontStyles.Bold;
            sh.characterSpacing = 20f;
            TMP_Text bt = CreateText("Text", border.rectTransform, Vector2.zero, new Vector2(blockW, blockH), 60f, new Color(0.247f, 0.918f, 1f), TextAlignmentOptions.Center);
            bt.fontStyle = FontStyles.Bold;
            bt.characterSpacing = 20f;
            transferCodeBlockTexts[i] = bt;
            transferCodeBlockShadows[i] = sh;
            if (i < 3)
            {
                TMP_Text hy = CreateText("Hyphen" + i, blocksRect, new Vector2(bx + (blockW + blockGap) * 0.5f, 0f), new Vector2(26f, 48f), 44f, new Color(0.345f, 0.863f, 0.922f, 0.65f), TextAlignmentOptions.Center);
                hy.text = "-";
                TmpAlign.CenterInkVertically(hy);
            }
        }
        // 履歴なしのときだけ出すメッセージ(ブロックと同じ位置)。
        transferCodeText = CreateText("Code", rootRect, new Vector2(0f, 55f), new Vector2(1002f, 110f), 40f, CyanDim, TextAlignmentOptions.Center);

        // 副役カード: コード入力(弱い枠で主役より一段引く)+横並び操作ストリップ。
        CreateCard("InputCard", rootRect, new Vector2(0f, -165f), new Vector2(1002f, 190f),
            new Color(0.106f, 0.651f, 0.769f, 0.45f), 1.5f, new Color(0.016f, 0.125f, 0.192f, 0.55f));
        TMP_Text inputLabel = CreateText("InputLabel", rootRect, new Vector2(-257f, -104f), new Vector2(440f, 36f), 24f, new Color(0.388f, 0.867f, 0.91f, 0.55f), TextAlignmentOptions.Left);
        inputLabel.characterSpacing = 3f;

        BuildInputField(rootRect, new Vector2(-120f, -192f), new Vector2(726f, 74f));

        // 適用ボタンは入力欄の右に添える(中央の大ボタンはフォーム臭さの元)。
        applyButton = CreateBanner("ApplyButton", rootRect, new Vector2(350f, -192f), new Vector2(200f, 74f), ApplyIdle);
        TMP_Text applyLabel = CreateText("ApplyLabel", applyButton.rectTransform, Vector2.zero, new Vector2(200f, 74f), 32f, ApplyLabelIdle, TextAlignmentOptions.Center);
        StretchToParent(applyLabel.rectTransform);
        applyLabel.fontStyle = FontStyles.Bold;
        applyLabelText = applyLabel;

        transferMessageText = CreateText("Message", rootRect, new Vector2(0f, -294f), new Vector2(1002f, 44f), 26f, Cyan, TextAlignmentOptions.Center);

        // 下部の操作ヒントバー(開始位置は主役カードの左端 x=-501 に揃える)。
        Image hintBar = CreatePanel("HintBar", rootRect, new Vector2(0f, -350f), new Vector2(1120f, 46f), new Color(0.012f, 0.094f, 0.153f, 0.82f));
        CreatePanel("HintBarLine", rootRect, new Vector2(0f, -326f), new Vector2(1120f, 2f), new Color(0.137f, 0.91f, 0.949f, 0.8f));
        float hx = -501f;
        hx = AddHintChip(hintBar.rectTransform, hx, "ENTER", "適用");
        hx = AddHintChip(hintBar.rectTransform, hx, "CTRL+C", "コピー");
        AddHintChip(hintBar.rectTransform, hx, "ESC", "戻る");

        // Fill the label texts.
        SetChildText(rootRect, "Heading", "引き継ぎ");
        SetChildText(rootRect, "CodeLabel", "あなたの引き継ぎコード");
        SetChildText(rootRect, "InputLabel", "コードを入力");
        SetChildText(applyButton.rectTransform, "ApplyLabel", "適用");
        transferMessageText.text = string.Empty;

        transferRoot.SetActive(false);
    }

    // 枠付きの区分カード。枠の明度と太さで主役/副役の階層を付ける。
    // innerLineColor を渡すと枠のすぐ内側に 1.5px の暗線を敷き、明るい枠が
    // 地に直接触れる「ワイヤーフレーム感」を消す(oracle 第2周)。
    private void CreateCard(string objectName, Transform parent, Vector2 pos, Vector2 size,
        Color borderColor, float borderWidth, Color fillColor, Color? innerLineColor = null)
    {
        Image border = CreatePanel(objectName, parent, pos, size, borderColor);
        RectTransform fillParent = border.rectTransform;
        float fillInset = borderWidth;
        if (innerLineColor.HasValue)
        {
            Image line = CreatePanel("InnerLine", border.rectTransform, Vector2.zero, Vector2.zero, innerLineColor.Value);
            line.rectTransform.anchorMin = Vector2.zero;
            line.rectTransform.anchorMax = Vector2.one;
            line.rectTransform.offsetMin = new Vector2(borderWidth, borderWidth);
            line.rectTransform.offsetMax = new Vector2(-borderWidth, -borderWidth);
            fillParent = line.rectTransform;
            fillInset = 1.5f;
        }
        Image fill = CreatePanel("Fill", fillParent, Vector2.zero, Vector2.zero, fillColor);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = new Vector2(fillInset, fillInset);
        fill.rectTransform.offsetMax = new Vector2(-fillInset, -fillInset);
    }

    // タイトルメニュー行と同じ StageBar スプライトの斜めバナー。
    private Image CreateBanner(string objectName, Transform parent, Vector2 pos, Vector2 size, Color color)
    {
        Image img = CreatePanel(objectName, parent, pos, size, color);
        if (bannerSprite == null)
        {
            Transform bar = transform.parent != null ? transform.parent.Find("StageBoxParent/DefficultyBar/List/Normal/StageBar") : null;
            Image src = bar != null ? bar.GetComponent<Image>() : null;
            bannerSprite = src != null ? src.sprite : null;
        }
        if (bannerSprite != null)
        {
            img.sprite = bannerSprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
        }
        return img;
    }

    // タイトルメニューの White ブラケットと同じ向きの白スラッシュ(シアン影付き)。
    private void CreateSlash(string objectName, Transform parent, Vector2 pos)
    {
        Image shadow = CreatePanel(objectName + "Shadow", parent, pos + new Vector2(3f, -3f), new Vector2(24f, 92f), new Color(0.208f, 0.91f, 1f, 0.35f));
        shadow.rectTransform.localEulerAngles = new Vector3(0f, 0f, -14f);
        Image s = CreatePanel(objectName, parent, pos, new Vector2(24f, 92f), new Color(1f, 1f, 1f, 0.95f));
        s.rectTransform.localEulerAngles = new Vector3(0f, 0f, -14f);
    }

    // ヒントバーへ「[キー] ラベル」を1組追加し、次の x カーソルを返す。
    private float AddHintChip(RectTransform parent, float x, string key, string label)
    {
        const float capH = 34f;
        const float capFont = 20f;
        float capW = Mathf.Max(capH, 18f + key.Length * capFont * 0.66f);

        Image cap = CreatePanel("Key_" + key, parent, Vector2.zero, new Vector2(capW, capH), KeyCapEdge);
        cap.rectTransform.pivot = new Vector2(0f, 0.5f);
        cap.rectTransform.anchoredPosition = new Vector2(x, 0f);
        Image fill = CreatePanel("Fill", cap.rectTransform, Vector2.zero, Vector2.zero, KeyCapBlue);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = new Vector2(2f, 2f);
        fill.rectTransform.offsetMax = new Vector2(-2f, -2f);
        TMP_Text keyText = CreateText("L", cap.rectTransform, Vector2.zero, new Vector2(capW, capH), capFont, Color.white, TextAlignmentOptions.Center);
        StretchToParent(keyText.rectTransform);
        keyText.text = key;
        TmpAlign.CenterInkVertically(keyText);
        x += capW + 10f;

        TMP_Text labelText = CreateText("Hint_" + label, parent, Vector2.zero, new Vector2(300f, capH), 24f, HintLabelCyan, TextAlignmentOptions.Left);
        labelText.rectTransform.pivot = new Vector2(0f, 0.5f);
        labelText.rectTransform.anchoredPosition = new Vector2(x, 0f);
        labelText.text = label;
        TmpAlign.CenterInkVertically(labelText);
        x += labelText.GetPreferredValues(label).x + 40f;
        return x;
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
        // フィールド本体の Image は枠として使い、フォーカス/エラーで色を変える。
        Image bg = fieldObj.GetComponent<Image>();
        bg.color = InputBorderIdle;
        transferInputBorder = bg;

        Image fieldFill = CreatePanel("Fill", fieldRect, Vector2.zero, Vector2.zero, new Color(0.016f, 0.067f, 0.114f, 1f));
        fieldFill.rectTransform.anchorMin = Vector2.zero;
        fieldFill.rectTransform.anchorMax = Vector2.one;
        fieldFill.rectTransform.offsetMin = new Vector2(3f, 3f);
        fieldFill.rectTransform.offsetMax = new Vector2(-3f, -3f);

        GameObject areaObj = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
        areaObj.layer = gameObject.layer;
        RectTransform areaRect = (RectTransform)areaObj.transform;
        areaRect.SetParent(fieldRect, false);
        areaRect.anchorMin = Vector2.zero;
        areaRect.anchorMax = Vector2.one;
        // プレースホルダーと入力文字は左基準で揃える(中央寄せだと入力開始時に
        // 文字位置が跳ねる。oracle 第2周)。
        areaRect.offsetMin = new Vector2(52f, 8f);
        areaRect.offsetMax = new Vector2(-24f, -8f);

        TMP_Text placeholder = CreateText("Placeholder", areaRect, Vector2.zero, size, 38f, new Color(0.498f, 0.682f, 0.722f, 0.5f), TextAlignmentOptions.Left);
        StretchToParent(placeholder.rectTransform);
        placeholder.text = "XXXX-XXXX-XXXX-XXXX";

        TMP_Text textComp = CreateText("Text", areaRect, Vector2.zero, size, 42f, new Color(0.953f, 0.984f, 1f, 0.95f), TextAlignmentOptions.Left);
        StretchToParent(textComp.rectTransform);
        textComp.fontStyle = FontStyles.Bold;
        textComp.characterSpacing = 8f;

        transferInput = fieldObj.GetComponent<TMP_InputField>();
        transferInput.textViewport = areaRect;
        transferInput.textComponent = textComp;
        transferInput.placeholder = placeholder;
        transferInput.fontAsset = uiFont;
        transferInput.pointSize = 42f;
        transferInput.characterLimit = 19; // 16 symbols + 3 grouping hyphens
        transferInput.lineType = TMP_InputField.LineType.SingleLine;
        transferInput.richText = false;
        transferInput.customCaretColor = true;
        transferInput.caretColor = Cyan;
        transferInput.caretWidth = 3;
        transferInput.onValidateInput += (string text, int pos, char ch) => char.ToUpperInvariant(ch);
        // 入力し直したらエラー表示(赤枠)を解除する。
        transferInput.onValueChanged.AddListener(_ => transferInputError = false);
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
        if (text != null)
        {
            text.text = value;
            TmpAlign.CenterInkVertically(text);
        }
    }
}

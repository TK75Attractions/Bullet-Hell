using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

// Full-screen pixel-mosaic transition: white cells pop in from the center
// outward until they cover the screen, and later vanish center-first to
// reveal what is behind. Cells are generated at runtime so the scene only
// needs an empty holder object with this component.
public class PixelTransition : MonoBehaviour
{
    private const int cols = 24;
    private const int rows = 14;
    private const float cellSize = 80f;
    private const float spreadTime = 0.45f;
    private const float jitterTime = 0.07f;
    // プレイ開始の MosaicReveal 専用の小さめジッタ。共有の jitterTime(0.07)だと
    // 拡大フロントのブロックが散らばって「中央から広がる」より「ランダムに溶ける」
    // ように見えるため、モザイク解像だけ front を綺麗にして中央発を読みやすくする。
    private const float mosaicJitterTime = 0.05f;
    private const float maxAnimationDelta = 1f / 30f;
    private const float loadedSceneHoldTime = 0.18f;
    private const float coveredHoldTime = 0.08f;

    // プレイ開始(ステージ決定→プレイ画面)専用のホワイトアウト+モザイク解像。
    // 全白ホールドは oracle 第30便の指摘(白の待ち時間が長くローディング風に
    // 見える)で 0.10 → 0.05 に短縮。
    // カバー工程は均一フェード(白飛び)ではなく、白いピクセルブロックが中央から
    // 外周へ広がって覆う中央発モザイクにする(第34便: ユーザー3回目の再指摘)。
    // リビールは従来どおり中央発で欠けていく。
    private const float mosaicCoverTime = 0.42f;
    private const float whiteoutHoldTime = 0.05f;
    private const float mosaicRevealTime = 0.76f;

    private RectTransform[] cells;
    private Image[] cellImages;
    private float[] baseDelays;
    private float[] delays;
    private CanvasGroup fadeGroup;
    private Image whiteSheet;
    private bool built;
    private static bool revealAfterSceneLoad;
    private static bool titleReturnAfterSceneLoad;

    private void Awake()
    {
        Build();
        if (titleReturnAfterSceneLoad) SetColor(Color.white);
        if (revealAfterSceneLoad)
        {
            for (int i = 0; i < cells.Length; i++) cells[i].localScale = Vector3.one;
            gameObject.SetActive(true);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private async void Start()
    {
        if (!revealAfterSceneLoad) return;
        revealAfterSceneLoad = false;

        // Keep the screen fully covered until the reloaded scene has finished
        // its asynchronous game/database initialization. Starting the reveal
        // earlier makes both transitions disappear inside the loading hitch.
        while (GManager.Control == null || !GManager.Control.ready)
        {
            await Task.Yield();
            if (this == null) return;
        }

        EnsureTopmost();
        Canvas.ForceUpdateCanvases();
        await Task.Yield();
        await Task.Yield();
        if (this == null) return;

        // A scene load can produce one very large unscaledDeltaTime. Hold the
        // completed cover for a few rendered frames so that hitch cannot skip
        // the entire centre-out reveal in a single frame.
        await Hold(loadedSceneHoldTime);
        if (this == null) return;

        bool isTitleReturn = ConsumeTitleReturn();
        TitleManager returnTitle = isTitleReturn ? GManager.Control.TManager : null;
        returnTitle?.PrepareReturnEntrance();

        Task revealTask = Reveal();
        if (returnTitle != null)
        {
            // Let the centre pixels clear first so the title punch-in is
            // actually visible instead of finishing behind the white cover.
            // Start the punch-in as soon as the first centre cells clear. A
            // longer pause exposes the prepared title as a frozen frame.
            float delay = 0.035f;
            while (delay > 0f)
            {
                delay -= AnimationDelta();
                await Task.Yield();
                if (this == null) return;
            }
            returnTitle.PlayReturnEntrance();
        }
        await revealTask;
    }

    public static void RevealAfterNextSceneLoad(bool titleReturn = false)
    {
        revealAfterSceneLoad = true;
        titleReturnAfterSceneLoad = titleReturn;
    }

    public static bool ConsumeTitleReturn()
    {
        bool result = titleReturnAfterSceneLoad;
        titleReturnAfterSceneLoad = false;
        return result;
    }

    private void Build()
    {
        if (built) return;
        built = true;
        // 全画面ワイプはどの UI よりも手前に描く。JSAB オーバーレイ(sortingOrder 5
        // の独立キャンバス)より StageCanvas が下にあるため、オーバーライドしないと
        // JSAB 画面を覆えず、先に画面を消してからカバーする不自然な順序になる。
        Canvas overrideCanvas = GetComponent<Canvas>();
        if (overrideCanvas == null) overrideCanvas = gameObject.AddComponent<Canvas>();
        overrideCanvas.overrideSorting = true;
        overrideCanvas.sortingOrder = 50;
        fadeGroup = GetComponent<CanvasGroup>();
        if (fadeGroup == null) fadeGroup = gameObject.AddComponent<CanvasGroup>();
        fadeGroup.alpha = 1f;
        fadeGroup.blocksRaycasts = false;
        fadeGroup.interactable = false;

        // ホワイトアウト用の白シート。セル群を中間 alpha で重ねると 1px の
        // 重なりが格子状に透けるため、フェードは単一の全画面 Image で行う。
        GameObject sheetGo = new GameObject("WhiteSheet", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        sheetGo.layer = gameObject.layer;
        RectTransform sheetRect = (RectTransform)sheetGo.transform;
        sheetRect.SetParent(transform, false);
        sheetRect.anchorMin = Vector2.zero;
        sheetRect.anchorMax = Vector2.one;
        sheetRect.offsetMin = new Vector2(-40f, -40f);
        sheetRect.offsetMax = new Vector2(40f, 40f);
        whiteSheet = sheetGo.GetComponent<Image>();
        whiteSheet.raycastTarget = false;
        whiteSheet.color = Color.white;
        sheetGo.SetActive(false);
        int count = cols * rows;
        cells = new RectTransform[count];
        cellImages = new Image[count];
        baseDelays = new float[count];
        delays = new float[count];
        float maxDist = new Vector2((cols - 1) * 0.5f * cellSize, (rows - 1) * 0.5f * cellSize).magnitude;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                int i = r * cols + c;
                GameObject go = new GameObject("Cell", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.layer = gameObject.layer;
                RectTransform rect = (RectTransform)go.transform;
                rect.SetParent(transform, false);
                // 1px overlap per side so neighbouring cells never show seams.
                rect.sizeDelta = Vector2.one * (cellSize + 2f);
                float x = (c - (cols - 1) * 0.5f) * cellSize;
                float y = (r - (rows - 1) * 0.5f) * cellSize;
                rect.anchoredPosition = new Vector2(x, y);
                rect.localScale = Vector3.zero;

                Image img = go.GetComponent<Image>();
                img.raycastTarget = false;
                img.color = Color.white;

                cells[i] = rect;
                cellImages[i] = img;
                baseDelays[i] = Mathf.Sqrt(x * x + y * y) / maxDist * spreadTime;
            }
        }
    }

    // 親の StageCanvas は ScreenSpaceCamera で、ScreenSpaceOverlay の JSAB
    // キャンバス(order 5)より常に奥に描かれる。ネスト Canvas の sortingOrder 50
    // では追い越せないため、初回使用時に Canvases 直下へ出して自前の Overlay
    // キャンバスにする。Awake で行うと StageSelectManager.Init の
    // Find("PixelTransition") が壊れるので、参照解決が終わった後の使用時に行う。
    private void EnsureTopmost()
    {
        Canvas canvas = GetComponent<Canvas>();
        // 注意: overrideSorting=true のネスト Canvas は isRootCanvas=true を返す
        // ため、移設済み判定は renderMode で行う。
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay) return;
        Transform canvasesRoot = transform.parent != null ? transform.parent.parent : null;
        transform.SetParent(canvasesRoot, false);
        canvas.overrideSorting = false;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
    }

    // Pixels appear from the center until the screen is fully covered.
    public async Task Cover()
    {
        EnsureTopmost();
        gameObject.SetActive(true);
        if (fadeGroup != null) fadeGroup.alpha = 1f;
        RollDelays();
        await Animate(coverIn: true);
        await Hold(coveredHoldTime);
    }

    // プレイ開始用: 白いピクセルブロックが画面中央から外周へ広がって覆う(中央発
    // モザイクカバー)。従来は白シート1枚の均一フェード(白飛び)だったが、
    // ユーザーの3回目の再指摘「白が中央から広がるようにして」に合わせ、カバー
    // 工程そのものを中央発のセルポップインにする(このゲームに元からあった旧
    // Cover の中央発挙動を踏襲)。覆い切ったら白ベタのまま MosaicReveal へ渡す。
    public async Task WhiteoutCover()
    {
        Build();
        EnsureTopmost();
        gameObject.SetActive(true);
        fadeGroup.alpha = 1f;
        if (whiteSheet != null) whiteSheet.gameObject.SetActive(false);
        // 全セルを白ベタに戻す(色替え演出後などでも確実に白でカバーする)。
        for (int i = 0; i < cellImages.Length; i++)
            if (cellImages[i] != null) cellImages[i].color = Color.white;
        SetCenterOutDelays(mosaicCoverTime);
        // coverIn: セルが scale0→1 でポップイン。中央発の順序で外周へ広がる。
        await Animate(coverIn: true);
        await Hold(whiteoutHoldTime);
    }

    // 中心からの距離に線形な遅延(中央=0→隅=最大)を各セルに割り当てる。カバーと
    // 解像の両方でこの中央発順序を使う。span は全体の所要時間。
    private void SetCenterOutDelays(float span)
    {
        float usable = span - mosaicJitterTime;
        for (int i = 0; i < delays.Length; i++)
        {
            // baseDelays は中心からの距離を spreadTime に正規化した値。
            float order = baseDelays[i] / spreadTime; // 0(中心)→1(隅)
            delays[i] = order * usable + Random.Range(0f, mosaicJitterTime);
        }
    }

    // プレイ開始用: 白カバーがピクセルブロック単位で中央から外周へ順に欠けて
    // いき、背後のプレイ画面が画面中央から広がって現れる(このゲームに従来から
    // ある中央発のピクセルワイプ)。第30便で入れた終盤圧縮(外周を前倒し)と
    // 大きめジッタは「ラジアル消去」に見えたため撤回し、従来の Reveal と同じ
    // 中心距離に線形な順序+小さめジッタに戻す(第31便)。
    public async Task MosaicReveal()
    {
        if (fadeGroup != null) fadeGroup.alpha = 1f;
        if (whiteSheet != null) whiteSheet.gameObject.SetActive(false);
        SetCenterOutDelays(mosaicRevealTime);
        await Animate(coverIn: false);
        gameObject.SetActive(false);
    }

    public void SetColor(Color color)
    {
        Build();
        for (int i = 0; i < cellImages.Length; i++)
        {
            if (cellImages[i] != null) cellImages[i].color = color;
        }
    }

    // Pixels vanish from the center, revealing the screen behind.
    public async Task Reveal()
    {
        if (fadeGroup != null) fadeGroup.alpha = 1f;
        RollDelays();
        await Animate(coverIn: false);
        gameObject.SetActive(false);
    }

    private void RollDelays()
    {
        for (int i = 0; i < delays.Length; i++)
        {
            delays[i] = baseDelays[i] + Random.Range(0f, jitterTime);
        }
    }

    private async Task Animate(bool coverIn)
    {
        float t = 0f;
        // 遅延の最大値から所要時間を出す(中心ワイプとモザイクで長さが違う)。
        float total = 0f;
        for (int i = 0; i < delays.Length; i++)
        {
            if (delays[i] > total) total = delays[i];
        }
        total += 0.05f;
        Vector3 onScale = coverIn ? Vector3.one : Vector3.zero;
        Vector3 offScale = coverIn ? Vector3.zero : Vector3.one;
        for (int i = 0; i < cells.Length; i++) cells[i].localScale = offScale;

        while (t < total)
        {
            t += AnimationDelta();
            for (int i = 0; i < cells.Length; i++)
            {
                if (t >= delays[i]) cells[i].localScale = onScale;
            }
            await Task.Yield();
            if (this == null) return;
        }
        for (int i = 0; i < cells.Length; i++) cells[i].localScale = onScale;
    }

    private async Task Hold(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += AnimationDelta();
            await Task.Yield();
            if (this == null) return;
        }
    }

    private static float AnimationDelta()
    {
        return Mathf.Min(Time.unscaledDeltaTime, maxAnimationDelta);
    }
}

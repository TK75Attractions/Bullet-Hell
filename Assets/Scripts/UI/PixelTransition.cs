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
    private const float maxAnimationDelta = 1f / 30f;
    private const float loadedSceneHoldTime = 0.18f;
    private const float coveredHoldTime = 0.08f;

    private RectTransform[] cells;
    private Image[] cellImages;
    private float[] baseDelays;
    private float[] delays;
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

    // Pixels appear from the center until the screen is fully covered.
    public async Task Cover()
    {
        gameObject.SetActive(true);
        RollDelays();
        await Animate(coverIn: true);
        await Hold(coveredHoldTime);
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
        float total = spreadTime + jitterTime + 0.05f;
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

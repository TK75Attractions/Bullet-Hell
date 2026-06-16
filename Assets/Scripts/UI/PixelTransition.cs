using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

// Full-screen pixel-mosaic transition: dark cells pop in from the center
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

    private static readonly Color shadeDark = Color.white;
    private static readonly Color shadeLight = Color.white;

    private RectTransform[] cells;
    private float[] baseDelays;
    private float[] delays;
    private bool built;

    private void Awake()
    {
        Build();
        gameObject.SetActive(false);
    }

    private void Build()
    {
        if (built) return;
        built = true;
        int count = cols * rows;
        cells = new RectTransform[count];
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
                img.color = Color.Lerp(shadeDark, shadeLight, Random.value);

                cells[i] = rect;
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
            t += Time.deltaTime;
            for (int i = 0; i < cells.Length; i++)
            {
                if (t >= delays[i]) cells[i].localScale = onScale;
            }
            await Task.Yield();
            if (this == null) return;
        }
        for (int i = 0; i < cells.Length; i++) cells[i].localScale = onScale;
    }
}

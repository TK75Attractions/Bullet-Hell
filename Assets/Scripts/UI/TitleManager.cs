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
            logoBaseY = logoRect.anchoredPosition.y;
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

        group.alpha = 1f;
        transform.localScale = Vector3.one;
        dismissed = false;
        gameObject.SetActive(true);
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
}

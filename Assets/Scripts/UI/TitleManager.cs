using System.Threading.Tasks;
using UnityEngine;
using TMPro;

public class TitleManager : MonoBehaviour
{
    private CanvasGroup group;
    private TMP_Text promptText;
    private RectTransform logoRect;
    private float logoBaseY;
    private float animTime;
    private bool dismissed;

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
            for (int i = 0; i < shapesRoot.childCount; i++)
            {
                RectTransform rect = shapesRoot.GetChild(i) as RectTransform;
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
        dismissed = false;
        gameObject.SetActive(true);
    }

    // Idle animation: prompt blinks, logo floats, background shapes drift and spin slowly.
    public void UpdateTitle(float dt)
    {
        if (dismissed) return;
        animTime += dt;
        if (promptText != null)
        {
            promptText.alpha = 0.35f + 0.55f * (0.5f + 0.5f * Mathf.Sin(animTime * 3.5f));
        }
        if (logoRect != null)
        {
            logoRect.anchoredPosition = new Vector2(logoRect.anchoredPosition.x, logoBaseY + Mathf.Sin(animTime * 1.2f) * 10f);
        }
        for (int i = 0; i < shapes.Length; i++)
        {
            ShapeAnim s = shapes[i];
            if (s.rect == null) continue;
            s.rect.anchoredPosition = s.basePos + new Vector2(
                Mathf.Sin(animTime * s.speedX + s.phase) * s.ampX,
                Mathf.Cos(animTime * s.speedY + s.phase * 1.3f) * s.ampY);
            s.rect.Rotate(0f, 0f, s.rotSpeed * dt);
        }
    }

    // Fade out and disable when the player presses the button.
    public async void Dismiss()
    {
        if (dismissed) return;
        dismissed = true;
        float d = 0.4f;
        while (d > 0f)
        {
            d -= Time.deltaTime;
            float p = Mathf.Clamp01(d / 0.4f);
            group.alpha = p * p;
            await Task.Yield();
            if (this == null || group == null) return;
        }
        group.alpha = 0f;
        gameObject.SetActive(false);
    }
}

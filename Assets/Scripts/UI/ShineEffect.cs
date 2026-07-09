using UnityEngine;
using UnityEngine.UI;

// Periodic gloss sweep for selected buttons: a thin translucent white
// parallelogram appears at the left edge, glides to the right with an
// ease-in-out curve and fades away. Attach to an Image placed over the button.
public class ShineEffect : MonoBehaviour
{
    [SerializeField] private float interval = 2.4f;
    [SerializeField] private float sweepTime = 0.65f;
    [SerializeField] private float range = 330f;
    [SerializeField] private float maxAlpha = 0.5f;

    private Image image;
    private RectTransform rect;
    private float time;

    private void Awake()
    {
        image = GetComponent<Image>();
        rect = (RectTransform)transform;
        // Desynchronize multiple shines so they don't sweep in unison.
        time = Random.Range(0f, interval);
        SetAlpha(0f);
    }

    private void Update()
    {
        time += Time.deltaTime;
        float cycle = Mathf.Repeat(time, interval);
        float p = cycle / sweepTime;
        if (p >= 1f)
        {
            SetAlpha(0f);
            return;
        }

        float eased = p < 0.5f ? 4f * p * p * p : 1f - Mathf.Pow(-2f * p + 2f, 3f) / 2f;
        rect.anchoredPosition = new Vector2(Mathf.Lerp(-range, range, eased), rect.anchoredPosition.y);
        SetAlpha(maxAlpha * Mathf.Sin(Mathf.PI * p));
    }

    private void SetAlpha(float a)
    {
        Color c = image.color;
        c.a = a;
        image.color = c;
    }
}

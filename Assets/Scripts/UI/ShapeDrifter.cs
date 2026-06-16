using UnityEngine;

// Slowly drifts and spins all child RectTransforms (decorative background
// shapes, Just Shapes & Beats style). Self-contained: attach to a container
// and it animates whatever children it has.
public class ShapeDrifter : MonoBehaviour
{
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
    private float animTime;

    private void Awake()
    {
        shapes = new ShapeAnim[transform.childCount];
        for (int i = 0; i < transform.childCount; i++)
        {
            RectTransform rect = transform.GetChild(i) as RectTransform;
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

    private void Update()
    {
        animTime += Time.deltaTime;
        for (int i = 0; i < shapes.Length; i++)
        {
            ShapeAnim s = shapes[i];
            if (s.rect == null) continue;
            s.rect.anchoredPosition = s.basePos + new Vector2(
                Mathf.Sin(animTime * s.speedX + s.phase) * s.ampX,
                Mathf.Cos(animTime * s.speedY + s.phase * 1.3f) * s.ampY);
            s.rect.Rotate(0f, 0f, s.rotSpeed * Time.deltaTime);
        }
    }
}

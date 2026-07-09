using UnityEngine;
using UnityEngine.UI;

// Crisp capsule/circle mesh used by settings controls. Unlike
// SoftCircleGraphic it has no feathered edge, so small knobs stay sharp.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class CapsuleGraphic : MaskableGraphic
{
    [SerializeField, Range(12, 64)] private int segments = 32;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = GetPixelAdjustedRect();
        if (rect.width <= 0f || rect.height <= 0f) return;

        float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
        float leftCenter = rect.xMin + radius;
        float rightCenter = rect.xMax - radius;
        Vector2 center = rect.center;
        Color32 vertexColor = color;
        int count = Mathf.Max(12, segments);

        vh.AddVert(center, vertexColor, Vector2.zero);
        for (int i = 0; i < count; i++)
        {
            float angle = i / (float)count * Mathf.PI * 2f;
            float cosine = Mathf.Cos(angle);
            float centerX = cosine >= 0f ? rightCenter : leftCenter;
            Vector2 point = new Vector2(
                centerX + cosine * radius,
                center.y + Mathf.Sin(angle) * radius);
            vh.AddVert(point, vertexColor, Vector2.zero);
        }

        for (int i = 0; i < count; i++)
        {
            int next = (i + 1) % count;
            vh.AddTriangle(0, i + 1, next + 1);
        }
    }
}

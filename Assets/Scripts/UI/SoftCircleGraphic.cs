using UnityEngine;
using UnityEngine.UI;

// Resolution-independent radial circle for the title background. A generated
// mesh avoids scaling Unity's tiny built-in UI sprite, which made the blurred
// edge look pixelated at several hundred pixels wide.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class SoftCircleGraphic : MaskableGraphic
{
    [SerializeField, Range(24, 128)] private int segments = 96;
    [SerializeField, Range(4, 20)] private int rings = 12;
    [SerializeField, Range(0.1f, 1f)] private float edgeSoftness = 0.48f;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = GetPixelAdjustedRect();
        float radiusX = rect.width * 0.5f;
        float radiusY = rect.height * 0.5f;
        if (radiusX <= 0f || radiusY <= 0f) return;

        Vector2 center = rect.center;
        Color32 centerColor = color;
        vh.AddVert(center, centerColor, Vector2.zero);

        int safeSegments = Mathf.Max(24, segments);
        int safeRings = Mathf.Max(4, rings);
        float fadeStart = 1f - edgeSoftness;

        for (int ring = 1; ring <= safeRings; ring++)
        {
            float radius = ring / (float)safeRings;
            float fade = radius <= fadeStart
                ? 1f
                : 1f - Mathf.SmoothStep(0f, 1f, (radius - fadeStart) / Mathf.Max(0.001f, edgeSoftness));
            Color ringColor = color;
            ringColor.a *= fade;
            Color32 vertexColor = ringColor;

            for (int segment = 0; segment < safeSegments; segment++)
            {
                float angle = segment / (float)safeSegments * Mathf.PI * 2f;
                Vector2 position = center + new Vector2(Mathf.Cos(angle) * radiusX * radius, Mathf.Sin(angle) * radiusY * radius);
                vh.AddVert(position, vertexColor, Vector2.zero);
            }
        }

        int firstRing = 1;
        for (int segment = 0; segment < safeSegments; segment++)
        {
            int next = (segment + 1) % safeSegments;
            vh.AddTriangle(0, firstRing + segment, firstRing + next);
        }

        for (int ring = 1; ring < safeRings; ring++)
        {
            int inner = 1 + (ring - 1) * safeSegments;
            int outer = inner + safeSegments;
            for (int segment = 0; segment < safeSegments; segment++)
            {
                int next = (segment + 1) % safeSegments;
                vh.AddTriangle(inner + segment, outer + segment, outer + next);
                vh.AddTriangle(inner + segment, outer + next, inner + next);
            }
        }
    }
}

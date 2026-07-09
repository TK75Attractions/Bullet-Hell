using UnityEngine;
using UnityEngine.UI;

// A font-independent triangle whose three sides are always equal.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class EquilateralTriangleGraphic : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        Rect rect = GetPixelAdjustedRect();
        float side = Mathf.Min(rect.width, rect.height * 2f / Mathf.Sqrt(3f));
        float height = side * Mathf.Sqrt(3f) * 0.5f;
        Vector2 center = rect.center;

        vertexHelper.AddVert(new Vector3(center.x, center.y + height * 0.5f), color, new Vector2(0.5f, 1f));
        vertexHelper.AddVert(new Vector3(center.x - side * 0.5f, center.y - height * 0.5f), color, Vector2.zero);
        vertexHelper.AddVert(new Vector3(center.x + side * 0.5f, center.y - height * 0.5f), color, Vector2.right);
        vertexHelper.AddTriangle(0, 1, 2);
    }
}

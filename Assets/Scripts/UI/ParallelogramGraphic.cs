using UnityEngine;
using UnityEngine.UI;

// Lightweight UI graphic used by the countdown warning wipe. The mesh keeps
// diagonal side edges as its RectTransform width changes, so the growing red
// area follows the slanted timer-cell silhouette instead of becoming a box.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ParallelogramGraphic : MaskableGraphic
{
    [SerializeField, Min(0f)] private float slant = 34f;
    [SerializeField] private bool slantRightEdge;

    public float Slant
    {
        get => slant;
        set
        {
            slant = Mathf.Max(0f, value);
            SetVerticesDirty();
        }
    }
    public bool SlantRightEdge
    {
        get => slantRightEdge;
        set
        {
            slantRightEdge = value;
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = GetPixelAdjustedRect();
        if (rect.width <= 0f || rect.height <= 0f) return;

        // 斜辺つき(=真の平行四辺形)の幾何は skew ≤ width まで成立する。
        // 細い白スラッシュは幅の大半が skew のため、従来の width*0.5 クランプ
        // だと線ごとに角度が潰れ「2枚のスラッシュが平行でない」原因になる。
        // 幅0から伸びるワイプ(左辺のみ斜め)は従来どおり width*0.5 で形を保つ。
        float maxSkew = slantRightEdge ? rect.width : rect.width * 0.5f;
        float skew = Mathf.Min(slant, maxSkew);
        Color32 vertexColor = color;

        AddVertex(vh, new Vector2(rect.xMin, rect.yMin), vertexColor);
        AddVertex(vh, new Vector2(rect.xMin + skew, rect.yMax), vertexColor);
        AddVertex(vh, new Vector2(rect.xMax, rect.yMax), vertexColor);
        // Timer wipes keep a vertical right edge; standalone ribbons can opt into
        // a matching diagonal right edge to form a true parallelogram.
        float bottomRight = slantRightEdge ? rect.xMax - skew : rect.xMax;
        AddVertex(vh, new Vector2(bottomRight, rect.yMin), vertexColor);

        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(0, 2, 3);
    }

    private static void AddVertex(VertexHelper vh, Vector2 position, Color32 color)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = color;
        vh.AddVert(vertex);
    }
}

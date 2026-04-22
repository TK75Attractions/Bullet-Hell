using UnityEngine;
using Unity.Mathematics;
using UnityEngine.UIElements;

public class Scroll : MonoBehaviour
{
    private const float length = 840f;
    private CanvasGroup CG;
    private RectTransform area;
    private float currentPos = 0f;
    private float areaHeight = 0f;
    private bool isTransitioning = false;
    private float velocity = 0f;
    private const float accel = 0.3f;

    public void Init()
    {
        CG = GetComponent<CanvasGroup>();
        area = transform.Find("Area").GetComponent<RectTransform>();
    }

    public void UpdateScroll(float dt)
    {
        dt *= 8;
        float targetPos = currentPos;
        velocity += (targetPos - area.anchoredPosition.y) * accel * dt;
        area.anchoredPosition += new Vector2(0, velocity * dt);
        area.sizeDelta = new Vector2(area.sizeDelta.x, areaHeight / (1 + math.abs(velocity) / 200));
    }

    public void UpdateArea(int index, int max)
    {
        if (max <= 0) return;
        currentPos = length / 2 - (index + 0.5f) * (length / max);
        areaHeight = length / max;
        velocity = (currentPos - area.anchoredPosition.y) * accel * 5;
    }

    public void SetAlpha(float alpha)
    {
        CG.alpha = alpha;
    }
}

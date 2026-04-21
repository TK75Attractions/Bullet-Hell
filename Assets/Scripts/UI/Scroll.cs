using UnityEngine;
using UnityEngine.UIElements;

public class Scroll : MonoBehaviour
{
    private const float length = 840f;
    private CanvasGroup CG;
    private RectTransform area;
    private float currentPos = 0f;
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
        float targetPos = currentPos;
        velocity += (targetPos - area.anchoredPosition.x) * accel * dt;
        area.anchoredPosition += new Vector2(velocity * dt, 0);
    }

    public void UpdateArea(int index, int max)
    {
        if (max <= 0) return;
        currentPos = length / 2 - (index + 0.5f) * (length / max);
        velocity = (currentPos - area.anchoredPosition.x) * accel * 1;
    }
}

using UnityEngine;
using UnityEngine.UI;

public class UIScrollBar : MonoBehaviour
{
    private RectTransform barArea;

    public void Init()
    {
        barArea = transform.Find("BarArea").GetComponent<RectTransform>();
    }

    public void UpdateUI(int index, int maxIndex)
    {
        float normalizedIndex = (float)index / maxIndex;
        barArea.anchoredPosition = new Vector2(0, -normalizedIndex * barArea.sizeDelta.y);
    }
}

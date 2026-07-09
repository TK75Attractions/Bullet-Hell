using UnityEngine;
using TMPro;

public class StageBox : MonoBehaviour
{
    private static readonly float normalScale = 1f;
    private static readonly float miniScale = 0.8f;

    private static readonly float interval = 140f;

    private CanvasGroup CG;
    private TMP_Text stageNameText;
    private RectTransform rectTransform;
    public void Init()
    {
        // Initialize the stage box here
        stageNameText = transform.Find("StageName").GetComponent<TMP_Text>();
        rectTransform = GetComponent<RectTransform>();
        CG = GetComponent<CanvasGroup>();
    }

    public void SetStageName(string name)
    {
        stageNameText.text = name;
    }

    public void SetPosition(float progress)
    {
        float a = 0.3f;
        if (2 < progress && progress < 3) a += (progress - 2) * 0.7f;
        else if (3 <= progress && progress < 4) a += (4 - progress) * 0.7f;
        if (Mathf.Approximately(progress, 0f) || Mathf.Approximately(progress, 6f)) a = 0;

        CG.alpha = a;
        rectTransform.localScale = Vector3.one * (miniScale + (normalScale - miniScale) * a);
        float y = Mathf.Round((3f - progress) * interval);
        rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, y);
    }
}

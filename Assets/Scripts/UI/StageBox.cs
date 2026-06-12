using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StageBox : MonoBehaviour
{
    private static readonly float normalScale = 1f;
    private static readonly float miniScale = 0.8f;

    private static readonly float interval = 140f;

    // Unselected bars render as muted navy, selected as the sprite's full blue.
    private static readonly Color barDimColor = new Color(0.42f, 0.55f, 0.72f);
    private static readonly Color textDimColor = new Color(0.66f, 0.78f, 0.9f);

    private CanvasGroup CG;
    private TMP_Text stageNameText;
    private RectTransform rectTransform;
    private Image backImage;
    private float baseScale = 1f;

    public void Init()
    {
        stageNameText = transform.Find("StageName").GetComponent<TMP_Text>();
        rectTransform = GetComponent<RectTransform>();
        CG = GetComponent<CanvasGroup>();
        Transform back = transform.Find("Back");
        if (back != null) backImage = back.GetComponent<Image>();
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
        baseScale = miniScale + (normalScale - miniScale) * a;
        rectTransform.localScale = Vector3.one * baseScale;
        float y = Mathf.Round((3f - progress) * interval);
        rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, y);

        float selection = Mathf.Clamp01((a - 0.3f) / 0.7f);
        if (backImage != null) backImage.color = Color.Lerp(barDimColor, Color.white, selection);
        stageNameText.color = Color.Lerp(textDimColor, Color.white, selection);
    }

    // Gentle breathing applied on top of the base scale while selected and idle.
    public void SetPulse(float multiplier)
    {
        rectTransform.localScale = Vector3.one * (baseScale * multiplier);
    }
}

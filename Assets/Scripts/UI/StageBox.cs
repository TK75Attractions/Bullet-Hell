using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class StageBox : MonoBehaviour
{
    private static readonly float normalScale = 1.08f;
    private static readonly float miniScale = 0.82f;

    private static readonly float interval = 140f;

    private CanvasGroup CG;
    private TMP_Text stageNameText;
    private RectTransform rectTransform;
    private Image stageBarImage;
    private Color normalBarColor = new Color(0.07f, 0.2f, 0.33f, 0.95f);
    private Color selectedBarColor = new Color(0.05f, 0.62f, 0.95f, 1f);
    private Color hiddenTextColor = new Color(1f, 1f, 1f, 0.45f);
    public void Init()
    {
        stageNameText = transform.Find("StageName").GetComponent<TMP_Text>();
        stageBarImage = transform.Find("StageBar").GetComponent<Image>();
        rectTransform = GetComponent<RectTransform>();
        CG = GetComponent<CanvasGroup>();
        stageNameText.textWrappingMode = TextWrappingModes.NoWrap;
        stageNameText.overflowMode = TextOverflowModes.Ellipsis;
        stageNameText.fontSize = 60f;
    }

    public void SetStageName(string name)
    {
        stageNameText.text = name;
    }

    public void SetPosition(float progress)
    {
        float distance = Mathf.Abs(progress - 3f);
        float focus = Mathf.Clamp01(1f - distance);
        float a = Mathf.Lerp(0.28f, 1f, Mathf.SmoothStep(0f, 1f, focus));
        if (string.IsNullOrEmpty(stageNameText.text) || progress <= 0.05f || progress >= 5.95f) a = 0f;

        CG.alpha = a;
        float scale = Mathf.Lerp(miniScale, normalScale, Mathf.SmoothStep(0f, 1f, focus));
        rectTransform.localScale = Vector3.one * scale;
        float y = Mathf.Round((3f - progress) * interval);
        float x = Mathf.Lerp(-40f, 0f, focus);
        rectTransform.anchoredPosition = new Vector2(x, y);
        stageBarImage.color = Color.Lerp(normalBarColor, selectedBarColor, Mathf.SmoothStep(0f, 1f, focus));
        stageNameText.color = Color.Lerp(hiddenTextColor, Color.white, focus);
    }
}

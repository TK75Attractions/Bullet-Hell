using UnityEngine;
using TMPro;

public class Header : MonoBehaviour
{
    private TMP_Text notesText;
    private TMP_Text timerText;
    private RectTransform notesRect;
    private RectTransform timerRect;
    public bool isready;
    private int currentState = -1;

    public void Init()
    {
        notesText = transform.Find("NotesText").GetComponent<TMP_Text>();
        timerText = transform.Find("TimerText").GetComponent<TMP_Text>();
        notesRect = notesText.GetComponent<RectTransform>();
        timerRect = timerText.GetComponent<RectTransform>();

        notesRect.anchoredPosition = new Vector2(-550f, 0f);
        notesRect.sizeDelta = new Vector2(900f, 96f);
        notesText.textWrappingMode = TextWrappingModes.NoWrap;
        notesText.overflowMode = TextOverflowModes.Overflow;
        notesText.alignment = TextAlignmentOptions.Left;
        notesText.fontSize = 52f;

        timerRect.anchoredPosition = new Vector2(770f, 0f);
        timerRect.sizeDelta = new Vector2(260f, 82f);
        timerText.textWrappingMode = TextWrappingModes.NoWrap;
        timerText.alignment = TextAlignmentOptions.Center;
        timerText.fontSize = 60f;

        isready = true;
        TransitionNotes(0);
        UpdateTimer(0f, 1f);
    }

    public void TransitionNotes(int state)
    {
        if (state != currentState)
        {
            switch (state)
            {
                case 0:
                    notesText.text = "<size=28>きょく せんたく</size>\n曲選択 / MUSIC SELECT";
                    break;
                case 1:
                    notesText.text = "<size=28>なんいど せんたく</size>\n難易度選択 / DIFFICULTY SELECT";
                    break;
                case 2:
                    notesText.text = "<size=28>プレイ</size>\nNOW PLAYING";
                    break;
                default:
                    break;
            }
            currentState = state;
        }
    }

    public void UpdateTimer(float time, float normalizedRemaining)
    {
        int seconds = Mathf.CeilToInt(Mathf.Max(0f, time));
        timerText.text = $"{seconds / 60}:{seconds % 60:00}";

        float danger = 1f - Mathf.Clamp01(normalizedRemaining);
        Color safeColor = Color.white;
        Color dangerColor = new Color(1f, 0.16f, 0.22f, 1f);
        timerText.color = Color.Lerp(safeColor, dangerColor, Mathf.SmoothStep(0f, 1f, danger));

        float pulse = 1f;
        if (time <= 10f && time > 0f)
        {
            pulse = 1f + Mathf.Sin(Time.unscaledTime * 12f) * 0.035f;
        }
        timerRect.localScale = Vector3.one * pulse;
    }
}

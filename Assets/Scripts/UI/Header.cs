using UnityEngine;
using TMPro;

public class Header : MonoBehaviour
{
    private static readonly Color timerNormalColor = Color.white;
    private static readonly Color timerWarningColor = new Color(1f, 0.25f, 0.3f);

    private TMP_Text notesText;
    private TMP_Text timerText;
    private RectTransform timerRect;
    public bool isready;
    private int currentState;

    public void Init()
    {
        notesText = transform.Find("NotesText").GetComponent<TMP_Text>();
        timerText = transform.Find("TimerText").GetComponent<TMP_Text>();
        timerRect = timerText.GetComponent<RectTransform>();
        currentState = 0;
        notesText.text = "曲選択 / MUSIC SELECT";
        timerText.text = string.Empty;
        isready = true;
    }

    public void TransitionNotes(int state)
    {
        if (state != currentState)
        {
            switch (state)
            {
                case 0:
                    notesText.text = "曲選択 / MUSIC SELECT";
                    break;
                case 1:
                    notesText.text = "難易度選択 / DIFFICULTY SELECT";
                    break;
                case 2:
                    // Transition to gameplay screen
                    break;
                default:
                    break;
            }
            currentState = state;
        }
    }

    public void UpdateTimer(float time)
    {
        if (time < 0f) time = 0f;
        int minutes = (int)(time / 60f);
        int seconds = (int)(time % 60f);
        timerText.text = $"残り時間 <b>{minutes}:{seconds:00}</b>";

        if (time <= 10f && time > 0f)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f);
            timerText.color = Color.Lerp(timerNormalColor, timerWarningColor, pulse);
            timerRect.localScale = Vector3.one * (1f + 0.04f * pulse);
        }
        else
        {
            timerText.color = timerNormalColor;
            timerRect.localScale = Vector3.one;
        }
    }
}

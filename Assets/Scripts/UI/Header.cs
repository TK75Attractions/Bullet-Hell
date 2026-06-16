using UnityEngine;
using TMPro;

public class Header : MonoBehaviour
{
    private static readonly Color timerNormalColor = Color.white;
    private static readonly Color timerWarningColor = new Color(1f, 0.25f, 0.3f);

    private TMP_Text notesText;
    private TMP_Text notesRubyA;
    private TMP_Text notesRubyB;
    private RectTransform notesRubyARect;
    private RectTransform notesRubyBRect;
    private TMP_Text timerText;
    private RectTransform timerRect;
    public bool isready;
    private int currentState;

    public void Init()
    {
        notesText = transform.Find("NotesText").GetComponent<TMP_Text>();
        timerText = transform.Find("TimerText").GetComponent<TMP_Text>();
        timerRect = timerText.GetComponent<RectTransform>();
        Transform rubyA = transform.Find("NotesRubyA");
        if (rubyA != null) { notesRubyA = rubyA.GetComponent<TMP_Text>(); notesRubyARect = rubyA.GetComponent<RectTransform>(); }
        Transform rubyB = transform.Find("NotesRubyB");
        if (rubyB != null) { notesRubyB = rubyB.GetComponent<TMP_Text>(); notesRubyBRect = rubyB.GetComponent<RectTransform>(); }
        currentState = 0;
        notesText.text = "曲選択 / MUSIC SELECT";
        SetNotesRuby("きょく", -773f, "せんたく", -692f);
        timerText.text = string.Empty;
        isready = true;
    }

    // Readings sit exactly over their kanji: NotesText is left-aligned at x=-800
    // with full-width glyphs 54px wide, so each kanji center is -800+27+54*i.
    private void SetNotesRuby(string textA, float xA, string textB, float xB)
    {
        if (notesRubyA != null)
        {
            notesRubyA.text = textA;
            notesRubyARect.anchoredPosition = new Vector2(xA, notesRubyARect.anchoredPosition.y);
        }
        if (notesRubyB != null)
        {
            notesRubyB.text = textB;
            notesRubyBRect.anchoredPosition = new Vector2(xB, notesRubyBRect.anchoredPosition.y);
        }
    }

    public void TransitionNotes(int state)
    {
        if (state != currentState)
        {
            switch (state)
            {
                case 0:
                    notesText.text = "曲選択 / MUSIC SELECT";
                    SetNotesRuby("きょく", -773f, "せんたく", -692f);
                    break;
                case 1:
                    notesText.text = "難易度選択 / DIFFICULTY SELECT";
                    SetNotesRuby("なんいど", -719f, "せんたく", -584f);
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
        // The「残り時間」label (with ruby) is a static scene object; this text is the time only.
        timerText.text = $"<b>{minutes}:{seconds:00}</b>";

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

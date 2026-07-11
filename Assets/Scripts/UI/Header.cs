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
        // 「曲選択」= 曲(0)・選択(1,2)。読みを各漢字の真上へ実測配置する。
        SetNotesRuby("きょく", -773f, 0, 1, "せんたく", -692f, 1, 2);
        timerText.text = string.Empty;
        isready = true;
    }

    // 読みは対応漢字の真上に置く。従来は全角 54px 前提の算術 x(NotesText は
    // 左揃え・x=-800、各漢字中心 -800+27+54*i)だったが、CJK フォールバックの
    // 実アドバンスと数 px ずれ得るため、TmpAlign.PlaceRubyOverKanji で notesText の
    // 指定漢字グリフの実測中心へ合わせる(算術 x はフォールバックとして残す)。
    // startX/lenX は notesText 文字列先頭からの漢字範囲。
    private void SetNotesRuby(string textA, float xA, int startA, int lenA,
        string textB, float xB, int startB, int lenB)
    {
        if (notesRubyA != null)
        {
            notesRubyA.text = textA;
            notesRubyARect.anchoredPosition = new Vector2(xA, notesRubyARect.anchoredPosition.y);
            TmpAlign.PlaceRubyOverKanji(notesText, notesRubyARect, startA, lenA);
        }
        if (notesRubyB != null)
        {
            notesRubyB.text = textB;
            notesRubyBRect.anchoredPosition = new Vector2(xB, notesRubyBRect.anchoredPosition.y);
            TmpAlign.PlaceRubyOverKanji(notesText, notesRubyBRect, startB, lenB);
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
                    // 曲(0)・選択(1,2)
                    SetNotesRuby("きょく", -773f, 0, 1, "せんたく", -692f, 1, 2);
                    break;
                case 1:
                    notesText.text = "難易度選択 / DIFFICULTY SELECT";
                    // 難易度(0,1,2)・選択(3,4)
                    SetNotesRuby("なんいど", -719f, 0, 3, "せんたく", -584f, 3, 2);
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

using UnityEngine;
using TMPro;

public class Header : MonoBehaviour
{
    private TMP_Text notesText;
    private TMP_Text timerText;
    public bool isready;
    private int currentState;

    public void Init()
    {
        // Initialize the header here
        notesText = transform.Find("NotesText").GetComponent<TMP_Text>();
        timerText = transform.Find("TimerText").GetComponent<TMP_Text>();
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
        timerText.text = $"残り時間: {time:F1}秒";
    }
}

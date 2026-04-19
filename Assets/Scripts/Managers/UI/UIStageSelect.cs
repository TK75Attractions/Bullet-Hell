using UnityEngine;

public class UIStageSelect : MonoBehaviour
{
    private UIHead head;
    private UIScrollBar scrollBar;
    private UIStageBarParent stageBarParent;
    private UIDescription description;
    private UIDifficulty difficulty;

    private enum UIState
    {
        Stage,
        Difficulty,
    }

    private UIState state;

    public void Init()
    {

    }

    public void UpdateUI(bool up, bool down, bool button)
    {
        if (state == UIState.Stage)
        {

        }
        else if (state == UIState.Difficulty)
        {
        }

    }


}

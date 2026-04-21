using System.Xml.Serialization;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.VisualScripting;

public class StageSelectManager : MonoBehaviour
{
    private DefficultyBar defficultyBar;
    private Header header;
    private Scroll scroll;
    private StageBar stageBar;
    private StageDescription stageDescription;

    private enum State
    {
        Music,
        Difficulty,
    }

    private State state = State.Music;

    public void Init()
    {
        defficultyBar = GetComponentInChildren<DefficultyBar>();
        header = GetComponentInChildren<Header>();
        scroll = GetComponentInChildren<Scroll>();
        stageBar = GetComponentInChildren<StageBar>();
        stageDescription = GetComponentInChildren<StageDescription>();

        defficultyBar.Init();
        header.Init();
        scroll.Init();
        stageBar.Init();
        stageDescription.Init();

        state = State.Music;
    }

    public void UpdateSelect(bool up, bool down, float dt, bool button)
    {
        scroll.UpdateScroll(dt);

        switch (state)
        {
            case State.Music:
                if (button)
                {
                    state = State.Difficulty;
                    break;
                }
                else
                {
                    if (up) stageBar.Up();
                    else if (down) stageBar.Down();
                    scroll.UpdateArea(stageBar.currentStage, GManager.Control.SDB.stages.Count);
                    break;
                }
            case State.Difficulty:
                //if (up) defficultyBar.Up();
                //else if (down) defficultyBar.Down();
                break;
            default:
                break;
        }
    }
}

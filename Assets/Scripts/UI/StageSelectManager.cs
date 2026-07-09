using System.Xml.Serialization;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.VisualScripting;

public class StageSelectManager : MonoBehaviour
{
    private CanvasGroup variableCG;
    private CanvasGroup staticCG;
    private DefficultyBar defficultyBar;
    private Header header;
    private Scroll scroll;
    private StageBar stageBar;
    private StageDescription stageDescription;

    private enum State
    {
        Music,
        Difficulty,
        InGame,
    }

    private State state = State.Music;

    private bool isTransitioning = false;

    public void Init()
    {
        variableCG = GetComponent<CanvasGroup>();
        staticCG = transform.parent.parent.Find("StaticCanvas").Find("StageBoxParent").GetComponent<CanvasGroup>();
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
        defficultyBar.SetStage(GManager.Control.SDB.GetStage(stageBar.currentStage));

        state = State.Music;

    }

    public void UpdateSelect(bool up, bool down, float dt, bool button, bool back)
    {
        scroll.UpdateScroll(dt);
        if (isTransitioning) return;

        switch (state)
        {
            case State.Music:
                if (button)
                {
                    defficultyBar.SetStage(GManager.Control.SDB.GetStage(stageBar.currentStage));
                    state = State.Difficulty;
                    TransitionToDifficulty();
                    break;
                }
                else
                {
                    if (up) stageBar.Up();
                    else if (down) stageBar.Down();
                    scroll.UpdateArea(stageBar.currentStage, GManager.Control.SDB.GetStageCount());
                    break;
                }
            case State.Difficulty:
                if (back)
                {
                    TransitionToMusic();
                    break;
                }
                else if (button)
                {
                    GManager.Control.GoGame(stageBar.currentStage, defficultyBar.SelectedDifficulty);
                    state = State.InGame;
                    variableCG.alpha = 0;
                    staticCG.alpha = 0;
                }
                else
                {
                    if (up) defficultyBar.Up();
                    else if (down) defficultyBar.Down();
                }
                break;
            case State.InGame:
                break;
            default:
                break;
        }
    }

    public async void TransitionToDifficulty()
    {
        if (!isTransitioning)
        {
            isTransitioning = true;
            float d = 0.5f;

            while (d > 0)
            {
                d -= Time.deltaTime;
                float p = 1 - (d / 0.5f);
                float progress = -p * (p - 2);
                stageDescription.Transition(progress);
                stageBar.SetAlpha(1 - progress);
                scroll.SetAlpha(1 - progress);
                defficultyBar.SetAlpha(progress);
                await Task.Yield();
            }

            stageDescription.Transition(1);
            header.TransitionNotes(1);
            stageBar.SetAlpha(0);
            scroll.SetAlpha(0);
            defficultyBar.SetAlpha(1);

            isTransitioning = false;
        }
    }

    public async void TransitionToMusic()
    {
        if (!isTransitioning)
        {
            isTransitioning = true;
            float d = 0.5f;

            while (d > 0)
            {
                d -= Time.deltaTime;
                float p = 1 - (d / 0.5f);
                float progress = -p * (p - 2);
                stageDescription.Transition(1 - progress);
                stageBar.SetAlpha(progress);
                scroll.SetAlpha(progress);
                defficultyBar.SetAlpha(1 - progress);
                await Task.Yield();
            }

            stageDescription.Transition(0);
            header.TransitionNotes(0);
            stageBar.SetAlpha(1);
            scroll.SetAlpha(1);
            defficultyBar.SetAlpha(0);
            state = State.Music;

            isTransitioning = false;
        }
    }

}

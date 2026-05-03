using System.Threading.Tasks;
using UnityEngine;

public class StageSelectManager : MonoBehaviour
{
    private IGameStarter starter;
    private IStageDB<IStageData> SDB;
    private CanvasGroup variableCG;
    private CanvasGroup staticCG;
    private DifficultyBar difficultyBar;
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

    public void Init(IStageDB<IStageData> stageDB, IGameStarter gameStarter)
    {
        starter = gameStarter;
        SDB = stageDB;

        variableCG = GetComponent<CanvasGroup>();
        staticCG = transform.parent.parent.Find("StaticCanvas").Find("StageBoxParent").GetComponent<CanvasGroup>();
        difficultyBar = GetComponentInChildren<DifficultyBar>();
        header = GetComponentInChildren<Header>();
        scroll = GetComponentInChildren<Scroll>();
        stageBar = GetComponentInChildren<StageBar>();
        stageDescription = GetComponentInChildren<StageDescription>();

        difficultyBar.Init();
        header.Init();
        scroll.Init();
        stageBar.Init(stageDB);
        stageDescription.Init(stageDB);

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
                    TransitionToDifficulty();
                    break;
                }
                else
                {
                    if (up) stageBar.Up();
                    else if (down) stageBar.Down();
                    scroll.UpdateArea(stageBar.currentStage, SDB.stages.Count);
                    break;
                }
            case State.Difficulty:
                if (button)
                {
                    starter.GoGame(stageBar.currentStage);
                    state = State.InGame;
                    variableCG.alpha = 0;
                    staticCG.alpha = 0;
                }
                else
                {
                    if (up) difficultyBar.Up();
                    else if (down) difficultyBar.Down();
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
                difficultyBar.SetAlpha(progress);
                await Task.Yield();
            }

            stageDescription.Transition(1);
            header.TransitionNotes(1);
            stageBar.SetAlpha(0);
            scroll.SetAlpha(0);
            difficultyBar.SetAlpha(1);

            isTransitioning = false;
        }
    }

}
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class StageSelectManager : MonoBehaviour
{
    private const float musicSelectTime = 90f;
    private const float difficultySelectTime = 30f;
    private const float maxDim = 0.55f;
    // How far the side lists travel off-screen during transitions (camera-pan feel).
    private const float slideDistance = 560f;

    private CanvasGroup variableCG;
    private CanvasGroup staticCG;
    private DefficultyBar defficultyBar;
    private Header header;
    private Scroll scroll;
    private StageBar stageBar;
    private StageDescription stageDescription;
    private Image timeDimImage;

    private RectTransform stageBarRect;
    private RectTransform scrollRect;
    private RectTransform defficultyRect;
    private Vector2 stageBarBasePos;
    private Vector2 scrollBasePos;
    private Vector2 defficultyBasePos;

    private enum State
    {
        Music,
        Difficulty,
        InGame,
    }

    private State state = State.Music;

    private bool isTransitioning = false;

    private float remainingTime = musicSelectTime;
    private float phaseTotalTime = musicSelectTime;

    public void Init()
    {
        variableCG = GetComponent<CanvasGroup>();
        staticCG = transform.parent.parent.Find("StaticCanvas").Find("StageBoxParent").GetComponent<CanvasGroup>();
        defficultyBar = GetComponentInChildren<DefficultyBar>();
        header = GetComponentInChildren<Header>();
        scroll = GetComponentInChildren<Scroll>();
        stageBar = GetComponentInChildren<StageBar>();
        stageDescription = GetComponentInChildren<StageDescription>();

        Transform dim = staticCG.transform.Find("TimeDim");
        if (dim != null) timeDimImage = dim.GetComponent<Image>();

        stageBarRect = stageBar.GetComponent<RectTransform>();
        scrollRect = scroll.GetComponent<RectTransform>();
        defficultyRect = defficultyBar.GetComponent<RectTransform>();
        stageBarBasePos = stageBarRect.anchoredPosition;
        scrollBasePos = scrollRect.anchoredPosition;
        defficultyBasePos = defficultyRect.anchoredPosition;
        ApplySlide(0);

        defficultyBar.Init();
        header.Init();
        scroll.Init();
        stageBar.Init();
        stageDescription.Init();
        stageDescription.Set(stageBar.currentStage);

        state = State.Music;
        remainingTime = musicSelectTime;
        phaseTotalTime = musicSelectTime;
        header.UpdateTimer(remainingTime);
    }

    public void UpdateSelect(bool up, bool down, float dt, bool button, bool back)
    {
        scroll.UpdateScroll(dt);
        if (state == State.InGame) return;

        stageBar.Tick(dt);
        defficultyBar.Tick(dt);

        // The countdown only runs while the player is actually on the select screens.
        bool timeUp = false;
        if (!isTransitioning && GManager.Control.state == GManager.GameState.ChoosingStage)
        {
            remainingTime -= dt;
            if (remainingTime <= 0f)
            {
                remainingTime = 0f;
                timeUp = true;
            }
        }
        header.UpdateTimer(remainingTime);
        UpdateTimeDim();

        if (isTransitioning) return;
        if (timeUp) button = true;

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
                    bool moved = false;
                    if (up) { stageBar.Up(); moved = true; }
                    else if (down) { stageBar.Down(); moved = true; }
                    if (moved) stageDescription.Set(stageBar.currentStage);
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
                    GManager.Control.selectedDifficulty = defficultyBar.index;
                    GManager.Control.GoGame(stageBar.currentStage);
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

    // Resets the countdown for the current phase (e.g. when leaving the title screen).
    public void ResetTimer()
    {
        remainingTime = state == State.Difficulty ? difficultySelectTime : musicSelectTime;
        phaseTotalTime = remainingTime;
    }

    // Slides the side lists horizontally like a camera pan: the song list exits
    // left while the difficulty list enters from the right (progress 0=music, 1=difficulty).
    private void ApplySlide(float progress)
    {
        stageBarRect.anchoredPosition = stageBarBasePos + new Vector2(-slideDistance * progress, 0);
        scrollRect.anchoredPosition = scrollBasePos + new Vector2(-slideDistance * progress, 0);
        defficultyRect.anchoredPosition = defficultyBasePos + new Vector2(slideDistance * (1f - progress), 0);
    }

    // The backdrop darkens as the remaining time runs out (per UI mock).
    private void UpdateTimeDim()
    {
        if (timeDimImage == null) return;
        float dim = phaseTotalTime > 0f ? (1f - remainingTime / phaseTotalTime) * maxDim : 0f;
        Color c = timeDimImage.color;
        c.a = dim;
        timeDimImage.color = c;
    }

    public async void TransitionToDifficulty()
    {
        if (!isTransitioning)
        {
            isTransitioning = true;
            remainingTime = difficultySelectTime;
            phaseTotalTime = difficultySelectTime;
            defficultyBar.ResetSelection(1);
            float d = 0.5f;

            while (d > 0)
            {
                d -= Time.deltaTime;
                float p = 1 - (d / 0.5f);
                float progress = -p * (p - 2);
                stageDescription.Transition(progress);
                ApplySlide(progress);
                stageBar.SetAlpha(1 - progress);
                scroll.SetAlpha(1 - progress);
                defficultyBar.SetAlpha(progress);
                await Task.Yield();
            }

            stageDescription.Transition(1);
            header.TransitionNotes(1);
            ApplySlide(1);
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
            remainingTime = musicSelectTime;
            phaseTotalTime = musicSelectTime;
            float d = 0.5f;

            while (d > 0)
            {
                d -= Time.deltaTime;
                float p = 1 - (d / 0.5f);
                float progress = -p * (p - 2);
                stageDescription.Transition(1 - progress);
                ApplySlide(1 - progress);
                stageBar.SetAlpha(progress);
                scroll.SetAlpha(progress);
                defficultyBar.SetAlpha(1 - progress);
                await Task.Yield();
            }

            stageDescription.Transition(0);
            header.TransitionNotes(0);
            ApplySlide(0);
            stageBar.SetAlpha(1);
            scroll.SetAlpha(1);
            defficultyBar.SetAlpha(0);
            state = State.Music;

            isTransitioning = false;
        }
    }

}

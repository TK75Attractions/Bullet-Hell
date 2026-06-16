using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class StageSelectManager : MonoBehaviour
{
    private const float musicSelectTime = 90f;
    private const float difficultySelectTime = 30f;
    // How far the side lists travel off-screen during transitions (camera-pan feel).
    private const float slideDistance = 560f;
    private const float screenTransitionDuration = 0.3f;

    private CanvasGroup variableCG;
    private CanvasGroup staticCG;
    private DefficultyBar defficultyBar;
    private Header header;
    private Scroll scroll;
    private StageBar stageBar;
    private StageDescription stageDescription;
    private Image timeDimImage;
    private PixelTransition pixelTransition;
    private CanvasGroup playHUD;
    private TMPro.TMP_Text playHUDSongName;
    private TutorialManager tutorialManager;
    private TMPro.TMP_Text guideText;
    private RectTransform guideRect;
    private Vector2 guideBasePos;
    private float guideAnimTime;

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

        Transform pixel = transform.parent.Find("PixelTransition");
        if (pixel != null) pixelTransition = pixel.GetComponent<PixelTransition>();

        Transform tutorial = transform.parent.Find("TutorialUI");
        if (tutorial != null) tutorialManager = tutorial.GetComponent<TutorialManager>();

        Transform guide = transform.Find("GuideText");
        if (guide != null)
        {
            guideText = guide.GetComponent<TMPro.TMP_Text>();
            guideRect = guide as RectTransform;
            guideBasePos = guideRect.anchoredPosition;
        }

        Transform hud = transform.parent.Find("PlayHUD");
        if (hud != null)
        {
            playHUD = hud.GetComponent<CanvasGroup>();
            Transform song = hud.Find("SongName");
            if (song != null) playHUDSongName = song.GetComponent<TMPro.TMP_Text>();
            if (playHUD != null) playHUD.alpha = 0;
        }

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
        stageDescription.Tick(dt);

        if (guideText != null && state == State.Music && !isTransitioning)
        {
            guideAnimTime += dt;
            guideText.alpha = 0.6f + 0.3f * Mathf.Sin(guideAnimTime * 2.5f);
        }

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
                    state = State.InGame;
                    StartGameTransition(stageBar.currentStage);
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

    // Pixel-mosaic transition into the stage: cover the screen from the center
    // outward, load the stage while hidden, then reveal it center-first.
    private async void StartGameTransition(int stageIndex)
    {
        if (pixelTransition != null)
        {
            await pixelTransition.Cover();
            variableCG.alpha = 0;
            staticCG.alpha = 0;
            ShowPlayHUD(stageIndex);
            // The player can already move while the tutorial runs on the bare field.
            GManager.Control.state = GManager.GameState.Tutorial;
            await pixelTransition.Reveal();
            if (tutorialManager != null)
            {
                await tutorialManager.RunTutorial(GManager.Control.IManager);
                await tutorialManager.ShowStartText();
            }
            await GManager.Control.GoGameAsync(stageIndex);
            if (tutorialManager != null)
            {
                StageData data = GManager.Control.SDB.GetStage(stageIndex);
                string diffName = defficultyBar.index == 0 ? "EASY" : defficultyBar.index == 2 ? "LUNATIC" : "NORMAL";
                tutorialManager.ShowSongIntro(data != null ? data.stageName : "", diffName, defficultyBar.index);
            }
        }
        else
        {
            variableCG.alpha = 0;
            staticCG.alpha = 0;
            ShowPlayHUD(stageIndex);
            GManager.Control.GoGame(stageIndex);
        }
    }

    private void ShowPlayHUD(int stageIndex)
    {
        if (playHUD == null) return;
        if (playHUDSongName != null)
        {
            StageData data = GManager.Control.SDB.GetStage(stageIndex);
            playHUDSongName.text = data != null && !string.IsNullOrWhiteSpace(data.stageName) ? data.stageName : "";
        }
        AnimateHUDIn();
    }

    // HUD slides down from above the screen edge while fading in.
    private async void AnimateHUDIn()
    {
        RectTransform rect = (RectTransform)playHUD.transform;
        Vector2 basePos = Vector2.zero;
        float t = 0f;
        const float duration = 0.35f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            float ease = 1f - Mathf.Pow(1f - p, 3f);
            playHUD.alpha = ease;
            rect.anchoredPosition = basePos + new Vector2(0, 70f * (1f - ease));
            await Task.Yield();
        }
        playHUD.alpha = 1f;
        rect.anchoredPosition = basePos;
    }

    // Entrance from the title screen: while the title zooms past the camera
    // (ease-in), the select screen settles in behind it with an ease-out
    // scale/fade, like the camera decelerating onto it.
    public async void PlayEntrance()
    {
        RectTransform variableRect = (RectTransform)variableCG.transform;
        RectTransform staticRect = (RectTransform)staticCG.transform;
        variableCG.alpha = 0;
        staticCG.alpha = 0;

        float delay = 0.16f;
        while (delay > 0f)
        {
            delay -= Time.deltaTime;
            await Task.Yield();
        }

        const float duration = 0.32f;
        float d = duration;
        while (d > 0f)
        {
            if (state == State.InGame) return;
            d -= Time.deltaTime;
            float t = 1f - Mathf.Clamp01(d / duration);
            float ease = 1f - (1f - t) * (1f - t) * (1f - t);
            variableCG.alpha = ease;
            staticCG.alpha = ease;
            // The camera keeps pushing forward: the select screen rushes up from
            // "far away" (small) and settles at full size.
            float scale = Mathf.Lerp(0.88f, 1f, ease);
            variableRect.localScale = Vector3.one * scale;
            staticRect.localScale = Vector3.one * scale;
            await Task.Yield();
        }

        variableCG.alpha = 1;
        staticCG.alpha = 1;
        variableRect.localScale = Vector3.one;
        staticRect.localScale = Vector3.one;
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
        if (guideRect != null)
        {
            guideRect.anchoredPosition = guideBasePos + new Vector2(-slideDistance * progress, 0);
            guideText.alpha = 1f - progress;
        }
    }

    // Width of the timer cell in the header that the wipe fills (right-pivot).
    private const float timerCellWidth = 460f;

    // A tinted panel confined to the remaining-time cell in the header grows
    // leftward as time runs out (per UI mock). Color/placement are set in the
    // scene; here we only drive its width (right-pivot rect).
    private void UpdateTimeDim()
    {
        if (timeDimImage == null) return;
        float p = phaseTotalTime > 0f ? 1f - remainingTime / phaseTotalTime : 0f;
        RectTransform rt = timeDimImage.rectTransform;
        rt.sizeDelta = new Vector2(timerCellWidth * p, rt.sizeDelta.y);
    }

    public async void TransitionToDifficulty()
    {
        if (!isTransitioning)
        {
            isTransitioning = true;
            remainingTime = difficultySelectTime;
            phaseTotalTime = difficultySelectTime;
            defficultyBar.ResetSelection(1);
            float d = screenTransitionDuration;

            while (d > 0)
            {
                d -= Time.deltaTime;
                float p = 1 - (d / screenTransitionDuration);
                float progress = -p * (p - 2);
                stageDescription.Transition(progress);
                ApplySlide(progress);
                stageBar.SetAlpha(1 - progress);
                scroll.SetAlpha(1 - progress);
                defficultyBar.SetAlpha(progress);
                defficultyBar.SetEntranceProgress(progress);
                await Task.Yield();
            }

            stageDescription.Transition(1);
            header.TransitionNotes(1);
            ApplySlide(1);
            stageBar.SetAlpha(0);
            scroll.SetAlpha(0);
            defficultyBar.SetAlpha(1);
            defficultyBar.SetEntranceProgress(1);

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
            float d = screenTransitionDuration;

            while (d > 0)
            {
                d -= Time.deltaTime;
                float p = 1 - (d / screenTransitionDuration);
                float progress = -p * (p - 2);
                stageDescription.Transition(1 - progress);
                ApplySlide(1 - progress);
                stageBar.SetAlpha(progress);
                scroll.SetAlpha(progress);
                defficultyBar.SetAlpha(1 - progress);
                defficultyBar.SetEntranceProgress(1 - progress);
                await Task.Yield();
            }

            stageDescription.Transition(0);
            header.TransitionNotes(0);
            ApplySlide(0);
            stageBar.SetAlpha(1);
            scroll.SetAlpha(1);
            defficultyBar.SetAlpha(0);
            defficultyBar.SetEntranceProgress(0);
            state = State.Music;

            isTransitioning = false;
        }
    }

}

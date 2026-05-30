using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class StageSelectManager : MonoBehaviour
{
    [SerializeField] private float musicSelectDuration = 90f;
    [SerializeField] private float difficultySelectDuration = 30f;
    [SerializeField] private float minimumBackgroundDim = 0.08f;
    [SerializeField] private float maximumBackgroundDim = 0.68f;

    private CanvasGroup variableCG;
    private CanvasGroup staticCG;
    private DefficultyBar defficultyBar;
    private Header header;
    private Scroll scroll;
    private StageBar stageBar;
    private StageDescription stageDescription;
    private Image dimOverlay;
    private float timerRemaining;
    private float timerDuration;
    private int lastStageIndex = -1;

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
        EnsureDimOverlay();

        state = State.Music;
        ResetTimer(musicSelectDuration);
        RefreshStagePreview(force: true);
        stageDescription.Transition(0f);
        stageBar.SetAlpha(1f);
        scroll.SetAlpha(1f);
        defficultyBar.SetAlpha(0f);
        header.TransitionNotes(0);

    }

    public void UpdateSelect(bool up, bool down, float dt, bool button)
    {
        scroll.UpdateScroll(dt);
        UpdateTimer(dt);

        if (isTransitioning) return;

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
                    RefreshStagePreview();
                    scroll.UpdateArea(stageBar.currentStage, GManager.Control.SDB.GetStageCount());
                    break;
                }
            case State.Difficulty:
                if (button)
                {
                    GManager.Control.GoGame(stageBar.currentStage);
                    state = State.InGame;
                    variableCG.alpha = 0;
                    staticCG.alpha = 0;
                    header.TransitionNotes(2);
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

    private void RefreshStagePreview(bool force = false)
    {
        if (!force && lastStageIndex == stageBar.currentStage) return;
        lastStageIndex = stageBar.currentStage;
        stageDescription.Set(lastStageIndex);
    }

    public async void TransitionToDifficulty()
    {
        if (!isTransitioning)
        {
            isTransitioning = true;
            ResetTimer(difficultySelectDuration);
            RefreshStagePreview(force: true);
            header.TransitionNotes(1);
            float duration = 0.58f;
            float d = duration;

            while (d > 0)
            {
                d -= Time.unscaledDeltaTime;
                float p = 1f - Mathf.Clamp01(d / duration);
                float progress = EaseOutCubic(p);
                stageDescription.Transition(progress);
                stageDescription.SetAlpha(0.9f + 0.1f * progress);
                stageBar.SetAlpha(1f - progress);
                scroll.SetAlpha(1f - progress);
                defficultyBar.SetAlpha(progress);
                await Task.Yield();
            }

            stageDescription.Transition(1f);
            stageDescription.SetAlpha(1f);
            stageBar.SetAlpha(0f);
            scroll.SetAlpha(0f);
            defficultyBar.SetAlpha(1f);

            isTransitioning = false;
        }
    }

    private void ResetTimer(float duration)
    {
        timerDuration = Mathf.Max(1f, duration);
        timerRemaining = timerDuration;
        UpdateTimer(0f);
    }

    private void UpdateTimer(float dt)
    {
        if (state == State.InGame) return;
        timerRemaining = Mathf.Max(0f, timerRemaining - dt);
        float normalizedRemaining = Mathf.Clamp01(timerRemaining / timerDuration);
        header.UpdateTimer(timerRemaining, normalizedRemaining);
        UpdateBackgroundDim(1f - normalizedRemaining);
    }

    private void UpdateBackgroundDim(float progress)
    {
        if (dimOverlay == null) return;
        float alpha = Mathf.Lerp(minimumBackgroundDim, maximumBackgroundDim, Mathf.SmoothStep(0f, 1f, progress));
        dimOverlay.color = new Color(0f, 0f, 0f, alpha);
    }

    private void EnsureDimOverlay()
    {
        Transform existing = transform.Find("TimerDimOverlay");
        GameObject overlayObject = existing == null ? new GameObject("TimerDimOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)) : existing.gameObject;
        overlayObject.transform.SetParent(transform, false);
        overlayObject.transform.SetAsFirstSibling();
        RectTransform rect = overlayObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        dimOverlay = overlayObject.GetComponent<Image>();
        dimOverlay.raycastTarget = false;
    }

    private static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        float oneMinus = 1f - t;
        return 1f - oneMinus * oneMinus * oneMinus;
    }
}

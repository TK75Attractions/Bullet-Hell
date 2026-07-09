using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class StageSelectManager : MonoBehaviour
{
    private const float musicSelectTime = 90f;
    private const float difficultySelectTime = 30f;
    private const string InitialStageName = "石工";
    private static readonly bool SkipStoneTutorialForDebug = true;
    private const string DebugSkipTutorialStageName = "石工";
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
    private RectTransform timeDimRect;
    private PixelTransition pixelTransition;
    private CanvasGroup playHUD;
    private TMPro.TMP_Text playHUDSongName;
    private TutorialManager tutorialManager;
    private TMPro.TMP_Text guideText;
    private RectTransform guideRect;
    private Vector2 guideBasePos;
    private float guideAnimTime;
    private readonly List<Renderer> tutorialEnemyRenderers = new List<Renderer>();
    private readonly List<bool> tutorialEnemyRendererStates = new List<bool>();
    private float timeDimBaseX;

    // JSAB-style variation overlay (built at runtime; scene stays unchanged).
    private const string StylePrefKey = "stageSelectStyle";
    private JsabStageSelect jsab;
    private int stageSelectStyle;

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

        Transform dim = staticCG.transform.Find("Head/TimeDim");
        if (dim != null)
        {
            timeDimRect = dim as RectTransform;
            timeDimBaseX = timeDimRect.anchoredPosition.x;
        }

        Transform pixel = transform.parent.Find("PixelTransition");
        if (pixel != null) pixelTransition = pixel.GetComponent<PixelTransition>();

        Transform tutorial = transform.parent.Find("TutorialUI");
        if (tutorial != null) tutorialManager = tutorial.GetComponent<TutorialManager>();

        Transform guide = transform.Find("GuideText");
        if (guide != null)
        {
            guideText = guide.GetComponent<TMPro.TMP_Text>();
            guideRect = guide as RectTransform;
            guideBasePos = guideRect.anchoredPosition + Vector2.up * 20f;
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
        stageBarBasePos = stageBarRect.anchoredPosition + Vector2.up * 35f;
        scrollBasePos = scrollRect.anchoredPosition + Vector2.up * 10f;
        defficultyBasePos = defficultyRect.anchoredPosition + Vector2.left * 20f;
        ApplySlide(0);

        defficultyBar.Init();
        header.Init();
        scroll.Init();
        stageBar.Init();
        int initialStage = FindStageIndex(InitialStageName);
        if (initialStage >= 0) stageBar.SetCurrentStage(initialStage);
        stageDescription.Init();
<<<<<<< HEAD
        stageDescription.Set(stageBar.currentStage);
        scroll.UpdateArea(stageBar.currentStage, GManager.Control.SDB.GetStageCount());
=======
        defficultyBar.SetStage(GManager.Control.SDB.GetStage(stageBar.currentStage));
>>>>>>> origin/main

        state = State.Music;
        remainingTime = musicSelectTime;
        phaseTotalTime = musicSelectTime;
        header.UpdateTimer(remainingTime);

        // Build the JSAB-style overlay (runtime only) and mirror the current stage.
        stageSelectStyle = PlayerPrefs.GetInt(StylePrefKey, 0);
        Transform canvasesRoot = transform.parent != null ? transform.parent.parent : null;
        TMPro.TMP_FontAsset uiFont = guideText != null ? guideText.font : null;
        Sprite playerSprite = null;
        if (GManager.Control.PlayerObj != null)
        {
            SpriteRenderer sr = GManager.Control.PlayerObj.GetComponent<SpriteRenderer>();
            if (sr != null) playerSprite = sr.sprite;
        }
        jsab = JsabStageSelect.Create(canvasesRoot, uiFont, playerSprite);
        jsab.SetStage(stageBar.currentStage, GManager.Control.SDB.GetStageCount(), false);
        RefreshStyleVisibility();
    }

    // JSAB style covers the whole music phase with an opaque canvas. When it is
    // active we also hide the default music UI's CanvasGroups (without destroying
    // anything); leaving Music restores them so the difficulty screen shows.
    private void RefreshStyleVisibility()
    {
        // タイトル画面中は JSAB オーバーレイ(不透明キャンバス)を出さない。
        // ここを見ないと style=1 のとき起動直後からタイトルを覆ってしまい、
        // 「タイトル画面が飛ばされる」ように見える。
        bool onTitle = GManager.Control != null && GManager.Control.state == GManager.GameState.Title;
        bool jsabOn = jsab != null && stageSelectStyle == 1 && state == State.Music && !onTitle;
        if (jsab != null) jsab.SetVisible(jsabOn);
        // The JSAB overlay is the only thing that hides the default UI; whenever it
        // is not covering the screen, the default CanvasGroups must be restored so
        // the music/difficulty screens render normally.
        float defaultAlpha = jsabOn ? 0f : 1f;
        variableCG.alpha = defaultAlpha;
        staticCG.alpha = defaultAlpha;
    }

    // GManager が GameState を切り替えた直後に呼ぶ(タイトル⇔ステージ選択で
    // JSAB オーバーレイの表示可否が変わるため)。
    public void NotifyGameStateChanged()
    {
        RefreshStyleVisibility();
    }

    private int FindStageIndex(string stageName)
    {
        int length = GManager.Control.SDB.GetStageCount();
        for (int i = 0; i < length; i++)
        {
            StageData stage = GManager.Control.SDB.GetStage(i);
            if (stage != null && stage.stageName == stageName) return i;
        }
        return -1;
    }

    public void UpdateSelect(bool up, bool down, bool left, bool right, float dt, bool button, bool back)
    {
        // Stage select is a full-screen view; the left/right keys (A/D and ←/→)
        // move between stages just like up/down (W/S and ↑/↓). The difficulty
        // list is vertical, so there we keep up/down as the primary axis.
        bool prev = up || left;
        bool next = down || right;
        scroll.UpdateScroll(dt);
        if (state == State.InGame) return;

        stageBar.Tick(dt);
        defficultyBar.Tick(dt);
        stageDescription.Tick(dt);
        if (jsab != null) jsab.Tick(dt);

        // Toggle between the default and JSAB stage-select styles with V.
        // Suppressed while the in-screen difficulty modal is open.
        bool jsabDifficultyOpen = jsab != null && jsab.DifficultyOpen;
        if (state == State.Music && !isTransitioning && !jsabDifficultyOpen)
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.vKey.wasPressedThisFrame)
            {
                stageSelectStyle = stageSelectStyle == 1 ? 0 : 1;
                PlayerPrefs.SetInt(StylePrefKey, stageSelectStyle);
                PlayerPrefs.Save();
                RefreshStyleVisibility();
            }
        }

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
                // JSAB style resolves the difficulty inside this same screen via a
                // centered modal, instead of sliding to the default difficulty list.
                if (jsabDifficultyOpen)
                {
                    if (back)
                    {
                        jsab.CloseDifficulty();
                        // Returning to the carousel restarts the music-select
                        // countdown, same as TransitionToMusic on the default screen.
                        remainingTime = musicSelectTime;
                        phaseTotalTime = musicSelectTime;
                    }
                    else if (button || jsab.ConsumeMouseConfirm())
                    {
                        GManager.Control.selectedDifficulty = jsab.DifficultyIndex;
                        // JSAB 画面はここでは隠さない。ピクセルトランジションが
                        // 覆い切ってから StartGameTransition 側で隠す(先に隠すと
                        // カバー完了前に下の画面が露出して一瞬乱れる)。
                        state = State.InGame;
                        StartGameTransition(stageBar.currentStage);
                    }
                    else if (left || up) jsab.MoveDifficulty(-1);
                    else if (right || down) jsab.MoveDifficulty(1);
                    break;
                }

                if (button)
                {
<<<<<<< HEAD
                    // On the JSAB screen the decision opens the in-screen difficulty
                    // modal; the default screen slides to the difficulty list. The
                    // modal gets the same fresh countdown as the difficulty screen,
                    // otherwise a music-phase timeout would auto-confirm it instantly.
                    if (jsab != null && stageSelectStyle == 1)
                    {
                        remainingTime = difficultySelectTime;
                        phaseTotalTime = difficultySelectTime;
                        jsab.OpenDifficulty();
                        break;
                    }
=======
                    defficultyBar.SetStage(GManager.Control.SDB.GetStage(stageBar.currentStage));
>>>>>>> origin/main
                    state = State.Difficulty;
                    RefreshStyleVisibility();
                    TransitionToDifficulty();
                    break;
                }
                else
                {
                    bool moved = false;
                    if (prev) { stageBar.Up(); moved = true; }
                    else if (next) { stageBar.Down(); moved = true; }
                    if (moved)
                    {
                        stageDescription.Set(stageBar.currentStage);
                        if (jsab != null) jsab.SetStage(stageBar.currentStage, GManager.Control.SDB.GetStageCount(), true);
                    }
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
<<<<<<< HEAD
                    GManager.Control.selectedDifficulty = defficultyBar.index;
=======
                    GManager.Control.GoGame(stageBar.currentStage, defficultyBar.SelectedDifficulty);
>>>>>>> origin/main
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

    // プレイ開始遷移: 難易度ボタン群がスライドアウトしてからホワイトアウトで
    // 画面を白く飛ばし、覆われている間にプレイ画面へ切り替え、白カバーが
    // 中央からピクセル(モザイク)状に欠けながらプレイ画面が解像していく。
    private async void StartGameTransition(int stageIndex)
    {
        if (pixelTransition != null)
        {
            // JSAB の難易度モーダルが開いていれば、行が右へ飛び去ってから白へ。
            if (jsab != null && jsab.DifficultyOpen)
            {
                await jsab.PlayDifficultyExit();
            }
            await pixelTransition.WhiteoutCover();
            // 画面が完全に覆われてから JSAB オーバーレイと難易度モーダルを隠す。
            if (jsab != null)
            {
                jsab.CloseDifficulty();
                jsab.SetVisible(false);
            }
            variableCG.alpha = 0;
            staticCG.alpha = 0;
            if (playHUD != null) playHUD.alpha = 0f;
            GManager.Control.PController?.ResetToCenter();
            SetTutorialEnemiesVisible(false);
            // The player can already move while the tutorial runs on the bare field.
            GManager.Control.state = GManager.GameState.Tutorial;
            await pixelTransition.MosaicReveal();
            bool skipPreStage = ShouldSkipPreStageTutorial(stageIndex);
            if (!skipPreStage && tutorialManager != null)
            {
                await tutorialManager.RunTutorial(GManager.Control.IManager);
                await tutorialManager.ShowStartText();
            }

            StageData data = GManager.Control.SDB.GetStage(stageIndex);
            // selectedDifficulty is the single source of truth; the JSAB modal sets
            // it without touching defficultyBar.index.
            int diff = GManager.Control.selectedDifficulty;
            string diffName = diff == 0 ? "EASY" : diff == 2 ? "LUNATIC" : "NORMAL";
            if (!skipPreStage && tutorialManager != null)
            {
                await tutorialManager.ShowSongIntro(data != null ? data.stageName : "", diffName, defficultyBar.index);
            }

            await ShowPlayHUD(stageIndex);
            // Keep the player's tutorial-end position when the actual stage begins.
            await GManager.Control.GoGameAsync(stageIndex);
            SetTutorialEnemiesVisible(true);
        }
        else
        {
            if (jsab != null)
            {
                jsab.CloseDifficulty();
                jsab.SetVisible(false);
            }
            variableCG.alpha = 0;
            staticCG.alpha = 0;
            GManager.Control.PController?.ResetToCenter();
            await ShowPlayHUD(stageIndex);
            await GManager.Control.GoGameAsync(stageIndex);
        }
    }

    private bool ShouldSkipPreStageTutorial(int stageIndex)
    {
        if (!SkipStoneTutorialForDebug) return false;

        StageData data = GManager.Control.SDB.GetStage(stageIndex);
        return data != null && data.stageName == DebugSkipTutorialStageName;
    }

    private async Task ShowPlayHUD(int stageIndex)
    {
        if (playHUD == null) return;
        if (playHUDSongName != null)
        {
            StageData data = GManager.Control.SDB.GetStage(stageIndex);
            playHUDSongName.text = data != null && !string.IsNullOrWhiteSpace(data.stageName) ? data.stageName : "";
        }
        await AnimateHUDIn();
    }

    // HUD slides down from above the screen edge while fading in.
    private async Task AnimateHUDIn()
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

    private void SetTutorialEnemiesVisible(bool visible)
    {
        if (!visible)
        {
            tutorialEnemyRenderers.Clear();
            tutorialEnemyRendererStates.Clear();
            Boss[] bosses = Object.FindObjectsByType<Boss>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Boss boss in bosses)
            {
                Renderer[] renderers = boss.GetComponentsInChildren<Renderer>(true);
                if (boss.gameObject.name == "boss")
                {
                    foreach (Renderer renderer in renderers)
                    {
                        renderer.enabled = false;
                    }
                    continue;
                }

                foreach (Renderer renderer in renderers)
                {
                    if (tutorialEnemyRenderers.Contains(renderer)) continue;
                    tutorialEnemyRenderers.Add(renderer);
                    tutorialEnemyRendererStates.Add(renderer.enabled);
                    renderer.enabled = false;
                }
            }
            return;
        }

        for (int i = 0; i < tutorialEnemyRenderers.Count; i++)
        {
            if (tutorialEnemyRenderers[i] != null)
            {
                tutorialEnemyRenderers[i].enabled = tutorialEnemyRendererStates[i];
            }
        }
        tutorialEnemyRenderers.Clear();
        tutorialEnemyRendererStates.Clear();
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
        // JSAB スタイルは不透明カルーセルを短いフェードで重ねる(即時 alpha=1
        // だとタイトル退場演出の途中にハードカットで割り込んでしまう)。
        if (jsab != null && stageSelectStyle == 1 && state == State.Music)
        {
            RefreshStyleVisibility();
            jsab.SetEntranceAlpha(0f);
            const float fadeDur = 0.25f;
            float ft = 0f;
            while (ft < fadeDur)
            {
                if (state == State.InGame) return;
                ft += Time.deltaTime;
                jsab.SetEntranceAlpha(Mathf.Clamp01(ft / fadeDur));
                await Task.Yield();
            }
            // V トグル等で途中から状態が変わっていても最終状態はここで正す。
            RefreshStyleVisibility();
            return;
        }

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
        RefreshStyleVisibility();
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

    // The right edge stays fixed just outside the screen. Only the mask width
    // grows, so the red background never drifts away from the right side.
    private const float timerVisibleWidth = 502f;
    private const float timerRightOverscan = 40f;

    // A tinted panel confined to the remaining-time cell in the header grows
    // leftward as time runs out (per UI mock). Color/placement are set in the
    // scene; here we only drive its width (right-pivot rect).
    private void UpdateTimeDim()
    {
        if (timeDimRect == null) return;
        float p = phaseTotalTime > 0f
            ? Mathf.Clamp01(1f - remainingTime / phaseTotalTime)
            : 0f;
        timeDimRect.anchoredPosition = new Vector2(timeDimBaseX, timeDimRect.anchoredPosition.y);
        timeDimRect.sizeDelta = new Vector2(
            timerRightOverscan + timerVisibleWidth * p,
            timeDimRect.sizeDelta.y);
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
            RefreshStyleVisibility();

            isTransitioning = false;
        }
    }

}

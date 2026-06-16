using System.Threading.Tasks;
using UnityEngine;
using TMPro;

// Pre-stage tutorial (move -> dash), the "here we go" callout and the big
// song-title / difficulty intro in the lower right. All text easing is done
// here; flow is driven by StageSelectManager's stage transition.
public class TutorialManager : MonoBehaviour
{
    private static readonly Color easyColor = new Color(0.56f, 0.72f, 0.91f);
    private static readonly Color normalColor = new Color(0.3f, 0.65f, 1f);
    private static readonly Color lunaticColor = new Color(0.95f, 0.45f, 0.6f);

    private TMP_Text tutorialText;
    private RectTransform tutorialRect;
    private TMP_Text startText;
    private RectTransform startRect;
    private TMP_Text introName;
    private RectTransform introNameRect;
    private TMP_Text introDiff;
    private RectTransform introDiffRect;
    private float introNameBaseX;
    private float introDiffBaseX;
    private bool initialized;

    private void EnsureInit()
    {
        if (initialized) return;
        initialized = true;
        Transform t = transform.Find("TutorialText");
        if (t != null) { tutorialText = t.GetComponent<TMP_Text>(); tutorialRect = t as RectTransform; }
        Transform s = transform.Find("StartText");
        if (s != null) { startText = s.GetComponent<TMP_Text>(); startRect = s as RectTransform; }
        Transform n = transform.Find("IntroName");
        if (n != null) { introName = n.GetComponent<TMP_Text>(); introNameRect = n as RectTransform; introNameBaseX = introNameRect.anchoredPosition.x; }
        Transform d = transform.Find("IntroDiff");
        if (d != null) { introDiff = d.GetComponent<TMP_Text>(); introDiffRect = d as RectTransform; introDiffBaseX = introDiffRect.anchoredPosition.x; }
        if (tutorialText != null) tutorialText.alpha = 0;
        if (startText != null) startText.alpha = 0;
        if (introName != null) introName.alpha = 0;
        if (introDiff != null) introDiff.alpha = 0;
    }

    // Step 1: hold a movement key, Step 2: dash. Completes when both are done.
    public async Task RunTutorial(InputManager input)
    {
        EnsureInit();
        if (tutorialText == null) return;

        await PopIn(tutorialText, tutorialRect, "WASDで移動!");
        float held = 0f;
        while (held < 0.8f)
        {
            if (input.upPressed || input.downPressed || input.leftPressed || input.rightPressed)
            {
                held += Time.deltaTime;
            }
            await Task.Yield();
            if (this == null) return;
        }
        await SuccessPop(tutorialText, tutorialRect);

        await PopIn(tutorialText, tutorialRect, "ナイス! 次はスペースでダッシュ!");
        while (!input.buttonPressedThisFrame)
        {
            await Task.Yield();
            if (this == null) return;
        }
        await SuccessPop(tutorialText, tutorialRect);
    }

    // 「はじまるよ!」 pop with a hold, then fade upward.
    public async Task ShowStartText()
    {
        EnsureInit();
        if (startText == null) return;
        await PopIn(startText, startRect, "はじまるよ!");
        float d = 0.8f;
        while (d > 0f) { d -= Time.deltaTime; await Task.Yield(); if (this == null) return; }
        await SuccessPop(startText, startRect);
    }

    // Big song title + difficulty sliding in from the right, holding, then fading.
    public async void ShowSongIntro(string songName, string difficultyName, int difficultyIndex)
    {
        EnsureInit();
        if (introName == null || introDiff == null) return;
        introName.text = songName;
        introDiff.text = difficultyName;
        introDiff.color = difficultyIndex == 0 ? easyColor : difficultyIndex == 2 ? lunaticColor : normalColor;

        const float slide = 260f;
        float t = 0f;
        const float inTime = 0.45f;
        while (t < inTime)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / inTime);
            float ease = 1f - Mathf.Pow(1f - p, 3f);
            introNameRect.anchoredPosition = new Vector2(introNameBaseX + slide * (1f - ease), introNameRect.anchoredPosition.y);
            introDiffRect.anchoredPosition = new Vector2(introDiffBaseX + slide * (1f - ease), introDiffRect.anchoredPosition.y);
            introName.alpha = ease;
            introDiff.alpha = ease;
            await Task.Yield();
            if (this == null) return;
        }

        float hold = 2.2f;
        while (hold > 0f) { hold -= Time.deltaTime; await Task.Yield(); if (this == null) return; }

        t = 0f;
        const float outTime = 0.5f;
        while (t < outTime)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / outTime);
            float ease = p * p;
            introName.alpha = 1f - ease;
            introDiff.alpha = 1f - ease;
            await Task.Yield();
            if (this == null) return;
        }
        introName.alpha = 0;
        introDiff.alpha = 0;
    }

    // Scale 0.7 -> 1 with a small overshoot while fading in.
    private async Task PopIn(TMP_Text text, RectTransform rect, string message)
    {
        text.text = message;
        float t = 0f;
        const float duration = 0.3f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            float back = 1f + 2.2f * Mathf.Pow(p - 1f, 3f) + 1.2f * Mathf.Pow(p - 1f, 2f); // easeOutBack-ish
            rect.localScale = Vector3.one * Mathf.LerpUnclamped(0.7f, 1f, back);
            text.alpha = p;
            await Task.Yield();
            if (this == null) return;
        }
        rect.localScale = Vector3.one;
        text.alpha = 1f;
    }

    // Quick celebratory squash then fade out.
    private async Task SuccessPop(TMP_Text text, RectTransform rect)
    {
        float t = 0f;
        const float duration = 0.3f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            rect.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(Mathf.PI * Mathf.Min(p * 2f, 1f)));
            text.alpha = 1f - p * p;
            await Task.Yield();
            if (this == null) return;
        }
        text.alpha = 0f;
        rect.localScale = Vector3.one;
    }
}

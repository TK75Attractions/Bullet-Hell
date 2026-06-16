using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Pause / option menu operation: cursor movement, volume slider, effects
// toggle (visual only for now) and the quit-confirmation popup. Driven by
// GManager while the game is paused, using unscaled delta time.
public class OptionMenu : MonoBehaviour
{
    private static readonly float[] rowY = { 170f, 10f, -130f, -280f };
    private const float itemBaseX = -410f;
    private const float sliderWidth = 660f;

    private static readonly Color unselectedColor = new Color(0.78f, 0.84f, 0.92f);
    private static readonly Color disabledGray = new Color(0.55f, 0.55f, 0.55f);

    private RectTransform selectBand;
    private RectTransform[] badges = new RectTransform[4];
    private TMP_Text[] items = new TMP_Text[4];
    private RectTransform[] itemRects = new RectTransform[4];
    private RectTransform sliderFill;
    private TMP_Text onText;
    private TMP_Text offText;
    private RectTransform effectBorder;
    private GameObject confirmGroup;
    private TMP_Text yesText;
    private TMP_Text noText;

    private const float selectedShiftX = 22f;

    private int index;
    private bool confirmOpen;
    private int confirmIndex = 1; // 0=はい, 1=いいえ (safe default)
    private bool effectsOn = true;
    private float bandY;
    private float[] itemOffset = new float[4];
    private bool borderSnap;
    private float animTime;
    private bool initialized;
    private CanvasGroup group;
    private float openAnimT = 1f;

    private void EnsureInit()
    {
        if (initialized) return;
        initialized = true;
        group = GetComponent<CanvasGroup>();
        selectBand = transform.Find("SelectBand") as RectTransform;
        for (int i = 0; i < 4; i++)
        {
            badges[i] = transform.Find("Badge" + i) as RectTransform;
            Transform item = transform.Find("Item" + i);
            items[i] = item.GetComponent<TMP_Text>();
            itemRects[i] = item as RectTransform;
        }
        sliderFill = transform.Find("SliderBack/SliderFill") as RectTransform;
        onText = transform.Find("OnText").GetComponent<TMP_Text>();
        offText = transform.Find("OffText").GetComponent<TMP_Text>();
        Transform border = transform.Find("EffectBorder");
        if (border != null) effectBorder = border as RectTransform;
        Transform confirm = transform.Find("ConfirmGroup");
        confirmGroup = confirm.gameObject;
        Transform yes = confirm.Find("YesText");
        if (yes != null) yesText = yes.GetComponent<TMP_Text>();
        Transform no = confirm.Find("NoText");
        if (no != null) noText = no.GetComponent<TMP_Text>();
    }

    // Called every time the pause screen opens.
    public void Open()
    {
        EnsureInit();
        index = 0;
        confirmOpen = false;
        confirmIndex = 1;
        for (int i = 0; i < itemOffset.Length; i++) itemOffset[i] = 0f;
        borderSnap = true;
        confirmGroup.SetActive(false);
        bandY = rowY[0];
        selectBand.anchoredPosition = new Vector2(0, bandY);
        RefreshSlider();
        RefreshEffects();

        openAnimT = 0f;
        if (group != null) group.alpha = 0f;
        transform.localScale = Vector3.one * 0.96f;
    }

    // Returns true if the back press was consumed (closing the confirm popup).
    public bool HandleBack()
    {
        if (confirmOpen)
        {
            CloseConfirm();
            return true;
        }
        return false;
    }

    // leftPress/rightPress: edge (this frame). leftHeld/rightHeld: continuous (for volume).
    public void UpdateMenu(float dt, bool up, bool down, bool leftPress, bool rightPress, bool leftHeld, bool rightHeld, bool button)
    {
        EnsureInit();
        animTime += dt;

        // Quick fade/scale-in when the pause screen opens.
        if (openAnimT < 1f && group != null)
        {
            openAnimT = Mathf.Min(1f, openAnimT + dt / 0.18f);
            float e = 1f - (1f - openAnimT) * (1f - openAnimT);
            group.alpha = e;
            transform.localScale = Vector3.one * (0.96f + 0.04f * e);
        }

        if (confirmOpen)
        {
            if (leftPress || rightPress)
            {
                confirmIndex = confirmIndex == 0 ? 1 : 0;
                RefreshConfirm();
            }
            if (button)
            {
                if (confirmIndex == 0) GManager.Control.QuitPlay();
                else CloseConfirm();
            }
        }
        else
        {
            int prevIndex = index;
            if (up && index > 0) index--;
            else if (down && index < rowY.Length - 1) index++;
            if (index != prevIndex && index == 2) borderSnap = true;

            // Volume: hold left/right to slide continuously (full range ~1.6s).
            if (index == 1 && (leftHeld || rightHeld))
            {
                float dir = (rightHeld ? 1f : 0f) - (leftHeld ? 1f : 0f);
                AudioListener.volume = Mathf.Clamp01(AudioListener.volume + dir * 0.6f * dt);
                RefreshSlider();
            }
            else if (index == 2 && (leftPress || rightPress || button))
            {
                effectsOn = !effectsOn;
                RefreshEffects();
            }

            if (button)
            {
                if (index == 0) GManager.Control.SetPaused(false);
                else if (index == 3) OpenConfirm();
            }
        }

        // Selection animations: the highlight band glides to the selected row,
        // the selected badge "puyo-puyo" pulses and the selected label eases a
        // little to the right and stays there.
        bandY = Mathf.Lerp(bandY, rowY[index], 1f - Mathf.Exp(-14f * dt));
        selectBand.anchoredPosition = new Vector2(0, bandY);
        for (int i = 0; i < 4; i++)
        {
            bool selected = i == index && !confirmOpen;
            float puyo = selected ? 1f + 0.14f * Mathf.Abs(Mathf.Sin(animTime * 4.5f)) : 1f;
            badges[i].sizeDelta = Vector2.one * (selected ? 26f : 16f);
            badges[i].localScale = Vector3.one * puyo;
            itemOffset[i] = Mathf.Lerp(itemOffset[i], selected ? selectedShiftX : 0f, 1f - Mathf.Exp(-12f * dt));
            itemRects[i].anchoredPosition = new Vector2(itemBaseX + itemOffset[i], rowY[i]);
            items[i].color = selected ? Color.white : unselectedColor;
        }

        UpdateEffectBorder(dt);
    }

    // The frame only appears while the effect row is selected, and slides
    // smoothly between ON and OFF when the value changes.
    private void UpdateEffectBorder(float dt)
    {
        if (effectBorder == null) return;
        bool show = index == 2 && !confirmOpen;
        if (effectBorder.gameObject.activeSelf != show) effectBorder.gameObject.SetActive(show);
        if (!show) return;

        RectTransform target = (effectsOn ? onText : offText).rectTransform;
        Vector2 tp = target.anchoredPosition;
        Vector2 ts = new Vector2(target.sizeDelta.x + 36f, 72f);
        if (borderSnap)
        {
            effectBorder.anchoredPosition = tp;
            effectBorder.sizeDelta = ts;
            borderSnap = false;
        }
        else
        {
            float k = 1f - Mathf.Exp(-18f * dt);
            effectBorder.anchoredPosition = Vector2.Lerp(effectBorder.anchoredPosition, tp, k);
            effectBorder.sizeDelta = Vector2.Lerp(effectBorder.sizeDelta, ts, k);
        }
    }

    private void OpenConfirm()
    {
        confirmOpen = true;
        confirmIndex = 1;
        confirmGroup.SetActive(true);
        RefreshConfirm();
    }

    private void CloseConfirm()
    {
        confirmOpen = false;
        confirmGroup.SetActive(false);
    }

    private void RefreshConfirm()
    {
        if (yesText != null) yesText.color = confirmIndex == 0 ? Color.white : disabledGray;
        if (noText != null) noText.color = confirmIndex == 1 ? Color.white : disabledGray;
    }

    private void RefreshSlider()
    {
        if (sliderFill != null) sliderFill.sizeDelta = new Vector2(sliderWidth * AudioListener.volume, sliderFill.sizeDelta.y);
    }

    private void RefreshEffects()
    {
        onText.color = effectsOn ? Color.white : disabledGray;
        offText.color = effectsOn ? disabledGray : Color.white;
    }
}

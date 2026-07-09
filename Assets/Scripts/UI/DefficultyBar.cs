using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DefficultyBar : MonoBehaviour
{
    private CanvasGroup CG;
    private RectTransform whiteBar;
    private CanvasGroup whiteCG;

    private TMP_Text descText;
    private RectTransform descRect;
    private TMP_Text promptText;
    private TMP_Text promptRubyO;
    private TMP_Text promptRubyK;
    private float descBaseX;
    private float descAnimT = 1f;
    private float animTime;

    private static readonly string[] descriptions =
    {
        "イージー / EASY - 気軽に遊べる難易度です",
        "ノーマル / NORMAL - 標準的な難易度です",
        "ルナティック / LUNATIC - 上級者向けの高難易度です",
    };

    private DefficultyBox[] boxes = new DefficultyBox[3];
    // Per-box selection progress (0=unselected, 1=selected), smoothed every frame
    // toward its target so rapid input still animates fluidly.
    private float[] selectProgress = new float[3];
    public int index = 1;
    private float whiteY;

    private class DefficultyBox
    {
        public CanvasGroup CG;
        public RectTransform rectTransform;
        public float baseX;
        private TMP_Text nameText;
        private Color baseTextColor;

        public DefficultyBox(Transform trans, string name, Color barColor, Color textColor)
        {
            CG = trans.GetComponent<CanvasGroup>();
            rectTransform = trans.GetComponent<RectTransform>();
            trans.Find("StageBar").GetComponent<Image>().color = barColor;
            nameText = trans.Find("StageName").GetComponent<TMP_Text>();
            nameText.text = name;
            baseTextColor = textColor;
            baseX = rectTransform.anchoredPosition.x;
        }

        public void SetPosition(float progress)
        {
            CG.alpha = 0.4f + 0.6f * progress;
            rectTransform.localScale = Vector3.one * (0.8f + 0.2f * progress);
            nameText.color = Color.Lerp(baseTextColor, Color.white, progress);
        }
    }

    public void Init()
    {
        Transform trans = transform.Find("List");
        (trans.Find("Easy") as RectTransform).anchoredPosition = new Vector2(0f, 135f);
        (trans.Find("Normal") as RectTransform).anchoredPosition = Vector2.zero;
        (trans.Find("Lunatic") as RectTransform).anchoredPosition = new Vector2(0f, -135f);
        boxes[0] = new DefficultyBox(trans.Find("Easy"), "EASY", new Color(0.086f, 0.227f, 0.373f), new Color(0.56f, 0.72f, 0.91f));
        boxes[1] = new DefficultyBox(trans.Find("Normal"), "NORMAL", new Color(0.055f, 0.525f, 0.91f), new Color(0.85f, 0.93f, 1f));
        boxes[2] = new DefficultyBox(trans.Find("Lunatic"), "LUNATIC", new Color(0.36f, 0.078f, 0.188f), new Color(0.91f, 0.6f, 0.69f));
        CG = GetComponent<CanvasGroup>();
        CG.alpha = 0;

        whiteBar = transform.Find("White").GetComponent<RectTransform>();
        whiteCG = whiteBar.GetComponent<CanvasGroup>();
        whiteCG.alpha = 1;

        Transform desc = transform.Find("DescText");
        if (desc != null)
        {
            descText = desc.GetComponent<TMP_Text>();
            descRect = desc.GetComponent<RectTransform>();
            descBaseX = descRect.anchoredPosition.x;
        }
        Transform prompt = transform.Find("Prompt");
        if (prompt != null) promptText = prompt.GetComponent<TMP_Text>();
        Transform rubyO = transform.Find("PromptRubyO");
        if (rubyO != null) promptRubyO = rubyO.GetComponent<TMP_Text>();
        Transform rubyK = transform.Find("PromptRubyK");
        if (rubyK != null) promptRubyK = rubyK.GetComponent<TMP_Text>();

        SetLayoutPosition("Title", new Vector2(0f, 365f));
        SetLayoutPosition("TitleRubyN", new Vector2(-114f, 425f));
        SetLayoutPosition("TitleRubyS", new Vector2(152f, 425f));
        SetLayoutPosition("LineT", new Vector2(0f, -315f));
        SetLayoutPosition("DescText", new Vector2(0f, -370f));
        SetLayoutPosition("LineB", new Vector2(0f, -425f));
        SetLayoutPosition("PromptRubyO", new Vector2(0f, -468f));
        SetLayoutPosition("PromptRubyK", new Vector2(133f, -468f));
        SetLayoutPosition("Prompt", new Vector2(0f, -505f));
        if (descRect != null) descRect.sizeDelta = new Vector2(960f, 60f);
        if (promptText != null) promptText.rectTransform.sizeDelta = new Vector2(650f, 60f);

        ResetSelection(1);
    }

    // Snap selection to the given index without animating (used on entering the screen).
    public void ResetSelection(int newIndex)
    {
        index = Mathf.Clamp(newIndex, 0, boxes.Length - 1);
        for (int i = 0; i < boxes.Length; i++)
        {
            selectProgress[i] = i == index ? 1f : 0f;
            boxes[i].SetPosition(selectProgress[i]);
        }
        whiteY = TargetWhiteY();
        whiteBar.anchoredPosition = new Vector2(0, whiteY);
        whiteCG.alpha = 1;
        RefreshDescription();
    }

    // Per-frame animation: boxes ease toward their selection state, the white
    // brackets glide to the selected row, the prompt blinks, the description slides in.
    public void Tick(float dt)
    {
        animTime += dt;

        float follow = 1f - Mathf.Exp(-14f * dt);
        for (int i = 0; i < boxes.Length; i++)
        {
            float target = i == index ? 1f : 0f;
            selectProgress[i] = Mathf.Abs(target - selectProgress[i]) < 0.001f
                ? target
                : Mathf.Lerp(selectProgress[i], target, follow);
            boxes[i].SetPosition(selectProgress[i]);
        }

        float targetY = TargetWhiteY();
        whiteY = Mathf.Abs(targetY - whiteY) < 0.5f ? targetY : Mathf.Lerp(whiteY, targetY, 1f - Mathf.Exp(-16f * dt));
        whiteBar.anchoredPosition = new Vector2(0, whiteY);

        if (promptText != null)
        {
            promptText.alpha = 0.45f + 0.4f * Mathf.Sin(animTime * 4f);
            if (promptRubyO != null) promptRubyO.alpha = promptText.alpha;
            if (promptRubyK != null) promptRubyK.alpha = promptText.alpha;
        }
        if (descText != null && descAnimT < 1f)
        {
            descAnimT = Mathf.Min(1f, descAnimT + dt / 0.2f);
            float p = -descAnimT * (descAnimT - 2);
            descText.alpha = p;
            descRect.anchoredPosition = new Vector2(descBaseX + 30f * (1f - p), descRect.anchoredPosition.y);
        }
    }

    // Staggered slide-in during the screen transition: lower rows arrive a beat
    // later. Only the X offset is written here; alpha/scale stay owned by Tick.
    public void SetEntranceProgress(float p)
    {
        for (int i = 0; i < boxes.Length; i++)
        {
            float local = Mathf.Clamp01((p - i * 0.12f) / 0.76f);
            float ease = 1f - (1f - local) * (1f - local) * (1f - local);
            RectTransform rect = boxes[i].rectTransform;
            rect.anchoredPosition = new Vector2(boxes[i].baseX + 140f * (1f - ease), rect.anchoredPosition.y);
        }
    }

    public void Up()
    {
        if (index <= 0) return;
        index--;
        RefreshDescription();
    }

    public void Down()
    {
        if (index >= boxes.Length - 1) return;
        index++;
        RefreshDescription();
    }

    public void SetAlpha(float alpha)
    {
        CG.alpha = alpha;
    }

    // The brackets follow the selected box's actual scene position, so the row
    // spacing can be tuned in the scene without touching code.
    private float TargetWhiteY()
    {
        return boxes[index].rectTransform.anchoredPosition.y;
    }

    private void RefreshDescription()
    {
        if (descText == null) return;
        descText.text = descriptions[index];
        descAnimT = 0f;
        descText.alpha = 0f;
    }

    private void SetLayoutPosition(string childName, Vector2 position)
    {
        RectTransform child = transform.Find(childName) as RectTransform;
        if (child != null) child.anchoredPosition = position;
    }
}

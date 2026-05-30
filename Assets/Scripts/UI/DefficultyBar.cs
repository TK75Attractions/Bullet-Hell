using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DefficultyBar : MonoBehaviour
{
    private CanvasGroup CG;
    private RectTransform rectTransform;
    private RectTransform whiteBar;
    private CanvasGroup whiteCG;
    private TMP_Text titleText;
    private TMP_Text descriptionText;
    private TMP_Text guideText;

    private DefficultyBox[] boxes = new DefficultyBox[3];
    public int index = 1;
    private readonly float duration = 0.18f;
    private bool isTransitioning = false;
    private readonly string[] descriptions =
    {
        "イージー / EASY - まず曲に慣れる難易度です",
        "ノーマル / NORMAL - 標準的な難易度です",
        "ハードコア / HARDCORE - 激しい弾幕に挑む難易度です"
    };

    private class DefficultyBox
    {
        public CanvasGroup CG;
        public RectTransform rectTransform;
        private TMP_Text label;
        private Image barImage;
        private Color baseColor;
        public DefficultyBox(Transform trans, Vector2 pos, string name, Color color)
        {
            CG = trans.GetComponent<CanvasGroup>();
            rectTransform = trans.GetComponent<RectTransform>();
            rectTransform.anchoredPosition = pos;
            barImage = trans.Find("StageBar").GetComponent<Image>();
            label = trans.Find("StageName").GetComponent<TMP_Text>();
            baseColor = color;
            barImage.color = color;
            label.text = name;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
        }

        public void SetPosition(float progress)
        {
            progress = Mathf.Clamp01(progress);
            CG.alpha = 0.42f + 0.58f * progress;
            rectTransform.localScale = Vector3.one * (0.82f + 0.2f * Mathf.SmoothStep(0f, 1f, progress));
            barImage.color = Color.Lerp(new Color(baseColor.r * 0.35f, baseColor.g * 0.35f, baseColor.b * 0.35f, 0.75f), baseColor, progress);
            label.color = Color.Lerp(new Color(1f, 1f, 1f, 0.55f), Color.white, progress);
        }
    }

    public void Init()
    {
        Transform trans = transform.Find("List");
        boxes[0] = new DefficultyBox(trans.Find("Easy"), new Vector2(0, 180), "EASY", new Color(0.18f, 0.85f, 0.34f));
        boxes[1] = new DefficultyBox(trans.Find("Normal"), new Vector2(0, 0), "NORMAL", new Color(0.05f, 0.62f, 0.95f));
        boxes[2] = new DefficultyBox(trans.Find("Lunatic"), new Vector2(0, -180), "HARDCORE", new Color(0.55f, 0.02f, 0.16f));
        rectTransform = GetComponent<RectTransform>();
        rectTransform.anchoredPosition = new Vector2(470f, -70f);
        rectTransform.localScale = Vector3.one;
        CG = GetComponent<CanvasGroup>();
        CG.alpha = 0;
        index = 1;
        CreateLabels(trans.Find("Normal").Find("StageName").GetComponent<TMP_Text>());
        for (int i = 0; i < boxes.Length; i++)
        {
            if (i == index) boxes[i].SetPosition(1);
            else boxes[i].SetPosition(0);
        }

        whiteBar = transform.Find("White").GetComponent<RectTransform>();
        whiteCG = whiteBar.GetComponent<CanvasGroup>();
        whiteCG.alpha = 1;
        whiteBar.anchoredPosition = GetWhitePosition(index);
        UpdateDescription();
    }

    public async void Up()
    {
        if (isTransitioning || index <= 0) return;
        isTransitioning = true;
        index--;
        float d = duration;

        while (d > 0)
        {
            d -= Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(d / duration);
            float progress = -t * (t - 2);
            if (progress > 1) progress = 1;

            for (int i = 0; i < boxes.Length; i++)
            {
                if (i == index) boxes[i].SetPosition(1 - progress);
                else if (i == index + 1) boxes[i].SetPosition(progress);
                SetWhite(index, index + 1, progress);
            }
            await Task.Yield();
        }

        for (int i = 0; i < boxes.Length; i++)
        {
            if (i == index) boxes[i].SetPosition(1);
            else if (i == index + 1) boxes[i].SetPosition(0);
        }
        SetWhite(index, index + 1, 0);
        UpdateDescription();
        isTransitioning = false;
    }

    public async void Down()
    {
        if (isTransitioning || index >= boxes.Length - 1) return;
        isTransitioning = true;
        index++;
        float d = duration;

        while (d > 0)
        {
            d -= Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(d / duration);
            float progress = -t * (t - 2);
            if (progress > 1) progress = 1;

            for (int i = 0; i < boxes.Length; i++)
            {
                if (i == index) boxes[i].SetPosition(1 - progress);
                else if (i == index - 1) boxes[i].SetPosition(progress);
                SetWhite(index, index - 1, progress);
            }
            await Task.Yield();
        }

        for (int i = 0; i < boxes.Length; i++)
        {
            if (i == index) boxes[i].SetPosition(1);
            else if (i == index - 1) boxes[i].SetPosition(0);
        }
        SetWhite(index, index - 1, 0);
        UpdateDescription();

        isTransitioning = false;
    }

    public void SetAlpha(float alpha)
    {
        alpha = Mathf.Clamp01(alpha);
        CG.alpha = alpha;
        CG.blocksRaycasts = alpha > 0.01f;
        rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(560f, 470f, Mathf.SmoothStep(0f, 1f, alpha)), -70f);
    }

    private void SetWhite(int index, int pre, float progress)
    {
        if (progress > 0.5)
        {
            whiteCG.alpha = (progress - 0.5f) * 2;
            whiteBar.anchoredPosition = GetWhitePosition(pre);
        }
        else
        {
            whiteCG.alpha = (0.5f - progress) * 2;
            whiteBar.anchoredPosition = GetWhitePosition(index);
        }
    }

    private Vector2 GetWhitePosition(int selectedIndex)
    {
        return selectedIndex switch
        {
            0 => new Vector2(0f, 180f),
            1 => new Vector2(0f, 0f),
            _ => new Vector2(0f, -180f),
        };
    }

    private void CreateLabels(TMP_Text sourceText)
    {
        titleText = CreateOrGetText("DifficultyTitle", new Vector2(0f, 310f), new Vector2(760f, 96f), 60f, TextAlignmentOptions.Center, sourceText);
        titleText.color = Color.white;
        titleText.text = "<size=28>なんいど　せんたく</size>\n難易度を選択";

        descriptionText = CreateOrGetText("DifficultyDescription", new Vector2(0f, -315f), new Vector2(860f, 64f), 30f, TextAlignmentOptions.Center, sourceText);
        descriptionText.color = Color.white;

        guideText = CreateOrGetText("DifficultyGuide", new Vector2(0f, -450f), new Vector2(760f, 54f), 28f, TextAlignmentOptions.Center, sourceText);
        guideText.color = new Color(1f, 1f, 1f, 0.72f);
        guideText.text = "ボタンを押して決定";
    }

    private TMP_Text CreateOrGetText(string objectName, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment, TMP_Text sourceText)
    {
        Transform child = transform.Find(objectName);
        TMP_Text text;
        if (child == null)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(transform, false);
            text = textObject.GetComponent<TMP_Text>();
            text.font = sourceText.font;
            text.material = sourceText.fontMaterial;
        }
        else
        {
            text = child.GetComponent<TMP_Text>();
        }

        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchoredPosition = position;
        textRect.sizeDelta = size;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private void UpdateDescription()
    {
        descriptionText.text = descriptions[Mathf.Clamp(index, 0, descriptions.Length - 1)];
    }
}

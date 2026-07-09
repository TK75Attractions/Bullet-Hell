<<<<<<< HEAD
=======
using System.Collections.Generic;
using System.Threading.Tasks;
>>>>>>> origin/main
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DefficultyBar : MonoBehaviour
{
    private const float TopY = 200f;
    private const float SlotSpacing = 200f;

    private CanvasGroup CG;
    private Transform listRoot;
    private RectTransform whiteBar;
    private CanvasGroup whiteCG;
    private GameObject customBoxTemplate;

    private readonly List<DefficultyBox> officialBoxes = new List<DefficultyBox>();
    private readonly List<DefficultyBox> customBoxes = new List<DefficultyBox>();
    private readonly List<DefficultyBox> boxes = new List<DefficultyBox>();
    private readonly List<DifficultySelection> difficultyOptions = new List<DifficultySelection>();

<<<<<<< HEAD
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
=======
    public int index = 0;
    public DifficultySelection SelectedDifficulty
    {
        get
        {
            if (difficultyOptions.Count == 0) return DifficultySelection.FromOfficial(Difficulty.Easy);
            return difficultyOptions[Mathf.Clamp(index, 0, difficultyOptions.Count - 1)];
        }
    }

    private readonly float duration = 0.15f;
    private bool isTransitioning = false;
>>>>>>> origin/main

    private class DefficultyBox
    {
        public GameObject gameObject;
        public CanvasGroup CG;
        public RectTransform rectTransform;
<<<<<<< HEAD
        public float baseX;
        private TMP_Text nameText;
        private Color baseTextColor;

        public DefficultyBox(Transform trans, string name, Color barColor, Color textColor)
=======
        private Image stageBar;
        private TMP_Text stageName;

        public DefficultyBox(Transform trans)
>>>>>>> origin/main
        {
            gameObject = trans.gameObject;
            CG = trans.GetComponent<CanvasGroup>();
            if (CG == null) CG = trans.gameObject.AddComponent<CanvasGroup>();

            rectTransform = trans.GetComponent<RectTransform>();
<<<<<<< HEAD
            trans.Find("StageBar").GetComponent<Image>().color = barColor;
            nameText = trans.Find("StageName").GetComponent<TMP_Text>();
            nameText.text = name;
            baseTextColor = textColor;
            baseX = rectTransform.anchoredPosition.x;
=======
            stageBar = trans.Find("StageBar").GetComponent<Image>();
            stageName = trans.Find("StageName").GetComponent<TMP_Text>();
        }

        public void Configure(string name, Color color, int slot)
        {
            stageBar.color = color;
            stageName.text = name;
            SetSlot(slot);
        }

        public void SetSlot(int slot)
        {
            rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, TopY - SlotSpacing * slot);
>>>>>>> origin/main
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
<<<<<<< HEAD
        Transform trans = transform.Find("List");
        (trans.Find("Easy") as RectTransform).anchoredPosition = new Vector2(0f, 135f);
        (trans.Find("Normal") as RectTransform).anchoredPosition = Vector2.zero;
        (trans.Find("Lunatic") as RectTransform).anchoredPosition = new Vector2(0f, -135f);
        boxes[0] = new DefficultyBox(trans.Find("Easy"), "EASY", new Color(0.086f, 0.227f, 0.373f), new Color(0.56f, 0.72f, 0.91f));
        boxes[1] = new DefficultyBox(trans.Find("Normal"), "NORMAL", new Color(0.055f, 0.525f, 0.91f), new Color(0.85f, 0.93f, 1f));
        boxes[2] = new DefficultyBox(trans.Find("Lunatic"), "LUNATIC", new Color(0.36f, 0.078f, 0.188f), new Color(0.91f, 0.6f, 0.69f));
        CG = GetComponent<CanvasGroup>();
        CG.alpha = 0;
=======
        listRoot = transform.Find("List");
        CG = GetComponent<CanvasGroup>();
        CG.alpha = 0;

        officialBoxes.Clear();
        officialBoxes.Add(new DefficultyBox(listRoot.Find("Easy")));
        officialBoxes.Add(new DefficultyBox(listRoot.Find("Normal")));
        officialBoxes.Add(new DefficultyBox(listRoot.Find("Lunatic")));
        customBoxTemplate = listRoot.Find("Lunatic").gameObject;
>>>>>>> origin/main

        whiteBar = transform.Find("White").GetComponent<RectTransform>();
        whiteCG = whiteBar.GetComponent<CanvasGroup>();
        whiteCG.alpha = 1;

<<<<<<< HEAD
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
=======
        SetDifficultyOptions(DifficultyUtility.GetOfficialSelections());
    }

    public void SetStage(StageData stage)
    {
        SetDifficultyOptions(stage != null
            ? stage.GetDifficultySelections()
            : DifficultyUtility.GetOfficialSelections());
    }

    public void SetDifficultyOptions(List<DifficultySelection> options)
    {
        DifficultySelection previousSelection = SelectedDifficulty;

        ClearCustomBoxes();
        boxes.Clear();
        difficultyOptions.Clear();

        AddOfficialOption(Difficulty.Easy);
        AddOfficialOption(Difficulty.Normal);
        AddOfficialOption(Difficulty.Lunatic);

        if (options != null)
        {
            for (int i = 0; i < options.Count; i++)
            {
                DifficultySelection option = options[i];
                if (!option.IsValid() || ContainsDifficulty(option.id)) continue;

                AddCustomOption(option);
            }
        }

        index = FindDifficultyIndex(previousSelection.id);
        if (index < 0) index = 0;
        RefreshBoxes();
>>>>>>> origin/main
    }

    // Snap selection to the given index without animating (used on entering the screen).
    public void ResetSelection(int newIndex)
    {
<<<<<<< HEAD
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
=======
        if (isTransitioning || index <= 0 || boxes.Count == 0) return;

        isTransitioning = true;
        int previousIndex = index;
        index--;
        float d = duration;

        while (d > 0)
        {
            d -= Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(d / duration);
            float progress = -t * (t - 2);
            if (progress > 1) progress = 1;

            for (int i = 0; i < boxes.Count; i++)
            {
                if (i == index) boxes[i].SetPosition(1 - progress);
                else if (i == previousIndex) boxes[i].SetPosition(progress);
            }

            SetWhite(index, previousIndex, progress);
            await Task.Yield();
        }

        RefreshBoxes();
        isTransitioning = false;
>>>>>>> origin/main
    }

    public void Down()
    {
<<<<<<< HEAD
        if (index >= boxes.Length - 1) return;
        index++;
        RefreshDescription();
=======
        if (isTransitioning || index >= boxes.Count - 1 || boxes.Count == 0) return;

        isTransitioning = true;
        int previousIndex = index;
        index++;
        float d = duration;

        while (d > 0)
        {
            d -= Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(d / duration);
            float progress = -t * (t - 2);
            if (progress > 1) progress = 1;

            for (int i = 0; i < boxes.Count; i++)
            {
                if (i == index) boxes[i].SetPosition(1 - progress);
                else if (i == previousIndex) boxes[i].SetPosition(progress);
            }

            SetWhite(index, previousIndex, progress);
            await Task.Yield();
        }

        RefreshBoxes();
        isTransitioning = false;
>>>>>>> origin/main
    }

    public void SetAlpha(float alpha)
    {
        CG.alpha = alpha;
    }

<<<<<<< HEAD
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
=======
    private void AddOfficialOption(Difficulty difficulty)
    {
        int slot = difficultyOptions.Count;
        DifficultySelection selection = DifficultySelection.FromOfficial(difficulty);
        difficultyOptions.Add(selection);

        DefficultyBox box = officialBoxes[(int)difficulty];
        box.gameObject.SetActive(true);
        box.Configure(selection.displayName, GetOfficialColor(difficulty), slot);
        boxes.Add(box);
    }

    private void AddCustomOption(DifficultySelection selection)
    {
        int slot = difficultyOptions.Count;
        DifficultySelection normalizedSelection = DifficultySelection.FromCustom(selection.id, selection.displayName);
        difficultyOptions.Add(normalizedSelection);

        GameObject boxObject = Instantiate(customBoxTemplate, listRoot);
        boxObject.name = "CustomDifficulty_" + normalizedSelection.id;

        DefficultyBox box = new DefficultyBox(boxObject.transform);
        box.Configure(normalizedSelection.displayName, GetCustomColor(slot), slot);
        customBoxes.Add(box);
        boxes.Add(box);
    }

    private void ClearCustomBoxes()
    {
        for (int i = 0; i < customBoxes.Count; i++)
        {
            if (customBoxes[i] != null && customBoxes[i].gameObject != null)
            {
                Destroy(customBoxes[i].gameObject);
            }
        }

        customBoxes.Clear();
    }

    private bool ContainsDifficulty(string difficultyId)
    {
        string normalizedId = DifficultyUtility.NormalizeId(difficultyId);
        for (int i = 0; i < difficultyOptions.Count; i++)
        {
            if (string.Equals(difficultyOptions[i].id, normalizedId, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private int FindDifficultyIndex(string difficultyId)
    {
        string normalizedId = DifficultyUtility.NormalizeId(difficultyId);
        for (int i = 0; i < difficultyOptions.Count; i++)
        {
            if (string.Equals(difficultyOptions[i].id, normalizedId, System.StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private void RefreshBoxes()
    {
        if (boxes.Count == 0) return;

        index = Mathf.Clamp(index, 0, boxes.Count - 1);
        for (int i = 0; i < boxes.Count; i++)
        {
            boxes[i].SetSlot(i);
            boxes[i].SetPosition(i == index ? 1 : 0);
        }

        whiteCG.alpha = 1;
        whiteBar.anchoredPosition = new Vector2(whiteBar.anchoredPosition.x, TopY - SlotSpacing * index);
    }

    private void SetWhite(int index, int previousIndex, float progress)
    {
        if (progress > 0.5f)
        {
            whiteCG.alpha = (progress - 0.5f) * 2;
            whiteBar.anchoredPosition = new Vector2(whiteBar.anchoredPosition.x, TopY - SlotSpacing * previousIndex);
        }
        else
        {
            whiteCG.alpha = (0.5f - progress) * 2;
            whiteBar.anchoredPosition = new Vector2(whiteBar.anchoredPosition.x, TopY - SlotSpacing * index);
        }
    }

    private static Color GetOfficialColor(Difficulty difficulty)
    {
        switch (difficulty)
        {
            case Difficulty.Easy:
                return new Color(0f, 1f, 0.227f);
            case Difficulty.Normal:
                return new Color(0.165f, 0.592f, 1f);
            case Difficulty.Lunatic:
                return new Color(1f, 0.447f, 0.502f);
            default:
                return Color.white;
        }
    }

    private static Color GetCustomColor(int slot)
    {
        return Color.HSVToRGB(Mathf.Repeat(0.13f * slot + 0.58f, 1f), 0.7f, 1f);
>>>>>>> origin/main
    }
}

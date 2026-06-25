using System.Collections.Generic;
using System.Threading.Tasks;
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

    private class DefficultyBox
    {
        public GameObject gameObject;
        public CanvasGroup CG;
        public RectTransform rectTransform;
        private Image stageBar;
        private TMP_Text stageName;

        public DefficultyBox(Transform trans)
        {
            gameObject = trans.gameObject;
            CG = trans.GetComponent<CanvasGroup>();
            if (CG == null) CG = trans.gameObject.AddComponent<CanvasGroup>();

            rectTransform = trans.GetComponent<RectTransform>();
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
        }

        public void SetPosition(float progress)
        {
            CG.alpha = 0.4f + 0.6f * progress;
            rectTransform.localScale = Vector3.one * (0.8f + 0.2f * progress);
        }
    }

    public void Init()
    {
        listRoot = transform.Find("List");
        CG = GetComponent<CanvasGroup>();
        CG.alpha = 0;

        officialBoxes.Clear();
        officialBoxes.Add(new DefficultyBox(listRoot.Find("Easy")));
        officialBoxes.Add(new DefficultyBox(listRoot.Find("Normal")));
        officialBoxes.Add(new DefficultyBox(listRoot.Find("Lunatic")));
        customBoxTemplate = listRoot.Find("Lunatic").gameObject;

        whiteBar = transform.Find("White").GetComponent<RectTransform>();
        whiteCG = whiteBar.GetComponent<CanvasGroup>();
        whiteCG.alpha = 1;

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
    }

    public async void Up()
    {
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
    }

    public async void Down()
    {
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
    }

    public void SetAlpha(float alpha)
    {
        CG.alpha = alpha;
    }

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
    }
}

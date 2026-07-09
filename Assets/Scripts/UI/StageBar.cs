using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;


public class StageBar : MonoBehaviour
{
    [SerializeField] private GameObject stageBoxPrefab;
    private Transform parent;
    private CanvasGroup canvasGroup;
    private CanvasGroup whiteBar;
    private List<StageBox> stageBoxes = new List<StageBox>();
    public int currentStage = 0;
    private bool isTransitioning = false;
    static private readonly float duration = 0.15f;

    public void Init()
    {
        parent = transform.Find("List");
        canvasGroup = GetComponent<CanvasGroup>();
        whiteBar = transform.Find("White").GetComponent<CanvasGroup>();
        isTransitioning = false;
        for (int i = 0; i < 6; i++)
        {
            stageBoxes.Add(Instantiate(stageBoxPrefab, parent).GetComponent<StageBox>());
            stageBoxes[i].Init();
            stageBoxes[i].gameObject.name = "StageBox" + i;
        }

        int length = GManager.Control.SDB.GetStageCount();
        RefreshStageNames(length);
        SetStageBoxPositions(length);
    }

    public async void Down()
    {
        if (!isTransitioning)
        {
            isTransitioning = true;
            float d = duration;

            int length = GManager.Control.SDB.GetStageCount();
            if (currentStage >= length - 1)
            {
                isTransitioning = false;
                return;
            }

            currentStage++;
            RefreshStageNames(length);

            while (d > 0)
            {
                d -= Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(d / duration);
                float progress = -t * (t - 2);
                if (progress > 1) progress = 1;

                for (int i = 0; i < stageBoxes.Count; i++)
                {
                    int p = currentStage - 3 + i;
                    if (0 <= p && p < length) stageBoxes[i].SetPosition(i + progress);
                    else stageBoxes[i].SetPosition(0);
                }

                SetWhiteAlpha(t * t * t);
                await Task.Yield();
            }

            SetStageBoxPositions(length);
            SetWhiteAlpha(1);
            isTransitioning = false;
        }
    }

    public async void Up()
    {
        if (!isTransitioning)
        {
            isTransitioning = true;
            float d = duration;

            int length = GManager.Control.SDB.GetStageCount();
            if (currentStage <= 0)
            {
                isTransitioning = false;
                return;
            }

            currentStage--;
            RefreshStageNames(length);

            while (d > 0)
            {
                d -= Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(d / duration);
                float progress = -t * (t - 2);

                for (int i = 0; i < stageBoxes.Count; i++)
                {
                    int k = 6;
                    if (i != 0) k = i - 1;

                    int p = currentStage - 3 + i;
                    if (0 <= p && p < length) stageBoxes[i].SetPosition(k + 1 - progress);
                    else stageBoxes[i].SetPosition(0);
                }
                SetWhiteAlpha(t * t * t);
                await Task.Yield();
            }

            SetStageBoxPositions(length);
            SetWhiteAlpha(1);

            isTransitioning = false;
        }
    }

    public void SetAlpha(float alpha)
    {
        canvasGroup.alpha = alpha;
    }

    private void SetWhiteAlpha(float progress)
    {
        if (progress < 0.5f) whiteBar.alpha = (0.5f - progress) * 2;
        else whiteBar.alpha = (progress - 0.5f) * 2;
    }

    private void RefreshStageNames(int length)
    {
        for (int i = 0; i < stageBoxes.Count; i++)
        {
            int stageIndex = currentStage - 3 + i;
            stageBoxes[i].SetStageName(GetStageName(stageIndex, length));
        }
    }

    private void SetStageBoxPositions(int length)
    {
        for (int i = 0; i < stageBoxes.Count; i++)
        {
            int stageIndex = currentStage - 3 + i;
            if (0 <= stageIndex && stageIndex < length) stageBoxes[i].SetPosition(i);
            else stageBoxes[i].SetPosition(0);
        }
    }

    private string GetStageName(int index, int length)
    {
        if (index < 0 || index >= length) return string.Empty;

        StageData stage = GManager.Control.SDB.GetStage(index);
        if (stage == null || string.IsNullOrWhiteSpace(stage.stageName)) return "Stage " + index;
        return stage.stageName;
    }
}

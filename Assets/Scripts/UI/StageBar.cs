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
    static private readonly float duration = 0.2f;

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
            stageBoxes[i].SetPosition(i);
            stageBoxes[i].gameObject.name = "StageBox" + i;
        }
        RefreshStageNames();
    }

    public async void Down()
    {
        if (!isTransitioning)
        {
            isTransitioning = true;
            float d = duration;

            int length = GManager.Control.SDB.stages.Count;
            if (currentStage >= length - 1)
            {
                isTransitioning = false;
                return;
            }

            currentStage++;
            RefreshStageNames();

            while (d > 0)
            {
                d -= Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(d / duration);
                float progress = EaseOutCubic(t);

                for (int i = 0; i < stageBoxes.Count; i++)
                {
                    int p = currentStage - 3 + i;
                    if (0 <= p && p < length) stageBoxes[i].SetPosition(i + progress);
                    else stageBoxes[i].SetPosition(0);
                }

                SetWhiteAlpha(progress);
                await Task.Yield();
            }

            for (int i = 0; i < stageBoxes.Count; i++)
            {
                int p = currentStage - 3 + i;
                if (0 <= p && p < length) stageBoxes[i].SetPosition(i);
                else stageBoxes[i].SetPosition(0);
            }
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

            int length = GManager.Control.SDB.stages.Count;
            if (currentStage <= 0)
            {
                isTransitioning = false;
                return;
            }

            currentStage--;
            RefreshStageNames();

            while (d > 0)
            {
                d -= Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(d / duration);
                float progress = EaseOutCubic(t);

                for (int i = 0; i < stageBoxes.Count; i++)
                {
                    int k = 6;
                    if (i != 0) k = i - 1;

                    int p = currentStage - 3 + i;
                    if (0 <= p && p < length) stageBoxes[i].SetPosition(k + 1 - progress);
                    else stageBoxes[i].SetPosition(0);
                }
                SetWhiteAlpha(progress);
                await Task.Yield();
            }

            for (int i = 0; i < stageBoxes.Count; i++)
            {
                int p = currentStage - 3 + i;
                if (0 <= p && p < length) stageBoxes[i].SetPosition(i);
                else stageBoxes[i].SetPosition(0);
            }
            SetWhiteAlpha(1);

            isTransitioning = false;
        }
    }

    public void SetAlpha(float alpha)
    {
        canvasGroup.alpha = alpha;
        canvasGroup.blocksRaycasts = alpha > 0.01f;
    }

    private void SetWhiteAlpha(float progress)
    {
        if (progress < 0.5f) whiteBar.alpha = (0.5f - progress) * 2;
        else whiteBar.alpha = (progress - 0.5f) * 2;
    }

    private void RefreshStageNames()
    {
        int length = GManager.Control.SDB.GetStageCount();
        for (int i = 0; i < stageBoxes.Count; i++)
        {
            int stageIndex = currentStage - 3 + i;
            if (0 <= stageIndex && stageIndex < length)
            {
                StageData stage = GManager.Control.SDB.GetStage(stageIndex);
                stageBoxes[i].SetStageName(string.IsNullOrEmpty(stage.stageName) ? $"Stage {stageIndex + 1}" : stage.stageName);
            }
            else
            {
                stageBoxes[i].SetStageName("");
            }
        }
    }

    private static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        float oneMinus = 1f - t;
        return 1f - oneMinus * oneMinus * oneMinus;
    }
}

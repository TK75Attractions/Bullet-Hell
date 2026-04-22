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
            stageBoxes[i].SetStageName("None");
            stageBoxes[i].SetPosition(i);
            stageBoxes[i].gameObject.name = "StageBox" + i;
        }
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
            if (0 <= currentStage - 2) stageBoxes[1].SetStageName("Stage " + (currentStage - 2));
            if (0 <= currentStage - 1) stageBoxes[2].SetStageName("Stage " + (currentStage - 1));
            if (currentStage < length) stageBoxes[3].SetStageName("Stage " + (currentStage));
            if (currentStage + 1 < length) stageBoxes[4].SetStageName("Stage " + (currentStage + 1));
            if (currentStage + 2 < length) stageBoxes[5].SetStageName("Stage " + (currentStage + 2));
            if (currentStage - 3 >= 0) stageBoxes[0].SetStageName("Stage " + (currentStage - 3));

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
            if (0 <= currentStage - 2) stageBoxes[1].SetStageName("Stage " + (currentStage - 2));
            if (0 <= currentStage - 1) stageBoxes[2].SetStageName("Stage " + (currentStage - 1));
            if (currentStage < length) stageBoxes[3].SetStageName("Stage " + (currentStage));
            if (currentStage + 1 < length) stageBoxes[4].SetStageName("Stage " + (currentStage + 1));
            if (currentStage + 2 < length) stageBoxes[5].SetStageName("Stage " + (currentStage + 2));
            if (currentStage + 3 < length) stageBoxes[0].SetStageName("Stage " + (currentStage + 3));

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
    }

    private void SetWhiteAlpha(float progress)
    {
        if (progress < 0.5f) whiteBar.alpha = (0.5f - progress) * 2;
        else whiteBar.alpha = (progress - 0.5f) * 2;
    }
}
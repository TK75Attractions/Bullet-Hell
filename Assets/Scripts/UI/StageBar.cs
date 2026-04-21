using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;


public class StageBar : MonoBehaviour
{
    [SerializeField] private GameObject stageBoxPrefab;
    private CanvasGroup canvasGroup;
    private List<StageBox> stageBoxes = new List<StageBox>();
    private int currentStage = 0;
    private bool isTransitioning = false;

    public void Init()
    {
        for (int i = 0; i < 6; i++)
        {
            stageBoxes.Add(Instantiate(stageBoxPrefab, transform).GetComponent<StageBox>());
            stageBoxes[i].Init();
            stageBoxes[i].SetStageName("Stage " + (i + 1));
            stageBoxes[i].SetPosition(i);
        }
    }

    public async void Up()
    {
        if (!isTransitioning)
        {
            isTransitioning = true;
            float duration = 0.08f;

            int length = GManager.Control.SDB.stages.Count;
            if (currentStage >= length - 1) return;
            currentStage++;
            if (0 <= currentStage - 2) stageBoxes[1].SetStageName("Stage " + (currentStage - 2));
            if (0 <= currentStage - 1) stageBoxes[2].SetStageName("Stage " + (currentStage - 1));
            if (currentStage < length) stageBoxes[3].SetStageName("Stage " + (currentStage));
            if (currentStage + 1 < length) stageBoxes[4].SetStageName("Stage " + (currentStage + 1));
            if (currentStage + 2 < length) stageBoxes[5].SetStageName("Stage " + (currentStage + 2));
            if (currentStage - 3 >= 0) stageBoxes[0].SetStageName("Stage " + (currentStage - 3));

            while (duration > 0)
            {
                duration -= Time.deltaTime;
                for (int i = 0; i < stageBoxes.Count; i++)
                {
                    stageBoxes[i].SetPosition(i + 1 - (duration / 0.08f));
                }
                await Task.Yield();
            }
            isTransitioning = false;
        }
    }

    public async void Down()
    {
        if (!isTransitioning)
        {
            isTransitioning = true;
            float duration = 0.08f;

            int length = GManager.Control.SDB.stages.Count;
            if (currentStage <= 0) return;
            currentStage--;
            if (0 <= currentStage - 2) stageBoxes[1].SetStageName("Stage " + (currentStage - 2));
            if (0 <= currentStage - 1) stageBoxes[2].SetStageName("Stage " + (currentStage - 1));
            if (currentStage < length) stageBoxes[3].SetStageName("Stage " + (currentStage));
            if (currentStage + 1 < length) stageBoxes[4].SetStageName("Stage " + (currentStage + 1));
            if (currentStage + 2 < length) stageBoxes[5].SetStageName("Stage " + (currentStage + 2));
            if (currentStage + 3 < length) stageBoxes[0].SetStageName("Stage " + (currentStage + 3));

            while (duration > 0)
            {
                duration -= Time.deltaTime;
                for (int i = 0; i < stageBoxes.Count; i++)
                {
                    int k = 6;
                    if (i != 0) k = i - 1;
                    stageBoxes[i].SetPosition(k + (duration / 0.08f));
                }
                await Task.Yield();
            }
            isTransitioning = false;
        }
    }
}
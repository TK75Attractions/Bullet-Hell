using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Android.Gradle;
using UnityEngine;


public class StageBar : MonoBehaviour
{
    [SerializeField] private GameObject stageBoxPrefab;
    private Transform parent;
    private CanvasGroup canvasGroup;
    private List<StageBox> stageBoxes = new List<StageBox>();
    public int currentStage = 0;
    private bool isTransitioning = false;
    static private readonly float duration = 0.1f;

    public void Init()
    {
        parent = transform.Find("List");
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
        Debug.Log("Up");
        Debug.Log(isTransitioning);
        if (!isTransitioning)
        {
            Debug.Log("UpUpUp");
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
                d -= Time.deltaTime;
                for (int i = 0; i < stageBoxes.Count; i++)
                {
                    int p = currentStage - 3 + i;
                    if (0 <= p && p < length) stageBoxes[i].SetPosition(i + (d / duration));
                    else stageBoxes[i].SetPosition(0);
                }
                await Task.Yield();
            }

            for (int i = 0; i < stageBoxes.Count; i++)
            {
                int p = currentStage - 3 + i;
                if (0 <= p && p < length) stageBoxes[i].SetPosition(i);
                else stageBoxes[i].SetPosition(0);
            }

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
                d -= Time.deltaTime;
                for (int i = 0; i < stageBoxes.Count; i++)
                {
                    int k = 6;
                    if (i != 0) k = i - 1;

                    int p = currentStage - 3 + i;
                    if (0 <= p && p < length) stageBoxes[i].SetPosition(k + 1 - (d / duration));
                    else stageBoxes[i].SetPosition(0);
                }
                await Task.Yield();
            }

            for (int i = 0; i < stageBoxes.Count; i++)
            {
                int p = currentStage - 3 + i;
                if (0 <= p && p < length) stageBoxes[i].SetPosition(i);
                else stageBoxes[i].SetPosition(0);
            }

            isTransitioning = false;
        }
    }
}
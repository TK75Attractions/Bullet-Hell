using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data;
using UnityEditor.SceneManagement;

public class UIManager : MonoBehaviour
{
    [SerializeField] private GameObject TitlePanel;
    [SerializeField] private GameObject ChooseStagePanel;
    [SerializeField] private Transform StageBoxParent;
    [SerializeField] private GameObject StageBoxPrefab;
    private StageBox[] StageBoxes = new StageBox[3];
    private int currentStageId = 0;
    private bool isInChooseStage = false;
    private bool isTransitioning = false;



    public void Init()
    {
        currentStageId = 0;
        TitlePanel.SetActive(true);
        ChooseStagePanel.SetActive(false);
        isInChooseStage = false;
        isTransitioning = false;
    }

    public void UpdateUI()
    {
        if (GManager.Control.IManager.buttonPressed && !isInChooseStage) GoToChooseStage();

        if (GManager.Control.IManager.upPressedThisFrame)
        {
            Debug.Log("Up Pressed");
            SetStageBox(false);
        }
        else if (GManager.Control.IManager.downPressedThisFrame)
        {
            SetStageBox(true);
        }
    }

    public async void GoToChooseStage()
    {
        TitlePanel.SetActive(false);
        ChooseStagePanel.SetActive(true);
        isInChooseStage = true;
        isTransitioning = true;
        currentStageId = 0;

        foreach (Transform child in StageBoxParent) Destroy(child.gameObject);

        StageBoxes[0] = Instantiate(StageBoxPrefab, StageBoxParent).GetComponent<StageBox>();
        StageBoxes[0].Init(0, 0, false);
        StageBoxes[1] = Instantiate(StageBoxPrefab, StageBoxParent).GetComponent<StageBox>();
        StageBoxes[1].Init(1, currentStageId, true);
        StageBoxes[2] = Instantiate(StageBoxPrefab, StageBoxParent).GetComponent<StageBox>();
        StageBoxes[2].Init(2, currentStageId + 1, true);

        while (!StageBoxes[1].isReady || !StageBoxes[2].isReady) await Task.Yield();

        Debug.Log("Ready");
        isTransitioning = false;
        return;
    }

    /*
        private async void DownPressed()
        {
            if (isInChooseStage && !isTransitioning)
            {
                Debug.Log("Down Pressed in Choose Stage");
                if(currentStageId == GManager.Control.SDB.stages.Count - 1) return;
                isTransitioning = true;

                currentStageId++;
                if(currentStageId == GManager.Control.SDB.stages.Count - 1)
                {
                    StageBoxes[0].Set(currentStageId - 1);
                    StageBoxes[1].Set(currentStageId);
                    StageBoxes[2].Disappear();
                }
                else if (currentStageId == 1)
                {
                    StageBoxes[0].Appear(currentStageId - 1, 0.5f);
                    StageBoxes[1].Set(currentStageId);
                    StageBoxes[2].Set(currentStageId + 1);
                }
                else
                {
                    StageBoxes[0].Set(currentStageId - 1);
                    StageBoxes[1].Set(currentStageId);
                    StageBoxes[2].Set(currentStageId + 1);
                }

                while (!StageBoxes[0].isReady || !StageBoxes[1].isReady || !StageBoxes[2].isReady) await Task.Yield();
                isTransitioning = false;
            }
        }

        private async void UpPressed()
        {
            if (isInChooseStage && !isTransitioning)
            {
                if(currentStageId == 0) return;
                isTransitioning = true;

                currentStageId--;
                if(currentStageId == 0)
                {
                    StageBoxes[0].Disappear();
                    StageBoxes[1].Set(currentStageId);
                    StageBoxes[2].Set(currentStageId + 1);
                }
                else if (currentStageId == GManager.Control.SDB.stages.Count - 2)
                {
                    StageBoxes[0].Set(currentStageId - 1);
                    StageBoxes[1].Set(currentStageId);
                    StageBoxes[2].Appear(currentStageId + 1, 0.5f);
                }
                else
                {
                    StageBoxes[0].Set(currentStageId - 1);
                    StageBoxes[1].Set(currentStageId);
                    StageBoxes[2].Set(currentStageId + 1);
                }

                while (!StageBoxes[0].isReady || !StageBoxes[1].isReady || !StageBoxes[2].isReady) await Task.Yield();
                isTransitioning = false;
            }
        }
    */

    private async void SetStageBox(bool Down)
    {
        if (!isInChooseStage || isTransitioning) return;
        if (Down)
        {
            Debug.Log("Down Pressed in Choose Stage");
            if (currentStageId == GManager.Control.SDB.stages.Count - 1) return;
            isTransitioning = true;

            List<int> vs = new List<int>();
            if (currentStageId == 0) vs = new List<int> { 1, 2 };
            else vs = new List<int> { 0, 1, 2 };

            float duration = 0.2f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                foreach (int i in vs) StageBoxes[i].SetState(t);
                await Task.Yield();
            }

            currentStageId++;
            if (currentStageId == GManager.Control.SDB.stages.Count - 1) vs = new List<int> { 0, 1 };
            else vs = new List<int> { 0, 1, 2 };
            foreach (int i in vs) StageBoxes[i].SetData(currentStageId + i - 1);

            elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                foreach (int i in vs) StageBoxes[i].SetState(1 - t);
                await Task.Yield();
            }

            isTransitioning = false;
        }
        else
        {
            if (currentStageId == 0) return;
            isTransitioning = true;

            List<int> vs = new List<int>();
            if (currentStageId == GManager.Control.SDB.stages.Count - 1) vs = new List<int> { 0, 1 };
            else vs = new List<int> { 0, 1, 2 };

            float duration = 0.2f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                foreach (int i in vs) StageBoxes[i].SetState(t);
                await Task.Yield();
            }

            currentStageId--;
            if (currentStageId == 0) vs = new List<int> { 1, 2 };
            else vs = new List<int> { 0, 1, 2 };
            foreach (int i in vs) StageBoxes[i].SetData(currentStageId + i - 1);

            elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                foreach (int i in vs) StageBoxes[i].SetState(1 - t);
                await Task.Yield();
            }

            isTransitioning = false;
        }



    }

    private async void GoToGame()
    {

    }

    public void GoToCreateStageBoxes()
    {

    }
}

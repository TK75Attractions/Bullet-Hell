using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using TMPro;


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

    private TMP_Text arrowUp;
    private TMP_Text arrowDown;
    private RectTransform arrowUpRect;
    private RectTransform arrowDownRect;
    private float arrowUpBaseY;
    private float arrowDownBaseY;
    private float animTime;

    public void Init()
    {
        parent = transform.Find("List");
        canvasGroup = GetComponent<CanvasGroup>();
        whiteBar = transform.Find("White").GetComponent<CanvasGroup>();
        whiteBar.alpha = 1f;
        isTransitioning = false;
        for (int i = 0; i < 6; i++)
        {
            stageBoxes.Add(Instantiate(stageBoxPrefab, parent).GetComponent<StageBox>());
            stageBoxes[i].Init();
            stageBoxes[i].gameObject.name = "StageBox" + i;
        }

        Transform up = transform.Find("ArrowUp");
        Transform down = transform.Find("ArrowDown");
        if (up != null)
        {
            arrowUp = up.GetComponent<TMP_Text>();
            arrowUpRect = up.GetComponent<RectTransform>();
            arrowUpBaseY = arrowUpRect.anchoredPosition.y;
        }
        if (down != null)
        {
            arrowDown = down.GetComponent<TMP_Text>();
            arrowDownRect = down.GetComponent<RectTransform>();
            arrowDownBaseY = arrowDownRect.anchoredPosition.y;
        }

        int length = GManager.Control.SDB.GetStageCount();
        RefreshStageNames(length);
        SetStageBoxPositions(length);
    }

    // Per-frame idle animation: arrow hints bob, the selected box breathes.
    public void Tick(float dt)
    {
        animTime += dt;
        int length = GManager.Control.SDB.GetStageCount();
        float bob = Mathf.Sin(animTime * 4f) * 6f;
        float blink = 0.55f + 0.35f * Mathf.Sin(animTime * 4f);

        if (arrowUp != null)
        {
            bool canUp = currentStage > 0;
            arrowUpRect.anchoredPosition = new Vector2(arrowUpRect.anchoredPosition.x, arrowUpBaseY + (canUp ? bob : 0f));
            arrowUp.alpha = canUp ? blink : 0.1f;
        }
        if (arrowDown != null)
        {
            bool canDown = currentStage < length - 1;
            arrowDownRect.anchoredPosition = new Vector2(arrowDownRect.anchoredPosition.x, arrowDownBaseY - (canDown ? bob : 0f));
            arrowDown.alpha = canDown ? blink : 0.1f;
        }

        if (!isTransitioning && stageBoxes.Count > 3)
        {
            float pulse = 1f + 0.02f * (0.5f + 0.5f * Mathf.Sin(animTime * 3f));
            stageBoxes[3].SetPulse(pulse);
        }
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

                await Task.Yield();
            }

            SetStageBoxPositions(length);
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
                await Task.Yield();
            }

            SetStageBoxPositions(length);

            isTransitioning = false;
        }
    }

    public void SetAlpha(float alpha)
    {
        canvasGroup.alpha = alpha;
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

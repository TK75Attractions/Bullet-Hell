using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using System.Threading.Tasks;
using System;
using UnityEngine.Rendering.UI;

public class StageBox : MonoBehaviour
{
    private static readonly float normalScale = 1f;
    private static readonly float miniScale = 0.8f;

    private static readonly float interval = 140f;

    private CanvasGroup CG;
    private TMP_Text stageNameText;
    private RectTransform rectTransform;
    public void Init()
    {
        // Initialize the stage box here
        stageNameText = transform.Find("StageName").GetComponent<TMP_Text>();
        rectTransform = GetComponent<RectTransform>();
        CG = GetComponent<CanvasGroup>();
    }

    public void SetStageName(string name)
    {
        stageNameText.text = name;
    }

    public void SetPosition(float progress)
    {
        float a = 0.8f;
        if (2 < progress && progress < 3) a += (progress - 2) * 0.2f;
        else if (3 <= progress && progress < 4) a += (4 - progress) * 0.2f;
        if (progress == 0 || progress == 6) a = 0;

        CG.alpha = a;
        rectTransform.localScale = Vector3.one * a;
        rectTransform.localPosition = new Vector3(0, (3 - progress) * interval, 0);
    }
}

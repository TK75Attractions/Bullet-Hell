using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using System.Threading.Tasks;
using System;
using UnityEngine.Rendering.UI;

public class StageBox : MonoBehaviour
{
    private static readonly Vector2[] positions = new Vector2[]
    {
        new Vector2(0, 200),
        new Vector2(0, 0),
        new Vector2(0, -200)
    };

    private static readonly Vector2 dis = new Vector2(100, 0);
    private int index;

    public int stageId;
    public bool isReady;
    private Transform trans;
    private RectTransform rectTrans;
    private CanvasGroup CG;
    private StageData stageData;
    private Image StageImage;
    private TMP_Text StageNameTxt;
    private TMP_Text StageDescTxt;

    public async void Init(int _index, int _stageId, bool appear = true)
    {
        index = _index;
        trans = transform;
        rectTrans = GetComponent<RectTransform>();
        CG = GetComponent<CanvasGroup>();
        CG.alpha = 0;
        StageImage = trans.Find("StageImage").GetComponent<Image>();
        StageNameTxt = trans.Find("StageName").GetComponent<TMP_Text>();
        StageDescTxt = trans.Find("Description").GetComponent<TMP_Text>();
        rectTrans.localPosition = positions[_index];
        isReady = false;

        if (appear)
        {
            SetData(_stageId);

            float duration = 0.5f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                rectTrans.localPosition = positions[index] + dis * (1 - t);
                rectTrans.localScale = new Vector3(0.8f + t * 0.2f, 0.8f + t * 0.2f, 1);
                CG.alpha = t;
                await Task.Yield();
            }

            rectTrans.localPosition = positions[index];
            rectTrans.localScale = Vector3.one;
            CG.alpha = 1;
            isReady = true;
        }
    }

    public void SetData(int _stageId)
    {
        stageId = _stageId;
        stageData = GManager.Control.SDB.stages[stageId];
        StageImage.sprite = stageData.StageImage;
        StageNameTxt.text = stageData.stageName;
        StageDescTxt.text = stageData.stageDescription;
    }

    public void SetState(float t)
    {
        rectTrans.localPosition = positions[index] + dis * t;
        rectTrans.localScale = new Vector3(0.8f + (1 - t) * 0.2f, 0.8f + (1 - t) * 0.2f, 1);
        CG.alpha = 1 - t;
    }

    public async void Set(int _stageId)
    {
        isReady = false;
        Disappear();
        while (CG.alpha > 0) await Task.Yield();
        Appear(_stageId);
    }

    public async void Appear(int _stageid, float delay)
    {
        await Task.Delay(TimeSpan.FromSeconds(delay));
        Appear(_stageid);
    }

    public async void Appear(int _stageId)
    {
        CG.alpha = 0;
        stageId = _stageId;
        stageData = GManager.Control.SDB.stages[stageId];
        StageImage.sprite = stageData.StageImage;
        StageNameTxt.text = stageData.stageName;
        StageDescTxt.text = stageData.stageDescription;

        float duration = 0.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            rectTrans.localPosition = positions[index] + dis * (1 - t);
            rectTrans.localScale = new Vector3(0.8f + t * 0.2f, 0.8f + t * 0.2f, 1);
            CG.alpha = t;
            await Task.Yield();
        }

        rectTrans.localPosition = positions[index];
        rectTrans.localScale = Vector3.one;
        CG.alpha = 1;
        isReady = true;
    }

    public async void Disappear()
    {
        float duration = 0.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            rectTrans.localPosition = positions[index] + dis * t;
            rectTrans.localScale = new Vector3(1 - t * 0.2f, 1 - t * 0.2f, 1);
            CG.alpha = 1 - t;
            await Task.Yield();
        }

        rectTrans.localPosition = positions[index] + dis;
        rectTrans.localScale = new Vector3(0.8f, 0.8f, 1);
        CG.alpha = 0;
    }
}

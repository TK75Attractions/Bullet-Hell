using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DefficultyBar : MonoBehaviour
{
    private CanvasGroup CG;
    private RectTransform whiteBar;
    private CanvasGroup whiteCG;

    private DefficultyBox[] boxes = new DefficultyBox[3];
    public int index = 0;
    private readonly float duration = 0.15f;
    private bool isTransitioning = false;

    private class DefficultyBox
    {
        public CanvasGroup CG;
        public RectTransform rectTransform;
        public DefficultyBox(Transform trans, Vector2 pos, string name, Color color)
        {
            CG = trans.GetComponent<CanvasGroup>();
            rectTransform = trans.GetComponent<RectTransform>();
            trans.Find("StageBar").GetComponent<Image>().color = color;
            trans.Find("StageName").GetComponent<TMP_Text>().text = name;
        }

        public void SetPosition(float progress)
        {
            CG.alpha = 0.4f + 0.6f * progress;
            rectTransform.localScale = Vector3.one * (0.8f + 0.2f * progress);
        }
    }

    public void Init()
    {
        Transform trans = transform.Find("List");
        boxes[0] = new DefficultyBox(trans.Find("Easy"), new Vector2(0, 0), "EASY", new Color(0f, 1f, 0.227f));
        boxes[1] = new DefficultyBox(trans.Find("Normal"), new Vector2(0, -140), "NORMAL", new Color(0.165f, 0.592f, 1f));
        boxes[2] = new DefficultyBox(trans.Find("Lunatic"), new Vector2(0, -280), "LUNATIC", new Color(1f, 0.447f, 0.502f));
        CG = GetComponent<CanvasGroup>();
        CG.alpha = 0;
        index = 0;
        for (int i = 0; i < boxes.Length; i++)
        {
            if (i == index) boxes[i].SetPosition(1);
            else boxes[i].SetPosition(0);
        }

        whiteBar = transform.Find("White").GetComponent<RectTransform>();
        whiteCG = whiteBar.GetComponent<CanvasGroup>();
        whiteCG.alpha = 1;
        whiteBar.anchoredPosition = new Vector2(0, 200);
    }

    public async void Up()
    {
        if (isTransitioning || index <= 0) return;
        isTransitioning = true;
        index--;
        float d = duration;

        while (d > 0)
        {
            d -= Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(d / duration);
            float progress = -t * (t - 2);
            if (progress > 1) progress = 1;

            for (int i = 0; i < boxes.Length; i++)
            {
                if (i == index) boxes[i].SetPosition(1 - progress);
                else if (i == index + 1) boxes[i].SetPosition(progress);
                SetWhite(index, index + 1, progress);
                Debug.Log($"index: {index}, progress: {progress}");
            }
            await Task.Yield();
        }

        for (int i = 0; i < boxes.Length; i++)
        {
            if (i == index) boxes[i].SetPosition(1);
            else if (i == index + 1) boxes[i].SetPosition(0);
        }
        SetWhite(index, index + 1, 0);
        isTransitioning = false;
    }

    public async void Down()
    {
        if (isTransitioning || index >= boxes.Length - 1) return;
        isTransitioning = true;
        index++;
        float d = duration;

        while (d > 0)
        {
            d -= Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(d / duration);
            float progress = -t * (t - 2);
            if (progress > 1) progress = 1;

            for (int i = 0; i < boxes.Length; i++)
            {
                if (i == index) boxes[i].SetPosition(1 - progress);
                else if (i == index - 1) boxes[i].SetPosition(progress);
                SetWhite(index, index - 1, progress);
            }
            await Task.Yield();
        }

        for (int i = 0; i < boxes.Length; i++)
        {
            if (i == index) boxes[i].SetPosition(1);
            else if (i == index - 1) boxes[i].SetPosition(0);
        }
        SetWhite(index, index - 1, 0);

        isTransitioning = false;
    }

    public void SetAlpha(float alpha)
    {
        CG.alpha = alpha;
    }

    private void SetWhite(int index, int pre, float progress)
    {
        if (progress > 0.5)
        {
            whiteCG.alpha = (progress - 0.5f) * 2;
            whiteBar.anchoredPosition = new Vector2(0, 200 - (pre * 200));
        }
        else
        {
            whiteCG.alpha = (0.5f - progress) * 2;
            whiteBar.anchoredPosition = new Vector2(0, 200 - (index * 200));
        }
    }
}
using UnityEngine;
using Unity.Mathematics;
using TMPro;
using UnityEngine.Video;
using System.Data;
using System;

public class StageDescription : MonoBehaviour
{
    private RectTransform rect;
    private RectTransform backRect;
    [SerializeField] private TransformData BackData;
    private RectTransform videoRect;
    [SerializeField] private TransformData VideoData;
    private RectTransform LineUpLeft;
    private RectTransform LineDownRight;

    private TMP_Text stageName = null;
    private VideoPlayer videoPlayer = null;

    [Serializable]
    private struct TransformData
    {
        public float mWidth;
        public float mHeight;
        public float dWidth;
        public float dHeight;
    }

    public void Init()
    {
        rect = GetComponent<RectTransform>();
        backRect = transform.Find("Back").GetComponent<RectTransform>();
        videoRect = transform.Find("Video").GetComponent<RectTransform>();
        LineUpLeft = transform.Find("UpLeft").GetComponent<RectTransform>();
        LineDownRight = transform.Find("DownRight").GetComponent<RectTransform>();
        stageName = transform.Find("StageName").GetComponent<TMP_Text>();
        videoPlayer = transform.Find("VideoPlayer").GetComponent<VideoPlayer>();
    }

    public void Set(int index)
    {
        StageData data = GManager.Control.SDB.GetStage(index);
        videoPlayer.clip = data.videoClip;
        stageName.text = data.stageName;
    }

    public void Transition(float progress)
    {
        rect.anchoredPosition = new Vector2(Mathf.Lerp(560, -440, progress), -60);
        backRect.sizeDelta = new Vector2(Mathf.Lerp(BackData.mWidth, BackData.dWidth, progress), Mathf.Lerp(BackData.mHeight, BackData.dHeight, progress));
        videoRect.sizeDelta = new Vector2(Mathf.Lerp(VideoData.mWidth, VideoData.dWidth, progress), Mathf.Lerp(VideoData.mHeight, VideoData.dHeight, progress));
        LineUpLeft.anchoredPosition = new Vector2(-Mathf.Lerp(BackData.mWidth, BackData.dWidth, progress) / 2, Mathf.Lerp(BackData.mHeight, BackData.dHeight, progress) / 2);
        LineDownRight.anchoredPosition = new Vector2(Mathf.Lerp(BackData.mWidth, BackData.dWidth, progress) / 2, -Mathf.Lerp(BackData.mHeight, BackData.dHeight, progress) / 2);
    }
}

using UnityEngine;
using Unity.Mathematics;
using TMPro;
using UnityEngine.Video;
using System.Data;
using System;

public class StageDescription : MonoBehaviour
{
    private RectTransform backRect;
    [SerializeField] private TransformData BackData;
    private RectTransform videoRect;
    [SerializeField] private TransformData VideoData;
    private RectTransform LineUpLeft;
    [SerializeField] private TransformData UpLeftData;
    private RectTransform LineDownRight;
    [SerializeField] private TransformData DownRightData;

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
        backRect.sizeDelta = new Vector2(Mathf.Lerp(BackData.mWidth, BackData.dWidth, progress), Mathf.Lerp(BackData.mHeight, BackData.dHeight, progress));
        videoRect.sizeDelta = new Vector2(Mathf.Lerp(VideoData.mWidth, VideoData.dWidth, progress), Mathf.Lerp(VideoData.mHeight, VideoData.dHeight, progress));
        LineUpLeft.sizeDelta = new Vector2(Mathf.Lerp(UpLeftData.mWidth, UpLeftData.dWidth, progress), Mathf.Lerp(UpLeftData.mHeight, UpLeftData.dHeight, progress));
        LineDownRight.sizeDelta = new Vector2(Mathf.Lerp(DownRightData.mWidth, DownRightData.dWidth, progress), Mathf.Lerp(DownRightData.mHeight, DownRightData.dHeight, progress));
    }
}

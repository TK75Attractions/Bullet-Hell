using UnityEngine;
using Unity.Mathematics;
using TMPro;
using UnityEngine.Video;
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
    private TMP_Text creatorText = null;
    private TMP_Text metaText = null;
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
        Transform creator = transform.Find("Creator");
        if (creator != null) creatorText = creator.GetComponent<TMP_Text>();
        Transform meta = transform.Find("Meta");
        if (meta != null) metaText = meta.GetComponent<TMP_Text>();
    }

    public void Set(int index)
    {
        StageData data = GManager.Control.SDB.GetStage(index);
        if (data == null)
        {
            return;
        }

        videoPlayer.Stop();
        videoPlayer.clip = null;
        videoPlayer.url = string.Empty;

        if (data.videoClip != null)
        {
            videoPlayer.source = VideoSource.VideoClip;
            videoPlayer.clip = data.videoClip;
        }
        else if (!string.IsNullOrWhiteSpace(data.videoPath))
        {
            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = new Uri(System.IO.Path.GetFullPath(data.videoPath)).AbsoluteUri;
        }

        stageName.text = data.stageName;

        if (creatorText != null)
        {
            creatorText.text = string.IsNullOrWhiteSpace(data.stageDescription) ? string.Empty : data.stageDescription;
        }

        if (metaText != null)
        {
            if (data.audioClip != null)
            {
                int len = (int)data.audioClip.length;
                metaText.text = $"プレイ時間  {len / 60}:{len % 60:00}";
            }
            else
            {
                metaText.text = string.Empty;
            }
        }
    }

    public void Transition(float progress)
    {
        rect.anchoredPosition = new Vector2(Mathf.Lerp(560, -440, progress), -60);
        backRect.sizeDelta = new Vector2(Mathf.Lerp(BackData.mWidth, BackData.dWidth, progress), Mathf.Lerp(BackData.mHeight, BackData.dHeight, progress));
        videoRect.sizeDelta = new Vector2(Mathf.Lerp(VideoData.mWidth, VideoData.dWidth, progress), Mathf.Lerp(VideoData.mHeight, VideoData.dHeight, progress));
        LineUpLeft.anchoredPosition = new Vector2(-Mathf.Lerp(BackData.mWidth, BackData.dWidth, progress) / 2, Mathf.Lerp(BackData.mHeight, BackData.dHeight, progress) / 2);
        LineDownRight.anchoredPosition = new Vector2(Mathf.Lerp(BackData.mWidth, BackData.dWidth, progress) / 2, -Mathf.Lerp(BackData.mHeight, BackData.dHeight, progress) / 2);

        // The play-time row only fits the compact music-select layout; fade it out
        // while the card expands for difficulty select.
        if (metaText != null) metaText.alpha = 1f - progress;
    }
}

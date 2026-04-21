using UnityEngine;
using Unity.Mathematics;
using TMPro;
using UnityEngine.Video;
using System.Data;

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

    private struct TransformData
    {
        public Vector2 mScale;
        public Vector2 mPosition;
        public Vector2 dScale;
        public Vector2 dPosition;
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
        backRect.localScale = Vector3.Lerp(BackData.mScale, BackData.dScale, progress);
        backRect.localPosition = Vector3.Lerp(BackData.mPosition, BackData.dPosition, progress);
        videoRect.localScale = Vector3.Lerp(VideoData.mScale, VideoData.dScale, progress);
        videoRect.localPosition = Vector3.Lerp(VideoData.mPosition, VideoData.dPosition, progress);
        LineUpLeft.localScale = Vector3.Lerp(UpLeftData.mScale, UpLeftData.dScale, progress);
        LineUpLeft.localPosition = Vector3.Lerp(UpLeftData.mPosition, UpLeftData.dPosition, progress);
        LineDownRight.localScale = Vector3.Lerp(DownRightData.mScale, DownRightData.dScale, progress);
        LineDownRight.localPosition = Vector3.Lerp(DownRightData.mPosition, DownRightData.dPosition, progress);
    }
}

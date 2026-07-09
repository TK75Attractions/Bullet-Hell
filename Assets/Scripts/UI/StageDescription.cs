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
    private RectTransform videoFrameRect;
    [SerializeField] private TransformData VideoData;
    private RectTransform LineUpLeft;
    private RectTransform LineDownRight;

    private TMP_Text stageName = null;
    private TMP_Text creatorText = null;
    private TMP_Text metaText = null;
    private VideoPlayer videoPlayer = null;

    private RectTransform notesIconRect;
    private RectTransform stageNameRect;
    private RectTransform diamondRect;
    private RectTransform creatorRect;
    private float stageNameLayoutX;
    private float creatorLayoutX;

    // Vertical layout of the header block inside the card. The video grows when
    // the card expands for difficulty select (top edge +120 -> +170), so the
    // texts above it shift up in step to keep clear of it.
    // Header block is compact (small icon/title) so the description line gets
    // room for up to two wrapped lines above the video.
    private static readonly Vector2 notesIconY = new Vector2(352f, 398f);
    private static readonly Vector2 stageNameY = new Vector2(288f, 338f);
    private static readonly Vector2 diamondY = new Vector2(242f, 292f);
    private static readonly Vector2 creatorY = new Vector2(180f, 230f);

    // Difficulty-select layout: a narrower card with a single compact header
    // row and even margins around the preview.
    private const float difficultyRootX = -450f;
    private const float difficultyCardWidth = 860f;
    private const float difficultyCardHeight = 800f;
    private const float difficultyVideoWidth = 680f;
    private const float difficultyVideoHeight = 555f;
    private static readonly Vector2 difficultyNotesPosition = new Vector2(-350f, 330f);
    private static readonly Vector2 difficultyNamePosition = new Vector2(-40f, 330f);
    private static readonly Vector2 difficultyDiamondPosition = new Vector2(-350f, 260f);
    private static readonly Vector2 difficultyCreatorPosition = new Vector2(-30f, 260f);
    private static readonly Vector2 difficultyVideoPosition = new Vector2(0f, -78f);
    private static readonly Vector2 musicNotesPosition = new Vector2(-255f, 340f);
    private static readonly Vector2 musicNamePosition = new Vector2(10f, 340f);
    private static readonly Vector2 musicDiamondPosition = new Vector2(-255f, 260f);
    private static readonly Vector2 musicCreatorPosition = new Vector2(20f, 260f);
    private static readonly Vector2 musicVideoPosition = new Vector2(0f, -55f);

    private float refreshAnimT = 1f;

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
        videoPlayer.aspectRatio = VideoAspectRatio.FitInside;
        Transform creator = transform.Find("Creator");
        if (creator != null) creatorText = creator.GetComponent<TMP_Text>();
        Transform videoFrame = transform.Find("VideoFrame");
        if (videoFrame != null) videoFrameRect = videoFrame.GetComponent<RectTransform>();

        notesIconRect = transform.Find("Notes") as RectTransform;
        stageNameRect = stageName.GetComponent<RectTransform>();
        Transform diamond = transform.Find("Diamond");
        if (diamond != null) diamondRect = diamond.GetComponent<RectTransform>();
        if (creatorText != null) creatorRect = creatorText.GetComponent<RectTransform>();
        Transform meta = transform.Find("Meta");
        if (meta != null) metaText = meta.GetComponent<TMP_Text>();
        Transition(0f);
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
                // TODO: 難易度レーティングは StageData にデータが無いためプレースホルダー表示。
                metaText.text = $"難易度 <b>A</b>  ・  プレイ時間 {len / 60}:{len % 60:00}";
            }
            else
            {
                metaText.text = string.Empty;
            }
        }

        refreshAnimT = 0f;
    }

    // Quick fade/slide-in of the card texts whenever a new song is shown.
    public void Tick(float dt)
    {
        if (refreshAnimT >= 1f) return;
        refreshAnimT = Mathf.Min(1f, refreshAnimT + dt / 0.18f);
        float ease = 1f - (1f - refreshAnimT) * (1f - refreshAnimT);
        float offset = 8f * (1f - ease);
        stageName.alpha = ease;
        if (stageNameRect != null) stageNameRect.anchoredPosition = new Vector2(stageNameLayoutX + offset, stageNameRect.anchoredPosition.y);
        if (creatorText != null)
        {
            creatorText.alpha = ease;
            if (creatorRect != null) creatorRect.anchoredPosition = new Vector2(creatorLayoutX + offset, creatorRect.anchoredPosition.y);
        }
        if (metaText != null) metaText.alpha = ease;
    }

    public void Transition(float progress)
    {
        rect.anchoredPosition = new Vector2(Mathf.Lerp(560f, difficultyRootX, progress), -60f);
        float cardWidth = Mathf.Lerp(BackData.mWidth, difficultyCardWidth, progress);
        float cardHeight = Mathf.Lerp(BackData.mHeight, difficultyCardHeight, progress);
        backRect.sizeDelta = new Vector2(cardWidth, cardHeight);
        videoRect.sizeDelta = new Vector2(
            Mathf.Lerp(VideoData.mWidth, difficultyVideoWidth, progress),
            Mathf.Lerp(VideoData.mHeight, difficultyVideoHeight, progress));
        videoRect.anchoredPosition = Vector2.Lerp(musicVideoPosition, difficultyVideoPosition, progress);
        if (videoFrameRect != null) videoFrameRect.sizeDelta = videoRect.sizeDelta + new Vector2(8, 8);
        if (videoFrameRect != null) videoFrameRect.anchoredPosition = videoRect.anchoredPosition;

        SetPosition(notesIconRect, Vector2.Lerp(musicNotesPosition, difficultyNotesPosition, progress));
        Vector2 namePosition = Vector2.Lerp(musicNamePosition, difficultyNamePosition, progress);
        Vector2 creatorPosition = Vector2.Lerp(musicCreatorPosition, difficultyCreatorPosition, progress);
        stageNameLayoutX = namePosition.x;
        creatorLayoutX = creatorPosition.x;
        SetPosition(stageNameRect, namePosition);
        SetPosition(diamondRect, Vector2.Lerp(musicDiamondPosition, difficultyDiamondPosition, progress));
        SetPosition(creatorRect, creatorPosition);
        if (stageNameRect != null) stageNameRect.sizeDelta = Vector2.Lerp(new Vector2(470f, 56f), new Vector2(560f, 58f), progress);
        if (creatorRect != null) creatorRect.sizeDelta = Vector2.Lerp(new Vector2(480f, 48f), new Vector2(560f, 48f), progress);
        stageName.alignment = TextAlignmentOptions.MidlineLeft;
        if (creatorText != null) creatorText.alignment = TextAlignmentOptions.MidlineLeft;
        LineUpLeft.anchoredPosition = new Vector2(-cardWidth / 2f, cardHeight / 2f);
        LineDownRight.anchoredPosition = new Vector2(cardWidth / 2f, -cardHeight / 2f);

        // The play-time row only fits the compact music-select layout; fade it out
        // while the card expands for difficulty select.
        if (metaText != null) metaText.alpha = 1f - progress;
    }

    private static void SetY(RectTransform rect, float y)
    {
        if (rect == null) return;
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
    }

    private static void SetPosition(RectTransform rect, Vector2 position)
    {
        if (rect == null) return;
        rect.anchoredPosition = position;
    }
}

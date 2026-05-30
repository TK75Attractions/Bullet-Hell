using UnityEngine;
using TMPro;
using UnityEngine.Video;
using System;
using UnityEngine.UI;

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
    private TMP_Text descriptionText = null;
    private CanvasGroup canvasGroup = null;
    private Image backImage = null;
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
        backImage = backRect.GetComponent<Image>();
        videoRect = transform.Find("Video").GetComponent<RectTransform>();
        LineUpLeft = transform.Find("UpLeft").GetComponent<RectTransform>();
        LineDownRight = transform.Find("DownRight").GetComponent<RectTransform>();
        stageName = transform.Find("StageName").GetComponent<TMP_Text>();
        videoPlayer = transform.Find("VideoPlayer").GetComponent<VideoPlayer>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        ConfigureMainText();
        creatorText = CreateOrGetText("CreatorText", new Vector2(0f, 245f), new Vector2(560f, 52f), 34f, TextAlignmentOptions.Center);
        descriptionText = CreateOrGetText("DescriptionText", new Vector2(0f, -430f), new Vector2(700f, 52f), 28f, TextAlignmentOptions.Center);
    }

    public void Set(int index)
    {
        StageData data = GManager.Control.SDB.GetStage(index);
        if (data == null) return;
        videoPlayer.clip = data.videoClip;
        stageName.text = string.IsNullOrEmpty(data.stageName) ? $"Stage {index + 1}" : data.stageName;
        creatorText.text = string.IsNullOrEmpty(data.stageDescription) ? "Unknown Creator" : data.stageDescription;
        descriptionText.text = $"難易度 {index + 1} / プレイ時間 --:--";
        if (videoPlayer.clip != null)
        {
            videoPlayer.isLooping = true;
            videoPlayer.Play();
        }
    }

    public void Transition(float progress)
    {
        progress = Mathf.Clamp01(progress);
        float eased = Mathf.SmoothStep(0f, 1f, progress);
        float width = Mathf.Lerp(BackData.mWidth, BackData.dWidth, eased);
        float height = Mathf.Lerp(BackData.mHeight, BackData.dHeight, eased);
        float videoWidth = Mathf.Lerp(VideoData.mWidth, VideoData.dWidth, eased);
        float videoHeight = Mathf.Lerp(VideoData.mHeight, VideoData.dHeight, eased);

        rect.anchoredPosition = new Vector2(Mathf.Lerp(560f, -500f, eased), Mathf.Lerp(-60f, -86f, eased));
        rect.localScale = Vector3.one * Mathf.Lerp(0.98f, 1.02f, Mathf.Sin(eased * Mathf.PI));
        backRect.sizeDelta = new Vector2(width, height);
        videoRect.sizeDelta = new Vector2(videoWidth, videoHeight);
        videoRect.anchoredPosition = new Vector2(0f, Mathf.Lerp(-100f, -120f, eased));
        LineUpLeft.anchoredPosition = new Vector2(-width / 2f, height / 2f);
        LineDownRight.anchoredPosition = new Vector2(width / 2f, -height / 2f);
        creatorText.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Lerp(220f, 255f, eased));
        descriptionText.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Lerp(-360f, -435f, eased));

        if (backImage != null)
        {
            backImage.color = Color.Lerp(new Color(0.0f, 0.16f, 0.24f, 0.9f), new Color(0.0f, 0.2f, 0.3f, 0.96f), eased);
        }
    }

    public void SetAlpha(float alpha)
    {
        canvasGroup.alpha = Mathf.Clamp01(alpha);
    }

    private void ConfigureMainText()
    {
        RectTransform stageNameRect = stageName.GetComponent<RectTransform>();
        stageNameRect.anchoredPosition = new Vector2(0f, 315f);
        stageNameRect.sizeDelta = new Vector2(720f, 92f);
        stageName.alignment = TextAlignmentOptions.Center;
        stageName.textWrappingMode = TextWrappingModes.NoWrap;
        stageName.overflowMode = TextOverflowModes.Ellipsis;
        stageName.fontSize = 60f;
    }

    private TMP_Text CreateOrGetText(string objectName, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment)
    {
        Transform child = transform.Find(objectName);
        TMP_Text text;
        if (child == null)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(transform, false);
            text = textObject.GetComponent<TMP_Text>();
            text.font = stageName.font;
            text.material = stageName.fontMaterial;
        }
        else
        {
            text = child.GetComponent<TMP_Text>();
        }

        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchoredPosition = position;
        textRect.sizeDelta = size;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = new Color(0.38f, 0.72f, 1f, 1f);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }
}

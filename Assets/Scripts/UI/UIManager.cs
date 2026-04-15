using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject TitlePanel;
    [SerializeField] private GameObject ChooseStagePanel;
    [SerializeField] private Transform StageBoxParent;
    [SerializeField] private GameObject StageBoxPrefab;

    [Header("Title Visual")]
    [SerializeField] private Sprite logoSprite;
    [SerializeField] private Color titleBackgroundColor = new Color(0.02f, 0.035f, 0.085f, 1f);
    [SerializeField] private Color promptColor = new Color(0.88f, 0.94f, 1f, 1f);
    [SerializeField] private float bpm = 118f;
    [SerializeField] private float logoBounceAmount = 0.16f;
    [SerializeField] private float flashAlpha = 0.16f;
    [SerializeField] private int shapeCount = 18;
    [SerializeField] private Vector2 shapeSizeRange = new Vector2(90f, 190f);
    [SerializeField] private Vector2 shapeSpeedRange = new Vector2(24f, 70f);

    private readonly StageBox[] StageBoxes = new StageBox[3];
    private readonly List<FloatingShape> floatingShapes = new List<FloatingShape>();
    private static readonly string[] shapeGlyphs = { "\u25A0", "\u25B2", "\u25C6" };

    private int currentStageId;
    private bool isInChooseStage;
    private bool isTransitioning;
    private bool titleVisualReady;

    private RectTransform titleRect;
    private Image titlePanelImage;
    private TMP_Text titleText;
    private TMP_Text promptText;
    private RectTransform logoRect;
    private Image logoImage;
    private Image flashImage;
    private RectTransform shapeLayerRect;

    private float titleTime;
    private float beatTimer;
    private float beatPulse;
    private float flashPulse;

    private class FloatingShape
    {
        public RectTransform rect;
        public float speed;
        public float driftAmplitude;
        public float driftFrequency;
        public float phase;
        public float rotationSpeed;
    }

    public void Init()
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);

        currentStageId = 0;
        isInChooseStage = false;
        isTransitioning = false;

        if (TitlePanel != null) TitlePanel.SetActive(true);
        if (ChooseStagePanel != null) ChooseStagePanel.SetActive(false);

        SetupTitleVisual();
        ResetTitleAnimationState();
    }

    public void UpdateUI()
    {
        float dt = Time.deltaTime;
        if (!isInChooseStage)
        {
            UpdateTitleVisual(dt);
        }

        if (GManager.Control.IManager.buttonPressedThisFrame && !isInChooseStage)
        {
            GoToChooseStage();
        }

        if (!isInChooseStage) return;

        if (GManager.Control.IManager.upPressedThisFrame)
        {
            SetStageBox(false);
        }
        else if (GManager.Control.IManager.downPressedThisFrame)
        {
            SetStageBox(true);
        }
    }

    private void SetupTitleVisual()
    {
        if (TitlePanel == null) return;

        titleRect = TitlePanel.GetComponent<RectTransform>();
        titlePanelImage = TitlePanel.GetComponent<Image>();
        if (titlePanelImage != null)
        {
            if (logoSprite == null && titlePanelImage.sprite != null)
            {
                logoSprite = titlePanelImage.sprite;
            }

            titlePanelImage.sprite = null;
            titlePanelImage.color = titleBackgroundColor;
            titlePanelImage.raycastTarget = false;
        }

        Transform titleTransform = TitlePanel.transform.Find("Title");
        if (titleTransform != null)
        {
            titleText = titleTransform.GetComponent<TMP_Text>();
            if (titleText != null)
            {
                titleText.text = "JUST SHAPES & BEATS";
                titleText.color = new Color(0.95f, 0.98f, 1f, 0.95f);
                titleText.fontStyle = FontStyles.Bold;
                titleText.fontSize = 132f;
                titleText.alignment = TextAlignmentOptions.Center;

                RectTransform rt = titleText.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, 160f);
                rt.sizeDelta = new Vector2(1600f, 300f);
            }
        }

        Transform promptTransform = TitlePanel.transform.Find("Text");
        if (promptTransform != null)
        {
            promptText = promptTransform.GetComponent<TMP_Text>();
            if (promptText != null)
            {
                promptText.text = "\u30DC\u30BF\u30F3\u3092\u62BC\u3057\u3066\u30B9\u30BF\u30FC\u30C8";
                promptText.color = promptColor;
                promptText.fontSize = 52f;
                promptText.alignment = TextAlignmentOptions.Center;

                RectTransform rt = promptText.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, -360f);
                rt.sizeDelta = new Vector2(1500f, 120f);
            }
        }

        EnsureShapeLayer();
        BuildShapes();
        EnsureLogo();
        EnsureFlashOverlay();

        if (shapeLayerRect != null) shapeLayerRect.SetAsFirstSibling();
        if (flashImage != null) flashImage.transform.SetAsLastSibling();

        titleVisualReady = true;
    }

    private void ResetTitleAnimationState()
    {
        titleTime = 0f;
        beatTimer = 0f;
        beatPulse = 0f;
        flashPulse = 0f;
    }

    private void EnsureShapeLayer()
    {
        if (TitlePanel == null) return;

        Transform found = TitlePanel.transform.Find("ShapeLayer");
        if (found == null)
        {
            GameObject layer = new GameObject("ShapeLayer", typeof(RectTransform));
            layer.transform.SetParent(TitlePanel.transform, false);
            shapeLayerRect = layer.GetComponent<RectTransform>();
        }
        else
        {
            shapeLayerRect = found as RectTransform;
        }

        if (shapeLayerRect == null) return;
        shapeLayerRect.anchorMin = Vector2.zero;
        shapeLayerRect.anchorMax = Vector2.one;
        shapeLayerRect.offsetMin = Vector2.zero;
        shapeLayerRect.offsetMax = Vector2.zero;
    }

    private void BuildShapes()
    {
        if (shapeLayerRect == null) return;

        for (int i = shapeLayerRect.childCount - 1; i >= 0; i--)
        {
            Destroy(shapeLayerRect.GetChild(i).gameObject);
        }
        floatingShapes.Clear();

        Vector2 panelSize = GetPanelSize();
        float halfWidth = panelSize.x * 0.6f;
        float halfHeight = panelSize.y * 0.6f;
        int count = Mathf.Max(shapeCount, 0);

        for (int i = 0; i < count; i++)
        {
            GameObject shapeObj = new GameObject($"Shape_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            shapeObj.transform.SetParent(shapeLayerRect, false);

            TMP_Text shapeText = shapeObj.GetComponent<TMP_Text>();
            shapeText.text = shapeGlyphs[Random.Range(0, shapeGlyphs.Length)];
            shapeText.fontSize = Random.Range(shapeSizeRange.x, shapeSizeRange.y);
            shapeText.alignment = TextAlignmentOptions.Center;
            shapeText.color = new Color(1f, 1f, 1f, Random.Range(0.07f, 0.18f));
            shapeText.raycastTarget = false;
            shapeText.enableWordWrapping = false;

            RectTransform rt = shapeObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.one * shapeText.fontSize * 1.4f;
            rt.anchoredPosition = new Vector2(
                Random.Range(-halfWidth, halfWidth),
                Random.Range(-halfHeight, halfHeight)
            );
            rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            floatingShapes.Add(new FloatingShape()
            {
                rect = rt,
                speed = Random.Range(shapeSpeedRange.x, shapeSpeedRange.y),
                driftAmplitude = Random.Range(8f, 30f),
                driftFrequency = Random.Range(0.15f, 0.45f),
                phase = Random.Range(0f, Mathf.PI * 2f),
                rotationSpeed = Random.Range(-12f, 12f)
            });
        }
    }

    private void EnsureLogo()
    {
        if (TitlePanel == null) return;

        Transform found = TitlePanel.transform.Find("Logo");
        if (found == null)
        {
            GameObject logoObj = new GameObject("Logo", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            logoObj.transform.SetParent(TitlePanel.transform, false);
            logoRect = logoObj.GetComponent<RectTransform>();
            logoImage = logoObj.GetComponent<Image>();
        }
        else
        {
            logoRect = found as RectTransform;
            logoImage = found.GetComponent<Image>();
        }

        if (logoRect == null || logoImage == null) return;

        logoRect.anchorMin = new Vector2(0.5f, 0.5f);
        logoRect.anchorMax = new Vector2(0.5f, 0.5f);
        logoRect.pivot = new Vector2(0.5f, 0.5f);
        logoRect.anchoredPosition = new Vector2(0f, 140f);
        logoRect.sizeDelta = new Vector2(720f, 420f);
        logoRect.localScale = Vector3.one;

        logoImage.sprite = logoSprite;
        logoImage.color = new Color(1f, 1f, 1f, 0.94f);
        logoImage.preserveAspect = true;
        logoImage.raycastTarget = false;
        logoImage.enabled = logoSprite != null;

        if (titleText != null)
        {
            titleText.gameObject.SetActive(logoSprite == null);
        }
    }

    private void EnsureFlashOverlay()
    {
        if (TitlePanel == null) return;

        Transform found = TitlePanel.transform.Find("FlashOverlay");
        if (found == null)
        {
            GameObject overlay = new GameObject("FlashOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            overlay.transform.SetParent(TitlePanel.transform, false);
            flashImage = overlay.GetComponent<Image>();
        }
        else
        {
            flashImage = found.GetComponent<Image>();
        }

        if (flashImage == null) return;

        RectTransform rt = flashImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        flashImage.color = new Color(1f, 1f, 1f, 0f);
        flashImage.raycastTarget = false;
    }

    private Vector2 GetPanelSize()
    {
        if (titleRect == null) return new Vector2(Screen.width, Screen.height);

        Vector2 size = titleRect.rect.size;
        if (size.x < 10f || size.y < 10f)
        {
            size = new Vector2(Screen.width, Screen.height);
        }

        return size;
    }

    private void UpdateTitleVisual(float dt)
    {
        if (!titleVisualReady || TitlePanel == null || !TitlePanel.activeInHierarchy) return;

        titleTime += dt;

        float beatInterval = 60f / Mathf.Max(1f, bpm);
        beatTimer += dt;
        while (beatTimer >= beatInterval)
        {
            beatTimer -= beatInterval;
            beatPulse = 1f;
            flashPulse = 1f;
        }

        beatPulse = Mathf.MoveTowards(beatPulse, 0f, dt * 4.2f);
        flashPulse = Mathf.MoveTowards(flashPulse, 0f, dt * 5.8f);

        float wobble = 1f + Mathf.Sin(titleTime * 5f) * 0.015f;
        float scale = wobble + beatPulse * logoBounceAmount;

        if (logoImage != null && logoImage.enabled && logoRect != null)
        {
            logoRect.localScale = new Vector3(scale, scale, 1f);
        }
        else if (titleText != null && titleText.gameObject.activeSelf)
        {
            titleText.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        if (promptText != null)
        {
            float blink = 0.45f + 0.55f * (0.5f + 0.5f * Mathf.Sin(titleTime * 3.5f));
            Color c = promptColor;
            c.a = blink;
            promptText.color = c;
        }

        if (flashImage != null)
        {
            Color c = flashImage.color;
            c.a = flashAlpha * flashPulse * flashPulse;
            flashImage.color = c;
        }

        UpdateShapes(dt);
    }

    private void UpdateShapes(float dt)
    {
        if (floatingShapes.Count == 0) return;

        Vector2 panelSize = GetPanelSize();
        float halfWidth = panelSize.x * 0.65f;
        float halfHeight = panelSize.y * 0.65f;

        for (int i = 0; i < floatingShapes.Count; i++)
        {
            FloatingShape shape = floatingShapes[i];
            if (shape.rect == null) continue;

            Vector2 p = shape.rect.anchoredPosition;
            p.y -= shape.speed * dt;
            p.x += Mathf.Sin((titleTime + shape.phase) * Mathf.PI * 2f * shape.driftFrequency) * shape.driftAmplitude * dt;

            if (p.y < -halfHeight - 180f)
            {
                p.y = halfHeight + 180f;
                p.x = Random.Range(-halfWidth, halfWidth);
            }

            shape.rect.anchoredPosition = p;
            shape.rect.Rotate(0f, 0f, shape.rotationSpeed * dt);
        }
    }

    public async void GoToChooseStage()
    {
        if (isTransitioning || isInChooseStage) return;
        if (TitlePanel == null || ChooseStagePanel == null || StageBoxParent == null || StageBoxPrefab == null) return;

        TitlePanel.SetActive(false);
        ChooseStagePanel.SetActive(true);
        isInChooseStage = true;
        isTransitioning = true;
        currentStageId = 0;

        foreach (Transform child in StageBoxParent)
        {
            Destroy(child.gameObject);
        }

        StageBoxes[0] = Instantiate(StageBoxPrefab, StageBoxParent).GetComponent<StageBox>();
        StageBoxes[0].Init(0, 0, false);
        StageBoxes[1] = Instantiate(StageBoxPrefab, StageBoxParent).GetComponent<StageBox>();
        StageBoxes[1].Init(1, currentStageId, true);
        StageBoxes[2] = Instantiate(StageBoxPrefab, StageBoxParent).GetComponent<StageBox>();
        StageBoxes[2].Init(2, currentStageId + 1, true);

        while (!StageBoxes[1].isReady || !StageBoxes[2].isReady)
        {
            await Task.Yield();
        }

        isTransitioning = false;
    }

    private async void SetStageBox(bool down)
    {
        if (!isInChooseStage || isTransitioning) return;

        if (down)
        {
            if (currentStageId == GManager.Control.SDB.stages.Count - 1) return;
            isTransitioning = true;

            List<int> indexes = currentStageId == 0
                ? new List<int> { 1, 2 }
                : new List<int> { 0, 1, 2 };

            float duration = 0.2f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                foreach (int i in indexes) StageBoxes[i].SetState(t);
                await Task.Yield();
            }

            currentStageId++;
            indexes = currentStageId == GManager.Control.SDB.stages.Count - 1
                ? new List<int> { 0, 1 }
                : new List<int> { 0, 1, 2 };

            foreach (int i in indexes) StageBoxes[i].SetData(currentStageId + i - 1);

            elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                foreach (int i in indexes) StageBoxes[i].SetState(1 - t);
                await Task.Yield();
            }

            isTransitioning = false;
        }
        else
        {
            if (currentStageId == 0) return;
            isTransitioning = true;

            List<int> indexes = currentStageId == GManager.Control.SDB.stages.Count - 1
                ? new List<int> { 0, 1 }
                : new List<int> { 0, 1, 2 };

            float duration = 0.2f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                foreach (int i in indexes) StageBoxes[i].SetState(t);
                await Task.Yield();
            }

            currentStageId--;
            indexes = currentStageId == 0
                ? new List<int> { 1, 2 }
                : new List<int> { 0, 1, 2 };

            foreach (int i in indexes) StageBoxes[i].SetData(currentStageId + i - 1);

            elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                foreach (int i in indexes) StageBoxes[i].SetState(1 - t);
                await Task.Yield();
            }

            isTransitioning = false;
        }
    }
}

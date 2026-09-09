using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;

/// <summary>
/// ステージ選択「城壁の街」スタイル(PlayerPrefs stageSelectStyle = 2)の前景ウィジェット。
///
/// 街そのものは <see cref="CityMapController"/> が専用カメラ→RenderTexture で描く。ここは
/// その RT を貼る最背面の RawImage と、選択中の区画の真上に出る▼＋明朝ラベル、右下の情報
/// パネル(曲名・説明・長さ・プレビュー動画)だけを持つ。
///
/// カルーセル(style 1)の資産はそのまま残してあり、<see cref="JsabStageSelect"/> が
/// スタイルに応じてどちらを見せるか切り替える。上部バー(タイマー)・難易度モーダル・
/// 下部ヒントバーは style 1 のものをそのまま共有する。
/// </summary>
public class CitySelectView : MonoBehaviour
{
    // ---- ▼とラベル(タイトルの部屋と同じ様式) --------------------------------
    private const string MinchoFontResource = "Fonts/ShipporiMincho-Regular SDF";
    private const float MarkerArrowW = 40f;
    private const float MarkerArrowH = 26f;
    private const float MarkerFloatPx = 4f;
    private const float MarkerLabelGap = 46f;
    private const float MarkerLabelH = 46f;
    private const float MarkerLabelFont = 32f;
    private const float MarkerLabelBoxW = 520f;
    private const float MarkerLabelSpacing = 4f;
    private const float MarkerFadeSpeed = 1f / 0.15f;
    private static readonly Color MarkerArrowInk = new Color(0.949f, 0.949f, 0.949f, 1f);
    private static readonly Color MarkerArrowEdge = new Color(0.02f, 0.02f, 0.04f, 0.70f);
    private const float MarkerArrowEdgeScale = 1.30f;
    private const float MarkerArrowEdgeDrop = 1.5f;
    private static readonly Color MarkerLabelInk = new Color(0.976f, 0.961f, 0.918f, 1f);
    private static readonly Color MarkerLabelShadow = new Color(0.01f, 0.01f, 0.02f, 1f);
    private static readonly Vector2[] ShadowDirs =
    {
        new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, 1f), new Vector2(0f, -1f),
        new Vector2(0.71f, 0.71f), new Vector2(-0.71f, 0.71f),
        new Vector2(0.71f, -0.71f), new Vector2(-0.71f, -0.71f),
    };
    private static readonly float[] ShadowRadius = { 3.0f, 1.5f };
    private static readonly float[] ShadowAlpha = { 0.50f, 0.85f };

    // ---- 情報パネル ----------------------------------------------------------
    private const float PanelW = 640f;
    private const float PanelH = 400f;
    private const float PanelInsetX = 96f;   // 19° の斜辺を避ける左右の余白
    private static readonly Color Cyan = new Color(0.22f, 0.76f, 0.878f, 1f);

    private RectTransform root;
    private TMP_FontAsset uiFont;
    private TMP_FontAsset minchoFont;
    private bool minchoTried;

    private RawImage cityView;
    // cityView へ最後に入れたマテリアル(null=既定)。ドット風の色数減衰でだけ使う。
    private Material appliedViewMaterial;
    private RectTransform markerRoot;
    private Image markerArrow;
    private CanvasGroup markerLabelCG;
    private TMP_Text markerLabel;
    private TMP_Text[] markerLabelShadows;
    private bool markerInkCentered;

    private CanvasGroup panelCG;
    private TMP_Text panelName;
    private TMP_Text panelDesc;
    private TMP_Text panelMeta;
    private CanvasGroup previewCG;
    private RawImage previewImage;
    private Image previewFallback;
    private VideoPlayer previewVideo;
    private RenderTexture previewRT;

    private Sprite arrowSprite;
    private readonly System.Collections.Generic.List<Texture2D> ownedTextures = new System.Collections.Generic.List<Texture2D>();
    private readonly System.Collections.Generic.List<Sprite> ownedSprites = new System.Collections.Generic.List<Sprite>();

    private CityMapController map;
    private int district;
    private float markerAlpha;
    private float animTime;
    private string videoUrl;

    public CityMapController Map => map;

    // ---- 生成 ----------------------------------------------------------------

    public static CitySelectView Create(RectTransform parent, TMP_FontAsset font)
    {
        GameObject go = new GameObject("CitySelectView", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        CitySelectView view = go.AddComponent<CitySelectView>();
        view.root = rt;
        view.uiFont = font;
        view.Build();
        return view;
    }

    private void Build()
    {
        // 街の描画役(3D)は Canvas のスケールを受けないようシーンのルートへ置く。
        GameObject rig = new GameObject("CityMapRig");
        map = rig.AddComponent<CityMapController>();
        map.cityPrefab = Resources.Load<GameObject>("CityCG/CityMap_v2");
        map.targetTexture = Resources.Load<RenderTexture>("CityCG/CityMapRT");
        map.rendererIndex = 1;

        // 最背面の表示板。
        GameObject viewGO = new GameObject("CityView", typeof(RectTransform));
        viewGO.transform.SetParent(root, false);
        cityView = viewGO.AddComponent<RawImage>();
        cityView.texture = map.Texture;
        cityView.material = appliedViewMaterial = map.ViewMaterial;
        cityView.raycastTarget = false;
        Stretch(cityView.rectTransform);

        BuildMarker();
        BuildPanel();
    }

    private void BuildMarker()
    {
        arrowSprite = CreateDownTriangleSprite(96, 62);

        GameObject markerObj = new GameObject("DistrictMarker", typeof(RectTransform));
        markerObj.transform.SetParent(root, false);
        markerRoot = (RectTransform)markerObj.transform;
        markerRoot.anchorMin = markerRoot.anchorMax = new Vector2(0.5f, 0.5f);
        markerRoot.pivot = new Vector2(0.5f, 0.5f);
        markerRoot.sizeDelta = Vector2.zero;

        Image edge = NewImage("ArrowEdge", markerRoot, MarkerArrowEdge);
        edge.sprite = arrowSprite;
        edge.rectTransform.sizeDelta = new Vector2(MarkerArrowW * MarkerArrowEdgeScale, MarkerArrowH * MarkerArrowEdgeScale);
        edge.rectTransform.anchoredPosition = new Vector2(0f, -MarkerArrowEdgeDrop);

        markerArrow = NewImage("Arrow", markerRoot, MarkerArrowInk);
        markerArrow.sprite = arrowSprite;
        markerArrow.rectTransform.sizeDelta = new Vector2(MarkerArrowW, MarkerArrowH);

        GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(CanvasGroup));
        labelObj.transform.SetParent(markerRoot, false);
        RectTransform label = (RectTransform)labelObj.transform;
        label.anchorMin = label.anchorMax = new Vector2(0.5f, 0.5f);
        label.pivot = new Vector2(0.5f, 0.5f);
        label.anchoredPosition = new Vector2(0f, MarkerLabelGap);
        label.sizeDelta = new Vector2(MarkerLabelBoxW, MarkerLabelH);
        markerLabelCG = labelObj.GetComponent<CanvasGroup>();
        markerLabelCG.alpha = 0f;
        markerLabelCG.blocksRaycasts = false;

        int n = ShadowDirs.Length * ShadowRadius.Length;
        markerLabelShadows = new TMP_Text[n];
        for (int r = 0; r < ShadowRadius.Length; r++)
        {
            Color sc = MarkerLabelShadow;
            sc.a = ShadowAlpha[r];
            for (int k = 0; k < ShadowDirs.Length; k++)
            {
                TMP_Text shadow = NewText("Shadow" + r + "_" + k, label, "", MarkerLabelFont, sc, TextAlignmentOptions.Center);
                RectTransform sr = (RectTransform)shadow.transform;
                sr.sizeDelta = new Vector2(MarkerLabelBoxW, MarkerLabelH);
                sr.anchoredPosition = ShadowDirs[k] * ShadowRadius[r];
                StyleLabel(shadow);
                markerLabelShadows[r * ShadowDirs.Length + k] = shadow;
            }
        }
        markerLabel = NewText("Text", label, "", MarkerLabelFont, MarkerLabelInk, TextAlignmentOptions.Center);
        ((RectTransform)markerLabel.transform).sizeDelta = new Vector2(MarkerLabelBoxW, MarkerLabelH);
        StyleLabel(markerLabel);
    }

    private void BuildPanel()
    {
        GameObject panelGO = new GameObject("InfoPanel", typeof(RectTransform), typeof(CanvasGroup));
        panelGO.transform.SetParent(root, false);
        RectTransform panel = (RectTransform)panelGO.transform;
        panel.anchorMin = panel.anchorMax = new Vector2(1f, 0f);
        panel.pivot = new Vector2(1f, 0f);
        // 下部ヒントバー(高さ 76)の上へ置く。
        panel.anchoredPosition = new Vector2(-40f, 96f);
        panel.sizeDelta = new Vector2(PanelW, PanelH);
        panelCG = panelGO.GetComponent<CanvasGroup>();
        panelCG.blocksRaycasts = false;

        // 平行四辺形(19°)の統一様式パネル。街(明るい面もある)の上に乗るので、
        // 同じスプライトを暗く着色したものを 1 枚下に敷いて地を締める(文字の可読性)。
        Sprite panelSprite = UiButtonStyle.CreateHudPanelSprite((int)PanelW, (int)PanelH, ownedTextures, ownedSprites, "CityInfoPanel");
        // 1 枚では下の街が透ける(スプライトのフィル alpha が 0.60)ので 2 枚重ねる。
        for (int i = 0; i < 2; i++)
        {
            Image shade = NewImage("PanelShade" + i, panel, new Color(0.012f, 0.024f, 0.045f, 1f));
            Stretch(shade.rectTransform);
            shade.sprite = panelSprite;
        }
        Image bg = NewImage("PanelBg", panel, Color.white);
        Stretch(bg.rectTransform);
        bg.sprite = panelSprite;

        float innerW = PanelW - PanelInsetX * 2f;

        panelName = NewText("Name", panel, "", 34f, Color.white, TextAlignmentOptions.Left);
        RectTransform nr = (RectTransform)panelName.transform;
        nr.anchorMin = nr.anchorMax = new Vector2(0.5f, 1f);
        nr.pivot = new Vector2(0.5f, 1f);
        nr.sizeDelta = new Vector2(innerW, 44f);
        nr.anchoredPosition = new Vector2(0f, -26f);

        panelDesc = NewText("Desc", panel, "", 20f, new Color(0.78f, 0.86f, 0.92f, 1f), TextAlignmentOptions.TopLeft);
        RectTransform dr = (RectTransform)panelDesc.transform;
        dr.anchorMin = dr.anchorMax = new Vector2(0.5f, 1f);
        dr.pivot = new Vector2(0.5f, 1f);
        dr.sizeDelta = new Vector2(innerW, 54f);
        dr.anchoredPosition = new Vector2(0f, -74f);
        panelDesc.textWrappingMode = TextWrappingModes.Normal;

        panelMeta = NewText("Meta", panel, "", 20f, Cyan, TextAlignmentOptions.Left);
        RectTransform mr = (RectTransform)panelMeta.transform;
        mr.anchorMin = mr.anchorMax = new Vector2(0.5f, 0f);
        mr.pivot = new Vector2(0.5f, 0f);
        mr.sizeDelta = new Vector2(innerW, 28f);
        mr.anchoredPosition = new Vector2(0f, 22f);

        // プレビュー動画(区画へ寄り切ってからフェードインする)。
        GameObject prevGO = new GameObject("Preview", typeof(RectTransform), typeof(CanvasGroup));
        prevGO.transform.SetParent(panel, false);
        RectTransform pr = (RectTransform)prevGO.transform;
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0f);
        pr.pivot = new Vector2(0.5f, 0f);
        pr.sizeDelta = new Vector2(384f, 216f);
        pr.anchoredPosition = new Vector2(0f, 58f);
        previewCG = prevGO.GetComponent<CanvasGroup>();
        previewCG.alpha = 0f;
        previewCG.blocksRaycasts = false;

        previewFallback = NewImage("PreviewFallback", pr, new Color(0.02f, 0.05f, 0.09f, 1f));
        Stretch(previewFallback.rectTransform);

        previewRT = new RenderTexture(384, 216, 0) { name = "CityPreviewRT" };
        previewImage = new GameObject("PreviewVideo", typeof(RectTransform)).AddComponent<RawImage>();
        previewImage.transform.SetParent(pr, false);
        previewImage.texture = previewRT;
        previewImage.raycastTarget = false;
        Stretch(previewImage.rectTransform);

        Image[] rim = BuildRim(pr, new Color(0.75f, 0.82f, 0.88f, 0.42f), 2f);
        if (rim != null && rim.Length > 0) { /* 枠は生成しっぱなしで良い */ }

        GameObject vpGO = new GameObject("PreviewVideoPlayer");
        vpGO.transform.SetParent(pr, false);
        previewVideo = vpGO.AddComponent<VideoPlayer>();
        previewVideo.playOnAwake = false;
        previewVideo.source = VideoSource.Url;
        previewVideo.renderMode = VideoRenderMode.RenderTexture;
        previewVideo.targetTexture = previewRT;
        previewVideo.isLooping = true;
        previewVideo.aspectRatio = VideoAspectRatio.FitInside;
        previewVideo.waitForFirstFrame = true;
        previewVideo.audioOutputMode = VideoAudioOutputMode.None;
        previewVideo.skipOnDrop = true;
    }

    // ---- 公開 API ------------------------------------------------------------

    /// <summary>city スタイルの表示/非表示。街のカメラとライトもここで止める。</summary>
    public void SetVisible(bool visible)
    {
        if (root != null) root.gameObject.SetActive(visible);
        if (map != null) map.SetCityActive(visible);
        if (previewVideo != null)
        {
            if (!visible && previewVideo.isPlaying) previewVideo.Pause();
            else if (visible && !string.IsNullOrEmpty(previewVideo.url) && !previewVideo.isPlaying) previewVideo.Play();
        }
    }

    /// <summary>区画割当のある(選択できる)ステージを街へ伝える。</summary>
    public void SetAvailableDistricts(bool[] flags)
    {
        if (map != null) map.SetAvailableDistricts(flags);
    }

    /// <summary>選択中のステージ。district が 0 なら区画未割当。</summary>
    public void SetStage(StageData data, int districtNumber, bool animate)
    {
        district = districtNumber;
        if (map != null) map.SelectDistrict(districtNumber, animate);

        string stageName = data != null && !string.IsNullOrWhiteSpace(data.stageName) ? data.stageName : "";
        if (markerLabel != null) markerLabel.text = stageName;
        if (markerLabelShadows != null)
            foreach (TMP_Text t in markerLabelShadows) if (t != null) t.text = stageName;
        markerInkCentered = false;

        if (panelName != null) panelName.text = stageName;
        if (panelDesc != null)
            panelDesc.text = data != null && !string.IsNullOrWhiteSpace(data.stageDescription) ? data.stageDescription : "";
        if (panelMeta != null)
        {
            if (data != null && data.audioClip != null)
            {
                int len = (int)data.audioClip.length;
                panelMeta.text = string.Format("プレイ時間 {0}:{1:00}", len / 60, len % 60);
            }
            else panelMeta.text = "";
        }
        UpdatePreviewClip(data);
    }

    private void UpdatePreviewClip(StageData data)
    {
        string path = VideoPath(data);
        if (previewVideo == null) return;
        if (path == null)
        {
            videoUrl = null;
            previewVideo.Stop();
            previewVideo.url = string.Empty;
            if (previewImage != null) previewImage.enabled = false;
            return;
        }
        if (videoUrl == path) return;
        videoUrl = path;
        previewVideo.Stop();
        previewVideo.url = path;
        if (previewImage != null) previewImage.enabled = true;
        previewVideo.Play();
    }

    private static string VideoPath(StageData data)
    {
        string dir = data != null ? data.stageDirectoryName : null;
        if (string.IsNullOrEmpty(dir)) return null;
        string path = Path.Combine(Application.dataPath, "StageData", dir, dir + ".mp4");
        return File.Exists(path) ? path : null;
    }

    /// <summary>選択画面へ入ったときの入場。まず街の俯瞰を見せ、少し置いてから
    /// 選択中の区画へ寄る(タイトルのスタート演出から街の全景へ交差フェードするため)。</summary>
    public void PlayEntrance()
    {
        if (map == null) return;
        map.SelectDistrict(0, false);
        if (entranceCo != null) StopCoroutine(entranceCo);
        if (isActiveAndEnabled) entranceCo = StartCoroutine(EntranceRoutine());
    }

    private Coroutine entranceCo;

    private System.Collections.IEnumerator EntranceRoutine()
    {
        float t = 0f;
        while (t < EntranceHold) { t += Time.deltaTime; yield return null; }
        entranceCo = null;
        if (map != null && district >= 1) map.SelectDistrict(district, true);
    }

    // 俯瞰を見せておく時間(秒)。
    private const float EntranceHold = 0.55f;

    /// <summary>決定で区画へさらに寄る / 戻す。</summary>
    public void SetCloseUp(bool on)
    {
        if (map != null) map.SetCloseUp(on);
    }

    public void Tick(float dt)
    {
        if (map == null || !map.CityVisible) return;
        animTime += dt;
        map.Tick(dt);

        // ドット風表示を切り替えると街の描画先 RT ごと差し替わるので、表示板を追従させる。
        // RawImage.material は null を入れると既定マテリアルを返すので、
        // 最後に入れた値を覚えて変化したときだけ差し替える。
        if (cityView != null)
        {
            if (cityView.texture != map.Texture) cityView.texture = map.Texture;
            if (appliedViewMaterial != map.ViewMaterial)
            {
                appliedViewMaterial = map.ViewMaterial;
                cityView.material = appliedViewMaterial;
            }
        }

        // 和文ラベルの光学中央合わせは、メッシュ生成後(表示後の初回)でないと空振る。
        if (!markerInkCentered)
        {
            bool all = markerLabel == null || TmpAlign.CenterInkVertically(markerLabel);
            if (markerLabelShadows != null)
                foreach (TMP_Text t in markerLabelShadows) if (t != null) all &= TmpAlign.CenterInkVertically(t);
            markerInkCentered = all;
        }

        // ▼とラベルを区画の中心の上へ。カメラが動いても毎フレーム投影し直すので追従する。
        Rect canvas = root.rect;
        Vector2 vp = Vector2.zero;
        bool show = district >= 1 && map.TryGetMarkerViewport(district, out vp);
        // 決定で寄り切る直前に消す(難易度モーダルの上に▼を残さない)。
        float zoomFade = 1f - Mathf.Clamp01((map.ZoomAmount - 0.55f) / 0.45f);
        if (show)
        {
            float floatY = Mathf.Sin(animTime * 1.9f) * MarkerFloatPx;
            markerRoot.anchoredPosition = new Vector2(
                (vp.x - 0.5f) * canvas.width,
                (vp.y - 0.5f) * canvas.height + floatY);
        }
        markerAlpha = Mathf.MoveTowards(markerAlpha, show ? 1f : 0f, dt * MarkerFadeSpeed);
        // 消えるときはフェードし切ってから落とす(即切りだと画面外へ出入りする瞬間に
        // ▼がパッと消えて見える)。
        bool alive = show || markerAlpha > 0.001f;
        if (markerRoot.gameObject.activeSelf != alive) markerRoot.gameObject.SetActive(alive);
        if (markerLabelCG != null) markerLabelCG.alpha = markerAlpha * zoomFade;
        if (markerArrow != null)
        {
            Color c = MarkerArrowInk;
            c.a = markerAlpha * zoomFade;
            markerArrow.color = c;
        }

        // 情報パネル: 区画へ寄り切ってからプレビューを出す。
        float previewWant = map.Arrived && district >= 1 ? 1f : 0f;
        if (previewCG != null)
            previewCG.alpha = Mathf.MoveTowards(previewCG.alpha, previewWant, dt / 0.25f);
        if (panelCG != null)
            panelCG.alpha = Mathf.MoveTowards(panelCG.alpha, 1f - Mathf.Clamp01((map.ZoomAmount - 0.4f) / 0.6f), dt / 0.25f);
        if (previewVideo != null && previewCG != null && previewCG.alpha > 0f
            && !previewVideo.isPlaying && !string.IsNullOrEmpty(previewVideo.url))
        {
            previewVideo.Play();
        }
    }

    /// <summary>検証用。</summary>
    public string DebugState()
    {
        return string.Format("district={0} markerA={1:F2} preview={2:F2} | {3}",
            district, markerAlpha, previewCG != null ? previewCG.alpha : -1f,
            map != null ? map.DebugState() : "map=null");
    }

    // ---- 小物 ----------------------------------------------------------------

    private void StyleLabel(TMP_Text label)
    {
        if (label == null) return;
        TMP_FontAsset mincho = MinchoFont;
        if (mincho != null) label.font = mincho;
        label.characterSpacing = MarkerLabelSpacing;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
    }

    private TMP_FontAsset MinchoFont
    {
        get
        {
            if (!minchoTried)
            {
                minchoTried = true;
                minchoFont = Resources.Load<TMP_FontAsset>(MinchoFontResource);
                if (minchoFont == null)
                    Debug.LogWarning("CitySelectView: 明朝フォントが見つかりません: " + MinchoFontResource);
            }
            return minchoFont;
        }
    }

    private static Sprite CreateDownTriangleSprite(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.DontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        const int ss = 4;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int hit = 0;
                for (int sy = 0; sy < ss; sy++)
                {
                    for (int sx = 0; sx < ss; sx++)
                    {
                        float px = (x + (sx + 0.5f) / ss) / w;
                        float py = (y + (sy + 0.5f) / ss) / h;
                        float half = 0.5f * py;
                        if (Mathf.Abs(px - 0.5f) <= half) hit++;
                    }
                }
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, hit / (float)(ss * ss)));
            }
        }
        tex.Apply();
        Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f);
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    private Image NewImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        RectTransform rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        return img;
    }

    private TMP_Text NewText(string name, Transform parent, string content, float size, Color color, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (uiFont != null) t.font = uiFont;
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        return t;
    }

    private Image[] BuildRim(RectTransform parent, Color color, float thickness)
    {
        Image[] rim = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            Image s = NewImage("Rim" + i, parent, color);
            RectTransform rt = s.rectTransform;
            switch (i)
            {
                case 0: rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f); break;
                case 1: rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f); rt.pivot = new Vector2(0.5f, 0f); break;
                case 2: rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 0.5f); break;
                default: rt.anchorMin = new Vector2(1f, 0f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(1f, 0.5f); break;
            }
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = i < 2 ? new Vector2(0f, thickness) : new Vector2(thickness, -2f * thickness);
            rim[i] = s;
        }
        return rim;
    }

    private static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    private void OnDestroy()
    {
        if (previewVideo != null) previewVideo.Stop();
        if (previewRT != null) { previewRT.Release(); Destroy(previewRT); }
        foreach (Texture2D t in ownedTextures) if (t != null) Destroy(t);
        foreach (Sprite s in ownedSprites) if (s != null) Destroy(s);
        if (map != null) Destroy(map.gameObject);
    }
}

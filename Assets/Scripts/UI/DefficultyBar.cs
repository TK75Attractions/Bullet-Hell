using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DefficultyBar : MonoBehaviour
{
    // 行ボタンの表示サイズ。2026-07-11 指摘「もっと余白を大きく=ボタン自体を
    // 大きく」でリザルト標準(660x120)級へ拡大(シーンの StageBar 583x109 は
    // Init でこの寸法に上書きする)。ラベルは 40 のまま=枠内の余白が増える。
    // 2026-07-11 夜の再指摘「余白を縦方向にさらに」で高さのみ 124→140
    // (横幅は現状維持の指定)。
    private const float BarW = 660f;
    private const float BarH = 140f;
    // 行間(Easy/Normal/Lunatic の中心間隔)。ボタン拡大に合わせて一段広げる
    // (158→180。行の隙間 34→40 で縦の抜けも一段増える)。
    private const float RowSpacing = 180f;

    private CanvasGroup CG;
    private RectTransform whiteBar;
    private CanvasGroup whiteCG;

    // 焼き込みスプライトの所有(自分が Init で焼いた分だけ破棄する。
    // JSAB モーダルのクローンは自身の Init で焼き直すため互いに独立)。
    private readonly List<Texture2D> ownedTextures = new List<Texture2D>();
    private readonly List<Sprite> ownedSprites = new List<Sprite>();

    private TMP_Text descText;
    private RectTransform descRect;
    private TMP_Text promptText;
    private TMP_Text promptRubyO;
    private TMP_Text promptRubyK;
    private float descBaseX;
    private float descAnimT = 1f;
    private float animTime;

    private static readonly string[] descriptions =
    {
        "イージー / EASY - 気軽に遊べる難易度です",
        "ノーマル / NORMAL - 標準的な難易度です",
        "ルナティック / LUNATIC - 上級者向けの高難易度です",
    };

    private DefficultyBox[] boxes = new DefficultyBox[3];
    // Per-box selection progress (0=unselected, 1=selected), smoothed every frame
    // toward its target so rapid input still animates fluidly.
    private float[] selectProgress = new float[3];
    public int index = 1;
    private float whiteY;

    private class DefficultyBox
    {
        public CanvasGroup CG;
        public RectTransform rectTransform;
        public float baseX;
        private TMP_Text nameText;
        private Color baseTextColor;
        private Image frameBoost;

        public DefficultyBox(Transform trans, string name, Sprite bodySprite, Sprite frameSprite, Color textColor)
        {
            CG = trans.GetComponent<CanvasGroup>();
            rectTransform = trans.GetComponent<RectTransform>();
            // 統一様式(2026-07-11 ユーザー確定): 本体はリザルト/タイトルと同じ
            // 銀枠+縦グラデの焼き込み。難易度の色分けは bodySprite に焼いてある。
            Image bar = trans.Find("StageBar").GetComponent<Image>();
            bar.sprite = bodySprite;
            bar.color = Color.white;
            // シーンの旧寸法(583x109)を拡大寸法へ上書き(焼き込みと同寸で表示)。
            ((RectTransform)bar.transform).sizeDelta = new Vector2(BarW, BarH);
            // 非選択行の銀枠補強: 行全体が CanvasGroup α0.4 まで減光すると焼き込み
            // の銀枠が背景に沈む。枠だけの焼き込みを同寸で重ね、非選択時ほど
            // 浮かせる(oracle 提案 2026-07-12)。クローンの再 Init で二重生成
            // しないよう名前で判定。
            Transform boostT = bar.transform.Find("FrameBoost");
            if (boostT == null)
            {
                GameObject boostGo = new GameObject("FrameBoost",
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                boostGo.layer = bar.gameObject.layer;
                boostT = boostGo.transform;
                RectTransform boostRect = (RectTransform)boostT;
                boostRect.SetParent(bar.transform, false);
                boostRect.anchorMin = Vector2.zero;
                boostRect.anchorMax = Vector2.one;
                boostRect.offsetMin = Vector2.zero;
                boostRect.offsetMax = Vector2.zero;
            }
            frameBoost = boostT.GetComponent<Image>();
            frameBoost.sprite = frameSprite;
            frameBoost.type = Image.Type.Simple;
            frameBoost.raycastTarget = false;
            frameBoost.color = new Color(1f, 1f, 1f, 0f);
            // 旧様式の灰色端キャップは廃止(斜め端は焼き込み枠が持つ)。
            Transform grayL = trans.Find("Gray_L");
            if (grayL != null) grayL.gameObject.SetActive(false);
            Transform grayR = trans.Find("Gray_R");
            if (grayR != null) grayR.gameObject.SetActive(false);
            nameText = trans.Find("StageName").GetComponent<TMP_Text>();
            nameText.text = name;
            nameText.fontSize = UiButtonStyle.LabelSizeTitleMenu;
            // 行スラッシュ(内側の細)。クローンの再 Init で二重生成しないよう名前で判定。
            if (trans.Find("RowSlashL") == null)
            {
                float thinX = UiButtonStyle.ThinSlashX(BarW);
                float h = UiButtonStyle.SlashHeight(BarH);
                UiButtonStyle.AddSlash(rectTransform, "RowSlashL", new Color(1f, 1f, 1f, 0.5f), -thinX, 2.5f, h);
                UiButtonStyle.AddSlash(rectTransform, "RowSlashR", new Color(1f, 1f, 1f, 0.5f), thinX, 2.5f, h);
            }
            baseTextColor = textColor;
            baseX = rectTransform.anchoredPosition.x;
        }

        public void SetPosition(float progress)
        {
            CG.alpha = 0.4f + 0.6f * progress;
            rectTransform.localScale = Vector3.one * (0.8f + 0.2f * progress);
            nameText.color = Color.Lerp(baseTextColor, Color.white, progress);
            // 非選択時のみ銀枠を補強(選択行は焼き込みそのままの見た目を維持)。
            if (frameBoost != null)
                frameBoost.color = new Color(1f, 1f, 1f, (1f - progress) * 0.5f);
        }
    }

    public void Init()
    {
        Transform trans = transform.Find("List");
        (trans.Find("Easy") as RectTransform).anchoredPosition = new Vector2(0f, RowSpacing);
        (trans.Find("Normal") as RectTransform).anchoredPosition = Vector2.zero;
        (trans.Find("Lunatic") as RectTransform).anchoredPosition = new Vector2(0f, -RowSpacing);
        // ベース色は従来の難易度色(色分け)を維持し、様式だけ統一する。
        // 銀枠補強は3行共通の1枚を焼いて共有する。
        Sprite rowFrame = UiButtonStyle.CreateFrameSprite((int)BarW, (int)BarH,
            ownedTextures, ownedSprites, "DiffRowFrameBoost");
        boxes[0] = new DefficultyBox(trans.Find("Easy"), "EASY",
            UiButtonStyle.CreateBodySpriteTinted((int)BarW, (int)BarH, new Color(0.086f, 0.227f, 0.373f),
                ownedTextures, ownedSprites, "DiffButtonEasy"), rowFrame,
            new Color(0.56f, 0.72f, 0.91f));
        boxes[1] = new DefficultyBox(trans.Find("Normal"), "NORMAL",
            UiButtonStyle.CreateBodySpriteTinted((int)BarW, (int)BarH, new Color(0.055f, 0.525f, 0.91f),
                ownedTextures, ownedSprites, "DiffButtonNormal"), rowFrame,
            new Color(0.85f, 0.93f, 1f));
        boxes[2] = new DefficultyBox(trans.Find("Lunatic"), "LUNATIC",
            UiButtonStyle.CreateBodySpriteTinted((int)BarW, (int)BarH, new Color(0.36f, 0.078f, 0.188f),
                ownedTextures, ownedSprites, "DiffButtonLunatic"), rowFrame,
            new Color(0.91f, 0.6f, 0.69f));
        CG = GetComponent<CanvasGroup>();
        CG.alpha = 0;

        whiteBar = transform.Find("White").GetComponent<RectTransform>();
        whiteCG = whiteBar.GetComponent<CanvasGroup>();
        whiteCG.alpha = 1;
        RestyleSelectionMarker();

        Transform desc = transform.Find("DescText");
        if (desc != null)
        {
            descText = desc.GetComponent<TMP_Text>();
            descRect = desc.GetComponent<RectTransform>();
            descBaseX = descRect.anchoredPosition.x;
        }
        Transform prompt = transform.Find("Prompt");
        if (prompt != null) promptText = prompt.GetComponent<TMP_Text>();
        Transform rubyO = transform.Find("PromptRubyO");
        if (rubyO != null) promptRubyO = rubyO.GetComponent<TMP_Text>();
        Transform rubyK = transform.Find("PromptRubyK");
        if (rubyK != null) promptRubyK = rubyK.GetComponent<TMP_Text>();

        SetLayoutPosition("Title", new Vector2(0f, 365f));
        SetLayoutPosition("TitleRubyN", new Vector2(-114f, 425f));
        SetLayoutPosition("TitleRubyS", new Vector2(152f, 425f));
        SetLayoutPosition("LineT", new Vector2(0f, -315f));
        SetLayoutPosition("DescText", new Vector2(0f, -370f));
        SetLayoutPosition("LineB", new Vector2(0f, -425f));
        SetLayoutPosition("PromptRubyO", new Vector2(0f, -468f));
        SetLayoutPosition("PromptRubyK", new Vector2(133f, -468f));
        SetLayoutPosition("Prompt", new Vector2(0f, -505f));
        if (descRect != null) descRect.sizeDelta = new Vector2(960f, 60f);
        if (promptText != null) promptText.rectTransform.sizeDelta = new Vector2(650f, 60f);

        ResetSelection(1);
    }

    // 選択マーカーを統一様式の太スラッシュ対へ(タイトルの選択マーカーと同規則:
    // 焼き込み枠のすぐ外 ThickSlashX に密着)。旧様式の白ブラケットスプライトと
    // Shine 残骸は使わない。
    private void RestyleSelectionMarker()
    {
        Transform whiteL = whiteBar.Find("White_L");
        if (whiteL != null) whiteL.gameObject.SetActive(false);
        Transform whiteR = whiteBar.Find("White_R");
        if (whiteR != null) whiteR.gameObject.SetActive(false);
        Transform shine = whiteBar.Find("ShineMask");
        if (shine != null) shine.gameObject.SetActive(false);
        if (whiteBar.Find("MarkerSlashL") != null) return; // クローン再 Init 対策
        float thickX = UiButtonStyle.ThickSlashX(BarW);
        float h = UiButtonStyle.SlashHeight(BarH);
        UiButtonStyle.AddSlash(whiteBar, "MarkerSlashL", Color.white, -thickX, 11f, h);
        UiButtonStyle.AddSlash(whiteBar, "MarkerSlashR", Color.white, thickX, 11f, h);
    }

    private void OnDestroy()
    {
        foreach (Sprite s in ownedSprites) if (s != null) Destroy(s);
        foreach (Texture2D t in ownedTextures) if (t != null) Destroy(t);
        ownedSprites.Clear();
        ownedTextures.Clear();
    }

    // Snap selection to the given index without animating (used on entering the screen).
    public void ResetSelection(int newIndex)
    {
        index = Mathf.Clamp(newIndex, 0, boxes.Length - 1);
        for (int i = 0; i < boxes.Length; i++)
        {
            selectProgress[i] = i == index ? 1f : 0f;
            boxes[i].SetPosition(selectProgress[i]);
        }
        whiteY = TargetWhiteY();
        whiteBar.anchoredPosition = new Vector2(0, whiteY);
        whiteCG.alpha = 1;
        RefreshDescription();
    }

    // Per-frame animation: boxes ease toward their selection state, the white
    // brackets glide to the selected row, the prompt blinks, the description slides in.
    public void Tick(float dt)
    {
        animTime += dt;

        float follow = 1f - Mathf.Exp(-14f * dt);
        for (int i = 0; i < boxes.Length; i++)
        {
            float target = i == index ? 1f : 0f;
            selectProgress[i] = Mathf.Abs(target - selectProgress[i]) < 0.001f
                ? target
                : Mathf.Lerp(selectProgress[i], target, follow);
            boxes[i].SetPosition(selectProgress[i]);
        }

        float targetY = TargetWhiteY();
        whiteY = Mathf.Abs(targetY - whiteY) < 0.5f ? targetY : Mathf.Lerp(whiteY, targetY, 1f - Mathf.Exp(-16f * dt));
        whiteBar.anchoredPosition = new Vector2(0, whiteY);

        if (promptText != null)
        {
            promptText.alpha = 0.45f + 0.4f * Mathf.Sin(animTime * 4f);
            if (promptRubyO != null) promptRubyO.alpha = promptText.alpha;
            if (promptRubyK != null) promptRubyK.alpha = promptText.alpha;
        }
        if (descText != null && descAnimT < 1f)
        {
            descAnimT = Mathf.Min(1f, descAnimT + dt / 0.2f);
            float p = -descAnimT * (descAnimT - 2);
            descText.alpha = p;
            descRect.anchoredPosition = new Vector2(descBaseX + 30f * (1f - p), descRect.anchoredPosition.y);
        }
    }

    // Staggered slide-in during the screen transition: lower rows arrive a beat
    // later. Only the X offset is written here; alpha/scale stay owned by Tick.
    public void SetEntranceProgress(float p)
    {
        for (int i = 0; i < boxes.Length; i++)
        {
            float local = Mathf.Clamp01((p - i * 0.12f) / 0.76f);
            float ease = 1f - (1f - local) * (1f - local) * (1f - local);
            RectTransform rect = boxes[i].rectTransform;
            rect.anchoredPosition = new Vector2(boxes[i].baseX + 140f * (1f - ease), rect.anchoredPosition.y);
        }
    }

    public void Up()
    {
        if (index <= 0) return;
        index--;
        RefreshDescription();
    }

    public void Down()
    {
        if (index >= boxes.Length - 1) return;
        index++;
        RefreshDescription();
    }

    public void SetAlpha(float alpha)
    {
        CG.alpha = alpha;
    }

    // The brackets follow the selected box's actual scene position, so the row
    // spacing can be tuned in the scene without touching code.
    private float TargetWhiteY()
    {
        return boxes[index].rectTransform.anchoredPosition.y;
    }

    private void RefreshDescription()
    {
        if (descText == null) return;
        descText.text = descriptions[index];
        descAnimT = 0f;
        descText.alpha = 0f;
    }

    private void SetLayoutPosition(string childName, Vector2 position)
    {
        RectTransform child = transform.Find(childName) as RectTransform;
        if (child != null) child.anchoredPosition = position;
    }
}

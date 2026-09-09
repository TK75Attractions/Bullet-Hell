using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TitleManager : MonoBehaviour
{
    // タイトル BGM(Discotheque)の実測 BPM。ロゴのビートパルス(振動)・図形の
    // フラッシュ周期をこの値に同期させる。測定: Tools/measure_bpm.py の
    // オンセット包絡自己相関 + 位相最適化櫛フィルタ(拍数正規化)で 130.00 BPM
    // (拍間隔 461.5ms)を確定(2026-07-13)。
    [SerializeField] private float bpm = 130f;
    // 引き継ぎコード表示・入力用の可読フォント(等幅コードフォント。0/O・1/I が
    // 紛れない)。未割当なら uiFont にフォールバック(第31便)。
    [SerializeField] private TMP_FontAsset codeFont;

    private CanvasGroup group;
    private TMP_Text promptText;
    private RectTransform logoRect;
    private float logoBaseY;
    private float animTime;
    private bool dismissed;
    // プレイ終了(シーン再読込)からの復帰専用のパンチイン演出の状態。設定画面を
    // 閉じたときには使わない(タイトルは背後で動き続けているため演出不要)。
    private bool returnAnimating;
    private Image returnBackdrop;

    private float beatTimer;
    private float beatPulse;
    // タイトル BGM(Discotheque)。AManager 常駐 BGMSource でループ再生し、
    // 戻り値の AudioSource から再生位置を読んでロゴのビートパルスを音にロックする。
    // clip の t=0 は実ダウンビート(2.763s)にトリム済み=位相 0 が拍頭。
    private AudioSource titleBgmSource;
    // Discotheque を stone(最も大きいステージ -10.7LUFS)より僅かに下の -12LUFS へ
    // 揃える減衰(実測 -6.7LUFS → -5.3dB)。Tools/measure_bpm.py / ffmpeg ebur128。
    // 2026-07-13 指摘「もう一段下げる」で 0.55→0.37(約 -3.4dB / -15.4LUFS 相当)。
    private const float TitleBgmVolume = 0.37f;
    // リザルト→選択復帰時のタイトル BGM 立ち上げ秒数(ぶつ切り防止)。
    private const float TitleBgmFadeIn = 0.6f;
    // ビートパルスの減衰(拍に対する割合)。frac=0(拍頭)で 1、この割合で 0 に。
    // 旧自走版(dt*5 減衰≒0.2s/拍0.46s)と同等の 0.43。
    private const float BeatPulseDecayFrac = 0.43f;
    private Graphic[] shapeGraphics = new Graphic[0];
    private float[] shapeBaseAlphas = new float[0];

    // Big flat shapes drifting slowly behind the logo (Just Shapes & Beats vibe).
    private struct ShapeAnim
    {
        public RectTransform rect;
        public Vector2 basePos;
        public float phase;
        public float speedX;
        public float speedY;
        public float ampX;
        public float ampY;
        public float rotSpeed;
    }

    private ShapeAnim[] shapes = new ShapeAnim[0];

    // ---- Title menu + transfer panel (black / cyan / deep navy) -------------
    public enum TitleMenuAction { Start = 0, Options = 1, Transfer = 2, Ranking = 3 }

    private static readonly Color Cyan = new Color(0.219f, 0.761f, 0.878f);
    private static readonly Color CyanDim = new Color(0.11f, 0.34f, 0.40f);
    private static readonly Color Navy = new Color(0.03f, 0.05f, 0.11f, 1f);
    private static readonly Color NavyDeep = new Color(0.015f, 0.028f, 0.06f, 0.98f);
    private static readonly Color ErrorRed = new Color(0.96f, 0.46f, 0.52f);

    // The title menu clones the difficulty-select rows (DefficultyBar) so both
    // screens share one design language: slanted StageBar banner + StageName
    // label + gliding white slash brackets. 統一便: バナー本体はリザルト画面で
    // 確立した焼き込み(銀枠+シアンリム+青縦グラデ・UiButtonStyle)へ差し替え。
    private static readonly Color MenuTextBase = new Color(0.85f, 0.93f, 1f);

    // How far above its scene-authored position the logo is lifted.
    // 2026-07-13: 上げすぎ(160)を元の位置(130)へ戻す。実フレーム(720p)で確認した
    // 通り、ボタン3行(660x160・行間172)は logo=130 でも画面内にそのまま収まり
    // (行はロゴと独立に配置され下端に余白がある)、ロゴ可視下端とスタート上端の
    // 間隔も約50px 空くため、行間側の調整は不要だった。
    private const float LogoRaiseOffset = 130f;

    // スタート決定からステージ選択を重ね始めるまでの時間。GManager がこの時間
    // 経過後に state を切り替えて SSManager.PlayEntrance を呼ぶ(演出は総尺
    // StartExitTotal まで続き、選択画面のフェードインと交差する)。
    // 第31便: スタート演出が「フラッシュみたい」に速く感じるため約1.25倍に伸ばす
    // (0.30→0.375 / 0.60→0.75)。CoverDelay はステージ選択が重なり始める時刻。
    public const float StartExitCoverDelay = 0.375f;
    private const float StartExitTotal = 0.75f;

    private TMP_FontAsset uiFont;
    private RectTransform menuRoot;
    private TMP_Text[] menuItems = new TMP_Text[0];
    private RectTransform[] menuItemRects = new RectTransform[0];
    private CanvasGroup[] menuRowCG = new CanvasGroup[0];
    private Image[] menuRowBars = new Image[0];
    // 決定閃光用の白オーバーレイ(バナーと同形の平行四辺形・通常 alpha0)。
    // 焼き込みスプライトは頂点色で白側へ飛ばせないため、色 lerp ではなく
    // オーバーレイの減衰で閃光を出す。
    private ParallelogramGraphic[] menuRowFlash = new ParallelogramGraphic[0];
    private Sprite menuButtonSprite;
    // 1P/2P トグル専用の焼き込み本体。menuButtonSprite(660x160)をトグル(176x78)へ
    // 流用すると非等倍ストレッチで 19° 斜辺が見かけ約 10.7° に歪むため、トグルの
    // 表示アスペクトに一致した解像度で別に焼く(2026-07-14 傾き一致対応)。
    private Sprite pcToggleSprite;
    private float[] menuItemSel = new float[0];
    private float[] menuRowY = new float[0];
    private int menuIndex;

    // 引き継ぎ・ランキングの導線(SPEC確定・2026-07-16「ランキング+引き継ぎ」便で復活)。
    // 4行メニュー(スタート/設定/引き継ぎ/ランキング)にする。
    private const bool ShowTransferRow = true;
    private const bool ShowRankingRow = true;
    // 4行を画面内に収めるため、難易度規格(660x160・行間172)から専用に縮小する
    // (旧3行版は本ファイル履歴 2026-07-13 時点で 660x160・行間172・topRowY=-106 で
    // 確定していた)。ロゴ実測(Base.unity Logo: anchoredY=60, sizeDelta.y=430,
    // LogoRaiseOffset=130 → 実行時中心 y=190, 下端 y=-25)+旧版の実測クリアランス
    // (topRowY=-106 のとき上端 y=-26、ロゴ下端との差はほぼ0=同じ余白比率を踏襲)を
    // 基準に、MenuRowH=120/MenuRowGap=132(12px クリアランス比を維持)で4行を
    // 上端 y=-86(旧版から+20 のみ)〜下端 y=-482(画面下端 -540 まで48pxの余裕)に
    // 収める。実フレームでの見た目確認は未実施(要親確認。NOTES.md 参照)。
    private const float MenuRowW = 660f;
    private const float MenuRowH = 120f;
    private const float MenuRowGap = 132f;
    private const float MenuRowCenter = -284f; // rowY[0] = -284+198 = -86
    private bool[] menuRowEnabled = new bool[0];
    // 無効行の見た目(灰色・沈む)。有効行は白/MenuTextBase。
    private static readonly Color MenuDisabledText = new Color(0.45f, 0.48f, 0.55f, 1f);
    private static readonly Color MenuDisabledBar = new Color(0.34f, 0.36f, 0.42f, 1f);

    // Cloned DefficultyBar "White" slash brackets; glide to the selected row.
    private RectTransform menuWhite;
    private float menuWhiteY;

    private GameObject transferRoot;
    private CanvasGroup transferCG;
    // 引き継ぎ画面の背景ぼかし(難易度オーバーレイと同構成: 完成フレームの
    // スナップショット+暗スクリム)。メニュー・ロゴを退場させず背景に残す。
    private RawImage transferBackdrop;
    private RenderTexture transferBlurRT;
    private Coroutine transferCaptureRoutine;
    private Coroutine transferCloseRoutine; // 閉じるときのフェードアウト(第34便)
    private TMP_Text transferMessageText;
    private TMP_Text transferHintText;
    private bool transferOpen;

    // 方向シーケンス入力(SPEC §1)。矢印12桁のスロット表示+入力状態機械。
    private enum TransferInputState { Entry, Confirm }
    private TransferInputState transferState = TransferInputState.Entry;
    private readonly List<int> transferDigits = new List<int>();
    private TMP_Text[] transferDigitTexts = new TMP_Text[0];
    private float transferDirCooldown;
    private HoldTrigger transferBackHold;
    private float transferCursorBlink;
    private DirectionTransferCode.Payload transferPendingPayload;
    private static readonly Color DigitFilled = new Color(0.62f, 0.98f, 1f);
    private static readonly Color DigitEmpty = new Color(0.25f, 0.36f, 0.46f, 0.55f);
    private static readonly Color DigitCursor = new Color(0.95f, 0.98f, 1f);

    // ビルド時に空振りした光学中央補正の再適用フラグ(表示後の初回に確定)。
    private bool menuInkCentered;
    private bool transferInkCentered;

    // ---- ランキング盤面(SPEC §2.2) -----------------------------------------
    private GameObject rankingRoot;
    private CanvasGroup rankingCG;
    private RawImage rankingBackdrop;
    private RenderTexture rankingBlurRT;
    private Coroutine rankingCaptureRoutine;
    private Coroutine rankingCloseRoutine;
    private bool rankingOpen;
    private TMP_Text rankingHeaderText;
    private TMP_Text[] rankingRowTexts = new TMP_Text[0];
    private int rankingStageIndex;
    private int rankingDifficultyIndex = DirectionTransferCode.DifficultyCount - 1;
    private int rankingModeIndex; // 0=1P, 1=2P
    private static readonly string[] RankingModes = { "1P", "2P" };
    private bool rankingInkCentered;

    // ---- 1P/2P 人数トグル(2P その1) --------------------------------------
    // 3 行メニュー(スタート/設定/引き継ぎ)は固定レイアウトのため触らず、ロゴと
    // メニューの間に独立した「1P | 2P」トグルを置く。既定 1P。P1 の ←/→ で切替。
    // 既存の平行四辺形ボタン様式(menuButtonSprite・19°スラッシュ言語)を流用する。
    private RectTransform playerCountRoot;
    private RectTransform titleControlGuideRoot;

    private readonly Image[] pcSegBars = new Image[2];
    private readonly TMP_Text[] pcSegLabels = new TMP_Text[2];
    private bool pcTwoPlayer = false;
    private static readonly Color PcSelText = Color.white;
    private static readonly Color PcSelBar = Color.white;
    private static readonly Color PcDimText = new Color(0.5f, 0.55f, 0.63f, 1f);
    private static readonly Color PcDimBar = new Color(0.38f, 0.42f, 0.5f, 0.9f);
    public bool TwoPlayerSelected => pcTwoPlayer;

    public int MenuIndex => menuIndex;
    public TitleMenuAction CurrentAction => (TitleMenuAction)menuIndex;
    public bool IsTransferOpen => transferOpen;
    public bool IsRankingOpen => rankingOpen;

    // ---- 3D の部屋(旅支度の部屋・第7便) ------------------------------------
    // 背景を Astra 制作の 3D 部屋にする。部屋の描画は TitleRoomController が
    // 専用カメラ→RenderTexture で行い、ここではその RT を最背面へ貼り、
    // ロゴ・メニューの配置と立ち絵・可読性用の暗幕を面倒みる。
    // 部屋が用意できていない(Rig 未配置・素材欠落)ときは、従来の JSAB 背景
    // (Back + Shapes)のまま動く。
    private RawImage roomView;
    // roomView へ最後に入れたマテリアル(null=既定)。ドット風の色数減衰でだけ使う。
    private Material appliedRoomMaterial;
    private Image roomScrim;
    private Image heroImage;
    private RectTransform heroRect;
    private Vector2 heroBasePos;
    private GameObject backObj;
    private GameObject shapesObj;
    private bool roomLayout;
    // 立ち絵は寄りのあいだフェードアウトする(寄った先のカメラでは画面外にいる、
    // という読み方になる)。全景では数 px の視差だけ付ける。
    private const float HeroParallaxPx = 10f;
    // スタート決定時に「地図へ寄る」ぶんの先行時間。GManager はこの分だけ
    // ステージ選択の重ね始めを遅らせる。
    public const float StartZoomLead = 0.4f;

    // ---- オブジェクト上の▼メニュー(第8便) ---------------------------------
    // 平行四辺形のボタン列をやめ、部屋のオブジェクトの真上に小さな白い▼を出す。
    // ▼は常時表示(弱い上下の浮遊)で、選択中のものだけ▼の上にラベル(枠も帯も無い
    // 明朝体の文字だけ)が出る。▼・ラベルは 3D の位置を毎フレーム投影して置くので、
    // カメラが寄っても対象の上に留まる。旧ボタン列はコードを残したまま隠す。
    [SerializeField] private bool useObjectMenu = true;
    private RectTransform objectMenuRoot;
    private RectTransform[] markerRoots = new RectTransform[0];
    private Image[] markerArrows = new Image[0];
    private CanvasGroup[] markerLabelCG = new CanvasGroup[0];
    private TMP_Text[] markerLabelTexts = new TMP_Text[0];
    private TMP_Text[] markerLabelShadows = new TMP_Text[0];
    private float[] markerLabelAlpha = new float[0];
    private bool markerInkCentered;
    private bool objectMenuOn;
    private static Sprite markerArrowSprite;
    // ラベルの明朝体(しっぽり明朝)。差し替えるときはこの 1 行だけを変える。
    // 読めなかったときは従来どおり uiFont(M PLUS 1 Code)で描く。
    private const string MinchoFontResource = "Fonts/ShipporiMincho-Regular SDF";
    private TMP_FontAsset minchoFont;
    private bool minchoFontTried;
    // ▼の寸法・浮遊量・ラベルまでの距離(px・1920x1080 基準)。
    private const float MarkerArrowW = 34f;
    private const float MarkerArrowH = 22f;
    private const float MarkerLift = 30f;     // 対象の上端から▼までの距離
    private const float MarkerFloatPx = 4f;   // 上下の浮遊
    private const float MarkerLabelGap = 40f; // ▼の中心からラベル中心まで
    private const float MarkerLabelH = 40f;
    private const float MarkerLabelFont = 26f;
    private const float MarkerFadeSpeed = 1f / 0.15f;
    // ▼は白(#F2F2F2)。非選択は同じ白のまま alpha だけ落とす(金には戻さない)。
    // 夜の室内は明部(窓・ランタン)も暗部もあるので、白 1 色では窓に重なると
    // 埋もれる。▼の後ろに一回り大きい暗い三角を敷いて縁取り代わりにする。
    private static readonly Color MarkerArrowInk = new Color(0.949f, 0.949f, 0.949f, 1f);
    private static readonly Color MarkerArrowInkDim = new Color(0.949f, 0.949f, 0.949f, 0.58f);
    private static readonly Color MarkerArrowEdge = new Color(0.02f, 0.02f, 0.04f, 0.70f);
    private const float MarkerArrowEdgeScale = 1.30f;  // 影三角の拡大率
    private const float MarkerArrowEdgeDrop = 1.5f;    // 影三角を下へずらす量(px)
    // ラベルは枠・帯・背景板なしの明朝体。白〜生成りの文字 1 枚と、その後ろに
    // 1px ずらした暗い影 1 枚だけ(読みやすさのため)。
    private static readonly Color MarkerLabelInk = new Color(0.976f, 0.961f, 0.918f, 1f);
    // 白い明朝を、窓の街明かりやランタンの炎(明部)の上でも読ませるための影。
    // 落ち影 1 枚だけだと明部に重なった側の輪郭が消えるので、8 方向へ 1.4px
    // ずらした薄い影を重ねて細い暗縁にする(帯・背景板は置かない)。
    private static readonly Color MarkerLabelShadow = new Color(0.01f, 0.01f, 0.02f, 1f);
    private static readonly Vector2[] MarkerLabelShadowDirs =
    {
        new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, 1f), new Vector2(0f, -1f),
        new Vector2(0.71f, 0.71f), new Vector2(-0.71f, 0.71f),
        new Vector2(0.71f, -0.71f), new Vector2(-0.71f, -0.71f),
    };
    // 内側の濃い縁(1.5px)と、外側の薄いにじみ(3px)の 2 重。窓の街明かりの上でも
    // 白い明朝が沈まないだけの暗さを、帯を置かずに作る。
    private static readonly float[] MarkerLabelShadowRadius = { 3.0f, 1.5f };
    private static readonly float[] MarkerLabelShadowAlpha = { 0.50f, 0.85f };
    // 字間 +4%。TMP の characterSpacing は 1/100em 単位なので 4 = +4%。
    private const float MarkerLabelSpacing = 4f;
    private const float MarkerLabelBoxW = 360f;  // 折り返さないための十分な幅
    // ▼から見たラベルの追加ずらし(px)。帯が無くなったぶん文字が直接背景に乗るので、
    // 明るい実体の真上に来るものだけ横へ逃がす。引き継ぎはランタンの炎に
    // 「引」が完全に埋まっていたので右へ、設定は窓の桟から少し右へ寄せる。
    private static readonly Vector2[] MarkerLabelOffset =
    {
        Vector2.zero,             // スタート
        new Vector2(0f, 6f),      // 設定
        new Vector2(62f, 4f),     // 引き継ぎ(ランタンの炎を外す)
        Vector2.zero,             // ランキング
        Vector2.zero,             // 1P
        Vector2.zero,             // 2P
    };
    private static readonly string[] MarkerLabels =
        { "スタート", "設定", "引き継ぎ", "ランキング", "1P", "2P" };
    // 投影点からの微調整(px・+y は上)。実フレームで詰めた値:
    // 引き継ぎ=手紙の真上はランタンの炎と重なって▼が読めないので封筒の右上へ、
    // ランキング=本の上ではなく一段上の棚板の前へ出す。
    private static readonly Vector2[] MarkerScreenOffset =
    {
        Vector2.zero,                  // スタート(地図)
        new Vector2(0f, -29f),         // 設定(ランタン。ラベルをロゴの下端から離す)
        new Vector2(30f, -60f),        // 引き継ぎ(手紙。ランタンの炎を避けて封筒の右上へ)
        new Vector2(0f, 55f),          // ランキング(本棚)
        new Vector2(-30f, 0f),         // 1P(2 枚のマントの帯が重ならないよう左右へ開く)
        new Vector2(20f, 0f),          // 2P
    };

    // ---- 部屋モードのロゴ(第9便) -------------------------------------------
    // 旧ロゴ(シーンの Logo・RawImage)はそのまま残し、部屋モードのときだけ隠して
    // 新ロゴ(Resources/UI/jbi-logo-v2)を代わりに置く。浮遊・ビートパルス・
    // スタート時の上抜けは logoRect を差し替えるだけで従来どおり効く
    // (演出コードには一切触っていない)。
    private Image roomLogoImage;
    private RectTransform roomLogoRect;
    private RectTransform sceneLogoRect;
    // 新ロゴの表示幅と原画(2146x733)の縦横比。位置は窓の中へ下げて拡大した(第11便)。
    // 幅 740 で rect は 740x252.8px。1080p 実フレームでの実測は、絵の最下点(リボンの
    // 尾)が rect 上端から +220px、「設定」のラベル上端が y=382。浮遊(±10px)と
    // ビートパルス(x1.035)を足した最下点でもラベルまで 40px 以上あく上限が y=320 で、
    // 窓(ガラス y=168..514)の 1/3 まで下げるとラベルに掛かるのでここで止めている。
    private const float RoomLogoWidth = 740f;
    private const float RoomLogoAspect = 2146f / 733f;
    private const float RoomLogoX = 20f;
    private const float RoomLogoY = 320f;

    private TitleRoomController Room => TitleRoomController.Instance;

    public void Init()
    {
        animTime = 0f;
        beatTimer = 0f;
        beatPulse = 0f;
        returnAnimating = false;
        titleBgmSource = null;
        StartTitleBgm();
        if (returnBackdrop != null) returnBackdrop.gameObject.SetActive(false);
        group = GetComponent<CanvasGroup>();
        Transform prompt = transform.Find("Prompt");
        if (prompt != null) promptText = prompt.GetComponent<TMP_Text>();
        Transform logo = transform.Find("Logo");
        if (logo != null)
        {
            logoRect = logo.GetComponent<RectTransform>();
            sceneLogoRect = logoRect;
            // Lift the logo above its scene-authored spot (float / beat pulse are
            // applied on top of this raised base position).
            logoBaseY = logoRect.anchoredPosition.y + LogoRaiseOffset;
            logoRect.anchoredPosition = new Vector2(logoRect.anchoredPosition.x, logoBaseY);
        }

        Transform shapesRoot = transform.Find("Shapes");
        if (shapesRoot != null)
        {
            shapes = new ShapeAnim[shapesRoot.childCount];
            shapeGraphics = new Graphic[shapesRoot.childCount];
            shapeBaseAlphas = new float[shapesRoot.childCount];
            for (int i = 0; i < shapesRoot.childCount; i++)
            {
                RectTransform rect = shapesRoot.GetChild(i) as RectTransform;
                shapeGraphics[i] = rect.GetComponent<Graphic>();
                shapeBaseAlphas[i] = shapeGraphics[i] != null ? shapeGraphics[i].color.a : 1f;
                shapes[i] = new ShapeAnim
                {
                    rect = rect,
                    basePos = rect.anchoredPosition,
                    phase = i * 1.7f,
                    speedX = 0.22f + 0.07f * (i % 3),
                    speedY = 0.17f + 0.06f * ((i + 1) % 4),
                    ampX = 70f + 30f * (i % 3),
                    ampY = 50f + 25f * ((i + 2) % 3),
                    rotSpeed = (i % 2 == 0 ? 1f : -1f) * (4f + 3f * (i % 3)),
                };
            }
        }

        EnsureUiBuilt();
        ApplyRoomLayout();

        group.alpha = 1f;
        transform.localScale = Vector3.one;
        dismissed = false;
        gameObject.SetActive(true);
    }

    // ---- 3D の部屋 ---------------------------------------------------------

    // 部屋を起こし、タイトルの並びを部屋向けへ組み替える(ロゴ左上・メニュー左寄せ・
    // 右手前に立ち絵・左側に薄い暗幕)。部屋が無ければ何もしない=従来の見た目。
    private void ApplyRoomLayout()
    {
        TitleRoomController room = Room;
        if (room == null) return;
        room.SetRoomActive(true);
        if (!room.Ready) return;

        if (backObj == null) backObj = transform.Find("Back")?.gameObject;
        if (shapesObj == null) shapesObj = transform.Find("Shapes")?.gameObject;
        // JSAB の平面図形と濃紺ベタは部屋と喧嘩する(写実的な室内の上に大きな図形が
        // 浮くと瓦礫に見える)ので、部屋があるときは下ろす。舞う塵が代わりになる。
        if (backObj != null) backObj.SetActive(false);
        if (shapesObj != null) shapesObj.SetActive(false);

        if (roomView == null)
        {
            roomView = CreateRawImage("RoomView", transform);
            StretchToParent(roomView.rectTransform);
            roomView.color = Color.white;
        }
        roomView.texture = room.Texture;
        appliedRoomMaterial = room.ViewMaterial;
        roomView.material = appliedRoomMaterial;
        roomView.gameObject.SetActive(true);
        roomView.rectTransform.SetSiblingIndex(0);

        if (roomScrim == null)
        {
            roomScrim = CreatePanel("RoomScrim", transform, new Vector2(-480f, 0f),
                new Vector2(960f, 1080f), Color.white);
            roomScrim.sprite = CreateHorizontalFadeSprite();
            roomScrim.type = Image.Type.Simple;
        }
        // 文字が載る左側だけを控えめに落とす(右端 alpha 0 → 左端 0.5)。
        roomScrim.color = new Color(0f, 0.012f, 0.035f, 1f);
        roomScrim.gameObject.SetActive(true);
        roomScrim.rectTransform.SetSiblingIndex(1);

        objectMenuOn = useObjectMenu;
        if (objectMenuOn)
        {
            // ロゴは窓の中央上部へ(実測: 窓の中心は画面 x≈960・上端 y≈115)。
            // 部屋モードでは新ロゴ(v2)を出し、旧ロゴは隠す(コードは残す)。
            EnsureRoomLogo();
            if (roomLogoRect != null)
            {
                if (sceneLogoRect != null) sceneLogoRect.gameObject.SetActive(false);
                logoRect = roomLogoRect;
                logoRect.gameObject.SetActive(true);
                logoRect.sizeDelta = new Vector2(RoomLogoWidth, RoomLogoWidth / RoomLogoAspect);
                logoRect.anchoredPosition = new Vector2(RoomLogoX, RoomLogoY);
                logoBaseY = RoomLogoY;
            }
            else if (logoRect != null)
            {
                // 新ロゴが読めなかったときは旧ロゴのまま(第8便の値)。
                logoRect.sizeDelta = new Vector2(620f, 350f);
                logoRect.anchoredPosition = new Vector2(20f, 352f);
                logoBaseY = 352f;
            }
            // 旧ボタン列と 1P/2P トグルは下ろす(コードは残す)。
            if (menuRoot != null) menuRoot.gameObject.SetActive(false);
            if (playerCountRoot != null) playerCountRoot.gameObject.SetActive(false);
            BuildObjectMenu();
        }
        else
        {
            // ロゴは左上へ。デザインは変えず、大きさと位置だけ整える。
            if (roomLogoRect != null) roomLogoRect.gameObject.SetActive(false);
            if (sceneLogoRect != null)
            {
                sceneLogoRect.gameObject.SetActive(true);
                logoRect = sceneLogoRect;
            }
            if (logoRect != null)
            {
                logoRect.sizeDelta = new Vector2(560f, 317f);
                logoRect.anchoredPosition = new Vector2(-618f, 336f);
                logoBaseY = 336f;
            }

            // メニュー列は左へ寄せ、少し縮める(行の寸法・19° の様式には触らない)。
            if (menuRoot != null)
            {
                menuRoot.localScale = Vector3.one * 0.82f;
                menuRoot.anchoredPosition = new Vector2(-560f, 30f);
            }
            if (playerCountRoot != null)
            {
                playerCountRoot.localScale = Vector3.one * 0.82f;
                playerCountRoot.anchoredPosition = new Vector2(-560f, 30f + (menuRowY.Length > 0 ? menuRowY[0] : 0f) * 0.82f + (MenuRowGap - 24f) * 0.82f);
            }
        }

        BuildHero();
        room.SetTwoPlayer(pcTwoPlayer);
        roomLayout = true;
    }

    // 部屋モード用の新ロゴを 1 度だけ作る。旧ロゴの直後に置いて描画順を揃える。
    private void EnsureRoomLogo()
    {
        if (roomLogoRect != null) return;
        Sprite sprite = Resources.Load<Sprite>("UI/jbi-logo-v2");
        if (sprite == null) return;
        GameObject go = new GameObject("LogoRoom", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;
        roomLogoRect = (RectTransform)go.transform;
        roomLogoRect.SetParent(transform, false);
        roomLogoRect.anchorMin = roomLogoRect.anchorMax = new Vector2(0.5f, 0.5f);
        roomLogoRect.pivot = new Vector2(0.5f, 0.5f);
        roomLogoImage = go.GetComponent<Image>();
        roomLogoImage.sprite = sprite;
        roomLogoImage.raycastTarget = false;
        roomLogoImage.preserveAspect = true;
        if (sceneLogoRect != null) roomLogoRect.SetSiblingIndex(sceneLogoRect.GetSiblingIndex() + 1);
    }

    private void BuildHero()
    {
        if (heroImage == null)
        {
            Sprite sprite = Room != null ? Room.heroSprite : null;
            if (sprite == null) return;
            GameObject go = new GameObject("Hero", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = gameObject.layer;
            heroRect = (RectTransform)go.transform;
            heroRect.SetParent(transform, false);
            heroRect.anchorMin = heroRect.anchorMax = new Vector2(1f, 0f);
            heroRect.pivot = new Vector2(1f, 0f);
            heroImage = go.GetComponent<Image>();
            heroImage.sprite = sprite;
            heroImage.raycastTarget = false;
            heroImage.preserveAspect = true;
        }
        // 画面高さの約 60%(1080 の 0.60 = 648px)。原寸 709x1024 の比を保つ。
        const float heroH = 648f;
        float heroW = heroH * 709f / 1024f;
        heroRect.sizeDelta = new Vector2(heroW, heroH);
        heroBasePos = new Vector2(-26f, -8f);
        heroRect.anchoredPosition = heroBasePos;
        heroRect.SetSiblingIndex(2); // 部屋と暗幕の上・ロゴ/メニューの下
        heroImage.gameObject.SetActive(true);
    }

    // ---- オブジェクト上の▼メニュー ----------------------------------------

    // ▼とラベルを 6 個(メニュー4 + 1P/2P)組む。位置は毎フレーム UpdateObjectMenu が
    // 部屋カメラの投影から決めるので、ここでは形だけ作る。
    private void BuildObjectMenu()
    {
        if (objectMenuRoot != null) return;

        GameObject rootObj = new GameObject("ObjectMenu", typeof(RectTransform));
        rootObj.layer = gameObject.layer;
        objectMenuRoot = (RectTransform)rootObj.transform;
        objectMenuRoot.SetParent(transform, false);
        objectMenuRoot.anchorMin = objectMenuRoot.anchorMax = new Vector2(0.5f, 0.5f);
        objectMenuRoot.anchoredPosition = Vector2.zero;
        objectMenuRoot.sizeDelta = Vector2.zero;

        int n = TitleRoomController.MarkerCount;
        markerRoots = new RectTransform[n];
        markerArrows = new Image[n];
        markerLabelCG = new CanvasGroup[n];
        markerLabelTexts = new TMP_Text[n];
        markerLabelShadows = new TMP_Text[n * MarkerLabelShadowDirs.Length * MarkerLabelShadowRadius.Length];
        markerLabelAlpha = new float[n];

        if (markerArrowSprite == null) markerArrowSprite = CreateDownTriangleSprite(96, 62);

        for (int i = 0; i < n; i++)
        {
            GameObject markerObj = new GameObject("Marker" + i, typeof(RectTransform));
            markerObj.layer = gameObject.layer;
            RectTransform marker = (RectTransform)markerObj.transform;
            marker.SetParent(objectMenuRoot, false);
            marker.anchorMin = marker.anchorMax = new Vector2(0.5f, 0.5f);
            marker.pivot = new Vector2(0.5f, 0.5f);
            marker.sizeDelta = Vector2.zero;
            markerRoots[i] = marker;

            // 縁取り(暗い三角)を先に置いて、その上に白い▼を重ねる。
            Image arrowEdge = CreatePanel("ArrowEdge", marker,
                new Vector2(0f, -MarkerArrowEdgeDrop),
                new Vector2(MarkerArrowW * MarkerArrowEdgeScale, MarkerArrowH * MarkerArrowEdgeScale),
                MarkerArrowEdge);
            arrowEdge.sprite = markerArrowSprite;
            arrowEdge.type = Image.Type.Simple;

            Image arrow = CreatePanel("Arrow", marker, Vector2.zero,
                new Vector2(MarkerArrowW, MarkerArrowH), MarkerArrowInk);
            arrow.sprite = markerArrowSprite;
            arrow.type = Image.Type.Simple;
            markerArrows[i] = arrow;

            // ラベル: 帯も縁も背景板も置かず、明朝体の文字と 1px の影だけ。
            string text = i < MarkerLabels.Length ? MarkerLabels[i] : string.Empty;
            float bandW = MarkerLabelBoxW;
            GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(CanvasGroup));
            labelObj.layer = gameObject.layer;
            RectTransform label = (RectTransform)labelObj.transform;
            label.SetParent(marker, false);
            label.anchorMin = label.anchorMax = new Vector2(0.5f, 0.5f);
            label.pivot = new Vector2(0.5f, 0.5f);
            Vector2 labelNudge = i < MarkerLabelOffset.Length ? MarkerLabelOffset[i] : Vector2.zero;
            label.anchoredPosition = new Vector2(labelNudge.x, MarkerLabelGap + labelNudge.y);
            label.sizeDelta = new Vector2(bandW, MarkerLabelH);
            CanvasGroup cg = labelObj.GetComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            cg.interactable = false;
            markerLabelCG[i] = cg;

            int shadowsPerLabel = MarkerLabelShadowDirs.Length * MarkerLabelShadowRadius.Length;
            // 外側のリングを先に作る(先に作った子ほど奥に描かれる)。
            for (int r = 0; r < MarkerLabelShadowRadius.Length; r++)
            {
                Color sc = MarkerLabelShadow;
                sc.a = MarkerLabelShadowAlpha[r];
                for (int k = 0; k < MarkerLabelShadowDirs.Length; k++)
                {
                    TMP_Text shadow = CreateText("Shadow" + r + "_" + k, label,
                        MarkerLabelShadowDirs[k] * MarkerLabelShadowRadius[r],
                        new Vector2(bandW, MarkerLabelH), MarkerLabelFont, sc,
                        TextAlignmentOptions.Center);
                    StyleMarkerLabel(shadow, text);
                    markerLabelShadows[i * shadowsPerLabel + r * MarkerLabelShadowDirs.Length + k] = shadow;
                }
            }

            TMP_Text ink = CreateText("Text", label, new Vector2(0f, 0f),
                new Vector2(bandW, MarkerLabelH), MarkerLabelFont, MarkerLabelInk,
                TextAlignmentOptions.Center);
            StyleMarkerLabel(ink, text);
            markerLabelTexts[i] = ink;
        }
        objectMenuRoot.SetAsLastSibling();
    }

    // ラベル 1 枚(本体・影とも)に明朝体・字間・折り返し無しを適用する。
    private void StyleMarkerLabel(TMP_Text label, string text)
    {
        if (label == null) return;
        TMP_FontAsset mincho = MinchoFont;
        if (mincho != null) label.font = mincho;
        label.characterSpacing = MarkerLabelSpacing;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.text = text;
    }

    // 明朝体は Resources から 1 度だけ読む(見つからなければ uiFont のまま)。
    private TMP_FontAsset MinchoFont
    {
        get
        {
            if (!minchoFontTried)
            {
                minchoFontTried = true;
                minchoFont = Resources.Load<TMP_FontAsset>(MinchoFontResource);
                if (minchoFont == null)
                    Debug.LogWarning("TitleManager: 明朝フォントが見つかりません: " + MinchoFontResource);
            }
            return minchoFont;
        }
    }

    // 下向きの三角(▼)。フォントの ▼ は UI フォントに収録が無く豆腐になるため、
    // アンチエイリアス付きのスプライトを自前で焼く(◀▶ 豆腐と同じ既知の罠)。
    private static Sprite CreateDownTriangleSprite(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.DontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        const int ss = 4; // 4x4 スーパーサンプル
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int hit = 0;
                for (int sy = 0; sy < ss; sy++)
                {
                    for (int sx = 0; sx < ss; sx++)
                    {
                        float px = (x + (sx + 0.5f) / ss) / w;          // 0..1 左→右
                        float py = (y + (sy + 0.5f) / ss) / h;          // 0..1 下→上
                        // 上辺が幅いっぱい、下端が頂点の二等辺三角形。
                        float half = 0.5f * py;
                        if (Mathf.Abs(px - 0.5f) <= half) hit++;
                    }
                }
                float a = hit / (float)(ss * ss);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f);
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    // ▼とラベルを部屋のオブジェクトの上へ置き直す(毎フレーム)。
    private void UpdateObjectMenu(float dt)
    {
        if (objectMenuRoot == null) return;
        TitleRoomController room = Room;
        if (room == null || !room.RoomVisible)
        {
            if (objectMenuRoot.gameObject.activeSelf) objectMenuRoot.gameObject.SetActive(false);
            return;
        }
        if (!objectMenuRoot.gameObject.activeSelf) objectMenuRoot.gameObject.SetActive(true);

        // 和文ラベルの光学中央合わせは、表示後にメッシュが生成されてからでないと空振る。
        if (!markerInkCentered)
        {
            bool all = true;
            foreach (TMP_Text t in markerLabelTexts)
                if (t != null) all &= TmpAlign.CenterInkVertically(t);
            foreach (TMP_Text t in markerLabelShadows)
                if (t != null) all &= TmpAlign.CenterInkVertically(t);
            markerInkCentered = all;
        }

        Rect canvas = ((RectTransform)transform).rect;
        // 寄り切ってパネルが出る直前に消す(寄っている途中は対象に追従したまま見せる)。
        float zoomFade = 1f - Mathf.Clamp01((room.ZoomAmount - 0.82f) / 0.18f);

        for (int i = 0; i < markerRoots.Length; i++)
        {
            RectTransform marker = markerRoots[i];
            if (marker == null) continue;
            bool onScreen = room.TryGetMarkerViewport(i, out Vector2 vp);
            if (marker.gameObject.activeSelf != onScreen) marker.gameObject.SetActive(onScreen);
            if (!onScreen) continue;

            float floatY = Mathf.Sin(animTime * 1.9f + i * 0.9f) * MarkerFloatPx;
            Vector2 nudge = i < MarkerScreenOffset.Length ? MarkerScreenOffset[i] : Vector2.zero;
            marker.anchoredPosition = new Vector2(
                (vp.x - 0.5f) * canvas.width + nudge.x,
                (vp.y - 0.5f) * canvas.height + MarkerLift + floatY + nudge.y);

            bool selected = i < TitleRoomController.MenuCount
                ? i == menuIndex
                : (i == TitleRoomController.MarkerP1) != pcTwoPlayer;
            markerLabelAlpha[i] = Mathf.MoveTowards(markerLabelAlpha[i], selected ? 1f : 0f,
                dt * MarkerFadeSpeed);
            if (markerLabelCG[i] != null) markerLabelCG[i].alpha = markerLabelAlpha[i] * zoomFade;
            if (markerArrows[i] != null)
            {
                Color c = Color.Lerp(MarkerArrowInkDim, MarkerArrowInk, markerLabelAlpha[i]);
                c.a *= zoomFade;
                markerArrows[i].color = c;
                float s = Mathf.Lerp(1f, 1.18f, markerLabelAlpha[i]);
                markerArrows[i].rectTransform.localScale = new Vector3(s, s, 1f);
            }
        }
    }

    // 左端 alpha0.5 → 右端 alpha0 の横グラデ。文字の可読性用の薄い暗幕。
    private static Sprite CreateHorizontalFadeSprite()
    {
        const int w = 128;
        Texture2D tex = new Texture2D(w, 4, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.DontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        for (int x = 0; x < w; x++)
        {
            float u = x / (float)(w - 1);
            // 左(u=0)が濃く、右へ滑らかに抜ける。
            float a = 0.5f * Mathf.Pow(1f - u, 1.35f);
            Color c = new Color(1f, 1f, 1f, a);
            for (int y = 0; y < 4; y++) tex.SetPixel(x, y, c);
        }
        tex.Apply();
        Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, w, 4f), new Vector2(0.5f, 0.5f), 100f);
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    // 部屋のカメラを寄せる/戻す(決定・戻る)。
    private void FocusRoom(int menuIndexValue) => Room?.FocusMenu(menuIndexValue);
    private void UnfocusRoom() => Room?.ClearFocus();

    // GManager の設定画面オープン/クローズから呼ぶ(ランタンへ寄る/戻る)。
    public void OnOptionsOpened() => FocusRoom((int)TitleMenuAction.Options);
    public void OnOptionsClosed() => UnfocusRoom();

    private void ShutdownRoom()
    {
        Room?.SetRoomActive(false);
    }

    // 部屋の毎フレーム更新。選択中のオブジェクトのハイライトと、立ち絵の視差・退避。
    private void TickRoom(float dt)
    {
        TitleRoomController room = Room;
        if (room == null || !room.Ready) return;
        room.SetSelection(menuIndex);
        room.Tick(dt);
        UpdateObjectMenu(dt);
        if (!roomLayout) return;

        // ドット風表示の解像度を切り替えると部屋の描画先 RT ごと差し替わるので、
        // 表示板の texture / material を毎フレーム追従させる(通常は変化しない)。
        if (roomView != null)
        {
            if (roomView.texture != room.Texture) roomView.texture = room.Texture;
            // RawImage.material は null を入れると既定マテリアルを返すので、
            // 直接比較せず「最後に入れた値」を覚えて変化したときだけ差し替える。
            if (appliedRoomMaterial != room.ViewMaterial)
            {
                appliedRoomMaterial = room.ViewMaterial;
                roomView.material = appliedRoomMaterial;
            }
        }

        float zoom = room.ZoomAmount;
        if (heroRect != null)
        {
            // 全景では左右に数 px だけ揺れる視差。寄っているあいだは右へ逃がして消す。
            float drift = Mathf.Sin(animTime * 0.5f) * HeroParallaxPx;
            heroRect.anchoredPosition = heroBasePos + new Vector2(drift + zoom * 220f, 0f);
            if (heroImage != null)
            {
                Color c = heroImage.color;
                c.a = 1f - Mathf.Clamp01(zoom * 1.6f);
                heroImage.color = c;
            }
        }
        if (roomScrim != null)
        {
            // 寄っているときはパネルが主役なので暗幕を薄める。
            Color c = roomScrim.color;
            c.a = Mathf.Lerp(1f, 0.35f, zoom);
            roomScrim.color = c;
        }
    }

    private void EnsureUiBuilt()
    {
        if (uiFont == null)
        {
            uiFont = promptText != null ? promptText.font : TMP_Settings.defaultFontAsset;
        }
        if (menuRoot == null) BuildMenu();
        if (transferRoot == null) BuildTransferPanel();
        if (rankingRoot == null) BuildRankingPanel();

        menuIndex = 0;
        transferOpen = false;
        rankingOpen = false;
        for (int i = 0; i < menuItemSel.Length; i++)
        {
            menuItemSel[i] = i == 0 ? 1f : 0f;
            ApplyMenuRowState(i, menuItemSel[i]);
        }
        if (menuWhite != null && menuRowY.Length > 0)
        {
            menuWhiteY = menuRowY[0];
            menuWhite.anchoredPosition = new Vector2(0f, menuWhiteY);
        }
        if (transferRoot != null) transferRoot.SetActive(false);
        if (rankingRoot != null) rankingRoot.SetActive(false);
        SetLegacyMenuVisible(true);

        // The scene-authored "PRESS ANY BUTTON" prompt is replaced by the menu.
        if (promptText != null)
        {
            promptText.gameObject.SetActive(false);
            promptText = null;
        }
    }

    // Returning from a quit-play scene reload: the title rushes toward the
    // viewer, overshoots slightly, then settles as the pixel cover clears.
    // (PixelTransition drives this; the title-options close path does not.)
    public void PrepareReturnEntrance()
    {
        EnsureReturnBackdrop();
        returnBackdrop.gameObject.SetActive(true);
        int titleSiblingIndex = transform.GetSiblingIndex();
        if (returnBackdrop.transform.GetSiblingIndex() > titleSiblingIndex)
        {
            returnBackdrop.transform.SetSiblingIndex(titleSiblingIndex);
        }
        returnAnimating = true;
        group.alpha = 1f;
        transform.localScale = Vector3.one * 0.78f;
        for (int i = 0; i < shapes.Length; i++)
        {
            if (shapes[i].rect != null) shapes[i].rect.anchoredPosition = shapes[i].basePos;
        }
        if (logoRect != null)
        {
            logoRect.localScale = Vector3.one;
            logoRect.anchoredPosition = new Vector2(logoRect.anchoredPosition.x, logoBaseY);
        }
        if (promptText != null) promptText.alpha = 0f;
    }

    public async void PlayReturnEntrance()
    {
        if (!returnAnimating) PrepareReturnEntrance();
        const float delay = 0.01f;
        float wait = 0f;
        while (wait < delay)
        {
            wait += Time.unscaledDeltaTime;
            await Task.Yield();
            if (this == null) return;
        }

        const float duration = 0.30f;
        float time = 0f;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(time / duration);
            float q = p - 1f;
            float easeOutBack = 1f + 2.2f * q * q * q + 1.2f * q * q;
            transform.localScale = Vector3.one * Mathf.LerpUnclamped(0.78f, 1f, easeOutBack);
            if (promptText != null) promptText.alpha = Mathf.Clamp01((p - 0.45f) / 0.4f);
            await Task.Yield();
            if (this == null || group == null) return;
        }
        if (logoRect != null)
        {
            logoRect.localScale = Vector3.one;
            logoRect.anchoredPosition = new Vector2(logoRect.anchoredPosition.x, logoBaseY);
        }
        transform.localScale = Vector3.one;
        group.alpha = 1f;
        animTime = 0f;
        beatTimer = 0f;
        beatPulse = 0f;
        returnAnimating = false;
        if (returnBackdrop != null) returnBackdrop.gameObject.SetActive(false);
    }

    private void EnsureReturnBackdrop()
    {
        if (returnBackdrop != null) return;

        Transform parent = transform.parent;
        Transform existing = parent != null ? parent.Find("TitleReturnBackdrop") : null;
        if (existing != null)
        {
            returnBackdrop = existing.GetComponent<Image>();
            if (returnBackdrop != null) return;
        }

        GameObject backdrop = new GameObject(
            "TitleReturnBackdrop",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform rect = backdrop.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;

        returnBackdrop = backdrop.GetComponent<Image>();
        returnBackdrop.color = new Color(0.018f, 0.02f, 0.035f, 1f);
        returnBackdrop.raycastTarget = false;
    }

    // タイトル BGM(Discotheque)を AManager 常駐 BGMSource でループ再生する。
    // 戻り値の AudioSource を保持し、UpdateTitle でロゴの拍を音に同期させる。
    // クリップは Resources/BGM/title_discotheque(1:50 でなく本編の頭 2.763s から
    // ダウンビート単位でトリム、末尾フェードは除去済みで自然にループ)。
    private async void StartTitleBgm(bool fadeIn = false)
    {
        AudioManager am = GManager.Control != null ? GManager.Control.AManager : null;
        if (am == null) return;
        AudioClip clip = Resources.Load<AudioClip>("BGM/title_discotheque");
        if (clip == null)
        {
            Debug.LogWarning("Title BGM clip not found: Resources/BGM/title_discotheque");
            return;
        }
        // fadeIn 時は音量 0 で再生開始し、直後に FadeInBGM で立ち上げる。
        AudioSource src = await am.PlayLoopingBGM(clip, fadeIn ? 0f : TitleBgmVolume);
        // 復帰などで await 中に破棄された場合の保険。
        if (this != null) titleBgmSource = src;
        if (fadeIn && src != null) am.FadeInBGM(TitleBgmVolume, TitleBgmFadeIn);
    }

    // リザルトから選択画面へ戻ったときに、共有 BGMSource で止まっているタイトル
    // BGM(Discotheque)を静かに再開する(2026-07-13 指摘: リザルト後の選択画面が
    // 無音になる問題への対策)。TitleManager の GameObject が非表示でも、再生自体は
    // 常駐 AManager 上で走るため呼び出せる。
    public void EnsureTitleBgm()
    {
        StartTitleBgm(fadeIn: true);
    }

    // Idle animation: prompt blinks, logo floats and bounces on the beat,
    // background shapes drift, spin and flash slightly in time with the BPM.
    public void UpdateTitle(float dt)
    {
        if (dismissed || returnAnimating) return;

        // メニューの光学中央補正はビルド時(シーンロード中)には TMP が文字を
        // 生成できず空振りすることがある(第30便で実測 8〜11px の上ずれ)。
        // 表示中の最初のフレームで測定できるようになってから確定させる。
        if (!menuInkCentered && menuItems != null)
        {
            bool all = true;
            foreach (TMP_Text item in menuItems)
            {
                if (item != null) all &= TmpAlign.CenterInkVertically(item);
            }
            menuInkCentered = all;
        }

        animTime += dt;
        TickRoom(dt);

        // ビートパルス(ロゴの振動・図形フラッシュ): Discotheque が再生中なら
        // 再生位置から拍位相を取って音にロックする(clip の t=0=実ダウンビート)。
        // BGM 未再生(ロード中/失敗)のときだけ従来の自走タイマーで代替する。
        float beatInterval = 60f / Mathf.Max(1f, bpm);
        if (titleBgmSource != null && titleBgmSource.isPlaying && titleBgmSource.clip != null)
        {
            float beats = titleBgmSource.time / beatInterval;
            float frac = beats - Mathf.Floor(beats);         // 0=拍頭 → 1=次拍直前
            beatPulse = Mathf.Max(0f, 1f - frac / BeatPulseDecayFrac);
        }
        else
        {
            beatTimer += dt;
            if (beatTimer >= beatInterval)
            {
                beatTimer -= beatInterval;
                beatPulse = 1f;
            }
            beatPulse = Mathf.Max(0f, beatPulse - dt * 5f);
        }

        if (promptText != null)
        {
            promptText.alpha = 0.35f + 0.55f * (0.5f + 0.5f * Mathf.Sin(animTime * 3.5f));
        }
        if (logoRect != null)
        {
            logoRect.anchoredPosition = new Vector2(logoRect.anchoredPosition.x, logoBaseY + Mathf.Sin(animTime * 1.2f) * 10f);
            logoRect.localScale = Vector3.one * (1f + 0.035f * beatPulse);
        }
        for (int i = 0; i < shapeGraphics.Length; i++)
        {
            if (shapeGraphics[i] == null) continue;
            Color c = shapeGraphics[i].color;
            c.a = Mathf.Min(1f, shapeBaseAlphas[i] * (1f + 0.65f * beatPulse));
            shapeGraphics[i].color = c;
        }
        for (int i = 0; i < shapes.Length; i++)
        {
            ShapeAnim s = shapes[i];
            if (s.rect == null) continue;
            s.rect.anchoredPosition = s.basePos + new Vector2(
                (Mathf.Sin(animTime * s.speedX + s.phase) - Mathf.Sin(s.phase)) * s.ampX,
                (Mathf.Cos(animTime * s.speedY + s.phase * 1.3f) - Mathf.Cos(s.phase * 1.3f)) * s.ampY);
            s.rect.Rotate(0f, 0f, s.rotSpeed * dt);
        }
    }

    // Dive "through" the title when the player presses the button: the camera
    // rushes in (cubic ease-in zoom) and the screen blows past the viewer,
    // fading only near the end so the acceleration reads clearly.
    public async void Dismiss()
    {
        if (dismissed) return;
        dismissed = true;
        ShutdownRoom();
        const float duration = 0.38f;
        float d = duration;
        while (d > 0f)
        {
            d -= Time.deltaTime;
            float t = 1f - Mathf.Clamp01(d / duration);
            transform.localScale = Vector3.one * (1f + 2.2f * t * t * t);
            float fade = Mathf.Clamp01((t - 0.4f) / 0.45f);
            group.alpha = 1f - fade * fade;
            await Task.Yield();
            if (this == null || group == null) return;
        }
        group.alpha = 0f;
        gameObject.SetActive(false);
    }

    // スタート決定の遷移演出: 選択バナーが白フラッシュ+小ポップ→行が右へ
    // 加速して飛び去り(選択行が先頭)、ロゴは上へ抜け、背景図形は加速する。
    // タイトルの背景は StartExitCoverDelay 経過後にステージ選択が重なって
    // くるまで残し、終盤で全体をフェードして交差させる(ハードカット防止)。
    public async void PlayStartExit()
    {
        if (dismissed) return;
        dismissed = true;

        // 部屋がある場合は、退場演出の前に 0.4 秒だけ「地図へ寄る」。
        // GManager は StartZoomLead ぶん遅らせてステージ選択を重ね始める。
        FocusRoom((int)TitleMenuAction.Start);
        if (Room != null && Room.Ready)
        {
            float lead = 0f;
            while (lead < StartZoomLead)
            {
                float ldt = Time.deltaTime;
                lead += ldt;
                TickRoom(ldt);
                await Task.Yield();
                if (this == null || group == null) return;
            }
        }

        const float flashDur = 0.175f;
        const float rowDur = 0.325f;
        const float slideDistance = 1500f;
        const float logoDelay = 0.125f;
        const float logoDur = 0.475f;
        const float fadeStart = 0.475f;

        int selected = Mathf.Clamp(menuIndex, 0, menuItemRects.Length > 0 ? menuItemRects.Length - 1 : 0);
        beatPulse = 1f; // 決定と同時に図形をひと光りさせる

        float time = 0f;
        while (time < StartExitTotal)
        {
            float dt = Time.deltaTime;
            time += dt;
            TickRoom(dt);

            // 背景図形は加速しながら流れ続ける(dismissed 中は UpdateTitle が
            // 止まるので、同じ式をここで加速倍率付きで駆動する)。
            float speedMul = Mathf.Lerp(1f, 6f, Mathf.Clamp01(time / StartExitTotal));
            animTime += dt * speedMul;
            beatPulse = Mathf.Max(0f, beatPulse - dt * 5f);
            for (int i = 0; i < shapeGraphics.Length; i++)
            {
                if (shapeGraphics[i] == null) continue;
                Color c = shapeGraphics[i].color;
                c.a = Mathf.Min(1f, shapeBaseAlphas[i] * (1f + 0.65f * beatPulse));
                shapeGraphics[i].color = c;
            }
            for (int i = 0; i < shapes.Length; i++)
            {
                ShapeAnim s = shapes[i];
                if (s.rect == null) continue;
                s.rect.anchoredPosition = s.basePos + new Vector2(
                    (Mathf.Sin(animTime * s.speedX + s.phase) - Mathf.Sin(s.phase)) * s.ampX,
                    (Mathf.Cos(animTime * s.speedY + s.phase * 1.3f) - Mathf.Cos(s.phase * 1.3f)) * s.ampY);
                s.rect.Rotate(0f, 0f, s.rotSpeed * dt * speedMul);
            }

            // 選択バナーのフラッシュ(白オーバーレイ減衰)+小ポップ(1→1.06→1)。
            // 文字は白地に飛ばないよう反転(ネイビー→白)させ、決定の一拍を
            // 読めるまま見せる。ピーク0.5は旧「バナー青→白50%」相当。
            float flashP = Mathf.Clamp01(time / flashDur);
            if (selected < menuRowFlash.Length && menuRowFlash[selected] != null)
            {
                menuRowFlash[selected].color = new Color(1f, 1f, 1f, 0.5f * (1f - flashP * flashP));
            }
            if (selected < menuItems.Length && menuItems[selected] != null)
            {
                menuItems[selected].color = Color.Lerp(Navy, Color.white, flashP * flashP);
            }

            // 行スライドアウト: ease-in cubic で緩→急。選択行が先頭で飛び出す。
            for (int i = 0; i < menuItemRects.Length; i++)
            {
                if (menuItemRects[i] == null) continue;
                float delay = i == selected ? 0.125f : 0.2125f + 0.0625f * i;
                float p = Mathf.Clamp01((time - delay) / rowDur);
                float x = p * p * p * slideDistance;
                menuItemRects[i].anchoredPosition = new Vector2(x, menuRowY[i]);
                if (i == selected)
                {
                    float pop = Mathf.Sin(flashP * Mathf.PI) * 0.06f;
                    menuItemRects[i].localScale = Vector3.one * ((0.8f + 0.2f * menuItemSel[i]) + pop);
                    // 白ブラケットは選択行と一体で飛ぶ。
                    if (menuWhite != null) menuWhite.anchoredPosition = new Vector2(x, menuWhiteY);
                }
            }

            // ロゴは上へ加速して画面外に抜ける。
            if (logoRect != null)
            {
                float lp = Mathf.Clamp01((time - logoDelay) / logoDur);
                logoRect.anchoredPosition = new Vector2(
                    logoRect.anchoredPosition.x, logoBaseY + lp * lp * lp * 520f);
            }

            // 覆われ始めてから全体をフェード(選択画面側のフェードインと交差)。
            float fade = Mathf.Clamp01((time - fadeStart) / (StartExitTotal - fadeStart));
            group.alpha = 1f - fade * fade;

            await Task.Yield();
            if (this == null || group == null) return;
        }

        group.alpha = 0f;
        gameObject.SetActive(false);
        ShutdownRoom();
        // 非表示中に退場前の配置へ戻し、次回 Init(再表示)を無傷にする。
        for (int i = 0; i < menuItemRects.Length; i++)
        {
            if (menuItemRects[i] == null) continue;
            menuItemRects[i].anchoredPosition = new Vector2(0f, menuRowY[i]);
            ApplyMenuRowState(i, menuItemSel[i]);
        }
        if (selected < menuRowFlash.Length && menuRowFlash[selected] != null)
        {
            menuRowFlash[selected].color = new Color(1f, 1f, 1f, 0f);
        }
        if (menuWhite != null) menuWhite.anchoredPosition = new Vector2(0f, menuWhiteY);
        if (logoRect != null)
        {
            logoRect.anchoredPosition = new Vector2(logoRect.anchoredPosition.x, logoBaseY);
        }
    }

    // ---- Menu -------------------------------------------------------------

    public void ShowMenu()
    {
        SetLegacyMenuVisible(true);
        SetPlayerCountToggleVisible(true);
        if (titleControlGuideRoot != null) titleControlGuideRoot.gameObject.SetActive(true);

    }

    public void HideMenu()
    {
        SetLegacyMenuVisible(false);
        SetPlayerCountToggleVisible(false);
        if (objectMenuRoot != null) objectMenuRoot.gameObject.SetActive(false);
        if (titleControlGuideRoot != null) titleControlGuideRoot.gameObject.SetActive(false);

    }

    // Navigate + animate the vertical menu. Selection mirrors DefficultyBox
    // exactly (alpha 0.4→1, scale 0.8→1, text toward white) and the cloned white
    // slash brackets glide to the selected row like DefficultyBar.Tick.
    public void UpdateMenu(float dt, bool up, bool down)
    {
        if (transferOpen || menuItems.Length == 0) return;

        // 無効行(引き継ぎ等)は飛ばして、その方向にある最も近い有効行へ移す。
        if (up) menuIndex = StepMenuIndex(menuIndex - 1, -1);
        else if (down) menuIndex = StepMenuIndex(menuIndex + 1, +1);

        float follow = 1f - Mathf.Exp(-14f * dt);
        for (int i = 0; i < menuItems.Length; i++)
        {
            float target = i == menuIndex ? 1f : 0f;
            menuItemSel[i] = Mathf.Abs(target - menuItemSel[i]) < 0.001f
                ? target
                : Mathf.Lerp(menuItemSel[i], target, follow);
            ApplyMenuRowState(i, menuItemSel[i]);
        }

        if (menuWhite != null && menuRowY.Length > menuIndex)
        {
            float targetY = menuRowY[menuIndex];
            menuWhiteY = Mathf.Abs(targetY - menuWhiteY) < 0.5f
                ? targetY
                : Mathf.Lerp(menuWhiteY, targetY, 1f - Mathf.Exp(-16f * dt));
            menuWhite.anchoredPosition = new Vector2(0f, menuWhiteY);
        }
    }

    // Same visual state math as DefficultyBox.SetPosition.
    private void ApplyMenuRowState(int i, float progress)
    {
        // 無効行は選択に反応させず、灰色(disabled 見た目)で固定する。
        if (!IsRowEnabled(i))
        {
            if (menuRowCG[i] != null) menuRowCG[i].alpha = 0.32f;
            if (menuItemRects[i] != null) menuItemRects[i].localScale = Vector3.one * 0.8f;
            if (menuItems[i] != null) menuItems[i].color = MenuDisabledText;
            if (menuRowBars[i] != null) menuRowBars[i].color = MenuDisabledBar;
            return;
        }
        if (menuRowBars[i] != null) menuRowBars[i].color = Color.white; // 有効行は焼き込みそのまま
        if (menuRowCG[i] != null) menuRowCG[i].alpha = 0.4f + 0.6f * progress;
        if (menuItemRects[i] != null) menuItemRects[i].localScale = Vector3.one * (0.8f + 0.2f * progress);
        if (menuItems[i] != null) menuItems[i].color = Color.Lerp(MenuTextBase, Color.white, progress);
    }

    private bool IsRowEnabled(int i)
        => menuRowEnabled == null || i < 0 || i >= menuRowEnabled.Length || menuRowEnabled[i];

    // from から dir 方向へ最初の有効行を探す。無ければ現在位置を維持(移動しない)。
    private int StepMenuIndex(int from, int dir)
    {
        for (int i = from; i >= 0 && i < menuItems.Length; i += dir)
            if (IsRowEnabled(i)) return i;
        return menuIndex;
    }

    private void BuildMenu()
    {
        GameObject rootObj = new GameObject("Menu", typeof(RectTransform));
        rootObj.layer = gameObject.layer;
        menuRoot = (RectTransform)rootObj.transform;
        menuRoot.SetParent(transform, false);
        menuRoot.anchorMin = menuRoot.anchorMax = new Vector2(0.5f, 0.5f);
        menuRoot.anchoredPosition = Vector2.zero;
        menuRoot.sizeDelta = new Vector2(700f, 500f);

        // ランキング+引き継ぎ便(2026-07-16 SPEC)で4行に確定。「スタート/設定/引き継ぎ」
        // は既発行(07-13以前)の3行版と同じ並びを維持し、末尾に「ランキング」を追加する。
        string[] labels = { "スタート", "設定", "引き継ぎ", "ランキング" };
        // 4行を安全に収めるため、難易度規格(660x160・行間172)から本メニュー専用に
        // 縮小する(MenuRowH/MenuRowGap)。ロゴ下端(実測 y≈-26)からの上端クリアランスは
        // 旧3行版の実測(約50px)と同じ比率になるよう rowCenter を逆算している。
        float rowGap = MenuRowGap;
        float rowCenter = MenuRowCenter;
        float[] rowY = new float[labels.Length];
        for (int i = 0; i < labels.Length; i++)
            rowY[i] = rowCenter + (labels.Length - 1) * rowGap * 0.5f - i * rowGap;

        // The real difficulty-select column lives next to the title in the same
        // canvas; clone its row (banner + label) and white brackets so the title
        // menu is literally the same parts, not a lookalike.
        Transform diffSrc = transform.parent != null ? transform.parent.Find("StageBoxParent/DefficultyBar") : null;
        Transform rowSrc = diffSrc != null ? diffSrc.Find("List/Normal") : null;
        Transform whiteSrc = diffSrc != null ? diffSrc.Find("White") : null;

        menuItems = new TMP_Text[labels.Length];
        menuItemRects = new RectTransform[labels.Length];
        menuRowCG = new CanvasGroup[labels.Length];
        menuRowBars = new Image[labels.Length];
        menuRowFlash = new ParallelogramGraphic[labels.Length];
        menuItemSel = new float[labels.Length];
        menuRowY = rowY;

        // 各行の有効/無効。設計確定(SPEC)につき全行有効。
        menuRowEnabled = new bool[labels.Length];
        for (int i = 0; i < labels.Length; i++) menuRowEnabled[i] = true;

        // リザルト様式のボタン本体を本メニュー専用サイズ(MenuRowW x MenuRowH)で焼く。
        if (menuButtonSprite == null)
            menuButtonSprite = UiButtonStyle.CreateBodySprite(
                (int)MenuRowW, (int)MenuRowH, null, null, "TitleMenuButton");

        for (int i = 0; i < labels.Length; i++)
        {
            RectTransform row;
            TMP_Text label;
            if (rowSrc != null)
            {
                GameObject rowObj = Instantiate(rowSrc.gameObject, menuRoot);
                rowObj.name = "Item" + i;
                rowObj.SetActive(true);
                row = (RectTransform)rowObj.transform;
                Image bar = rowObj.transform.Find("StageBar")?.GetComponent<Image>();
                if (bar != null)
                {
                    bar.sprite = menuButtonSprite;
                    bar.type = Image.Type.Simple;
                    bar.color = Color.white;
                    // クローン元(シーンの DefficultyBar)は起動順の都合で先に
                    // Init 済みで StageBar が拡大寸法に上書きされている。タイトル行は
                    // 本メニュー専用サイズへ明示的に設定する。
                    bar.rectTransform.sizeDelta = new Vector2(MenuRowW, MenuRowH);
                    menuRowFlash[i] = CreateRowFlash(bar.rectTransform);
                }
                menuRowBars[i] = bar;
                label = rowObj.transform.Find("StageName")?.GetComponent<TMP_Text>();
            }
            else
            {
                // Degraded fallback (scene layout changed): plain banner + label.
                row = new GameObject("Item" + i, typeof(RectTransform)).GetComponent<RectTransform>();
                row.SetParent(menuRoot, false);
                Vector2 btnSize = new Vector2(MenuRowW, MenuRowH);
                menuRowBars[i] = CreatePanel("StageBar", row, Vector2.zero, btnSize, Color.white);
                menuRowBars[i].sprite = menuButtonSprite;
                menuRowFlash[i] = CreateRowFlash(menuRowBars[i].rectTransform);
                label = CreateText("StageName", row, Vector2.zero, btnSize, UiButtonStyle.LabelSizeDifficulty, MenuTextBase, TextAlignmentOptions.Center);
            }

            row.anchorMin = row.anchorMax = new Vector2(0.5f, 0.5f);
            row.pivot = new Vector2(0.5f, 0.5f);
            row.anchoredPosition = new Vector2(0f, rowY[i]);

            CanvasGroup cg = row.GetComponent<CanvasGroup>();
            if (cg == null) cg = row.gameObject.AddComponent<CanvasGroup>();

            // 行付属の灰スラッシュ(クローン元 sprite・角度約18°)はリザルト様式の
            // 細スラッシュ(19°・2.5px・白α0.5)へ差し替え、上下端をボタンの
            // 上下辺(焼き込み枠)に合わせる(2026-07-11 指摘)。x は共通則
            // ThinSlashX でボタン枠に密着させる(2026-07-11 指摘「離れすぎ」)。
            // 行 CanvasGroup の減光には子としてそのまま追従する。
            // RowSlashL/R は Init 済みクローン元から継承した難易度画面幅基準の
            // スラッシュ(位置が合わない)。無効化して下で 583 幅基準を付け直す。
            foreach (string grayName in new[] { "Gray_L", "Gray_R", "RowSlashL", "RowSlashR" })
            {
                Transform gray = row.Find(grayName);
                if (gray != null) gray.gameObject.SetActive(false);
            }
            float rowThinX = UiButtonStyle.ThinSlashX(MenuRowW);
            UiButtonStyle.AddSlash(row, "RowSlashL", new Color(1f, 1f, 1f, 0.5f),
                -rowThinX, 2.5f, UiButtonStyle.SlashHeight(MenuRowH));
            UiButtonStyle.AddSlash(row, "RowSlashR", new Color(1f, 1f, 1f, 0.5f),
                rowThinX, 2.5f, UiButtonStyle.SlashHeight(MenuRowH));

            if (label != null)
            {
                label.text = labels[i];
                // ラベルサイズは難易度ボタンと同じ規格へ統一(2026-07-13)。
                label.fontSize = UiButtonStyle.LabelSizeDifficulty;
                // Japanese labels ride high under Middle alignment (Latin UI font
                // + CJK fallback metrics); optically center them in the banner.
                TmpAlign.CenterInkVertically(label);
            }

            menuItems[i] = label;
            menuItemRects[i] = row;
            menuRowCG[i] = cg;
            menuItemSel[i] = i == 0 ? 1f : 0f;
            ApplyMenuRowState(i, menuItemSel[i]);
        }

        if (whiteSrc != null)
        {
            GameObject whiteObj = Instantiate(whiteSrc.gameObject, menuRoot);
            whiteObj.name = "White";
            whiteObj.SetActive(true);
            menuWhite = (RectTransform)whiteObj.transform;
            CanvasGroup whiteCG = whiteObj.GetComponent<CanvasGroup>();
            if (whiteCG != null) whiteCG.alpha = 1f;
            // クローン元の White は難易度画面の Init 済みで、難易度ボタン幅基準の
            // MarkerSlashL/R が付いている(起動順: SSManager.Init→ここ)。継承分を
            // 残すとマーカーが二重に見える(2026-07-11 実フレームで確認)ため無効化し、
            // タイトル幅(583)基準の自前スラッシュだけを使う。
            foreach (string inheritedName in new[] { "MarkerSlashL", "MarkerSlashR" })
            {
                Transform inherited = menuWhite.Find(inheritedName);
                if (inherited != null) inherited.gameObject.SetActive(false);
            }
            // 選択マーカーの白スラッシュ(クローン元 sprite・角度約21°)を
            // リザルト様式の太スラッシュ(19°・11px)へ差し替え、上下端を
            // ボタンの上下辺(焼き込み枠)に合わせる(2026-07-11 指摘)。
            // x は共通則 ThickSlashX でボタン枠のすぐ外に密着させる
            // (2026-07-11 指摘「離れすぎ」。クローン元のバナー外側配置を廃止)。
            foreach (string slashName in new[] { "White_L", "White_R" })
            {
                RectTransform slash = menuWhite.Find(slashName) as RectTransform;
                if (slash == null) continue;
                float slashX = Mathf.Sign(slash.anchoredPosition.x)
                    * UiButtonStyle.ThickSlashX(MenuRowW);
                slash.gameObject.SetActive(false);
                UiButtonStyle.AddSlash(menuWhite, slashName + "19", Color.white,
                    slashX, 11f, UiButtonStyle.SlashHeight(MenuRowH));
            }
            // Shine 用マスクの mask graphic は旧様式 SimpleBar(矩形枠)で、
            // 選択行に「横方向の白い線」(矩形の上下辺)を描いてしまう。
            // 描画を止め、マスク形状も焼き込みバナーの平行四辺形に合わせる
            // (2026-07-11 指摘「横方向の白い線もリザルト様式に統一」)。
            Transform shineMask = menuWhite.Find("ShineMask");
            if (shineMask != null)
            {
                Image maskImage = shineMask.GetComponent<Image>();
                if (maskImage != null) maskImage.sprite = menuButtonSprite;
                Mask mask = shineMask.GetComponent<Mask>();
                if (mask != null) mask.showMaskGraphic = false;
            }
            menuWhite.SetAsLastSibling();
            menuWhiteY = rowY[0];
            menuWhite.anchoredPosition = new Vector2(0f, menuWhiteY);
        }

        BuildPlayerCountToggle(rowY.Length > 0 ? rowY[0] : rowCenter + rowGap);
        BuildTitleControlGuide();

    }

    // ロゴとメニュー最上段の間に「1P | 2P」トグルを組む。topRowY はメニュー最上段の y。
    // その少し上(+124)へ 2 セグメントを横並びで置く。焼き込みボタン様式を縮小流用。
    private void BuildPlayerCountToggle(float topRowY)
    {
        GameObject rootObj = new GameObject("PlayerCountToggle", typeof(RectTransform));
        rootObj.layer = gameObject.layer;
        playerCountRoot = (RectTransform)rootObj.transform;
        playerCountRoot.SetParent(transform, false);
        playerCountRoot.anchorMin = playerCountRoot.anchorMax = new Vector2(0.5f, 0.5f);
        playerCountRoot.pivot = new Vector2(0.5f, 0.5f);
        // トグルはメニュー最上段の 1 行分(MenuRowGap)上へ。ロゴと最上段ボタンの中間で
        // 均等間隔になり、ロゴへの重なりを解消(2026-07-14 指摘「ぐちゃぐちゃ」)。
        playerCountRoot.anchoredPosition = new Vector2(0f, topRowY + MenuRowGap - 24f);
        playerCountRoot.sizeDelta = new Vector2(520f, 96f);

        const float segW = 176f;
        const float segH = 78f;
        const float segGap = 24f;
        string[] segText = { "1P", "2P" };
        float[] segX = { -(segW + segGap) * 0.5f, (segW + segGap) * 0.5f };

        // トグル本体は segW:segH と同一アスペクト(=整数4倍 704x312)で焼く。表示は
        // 等倍スケールになるので焼き込み 19° 斜辺が歪まず、追加の細スラッシュ(真の
        // 19°)と平行に揃う。従来の menuButtonSprite(660x160)流用は非等倍ストレッチで
        // 見かけ角が約 10.7° に潰れていた(2026-07-14 指摘「1P/2P の傾きが食い違う」)。
        const int pcBakeScale = 4;
        if (pcToggleSprite == null)
            pcToggleSprite = UiButtonStyle.CreateBodySprite(
                (int)segW * pcBakeScale, (int)segH * pcBakeScale, null, null, "PlayerCountToggleButton");

        for (int i = 0; i < 2; i++)
        {
            GameObject segObj = new GameObject("Seg" + i, typeof(RectTransform));
            segObj.layer = gameObject.layer;
            RectTransform seg = (RectTransform)segObj.transform;
            seg.SetParent(playerCountRoot, false);
            seg.anchorMin = seg.anchorMax = new Vector2(0.5f, 0.5f);
            seg.pivot = new Vector2(0.5f, 0.5f);
            seg.anchoredPosition = new Vector2(segX[i], 0f);
            seg.sizeDelta = new Vector2(segW, segH);

            Image bar = segObj.AddComponent<Image>();
            bar.sprite = pcToggleSprite;   // segW:segH 一致で焼いた本体(等倍表示=19°不変)
            bar.type = Image.Type.Simple;
            bar.raycastTarget = false;
            pcSegBars[i] = bar;

            // 焼き込みボタンと同じ 19° 細スラッシュを左右端に添えてメニューと様式統一。
            float thinX = segW * 0.5f - 8f;
            UiButtonStyle.AddSlash(seg, "PcSlashL", new Color(1f, 1f, 1f, 0.5f), -thinX, 2.2f, segH - 16f);
            UiButtonStyle.AddSlash(seg, "PcSlashR", new Color(1f, 1f, 1f, 0.5f), thinX, 2.2f, segH - 16f);

            GameObject lblObj = new GameObject("Label", typeof(RectTransform));
            lblObj.layer = gameObject.layer;
            RectTransform lblRect = (RectTransform)lblObj.transform;
            lblRect.SetParent(seg, false);
            lblRect.anchorMin = Vector2.zero;
            lblRect.anchorMax = Vector2.one;
            lblRect.offsetMin = lblRect.offsetMax = Vector2.zero;
            TMP_Text lbl = lblObj.AddComponent<TextMeshProUGUI>();
            if (uiFont != null) lbl.font = uiFont;
            lbl.text = segText[i];
            lbl.fontSize = 40f;
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.raycastTarget = false;
            pcSegLabels[i] = lbl;
        }

        // 人数選択は 1P / 2P トグル自体で示す。

        ApplyPlayerCountVisual();
    }

    // タイトル右下に、筐体操作を常時確認できる簡潔なガイドを置く。
    private void BuildTitleControlGuide()
    {
        if (titleControlGuideRoot != null) return;

        GameObject rootObj = new GameObject("TitleControlGuide", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(ParallelogramGraphic));
        rootObj.layer = gameObject.layer;
        titleControlGuideRoot = (RectTransform)rootObj.transform;
        titleControlGuideRoot.SetParent(transform, false);
        titleControlGuideRoot.anchorMin = titleControlGuideRoot.anchorMax = new Vector2(1f, 0f);
        titleControlGuideRoot.pivot = new Vector2(1f, 0f);
        titleControlGuideRoot.anchoredPosition = new Vector2(-42f, 34f);
        titleControlGuideRoot.sizeDelta = new Vector2(530f, 68f);

        ParallelogramGraphic panel = rootObj.GetComponent<ParallelogramGraphic>();
        panel.color = new Color(0.012f, 0.03f, 0.075f, 0.86f);
        panel.Slant = 18f;
        panel.SlantRightEdge = true;
        panel.raycastTarget = false;

        AddTitleGuideIcon("Stick", UiIconFactory.IconKind.Stick, new Vector2(-192f, 0f), new Vector2(78f, 44f));
        TMP_Text select = CreateText("SelectLabel", titleControlGuideRoot,
            new Vector2(-112f, -1f), new Vector2(102f, 44f), 25f,
            new Color(0.86f, 0.93f, 1f, 0.96f), TextAlignmentOptions.MidlineLeft);
        select.text = "で選択";

        GameObject divider = new GameObject("Divider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        divider.layer = gameObject.layer;
        RectTransform dividerRect = (RectTransform)divider.transform;
        dividerRect.SetParent(titleControlGuideRoot, false);
        dividerRect.anchorMin = dividerRect.anchorMax = new Vector2(0.5f, 0.5f);
        dividerRect.anchoredPosition = new Vector2(-38f, 0f);
        dividerRect.sizeDelta = new Vector2(2f, 34f);
        Image dividerImage = divider.GetComponent<Image>();
        dividerImage.color = new Color(0.22f, 0.76f, 0.88f, 0.52f);
        dividerImage.raycastTarget = false;

        AddTitleGuideIcon("ConfirmButton", UiIconFactory.IconKind.Button, new Vector2(25f, 0f), new Vector2(48f, 48f));
        TMP_Text confirm = CreateText("ConfirmLabel", titleControlGuideRoot,
            new Vector2(96f, -1f), new Vector2(112f, 44f), 25f,
            new Color(0.86f, 0.93f, 1f, 0.96f), TextAlignmentOptions.MidlineLeft);
        confirm.text = "で決定";
    }

    private void AddTitleGuideIcon(string objectName, UiIconFactory.IconKind kind, Vector2 position, Vector2 size)
    {
        UiIconFactory.CreateIcon(titleControlGuideRoot, objectName, kind, position, size,
            new Color(0.56f, 0.87f, 1f, 0.98f));
    }

    // トグルの選択状態を見た目に反映(選択セグメント=白/明、非選択=灰/沈む)。
    private void ApplyPlayerCountVisual()
    {
        for (int i = 0; i < 2; i++)
        {
            bool selected = (i == 1) == pcTwoPlayer;
            if (pcSegBars[i] != null) pcSegBars[i].color = selected ? PcSelBar : PcDimBar;
            if (pcSegLabels[i] != null)
            {
                pcSegLabels[i].color = selected ? PcSelText : PcDimText;
                pcSegLabels[i].fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
            }
        }
    }

    // GManager から呼ぶ。人数選択を設定して見た目を更新する。戻り値=変化したか。
    public bool SetTwoPlayer(bool two)
    {
        Room?.SetTwoPlayer(two);   // マントの点灯枚数(1P=左1枚 / 2P=2枚)
        if (pcTwoPlayer == two) return false;
        pcTwoPlayer = two;
        ApplyPlayerCountVisual();
        return true;
    }

    // メニュー表示/非表示に人数トグルも追従させる。
    public void SetPlayerCountToggleVisible(bool visible)
    {
        // ▼メニュー方式では 1P/2P は壁のマントで示すので、トグルは出さない。
        if (playerCountRoot != null) playerCountRoot.gameObject.SetActive(visible && !objectMenuOn);
    }

    // 旧ボタン列の表示。▼メニュー方式が有効なあいだは常に隠す(コードは残す)。
    private void SetLegacyMenuVisible(bool visible)
    {
        if (menuRoot != null) menuRoot.gameObject.SetActive(visible && !objectMenuOn);
        if (objectMenuRoot != null && visible) objectMenuRoot.gameObject.SetActive(true);
    }

    // 決定閃光用の白オーバーレイ。焼き込みバナーの枠(内側 44/22 マージン)と同じ
    // 平行四辺形で、通常は alpha0。閃光時のみ白く光らせる。バナー寸法に追従させる
    // (難易度規格 660x160 なら 616x138)。
    private static ParallelogramGraphic CreateRowFlash(RectTransform barRect)
    {
        GameObject go = new GameObject("Flash", typeof(RectTransform), typeof(CanvasRenderer), typeof(ParallelogramGraphic));
        go.layer = barRect.gameObject.layer;
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(barRect, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        float flashW = barRect.sizeDelta.x - 44f;
        float flashH = barRect.sizeDelta.y - 22f;
        rect.sizeDelta = new Vector2(flashW, flashH);
        ParallelogramGraphic flash = go.GetComponent<ParallelogramGraphic>();
        flash.Slant = flashH * Mathf.Tan(UiButtonStyle.SlashAngleDeg * Mathf.Deg2Rad);
        flash.SlantRightEdge = true;
        flash.color = new Color(1f, 1f, 1f, 0f);
        flash.raycastTarget = false;
        return flash;
    }

    // ---- Transfer panel ---------------------------------------------------

    // 決定した瞬間はまずカメラが対象へ寄り(StartZoomLead 秒)、寄り切ってから
    // パネルが出る。部屋が無いときは従来どおり即座に開く。
    public void OpenTransfer()
    {
        FocusRoom((int)TitleMenuAction.Transfer);
        AfterRoomZoom(OpenTransferNow);
    }

    public void AfterRoomZoom(System.Action act)
    {
        if (act == null) return;
        if (Room == null || !Room.Ready || !isActiveAndEnabled) { act(); return; }
        StartCoroutine(AfterRoomZoomRoutine(act));
    }

    private IEnumerator AfterRoomZoomRoutine(System.Action act)
    {
        float t = 0f;
        while (t < StartZoomLead) { t += Time.unscaledDeltaTime; yield return null; }
        act();
    }

    private void OpenTransferNow()
    {
        if (transferRoot == null) return;
        if (transferCloseRoutine != null) { StopCoroutine(transferCloseRoutine); transferCloseRoutine = null; }
        transferRoot.transform.localScale = Vector3.one; // 閉じるアニメの縮小をリセット
        transferOpen = true;
        // メニュー・ロゴは退場させない。難易度オーバーレイと同様、完成フレーム
        // (メニュー・ロゴを含む)を撮ってぼかし、その上にパネルを重ねる(第31便)。
        transferRoot.SetActive(true);
        transferRoot.transform.SetAsLastSibling();
        // 撮影フレームはパネルを不可視にして背景(タイトル)だけを撮る。撮影後に
        // ぼかしスナップショットを差し込んで表示する。
        if (transferCG != null) transferCG.alpha = 0f;
        if (transferBackdrop != null) transferBackdrop.gameObject.SetActive(false);
        if (transferCaptureRoutine != null) StopCoroutine(transferCaptureRoutine);
        transferCaptureRoutine = StartCoroutine(CaptureTransferBackdrop());
        // 固定ラベルの光学中央補正はビルド時(非アクティブ)に空振りしていることがあるため、
        // 初回オープン時に測り直す(可変テキストは RefreshTransferDigits が毎回再適用)。
        if (!transferInkCentered)
        {
            RectTransform rootRect = (RectTransform)transferRoot.transform;
            bool all = true;
            foreach (string n in new[] { "Heading", "HeadingSub" })
            {
                TMP_Text label = rootRect.Find(n)?.GetComponent<TMP_Text>();
                if (label != null) all &= TmpAlign.CenterInkVertically(label);
            }
            transferInkCentered = all;
        }

        // 方向シーケンス入力状態を初期化(SPEC §1: 開くたびに空の状態から入力)。
        transferState = TransferInputState.Entry;
        transferDigits.Clear();
        transferDirCooldown = 0f;
        transferBackHold.Reset();
        transferCursorBlink = 0f;
        if (transferMessageText != null) transferMessageText.text = string.Empty;
        RefreshTransferDigits();
    }

    public void CloseTransfer()
    {
        transferOpen = false;
        UnfocusRoom();
        if (transferCaptureRoutine != null) { StopCoroutine(transferCaptureRoutine); transferCaptureRoutine = null; }
        // 開くとき(0.14sフェードイン)と対称に、パネル+ぼかし背景をフェードアウト
        // (+わずかに縮小)して閉じる。メニュー・ロゴは隠していないので、フェードの
        // 裏に生きたタイトルが現れて自然に戻る(第34便: 従来は即 SetActive(false)で
        // アニメーション無しだった)。
        if (transferCloseRoutine != null) StopCoroutine(transferCloseRoutine);
        if (transferRoot != null && transferRoot.activeInHierarchy && gameObject.activeInHierarchy)
        {
            transferCloseRoutine = StartCoroutine(CloseTransferRoutine());
        }
        else
        {
            FinishCloseTransfer();
        }
    }

    private IEnumerator CloseTransferRoutine()
    {
        RectTransform rootRect = transferRoot != null ? (RectTransform)transferRoot.transform : null;
        float startAlpha = transferCG != null ? transferCG.alpha : 1f;
        float t = 0f;
        const float dur = 0.14f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / dur);
            if (transferCG != null) transferCG.alpha = startAlpha * (1f - p);
            if (rootRect != null) rootRect.localScale = Vector3.one * Mathf.Lerp(1f, 0.98f, p);
            yield return null;
        }
        transferCloseRoutine = null;
        FinishCloseTransfer();
    }

    private void FinishCloseTransfer()
    {
        if (transferRoot != null)
        {
            transferRoot.transform.localScale = Vector3.one;
            transferRoot.SetActive(false);
        }
        if (transferCG != null) transferCG.alpha = 1f; // 次回オープンは OpenTransfer が 0 から再フェード
        if (transferBackdrop != null) transferBackdrop.texture = null;
        ReleaseBackdropTexture();
        // メニュー・ロゴはそもそも隠していないので再表示は不要(念のため確認)。
        SetLegacyMenuVisible(true);
        if (logoRect != null) logoRect.gameObject.SetActive(true);
    }

    // 引き継ぎパネルを一瞬透明にして背景(タイトル: メニュー・ロゴ・図形)だけを
    // 撮り、ぼかしマテリアル越しに背景として敷く。撮影はフレーム描画後に行う
    // (入力処理中の同期キャプチャは白バッファを返すことがある)。
    private IEnumerator CaptureTransferBackdrop()
    {
        yield return new WaitForEndOfFrame();
        transferCaptureRoutine = null;
        if (!transferOpen) yield break;
        ReleaseBackdropTexture();
        transferBlurRT = BackdropBlurUtil.CapturePyramidBlur();
        if (transferBackdrop != null)
        {
            transferBackdrop.texture = transferBlurRT;
            transferBackdrop.gameObject.SetActive(true);
        }
        // パネルをふわりと出す(急な表示を防ぐ。難易度オーバーレイと同傾向)。
        float t = 0f;
        while (t < 0.14f)
        {
            t += Time.unscaledDeltaTime;
            if (transferCG != null) transferCG.alpha = Mathf.Clamp01(t / 0.14f);
            yield return null;
        }
        if (transferCG != null) transferCG.alpha = 1f;
    }

    private void ReleaseBackdropTexture()
    {
        BackdropBlurUtil.ReleaseRT(ref transferBlurRT);
    }

    // 引き継ぎ画面の毎フレーム入力処理(SPEC §1.2/1.3)。GManager.UpdateTitleTransfer
    // から呼ぶ。戻り値 true は「画面を閉じてタイトルへ戻る」(B長押し完了)。
    // 方向はスティック1回倒し=1記号(150msクールダウン、Entry状態でのみ消費)。
    // A: Entry かつ12桁埋まったら decode を試す(成功→Confirm状態、失敗→エラー表示のまま桁は保持)。
    //    Confirm では反映して Entry へ戻る(全消去はしない=digits は都度クリア)。
    // B: 短押しで1文字削除(Entryかつ桁がある時)/Confirmから戻る。長押し(0.6s)で画面を閉じる。
    public bool TickTransferInput(float dt, bool upEdge, bool downEdge, bool leftEdge, bool rightEdge,
        bool confirmEdge, bool backHeld)
    {
        if (!transferOpen) return false;

        transferCursorBlink += dt;

        // 長押しが閾値に達したフレームで即座に画面を閉じる(離すのを待たない)。
        // 長押しが成立した場合、この画面はここで閉じて以後 Tick されないため、
        // 「離された瞬間=閾値未満だった=短押し」と確定できる。
        if (transferBackHold.Tick(backHeld, dt, 0.6f))
        {
            return true; // 長押し完了: GManager 側で CloseTransfer する
        }

        bool backReleasedShort = transferBackHeldPrev && !backHeld;
        transferBackHeldPrev = backHeld;

        if (backReleasedShort)
        {
            if (transferState == TransferInputState.Confirm)
            {
                transferState = TransferInputState.Entry;
                if (transferMessageText != null) transferMessageText.text = string.Empty;
            }
            else if (transferDigits.Count > 0)
            {
                transferDigits.RemoveAt(transferDigits.Count - 1);
                if (transferMessageText != null) transferMessageText.text = string.Empty;
                RefreshTransferDigits();
            }
        }

        if (transferState == TransferInputState.Entry)
        {
            if (transferDigits.Count < DirectionTransferCode.DigitCount)
            {
                int digit = InputManager.TryConsumeDirection(upEdge, downEdge, leftEdge, rightEdge, dt, ref transferDirCooldown);
                if (digit >= 0)
                {
                    transferDigits.Add(digit);
                    if (transferMessageText != null) transferMessageText.text = string.Empty;
                    RefreshTransferDigits();
                }
            }

            if (confirmEdge && transferDigits.Count == DirectionTransferCode.DigitCount)
            {
                if (DirectionTransferCode.TryDecode(transferDigits, out DirectionTransferCode.Payload payload, out string error))
                {
                    transferPendingPayload = payload;
                    transferState = TransferInputState.Confirm;
                    List<string> lines = TransferAchievements.SummaryLines(payload);
                    string summary = lines.Count > 0 ? string.Join("\n", lines) : "(引き継ぐ実績はありません)";
                    if (transferMessageText != null)
                    {
                        transferMessageText.color = Cyan;
                        transferMessageText.text = summary + "\n\nA で反映 / B で戻る";
                    }
                }
                else if (transferMessageText != null)
                {
                    transferMessageText.color = ErrorRed;
                    transferMessageText.text = error; // 桁はそのまま保持(全消去はしない)
                }
            }
        }
        else // Confirm
        {
            if (confirmEdge)
            {
                TransferAchievements.ApplyPayload(transferPendingPayload);
                transferDigits.Clear();
                transferState = TransferInputState.Entry;
                RefreshTransferDigits();
                if (transferMessageText != null)
                {
                    transferMessageText.color = Cyan;
                    transferMessageText.text = "反映しました。もう1件入力するか B で戻ってください。";
                }
            }
        }

        RefreshTransferDigits(); // カーソル点滅を毎フレーム反映
        return false;
    }
    private bool transferBackHeldPrev;

    // 12桁スロットの表示更新: 入力済みは矢印+明色、未入力は薄いドット、カーソル位置は点滅。
    private void RefreshTransferDigits()
    {
        for (int i = 0; i < transferDigitTexts.Length; i++)
        {
            TMP_Text slot = transferDigitTexts[i];
            if (slot == null) continue;
            if (i < transferDigits.Count)
            {
                slot.text = DirectionTransferCode.Symbols[transferDigits[i]].ToString();
                slot.color = DigitFilled;
            }
            else if (i == transferDigits.Count)
            {
                slot.text = "・";
                float blink = 0.5f + 0.5f * Mathf.Sin(transferCursorBlink * 6f);
                slot.color = Color.Lerp(DigitEmpty, DigitCursor, blink);
            }
            else
            {
                slot.text = "・";
                slot.color = DigitEmpty;
            }
        }
    }

    // 引き継ぎ画面(第29便: ミニマル再設計)。装飾(バナー/スラッシュ/カード枠/
    // ヒントバー)を取り払い、1枚の暗いパネルの上にタイポグラフィと余白だけで
    // 階層を作る。コードチップも一段小さくして画面の主張を抑える。
    private void BuildTransferPanel()
    {
        GameObject rootObj = new GameObject("TransferPanel", typeof(RectTransform));
        rootObj.layer = gameObject.layer;
        transferRoot = rootObj;
        RectTransform rootRect = (RectTransform)rootObj.transform;
        rootRect.SetParent(transform, false);
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        transferCG = rootObj.AddComponent<CanvasGroup>();

        // 最背面: 完成フレームのぼかしスナップショット(オープン時に差し込む)。
        // メニュー・ロゴを退場させず、その凍結ぼかしを背景として敷く(第31便)。
        transferBackdrop = CreateRawImage("Backdrop", rootRect);
        StretchToParent(transferBackdrop.rectTransform);
        // ぼかしは難易度オーバーレイと同じダウンサンプルピラミッド方式(BackdropBlurUtil)で
        // 作るため、シェーダマテリアルは使わない。既定マテリアルで 1/4 解像度のぼかし RT を
        // バイリニア拡大表示する。
        transferBackdrop.color = new Color(0.55f, 0.62f, 0.72f, 1f); // 難易度オーバーレイと同じ軽い減光
        transferBackdrop.gameObject.SetActive(false);

        // 背景: 薄いスクリム+中央の無枠パネル1枚のみ。パネルはわずかに透けさせ
        // (0.90)、上辺ハイライト+下辺シャドウの各1pxで「ただの黒い板」感を消す
        // (oracle 第29便)。
        // 第34便(oracle bin34): 幅を絞り(940→800)、ヒント行削除に合わせ高さも詰める
        // (600→480)。「黒い板」感を消すため、単色板の上に薄い青の内側レイヤー・
        // 上下の締め・辺ハイライトを重ねる(明るいシアン全周枠はコード帯のネオンと
        // 競合するため使わない)。
        const float panelW = 800f;
        // 第35便: ヒント行削除の名残で下部に空白が残っていたため高さを詰め(480→440)、
        // 内容ブロック(見出し〜メッセージ)を y-12 下げてパネル中央に再配置する。
        const float panelH = 440f;
        const float panelHalfW = panelW * 0.5f;
        const float panelHalfH = panelH * 0.5f;
        Vector2 panelSize = new Vector2(panelW, panelH);
        CreatePanel("Scrim", rootRect, Vector2.zero, new Vector2(4000f, 4000f), new Color(0f, 0.024f, 0.071f, 0.22f));
        // 背面の影板(わずかに右下へずらす。ぼかし無しでも黒板の浮きが和らぐ)。
        CreatePanel("PanelShadow", rootRect, new Vector2(6f, -8f), panelSize, new Color(0f, 0f, 0f, 0.24f));
        CreatePanel("Panel", rootRect, Vector2.zero, panelSize, new Color(0.008f, 0.031f, 0.078f, 0.90f));
        // 下部の締め(内側グラデの代替)。
        CreatePanel("PanelBottomDark", rootRect, new Vector2(0f, -(panelHalfH - 45f)), new Vector2(panelW, 90f), new Color(0f, 0f, 0f, 0.16f));
        // 辺は細い銀の額装枠のみ(統一便2で足した4隅ブラケット・ヘッダー帯・
        // スラッシュ・ダイヤノードの加飾は撤去。コードとコピー導線を主役に戻す)。
        Color edgeSilver = new Color(0.268f, 0.325f, 0.456f);
        CreatePanel("EdgeTop", rootRect, new Vector2(0f, panelHalfH - 1f), new Vector2(panelW, 2f), new Color(edgeSilver.r, edgeSilver.g, edgeSilver.b, 0.80f));
        CreatePanel("EdgeBottom", rootRect, new Vector2(0f, -(panelHalfH - 1f)), new Vector2(panelW, 2f), new Color(edgeSilver.r, edgeSilver.g, edgeSilver.b, 0.60f));
        CreatePanel("EdgeLeft", rootRect, new Vector2(-(panelHalfW - 1f), 0f), new Vector2(2f, panelH), new Color(edgeSilver.r, edgeSilver.g, edgeSilver.b, 0.60f));
        CreatePanel("EdgeRight", rootRect, new Vector2(panelHalfW - 1f, 0f), new Vector2(2f, panelH), new Color(edgeSilver.r, edgeSilver.g, edgeSilver.b, 0.60f));
        // コンテンツ(ラベル・コード帯・入力行)の左右端を揃える基準。
        const float contentHalf = 272f;

        // 見出しはタイポグラフィのみ(帯・スラッシュ・ルビ無し)。白見出し+シアン
        // 英字サブ+短い1本の細線で締める(pre-d4f748c のシンプル様式へ戻す)。
        TMP_Text heading = CreateText("Heading", rootRect, new Vector2(0f, 168f), new Vector2(700f, 52f), 40f, Color.white, TextAlignmentOptions.Center);
        heading.fontStyle = FontStyles.Bold;
        TMP_Text headingSub = CreateText("HeadingSub", rootRect, new Vector2(0f, 138f), new Vector2(700f, 22f), 15f, Cyan, TextAlignmentOptions.Center);
        headingSub.characterSpacing = 8f;
        headingSub.text = "DIRECTION CODE";
        CreatePanel("HeadingRule", rootRect, new Vector2(0f, 114f), new Vector2(240f, 1f), new Color(0.275f, 0.863f, 0.941f, 0.28f));

        // 方向シーケンス入力(SPEC §1): ↑↓←→ 12桁を4桁3組で表示。埋まった桁は明色、
        // 未入力は薄いドット、カーソル位置は点滅。TickTransfer/TickTransferInput が更新する。
        TMP_Text hint = CreateText("Hint", rootRect, new Vector2(0f, 96f), new Vector2(720f, 26f), 18f,
            new Color(0.388f, 0.867f, 0.91f, 0.6f), TextAlignmentOptions.Center);
        hint.text = "スティックで入力  A:決定  B:削除(長押しで戻る)";
        transferHintText = hint;

        transferDigitTexts = new TMP_Text[DirectionTransferCode.DigitCount];
        const float slotW = 40f;
        const float slotGap = 10f;
        const float groupGap = 26f;
        int groups = DirectionTransferCode.DigitCount / DirectionTransferCode.DigitGroupSize;
        float totalW = DirectionTransferCode.DigitCount * slotW
            + (DirectionTransferCode.DigitCount - groups) * slotGap
            + (groups - 1) * groupGap;
        float x = -totalW * 0.5f + slotW * 0.5f;
        for (int i = 0; i < DirectionTransferCode.DigitCount; i++)
        {
            TMP_Text slot = CreateText("Digit" + i, rootRect, new Vector2(x, 32f), new Vector2(slotW, 56f), 34f, DigitEmpty, TextAlignmentOptions.Center);
            if (codeFont != null) slot.font = codeFont;
            slot.fontStyle = FontStyles.Bold;
            slot.text = "・";
            transferDigitTexts[i] = slot;

            x += slotW + slotGap;
            if ((i + 1) % DirectionTransferCode.DigitGroupSize == 0) x += groupGap - slotGap;
        }

        transferMessageText = CreateText("Message", rootRect, new Vector2(0f, -70f), new Vector2(720f, 160f), 22f, Cyan, TextAlignmentOptions.Center);
        transferMessageText.textWrappingMode = TextWrappingModes.Normal;

        SetChildText(rootRect, "Heading", "引き継ぎ");
        transferMessageText.text = string.Empty;

        transferRoot.SetActive(false);
    }

    // ---- ランキング盤面(SPEC §2.2) -----------------------------------------
    // 左右=難易度切替・上下=ステージ切替・A=1P/2Pモード切替・B=戻る、という解釈で実装。
    // (SPEC本文は「スティック左右でステージ/難易度/モード切替」とだけ書いてあり、
    // 3軸をどう1入力に割り当てるかは明記が無いため、既存の上下=一覧送りの慣習に
    // 合わせて上下=ステージへ広げた。要親確認: Instructions/ranking-transfer/NOTES.md 参照)

    public void OpenRanking()
    {
        FocusRoom((int)TitleMenuAction.Ranking);
        AfterRoomZoom(OpenRankingNow);
    }

    private void OpenRankingNow()
    {
        if (rankingRoot == null) return;
        if (rankingCloseRoutine != null) { StopCoroutine(rankingCloseRoutine); rankingCloseRoutine = null; }
        rankingRoot.transform.localScale = Vector3.one;
        rankingOpen = true;
        rankingRoot.SetActive(true);
        rankingRoot.transform.SetAsLastSibling();
        if (rankingCG != null) rankingCG.alpha = 0f;
        if (rankingBackdrop != null) rankingBackdrop.gameObject.SetActive(false);
        if (rankingCaptureRoutine != null) StopCoroutine(rankingCaptureRoutine);
        rankingCaptureRoutine = StartCoroutine(CaptureRankingBackdrop());
        if (!rankingInkCentered)
        {
            RectTransform rootRect = (RectTransform)rankingRoot.transform;
            TMP_Text heading = rootRect.Find("Heading")?.GetComponent<TMP_Text>();
            if (heading != null) rankingInkCentered = TmpAlign.CenterInkVertically(heading);
        }
        RefreshRankingBoard();
    }

    public void CloseRanking()
    {
        rankingOpen = false;
        UnfocusRoom();
        if (rankingCaptureRoutine != null) { StopCoroutine(rankingCaptureRoutine); rankingCaptureRoutine = null; }
        if (rankingCloseRoutine != null) StopCoroutine(rankingCloseRoutine);
        if (rankingRoot != null && rankingRoot.activeInHierarchy && gameObject.activeInHierarchy)
        {
            rankingCloseRoutine = StartCoroutine(CloseRankingRoutine());
        }
        else
        {
            FinishCloseRanking();
        }
    }

    private IEnumerator CloseRankingRoutine()
    {
        RectTransform rootRect = rankingRoot != null ? (RectTransform)rankingRoot.transform : null;
        float startAlpha = rankingCG != null ? rankingCG.alpha : 1f;
        float t = 0f;
        const float dur = 0.14f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / dur);
            if (rankingCG != null) rankingCG.alpha = startAlpha * (1f - p);
            if (rootRect != null) rootRect.localScale = Vector3.one * Mathf.Lerp(1f, 0.98f, p);
            yield return null;
        }
        rankingCloseRoutine = null;
        FinishCloseRanking();
    }

    private void FinishCloseRanking()
    {
        if (rankingRoot != null)
        {
            rankingRoot.transform.localScale = Vector3.one;
            rankingRoot.SetActive(false);
        }
        if (rankingCG != null) rankingCG.alpha = 1f;
        if (rankingBackdrop != null) rankingBackdrop.texture = null;
        BackdropBlurUtil.ReleaseRT(ref rankingBlurRT);
    }

    private IEnumerator CaptureRankingBackdrop()
    {
        yield return new WaitForEndOfFrame();
        rankingCaptureRoutine = null;
        if (!rankingOpen) yield break;
        BackdropBlurUtil.ReleaseRT(ref rankingBlurRT);
        rankingBlurRT = BackdropBlurUtil.CapturePyramidBlur();
        if (rankingBackdrop != null)
        {
            rankingBackdrop.texture = rankingBlurRT;
            rankingBackdrop.gameObject.SetActive(true);
        }
        float t = 0f;
        while (t < 0.14f)
        {
            t += Time.unscaledDeltaTime;
            if (rankingCG != null) rankingCG.alpha = Mathf.Clamp01(t / 0.14f);
            yield return null;
        }
        if (rankingCG != null) rankingCG.alpha = 1f;
    }

    // 戻り値 true は「画面を閉じてタイトルへ戻る」(B)。
    public bool TickRankingInput(bool leftEdge, bool rightEdge, bool upEdge, bool downEdge, bool confirmEdge, bool backEdge)
    {
        if (!rankingOpen) return false;
        if (backEdge) return true;

        bool changed = false;
        if (rightEdge)
        {
            rankingDifficultyIndex = (rankingDifficultyIndex + 1) % DirectionTransferCode.DifficultyCount;
            changed = true;
        }
        else if (leftEdge)
        {
            rankingDifficultyIndex = (rankingDifficultyIndex - 1 + DirectionTransferCode.DifficultyCount) % DirectionTransferCode.DifficultyCount;
            changed = true;
        }
        if (downEdge)
        {
            rankingStageIndex = (rankingStageIndex + 1) % DirectionTransferCode.StageCount;
            changed = true;
        }
        else if (upEdge)
        {
            rankingStageIndex = (rankingStageIndex - 1 + DirectionTransferCode.StageCount) % DirectionTransferCode.StageCount;
            changed = true;
        }
        if (confirmEdge)
        {
            rankingModeIndex = 1 - rankingModeIndex;
            changed = true;
        }

        if (changed) RefreshRankingBoard();
        return false;
    }

    private void RefreshRankingBoard()
    {
        string stageDir = DirectionTransferCode.StageOrder[rankingStageIndex];
        string stageName = TransferAchievements.StageDisplayName(stageDir);
        string diffName = DifficultyUtility.GetDisplayName((Difficulty)rankingDifficultyIndex);
        string mode = RankingModes[rankingModeIndex];
        if (rankingHeaderText != null)
        {
            rankingHeaderText.text = $"{stageName}  {diffName}  {mode}";
        }

        List<RankingStore.Entry> top = RankingStore.GetTop(stageDir, rankingDifficultyIndex, mode);
        for (int i = 0; i < rankingRowTexts.Length; i++)
        {
            TMP_Text row = rankingRowTexts[i];
            if (row == null) continue;
            if (i < top.Count)
            {
                RankingStore.Entry e = top[i];
                row.text = $"{i + 1,2}   {e.name,-3}   {e.score,8:N0}   {e.dateTime}";
                row.color = MenuTextBase;
            }
            else
            {
                row.text = $"{i + 1,2}   ---";
                row.color = new Color(MenuTextBase.r, MenuTextBase.g, MenuTextBase.b, 0.35f);
            }
        }
    }

    private void BuildRankingPanel()
    {
        GameObject rootObj = new GameObject("RankingPanel", typeof(RectTransform));
        rootObj.layer = gameObject.layer;
        rankingRoot = rootObj;
        RectTransform rootRect = (RectTransform)rootObj.transform;
        rootRect.SetParent(transform, false);
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        rankingCG = rootObj.AddComponent<CanvasGroup>();

        rankingBackdrop = CreateRawImage("Backdrop", rootRect);
        StretchToParent(rankingBackdrop.rectTransform);
        rankingBackdrop.color = new Color(0.55f, 0.62f, 0.72f, 1f);
        rankingBackdrop.gameObject.SetActive(false);

        const float panelW = 900f;
        const float panelH = 720f;
        const float panelHalfW = panelW * 0.5f;
        const float panelHalfH = panelH * 0.5f;
        Vector2 panelSize = new Vector2(panelW, panelH);
        CreatePanel("Scrim", rootRect, Vector2.zero, new Vector2(4000f, 4000f), new Color(0f, 0.024f, 0.071f, 0.22f));
        CreatePanel("PanelShadow", rootRect, new Vector2(6f, -8f), panelSize, new Color(0f, 0f, 0f, 0.24f));
        CreatePanel("Panel", rootRect, Vector2.zero, panelSize, new Color(0.008f, 0.031f, 0.078f, 0.90f));
        Color edgeSilver = new Color(0.268f, 0.325f, 0.456f);
        CreatePanel("EdgeTop", rootRect, new Vector2(0f, panelHalfH - 1f), new Vector2(panelW, 2f), new Color(edgeSilver.r, edgeSilver.g, edgeSilver.b, 0.80f));
        CreatePanel("EdgeBottom", rootRect, new Vector2(0f, -(panelHalfH - 1f)), new Vector2(panelW, 2f), new Color(edgeSilver.r, edgeSilver.g, edgeSilver.b, 0.60f));
        CreatePanel("EdgeLeft", rootRect, new Vector2(-(panelHalfW - 1f), 0f), new Vector2(2f, panelH), new Color(edgeSilver.r, edgeSilver.g, edgeSilver.b, 0.60f));
        CreatePanel("EdgeRight", rootRect, new Vector2(panelHalfW - 1f, 0f), new Vector2(2f, panelH), new Color(edgeSilver.r, edgeSilver.g, edgeSilver.b, 0.60f));

        TMP_Text heading = CreateText("Heading", rootRect, new Vector2(0f, panelHalfH - 56f), new Vector2(700f, 52f), 36f, Color.white, TextAlignmentOptions.Center);
        heading.fontStyle = FontStyles.Bold;
        heading.text = "ランキング";

        TMP_Text header = CreateText("SubHeader", rootRect, new Vector2(0f, panelHalfH - 104f), new Vector2(820f, 30f), 22f, Cyan, TextAlignmentOptions.Center);
        header.characterSpacing = 3f;
        rankingHeaderText = header;

        TMP_Text hint = CreateText("Hint", rootRect, new Vector2(0f, panelHalfH - 132f), new Vector2(820f, 22f), 15f,
            new Color(0.388f, 0.867f, 0.91f, 0.55f), TextAlignmentOptions.Center);
        hint.text = "←→ 難易度   ↑↓ ステージ   A モード切替   B 戻る";

        rankingRowTexts = new TMP_Text[RankingStore.TopCount];
        const float rowH = 40f;
        float rowTop = panelHalfH - 176f;
        for (int i = 0; i < rankingRowTexts.Length; i++)
        {
            TMP_Text row = CreateText("Row" + i, rootRect, new Vector2(0f, rowTop - i * rowH), new Vector2(760f, rowH), 22f, MenuTextBase, TextAlignmentOptions.Left);
            if (codeFont != null) row.font = codeFont;
            rankingRowTexts[i] = row;
        }

        rankingRoot.SetActive(false);
    }

    // ---- UI helpers -------------------------------------------------------

    private TMP_Text CreateText(string objectName, Transform parent, Vector2 pos, Vector2 size, float fontSize, Color color, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.layer = gameObject.layer;
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        if (uiFont != null) label.font = uiFont;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = align;
        label.raycastTarget = false;
        return label;
    }

    private void OnDestroy()
    {
        ReleaseBackdropTexture();
        BackdropBlurUtil.ReleaseRT(ref rankingBlurRT);
    }

    private RawImage CreateRawImage(string objectName, Transform parent)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        go.layer = gameObject.layer;
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        RawImage raw = go.GetComponent<RawImage>();
        raw.raycastTarget = false;
        return raw;
    }

    private Image CreatePanel(string objectName, Transform parent, Vector2 pos, Vector2 size, Color color)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetChildText(Transform parent, string childName, string value)
    {
        Transform child = parent.Find(childName);
        if (child == null) return;
        TMP_Text text = child.GetComponent<TMP_Text>();
        if (text != null)
        {
            text.text = value;
            TmpAlign.CenterInkVertically(text);
        }
    }
}

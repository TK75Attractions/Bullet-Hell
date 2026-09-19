using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// タイトル画面の背景を Astra 制作の 3D「旅支度の部屋」(Instructions/タイトル/cg/v3.fbx) にする。
///
/// 仕組みはステージ背景 CG (<see cref="StageCgController"/>) と同じ「専用カメラ → RenderTexture →
/// タイトル Canvas 最背面の RawImage」方式。ただし材質が写実寄りなので、石工の自前 unlit シェーダ
/// ではなく URP/Lit + 実ライト(UniversalRenderer3D)で描く。
///
/// - 部屋はレイヤー <c>TitleCG</c>(11) に置き、専用カメラだけがそれを写す。既存のカメラスタック
///   (BackImageCamera / MainCamera / Front / UI) にも StageCG のステージ背景にも触れない。
/// - メニュー項目は部屋のオブジェクトに対応する(スタート=地図と瓶 / 設定=ランタン /
///   引き継ぎ=手紙 / ランキング=本棚の中段 / 1P・2P=壁のマント 2 枚)。選択中のものだけを
///   暖色のポイントライト(リム発光の代用)と _BaseColor の持ち上げで光らせ、非選択は素の色に戻す
///   (第8便。切替は 0.15 秒のクロスフェード)。メニュー名は TitleManager が対象の真上に置く▼と
///   ラベルで示す。
/// - 決定でカメラが 0.4 秒 ease-out cubic でそのオブジェクトへ寄り、戻るで全景へ帰る。
///
/// 生成物(カメラ・ライト・塵・部屋インスタンス)はすべてランタイムに作るので、シーンには
/// この空の Rig だけが増える。
/// </summary>
public class TitleRoomController : MonoBehaviour
{
    public static TitleRoomController Instance { get; private set; }

    [Header("素材")]
    [Tooltip("Assets/TitleCG/TitleRoom_v3.fbx のプレハブ。ルートの transform はそのまま使う。")]
    public GameObject roomPrefab;
    [Tooltip("部屋を描く RenderTexture。1920x1080。")]
    public RenderTexture targetTexture;
    [Tooltip("UniversalRenderer3D の RendererDataList 上の index。既定 1。")]
    public int rendererIndex = 1;
    [Tooltip("班員が描いた主人公の立ち絵(背景透過済み)。部屋の 3D 空間へ板として立てる。")]
    public Sprite heroSprite;

    [Header("立ち絵(部屋の 3D 空間に立てる板)")]
    // Canvas の 2D 画像ではなく、部屋の中に Quad を立てて URP/Lit のアルファカットアウトで
    // 描く。こうすると部屋のライト(ランタンの暖色・月光・環境光)を受け、低解像度の
    // ドット風描画も部屋と同じだけ掛かるので粗さが揃う。
    [Tooltip("足元の位置(部屋のワールド座標)。床は y=0。")]
    public Vector3 heroFootPos = new Vector3(2.6f, 0f, -2.6f);
    [Tooltip("板の高さ(ワールド単位)。全景カメラで画面高さの約 60% になる値。")]
    public float heroHeight = 4.08f;
    [Tooltip("立ち絵の明るさ(材質のベース色。1 で原画そのまま)。部屋の中景と同じ明度まで落とす。")]
    [Range(0f, 1.5f)] public float heroBrightness = 1.15f;
    [Tooltip("足元の接地影の直径(ワールド単位)。0 で影なし。")]
    public float heroShadowSize = 1.76f;
    [Tooltip("足元の接地影の濃さ。")]
    [Range(0f, 1f)] public float heroShadowAlpha = 0.55f;
    [Tooltip("ON で立ち絵だけを専用レイヤー・専用カメラで 1920x1080 の RT へ描き、粗い部屋の上に元解像度で重ねる(プレイ中のボスと同じ方式)。OFF で部屋と同じ低解像度。")]
    public bool heroFullRes = true;

    [Header("立ち絵の左リムライト(2026-09-19 指示)")]
    // 画面左のランタンの橙の光が、髪・腕・マント・杖の「左を向いた縁」にだけ
    // 細く乗る(参考: Instructions/UI/from_user_20260919/title_fix/hero_rimlight_reference.webp)。
    // 板ポリなので法線は使えない。原画のアルファを行ごとに左から走査して
    // 「透明 → 不透明」へ変わる縁を見つけ、そこから右へ数テクセルだけ減衰する
    // マスクを焼き、URP/Lit の Emission(自発光)として足す。
    // 自発光なので heroBrightness(ベース色の倍率)とは独立に効く。
    [Tooltip("リムライトの強さ。0 で無し。")]
    [Range(0f, 3f)] public float heroRimStrength = 1.5f;
    [Tooltip("リムライトの色(ランタンの橙)。")]
    public Color heroRimColor = new Color(1.0f, 0.62f, 0.30f, 1f);
    // 原画 1024px の立ち絵は画面では約 470px なので、1 テクセル ≒ 0.46 画面 px。
    // 指示の「幅 2〜4(画面 px)」は原画で 5〜9 テクセルにあたる。
    [Tooltip("リムの幅(原画のテクセル数)。画面では約 0.46 倍の太さになる。")]
    [Range(1f, 16f)] public float heroRimWidth = 6f;
    Texture2D heroRimTex;
    static readonly int HeroEmissionColorId = Shader.PropertyToID("_EmissionColor");

    [Header("タイトルロゴ(部屋の 3D 空間に立てる板・2026-09-19 指示)")]
    // 旧: Canvas 上の 2D 画像。カメラが寄っても動かないので「画面に貼り付いた紙」に
    // 見えていた。部屋の奥(背面壁 z=4.72 の手前)に板として置くと、寄りで
    // 自然に画面の外へ出ていく。
    // 板の位置と大きさは「全景カメラで画面のどこに何 px で写るか」から逆算するので、
    // 下の 3 つを触るだけで見え方を合わせられる(値の正は .tmp_ui/progress.md U8)。
    [Tooltip("全景カメラで見たときのロゴの中心(1920x1080 の画面中心からの px)。")]
    // y は旧 2D ロゴ(261)より 57px 上げてある。旧位置だと絵の下端が「設定」の
    // ラベルに重なって読めなかった(2026-09-19 の指摘 5 の一因)。
    // 第 U9 便: 306 → 318。あわせて幅を 484 → 452 に詰めた(-6.6%)。
    // ロゴの絵の下端は「上へ 12px(位置) + 8px(縮小)= 20px」持ち上がり、上端は 4px しか
    // 動かない(天井の梁に触れない)。「設定」のラベルとの最小の隙間は 29px → 49px。
    public Vector2 logoScreenPos = new Vector2(19f, 318f);
    [Tooltip("全景カメラで見たときのロゴの幅(px)。高さは原画の縦横比で決まる。")]
    public float logoScreenWidth = 452f;
    [Tooltip("板を置く奥行き(部屋のワールド z)。背面壁は z=4.72。")]
    public float logoPlaneZ = 4.40f;
    Transform logoBoard;
    Renderer logoRenderer;
    MaterialPropertyBlock logoMpb;
    float logoBobPx;

    /// <summary>ロゴの板が作れているか(TitleManager が 2D のロゴを出すかの判断に使う)。</summary>
    public bool HasLogoBoard => logoRenderer != null;

    /// <summary>ロゴの上下の揺れ(全景カメラでの px)。TitleManager が毎フレーム渡す。</summary>
    public void SetLogoBob(float offsetPx)
    {
        if (Mathf.Approximately(offsetPx, logoBobPx)) return;
        logoBobPx = offsetPx;
        ApplyLogoTransform();
    }

    Transform heroBoard;
    Renderer heroRenderer;
    Transform heroShadow;
    Renderer heroShadowRenderer;
    MaterialPropertyBlock heroMpb;
    static readonly int HeroBaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int HeroCutoffId = Shader.PropertyToID("_Cutoff");
    // 立ち絵だけを元解像度で描く 2 台目のカメラと、その描画先(実行時生成)。
    Camera heroCamera;
    RenderTexture heroRT;
    int roomLayer;      // 部屋のジオメトリ(TitleCG)
    int heroLayer = -1; // 立ち絵と接地影(TitleHero)。分離しないときは roomLayer と同じ
    int lightLayer = -1;// 部屋のライト(TitleLight)。両方のカメラから見える

    [Header("明るさ")]
    [Tooltip("全ライトに掛かる倍率。v3 の参考レンダーより +15% 明るくする指示のため既定 1.15。")]
    public float exposure = 1.15f;
    [Tooltip("部屋を出しているあいだだけ差し替える環境光。既定のフラット灰(0.21)のままだと室内が真っ平らに明るくなる。")]
    public Color ambientColor = new Color(0.0055f, 0.0062f, 0.0100f, 1f);
    [Tooltip("窓から差す月光(平行光)の強さ。exposure が掛かる。")]
    public float moonIntensity = 0.42f;
    [Tooltip("室内の起こし光(平行光)。exposure が掛かる。")]
    public float fillIntensity = 0.07f;
    [Tooltip("ランタン(点光源)の強さ。exposure が掛かる。")]
    public float lanternIntensity = 5.4f;
    [Tooltip("ランタンの届く距離。窓の外の街まで届かせない(5.5 で背面壁 z=4.72 の少し先まで)。")]
    public float lanternRange = 5.5f;

    [Header("ドット風表示(部屋だけ低解像度で描く)")]
    [Tooltip("ON で部屋を pixelWidth x pixelHeight の RenderTexture へ描き、Point(最近傍)で画面いっぱいに引き伸ばす。ロゴ・立ち絵・メニュー文字は従来の解像度のまま。")]
    public bool pixelate = true;
    [Tooltip("ドット風の内部解像度(幅)。既定 640(=1920 の 1/3)。タイトル・街・プレイ中の CG で 640x360 に統一している。")]
    public int pixelWidth = 640;
    [Tooltip("ドット風の内部解像度(高さ)。既定 360。16:9 を保つこと。")]
    public int pixelHeight = 360;
    [Tooltip("1 チャンネルあたりの階調数。0 で色数の減衰なし(既定)。4〜8 でレトロなポスタリゼーション。")]
    public int pixelatePalette = 0;
    [Tooltip("色数を減らしたときの 4x4 順序ディザの強さ。0 でディザなし。")]
    [Range(0f, 1f)] public float pixelateDither = 1f;

    // ドット風のときだけ使う低解像度の描画先(実行時生成)。null なら targetTexture をそのまま使う。
    RenderTexture pixelRT;
    // 色数を落とすときだけ RawImage に貼るマテリアル(pixelatePalette=0 なら null)。
    Material pixelMat;

    // 部屋を消したときに戻す環境光。
    UnityEngine.Rendering.AmbientMode savedAmbientMode;
    Color savedAmbientLight;
    float savedAmbientIntensity;
    float savedReflectionIntensity;
    bool ambientSaved;

    // ---- 部屋の座標(v3_camera.json の実値) ----------------------------------
    // 全景。lens 40mm / sensor 36x20.25 → 垂直画角 28.409°。
    static readonly Vector3 TitlePos = new Vector3(0f, 6.4f, -14.8f);
    static readonly Vector3 TitleEuler = new Vector3(15.25512f, 0f, 0f);
    const float TitleVFov = 28.409272f;
    // 机(地図)。
    static readonly Vector3 DeskPos = new Vector3(-0.95f, 5.7f, -4.65f);
    static readonly Vector3 DeskEuler = new Vector3(39.731927f, -1.1503626f, 0f);
    const float DeskVFov = 28.409272f;

    // スタート決定時だけ、地図への寄りを全景→camera_desk の 1.3 倍まで延長する
    // (「ぐっと寄る」感を出すため。2026-09-15 指示)。全景からの外挿値。
    const float StartZoomExtra = 1.3f;
    static readonly Vector3 StartDeskPos = TitlePos + (DeskPos - TitlePos) * StartZoomExtra;
    static readonly Vector3 StartDeskEuler = TitleEuler + (DeskEuler - TitleEuler) * StartZoomExtra;
    // 壁(マント)。lens 34mm → 33.166°。
    static readonly Vector3 WallPos = new Vector3(1.5f, 3.3f, -0.8f);
    static readonly Vector3 WallEuler = new Vector3(7.6904855f, 50.937416f, 0f);
    const float WallVFov = 33.166443f;

    // 寄りの中心(v3_camera.json の menus[].center)。
    static readonly Vector3 BottleCenter = new Vector3(-3.47f, 2.043f, 1.07375f);
    static readonly Vector3 MapCenter = new Vector3(-1.65f, 1.582f, -0.04f);
    static readonly Vector3 LetterCenter = new Vector3(0.4775f, 1.935f, -0.3575f);
    static readonly Vector3 QuillCenter = new Vector3(0.66f, 2.03f, 0.0f);
    static readonly Vector3 LanternCenter = new Vector3(0.4f, 2.1907f, 1.12f);
    static readonly Vector3 CloakCenter = new Vector3(5.6111f, 2.5159f, 2.56f);
    // 本棚(ランキング)。参考画に合わせて手紙から本棚へ移した。棚の中段(本が並ぶ段)。
    static readonly Vector3 ShelfCenter = new Vector3(-5.20f, 2.30f, 3.72f);

    /// <summary>メニュー index(0=スタート/1=設定/2=引き継ぎ/3=ランキング)。</summary>
    public const int MenuStart = 0;
    public const int MenuOptions = 1;
    public const int MenuTransfer = 2;
    public const int MenuRanking = 3;
    public const int MenuCount = 4;

    // ---- ▼マーカーの 3D アンカー(第8便) --------------------------------------
    // 各メニューは「部屋のオブジェクトの真上の小さな金色の▼」で示す。▼とラベルは
    // Canvas 上のウィジェットなので、ここでは対象の上端付近のワールド座標だけを持ち、
    // TitleManager が毎フレーム投影して置く(カメラが寄っても追従する)。
    /// <summary>マーカー番号: 0..3=メニュー / 4=1P のマント(左) / 5=2P のマント(右)。</summary>
    public const int MarkerP1 = 4;
    public const int MarkerP2 = 5;
    public const int MarkerCount = 6;

    static readonly Vector3[] MarkerAnchors =
    {
        new Vector3(-1.65f, 1.617f, -0.04f),  // スタート: 机の地図の上端
        new Vector3(0.40f, 2.831f, 1.12f),    // 設定: ランタンの上端
        new Vector3(0.4775f, 2.05f, -0.45f),  // 引き継ぎ: 手紙(羽根ペンは装飾)
        new Vector3(-5.20f, 2.62f, 3.76f),    // ランキング: 本棚の中段
        new Vector3(5.58f, 3.85f, 3.22f),     // 1P: 画面左のマント(cloak_02)
        new Vector3(5.58f, 3.85f, 1.82f),     // 2P: 画面右のマント(cloak_01)
    };

    // 寄りカメラの姿勢。explicitPose が真なら pos/euler をそのまま使い、偽なら
    // 全景カメラから対象へまっすぐ寄る(ドリー)姿勢を距離 dollyDist で作る。
    struct FocusView
    {
        public bool explicitPose;
        public Vector3 pos;
        public Vector3 euler;
        public float vFov;
        public Vector3 target;
        public float dollyDist;
    }

    static FocusView Explicit(Vector3 p, Vector3 e, float fov)
        => new FocusView { explicitPose = true, pos = p, euler = e, vFov = fov };

    static FocusView Dolly(Vector3 target, float dist)
        => new FocusView { explicitPose = false, target = target, dollyDist = dist, vFov = TitleVFov };

    FocusView[] focusViews;

    // ---- 実体 --------------------------------------------------------------
    Camera roomCamera;
    Transform roomRoot;
    Transform lanternTf;
    Light lanternLight;
    Light selectionLight;
    Light cloakLight1;
    Light cloakLight2;
    Light moonLight;
    Light fillLight;
    Transform cloak1;
    Transform cloak2;
    Renderer[] cloak1Rend;
    Renderer[] cloak2Rend;
    Quaternion cloak1Home;
    Quaternion cloak2Home;
    Renderer cityLights;
    MaterialPropertyBlock cityMpb;
    // メニューごとに光らせる実体(リム発光の代わりに _BaseColor を持ち上げる)。
    // 材質の多くは _EMISSION キーワードが無効なので MPB の _EmissionColor は効かない。
    // URP/Lit の _BaseColor は 1 を超える倍率がそのまま乗るので、こちらで持ち上げる。
    Renderer[][] menuTargets;
    MaterialPropertyBlock targetMpb;
    // メニューごとの発光の重み(選択中=1 / 非選択=0)。切替は 0.15 秒のクロスフェード。
    float[] menuWeight;
    const float HighlightFadeDuration = 0.15f;
    // 1P/2P のマントの点灯の重み(選択中の側だけ光る)。
    float cloakWeightP1 = 1f;
    float cloakWeightP2;
    ParticleSystem dust;
    bool built;

    // 状態
    bool activeNow;
    int selection = MenuStart;
    int zoomTarget = -1;      // -1 = 全景
    float zoomProgress;       // 0=全景 1=寄り
    // 寄り/戻りの尺。タイトル→ステージ選択のクロスフェードはこの寄りの
    // 「速度が最大になる瞬間」(ease-in-out の中点 = ZoomDuration/2)を中心に
    // 置くので、TitleManager 側の定数もここを参照する(2026-09-15 指示)。
    public const float ZoomDuration = 0.6f;
    bool twoPlayer;
    float time;
    float flicker = 1f;
    float flickerVel;
    // マントの点灯/消灯。選択中の側(1P=左 / 2P=右)の 1 枚だけが灯る。
    const float CloakLitIntensity = 1.6f;
    const float CloakDimIntensity = 0f;

    /// <summary>寄りの進み具合(0=全景 / 1=寄りきり)。立ち絵の視差・退避に使う。</summary>
    public float ZoomAmount => zoomProgress;
    /// <summary>部屋の描画先。タイトルの RawImage が貼る。ドット風のときは低解像度の RT。</summary>
    public RenderTexture Texture => pixelRT != null ? pixelRT : targetTexture;
    /// <summary>色数を落とすときに RawImage へ貼るマテリアル。既定(pixelatePalette=0)は null。</summary>
    public Material ViewMaterial => pixelMat;
    /// <summary>立ち絵だけを元解像度で描いた RT。分離していないときは null(部屋の RT に含まれる)。</summary>
    public RenderTexture HeroTexture => heroCamera != null ? heroRT : null;
    public bool Ready => built && roomCamera != null && targetTexture != null;
    /// <summary>部屋がいま画面に出ているか(退場演出で落としたあとは false)。</summary>
    public bool RoomVisible => Ready && activeNow;

    void OnEnable() { Instance = this; }
    void OnDisable()
    {
        RestoreAmbient();
        if (Instance == this) Instance = null;
    }

    void OnApplicationQuit() { RestoreAmbient(); }

    void OnDestroy() { ReleasePixelTexture(); ReleaseHeroCamera(); }

    void RestoreAmbient()
    {
        if (!ambientSaved) return;
        UnityEngine.RenderSettings.ambientMode = savedAmbientMode;
        UnityEngine.RenderSettings.ambientLight = savedAmbientLight;
        UnityEngine.RenderSettings.ambientIntensity = savedAmbientIntensity;
        UnityEngine.RenderSettings.reflectionIntensity = savedReflectionIntensity;
        ambientSaved = false;
    }

    void Awake()
    {
        Instance = this;
        focusViews = new[]
        {
            Explicit(StartDeskPos, StartDeskEuler, DeskVFov),   // スタート: 地図(camera_desk の 1.3 倍寄り)
            Dolly(LanternCenter, 4.6f),               // 設定: ランタン
            Dolly(LetterCenter, 3.95f),               // 引き継ぎ: 手紙(羽根ペンは装飾)
            Dolly(ShelfCenter, 4.4f),                 // ランキング: 本棚
        };
        menuWeight = new float[MenuCount];
        menuWeight[MenuStart] = 1f;
    }

    // ---- 構築 --------------------------------------------------------------

    void Build()
    {
        if (built) return;
        built = true;
        if (roomPrefab == null || targetTexture == null)
        {
            Debug.LogWarning("TitleRoomController: roomPrefab / targetTexture が未割り当てです。");
            built = false;
            return;
        }

        int layer = LayerMask.NameToLayer("TitleCG");
        if (layer < 0) layer = 0;
        roomLayer = layer;
        // 立ち絵を元解像度で重ねるときだけ、立ち絵とライトを別レイヤーへ分ける。
        // ライトを別にするのは、カメラのカリングマスクがライトにも効くため
        // (部屋カメラと立ち絵カメラの両方から見えるレイヤーが要る)。
        int hero = LayerMask.NameToLayer("TitleHero");
        int lit = LayerMask.NameToLayer("TitleLight");
        bool separate = heroFullRes && hero >= 0 && lit >= 0;
        heroLayer = separate ? hero : layer;
        lightLayer = separate ? lit : layer;

        // 部屋本体。プレハブルートの transform(Blender の右手系相殺の名残で回転・スケールが
        // 入っていることがある)は絶対に上書きしない。位置だけ原点へ置く。
        GameObject room = Instantiate(roomPrefab, transform);
        room.name = "TitleRoom";
        room.transform.localPosition = Vector3.zero;
        roomRoot = room.transform;
        SetLayerRecursive(room, layer);

        lanternTf = FindDeep(roomRoot, "obj_lantern");
        // 机の脇の杖は出さない。主人公の立ち絵が杖を持っているので同じ杖が 2 本に
        // 見える(2026-09-19 のユーザー指摘)。モデルは残したまま枝ごと下ろす。
        Transform staffTf = FindDeep(roomRoot, "obj_staff");
        if (staffTf == null) staffTf = FindDeep(roomRoot, "staff_mesh");
        if (staffTf != null) staffTf.gameObject.SetActive(false);
        cloak1 = FindDeep(roomRoot, "cloak_01");
        cloak2 = FindDeep(roomRoot, "cloak_02");
        if (cloak1 != null) cloak1Home = cloak1.localRotation;
        if (cloak2 != null) cloak2Home = cloak2.localRotation;
        cloak1Rend = CollectRenderers("cloak_01");
        cloak2Rend = CollectRenderers("cloak_02");
        Transform cityTf = FindDeep(roomRoot, "city_lights");
        if (cityTf != null) cityLights = cityTf.GetComponent<Renderer>();
        cityMpb = new MaterialPropertyBlock();
        targetMpb = new MaterialPropertyBlock();
        menuTargets = new[]
        {
            CollectRenderers("map_parchment", "map_ink", "map_blue_linen", "map_fine_engraving", "obj_bottle"),
            CollectRenderers("lantern_frame"),
            // 引き継ぎ=手紙(封筒・封蝋)。羽根ペンとインク壺は装飾なので光らせない。
            CollectRenderers("letter_paper", "envelope_fold", "envelope_fold.001", "wax_seal", "seal_imprint"),
            // ランキング=本棚の中段(本と巻物)。棚枠(bookcase)ごと持ち上げると
            // 左壁一面が明るくなるので、中身だけにする。
            CollectRenderers("shelf_books", "shelf_scrolls_and_chests"),
        };

        BuildHeroBoard(heroLayer);
        BuildLogoBoard(heroLayer);

        // ---- カメラ ----
        GameObject camObj = new GameObject("TitleRoomCamera");
        camObj.transform.SetParent(transform, false);
        roomCamera = camObj.AddComponent<Camera>();
        roomCamera.clearFlags = CameraClearFlags.SolidColor;
        roomCamera.backgroundColor = new Color(0.008f, 0.010f, 0.020f, 1f);
        roomCamera.cullingMask = RoomCullingMask();
        roomCamera.nearClipPlane = 0.05f;
        roomCamera.farClipPlane = 150f;
        roomCamera.depth = -100f;
        EnsurePixelTexture();
        roomCamera.targetTexture = Texture;
        roomCamera.useOcclusionCulling = false;
        // ドット風のときは MSAA を切る(1 ドットの縁がぼけると最近傍拡大の意味が薄れる)。
        roomCamera.allowMSAA = pixelRT == null;
        UniversalAdditionalCameraData data = camObj.AddComponent<UniversalAdditionalCameraData>();
        data.renderType = CameraRenderType.Base;
        data.renderPostProcessing = false;
        data.requiresColorOption = CameraOverrideOption.Off;
        data.requiresDepthOption = CameraOverrideOption.Off;
        data.SetRenderer(rendererIndex);
        EnsureHeroCamera();
        ApplyPose(TitlePos, Quaternion.Euler(TitleEuler), TitleVFov);

        // ---- ライト ----
        // v3_notes の光源表を Unity の実ライトへ写した(エネルギー値は Blender のワット数
        // をそのまま使えないので、参考レンダーと見比べて決めた実測値)。
        // v3_notes の光源色は線形値。Light.color は sRGB として解釈されるので、変換した値を入れる
        // (線形 (0.57,0.65,0.86) → sRGB 約 (0.78,0.82,0.93))。取り違えると部屋全体が青紫に沈む。
        moonLight = CreateLight("MoonKey", LightType.Directional, Vector3.zero,
            new Color(0.78f, 0.82f, 0.93f), moonIntensity, layer);
        moonLight.transform.rotation = Quaternion.LookRotation(new Vector3(0.34f, -0.52f, -0.78f));
        moonLight.shadows = LightShadows.Soft;
        moonLight.shadowStrength = 0.62f;

        fillLight = CreateLight("RoomFill", LightType.Directional, Vector3.zero,
            new Color(0.86f, 0.85f, 0.88f), fillIntensity, layer);
        fillLight.transform.rotation = Quaternion.LookRotation(new Vector3(-0.12f, -0.72f, 0.68f));
        fillLight.shadows = LightShadows.None;

        lanternLight = CreateLight("LanternLight", LightType.Point,
            new Vector3(0.4f, 2.04f, 1.0f), new Color(1f, 0.72f, 0.50f), lanternIntensity, layer);
        lanternLight.range = lanternRange;
        lanternLight.shadows = LightShadows.None;

        selectionLight = CreateLight("SelectionRim", LightType.Point,
            LanternCenter, new Color(1f, 0.86f, 0.68f), 0f, layer);
        selectionLight.range = 1.4f;
        selectionLight.shadows = LightShadows.None;

        cloakLight1 = CreateLight("CloakLight1", LightType.Point,
            new Vector3(4.85f, 3.15f, 1.82f), new Color(0.88f, 0.93f, 1f), 0f, layer);
        cloakLight1.range = 3.2f;
        cloakLight2 = CreateLight("CloakLight2", LightType.Point,
            new Vector3(4.85f, 3.15f, 3.22f), new Color(0.88f, 0.93f, 1f), 0f, layer);
        cloakLight2.range = 3.2f;

        BuildDust(layer);
        ApplyExposure();
    }

    // 立ち絵を部屋の 3D 空間へ板として立てる。
    //   ・URP/Lit のアルファカットアウト(両面)。部屋のライトを受ける。
    //   ・カメラの方を水平にだけ向く(あおりは付けない)。
    //   ・足元に接地の影(円形グラデーションの板)を敷く。
    // フェード(カメラが寄るときに消える)はベース色の alpha とカットオフを
    // 同じ比率で動かす。比率が同じなので抜きの形はフェード中も変わらない。
    int RoomCullingMask()
    {
        int m = 1 << roomLayer;
        if (lightLayer >= 0) m |= 1 << lightLayer;
        // 立ち絵を分離していないときは同じレイヤーなのでこのまま部屋カメラが描く。
        return m;
    }

    // 透明の RT へ描くとき、アルファチャンネルが二重に掛からないようにする。
    // 色は SrcAlpha/OneMinusSrcAlpha のまま、アルファだけ One/OneMinusSrcAlpha にすると
    // RT の中身が (色 x アルファ, アルファ) = プリマルチプライドになる。
    // 表示側は BulletHell/UI/Premultiplied で重ねる。
    static void SetPremultipliedAlphaWrite(Material mat)
    {
        if (mat.HasProperty("_SrcBlendAlpha"))
            mat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        if (mat.HasProperty("_DstBlendAlpha"))
            mat.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
    }

    // 立ち絵だけを 1920x1080 で描く 2 台目のカメラ。部屋カメラを複製して作る
    // (URP のレンダラ番号・設定をそのまま引き継ぐため)。
    void EnsureHeroCamera()
    {
        bool want = heroFullRes && heroRenderer != null && heroLayer >= 0 && heroLayer != roomLayer;
        if (!want) { ReleaseHeroCamera(); return; }
        if (roomCamera == null) return;

        if (heroRT == null)
        {
            // 書式・MSAA はシーンの 1920x1080 RT に合わせる(合わせないと立ち絵の
            // 線の縁だけ従来と変わる)。
            heroRT = new RenderTexture(1920, 1080, 32,
                targetTexture != null ? targetTexture.format : RenderTextureFormat.DefaultHDR)
            {
                name = "TitleHeroRT",
                filterMode = FilterMode.Bilinear,
                antiAliasing = targetTexture != null ? targetTexture.antiAliasing : 1,
                useMipMap = false,
                autoGenerateMips = false,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            heroRT.Create();
        }
        if (heroCamera == null)
        {
            GameObject go = Instantiate(roomCamera.gameObject, transform);
            go.name = "TitleHeroCamera";
            go.hideFlags = HideFlags.DontSave;
            heroCamera = go.GetComponent<Camera>();
        }
        heroCamera.cullingMask = (1 << heroLayer) | (lightLayer >= 0 ? 1 << lightLayer : 0);
        heroCamera.clearFlags = CameraClearFlags.SolidColor;
        heroCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        heroCamera.targetTexture = heroRT;
        heroCamera.allowMSAA = heroRT.antiAliasing > 1;
        // 部屋より後に描く(表示は RawImage の重ね順で決めるので描画順は問わない)。
        heroCamera.depth = roomCamera.depth + 1f;
        heroCamera.useOcclusionCulling = false;
    }

    void ReleaseHeroCamera()
    {
        if (heroCamera != null)
        {
            heroCamera.targetTexture = null;
            DestroyImmediate(heroCamera.gameObject);
            heroCamera = null;
        }
        if (heroRT != null)
        {
            heroRT.Release();
            DestroyImmediate(heroRT);
            heroRT = null;
        }
    }

    void BuildHeroBoard(int layer)
    {
        Texture heroTex = heroSprite != null ? heroSprite.texture : null;
        if (heroTex == null) return;

        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) return;
        Material mat = new Material(lit) { hideFlags = HideFlags.DontSave, name = "HeroBoardMat" };
        mat.SetFloat("_Surface", 1f);              // Transparent
        mat.SetFloat("_Blend", 0f);                // Alpha
        mat.SetFloat("_AlphaClip", 1f);
        mat.SetFloat("_Cutoff", 0.5f);
        mat.SetFloat("_Cull", 0f);                 // 両面
        mat.SetFloat("_Smoothness", 0.05f);
        mat.SetFloat("_SpecularHighlights", 0f);
        mat.SetFloat("_EnvironmentReflections", 0f);
        mat.SetFloat("_ZWrite", 1f);
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        mat.SetTexture("_BaseMap", heroTex);
        mat.SetColor("_BaseColor", Color.white);
        SetPremultipliedAlphaWrite(mat);
        ApplyHeroRim(mat, heroTex);

        GameObject board = GameObject.CreatePrimitive(PrimitiveType.Quad);
        board.name = "HeroBoard";
        DestroyImmediate(board.GetComponent<Collider>());
        // 部屋プレハブのルートには X 反転(scale -1)が入っているので、板は Rig 直下へ置く。
        board.transform.SetParent(transform, false);
        heroRenderer = board.GetComponent<Renderer>();
        heroRenderer.sharedMaterial = mat;
        heroRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        heroRenderer.receiveShadows = true;
        heroBoard = board.transform;
        SetLayerRecursive(board, layer);

        // 足元の接地影(円形のグラデーションを焼いた Unlit の板)。
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlit != null)
        {
            Material sm = new Material(unlit) { hideFlags = HideFlags.DontSave, name = "HeroShadowMat" };
            sm.SetFloat("_Surface", 1f);
            sm.SetFloat("_Blend", 0f);
            sm.SetFloat("_ZWrite", 0f);
            sm.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            sm.SetTexture("_BaseMap", CreateSoftDiscTexture(64));
            sm.SetColor("_BaseColor", new Color(0f, 0f, 0f, heroShadowAlpha));
            SetPremultipliedAlphaWrite(sm);
            sm.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            GameObject shade = GameObject.CreatePrimitive(PrimitiveType.Quad);
            shade.name = "HeroShadow";
            DestroyImmediate(shade.GetComponent<Collider>());
            shade.transform.SetParent(transform, false);
            heroShadowRenderer = shade.GetComponent<Renderer>();
            heroShadowRenderer.sharedMaterial = sm;
            heroShadowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            heroShadowRenderer.receiveShadows = false;
            heroShadow = shade.transform;
            SetLayerRecursive(shade, layer);
        }
        ApplyHeroTransform(0f);
    }

    // ---- タイトルロゴの板 --------------------------------------------------

    // ロゴは立ち絵と同じ「元解像度で描くレイヤー」へ置く。部屋は 640x360 の
    // ドット風なので、部屋レイヤーに置くとドット絵のロゴがさらに潰れる。
    void BuildLogoBoard(int layer)
    {
        Texture tex = Resources.Load<Texture2D>("UI/jbi-logo-pixel");
        if (tex == null) tex = Resources.Load<Texture2D>("UI/jbi-logo-v2");
        if (tex == null) return;

        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlit == null) return;
        Material mat = new Material(unlit) { hideFlags = HideFlags.DontSave, name = "TitleLogoMat" };
        mat.SetFloat("_Surface", 1f);      // Transparent
        mat.SetFloat("_Blend", 0f);        // Alpha
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_Cull", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", Color.white);
        SetPremultipliedAlphaWrite(mat);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 10;

        GameObject board = GameObject.CreatePrimitive(PrimitiveType.Quad);
        board.name = "TitleLogoBoard";
        DestroyImmediate(board.GetComponent<Collider>());
        board.transform.SetParent(transform, false);
        logoRenderer = board.GetComponent<Renderer>();
        logoRenderer.sharedMaterial = mat;
        logoRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        logoRenderer.receiveShadows = false;
        logoBoard = board.transform;
        SetLayerRecursive(board, layer);
        logoAspect = tex.height > 0 ? (float)tex.width / tex.height : 2f;
        ApplyLogoTransform();
    }

    float logoAspect = 2f;

    // 全景カメラで「画面中心から (px,py) の所へ幅 w px」で写るように板を置く。
    //   ・視野の高さ(世界単位) = 2 * d * tan(vFov/2) が画面 1080px に対応する。
    //   ・奥行き d は「板の中心の z が logoPlaneZ になる」条件から解く。
    void ApplyLogoTransform()
    {
        if (logoBoard == null) return;
        Quaternion rot = Quaternion.Euler(TitleEuler);
        Vector3 fwd = rot * Vector3.forward;
        Vector3 right = rot * Vector3.right;
        Vector3 up = rot * Vector3.up;
        // 画面 1px あたりの世界の大きさ(距離 1 のとき)。
        float k = 2f * Mathf.Tan(TitleVFov * 0.5f * Mathf.Deg2Rad) / 1080f;
        float px = logoScreenPos.x;
        float py = logoScreenPos.y + logoBobPx;
        float denom = fwd.z + right.z * px * k + up.z * py * k;
        if (Mathf.Abs(denom) < 1e-5f) return;
        float d = (logoPlaneZ - TitlePos.z) / denom;
        if (d <= 0.1f) return;
        Vector3 center = TitlePos + fwd * d + right * (px * k * d) + up * (py * k * d);
        float wWorld = logoScreenWidth * k * d;
        logoBoard.localPosition = center;
        logoBoard.localRotation = rot;    // Unity の Quad は -Z が表。カメラと同じ回転で正対する
        logoBoard.localScale = new Vector3(wWorld, wWorld / Mathf.Max(0.01f, logoAspect), 1f);
    }

    /// <summary>ロゴの見え方(退場フェードなど)。1 で表示、0 で消える。</summary>
    public void SetLogoAlpha(float alpha)
    {
        if (logoRenderer == null) return;
        alpha = Mathf.Clamp01(alpha);
        bool visible = alpha > 0.004f;
        if (logoRenderer.enabled != visible) logoRenderer.enabled = visible;
        if (!visible) return;
        logoMpb ??= new MaterialPropertyBlock();
        logoRenderer.GetPropertyBlock(logoMpb);
        logoMpb.SetColor("_BaseColor", new Color(1f, 1f, 1f, alpha));
        logoRenderer.SetPropertyBlock(logoMpb);
    }

    // ---- 立ち絵の左リムライト ----------------------------------------------

    /// <summary>立ち絵の材質へ「左を向いた縁だけ橙に光る」自発光を仕込む。</summary>
    void ApplyHeroRim(Material mat, Texture heroTex)
    {
        if (mat == null) return;
        if (heroRimStrength <= 0.0001f)
        {
            mat.DisableKeyword("_EMISSION");
            mat.SetColor(HeroEmissionColorId, Color.black);
            return;
        }
        if (heroRimTex == null) heroRimTex = BuildLeftRimTexture(heroTex, heroRimWidth);
        if (heroRimTex == null) return;
        mat.EnableKeyword("_EMISSION");
        mat.SetTexture("_EmissionMap", heroRimTex);
        mat.SetColor(HeroEmissionColorId, heroRimColor * heroRimStrength);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;   // リアルタイムのみ
    }

    /// <summary>
    /// 原画の左向きの縁だけを残したマスク(白 = リム)。
    /// 読み取り不可のインポート設定でも使えるよう RenderTexture 経由で取り出す。
    /// </summary>
    static Texture2D BuildLeftRimTexture(Texture src, float widthTexels)
    {
        if (src == null) return null;
        int w = src.width, h = src.height;
        if (w <= 2 || h <= 2) return null;

        RenderTexture rt = RenderTexture.GetTemporary(w, h, 0,
            RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture prev = RenderTexture.active;
        Graphics.Blit(src, rt);
        RenderTexture.active = rt;
        Texture2D copy = new Texture2D(w, h, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
        copy.ReadPixels(new Rect(0f, 0f, w, h), 0, 0, false);
        copy.Apply(false);
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        Color32[] srcPx = copy.GetPixels32();
        Color32[] outPx = new Color32[w * h];
        float width = Mathf.Max(1f, widthTexels);
        const byte cut = 128;   // 材質のアルファカットオフ 0.5 と同じしきい値
        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            // 行を左から走査し、「透明 → 不透明」へ変わった点から右へ減衰させる。
            // 腕と胴のすき間など内側の縁にも同じように乗る(左からの光なので正しい)。
            int since = int.MaxValue;
            bool prevOpaque = false;
            for (int x = 0; x < w; x++)
            {
                bool opaque = srcPx[row + x].a >= cut;
                if (opaque && !prevOpaque) since = 0;
                else if (opaque && since != int.MaxValue) since++;
                else if (!opaque) since = int.MaxValue;
                prevOpaque = opaque;

                byte v = 0;
                if (opaque && since != int.MaxValue)
                {
                    // 縁で 1、width テクセルで 0 へ。二乗で立ち上がりを締める。
                    float k = Mathf.Clamp01(1f - since / width);
                    k *= k;
                    v = (byte)Mathf.RoundToInt(k * 255f);
                }
                outPx[row + x] = new Color32(v, v, v, 255);
            }
        }
        DestroyImmediate(copy);

        Texture2D rim = new Texture2D(w, h, TextureFormat.RGBA32, false, false)
        {
            name = "HeroRimMask",
            hideFlags = HideFlags.DontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        rim.SetPixels32(outPx);
        rim.Apply(false);
        return rim;
    }

    // 中心が濃く外周で 0 になる円。接地影に使う。
    static Texture2D CreateSoftDiscTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.DontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float av = Mathf.Clamp01(1f - d);
                av *= av;               // 中心を濃く、外周をなだらかに
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, av));
            }
        }
        tex.Apply();
        return tex;
    }

    // 板の位置・大きさ・向きを現在の設定から作り直す。slide は寄りのときに右へ逃がす量。
    void ApplyHeroTransform(float slide)
    {
        if (heroBoard == null) return;
        Texture tex = heroSprite != null ? heroSprite.texture : null;
        float aspect = tex != null && tex.height > 0 ? (float)tex.width / tex.height : 709f / 1024f;
        float h = Mathf.Max(0.01f, heroHeight);
        Vector3 foot = heroFootPos + new Vector3(slide, 0f, 0f);
        heroBoard.localPosition = foot + new Vector3(0f, h * 0.5f, 0f);
        heroBoard.localScale = new Vector3(h * aspect, h, 1f);
        // 水平方向だけ全景カメラの方を向く。
        Vector3 toCam = TitlePos - foot;
        toCam.y = 0f;
        if (toCam.sqrMagnitude > 1e-4f)
            heroBoard.localRotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up);
        if (heroShadow != null)
        {
            heroShadow.localPosition = foot + new Vector3(0f, 0.03f, 0.05f);
            heroShadow.localRotation = Quaternion.Euler(90f, 0f, 0f);
            heroShadow.localScale = new Vector3(heroShadowSize, heroShadowSize * 0.55f, 1f);
        }
    }

    /// <summary>立ち絵の見え方を更新する。fade=1 で表示、0 で消える。slide は右へ逃がす量。</summary>
    public void SetHeroState(float fade, float slide)
    {
        if (heroRenderer == null) return;
        fade = Mathf.Clamp01(fade);
        ApplyHeroTransform(slide);
        bool visible = fade > 0.01f;
        if (heroRenderer.enabled != visible) heroRenderer.enabled = visible;
        if (heroShadowRenderer != null && heroShadowRenderer.enabled != visible)
            heroShadowRenderer.enabled = visible;
        if (!visible) return;
        heroMpb ??= new MaterialPropertyBlock();
        heroRenderer.GetPropertyBlock(heroMpb);
        float bb = heroBrightness;
        heroMpb.SetColor(HeroBaseColorId, new Color(bb, bb, bb, fade));
        heroMpb.SetFloat(HeroCutoffId, 0.5f * fade);
        heroRenderer.SetPropertyBlock(heroMpb);
        if (heroShadowRenderer != null)
        {
            heroShadowRenderer.GetPropertyBlock(heroMpb);
            heroMpb.SetColor(HeroBaseColorId, new Color(0f, 0f, 0f, heroShadowAlpha * fade));
            heroShadowRenderer.SetPropertyBlock(heroMpb);
        }
    }

    /// <summary>立ち絵の板があるか(TitleManager が 2D の立ち絵を出すかの判断に使う)。</summary>
    public bool HasHeroBoard => heroRenderer != null;

    Light CreateLight(string objectName, LightType type, Vector3 pos, Color color, float intensity, int layer)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = pos;
        // ライトは部屋カメラと立ち絵カメラの両方から見える必要がある(分離時は TitleLight)。
        go.layer = lightLayer >= 0 ? lightLayer : layer;
        Light light = go.AddComponent<Light>();
        light.type = type;
        light.color = color;
        light.intensity = intensity;
        light.shadows = LightShadows.None;
        // URP はライトのカリングマスクを使わないので、部屋以外へ漏れないよう
        // レイヤーではなく「タイトル中だけ有効化する」ことで隔離する。
        light.renderingLayerMask = 1;
        return light;
    }

    // 舞う塵。数十粒・薄く・ゆっくり。机の上あたりを漂わせる。
    void BuildDust(int layer)
    {
        GameObject go = new GameObject("Dust");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(-0.6f, 2.4f, 0.4f);
        go.layer = layer;
        dust = go.AddComponent<ParticleSystem>();
        ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        Material mat = new Material(shader) { hideFlags = HideFlags.DontSave };
        mat.mainTexture = CreateDotTexture();
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 1f); // additive
        mat.SetColor("_BaseColor", new Color(1f, 0.86f, 0.66f, 1f));
        mat.renderQueue = 3000;
        psr.sharedMaterial = mat;
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        psr.alignment = ParticleSystemRenderSpace.View;

        // AddComponent 直後の ParticleSystem は再生中で、そのまま duration を書くと
        // Assert が出る。設定前に完全停止させる。
        dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = dust.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 8f;
        main.startLifetime = 9f;
        main.startSpeed = 0.045f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.03f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.88f, 0.7f, 0.16f), new Color(0.86f, 0.9f, 1f, 0.10f));
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 60;
        main.gravityModifier = -0.004f;
        main.prewarm = true;

        ParticleSystem.EmissionModule emission = dust.emission;
        emission.rateOverTime = 6f;

        ParticleSystem.ShapeModule shape = dust.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(5.2f, 2.4f, 3.2f);

        ParticleSystem.NoiseModule noise = dust.noise;
        noise.enabled = true;
        noise.strength = 0.09f;
        noise.frequency = 0.25f;
        noise.scrollSpeed = 0.06f;

        ParticleSystem.ColorOverLifetimeModule col = dust.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);
    }

    static Texture2D CreateDotTexture()
    {
        const int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { hideFlags = HideFlags.DontSave };
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size - 0.5f;
                float dy = (y + 0.5f) / size - 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                float a = Mathf.Clamp01(1f - r);
                a = a * a;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return tex;
    }

    Renderer[] CollectRenderers(params string[] names)
    {
        List<Renderer> found = new List<Renderer>();
        foreach (string n in names)
        {
            Transform t = FindDeep(roomRoot, n);
            if (t == null) continue;
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true)) found.Add(r);
        }
        return found.ToArray();
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform) SetLayerRecursive(child.gameObject, layer);
    }

    static Transform FindDeep(Transform root, string wanted)
    {
        if (root.name == wanted) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeep(root.GetChild(i), wanted);
            if (found != null) return found;
        }
        return null;
    }

    // ---- ドット風表示 ------------------------------------------------------

    // 低解像度の描画先とマテリアルを現在の設定に合わせる。pixelate=false なら
    // 何も作らず、シーンに割り当てられた 1920x1080 の RT をそのまま使う。
    void EnsurePixelTexture()
    {
        if (!pixelate)
        {
            ReleasePixelTexture();
            return;
        }
        int w = Mathf.Clamp(pixelWidth, 32, 1920);
        int h = Mathf.Clamp(pixelHeight, 18, 1080);
        if (pixelRT != null && (pixelRT.width != w || pixelRT.height != h)) ReleasePixelTexture();
        if (pixelRT == null)
        {
            // 元 RT と同じ書式(HDR)で解像度だけ落とす。filterMode=Point で最近傍拡大になる。
            pixelRT = new RenderTexture(w, h, 24,
                targetTexture != null ? targetTexture.format : RenderTextureFormat.DefaultHDR)
            {
                name = "TitleRoomPixelRT",
                filterMode = FilterMode.Point,
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            pixelRT.Create();
        }
        EnsurePixelMaterial();
    }

    void EnsurePixelMaterial()
    {
        if (pixelatePalette <= 1)
        {
            if (pixelMat != null) { DestroyImmediate(pixelMat); pixelMat = null; }
            return;
        }
        if (pixelMat == null)
        {
            Shader sh = Shader.Find("BulletHell/UI/PixelQuantize");
            if (sh == null) return;
            pixelMat = new Material(sh) { hideFlags = HideFlags.DontSave };
        }
        pixelMat.SetFloat("_PixelatePalette", pixelatePalette);
        pixelMat.SetFloat("_PixelateDither", pixelateDither);
    }

    void ReleasePixelTexture()
    {
        if (pixelRT != null)
        {
            if (roomCamera != null && roomCamera.targetTexture == pixelRT)
                roomCamera.targetTexture = targetTexture;
            pixelRT.Release();
            DestroyImmediate(pixelRT);
            pixelRT = null;
        }
        if (pixelMat != null) { DestroyImmediate(pixelMat); pixelMat = null; }
    }

    /// <summary>
    /// ドット風表示を切り替える(比較用。既定は 480x270)。RawImage 側は
    /// <see cref="Texture"/> / <see cref="ViewMaterial"/> を毎フレーム見て貼り替える。
    /// </summary>
    public void SetPixelate(bool on, int width, int height, int palette = -1, float dither = -1f)
    {
        pixelate = on;
        if (width > 0) pixelWidth = width;
        if (height > 0) pixelHeight = height;
        if (palette >= 0) pixelatePalette = palette;
        if (dither >= 0f) pixelateDither = dither;
        if (!built) return;
        EnsurePixelTexture();
        if (roomCamera != null)
        {
            roomCamera.targetTexture = Texture;
            roomCamera.allowMSAA = pixelRT == null;
        }
    }

    // ---- 制御 --------------------------------------------------------------

    /// <summary>タイトル表示中だけ部屋を動かす。false でカメラ・ライト・部屋をすべて止める。</summary>
    public void SetRoomActive(bool on)
    {
        if (on) Build();
        if (!built) return;
        activeNow = on;
        if (roomRoot != null) roomRoot.gameObject.SetActive(on);
        if (heroBoard != null) heroBoard.gameObject.SetActive(on);
        if (heroShadow != null) heroShadow.gameObject.SetActive(on);
        if (logoBoard != null) logoBoard.gameObject.SetActive(on);
        if (roomCamera != null) roomCamera.gameObject.SetActive(on);
        if (heroCamera != null) heroCamera.gameObject.SetActive(on);
        foreach (Light light in new[] { moonLight, fillLight, lanternLight, selectionLight, cloakLight1, cloakLight2 })
            if (light != null) light.gameObject.SetActive(on);
        if (dust != null)
        {
            dust.gameObject.SetActive(on);
            if (on) dust.Play();
        }
        if (on)
        {
            // 環境光を部屋向け(暗い藍)へ差し替える。既定のフラット灰 0.21 のままでは
            // 室内が一様に明るくなり、参考レンダーの夜の油彩にならない。部屋を消すときに
            // 必ず元へ戻す(タイトル以外の画面に影響を残さない)。
            if (!globalsOwned)
            {
                // ステージ選択から戻っている最中。環境光・反射は街側が 1 本のカーブで
                // 送り返すので、部屋は触らない(2026-09-16)。
            }
            else if (!ambientSaved)
            {
                savedAmbientMode = UnityEngine.RenderSettings.ambientMode;
                savedAmbientLight = UnityEngine.RenderSettings.ambientLight;
                savedAmbientIntensity = UnityEngine.RenderSettings.ambientIntensity;
                savedReflectionIntensity = UnityEngine.RenderSettings.reflectionIntensity;
                ambientSaved = true;
            }
            if (globalsOwned)
            {
                UnityEngine.RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                UnityEngine.RenderSettings.ambientIntensity = 1f;
                UnityEngine.RenderSettings.ambientLight = ambientColor * exposure;
                // 既定の skybox 反射(グレーのデフォルトキューブ)が鏡面環境光として全面に乗り、
                // 天井・床・棚が参考レンダーの 5〜8 倍明るくなっていた。部屋を出しているあいだは切る。
                UnityEngine.RenderSettings.reflectionIntensity = 0f;
            }
            exitDim = 1f;   // 前回の退場フェードの残りを持ち越さない
            ApplyExposure();
            zoomTarget = -1;
            zoomProgress = 0f;
            // 発光の重みは表示のたびに現在の選択へ合わせておく(前回の残りでちらつかせない)。
            if (menuWeight != null)
                for (int i = 0; i < menuWeight.Length; i++) menuWeight[i] = i == selection ? 1f : 0f;
            cloakWeightP1 = twoPlayer ? 0f : 1f;
            cloakWeightP2 = twoPlayer ? 1f : 0f;
            ApplyPose(TitlePos, Quaternion.Euler(TitleEuler), TitleVFov);
        }
        else if (ambientSaved)
        {
            UnityEngine.RenderSettings.ambientMode = savedAmbientMode;
            UnityEngine.RenderSettings.ambientLight = savedAmbientLight;
            UnityEngine.RenderSettings.ambientIntensity = savedAmbientIntensity;
            UnityEngine.RenderSettings.reflectionIntensity = savedReflectionIntensity;
            ambientSaved = false;
        }
    }

    // 退場クロスフェード用の減光係数(1=通常 / 0=部屋を消した後と同じ見え方)。
    // URP はライトのカリングマスクを使わないため、部屋の平行光(月・フィル)は
    // ステージ選択の「城壁の街」まで照らしてしまう。部屋を一気に消すと街の明るさが
    // 1 コマで跳ぶので、クロスフェードと同じカーブでここを 1→0 に落としてから
    // 部屋を消す(2026-09-15。実測で跳び -17.3/255 の原因はこの 2 灯だけだった)。
    float exitDim = 1f;

    // ステージ選択から戻る演出の間は、グローバルの環境光・反射(RenderSettings)を
    // 街側に任せる。街の SetEntranceDim は「選択画面が持っていた値 → 街の値」を
    // 補間して持っているので、退場ではその逆再生がそのまま正しい受け渡しになる。
    // 部屋がここで自分の値(暗い藍・反射 0)を書くと、まだ映っている街が 1 コマで
    // 暗くなり、街を片付けた瞬間に既定値へ戻って明るさが跳ぶ(実測 -12 → +10 / 255)。
    bool globalsOwned = true;

    /// <summary>この部屋が RenderSettings の環境光・反射を書いてよいか。</summary>
    public void SetGlobalsOwned(bool on)
    {
        globalsOwned = on;
        if (!on) ambientSaved = false;   // 以後この部屋は環境光・反射を触らない
    }

    /// <summary>退場クロスフェード中の減光。1=通常、0=部屋を消した後と同じ明るさ。</summary>
    public void SetExitDim(float k)
    {
        k = Mathf.Clamp01(k);
        if (Mathf.Approximately(k, exitDim)) return;
        exitDim = k;
        if (built) ApplyExposure();
    }

    /// <summary>メニュー選択の変化を部屋のハイライトへ反映する。</summary>
    public void SetSelection(int menuIndex) => selection = Mathf.Clamp(menuIndex, 0, MenuCount - 1);

    /// <summary>1P/2P の選択。マントの光る枚数で示す。</summary>
    public void SetTwoPlayer(bool two) => twoPlayer = two;

    /// <summary>決定でそのメニューのオブジェクトへ寄る。index &lt; 0 で全景へ戻る。</summary>
    public void FocusMenu(int menuIndex) => zoomTarget = menuIndex;

    /// <summary>全景へ戻す。</summary>
    public void ClearFocus() => zoomTarget = -1;

    /// <summary>ステージ選択からの復帰(入場の逆再生)。地図へ寄り切った姿勢から
    /// 始めて、Tick が ZoomDuration かけて全景まで引き戻す。SetRoomActive(true) が
    /// 寄りを 0 に戻した直後に呼ぶこと(2026-09-16)。</summary>
    public void BeginReturnFromFocus(int menuIndex)
    {
        if (!built) return;
        selection = Mathf.Clamp(menuIndex, 0, MenuCount - 1);
        zoomTarget = -1;        // 目標は全景
        zoomProgress = 1f;      // いまは寄り切った状態
        UpdateCamera(selection, zoomProgress);   // 1 コマ目から寄り切った絵を出す
    }

    // ---- 選択メニューへの視線の微調整(2026-09-19 指示) ----------------------
    // 全景のまま、選択中のメニューの対象へ yaw/pitch を数度だけ振る。
    // 寄り(zoomProgress)が進むと寄りの姿勢が主になるので、演出とは干渉しない。
    [Header("選択メニューへ振る視線")]
    [Tooltip("全景から対象を正面に捉える姿勢まで、どれだけ寄せるか。0.15 で約 2〜3 度。")]
    [Range(0f, 0.6f)] public float menuLookAmount = 0.15f;
    [Tooltip("視線が切り替わるまでの時間(秒・臨界減衰)。")]
    public float menuLookSmoothTime = 0.12f;
    Vector3 menuLookNow;
    Vector3 menuLookVel;
    bool menuLookReady;

    void UpdateMenuLook(float dt)
    {
        Vector3 want = SelectionCenter(selection);
        if (!menuLookReady) { menuLookNow = want; menuLookVel = Vector3.zero; menuLookReady = true; return; }
        menuLookNow = Vector3.SmoothDamp(menuLookNow, want, ref menuLookVel,
            Mathf.Max(0.01f, menuLookSmoothTime), Mathf.Infinity, dt);
    }

    // 全景の姿勢に、選択中の対象への振りを混ぜたもの。
    Quaternion OverviewRotation()
    {
        Quaternion home = Quaternion.Euler(TitleEuler);
        if (menuLookAmount <= 0.0001f || !menuLookReady) return home;
        Vector3 dir = menuLookNow - TitlePos;
        if (dir.sqrMagnitude < 1e-4f) return home;
        return Quaternion.Slerp(home, Quaternion.LookRotation(dir, Vector3.up), menuLookAmount);
    }

    public void Tick(float dt)
    {
        if (!built || !activeNow) return;
        time += dt;

        UpdateMenuLook(dt);

        // 寄り/戻りの進み(ease-out cubic は姿勢の補間側で掛ける)。
        float target = zoomTarget >= 0 ? 1f : 0f;
        float step = dt / ZoomDuration;
        zoomProgress = Mathf.MoveTowards(zoomProgress, target, step);

        int viewIndex = zoomTarget >= 0 ? zoomTarget : selection;
        UpdateCamera(viewIndex, zoomProgress);
        UpdateFlicker(dt);
        UpdateHighlight(dt);
        UpdateCloaks(dt);
    }

    // ステージ選択への受け渡し中だけ立てる。寄りの終端で速度を 0 に落とさず、
    // 加速したまま街のスイープへ渡す(2026-09-19 指示「動く方向が連続になるように」)。
    // ease = p^2 は 往きで「遅→速」、戻りで「速→遅」になるので、入場・退場の
    // どちらでも継ぎ目側が最大速度になる。
    bool zoomHandoff;

    /// <summary>ステージ選択との受け渡し中か。true のあいだ寄りは ease-in(p^2)。</summary>
    public void SetZoomHandoff(bool on) => zoomHandoff = on;

    /// <summary>いまの寄りの「進みに対する姿勢の進み」(グラフ検証用)。</summary>
    public float ZoomEase => zoomHandoff
        ? zoomProgress * zoomProgress
        : zoomProgress * zoomProgress * (3f - 2f * zoomProgress);

    void UpdateCamera(int viewIndex, float progress)
    {
        Quaternion home = OverviewRotation();
        Vector3 pos = TitlePos;
        Quaternion rot = home;
        float fov = TitleVFov;
        if (progress > 0f && focusViews != null && viewIndex >= 0 && viewIndex < focusViews.Length)
        {
            ResolveFocus(focusViews[viewIndex], out Vector3 fpos, out Quaternion frot, out float ffov);
            // 通常は ease-in-out(SmoothStep)。速度が中点で最大になるので、そこへ
            // クロスフェードの中心を合わせられる(旧: ease-out cubic = 出だしが最速)。
            // 受け渡し中だけ ease-in(p^2)にして、終端で速度を 0 に落とさない。
            float ease = ZoomEase;
            pos = Vector3.Lerp(TitlePos, fpos, ease);
            rot = Quaternion.Slerp(home, frot, ease);
            fov = Mathf.Lerp(TitleVFov, ffov, ease);
        }
        ApplyPose(pos, rot, fov);
    }

    void ResolveFocus(FocusView view, out Vector3 pos, out Quaternion rot, out float fov)
    {
        fov = view.vFov;
        if (view.explicitPose)
        {
            pos = view.pos;
            rot = Quaternion.Euler(view.euler);
            return;
        }
        // 全景カメラから対象へまっすぐ寄る(ドリー)。視線方向が変わらないので
        // 「同じ場所へ近づいた」と読める。
        Vector3 dir = (TitlePos - view.target).normalized;
        pos = view.target + dir * view.dollyDist;
        rot = Quaternion.LookRotation(view.target - pos, Vector3.up);
    }

    void ApplyPose(Vector3 pos, Quaternion rot, float vFov)
    {
        if (roomCamera == null) return;
        roomCamera.transform.SetPositionAndRotation(pos, rot);
        roomCamera.fieldOfView = vFov;
        if (heroCamera != null)
        {
            // 立ち絵カメラは部屋カメラと同じ姿勢・同じ画角(RT の縦横比も 16:9 で同じ)。
            heroCamera.transform.SetPositionAndRotation(pos, rot);
            heroCamera.fieldOfView = vFov;
        }
    }

    // ランタンの炎: 3〜5Hz のゆらぎ(拍とは無関係)。窓の外の灯りも弱く明滅させる。
    void UpdateFlicker(float dt)
    {
        float n =
            0.55f * Mathf.Sin(time * 3.1f * Mathf.PI * 2f) +
            0.30f * Mathf.Sin(time * 4.7f * Mathf.PI * 2f + 1.7f) +
            0.15f * Mathf.Sin(time * 6.3f * Mathf.PI * 2f + 3.9f);
        float wanted = 1f + 0.085f * n;
        flicker = Mathf.SmoothDamp(flicker, wanted, ref flickerVel, 0.05f, Mathf.Infinity, dt);
        if (lanternLight != null) lanternLight.intensity = lanternIntensity * exposure * flicker;

        if (cityLights != null)
        {
            float cityPulse = 1f + 0.10f * Mathf.Sin(time * 0.9f) + 0.06f * Mathf.Sin(time * 2.3f + 2.1f);
            cityLights.GetPropertyBlock(cityMpb);
            // 材質の焼き込み値(白 0.90 × アトラス)を基準に、ゆっくり明滅させる。
            cityMpb.SetColor("_EmissionColor", new Color(0.90f, 0.90f, 0.90f) * cityPulse);
            cityLights.SetPropertyBlock(cityMpb);
        }
    }

    // 選択中のオブジェクトを暖色の弱い点光源で持ち上げる(リム発光の代用)。
    // ランタンが選ばれているときはランタン自身の光もわずかに強まる。
    void UpdateHighlight(float dt)
    {
        // 選択中=1 / 非選択=0 へ 0.15 秒でクロスフェードする(第8便。第7便の
        // 「非選択も弱く光る」常時ハイライトは廃止)。
        float step = dt / HighlightFadeDuration;
        for (int i = 0; i < menuWeight.Length; i++)
            menuWeight[i] = Mathf.MoveTowards(menuWeight[i], i == selection ? 1f : 0f, step);

        if (selectionLight != null)
        {
            // 重み付き平均で位置・範囲・強さを作る。切替中はリム光が対象間を移動する。
            Vector3 center = Vector3.zero;
            float sum = 0f, range = 0f, intensity = 0f;
            for (int i = 0; i < menuWeight.Length; i++)
            {
                float w = menuWeight[i];
                if (w <= 0f) continue;
                center += SelectionCenter(i) * w;
                range += SelectionRange(i) * w;
                intensity += SelectionIntensity(i) * w;
                sum += w;
            }
            if (sum > 0.0001f) { center /= sum; range /= sum; }
            else { center = SelectionCenter(selection); range = SelectionRange(selection); }
            float pulse = 1f + 0.12f * Mathf.Sin(time * 2.2f);
            selectionLight.transform.localPosition = center;
            selectionLight.range = range;
            selectionLight.intensity = intensity * exposure * pulse;
        }
        // 設定はランタンそのものが対象なので、リムではなくランタンの光を強める。
        if (lanternLight != null && menuWeight[MenuOptions] > 0f)
        {
            lanternLight.intensity *= Mathf.Lerp(1f, 1.7f, menuWeight[MenuOptions]);
        }
        ApplyTargetGlow();
    }

    // ---- ▼マーカーの投影(第8便) ---------------------------------------------

    /// <summary>マーカー <paramref name="index"/> の 3D アンカーを部屋カメラのビューポート
    /// 座標へ投影する。画面外・カメラ後方なら false。</summary>
    public bool TryGetMarkerViewport(int index, out Vector2 viewport)
    {
        viewport = Vector2.zero;
        if (roomCamera == null || index < 0 || index >= MarkerAnchors.Length) return false;
        Vector3 vp = roomCamera.WorldToViewportPoint(MarkerAnchors[index]);
        if (vp.z <= 0f) return false;
        viewport = new Vector2(vp.x, vp.y);
        // 少し外側まで許容して、寄りで画面際へ出るときに突然消えないようにする。
        return vp.x > -0.25f && vp.x < 1.25f && vp.y > -0.25f && vp.y < 1.25f;
    }

    // 選択中のオブジェクトの実体を暖色寄りに持ち上げ、他は素の色へ戻す。
    // 手紙と羽根ペンのように 0.4m しか離れていない相手でも、どちらが選ばれているか
    // 一目で分かるようにするための処理(点光源だけでは分離できない)。
    void ApplyTargetGlow()
    {
        if (menuTargets == null) return;
        float pulse = 1f + 0.10f * Mathf.Sin(time * 2.2f);
        for (int g = 0; g < menuTargets.Length; g++)
        {
            float w = g < menuWeight.Length ? menuWeight[g] : 0f;
            Color lit = new Color(1.85f, 1.62f, 1.30f, 1f) * pulse;
            Color c = Color.Lerp(Color.white, lit, w);
            Renderer[] group = menuTargets[g];
            if (group == null) continue;
            foreach (Renderer r in group)
            {
                if (r == null) continue;
                r.GetPropertyBlock(targetMpb);
                targetMpb.SetColor("_BaseColor", c);
                r.SetPropertyBlock(targetMpb);
            }
        }
    }

    static float SelectionRange(int menuIndex)
    {
        switch (menuIndex)
        {
            case MenuStart: return 3.4f;   // 地図と瓶をまとめて照らす
            case MenuOptions: return 1.6f;
            case MenuTransfer: return 1.1f; // 手紙(ランタンへ漏らさない)
            case MenuRanking: return 2.4f;  // 本棚の中段
            default: return 1.4f;
        }
    }

    static float SelectionIntensity(int menuIndex)
    {
        switch (menuIndex)
        {
            case MenuStart: return 1.9f;
            case MenuOptions: return 0.5f;  // ランタン本体を強める分ひかえめ
            case MenuTransfer: return 2.4f; // 手紙
            case MenuRanking: return 3.0f;  // 本棚は元が暗いので強めに
            default: return 1.6f;
        }
    }

    Vector3 SelectionCenter(int menuIndex)
    {
        switch (menuIndex)
        {
            case MenuStart: return Vector3.Lerp(MapCenter, BottleCenter, 0.40f) + new Vector3(0f, 0.75f, -0.30f);
            case MenuOptions: return LanternCenter + new Vector3(0f, 0.15f, -0.1f);
            case MenuTransfer: return LetterCenter + new Vector3(-0.05f, 0.26f, -0.22f);
            case MenuRanking: return ShelfCenter + new Vector3(0.55f, 0.20f, -0.55f);
            default: return LanternCenter;
        }
    }

    // マントの裾の微小な揺れ + 1P/2P の点灯(1P=左1枚 / 2P=2枚とも)。
    void UpdateCloaks(float dt)
    {
        if (cloak1 != null)
        {
            cloak1.localRotation = cloak1Home * Quaternion.Euler(0f, 0f, Mathf.Sin(time * 0.62f) * 0.55f);
        }
        if (cloak2 != null)
        {
            cloak2.localRotation = cloak2Home * Quaternion.Euler(0f, 0f, Mathf.Sin(time * 0.51f + 1.2f) * 0.5f);
        }
        // 画面上では cloak_02(z=3.22)が左=1P、cloak_01(z=1.82)が右=2P に見える。
        // 第8便から「選択中の側だけが光る」(第7便の 1P で左が常時点灯・2P で両方、は廃止)。
        float step = dt / HighlightFadeDuration;
        cloakWeightP1 = Mathf.MoveTowards(cloakWeightP1, twoPlayer ? 0f : 1f, step);
        cloakWeightP2 = Mathf.MoveTowards(cloakWeightP2, twoPlayer ? 1f : 0f, step);
        if (cloakLight2 != null)
            cloakLight2.intensity = Mathf.Lerp(CloakDimIntensity, CloakLitIntensity, cloakWeightP1) * exposure;
        if (cloakLight1 != null)
            cloakLight1.intensity = Mathf.Lerp(CloakDimIntensity, CloakLitIntensity, cloakWeightP2) * exposure;
        // 2 枚は 1.4m しか離れていないので点光源だけでは互いに漏れる。メニューと同じく
        // 実体の _BaseColor も持ち上げて、どちらが選ばれているか一目で分かるようにする。
        ApplyCloakGlow(cloak2Rend, cloakWeightP1);
        ApplyCloakGlow(cloak1Rend, cloakWeightP2);
    }

    void ApplyCloakGlow(Renderer[] group, float weight)
    {
        if (group == null) return;
        Color c = Color.Lerp(Color.white, new Color(1.55f, 1.62f, 1.85f, 1f), weight);
        foreach (Renderer r in group)
        {
            if (r == null) continue;
            r.GetPropertyBlock(targetMpb);
            targetMpb.SetColor("_BaseColor", c);
            r.SetPropertyBlock(targetMpb);
        }
    }

    /// <summary>ライトの強さを現在の設定値から作り直す(検証中に値を触ったら呼ぶ)。</summary>
    public void ApplyExposure()
    {
        if (moonLight != null) moonLight.intensity = moonIntensity * exposure * exitDim;
        if (fillLight != null) fillLight.intensity = fillIntensity * exposure * exitDim;
        if (lanternLight != null) lanternLight.intensity = lanternIntensity * exposure;
        if (ambientSaved) UnityEngine.RenderSettings.ambientLight = ambientColor * exposure;
    }

    /// <summary>検証用: いまのカメラ姿勢を文字列で返す。</summary>
    public string DebugState()
    {
        if (roomCamera == null) return "camera=null";
        return string.Format("active={0} sel={1} zoomTarget={2} zoom={3:F2} pos={4} euler={5} fov={6:F2}",
            activeNow, selection, zoomTarget, zoomProgress,
            roomCamera.transform.position.ToString("F3"),
            roomCamera.transform.eulerAngles.ToString("F2"),
            roomCamera.fieldOfView);
    }
}

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// ステージ選択の背景を Astra 制作の 3D「城壁の街」(Instructions/ステージ選択/cg/v5.fbx) にする。
///
/// 仕組みはタイトルの部屋 (<see cref="TitleRoomController"/>) と同じ「専用カメラ → RenderTexture →
/// 選択画面 Canvas 最背面の RawImage」方式。街はレイヤー <c>CityCG</c>(12) に置き、専用カメラだけが
/// それを写すので、既存のカメラスタック(BackImageCamera / MainCamera / Front / UI)にも StageCG の
/// ステージ背景にも TitleCG の部屋にも触れない。
///
/// - カメラは全景(camera_overview)から始まり、選択中の区画のカメラ(camera_district_NN)へ
///   0.5 秒 ease-in-out で移動する。決定でさらに 0.4 秒寄る。値は v2_camera.json の実値
///   (FBX は輸出時に X 反転ルートを持つので、JSON の座標がそのまま Unity ワールド座標になる。
///   district_03_anchor の実測 (17.892,0.120,-1.459) が JSON と一致することを確認済み)。
/// - 選択中の区画は district_NN_ground を暖色で持ち上げる(0.15 秒フェード)。ステージ未実装の
///   区画は常に暗く沈める。
/// - ▼とラベルは Canvas 側のウィジェットなので、ここでは anchor のワールド座標を投影して返すだけ
///   (<see cref="TryGetMarkerViewport"/>)。カメラが動いても追従する。
///
/// 生成物(カメラ・ライト・街のインスタンス)はすべてランタイムに作るので、シーンは変わらない。
/// </summary>
public class CityMapController : MonoBehaviour
{
    public static CityMapController Instance { get; private set; }

    /// <summary>区画は 1..9。0 は「区画なし」を表す。</summary>
    public const int DistrictCount = 9;

    [Header("素材")]
    public GameObject cityPrefab;
    public RenderTexture targetTexture;
    [Tooltip("UniversalRenderer3D の RendererDataList 上の index。既定 1。")]
    public int rendererIndex = 1;

    [Header("ドット風表示(街だけ低解像度で描く)")]
    [Tooltip("ON で街を pixelWidth x pixelHeight の RenderTexture へ描き、Point(最近傍)で拡大する。タイトルの部屋・プレイ中の CG と同じ 640x360 に揃えてある。")]
    public bool pixelate = true;
    [Tooltip("ドット風の内部解像度(幅)。既定 640(タイトル・プレイ中の CG と統一)。")]
    public int pixelWidth = 640;
    [Tooltip("ドット風の内部解像度(高さ)。既定 360。16:9 を保つこと。")]
    public int pixelHeight = 360;
    [Tooltip("1 チャンネルあたりの階調数。0 で色数の減衰なし。既定 8(タイトルの質感に合わせる)。")]
    public int pixelatePalette = 0;
    [Tooltip("色数を減らしたときの 4x4 順序ディザの強さ。")]
    [Range(0f, 1f)] public float pixelateDither = 0f;

    [Header("色の調整(タイトルの質感に合わせる)")]
    [Tooltip("ON で彩度・コントラストの調整と区画の基調色を掛ける。")]
    public bool colorGrade = false;
    [Tooltip("彩度。1 で素、0.75 で少し落とす。窓灯り・ランタンの橙は別扱いで残る。")]
    [Range(0f, 1.5f)] public float saturation = 0.78f;
    [Tooltip("コントラスト。1 で素、1 未満でハイライトが下がる。")]
    [Range(0.5f, 1.5f)] public float contrast = 0.86f;
    [Tooltip("コントラストの軸。0.5 より下げるとハイライトの方が大きく下がる(夜の暗さが残る)。")]
    [Range(0f, 1f)] public float contrastPivot = 0.30f;
    [Tooltip("黒の持ち上げ。")]
    [Range(0f, 0.4f)] public float blackLift = 0.03f;
    [Tooltip("橙(窓灯り・ランタン)の彩度をどれだけ残すか。1 で完全に残す。")]
    [Range(0f, 1f)] public float warmKeep = 1f;
    [Tooltip("区画の基調色をどれだけ被せるか(寄り切ったときの最大値)。")]
    [Range(0f, 1f)] public float districtTintAmount = 0.35f;
    [Tooltip("区画の基調色のクロスフェード時間(秒)。")]
    public float districtTintFade = 0.5f;

    // ドット風のときだけ使う低解像度の描画先(実行時生成)。
    RenderTexture pixelRT;
    Material pixelMat;
    // 輪郭線用: 街の複製を CityDepth レイヤーに置き、街カメラと同じ姿勢の深度専用カメラで
    // 距離(ワールド単位)を RFloat の RT へ描く(CityDepthWrite.shader)。線は UIPixelQuantize が引く。
    // (URP の _CameraDepthTexture はこの構成の透明パスで読めなかったので自前で描く。)
    Camera depthCamera;
    RenderTexture depthRT;
    Transform depthRoot;
    Material depthMat;
    const string DepthLayerName = "CityDepth";

    [Header("明るさ")]
    [Tooltip("全ライトに掛かる倍率。1.0 だとタイトル部屋より明るく、モデルの単純さが目立つ(2026-09-13 指摘)。0.55 でタイトルと同じ平均輝度(実測 38/255)になる。")]
    public float exposure = 0.55f;
    [Tooltip("街を出しているあいだだけ差し替える環境光(夜の藍)。")]
    public Color ambientColor = new Color(0.150f, 0.160f, 0.240f, 1f);
    [Tooltip("月光(平行光)の強さ。")]
    public float moonIntensity = 1.60f;
    [Tooltip("北の縁取り光。")]
    public float rimIntensity = 0.60f;
    [Tooltip("南からの冷たい起こし光。")]
    public float fillIntensity = 0.34f;
    [Tooltip("街灯・窓明かりの点光源(FBX 内の *_light_* 空オブジェクトの位置に置く)の強さ。")]
    public float lanternIntensity = 2.0f;
    [Tooltip("街灯の届く距離。長くすると隣の区画まで橙が漏れる。")]
    public float lanternRange = 4.5f;
    [Tooltip("街灯だけに掛かる倍率(exposure とは独立)。全体を夜へ落としても街灯の橙の溜まりを残すため。")]
    public float lanternExposure = 1f;
    [Tooltip("ON で空(カメラの背景色)にも exposure を掛ける。")]
    public bool exposureAffectsSky = true;

    [Header("輪郭線(低解像度 RT の明暗の段差に 1 ドット幅の線を重ねる)")]
    [Tooltip("ON で輪郭線を重ねる。既定は深度方式(outlineDepth)のみ。")]
    public bool outline = true;
    [Tooltip("ON で明暗の段差にも線を置く(UIPixelQuantize 側。暗い側の 1 ドット)。夜の街では段差が小さく効果が薄いので既定 OFF。")]
    public bool outlineLuminance = false;
    [Tooltip("線の濃さ。1 で outlineColor そのまま(2026-09-13 の試作は 1.0・黒・外側 1 ドット)。")]
    [Range(0f, 1f)] public float outlineStrength = 1f;
    [Tooltip("線を出す明暗差のしきい値(ガンマ空間の輝度差)。小さいほど細部にも線が入る。")]
    [Range(0f, 1f)] public float outlineThreshold = 0.10f;
    [Tooltip("しきい値からこの幅で線が濃くなる。")]
    [Range(0f, 1f)] public float outlineSoftness = 0.08f;
    public Color outlineColor = new Color(0.03f, 0.03f, 0.06f, 1f);
    [Tooltip("ON で明るい側にも線を置く(2 ドット幅)。OFF は暗い側だけ(1 ドット幅)。")]
    public bool outlineBothSides = false;
    [Tooltip("ON で街の複製を深度専用カメラ(CityDepth レイヤー)で描き、視点からの距離の段差=物体のシルエットに線を置く。明暗の段差だけでは出ない建物の縁が出る。")]
    public bool outlineDepth = true;
    [Tooltip("距離差のしきい値(ワールド単位)。家 1 軒が 2〜3 なので 0.6 前後で建物の縁だけが出る。")]
    public float outlineDepthThreshold = 0.6f;
    [Tooltip("線を置く側。0=物体側(手前の 1 ドット)、1=背景側(物体の外周 1 ドット。地面の上に線が乗るので最も読める)、2=両側(2 ドット幅)。")]
    [Range(0, 2)] public int outlineDepthSide = 1;

    [Header("区画の色")]
    // 第 14 便: 面全体を暖色で持ち上げる発光はやめ、ほとんど分からない程度に弱めた
    // (1.22 → 1.045)。区画の場所は▼と区画の基調色で示す。
    [Tooltip("選択中の区画の地面に乗せる暖色(_BaseColor の倍率。1 で素の色)。面全体は光らせない。")]
    public Color glowTint = new Color(1.045f, 1.02f, 0.975f, 1f);
    [Tooltip("ステージ未実装の区画を沈める色。")]
    public Color dimTint = new Color(0.30f, 0.32f, 0.42f, 1f);

    [System.Serializable]
    public struct DarkenEntry
    {
        [Tooltip("街の中のオブジェクト名(完全一致)。")] public string rendererName;
        [Tooltip("_BaseColor に掛ける倍率(リニア)。0.35 で見た目の明るさが約 6 割になる。")] public float factor;
    }
    [Tooltip("月光で白く飛んで目立つオブジェクトを個別に暗くする(2026-09-13 指摘: 石切り場の岩と轍)。")]
    public DarkenEntry[] darkenRenderers =
    {
        new DarkenEntry { rendererName = "quarry_terraced_rock_cut", factor = 0.30f },
        new DarkenEntry { rendererName = "quarry_timber_crane_chain_stone_stock", factor = 0.45f },
        new DarkenEntry { rendererName = "v5_district_03_surface_relief", factor = 0.12f },
    };

    float DarkenFactor(string rendererName)
    {
        if (darkenRenderers == null) return 1f;
        for (int i = 0; i < darkenRenderers.Length; i++)
            if (darkenRenderers[i].rendererName == rendererName) return Mathf.Max(0f, darkenRenderers[i].factor);
        return 1f;
    }

    // 参考レンダーの背景(藍紫)。実測 (21,24,40)〜(26,30,46)。exposureAffectsSky で露出が掛かる。
    static readonly Color SkyColor = new Color(0.0865f, 0.0965f, 0.1620f, 1f);

    // ---- カメラ(v2_camera.json の実値。すべて正投影) --------------------------
    struct CamPose
    {
        public Vector3 pos;
        public Vector3 euler;
        public float size;
        public Vector3 target;
    }

    static readonly CamPose Overview = new CamPose
    {
        pos = new Vector3(0f, 53.421879f, -73.723686f),
        euler = new Vector3(35f, 0f, 0f),
        size = 21.09375f,
        target = new Vector3(0f, 1.8f, 0f),
    };

    // index 0 は未使用(区画番号 1..9 をそのまま添字に使う)。
    static readonly CamPose[] Districts =
    {
        default,
        new CamPose { pos = new Vector3(10.829823f, 65.940582f, 74.929504f), euler = new Vector3(46f, -165f, 0f), size = 6.1875f, target = new Vector3(-5.351351f, 1.2f, 14.540541f) },
        new CamPose { pos = new Vector3(24.019011f, 65.940582f, 72.875450f), euler = new Vector3(46f, -165f, 0f), size = 6.75f, target = new Vector3(7.837838f, 1.2f, 12.486486f) },
        new CamPose { pos = new Vector3(47.028885f, 59.050884f, -63.943947f), euler = new Vector3(40f, -25f, 0f), size = 6.75f, target = new Vector3(17.891891f, 1.2f, -1.459459f) },
        new CamPose { pos = new Vector3(0.810811f, 59.050884f, -83.268326f), euler = new Vector3(40f, 0f, 0f), size = 8.15625f, target = new Vector3(0.810811f, 1.2f, -14.324325f) },
        new CamPose { pos = new Vector3(-53.444973f, 59.050884f, -65.058609f), euler = new Vector3(40f, 30f, 0f), size = 7.59375f, target = new Vector3(-18.972973f, 1.2f, -5.351351f) },
        new CamPose { pos = new Vector3(-14.324325f, 65.940582f, -55.654388f), euler = new Vector3(46f, 0f, 0f), size = 5.34375f, target = new Vector3(-14.324325f, 1.2f, 6.864865f) },
        new CamPose { pos = new Vector3(15.046038f, 67.240585f, 66.172745f), euler = new Vector3(46f, -165f, 0f), size = 4.5f, target = new Vector3(-1.135135f, 2.5f, 5.783784f) },
        new CamPose { pos = new Vector3(-0.810811f, 65.940582f, -68.735466f), euler = new Vector3(46f, 0f, 0f), size = 5.0625f, target = new Vector3(-0.810811f, 1.2f, -6.216216f) },
        new CamPose { pos = new Vector3(-3.059994f, 62.999130f, -68.346001f), euler = new Vector3(40f, 0f, 0f), size = 8.732357f, target = new Vector3(-3.059994f, 5.148245f, 0.597996f) },
    };

    // 区画 anchor(v2_camera.json)。▼とラベルはこの真上に置く。
    static readonly Vector3[] Anchors =
    {
        Vector3.zero,
        new Vector3(-5.351351f, 0.12f, 14.540541f),
        new Vector3(7.837838f, 0.12f, 12.486486f),
        new Vector3(17.891891f, 0.12f, -1.459459f),
        new Vector3(0.810811f, 0.12f, -14.324325f),
        new Vector3(-18.972973f, 0.12f, -5.351351f),
        new Vector3(-14.324325f, 0.12f, 6.864865f),
        new Vector3(-1.135135f, 0.12f, 5.783784f),
        new Vector3(-0.810811f, 0.12f, -6.216216f),
        new Vector3(-3.621622f, 0.12f, 0.054054f),
    };

    // ▼を置く高さ。カメラの orthographicSize(= 画面の半分の高さの実寸)に対する比で
    // 持つので、全景でも区画へ寄っても▼は画面上の同じくらいの位置に浮く
    // (実寸で持つと、寄った(size 6.75)ときに 6m の▼が画面外まで飛ぶ)。
    // 区画 05(大河・艦長)は▼が街灯の灯りに重なって読めなかったので高く逃がす
    // (第 13 便の指摘 .tmp_select/s18/z_captain_arrow_4x.png)。
    static readonly float[] MarkerHeightFactor = { 0f, 0.30f, 0.30f, 0.30f, 0.26f, 0.52f, 0.26f, 0.28f, 0.26f, 0.55f };

    static readonly string[] DistrictParents =
    {
        null,
        "district_01_market", "district_02_underground", "district_03_quarry",
        "district_04_ruins", "district_05_river", "district_06_catacombs",
        "district_07_treasury", "district_08_forecourt", "district_09_cathedral",
    };

    public const float MoveDuration = 0.5f;
    public const float ZoomDuration = 0.4f;
    const float GlowFadeDuration = 0.15f;
    // 決定で寄るときの倍率(正投影なので size を縮めるのが「寄る」)。
    const float CloseUpScale = 0.58f;

    // ---- 実体 --------------------------------------------------------------
    Camera cityCamera;
    Transform cityRoot;
    Light moonLight;
    Light rimLight;
    Light fillLight;
    readonly System.Collections.Generic.List<Light> lanternLights = new System.Collections.Generic.List<Light>();
    readonly Renderer[] groundRenderers = new Renderer[DistrictCount + 1];
    readonly Renderer[][] districtRenderers = new Renderer[DistrictCount + 1][];
    readonly float[] glowWeight = new float[DistrictCount + 1];
    readonly bool[] available = new bool[DistrictCount + 1];
    MaterialPropertyBlock mpb;
    bool built;
    bool activeNow;

    // 環境光の退避(街を消したら必ず戻す)。
    UnityEngine.Rendering.AmbientMode savedAmbientMode;
    Color savedAmbientLight;
    float savedAmbientIntensity;
    float savedReflectionIntensity;
    bool ambientSaved;

    // カメラ移動。from → to を dur 秒で ease-in-out する。
    CamPose viewFrom;
    CamPose viewTo;
    float moveT = 1f;
    float moveDur = MoveDuration;
    CamPose viewNow;
    int selected;          // 0 = 全景
    float zoomIn;          // 0=区画の引き / 1=決定後の寄り
    float zoomInTarget;
    float time;
    // 区画の基調色。俯瞰(selected=0)では被せず、区画を選ぶと 0.5 秒で乗る。
    Color tintNow = Color.white;
    Color tintTarget = Color.white;
    float tintWeight;
    float tintWeightTarget;

    /// <summary>いま選択中の区画(0=全景)。</summary>
    public int SelectedDistrict => selected;
    /// <summary>カメラ移動が終わっているか(プレビュー動画はこれで出す)。</summary>
    public bool Arrived => moveT >= 1f;
    public float ZoomAmount => zoomIn;
    /// <summary>街の描画先。ドット風のときは低解像度の RT。</summary>
    public RenderTexture Texture => pixelRT != null ? pixelRT : targetTexture;
    /// <summary>色数を落とすときに RawImage へ貼るマテリアル(既定は null)。</summary>
    public Material ViewMaterial => pixelMat;
    public bool Ready => built && cityCamera != null && targetTexture != null;
    public bool CityVisible => Ready && activeNow;

    void OnEnable() { Instance = this; }

    void OnDisable()
    {
        RestoreAmbient();
        if (Instance == this) Instance = null;
    }

    void OnApplicationQuit() { RestoreAmbient(); }

    void OnDestroy() { ReleasePixelTexture(); }

    // ---- ドット風表示(タイトルの部屋と同じ仕組み) --------------------------

    void EnsurePixelTexture()
    {
        if (!pixelate) { ReleasePixelTexture(); return; }
        int w = Mathf.Clamp(pixelWidth, 32, 1920);
        int h = Mathf.Clamp(pixelHeight, 18, 1080);
        if (pixelRT != null && (pixelRT.width != w || pixelRT.height != h)) ReleasePixelTexture();
        if (pixelRT == null)
        {
            pixelRT = new RenderTexture(w, h, 24,
                targetTexture != null ? targetTexture.format : RenderTextureFormat.DefaultHDR)
            {
                name = "CityMapPixelRT",
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
        EnsureDepthCamera(outline && outlineDepth);
        // 一度作ったマテリアルは壊さない。RawImage.material の setter は「破棄済み == null」で
        // 早期 return するため、破棄すると CanvasRenderer が死んだマテリアルを掴んだまま真っ黒になる。
        if (pixelatePalette <= 1 && !colorGrade && !outline)
        {
            if (pixelMat != null) ApplyViewMaterial();
            return;
        }
        if (pixelMat == null)
        {
            Shader sh = Shader.Find("BulletHell/UI/PixelQuantize");
            if (sh == null) return;
            pixelMat = new Material(sh) { hideFlags = HideFlags.DontSave };
        }
        ApplyViewMaterial();
    }

    // 表示板のマテリアルへ、色数・彩度・コントラスト・区画の基調色を流し込む。
    void ApplyViewMaterial()
    {
        if (pixelMat == null) return;
        pixelMat.SetFloat("_PixelatePalette", pixelatePalette);
        pixelMat.SetFloat("_PixelateDither", pixelateDither);
        pixelMat.SetFloat("_GradeEnabled", colorGrade ? 1f : 0f);
        pixelMat.SetFloat("_Saturation", saturation);
        pixelMat.SetFloat("_Contrast", contrast);
        pixelMat.SetFloat("_ContrastPivot", contrastPivot);
        pixelMat.SetFloat("_BlackLift", blackLift);
        pixelMat.SetFloat("_WarmKeep", warmKeep);
        pixelMat.SetColor("_TintColor", tintNow);
        pixelMat.SetFloat("_TintAmount", colorGrade ? tintWeight * districtTintAmount : 0f);
        pixelMat.SetFloat("_OutlineEnabled", outline && (outlineLuminance || (outlineDepth && depthRT != null)) ? 1f : 0f);
        pixelMat.SetFloat("_OutlineLuminance", outline && outlineLuminance ? 1f : 0f);
        pixelMat.SetFloat("_OutlineStrength", outlineStrength);
        pixelMat.SetFloat("_OutlineThreshold", outlineThreshold);
        pixelMat.SetFloat("_OutlineSoftness", outlineSoftness);
        pixelMat.SetColor("_OutlineColor", outlineColor);
        pixelMat.SetFloat("_OutlineBothSides", outlineBothSides ? 1f : 0f);
        pixelMat.SetFloat("_DepthOutline", outline && outlineDepth && depthRT != null ? 1f : 0f);
        pixelMat.SetFloat("_DepthThreshold", outlineDepthThreshold);
        pixelMat.SetFloat("_DepthSide", outlineDepthSide);
        pixelMat.SetTexture("_DepthTex", depthRT);
    }

    /// <summary>輪郭線の設定をまとめて変える。検証用。</summary>
    public void SetOutline(bool on, float strength = -1f, float threshold = -1f, float softness = -1f, int bothSides = -1, Color? color = null, int depth = -1, float depthThreshold = -1f, int luminance = -1, int depthSide = -1)
    {
        outline = on;
        if (depthSide >= 0) outlineDepthSide = Mathf.Clamp(depthSide, 0, 2);
        if (luminance >= 0) outlineLuminance = luminance > 0;
        if (depth >= 0) outlineDepth = depth > 0;
        if (depthThreshold >= 0f) outlineDepthThreshold = depthThreshold;
        if (bothSides >= 0) outlineBothSides = bothSides > 0;
        if (color.HasValue) outlineColor = color.Value;
        if (strength >= 0f) outlineStrength = strength;
        if (threshold >= 0f) outlineThreshold = threshold;
        if (softness >= 0f) outlineSoftness = softness;
        EnsurePixelMaterial();
        ApplyViewMaterial();
    }

    /// <summary>明るさ(全体の露出と街灯だけの倍率)をまとめて変える。検証用。</summary>
    public void SetNight(float exposureValue, float lanternValue = -1f, float moon = -1f)
    {
        exposure = exposureValue;
        if (lanternValue >= 0f) lanternExposure = lanternValue;
        if (moon >= 0f) moonIntensity = moon;
        ApplyExposure();
    }

    // 街の複製を深度専用カメラで描く仕組みを用意/片付けする。
    void EnsureDepthCamera(bool on)
    {
        if (!on)
        {
            if (depthCamera != null) depthCamera.enabled = false;
            if (depthRoot != null) depthRoot.gameObject.SetActive(false);
            return;
        }
        if (cityCamera == null || cityRoot == null) return;
        int depthLayer = LayerMask.NameToLayer(DepthLayerName);
        if (depthLayer < 0)
        {
            Debug.LogWarning("CityMapController: レイヤー " + DepthLayerName + " が無いので輪郭線を出せません。");
            return;
        }
        int w = pixelRT != null ? pixelRT.width : Mathf.Clamp(pixelWidth, 32, 1920);
        int h = pixelRT != null ? pixelRT.height : Mathf.Clamp(pixelHeight, 18, 1080);
        if (depthRT != null && (depthRT.width != w || depthRT.height != h))
        {
            if (depthCamera != null) depthCamera.targetTexture = null;
            depthRT.Release(); DestroyImmediate(depthRT); depthRT = null;
        }
        if (depthRT == null)
        {
            depthRT = new RenderTexture(w, h, 24, RenderTextureFormat.RFloat)
            {
                name = "CityMapDepthRT",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                antiAliasing = 1,
                useMipMap = false,
                hideFlags = HideFlags.DontSave
            };
            depthRT.Create();
        }
        if (depthMat == null)
        {
            Shader sh = Shader.Find("BulletHell/City/DepthWrite");
            if (sh == null) return;
            depthMat = new Material(sh) { hideFlags = HideFlags.DontSave };
        }
        if (depthRoot == null)
        {
            // 街をそのまま複製し、ライトを外して全メッシュを距離書き込みの材質にする。
            GameObject copy = Instantiate(cityRoot.gameObject, cityRoot.parent);
            copy.name = "CityDepthCopy";
            foreach (Light l in copy.GetComponentsInChildren<Light>(true)) DestroyImmediate(l);
            foreach (Renderer r in copy.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = depthMat;
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            }
            SetLayerRecursive(copy, depthLayer);
            depthRoot = copy.transform;
            depthRoot.localPosition = cityRoot.localPosition;
            depthRoot.localRotation = cityRoot.localRotation;
            depthRoot.localScale = cityRoot.localScale;
        }
        depthRoot.gameObject.SetActive(true);
        if (depthCamera == null)
        {
            GameObject camObj = new GameObject("CityDepthCamera");
            camObj.transform.SetParent(cityCamera.transform, false);
            depthCamera = camObj.AddComponent<Camera>();
            depthCamera.clearFlags = CameraClearFlags.SolidColor;
            depthCamera.backgroundColor = new Color(1e5f, 0f, 0f, 1f);   // 何も無い所は「とても遠い」
            depthCamera.cullingMask = 1 << depthLayer;
            depthCamera.orthographic = true;
            depthCamera.allowMSAA = false;
            depthCamera.allowHDR = true;
            depthCamera.useOcclusionCulling = false;
            depthCamera.depth = cityCamera.depth - 1f;   // 街より先に描く
            depthCamera.targetTexture = depthRT;
            UniversalAdditionalCameraData data = camObj.AddComponent<UniversalAdditionalCameraData>();
            data.renderType = CameraRenderType.Base;
            data.renderPostProcessing = false;
            data.requiresColorOption = CameraOverrideOption.Off;
            data.requiresDepthOption = CameraOverrideOption.Off;
            data.SetRenderer(rendererIndex);
        }
        depthCamera.targetTexture = depthRT;
        depthCamera.enabled = true;
        SyncDepthCamera();
    }

    // 深度カメラを街カメラと同じ姿勢・画角にする(ズームで orthographicSize が変わるたびに呼ぶ)。
    void SyncDepthCamera()
    {
        if (depthCamera == null || cityCamera == null) return;
        depthCamera.orthographicSize = cityCamera.orthographicSize;
        depthCamera.nearClipPlane = cityCamera.nearClipPlane;
        depthCamera.farClipPlane = cityCamera.farClipPlane;
        depthCamera.aspect = cityCamera.aspect;
    }

    void ReleasePixelTexture()
    {
        if (pixelRT != null)
        {
            if (cityCamera != null && cityCamera.targetTexture == pixelRT)
                cityCamera.targetTexture = targetTexture;
            pixelRT.Release();
            DestroyImmediate(pixelRT);
            pixelRT = null;
        }
        if (pixelMat != null) { DestroyImmediate(pixelMat); pixelMat = null; }
        if (depthCamera != null) { depthCamera.targetTexture = null; DestroyImmediate(depthCamera.gameObject); depthCamera = null; }
        if (depthRoot != null) { DestroyImmediate(depthRoot.gameObject); depthRoot = null; }
        if (depthMat != null) { DestroyImmediate(depthMat); depthMat = null; }
        if (depthRT != null) { depthRT.Release(); DestroyImmediate(depthRT); depthRT = null; }
    }

    /// <summary>ドット風表示を切り替える(比較用)。表示板は Texture / ViewMaterial を追従する。</summary>
    public void SetPixelate(bool on, int width, int height, int palette = -1, float dither = -1f)
    {
        pixelate = on;
        if (width > 0) pixelWidth = width;
        if (height > 0) pixelHeight = height;
        if (palette >= 0) pixelatePalette = palette;
        if (dither >= 0f) pixelateDither = dither;
        if (!built) return;
        EnsurePixelTexture();
        if (cityCamera != null)
        {
            cityCamera.targetTexture = Texture;
            cityCamera.allowMSAA = pixelRT == null;
        }
    }

    /// <summary>色の調整(彩度・コントラスト・黒の持ち上げ・基調色の強さ)をまとめて変える。検証用。</summary>
    public void SetColorGrade(bool on, float sat, float con, float lift, float tintAmount, float pivot = -1f)
    {
        colorGrade = on;
        if (pivot >= 0f) contrastPivot = pivot;
        if (sat >= 0f) saturation = sat;
        if (con >= 0f) contrast = con;
        if (lift >= 0f) blackLift = lift;
        if (tintAmount >= 0f) districtTintAmount = tintAmount;
        EnsurePixelMaterial();
        ApplyViewMaterial();
    }

    void Awake()
    {
        Instance = this;
        mpb = new MaterialPropertyBlock();
        viewNow = Overview;
        viewFrom = Overview;
        viewTo = Overview;
    }

    void RestoreAmbient()
    {
        if (!ambientSaved) return;
        RenderSettings.ambientMode = savedAmbientMode;
        RenderSettings.ambientLight = savedAmbientLight;
        RenderSettings.ambientIntensity = savedAmbientIntensity;
        RenderSettings.reflectionIntensity = savedReflectionIntensity;
        ambientSaved = false;
    }

    // ---- 構築 --------------------------------------------------------------

    void Build()
    {
        if (built) return;
        if (cityPrefab == null || targetTexture == null)
        {
            Debug.LogWarning("CityMapController: cityPrefab / targetTexture が未割り当てです。");
            return;
        }
        built = true;
        // Awake が走らない文脈(エディタでの検証など)でも使えるように初期化を保証する。
        if (mpb == null) mpb = new MaterialPropertyBlock();

        int layer = LayerMask.NameToLayer("CityCG");
        if (layer < 0) layer = 0;

        // プレハブルートの transform(Blender の右手系相殺の名残 rot(0,180,180)/scale(-1,-1,-1))は
        // 絶対に上書きしない。これが入った状態の座標が v2_camera.json の値と一致する。
        GameObject city = Instantiate(cityPrefab, transform);
        city.name = "WalledCity";
        city.transform.localPosition = Vector3.zero;
        cityRoot = city.transform;
        SetLayerRecursive(city, layer);

        for (int d = 1; d <= DistrictCount; d++)
        {
            Transform parent = FindDeep(cityRoot, DistrictParents[d]);
            if (parent == null) continue;
            districtRenderers[d] = parent.GetComponentsInChildren<Renderer>(true);
            Transform ground = FindDeep(parent, string.Format("district_{0:00}_ground", d));
            if (ground != null) groundRenderers[d] = ground.GetComponent<Renderer>();
        }

        // ---- カメラ ----
        GameObject camObj = new GameObject("CityMapCamera");
        camObj.transform.SetParent(transform, false);
        cityCamera = camObj.AddComponent<Camera>();
        cityCamera.clearFlags = CameraClearFlags.SolidColor;
        // 参考レンダーの背景(藍紫)。実測 (21,24,40)〜(26,30,46)。
        cityCamera.backgroundColor = SkyColor;
        cityCamera.cullingMask = 1 << layer;
        cityCamera.orthographic = true;
        cityCamera.nearClipPlane = 0.05f;
        cityCamera.farClipPlane = 300f;
        cityCamera.depth = -101f;
        EnsurePixelTexture();
        cityCamera.targetTexture = Texture;
        cityCamera.useOcclusionCulling = false;
        // ドット風のときは MSAA を切る(1 ドットの縁がぼけると最近傍拡大の意味が薄れる)。
        cityCamera.allowMSAA = pixelRT == null;
        UniversalAdditionalCameraData data = camObj.AddComponent<UniversalAdditionalCameraData>();
        data.renderType = CameraRenderType.Base;
        data.renderPostProcessing = false;
        data.requiresColorOption = CameraOverrideOption.Off;
        data.requiresDepthOption = CameraOverrideOption.Off;
        data.SetRenderer(rendererIndex);
        EnsurePixelMaterial();

        // ---- ライト ----
        // v2_camera.json の光源色は線形値。Light.color は sRGB として解釈されるので変換して入れる
        // (取り違えると街全体が青紫に沈む。タイトル部屋で実証済みの罠)。
        // 面光源 3 灯は Unity では平行光で近似し、向きは FBX に入っている同名の空オブジェクトから取る。
        moonLight = CreateLight("CityMoon", LinearToSrgb(new Color(0.57f, 0.63f, 0.85f)), moonIntensity, layer);
        AimFromEmpty(moonLight, "moon_directional", new Vector3(0.30f, -0.86f, 0.41f));
        // 影は落とさない。街全体(100 メッシュ)にかかる平行光の影は実測で
        // 平均 55fps → 116fps(最小 35.9 → 65.1)の差になり、俯瞰では見た目の差が
        // ほとんど無かった(.tmp_select/f2/cmp_shadow.png で実フレーム比較)。
        moonLight.shadows = LightShadows.None;

        rimLight = CreateLight("CityRim", LinearToSrgb(new Color(0.46f, 0.53f, 0.78f)), rimIntensity, layer);
        AimFromEmpty(rimLight, "moon_north_rim", new Vector3(-0.32f, -0.62f, -0.72f));

        fillLight = CreateLight("CityFill", LinearToSrgb(new Color(0.43f, 0.48f, 0.68f)), fillIntensity, layer);
        AimFromEmpty(fillLight, "cool_south_fill", new Vector3(0f, -0.62f, 0.78f));

        BuildLanternLights(layer);

        ApplyView(Overview);
        ApplyExposure();
    }

    // 街灯・門灯の点光源。FBX には Blender の POINT ライトと同じ位置に空オブジェクト
    // (*_light_NN)が入っているので、そこへ Light を足すだけで参考レンダーと同じ位置になる。
    // 街の子として作るので、街を SetActive(false) すればまとめて消える。
    void BuildLanternLights(int layer)
    {
        lanternLights.Clear();
        Color warm = LinearToSrgb(new Color(1f, 0.42f, 0.15f));
        foreach (Transform t in cityRoot.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name;
            if (n.IndexOf("_light", System.StringComparison.Ordinal) < 0) continue;
            if (n.StartsWith("moon") || n.StartsWith("cool")) continue;
            if (t.GetComponent<Renderer>() != null) continue;   // 灯具のメッシュ自体は除く
            Light light = t.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = warm;
            light.intensity = lanternIntensity * lanternExposure;
            light.range = lanternRange;
            light.shadows = LightShadows.None;
            light.renderingLayerMask = 1;
            t.gameObject.layer = layer;
            lanternLights.Add(light);
        }
    }

    // FBX に含まれる空オブジェクト(moon_directional など)の向きをそのまま使う。
    // X 反転ルートを通った後の向きなので、街の見え方と必ず一致する。
    void AimFromEmpty(Light light, string emptyName, Vector3 fallbackDir)
    {
        Transform t = cityRoot != null ? FindDeep(cityRoot, emptyName) : null;
        if (t != null) light.transform.rotation = Quaternion.LookRotation(t.forward, Vector3.up);
        else light.transform.rotation = Quaternion.LookRotation(fallbackDir.normalized, Vector3.up);
    }

    static Color LinearToSrgb(Color c)
    {
        return new Color(Mathf.LinearToGammaSpace(c.r), Mathf.LinearToGammaSpace(c.g), Mathf.LinearToGammaSpace(c.b), 1f);
    }

    Light CreateLight(string objectName, Color color, float intensity, int layer)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(transform, false);
        go.layer = layer;
        Light light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = color;
        light.intensity = intensity;
        light.shadows = LightShadows.None;
        light.renderingLayerMask = 1;
        return light;
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

    // ---- 制御 --------------------------------------------------------------

    /// <summary>ステージ選択を出しているあいだだけ街を動かす。</summary>
    public void SetCityActive(bool on)
    {
        if (on) Build();
        if (!built) return;
        activeNow = on;
        if (cityRoot != null) cityRoot.gameObject.SetActive(on);
        if (depthRoot != null) depthRoot.gameObject.SetActive(on && outline && outlineDepth);
        if (cityCamera != null) cityCamera.gameObject.SetActive(on);   // 深度カメラは街カメラの子なので一緒に消える
        foreach (Light light in new[] { moonLight, rimLight, fillLight })
            if (light != null) light.gameObject.SetActive(on);

        if (on)
        {
            if (!ambientSaved)
            {
                savedAmbientMode = RenderSettings.ambientMode;
                savedAmbientLight = RenderSettings.ambientLight;
                savedAmbientIntensity = RenderSettings.ambientIntensity;
                savedReflectionIntensity = RenderSettings.reflectionIntensity;
                ambientSaved = true;
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.ambientLight = ambientColor * exposure;
            // 既定の skybox 反射(グレーのキューブ)が鏡面環境光として全面に乗ると
            // 夜の街が一様に明るくなる(タイトル部屋で実証)。出しているあいだは切る。
            RenderSettings.reflectionIntensity = 0f;
            ApplyExposure();
            ApplyAllTint(true);
        }
        else
        {
            RestoreAmbient();
        }
    }

    /// <summary>ステージが割り当てられている(選択できる)区画を登録する。</summary>
    public void SetAvailableDistricts(bool[] flags)
    {
        for (int d = 0; d <= DistrictCount; d++) available[d] = flags != null && d < flags.Length && flags[d];
        if (built) ApplyAllTint(true);
    }

    /// <summary>選択中の区画へカメラを移す。0 で全景へ戻る。</summary>
    public void SelectDistrict(int district, bool animate)
    {
        district = district < 1 || district > DistrictCount ? 0 : district;
        if (district == selected && animate) return;
        selected = district;
        zoomInTarget = 0f;
        tintTarget = selected >= 1 ? StageCityProfile.TintOf(selected) : Color.white;
        tintWeightTarget = selected >= 1 ? 1f : 0f;
        if (!animate)
        {
            tintNow = tintTarget;
            tintWeight = tintWeightTarget;
            ApplyViewMaterial();
        }
        BeginMove(TargetPose(), animate ? MoveDuration : 0f);
    }

    /// <summary>決定でさらに寄る / 戻す。</summary>
    public void SetCloseUp(bool on)
    {
        zoomInTarget = on ? 1f : 0f;
        BeginMove(TargetPose(), ZoomDuration);
    }

    void BeginMove(CamPose to, float dur)
    {
        viewFrom = viewNow;
        viewTo = to;
        moveDur = Mathf.Max(0.0001f, dur);
        moveT = dur <= 0f ? 1f : 0f;
        if (moveT >= 1f)
        {
            viewNow = to;
            ApplyView(viewNow);
        }
    }

    // 街の CG は全画面のまま、区画の中心だけを画面の左 27% へ寄せる。正投影なので
    // カメラをそのまま右へ平行移動すれば、写っているものが左へずれる
    // (右半分に既存の JSAB カードを置くため。俯瞰は中央のまま)。
    public const float DistrictScreenX = 0.27f;

    float HorizontalShiftFor(float size)
    {
        float aspect = 16f / 9f;
        if (cityCamera != null && cityCamera.aspect > 0.01f) aspect = cityCamera.aspect;
        return (0.5f - DistrictScreenX) * 2f * size * aspect;
    }

    CamPose OffsetToLeftHalf(CamPose p)
    {
        Vector3 right = Quaternion.Euler(p.euler) * Vector3.right;
        Vector3 shift = right * HorizontalShiftFor(p.size);
        p.pos += shift;
        return p;
    }

    CamPose TargetPose()
    {
        if (selected < 1) return Overview;
        CamPose p = Districts[selected];
        if (zoomInTarget <= 0f) return OffsetToLeftHalf(p);
        // 正投影なので「寄る」= size を縮める。区画の anchor が画面中心に来るよう
        // 視線方向を保ったままカメラを平行移動する。
        Quaternion rot = Quaternion.Euler(p.euler);
        Vector3 dir = rot * Vector3.forward;
        Vector3 anchor = Anchors[selected];
        float dist = Vector3.Distance(p.pos, p.target);
        return OffsetToLeftHalf(new CamPose
        {
            pos = anchor + new Vector3(0f, 1.2f, 0f) - dir * dist,
            euler = p.euler,
            size = p.size * CloseUpScale,
            target = anchor,
        });
    }

    public void Tick(float dt)
    {
        if (!built || !activeNow) return;
        time += dt;

        if (moveT < 1f)
        {
            moveT = Mathf.Min(1f, moveT + dt / moveDur);
            float e = EaseInOut(moveT);
            viewNow = new CamPose
            {
                pos = Vector3.Lerp(viewFrom.pos, viewTo.pos, e),
                euler = LerpEuler(viewFrom.euler, viewTo.euler, e),
                size = Mathf.Lerp(viewFrom.size, viewTo.size, e),
                target = Vector3.Lerp(viewFrom.target, viewTo.target, e),
            };
            ApplyView(viewNow);
        }
        zoomIn = Mathf.MoveTowards(zoomIn, zoomInTarget, dt / ZoomDuration);

        // 区画の基調色は 0.5 秒でクロスフェード(俯瞰へ戻ると被せが 0 になる)。
        float tintStep = dt / Mathf.Max(0.01f, districtTintFade);
        bool tintChanged = false;
        if (tintWeight != tintWeightTarget)
        {
            tintWeight = Mathf.MoveTowards(tintWeight, tintWeightTarget, tintStep);
            tintChanged = true;
        }
        if (tintNow != tintTarget)
        {
            tintNow = new Color(
                Mathf.MoveTowards(tintNow.r, tintTarget.r, tintStep),
                Mathf.MoveTowards(tintNow.g, tintTarget.g, tintStep),
                Mathf.MoveTowards(tintNow.b, tintTarget.b, tintStep), 1f);
            tintChanged = true;
        }
        if (tintChanged) ApplyViewMaterial();

        // 区画の発光(選択中だけ暖色で持ち上げる)。
        float step = dt / GlowFadeDuration;
        bool changed = false;
        for (int d = 1; d <= DistrictCount; d++)
        {
            float want = d == selected && available[d] ? 1f : 0f;
            float next = Mathf.MoveTowards(glowWeight[d], want, step);
            if (!Mathf.Approximately(next, glowWeight[d])) changed = true;
            glowWeight[d] = next;
        }
        if (changed || Mathf.Abs(Mathf.Sin(time * 2f)) < 1f) ApplyGlow();
    }

    static float EaseInOut(float t)
    {
        return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;
    }

    // 方位角は -180/180 を跨ぐので最短経路で補間する。
    static Vector3 LerpEuler(Vector3 a, Vector3 b, float t)
    {
        return new Vector3(
            Mathf.Lerp(a.x, b.x, t),
            Mathf.LerpAngle(a.y, b.y, t),
            Mathf.Lerp(a.z, b.z, t));
    }

    void ApplyView(CamPose p)
    {
        if (cityCamera == null) return;
        cityCamera.transform.SetPositionAndRotation(p.pos, Quaternion.Euler(p.euler));
        cityCamera.orthographicSize = p.size;
        SyncDepthCamera();
    }

    // 未実装の区画を沈め、実装済みの区画を素の明るさにする。
    void ApplyAllTint(bool immediate)
    {
        for (int d = 1; d <= DistrictCount; d++)
        {
            Renderer[] group = districtRenderers[d];
            if (group == null) continue;
            bool dim = !available[d];
            Color baseTint = dim ? dimTint : Color.white;
            // 窓の灯り(warm 材質のみ _EMISSION が有効)も一緒に落とす。
            Color emis = dim ? WarmEmission * 0.16f : WarmEmission;
            foreach (Renderer r in group)
            {
                if (r == null) continue;
                r.GetPropertyBlock(mpb);
                float k = DarkenFactor(r.name);
                mpb.SetColor(BaseColorId, k < 1f ? new Color(baseTint.r * k, baseTint.g * k, baseTint.b * k, baseTint.a) : baseTint);
                mpb.SetColor(EmissionId, emis);
                r.SetPropertyBlock(mpb);
            }
            if (immediate) glowWeight[d] = d == selected && available[d] ? 1f : 0f;
        }
        ApplyGlow();
    }

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    // FBX から入った warm 材質の発光値(HDR 強度 3)。MPB で上書きするときの基準。
    static readonly Color WarmEmission = new Color(2.492f, 0.862f, 0.159f, 1f);

    void ApplyGlow()
    {
        float pulse = 1f + 0.025f * Mathf.Sin(time * 2.0f);
        for (int d = 1; d <= DistrictCount; d++)
        {
            Renderer ground = groundRenderers[d];
            if (ground == null) continue;
            float w = glowWeight[d] * pulse;
            Color c = Color.Lerp(available[d] ? Color.white : dimTint, glowTint, Mathf.Clamp01(w));
            ground.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, c);
            ground.SetPropertyBlock(mpb);
        }
    }

    /// <summary>区画 <paramref name="district"/> の▼を置く点を、街カメラのビューポート座標へ投影する。</summary>
    public bool TryGetMarkerViewport(int district, out Vector2 viewport)
    {
        viewport = Vector2.zero;
        if (cityCamera == null || district < 1 || district > DistrictCount) return false;
        float lift = cityCamera.orthographicSize * MarkerHeightFactor[district];
        Vector3 world = Anchors[district] + new Vector3(0f, lift, 0f);
        Vector3 vp = cityCamera.WorldToViewportPoint(world);
        viewport = new Vector2(vp.x, vp.y);
        return vp.x > -0.25f && vp.x < 1.25f && vp.y > -0.25f && vp.y < 1.25f;
    }

    public void ApplyExposure()
    {
        if (moonLight != null) moonLight.intensity = moonIntensity * exposure;
        if (rimLight != null) rimLight.intensity = rimIntensity * exposure;
        if (fillLight != null) fillLight.intensity = fillIntensity * exposure;
        for (int i = 0; i < lanternLights.Count; i++)
        {
            if (lanternLights[i] == null) continue;
            lanternLights[i].intensity = lanternIntensity * lanternExposure;
            lanternLights[i].range = lanternRange;
        }
        if (ambientSaved) RenderSettings.ambientLight = ambientColor * exposure;
        if (cityCamera != null) cityCamera.backgroundColor = exposureAffectsSky ? SkyColor * exposure : SkyColor;
    }

    /// <summary>検証用: いまのカメラ姿勢を文字列で返す。</summary>
    public string DebugState()
    {
        if (cityCamera == null) return "camera=null";
        return string.Format("active={0} sel={1} moveT={2:F2} zoom={3:F2} pos={4} euler={5} size={6:F2}",
            activeNow, selected, moveT, zoomIn,
            cityCamera.transform.position.ToString("F3"),
            cityCamera.transform.eulerAngles.ToString("F2"),
            cityCamera.orthographicSize);
    }
}

using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// ステージ選択の背景を Astra 制作の 3D「城壁の街」(Instructions/ステージ選択/cg/v2.fbx) にする。
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

    [Header("明るさ")]
    [Tooltip("全ライトに掛かる倍率。")]
    public float exposure = 1f;
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

    [Header("区画の色")]
    [Tooltip("選択中の区画の地面に乗せる暖色(_BaseColor の倍率。1 で素の色)。")]
    public Color glowTint = new Color(1.22f, 1.10f, 0.94f, 1f);
    [Tooltip("ステージ未実装の区画を沈める色。")]
    public Color dimTint = new Color(0.30f, 0.32f, 0.42f, 1f);

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
    static readonly float[] MarkerHeightFactor = { 0f, 0.30f, 0.30f, 0.30f, 0.26f, 0.34f, 0.26f, 0.28f, 0.26f, 0.55f };

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

    /// <summary>いま選択中の区画(0=全景)。</summary>
    public int SelectedDistrict => selected;
    /// <summary>カメラ移動が終わっているか(プレビュー動画はこれで出す)。</summary>
    public bool Arrived => moveT >= 1f;
    public float ZoomAmount => zoomIn;
    public RenderTexture Texture => targetTexture;
    public bool Ready => built && cityCamera != null && targetTexture != null;
    public bool CityVisible => Ready && activeNow;

    void OnEnable() { Instance = this; }

    void OnDisable()
    {
        RestoreAmbient();
        if (Instance == this) Instance = null;
    }

    void OnApplicationQuit() { RestoreAmbient(); }

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
        cityCamera.backgroundColor = new Color(0.0865f, 0.0965f, 0.1620f, 1f);
        cityCamera.cullingMask = 1 << layer;
        cityCamera.orthographic = true;
        cityCamera.nearClipPlane = 0.05f;
        cityCamera.farClipPlane = 300f;
        cityCamera.depth = -101f;
        cityCamera.targetTexture = targetTexture;
        cityCamera.useOcclusionCulling = false;
        cityCamera.allowMSAA = true;
        UniversalAdditionalCameraData data = camObj.AddComponent<UniversalAdditionalCameraData>();
        data.renderType = CameraRenderType.Base;
        data.renderPostProcessing = false;
        data.requiresColorOption = CameraOverrideOption.Off;
        data.requiresDepthOption = CameraOverrideOption.Off;
        data.SetRenderer(rendererIndex);

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
            light.intensity = lanternIntensity * exposure;
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
        if (cityCamera != null) cityCamera.gameObject.SetActive(on);
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

    CamPose TargetPose()
    {
        if (selected < 1) return Overview;
        CamPose p = Districts[selected];
        if (zoomInTarget <= 0f) return p;
        // 正投影なので「寄る」= size を縮める。区画の anchor が画面中心に来るよう
        // 視線方向を保ったままカメラを平行移動する。
        Quaternion rot = Quaternion.Euler(p.euler);
        Vector3 dir = rot * Vector3.forward;
        Vector3 anchor = Anchors[selected];
        float dist = Vector3.Distance(p.pos, p.target);
        return new CamPose
        {
            pos = anchor + new Vector3(0f, 1.2f, 0f) - dir * dist,
            euler = p.euler,
            size = p.size * CloseUpScale,
            target = anchor,
        };
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
                mpb.SetColor(BaseColorId, baseTint);
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
        float pulse = 1f + 0.06f * Mathf.Sin(time * 2.0f);
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
            lanternLights[i].intensity = lanternIntensity * exposure;
            lanternLights[i].range = lanternRange;
        }
        if (ambientSaved) RenderSettings.ambientLight = ambientColor * exposure;
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

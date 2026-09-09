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
///   引き継ぎ=羽根ペン / ランキング=手紙 / 1P・2P=マント)。選択中のオブジェクトは
///   暖色の弱いポイントライト(リム発光の代用)で持ち上げ、ランタン側もわずかに強まる。
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
    [Tooltip("班員が描いた主人公の立ち絵(背景透過済み)。タイトルの右手前に 2D の板として出す。")]
    public Sprite heroSprite;

    [Header("明るさ")]
    [Tooltip("全ライトに掛かる倍率。v3 の参考レンダーより +15% 明るくする指示のため既定 1.15。")]
    public float exposure = 1.15f;

    // ---- 部屋の座標(v3_camera.json の実値) ----------------------------------
    // 全景。lens 40mm / sensor 36x20.25 → 垂直画角 28.409°。
    static readonly Vector3 TitlePos = new Vector3(0f, 6.4f, -14.8f);
    static readonly Vector3 TitleEuler = new Vector3(15.25512f, 0f, 0f);
    const float TitleVFov = 28.409272f;
    // 机(地図)。
    static readonly Vector3 DeskPos = new Vector3(-0.95f, 5.7f, -4.65f);
    static readonly Vector3 DeskEuler = new Vector3(39.731927f, -1.1503626f, 0f);
    const float DeskVFov = 28.409272f;
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

    /// <summary>メニュー index(0=スタート/1=設定/2=引き継ぎ/3=ランキング)。</summary>
    public const int MenuStart = 0;
    public const int MenuOptions = 1;
    public const int MenuTransfer = 2;
    public const int MenuRanking = 3;

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
    Quaternion cloak1Home;
    Quaternion cloak2Home;
    Renderer cityLights;
    MaterialPropertyBlock cityMpb;
    ParticleSystem dust;
    bool built;

    // 状態
    bool activeNow;
    int selection = MenuStart;
    int zoomTarget = -1;      // -1 = 全景
    float zoomProgress;       // 0=全景 1=寄り
    const float ZoomDuration = 0.4f;
    bool twoPlayer;
    float time;
    float flicker = 1f;
    float flickerVel;

    /// <summary>寄りの進み具合(0=全景 / 1=寄りきり)。立ち絵の視差・退避に使う。</summary>
    public float ZoomAmount => zoomProgress;
    /// <summary>部屋の描画先。タイトルの RawImage が貼る。</summary>
    public RenderTexture Texture => targetTexture;
    public bool Ready => built && roomCamera != null && targetTexture != null;

    void OnEnable() { Instance = this; }
    void OnDisable() { if (Instance == this) Instance = null; }

    void Awake()
    {
        Instance = this;
        focusViews = new[]
        {
            Explicit(DeskPos, DeskEuler, DeskVFov),   // スタート: 地図(camera_desk)
            Dolly(LanternCenter, 4.6f),               // 設定: ランタン
            Dolly(QuillCenter, 3.5f),                 // 引き継ぎ: 羽根ペン
            Dolly(LetterCenter, 3.95f),               // ランキング: 手紙
        };
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

        // 部屋本体。プレハブルートの transform(Blender の右手系相殺の名残で回転・スケールが
        // 入っていることがある)は絶対に上書きしない。位置だけ原点へ置く。
        GameObject room = Instantiate(roomPrefab, transform);
        room.name = "TitleRoom";
        room.transform.localPosition = Vector3.zero;
        roomRoot = room.transform;
        SetLayerRecursive(room, layer);

        lanternTf = FindDeep(roomRoot, "obj_lantern");
        cloak1 = FindDeep(roomRoot, "cloak_01");
        cloak2 = FindDeep(roomRoot, "cloak_02");
        if (cloak1 != null) cloak1Home = cloak1.localRotation;
        if (cloak2 != null) cloak2Home = cloak2.localRotation;
        Transform cityTf = FindDeep(roomRoot, "city_lights");
        if (cityTf != null) cityLights = cityTf.GetComponent<Renderer>();
        cityMpb = new MaterialPropertyBlock();

        // ---- カメラ ----
        GameObject camObj = new GameObject("TitleRoomCamera");
        camObj.transform.SetParent(transform, false);
        roomCamera = camObj.AddComponent<Camera>();
        roomCamera.clearFlags = CameraClearFlags.SolidColor;
        roomCamera.backgroundColor = new Color(0.008f, 0.010f, 0.020f, 1f);
        roomCamera.cullingMask = 1 << layer;
        roomCamera.nearClipPlane = 0.05f;
        roomCamera.farClipPlane = 150f;
        roomCamera.depth = -100f;
        roomCamera.targetTexture = targetTexture;
        roomCamera.useOcclusionCulling = false;
        roomCamera.allowMSAA = true;
        UniversalAdditionalCameraData data = camObj.AddComponent<UniversalAdditionalCameraData>();
        data.renderType = CameraRenderType.Base;
        data.renderPostProcessing = false;
        data.requiresColorOption = CameraOverrideOption.Off;
        data.requiresDepthOption = CameraOverrideOption.Off;
        data.SetRenderer(rendererIndex);
        ApplyPose(TitlePos, Quaternion.Euler(TitleEuler), TitleVFov);

        // ---- ライト ----
        // v3_notes の光源表を Unity の実ライトへ写した(エネルギー値は Blender のワット数
        // をそのまま使えないので、参考レンダーと見比べて決めた実測値)。
        moonLight = CreateLight("MoonKey", LightType.Directional, Vector3.zero,
            new Color(0.57f, 0.65f, 0.86f), 0.62f, layer);
        moonLight.transform.rotation = Quaternion.LookRotation(new Vector3(0.34f, -0.52f, -0.78f));
        moonLight.shadows = LightShadows.Soft;
        moonLight.shadowStrength = 0.62f;

        fillLight = CreateLight("RoomFill", LightType.Directional, Vector3.zero,
            new Color(0.57f, 0.65f, 0.86f), 0.16f, layer);
        fillLight.transform.rotation = Quaternion.LookRotation(new Vector3(-0.12f, -0.72f, 0.68f));
        fillLight.shadows = LightShadows.None;

        lanternLight = CreateLight("LanternLight", LightType.Point,
            new Vector3(0.4f, 2.04f, 1.0f), new Color(1f, 0.46f, 0.16f), 6.4f, layer);
        lanternLight.range = 9f;
        lanternLight.shadows = LightShadows.None;

        selectionLight = CreateLight("SelectionRim", LightType.Point,
            LanternCenter, new Color(1f, 0.72f, 0.42f), 0f, layer);
        selectionLight.range = 2.6f;
        selectionLight.shadows = LightShadows.None;

        cloakLight1 = CreateLight("CloakLight1", LightType.Point,
            new Vector3(4.85f, 3.15f, 1.82f), new Color(0.72f, 0.84f, 1f), 0f, layer);
        cloakLight1.range = 3.2f;
        cloakLight2 = CreateLight("CloakLight2", LightType.Point,
            new Vector3(4.85f, 3.15f, 3.22f), new Color(0.72f, 0.84f, 1f), 0f, layer);
        cloakLight2.range = 3.2f;

        BuildDust(layer);
        ApplyExposure();
    }

    Light CreateLight(string objectName, LightType type, Vector3 pos, Color color, float intensity, int layer)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = pos;
        go.layer = layer;
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

        ParticleSystem.MainModule main = dust.main;
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

    /// <summary>タイトル表示中だけ部屋を動かす。false でカメラ・ライト・部屋をすべて止める。</summary>
    public void SetRoomActive(bool on)
    {
        if (on) Build();
        if (!built) return;
        activeNow = on;
        if (roomRoot != null) roomRoot.gameObject.SetActive(on);
        if (roomCamera != null) roomCamera.gameObject.SetActive(on);
        foreach (Light light in new[] { moonLight, fillLight, lanternLight, selectionLight, cloakLight1, cloakLight2 })
            if (light != null) light.gameObject.SetActive(on);
        if (dust != null)
        {
            dust.gameObject.SetActive(on);
            if (on) dust.Play();
        }
        if (on)
        {
            zoomTarget = -1;
            zoomProgress = 0f;
            ApplyPose(TitlePos, Quaternion.Euler(TitleEuler), TitleVFov);
        }
    }

    /// <summary>メニュー選択の変化を部屋のハイライトへ反映する。</summary>
    public void SetSelection(int menuIndex) => selection = menuIndex;

    /// <summary>1P/2P の選択。マントの光る枚数で示す。</summary>
    public void SetTwoPlayer(bool two) => twoPlayer = two;

    /// <summary>決定でそのメニューのオブジェクトへ寄る。index &lt; 0 で全景へ戻る。</summary>
    public void FocusMenu(int menuIndex) => zoomTarget = menuIndex;

    /// <summary>全景へ戻す。</summary>
    public void ClearFocus() => zoomTarget = -1;

    public void Tick(float dt)
    {
        if (!built || !activeNow) return;
        time += dt;

        // 寄り/戻りの進み(ease-out cubic は姿勢の補間側で掛ける)。
        float target = zoomTarget >= 0 ? 1f : 0f;
        float step = dt / ZoomDuration;
        zoomProgress = Mathf.MoveTowards(zoomProgress, target, step);

        int viewIndex = zoomTarget >= 0 ? zoomTarget : selection;
        UpdateCamera(viewIndex, zoomProgress);
        UpdateFlicker(dt);
        UpdateHighlight(dt);
        UpdateCloaks();
    }

    void UpdateCamera(int viewIndex, float progress)
    {
        Vector3 pos = TitlePos;
        Quaternion rot = Quaternion.Euler(TitleEuler);
        float fov = TitleVFov;
        if (progress > 0f && focusViews != null && viewIndex >= 0 && viewIndex < focusViews.Length)
        {
            ResolveFocus(focusViews[viewIndex], out Vector3 fpos, out Quaternion frot, out float ffov);
            float q = progress - 1f;
            float ease = 1f + q * q * q; // ease-out cubic
            pos = Vector3.Lerp(TitlePos, fpos, ease);
            rot = Quaternion.Slerp(Quaternion.Euler(TitleEuler), frot, ease);
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
        if (lanternLight != null) lanternLight.intensity = 6.4f * exposure * flicker;

        if (cityLights != null)
        {
            float cityPulse = 1f + 0.10f * Mathf.Sin(time * 0.9f) + 0.06f * Mathf.Sin(time * 2.3f + 2.1f);
            cityLights.GetPropertyBlock(cityMpb);
            cityMpb.SetColor("_EmissionColor", new Color(1f, 0.72f, 0.42f) * cityPulse);
            cityLights.SetPropertyBlock(cityMpb);
        }
    }

    // 選択中のオブジェクトを暖色の弱い点光源で持ち上げる(リム発光の代用)。
    // ランタンが選ばれているときはランタン自身の光もわずかに強まる。
    void UpdateHighlight(float dt)
    {
        Vector3 wanted = SelectionCenter(selection);
        if (selectionLight != null)
        {
            Vector3 p = selectionLight.transform.localPosition;
            selectionLight.transform.localPosition = Vector3.Lerp(p, wanted, 1f - Mathf.Exp(-12f * dt));
            float pulse = 1f + 0.12f * Mathf.Sin(time * 2.2f);
            float baseIntensity = selection == MenuOptions ? 0.55f : 1.35f;
            selectionLight.intensity = baseIntensity * exposure * pulse;
        }
        if (lanternLight != null && selection == MenuOptions)
        {
            lanternLight.intensity *= 1.25f;
        }
    }

    Vector3 SelectionCenter(int menuIndex)
    {
        switch (menuIndex)
        {
            case MenuStart: return Vector3.Lerp(MapCenter, BottleCenter, 0.35f) + new Vector3(0f, 0.55f, -0.35f);
            case MenuOptions: return LanternCenter + new Vector3(0f, 0.15f, -0.1f);
            case MenuTransfer: return QuillCenter + new Vector3(0f, 0.25f, -0.25f);
            case MenuRanking: return LetterCenter + new Vector3(0f, 0.35f, -0.3f);
            default: return LanternCenter;
        }
    }

    // マントの裾の微小な揺れ + 1P/2P の点灯(1P=左1枚 / 2P=2枚とも)。
    void UpdateCloaks()
    {
        if (cloak1 != null)
        {
            cloak1.localRotation = cloak1Home * Quaternion.Euler(0f, 0f, Mathf.Sin(time * 0.62f) * 0.55f);
        }
        if (cloak2 != null)
        {
            cloak2.localRotation = cloak2Home * Quaternion.Euler(0f, 0f, Mathf.Sin(time * 0.51f + 1.2f) * 0.5f);
        }
        if (cloakLight1 != null) cloakLight1.intensity = 0.95f * exposure;
        if (cloakLight2 != null) cloakLight2.intensity = twoPlayer ? 0.95f * exposure : 0.06f * exposure;
    }

    void ApplyExposure()
    {
        if (moonLight != null) moonLight.intensity = 0.62f * exposure;
        if (fillLight != null) fillLight.intensity = 0.16f * exposure;
        if (lanternLight != null) lanternLight.intensity = 6.4f * exposure;
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

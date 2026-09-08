using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 石工ステージの 3D 背景 CG（Astra 制作 v3n）の制御。
///
/// 構図は「奥に CG の舞台 → その手前にボス → 最前面に弾幕（不透明）」。
/// 実現方法:
///   1. CG 本体はレイヤー StageCG に置き、専用の <see cref="cgCamera"/>（Universal 3D Renderer）が
///      RenderTexture へ描く。既存のカメラスタック（BackImageCamera→MainCamera/Front/UI）には触らない。
///   2. その RenderTexture を、MainCamera から見てフィールド 32x18 をちょうど覆う位置に置いた
///      不透明 Quad（<see cref="displayQuad"/>・キュー Geometry）に貼る。不透明なので
///      Transparent の弾・ボススプライトより必ず奥に描かれる。
///   3. 額装（FreezeAspectRate.SetPlayFrame）は MainCamera 側のズームなので板は自動で追従する。
///
/// 有効化はステージ id（stone）と GameState.Playing でゲートする。他ステージ・選択画面・
/// リザルトでは CGCamera と板を非アクティブにするので、従来どおり黒背景のまま。
///
/// カメラの投影は Blender 側と同じ非対称フラスタムを直接与える（v3_notes.md §カメラ・導入）。
/// 導入（時刻は StoneCgIntro が正本）: 0.56〜1.30 秒で黒から空へフェードし、1.06〜4.56 秒で
/// 見上げ姿勢から通常姿勢へ ease in-out（中点 2.81 秒で速度最大）。自機は 4.07〜4.73 秒に
/// 画面下から上がってくる（PlayerController 側）。
///
/// ボスは石工の CG 有効時だけ 2D の SpriteRenderer を止め、CG 空間（z = bossDepth）へ
/// 逆投影した代理スプライトとして CGCamera に描かせる。画面上の位置・大きさは 2D のときと同じ。
/// </summary>
[ExecuteAlways]
public class StoneCgController : MonoBehaviour
{
    [Header("対象ステージ")]
    [Tooltip("この stageDirectoryName（または stageName）のときだけ CG を出す。")]
    public string targetStageDirectory = "stone";
    public string targetStageNameFallback = "石工";

    [Header("参照")]
    public Camera cgCamera;
    public Renderer displayQuad;
    public GameObject cgSceneRoot;

    [Header("表示板の前後関係")]
    // URP 2D Renderer は MeshRenderer も 2D のソート(sortingLayer/sortingOrder)に載せるため、
    // 「不透明キューだから必ず奥」にはならない。実測(.tmp_cg)では
    //   板 0(既定) → 弾より奥だが sortingOrder -10 のボスより手前でボスを隠す
    //   板 -11 / -20 → CG・ボス・弾がすべて意図どおり(奥→手前)
    //   板 -100 以下 → CG が描かれなくなる(BackCamera 側の描画順との兼ね合い)
    // ボスの sortingOrder は stage.json の -10。その 10 段下の -20 を使う。
    public int quadSortingOrder = -20;

    [Header("明るさ")]
    [Tooltip("表示板の露出。実運用では Astra のレンダーよりかなり暗くする（2026-09-07 ユーザー決定 0.35）。")]
    [Range(0f, 2f)] public float exposure = 0.35f;
    [Tooltip("フィールド中央（弾が飛ぶ帯）を落として弾の視認性を上げる量。")]
    [Range(0f, 1f)] public float centerDarken = 0.55f;

    [Header("ボスを CG の 3D 空間へ置く")]
    [Tooltip("ボスの代理スプライトに使うマテリアル（シェーダ StoneCG/BossSprite）。")]
    public Material bossSpriteMaterial;
    [Tooltip("ボスを置く奥行き。岩棚の手前縁と同じ z。")]
    public float bossDepth = 5.5f;
    [Tooltip("ボスの明度。CG の露出・中央減光とは別に掛かる（表示板の _BossBrightness）。露出 0.35 の背景に対して 0.8 では明るすぎて浮くので 0.5 にした（2026-09-08 実測比較）。")]
    [Range(0f, 2f)] public float bossBrightness = 0.5f;

    [Header("ライティング（Blender 側の数値をリニアで再現）")]
    // moon SUN: 位置 (-30,55,-12) → 注視 (16,2,12)、energy 1.65、色 (.64,.59,1)
    public Vector3 sunFrom = new Vector3(-30f, 55f, -12f);
    public Vector3 sunTo = new Vector3(16f, 2f, 12f);
    public Vector3 sunColorLinear = new Vector3(0.64f, 0.59f, 1f);
    [Tooltip("Blender の sun energy 1.65 W/m^2 をランバート応答へ換算した値 (1.65/π)。")]
    public float sunIntensity = 0.5252f;
    // world background (.115,.098,.18) * strength .3
    public Vector3 ambientLinear = new Vector3(0.0345f, 0.0294f, 0.054f);

    [Header("カメラ（通常姿勢 = v3_notes.md）")]
    public Vector3 normalPosition = new Vector3(16f, 20f, -36f);
    public float nearClip = 0.05f;
    public float farClip = 450f;

    [Header("形態変化（ゴーレム降臨）")]
    [Tooltip("ゴーレムが着地する時刻。Tools/danmaku-lab/choreo/stone3.js の BOSS_LAND_TIME。")]
    public float landTime = 72.94f;
    [Tooltip("降下にかかる時間。stone3.js の BOSS_DESCEND_SEC（降下開始 = landTime - この値）。")]
    public float descendSec = 0.833333f;

    [Tooltip("false にすると形態変化の演出（拍連動・揺れ・粉・コアの赤い光・割れ目の発光）を全て切る。")]
    public bool stageFxEnabled = true;

    [Header("拍連動（BPM144・offset 0）")]
    public float beatSec = 60f / 144f;
    [Tooltip("拍頭でランタン・街の灯りを何割増やすか。")]
    [Range(0f, 1f)] public float beatPulse = 0.2f;
    [Tooltip("拍頭の増分が戻るまでの時間。")]
    public float beatDecaySec = 0.15f;

    [Header("降臨の瞬間")]
    [Tooltip("着地時に CG カメラを揺らす振幅（既存 CameraShake の石工着地と同じ 0.6/0.34/18Hz）。")]
    public float shakeAmplitude = 0.6f;
    public float shakeDuration = 0.34f;
    public float shakeFrequency = 18f;
    [Tooltip("着地でランタンを消しておく時間。")]
    public float lanternBlackoutSec = 0.3f;
    [Tooltip("落ちる粉に使うマテリアル（シェーダ StoneCG/Flat）。未設定なら粉を出さない。")]
    public Material dustMaterial;
    public int dustCount = 60;
    public float dustLifeSec = 1.5f;

    [Header("後半（降臨後）の照明")]
    [Tooltip("コアの赤い点光源の位置（CG の core_red_off 空オブジェクトの実測値）。")]
    public Vector3 coreLightPosition = new Vector3(16f, 13.1f, 5.7f);
    [Tooltip("コアの色（リニア）。")]
    public Vector3 coreColorLinear = new Vector3(1f, 0.25f, 0.2f);
    [Tooltip("コアの減衰半径（ユニット）。")]
    public float coreRadius = 10f;
    [Tooltip("コアの強さ。岩棚天面で +12 レベル程度になる値（実測で決めた）。")]
    public float coreIntensity = 1.7f;
    [Tooltip("後半の拍連動はコアの明滅に切り替わる。拍頭での増分。")]
    [Range(0f, 1f)] public float corePulse = 0.25f;
    [Tooltip("後半のランタンの明るさ（前半比）。")]
    [Range(0f, 1f)] public float lateLanternScale = 0.6f;
    [Tooltip("後半の遠景の街の灯り（前半比）。1/3 を消すぶん。")]
    [Range(0f, 1f)] public float lateCityScale = 0.6667f;
    [Tooltip("後半の空の色（前半比）。わずかに赤紫へ寄せる。")]
    public Vector3 lateSkyTint = new Vector3(1.06f, 0.94f, 1.02f);
    [Tooltip("後半の露出。コアの赤い光で中央が明るくならないよう、実測で決めた係数を掛ける。")]
    [Range(0f, 1f)] public float lateExposureScale = 0.98f;

    [Header("カメラ（見上げ姿勢 = v3n_lookup.png の実値）")]
    public Vector3 lookupPosition = new Vector3(16f, 20f, -28f);
    public Vector3 lookupTarget = new Vector3(16f, 58f, 72f);
    [Tooltip("見上げのレンズ mm（sensor 36mm・水平フィット・対称フラスタム）。")]
    public float lookupLensMm = 32f;

    // 導入の時刻表は StoneCgIntro（自機の登場と共有）が正本。
    //   0.56〜1.30 黒 → 空のフェード / 1.06〜4.56 見上げ → 通常（中点 2.81 で速度最大）
    //   4.07〜4.73 自機が下から登場（PlayerController 側）

    // シェーダのグローバル uniform 名
    static readonly int SunDirId = Shader.PropertyToID("_StoneCgSunDir");
    static readonly int SunColorId = Shader.PropertyToID("_StoneCgSunColor");
    static readonly int AmbientId = Shader.PropertyToID("_StoneCgAmbient");
    static readonly int ExposureId = Shader.PropertyToID("_Exposure");
    static readonly int CenterDarkenId = Shader.PropertyToID("_CenterDarken");
    static readonly int BossBrightnessId = Shader.PropertyToID("_BossBrightness");
    static readonly int FadeId = Shader.PropertyToID("_Fade");
    static readonly int CoreParamsId = Shader.PropertyToID("_StoneCgCoreParams");
    static readonly int CoreColorId = Shader.PropertyToID("_StoneCgCoreColor");
    static readonly int EmisGrp1Id = Shader.PropertyToID("_StoneCgEmisGrp1");
    static readonly int EmisGrp2Id = Shader.PropertyToID("_StoneCgEmisGrp2");
    static readonly int EmisGrp3Id = Shader.PropertyToID("_StoneCgEmisGrp3");
    static readonly int EmisGrp4Id = Shader.PropertyToID("_StoneCgEmisGrp4");
    static readonly int EmisLinId = Shader.PropertyToID("_EmisLin");

    MaterialPropertyBlock mpb;
    MaterialPropertyBlock dustMpb;
    Mesh dustMesh;
    // 割れ目の発光メッシュ（Astra v3p の ledge_crack_glow_*）。消灯中は描画そのものを止める。
    MeshRenderer[] crackGlowRenderers;
    bool crackGlowVisible = true;
    bool active;
    float introFade = 1f;
    float currentExposureScale = 1f;

    // 拍の通し番号（OnBeat を 1 拍 1 回だけ呼ぶための記録。絵づくりは拍頭からの位相で作るので
    // シークしてもこの値には依存しない）。
    int lastBeatIndex = -1;

    // 直近フレームで計算した演出量（検証で読む）。
    public float LastLanternScale { get; private set; } = 1f;
    public float LastCityScale { get; private set; } = 1f;
    public float LastCrackScale { get; private set; }
    public float LastCoreScale { get; private set; }
    public Vector2 LastShakeOffset { get; private set; }

    // ボスの代理スプライト（CG の 3D 空間側）。key = 元のボス GameObject の instanceID。
    readonly Dictionary<int, SpriteRenderer> bossProxies = new Dictionary<int, SpriteRenderer>();
    readonly List<int> proxyScratch = new List<int>();
    Transform bossParent;
    Transform proxyRoot;

    void OnEnable()
    {
        ApplyGlobals();
        if (Application.isPlaying) StoneCgIntro.Available = true;
    }

    void OnDisable()
    {
        StoneCgIntro.Available = false;
        ClearBossProxies();
    }

    void LateUpdate()
    {
        ApplyGlobals();
        bool want = ShouldShow(out float stageTime);
        if (want != active)
        {
            active = want;
            if (cgCamera != null) cgCamera.gameObject.SetActive(want);
            if (cgSceneRoot != null) cgSceneRoot.SetActive(want);
            if (displayQuad != null) displayQuad.gameObject.SetActive(want);
        }
        if (!want)
        {
            ClearBossProxies();
            return;
        }
        introFade = StoneCgIntro.BlackFade(stageTime);
        UpdateStageFx(stageTime);
        ApplyCamera(stageTime);
        ApplyDisplay();
        UpdateBossProxies();
        DrawDust(stageTime);
    }

    bool ShouldShow(out float stageTime)
    {
        stageTime = 0f;
        if (!Application.isPlaying) return false;
        GManager g = GManager.Control;
        if (g == null || g.state != GManager.GameState.Playing) return false;
        StageReader reader = g.SReader;
        if (reader == null) return false;
        StageData stage = reader.CurrentStage;
        if (stage == null) return false;
        bool match = (!string.IsNullOrEmpty(stage.stageDirectoryName) && stage.stageDirectoryName == targetStageDirectory)
                     || stage.stageName == targetStageNameFallback;
        if (!match) return false;
        stageTime = reader.CurrentTime;
        return true;
    }

    void ApplyGlobals()
    {
        Vector3 toLight = (sunFrom - sunTo).normalized;
        Shader.SetGlobalVector(SunDirId, new Vector4(toLight.x, toLight.y, toLight.z, 0f));
        Shader.SetGlobalVector(SunColorId, new Vector4(
            sunColorLinear.x * sunIntensity, sunColorLinear.y * sunIntensity, sunColorLinear.z * sunIntensity, 0f));
        Shader.SetGlobalVector(AmbientId, new Vector4(ambientLinear.x, ambientLinear.y, ambientLinear.z, 0f));
    }

    void ApplyDisplay()
    {
        if (displayQuad == null) return;
        if (displayQuad.sortingOrder != quadSortingOrder) displayQuad.sortingOrder = quadSortingOrder;
        mpb ??= new MaterialPropertyBlock();
        displayQuad.GetPropertyBlock(mpb);
        mpb.SetFloat(ExposureId, exposure * currentExposureScale);
        mpb.SetFloat(CenterDarkenId, centerDarken);
        mpb.SetFloat(BossBrightnessId, bossBrightness);
        mpb.SetFloat(FadeId, introFade);
        displayQuad.SetPropertyBlock(mpb);
    }

    /// <summary>通常姿勢の非対称フラスタム。フィールド (0,0,0)..(32,18,0) の四隅が画面四隅に一致する。</summary>
    public Matrix4x4 NormalProjection()
    {
        float n = nearClip;
        return Matrix4x4.Frustum(-16f * n / 36f, 16f * n / 36f, -20f * n / 36f, -2f * n / 36f, n, farClip);
    }

    /// <summary>見上げ姿勢の対称フラスタム（sensor 36mm 水平フィット・16:9）。</summary>
    public Matrix4x4 LookupProjection()
    {
        float n = nearClip;
        float halfW = n * 18f / Mathf.Max(1e-4f, lookupLensMm);
        float halfH = halfW * 9f / 16f;
        return Matrix4x4.Frustum(-halfW, halfW, -halfH, halfH, n, farClip);
    }

    void ApplyCamera(float stageTime)
    {
        if (cgCamera == null) return;
        // 見上げ姿勢を 1.06 秒まで保ち、4.56 秒で通常姿勢へ着地する ease in-out（smoothstep）。
        // 中点 2.81 秒が最大速度＝指示書 2.530（+0.28）の「ここで速度最大」。
        float e = StoneCgIntro.CameraProgress(stageTime);

        Quaternion lookupRot = Quaternion.LookRotation((lookupTarget - lookupPosition).normalized, Vector3.up);
        Vector2 shake = LastShakeOffset;
        cgCamera.transform.position = Vector3.Lerp(lookupPosition, normalPosition, e)
                                      + new Vector3(shake.x, shake.y, 0f);
        cgCamera.transform.rotation = Quaternion.Slerp(lookupRot, Quaternion.identity, e);

        Matrix4x4 a = LookupProjection();
        Matrix4x4 b = NormalProjection();
        Matrix4x4 p = new Matrix4x4();
        for (int i = 0; i < 16; i++) p[i] = Mathf.Lerp(a[i], b[i], e);
        // RT のアルファは「ボスの被覆率」として表示板が読むので、背景は透明の黒で消す。
        if (cgCamera.backgroundColor.a != 0f) cgCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cgCamera.nearClipPlane = nearClip;
        cgCamera.farClipPlane = farClip;
        cgCamera.projectionMatrix = p;
    }

    // --- ボスを CG の 3D 空間へ置く -------------------------------------------------
    //
    // 2D の論理座標 (x,y) と同じ画面位置に見えるよう、CGCamera の非対称フラスタムで
    // 平面 z = bossDepth へ逆投影する。カメラは (16,20,-36) にいるので、深さ z の平面では
    // 画面が (36+z)/36 倍に広がる。だから
    //   x_world = 16 + (x_field - 16) * (36+z)/36
    //   y_world = 20 + (y_field - 20) * (36+z)/36
    // と置き、大きさも同じ倍率を掛けると、投影後の位置・大きさが 2D のときと一致する。
    // これで足元 y_field=10 が岩棚の天面に載ったまま、ボスが CG と同じ空間の住人になる。

    /// <summary>論理座標を CG 空間（z = bossDepth の平面）へ逆投影する倍率。</summary>
    public float BossScaleFactor => (36f + bossDepth) / 36f;

    public Vector3 FieldToCgSpace(Vector2 fieldPos)
    {
        float k = BossScaleFactor;
        return new Vector3(16f + (fieldPos.x - 16f) * k, 20f + (fieldPos.y - 20f) * k, bossDepth);
    }

    void UpdateBossProxies()
    {
        if (bossParent == null || !bossParent)
        {
            BossManager bm = FindFirstObjectByType<BossManager>();
            bossParent = bm != null ? bm.transform.Find("Bosses") : null;
        }
        if (bossParent == null) { ClearBossProxies(); return; }
        if (proxyRoot == null || !proxyRoot)
        {
            GameObject go = new GameObject("BossProxies");
            go.transform.SetParent(transform, false);
            go.layer = gameObject.layer;
            proxyRoot = go.transform;
        }

        proxyScratch.Clear();
        proxyScratch.AddRange(bossProxies.Keys);

        int cgLayer = cgSceneRoot != null ? cgSceneRoot.layer : LayerMask.NameToLayer("StageCG");
        float k = BossScaleFactor;

        for (int i = 0; i < bossParent.childCount; i++)
        {
            Transform src = bossParent.GetChild(i);
            SpriteRenderer srcRenderer = src.GetComponent<SpriteRenderer>();
            if (srcRenderer == null) continue;
            // 2D 側の描画は止める（見えるのは CG 空間の代理だけ）。
            if (srcRenderer.enabled) srcRenderer.enabled = false;

            int id = src.gameObject.GetInstanceID();
            proxyScratch.Remove(id);
            if (!bossProxies.TryGetValue(id, out SpriteRenderer proxy) || proxy == null)
            {
                GameObject go = new GameObject("BossProxy");
                go.transform.SetParent(proxyRoot, false);
                go.layer = cgLayer;
                proxy = go.AddComponent<SpriteRenderer>();
                if (bossSpriteMaterial != null) proxy.sharedMaterial = bossSpriteMaterial;
                bossProxies[id] = proxy;
            }

            proxy.sprite = srcRenderer.sprite;
            proxy.flipX = srcRenderer.flipX;
            proxy.flipY = srcRenderer.flipY;
            proxy.enabled = srcRenderer.sprite != null;
            // 明度は表示板の _BossBrightness 側で掛けるので、ここでは元の色（フェード α）をそのまま。
            proxy.color = srcRenderer.color;

            Vector3 p = src.position;
            proxy.transform.position = FieldToCgSpace(new Vector2(p.x, p.y));
            proxy.transform.rotation = src.rotation;
            Vector3 sc = src.lossyScale;
            proxy.transform.localScale = new Vector3(sc.x * k, sc.y * k, 1f);
        }

        for (int i = 0; i < proxyScratch.Count; i++)
        {
            if (bossProxies.TryGetValue(proxyScratch[i], out SpriteRenderer dead) && dead != null)
            {
                DestroyProxy(dead.gameObject);
            }
            bossProxies.Remove(proxyScratch[i]);
        }
    }

    void ClearBossProxies()
    {
        if (bossProxies.Count == 0) return;
        foreach (SpriteRenderer proxy in bossProxies.Values)
        {
            if (proxy != null) DestroyProxy(proxy.gameObject);
        }
        bossProxies.Clear();
        // CG を止めるときは 2D 側の描画を戻す（他ステージ・リザルトで従来どおりに見える）。
        if (bossParent != null)
        {
            for (int i = 0; i < bossParent.childCount; i++)
            {
                SpriteRenderer sr = bossParent.GetChild(i).GetComponent<SpriteRenderer>();
                if (sr != null) sr.enabled = true;
            }
        }
    }

    static void DestroyProxy(GameObject go)
    {
        if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
    }

    // --- 形態変化（ゴーレム降臨）の演出 -------------------------------------------
    //
    // すべてステージ時計 stageTime だけから決まる（内部状態を持たない）ので、シーク・
    // ポーズ・録画のどれでも同じ絵になる。降下開始 = landTime - descendSec = 72.107、
    // 着地 = landTime = 72.94。
    //   前半: 拍頭でランタン(warm)と街の灯り(city_light)を +beatPulse、beatDecaySec で戻す。
    //   着地: CG カメラを揺らす / 端と上部から粉が落ちる / ランタンを lanternBlackoutSec 消す。
    //   後半: コアの赤い点光源が灯り、割れ目(crack_glow)が発光。ランタンは lateLanternScale、
    //         街の灯りは lateCityScale、空は lateSkyTint。拍連動はコアの明滅に移る。

    /// <summary>拍頭からの減衰エンベロープ 0..1（拍頭で 1、beatDecaySec で 0）。</summary>
    float BeatEnvelope(float stageTime, out int beatIndex)
    {
        float b = Mathf.Max(1e-4f, beatSec);
        beatIndex = Mathf.FloorToInt(stageTime / b);
        if (stageTime < 0f) return 0f;
        float phase = stageTime - beatIndex * b;
        float u = Mathf.Clamp01(1f - phase / Mathf.Max(1e-4f, beatDecaySec));
        return u * u;   // 拍頭で立ち上がり、戻りはゆっくり
    }

    /// <summary>拍頭からの位相（秒）。検証で拍頭・拍裏のコマを選ぶのに使う。</summary>
    public float BeatPhase(float stageTime)
    {
        float b = Mathf.Max(1e-4f, beatSec);
        return stageTime - Mathf.Floor(stageTime / b) * b;
    }

    /// <summary>着地の揺れ（既存 CameraShake と同じ減衰余弦）。範囲外では 0。</summary>
    Vector2 ShakeOffset(float stageTime)
    {
        float t = stageTime - landTime;
        if (t < 0f || t >= shakeDuration || shakeAmplitude <= 0f) return Vector2.zero;
        float remaining = 1f - t / Mathf.Max(1e-4f, shakeDuration);
        float decay = remaining * remaining;
        float w = t * shakeFrequency * (2f * Mathf.PI);
        float oy = -Mathf.Cos(w);
        float ox = Mathf.Cos(w * 0.9f + 1.7f) * 0.6f;   // 横は 0.6 倍（CameraShake と同じ）
        return new Vector2(ox, oy) * (shakeAmplitude * decay);
    }

    void UpdateStageFx(float stageTime)
    {
        if (!stageFxEnabled)
        {
            LastLanternScale = LastCityScale = 1f;
            LastCrackScale = LastCoreScale = 0f;
            LastShakeOffset = Vector2.zero;
            currentExposureScale = 1f;
            Vector4 one = new Vector4(1f, 1f, 1f, 1f);
            Shader.SetGlobalVector(EmisGrp1Id, one);
            Shader.SetGlobalVector(EmisGrp2Id, one);
            Shader.SetGlobalVector(EmisGrp3Id, Vector4.zero);
            Shader.SetGlobalVector(EmisGrp4Id, one);
            SetCoreLight(0f);
            ApplyCrackGlow(false);
            return;
        }

        float env = BeatEnvelope(stageTime, out int beatIndex);
        if (beatIndex != lastBeatIndex)
        {
            lastBeatIndex = beatIndex;
            OnBeat();
        }

        bool late = stageTime >= landTime;
        // コアは着地から lanternBlackoutSec かけて立ち上がる（ランタンが消えている間に入れ替わる）。
        float coreRamp = Mathf.Clamp01((stageTime - landTime) / Mathf.Max(1e-4f, lanternBlackoutSec));
        coreRamp = coreRamp * coreRamp * (3f - 2f * coreRamp);

        float lantern;
        if (!late) lantern = 1f + beatPulse * env;
        else if (stageTime < landTime + lanternBlackoutSec) lantern = 0f;
        else lantern = lateLanternScale;

        float city = late ? lateCityScale : 1f + beatPulse * env;
        float pulsed = coreRamp * (1f + corePulse * env);
        Vector3 sky = late ? lateSkyTint : Vector3.one;

        LastLanternScale = lantern;
        LastCityScale = city;
        LastCrackScale = pulsed;
        LastCoreScale = pulsed;
        LastShakeOffset = ShakeOffset(stageTime);
        currentExposureScale = late ? lateExposureScale : 1f;

        Shader.SetGlobalVector(EmisGrp1Id, new Vector4(lantern, lantern, lantern, 1f));
        Shader.SetGlobalVector(EmisGrp2Id, new Vector4(city, city, city, 1f));
        Shader.SetGlobalVector(EmisGrp3Id, new Vector4(pulsed, pulsed, pulsed, 1f));
        Shader.SetGlobalVector(EmisGrp4Id, new Vector4(sky.x, sky.y, sky.z, 1f));
        SetCoreLight(pulsed);
        ApplyCrackGlow(pulsed > 0.001f);
    }

    /// <summary>
    /// 割れ目の発光メッシュの表示。emission を 0 にしても板そのものは黒く描かれて岩棚に
    /// 黒い線が残るので、消灯中は MeshRenderer ごと切る（前半の絵は v3o と同じになる）。
    /// </summary>
    void ApplyCrackGlow(bool visible)
    {
        if (crackGlowRenderers == null || crackGlowRenderers.Length == 0)
        {
            if (cgSceneRoot == null) return;
            var list = new List<MeshRenderer>();
            foreach (MeshRenderer mr in cgSceneRoot.GetComponentsInChildren<MeshRenderer>(true))
                if (mr.name.StartsWith("ledge_crack_glow_")) list.Add(mr);
            crackGlowRenderers = list.ToArray();
            crackGlowVisible = true;   // 次の代入で必ず反映させる
            if (crackGlowRenderers.Length == 0) return;
        }
        if (visible == crackGlowVisible) return;
        crackGlowVisible = visible;
        for (int i = 0; i < crackGlowRenderers.Length; i++)
            if (crackGlowRenderers[i] != null) crackGlowRenderers[i].enabled = visible;
    }

    /// <summary>拍頭で 1 回だけ呼ばれるフック（明滅そのものは stageTime から作る）。</summary>
    public void OnBeat() { }

    /// <summary>ゴーレム降臨時のコア赤ライト。on=false で完全消灯。</summary>
    public void SetCoreLight(bool on) { SetCoreLight(on ? 1f : 0f); }

    /// <summary>コア赤ライトの強さ（0 で消灯）。シェーダのグローバル 1 灯ぶんを書く。</summary>
    public void SetCoreLight(float scale)
    {
        Shader.SetGlobalVector(CoreParamsId, new Vector4(
            coreLightPosition.x, coreLightPosition.y, coreLightPosition.z, coreRadius));
        float k = coreIntensity * Mathf.Max(0f, scale);
        Shader.SetGlobalVector(CoreColorId, new Vector4(
            coreColorLinear.x * k, coreColorLinear.y * k, coreColorLinear.z * k, 0f));
    }

    // --- 着地の粉 -----------------------------------------------------------------
    //
    // ParticleSystem は再生位置に依存して破綻するので、粉は stageTime の閉じた式で置く。
    // 粒 i の発生時刻・位置・大きさは Hash(i) で決まる決定的な値。中央（論理 x4..28・y2..16）
    // には落とさず、左右の端と画面上部だけに出す。

    static float Hash(int i, int salt)
    {
        uint h = (uint)(i * 73856093) ^ (uint)(salt * 19349663);
        h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
        return (h & 0xFFFFFFu) / 16777215f;
    }

    void DrawDust(float stageTime)
    {
        if (!stageFxEnabled || dustMaterial == null || dustCount <= 0 || cgCamera == null) return;
        float t0 = landTime;
        if (stageTime < t0 || stageTime > t0 + dustLifeSec + 0.6f) return;

        if (dustMesh == null) dustMesh = BuildQuad();
        dustMpb ??= new MaterialPropertyBlock();
        Vector4 emis = dustMaterial.GetVector(EmisLinId);
        const float depth = 10f;
        float k = (36f + depth) / 36f;
        int layer = cgSceneRoot != null ? cgSceneRoot.layer : gameObject.layer;

        for (int i = 0; i < dustCount; i++)
        {
            float ts = t0 + 0.5f * Hash(i, 1);
            float age = stageTime - ts;
            if (age < 0f || age > dustLifeSec) continue;

            bool top = (i % 3) == 2;
            float fx, fy0, g;
            if (top)
            {
                // 上部（天井）から。中央の帯（y 2..16）へは落ちきらない速さにする。
                fx = Mathf.Lerp(1f, 31f, Hash(i, 2));
                fy0 = Mathf.Lerp(18.2f, 19.6f, Hash(i, 3));
                g = 2.0f;
            }
            else
            {
                // 左右の端（論理 x 4..28 の外）だけ。
                bool left = Hash(i, 4) < 0.5f;
                fx = left ? Mathf.Lerp(0.3f, 3.9f, Hash(i, 2)) : Mathf.Lerp(28.1f, 31.7f, Hash(i, 2));
                fy0 = Mathf.Lerp(13.5f, 17.5f, Hash(i, 3));
                g = 8.0f;
            }
            float fy = fy0 - 0.5f * g * age * age;
            float size = Mathf.Lerp(0.10f, 0.22f, Hash(i, 5));
            float fade = Mathf.Clamp01((dustLifeSec - age) / 0.4f) * Mathf.Clamp01(age / 0.08f);

            Vector3 pos = new Vector3(16f + (fx - 16f) * k, 20f + (fy - 20f) * k, depth);
            Matrix4x4 m = Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(size * k, size * k, 1f));
            dustMpb.SetVector(EmisLinId, emis * fade);
            Graphics.DrawMesh(dustMesh, m, dustMaterial, layer, cgCamera, 0, dustMpb, false, false, false);
        }
    }

    static Mesh BuildQuad()
    {
        Mesh mesh = new Mesh { name = "StoneCgDustQuad", hideFlags = HideFlags.HideAndDontSave };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f),
        };
        mesh.normals = new[] { -Vector3.forward, -Vector3.forward, -Vector3.forward, -Vector3.forward };
        mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        return mesh;
    }
}

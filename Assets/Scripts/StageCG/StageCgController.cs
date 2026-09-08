using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ステージの 3D 背景 CG（Astra 制作）の制御。石工・艦長・浮浪者で共通の仕組みを使い、
/// 数値の違いは <see cref="StageCgProfile"/> 1 個にまとめてある（<see cref="profiles"/>）。
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
/// 有効化はステージ id と GameState.Playing でゲートする。プロファイルの無いステージ
/// （mirror / 25 / debug）・選択画面・リザルトでは CGCamera と板を非アクティブにするので、
/// 従来どおり黒背景のまま。
///
/// カメラの投影は Blender 側と同じ非対称フラスタムを直接与える（3 ステージとも同一）。
/// 導入とボスの 3D 配置・形態変化の演出はすべてステージ時計の閉じた式なので、シーク・
/// ポーズ・録画のどれでも同じ絵になる。
/// </summary>
[ExecuteAlways]
public class StageCgController : MonoBehaviour
{
    [Header("ステージ別プロファイル")]
    [Tooltip("先頭から順にステージ id を照合し、最初に一致したものを使う。")]
    public StageCgProfile[] profiles = new StageCgProfile[0];

    [Header("参照（3 ステージ共通）")]
    public Camera cgCamera;
    public Renderer displayQuad;

    [Header("表示板の前後関係")]
    // URP 2D Renderer は MeshRenderer も 2D のソート(sortingLayer/sortingOrder)に載せるため、
    // 「不透明キューだから必ず奥」にはならない。実測(.tmp_cg)では
    //   板 0(既定) → 弾より奥だが sortingOrder -10 のボスより手前でボスを隠す
    //   板 -11 / -20 → CG・ボス・弾がすべて意図どおり(奥→手前)
    //   板 -100 以下 → CG が描かれなくなる(BackCamera 側の描画順との兼ね合い)
    public int quadSortingOrder = -20;

    [Header("ボスの代理スプライト")]
    [Tooltip("ボスの代理スプライトに使うマテリアル（シェーダ StoneCG/BossSprite）。")]
    public Material bossSpriteMaterial;

    [Header("カメラ（通常姿勢・3 ステージ共通）")]
    public Vector3 normalPosition = new Vector3(16f, 20f, -36f);
    public float nearClip = 0.05f;
    public float farClip = 450f;

    [Tooltip("false にすると形態変化の演出（拍連動・揺れ・粉・光の切替）を全て切る。")]
    public bool stageFxEnabled = true;

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
    static readonly int FadeAlphaId = Shader.PropertyToID("_FadeAlpha");

    MaterialPropertyBlock mpb;
    MaterialPropertyBlock dustMpb;
    MaterialPropertyBlock phaseMpb;
    Mesh dustMesh;
    bool active;
    float introFade = 1f;
    float currentExposureScale = 1f;

    /// <summary>いま使っているプロファイル（CG 非表示なら null）。</summary>
    public StageCgProfile Profile { get; private set; }
    StageCgProfile lastProfile;

    // 拍の通し番号（OnBeat を 1 拍 1 回だけ呼ぶための記録。絵づくりは拍頭からの位相で作るので
    // シークしてもこの値には依存しない）。
    int lastBeatIndex = -1;

    // 直近フレームで計算した演出量（検証で読む）。
    public float LastLanternScale { get; private set; } = 1f;
    public float LastCityScale { get; private set; } = 1f;
    public float LastCrackScale { get; private set; }
    public float LastCoreScale { get; private set; }
    public float LastPhase1Alpha { get; private set; } = 1f;
    public float LastPhase2Alpha { get; private set; }
    public Vector2 LastShakeOffset { get; private set; }

    // ボスの代理スプライト（CG の 3D 空間側）。key = 元のボス GameObject の instanceID。
    readonly Dictionary<int, SpriteRenderer> bossProxies = new Dictionary<int, SpriteRenderer>();
    readonly List<int> proxyScratch = new List<int>();
    Transform bossParent;
    Transform proxyRoot;

    /// <summary>シーン内の名前で拾った演出用のオブジェクト群（プロファイルごとに 1 度だけ集める）。</summary>
    class SceneCache
    {
        public MeshRenderer[] crackGlow;      // 石工: ledge_crack_glow_*
        public MeshRenderer[] phase1;         // p1_*
        public MeshRenderer[] phase2;         // p2_*
        public MeshRenderer[] hidePhase2;     // 第 2 フェーズで隠す既存オブジェクト
        public Transform[] wisps;             // 浮浪者: p2_wisp_*
        public Vector3[] wispHome;
        public Transform[] drifters;          // 浮浪者: p2_fog_* / p1_dust_*
        public Vector3[] drifterHome;
        public float[] drifterSpeed;
        public float[] drifterRange;
        public bool crackVisible = true;
        public bool hideApplied;
        public bool hideState;
    }
    readonly Dictionary<GameObject, SceneCache> caches = new Dictionary<GameObject, SceneCache>();

    void OnEnable()
    {
        if (Application.isPlaying) StageCgIntro.Available = true;
    }

    void OnDisable()
    {
        StageCgIntro.Available = false;
        StageCgIntro.ActiveProfile = null;
        ClearBossProxies();
    }

    void LateUpdate()
    {
        StageCgProfile want = ShouldShow(out float stageTime);
        Profile = want;
        StageCgIntro.ActiveProfile = want;

        if (want != lastProfile)
        {
            // 前のプロファイルの CG 本体を切り、新しい方を出す。
            if (lastProfile != null && lastProfile.sceneRoot != null) lastProfile.sceneRoot.SetActive(false);
            if (want != null && want.sceneRoot != null) want.sceneRoot.SetActive(true);
            lastProfile = want;
        }
        bool show = want != null;
        if (show != active)
        {
            active = show;
            if (cgCamera != null) cgCamera.gameObject.SetActive(show);
            if (displayQuad != null) displayQuad.gameObject.SetActive(show);
        }
        if (!show)
        {
            // 編集中はシーンビューの見た目のために先頭プロファイルのライティングを流しておく。
            if (!Application.isPlaying && profiles.Length > 0 && profiles[0] != null) ApplyGlobals(profiles[0]);
            ClearBossProxies();
            // 演出のグローバルは他ステージへ持ち越さない（材質を共有していないので絵には
            // 出ないが、CG のあるステージを抜けた時点で必ず素の値に戻しておく）。
            ResetStageFxGlobals();
            return;
        }

        ApplyGlobals(want);
        introFade = want.BlackFade(stageTime);
        UpdateStageFx(want, stageTime);
        ApplyCamera(want, stageTime);
        ApplyDisplay(want);
        UpdateBossProxies(want);
        DrawDust(want, stageTime);
    }

    StageCgProfile ShouldShow(out float stageTime)
    {
        stageTime = 0f;
        if (!Application.isPlaying) return null;
        GManager g = GManager.Control;
        if (g == null || g.state != GManager.GameState.Playing) return null;
        StageReader reader = g.SReader;
        if (reader == null) return null;
        StageData stage = reader.CurrentStage;
        if (stage == null) return null;
        for (int i = 0; i < profiles.Length; i++)
        {
            if (profiles[i] != null && profiles[i].Matches(stage))
            {
                stageTime = reader.CurrentTime;
                return profiles[i];
            }
        }
        return null;
    }

    void ApplyGlobals(StageCgProfile p)
    {
        Vector3 toLight = (p.sunFrom - p.sunTo).normalized;
        Shader.SetGlobalVector(SunDirId, new Vector4(toLight.x, toLight.y, toLight.z, 0f));
        Shader.SetGlobalVector(SunColorId, new Vector4(
            p.sunColorLinear.x * p.sunIntensity, p.sunColorLinear.y * p.sunIntensity, p.sunColorLinear.z * p.sunIntensity, 0f));
        Shader.SetGlobalVector(AmbientId, new Vector4(p.ambientLinear.x, p.ambientLinear.y, p.ambientLinear.z, 0f));
    }

    void ApplyDisplay(StageCgProfile p)
    {
        if (displayQuad == null) return;
        if (displayQuad.sortingOrder != quadSortingOrder) displayQuad.sortingOrder = quadSortingOrder;
        mpb ??= new MaterialPropertyBlock();
        displayQuad.GetPropertyBlock(mpb);
        mpb.SetFloat(ExposureId, p.exposure * currentExposureScale);
        mpb.SetFloat(CenterDarkenId, p.centerDarken);
        mpb.SetFloat(BossBrightnessId, p.bossBrightness);
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
    public Matrix4x4 LookupProjection(StageCgProfile p)
    {
        float n = nearClip;
        float halfW = n * 18f / Mathf.Max(1e-4f, p.lookupLensMm);
        float halfH = halfW * 9f / 16f;
        return Matrix4x4.Frustum(-halfW, halfW, -halfH, halfH, n, farClip);
    }

    void ApplyCamera(StageCgProfile p, float stageTime)
    {
        if (cgCamera == null) return;
        float e = p.CameraProgress(stageTime);

        Quaternion lookupRot = Quaternion.LookRotation((p.lookupTarget - p.lookupPosition).normalized, Vector3.up);
        Vector2 shake = LastShakeOffset;
        cgCamera.transform.position = Vector3.Lerp(p.lookupPosition, normalPosition, e)
                                      + new Vector3(shake.x, shake.y, 0f);
        cgCamera.transform.rotation = Quaternion.Slerp(lookupRot, Quaternion.identity, e);

        Matrix4x4 a = LookupProjection(p);
        Matrix4x4 b = NormalProjection();
        Matrix4x4 proj = new Matrix4x4();
        for (int i = 0; i < 16; i++) proj[i] = Mathf.Lerp(a[i], b[i], e);
        // RT のアルファは「ボスの被覆率」として表示板が読むので、背景は透明の黒で消す。
        if (cgCamera.backgroundColor.a != 0f) cgCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cgCamera.nearClipPlane = nearClip;
        cgCamera.farClipPlane = farClip;
        cgCamera.projectionMatrix = proj;
    }

    // --- ボスを CG の 3D 空間へ置く -------------------------------------------------
    //
    // 2D の論理座標 (x,y) と同じ画面位置に見えるよう、CGCamera の非対称フラスタムで
    // 平面 z = bossDepth へ逆投影する。カメラは (16,20,-36) にいるので、深さ z の平面では
    // 画面が (36+z)/36 倍に広がる。だから
    //   x_world = 16 + (x_field - 16) * (36+z)/36
    //   y_world = 20 + (y_field - 20) * (36+z)/36
    // と置き、大きさも同じ倍率を掛けると、投影後の位置・大きさが 2D のときと一致する。

    /// <summary>論理座標を CG 空間（z = bossDepth の平面）へ逆投影する倍率。</summary>
    public float BossScaleFactor(StageCgProfile p) => (36f + p.bossDepth) / 36f;

    public Vector3 FieldToCgSpace(StageCgProfile p, Vector2 fieldPos)
    {
        float k = BossScaleFactor(p);
        return new Vector3(16f + (fieldPos.x - 16f) * k, 20f + (fieldPos.y - 20f) * k, p.bossDepth);
    }

    void UpdateBossProxies(StageCgProfile p)
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

        int cgLayer = p.sceneRoot != null ? p.sceneRoot.layer : LayerMask.NameToLayer("StageCG");
        float k = BossScaleFactor(p);

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

            proxy.gameObject.layer = cgLayer;
            proxy.sprite = srcRenderer.sprite;
            proxy.flipX = srcRenderer.flipX;
            proxy.flipY = srcRenderer.flipY;
            proxy.enabled = srcRenderer.sprite != null;
            // 明度は表示板の _BossBrightness 側で掛けるので、ここでは元の色（フェード α）をそのまま。
            proxy.color = srcRenderer.color;

            Vector3 pos = src.position;
            proxy.transform.position = FieldToCgSpace(p, new Vector2(pos.x, pos.y));
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

    // --- 形態変化の演出 -----------------------------------------------------------
    //
    // すべてステージ時計 stageTime だけから決まる（内部状態を持たない）ので、シーク・
    // ポーズ・録画のどれでも同じ絵になる。共通部分は「p1_* を消して p2_* を出す」
    // クロスフェードで、それに加えてステージごとの味付けを phase で選ぶ。

    /// <summary>拍頭からの減衰エンベロープ 0..1（拍頭で 1、beatDecaySec で 0）。</summary>
    float BeatEnvelope(StageCgProfile p, float stageTime, out int beatIndex)
    {
        float b = Mathf.Max(1e-4f, p.beatSec);
        beatIndex = Mathf.FloorToInt(stageTime / b);
        if (stageTime < 0f) return 0f;
        float phase = stageTime - beatIndex * b;
        float u = Mathf.Clamp01(1f - phase / Mathf.Max(1e-4f, p.beatDecaySec));
        return u * u;   // 拍頭で立ち上がり、戻りはゆっくり
    }

    /// <summary>拍頭からの位相（秒）。検証で拍頭・拍裏のコマを選ぶのに使う。</summary>
    public float BeatPhase(float stageTime)
    {
        float b = Profile != null ? Mathf.Max(1e-4f, Profile.beatSec) : 0.4166667f;
        return stageTime - Mathf.Floor(stageTime / b) * b;
    }

    /// <summary>着地の揺れ（既存 CameraShake と同じ減衰余弦）。範囲外では 0。</summary>
    Vector2 ShakeOffset(StageCgProfile p, float stageTime)
    {
        float t = stageTime - p.phaseTime;
        if (t < 0f || t >= p.shakeDuration || p.shakeAmplitude <= 0f) return Vector2.zero;
        float remaining = 1f - t / Mathf.Max(1e-4f, p.shakeDuration);
        float decay = remaining * remaining;
        float w = t * p.shakeFrequency * (2f * Mathf.PI);
        float oy = -Mathf.Cos(w);
        float ox = Mathf.Cos(w * 0.9f + 1.7f) * 0.6f;   // 横は 0.6 倍（CameraShake と同じ）
        return new Vector2(ox, oy) * (p.shakeAmplitude * decay);
    }

    /// <summary>発光スケールとコア光を素の値へ戻す（演出オフ・CG の無いステージ）。</summary>
    void ResetStageFxGlobals()
    {
        LastLanternScale = LastCityScale = 1f;
        LastCrackScale = LastCoreScale = 0f;
        LastPhase1Alpha = 1f; LastPhase2Alpha = 0f;
        LastShakeOffset = Vector2.zero;
        currentExposureScale = 1f;
        Vector4 one = new Vector4(1f, 1f, 1f, 1f);
        Shader.SetGlobalVector(EmisGrp1Id, one);
        Shader.SetGlobalVector(EmisGrp2Id, one);
        Shader.SetGlobalVector(EmisGrp3Id, Vector4.zero);
        Shader.SetGlobalVector(EmisGrp4Id, one);
        Shader.SetGlobalVector(CoreParamsId, new Vector4(0f, 0f, 0f, 1f));
        Shader.SetGlobalVector(CoreColorId, Vector4.zero);
    }

    SceneCache GetCache(StageCgProfile p)
    {
        if (p.sceneRoot == null) return null;
        if (caches.TryGetValue(p.sceneRoot, out SceneCache c) && c != null) return c;
        c = new SceneCache();
        var crack = new List<MeshRenderer>();
        var ph1 = new List<MeshRenderer>();
        var ph2 = new List<MeshRenderer>();
        var hide = new List<MeshRenderer>();
        var wisp = new List<Transform>();
        var drift = new List<Transform>();
        var driftSpeed = new List<float>();
        var driftRange = new List<float>();
        foreach (MeshRenderer mr in p.sceneRoot.GetComponentsInChildren<MeshRenderer>(true))
        {
            string n = mr.name;
            if (n.StartsWith("ledge_crack_glow_")) crack.Add(mr);
            if (n.StartsWith("p1_")) ph1.Add(mr);
            else if (n.StartsWith("p2_")) ph2.Add(mr);
            else
            {
                for (int i = 0; i < p.hideInPhase2.Length; i++)
                {
                    if (!string.IsNullOrEmpty(p.hideInPhase2[i]) && n.StartsWith(p.hideInPhase2[i])) { hide.Add(mr); break; }
                }
            }
            if (n.StartsWith("p2_wisp_")) wisp.Add(mr.transform);
            if (n.StartsWith("p2_fog_")) { drift.Add(mr.transform); driftSpeed.Add(p.fogDriftSpeed); driftRange.Add(p.fogDriftRange); }
            else if (n.StartsWith("p1_dust_")) { drift.Add(mr.transform); driftSpeed.Add(p.dustDriftSpeed); driftRange.Add(p.dustDriftRange); }
        }
        c.crackGlow = crack.ToArray();
        c.phase1 = ph1.ToArray();
        c.phase2 = ph2.ToArray();
        c.hidePhase2 = hide.ToArray();
        c.wisps = wisp.ToArray();
        c.wispHome = new Vector3[c.wisps.Length];
        for (int i = 0; i < c.wisps.Length; i++) c.wispHome[i] = c.wisps[i].localPosition;
        c.drifters = drift.ToArray();
        c.drifterHome = new Vector3[c.drifters.Length];
        for (int i = 0; i < c.drifters.Length; i++) c.drifterHome[i] = c.drifters[i].localPosition;
        c.drifterSpeed = driftSpeed.ToArray();
        c.drifterRange = driftRange.ToArray();
        caches[p.sceneRoot] = c;
        return c;
    }

    void UpdateStageFx(StageCgProfile p, float stageTime)
    {
        SceneCache cache = GetCache(p);
        if (!stageFxEnabled)
        {
            ResetStageFxGlobals();
            if (cache != null)
            {
                ApplyPhaseAlpha(cache.phase1, 1f);
                ApplyPhaseAlpha(cache.phase2, 0f);
                SetRenderers(cache.crackGlow, false, ref cache.crackVisible);
                SetHidden(cache, false);
            }
            return;
        }

        float env = BeatEnvelope(p, stageTime, out int beatIndex);
        if (beatIndex != lastBeatIndex)
        {
            lastBeatIndex = beatIndex;
            OnBeat();
        }

        // --- p1 / p2 のクロスフェード（共通） ---
        float a2 = 0f;
        if (p.phase != StageCgPhaseKind.None)
        {
            a2 = Mathf.Clamp01((stageTime - p.phaseTime) / Mathf.Max(1e-4f, p.phaseCrossfadeSec));
            a2 = a2 * a2 * (3f - 2f * a2);
        }
        float a1 = 1f - a2;
        LastPhase1Alpha = a1;
        LastPhase2Alpha = a2;
        bool late = p.phase != StageCgPhaseKind.None && stageTime >= p.phaseTime;
        if (cache != null)
        {
            ApplyPhaseAlpha(cache.phase1, a1);
            ApplyPhaseAlpha(cache.phase2, a2);
            SetHidden(cache, late);
        }

        // --- 発光グループのスケール ---
        float lantern = 1f + p.beatPulse * env;
        float city = 1f;
        float coreScale = 0f;
        Vector3 sky = Vector3.one;
        currentExposureScale = 1f;

        switch (p.phase)
        {
            case StageCgPhaseKind.StoneGolem:
            {
                // コアは着地から lanternBlackoutSec かけて立ち上がる（ランタンが消えている間に入れ替わる）。
                float coreRamp = Mathf.Clamp01((stageTime - p.phaseTime) / Mathf.Max(1e-4f, p.lanternBlackoutSec));
                coreRamp = coreRamp * coreRamp * (3f - 2f * coreRamp);
                if (!late) lantern = 1f + p.beatPulse * env;
                else if (stageTime < p.phaseTime + p.lanternBlackoutSec) lantern = 0f;
                else lantern = p.lateLanternScale;
                city = late ? p.lateCityScale : 1f + p.beatPulse * env;
                coreScale = coreRamp * (1f + p.corePulse * env);
                sky = late ? p.lateSkyTint : Vector3.one;
                currentExposureScale = late ? p.lateExposureScale : 1f;
                LastShakeOffset = ShakeOffset(p, stageTime);
                break;
            }
            case StageCgPhaseKind.CaptainAnchor:
                // p1 は回路発光、p2 はランタンの光輪。どちらもグループ 1 なので同じ式でよい。
                // 街の灯りは帆に隠れるぶん、後半だけ少し持ち上げる。
                city = late ? 1.15f : 1f + p.beatPulse * env;
                LastShakeOffset = Vector2.zero;
                break;
            case StageCgPhaseKind.VagrantWisp:
                // 人魂・紋様（グループ 1）はゆっくり息づく。拍連動は使わない。
                lantern = 1f;
                city = 1f;
                LastShakeOffset = Vector2.zero;
                UpdateVagrantMotion(p, cache, stageTime);
                break;
            default:
                LastShakeOffset = Vector2.zero;
                break;
        }

        LastLanternScale = lantern;
        LastCityScale = city;
        LastCrackScale = coreScale;
        LastCoreScale = coreScale;

        Shader.SetGlobalVector(EmisGrp1Id, new Vector4(lantern, lantern, lantern, 1f));
        Shader.SetGlobalVector(EmisGrp2Id, new Vector4(city, city, city, 1f));
        Shader.SetGlobalVector(EmisGrp3Id, new Vector4(coreScale, coreScale, coreScale, 1f));
        Shader.SetGlobalVector(EmisGrp4Id, new Vector4(sky.x, sky.y, sky.z, 1f));
        SetCoreLight(p, coreScale);
        if (cache != null) SetRenderers(cache.crackGlow, coreScale > 0.001f, ref cache.crackVisible);
    }

    /// <summary>浮浪者の人魂の上下・霧と土埃の横流れ。位置はステージ時計の閉じた式。</summary>
    void UpdateVagrantMotion(StageCgProfile p, SceneCache cache, float stageTime)
    {
        if (cache == null) return;
        for (int i = 0; i < cache.wisps.Length; i++)
        {
            Transform t = cache.wisps[i];
            if (t == null) continue;
            float u = cache.wisps.Length > 1 ? i / (float)(cache.wisps.Length - 1) : 0f;
            float period = Mathf.Lerp(p.wispPeriodMin, p.wispPeriodMax, Hash(i, 11));
            float phase = Hash(i, 12) * Mathf.PI * 2f + u;
            float dy = Mathf.Sin(stageTime * (Mathf.PI * 2f / Mathf.Max(0.1f, period)) + phase) * p.wispAmplitude;
            Vector3 home = cache.wispHome[i];
            t.localPosition = new Vector3(home.x, home.y + dy, home.z);
        }
        for (int i = 0; i < cache.drifters.Length; i++)
        {
            Transform t = cache.drifters[i];
            if (t == null) continue;
            float range = cache.drifterRange[i];
            float speed = cache.drifterSpeed[i];
            float phase = Hash(i, 21) * Mathf.PI * 2f;
            float dx = Mathf.Sin(stageTime * speed + phase) * range;
            Vector3 home = cache.drifterHome[i];
            t.localPosition = new Vector3(home.x + dx, home.y, home.z);
        }
    }

    void ApplyPhaseAlpha(MeshRenderer[] renderers, float alpha)
    {
        if (renderers == null || renderers.Length == 0) return;
        phaseMpb ??= new MaterialPropertyBlock();
        bool visible = alpha > 0.002f;
        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer mr = renderers[i];
            if (mr == null) continue;
            if (mr.enabled != visible) mr.enabled = visible;
            if (!visible) continue;
            mr.GetPropertyBlock(phaseMpb);
            phaseMpb.SetFloat(FadeAlphaId, alpha);
            mr.SetPropertyBlock(phaseMpb);
        }
    }

    void SetHidden(SceneCache cache, bool hidden)
    {
        if (cache.hidePhase2 == null || cache.hidePhase2.Length == 0) return;
        if (cache.hideApplied && cache.hideState == hidden) return;
        cache.hideApplied = true;
        cache.hideState = hidden;
        for (int i = 0; i < cache.hidePhase2.Length; i++)
            if (cache.hidePhase2[i] != null) cache.hidePhase2[i].enabled = !hidden;
    }

    /// <summary>
    /// 割れ目の発光メッシュの表示。emission を 0 にしても板そのものは黒く描かれて岩棚に
    /// 黒い線が残るので、消灯中は MeshRenderer ごと切る。
    /// </summary>
    static void SetRenderers(MeshRenderer[] renderers, bool visible, ref bool state)
    {
        if (renderers == null || renderers.Length == 0) return;
        if (visible == state) return;
        state = visible;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].enabled = visible;
    }

    /// <summary>拍頭で 1 回だけ呼ばれるフック（明滅そのものは stageTime から作る）。</summary>
    public void OnBeat() { }

    /// <summary>コア赤ライトの強さ（0 で消灯）。シェーダのグローバル 1 灯ぶんを書く。</summary>
    public void SetCoreLight(StageCgProfile p, float scale)
    {
        Shader.SetGlobalVector(CoreParamsId, new Vector4(
            p.coreLightPosition.x, p.coreLightPosition.y, p.coreLightPosition.z, p.coreRadius));
        float k = p.coreIntensity * Mathf.Max(0f, scale);
        Shader.SetGlobalVector(CoreColorId, new Vector4(
            p.coreColorLinear.x * k, p.coreColorLinear.y * k, p.coreColorLinear.z * k, 0f));
    }

    // --- 着地の粉（石工） ---------------------------------------------------------
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

    void DrawDust(StageCgProfile p, float stageTime)
    {
        if (!stageFxEnabled || p.phase != StageCgPhaseKind.StoneGolem) return;
        if (p.dustMaterial == null || p.dustCount <= 0 || cgCamera == null) return;
        float t0 = p.phaseTime;
        if (stageTime < t0 || stageTime > t0 + p.dustLifeSec + 0.6f) return;

        if (dustMesh == null) dustMesh = BuildQuad();
        dustMpb ??= new MaterialPropertyBlock();
        Vector4 emis = p.dustMaterial.GetVector(EmisLinId);
        const float depth = 10f;
        float k = (36f + depth) / 36f;
        int layer = p.sceneRoot != null ? p.sceneRoot.layer : gameObject.layer;

        for (int i = 0; i < p.dustCount; i++)
        {
            float ts = t0 + 0.5f * Hash(i, 1);
            float age = stageTime - ts;
            if (age < 0f || age > p.dustLifeSec) continue;

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
            float fade = Mathf.Clamp01((p.dustLifeSec - age) / 0.4f) * Mathf.Clamp01(age / 0.08f);

            Vector3 pos = new Vector3(16f + (fx - 16f) * k, 20f + (fy - 20f) * k, depth);
            Matrix4x4 m = Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(size * k, size * k, 1f));
            dustMpb.SetVector(EmisLinId, emis * fade);
            Graphics.DrawMesh(dustMesh, m, p.dustMaterial, layer, cgCamera, 0, dustMpb, false, false, false);
        }
    }

    static Mesh BuildQuad()
    {
        Mesh mesh = new Mesh { name = "StageCgDustQuad", hideFlags = HideFlags.HideAndDontSave };
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

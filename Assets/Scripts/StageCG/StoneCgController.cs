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
    [Tooltip("ボスの明度。CG の露出・中央減光とは別に掛かる（表示板の _BossBrightness）。")]
    [Range(0f, 2f)] public float bossBrightness = 0.8f;

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

    MaterialPropertyBlock mpb;
    bool active;
    float introFade = 1f;

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
        ApplyCamera(stageTime);
        ApplyDisplay();
        UpdateBossProxies();
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
        mpb.SetFloat(ExposureId, exposure);
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
        cgCamera.transform.position = Vector3.Lerp(lookupPosition, normalPosition, e);
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

    // --- 今回は未実装（パラメータの置き場だけ用意する。拍連動・降臨の赤ライト） ---

    /// <summary>拍に合わせた CG の微動。今回は未実装。</summary>
    public void OnBeat() { }

    /// <summary>ゴーレム降臨時のコア赤ライト。今回は未実装。</summary>
    public void SetCoreLight(bool on) { }
}

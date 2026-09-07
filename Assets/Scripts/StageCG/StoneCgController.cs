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
/// 導入は 0〜lookupHoldTime 秒が見上げ、そこから settleTime 秒で ease-out cubic で通常姿勢へ。
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
    [Tooltip("表示板の露出。実運用では Astra のレンダーよりかなり暗くする。")]
    [Range(0f, 2f)] public float exposure = 0.45f;
    [Tooltip("フィールド中央（弾が飛ぶ帯）を落として弾の視認性を上げる量。")]
    [Range(0f, 1f)] public float centerDarken = 0.55f;

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

    [Header("導入（ステージ時計 秒）")]
    [Tooltip("0〜この秒までは見上げ姿勢を保つ。")]
    public float lookupHoldTime = 4.1f;
    [Tooltip("この秒に通常姿勢へ着地する。")]
    public float settleTime = 6.5f;

    // シェーダのグローバル uniform 名
    static readonly int SunDirId = Shader.PropertyToID("_StoneCgSunDir");
    static readonly int SunColorId = Shader.PropertyToID("_StoneCgSunColor");
    static readonly int AmbientId = Shader.PropertyToID("_StoneCgAmbient");
    static readonly int ExposureId = Shader.PropertyToID("_Exposure");
    static readonly int CenterDarkenId = Shader.PropertyToID("_CenterDarken");

    MaterialPropertyBlock mpb;
    bool active;

    void OnEnable() { ApplyGlobals(); }

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
        if (!want) return;
        ApplyCamera(stageTime);
        ApplyDisplay();
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
        float u = Mathf.InverseLerp(lookupHoldTime, Mathf.Max(lookupHoldTime + 1e-3f, settleTime), stageTime);
        u = Mathf.Clamp01(u);
        float e = 1f - Mathf.Pow(1f - u, 3f); // ease-out cubic

        Quaternion lookupRot = Quaternion.LookRotation((lookupTarget - lookupPosition).normalized, Vector3.up);
        cgCamera.transform.position = Vector3.Lerp(lookupPosition, normalPosition, e);
        cgCamera.transform.rotation = Quaternion.Slerp(lookupRot, Quaternion.identity, e);

        Matrix4x4 a = LookupProjection();
        Matrix4x4 b = NormalProjection();
        Matrix4x4 p = new Matrix4x4();
        for (int i = 0; i < 16; i++) p[i] = Mathf.Lerp(a[i], b[i], e);
        cgCamera.nearClipPlane = nearClip;
        cgCamera.farClipPlane = farClip;
        cgCamera.projectionMatrix = p;
    }

    // --- 今回は未実装（パラメータの置き場だけ用意する。拍連動・降臨の赤ライト） ---

    /// <summary>拍に合わせた CG の微動。今回は未実装。</summary>
    public void OnBeat() { }

    /// <summary>ゴーレム降臨時のコア赤ライト。今回は未実装。</summary>
    public void SetCoreLight(bool on) { }
}

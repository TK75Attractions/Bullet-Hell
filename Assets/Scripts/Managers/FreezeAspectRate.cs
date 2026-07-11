using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class FreezeAspectRate : MonoBehaviour
{
    public Vector2Int aspect = new Vector2Int(16,9);
    public Color32 colorbase = Color.black;
    [SerializeField] private float aspectRate;
    [SerializeField] private float cameraSize = 0;
    [SerializeField] private float oldSize = 0;
    [SerializeField] private float setTime = 0;
    [SerializeField] private Camera main;
    [SerializeField] private Camera backCamera;
    [SerializeField] private Camera UICamera;
    [SerializeField] private Camera frontCamera;
    [SerializeField] private Camera backImageCamera;
    [SerializeField] private Sprite Sup;
    [SerializeField] private Sprite Sdown;
    [SerializeField] private Sprite Sright;
    [SerializeField] private Sprite Sleft;
    [SerializeField] private Transform up, down, right, left;

    // ---- プレイ領域の額装(HUD帯と弾幕の被り対策) ----
    // プレイ中だけ MainCamera の orthographicSize と位置をずらし、論理 32x18 の
    // フィールド全体を HUD 帯(上端 playFrameTopPx)の下へ縮小表示する。
    // 論理座標・弾データ・カメラ rect(レターボックス機構)には一切触れない。
    // URP の Overlay カメラは viewport rect が効かないため、ビュー行列側で額装する。
    // ズームアウトで画面に入るフィールド外(弾の生存域 [-2,36)²)は、
    // PlayHudController 側の不透明額縁 UI が覆う前提。
    // インセットは 1080 基準 px。上は PlayHudController が BandH と同期させる。
    // 下 20px は四辺クローズの額縁(oracle レビュー playframe-ab-review:
    // Plan B 採用+下余白 18-20px 推奨)。
    [System.NonSerialized] public float playFrameTopPx = 104f;
    [System.NonSerialized] public float playFrameBottomPx = 20f;
    private const float PlayFrameAnimDur = 0.35f; // StageSelectManager.AnimateHUDIn と同じ
    private float playFrameBlend;
    private float playFrameTarget;
    private float appliedFrameEased = -1f;
    private float appliedFrameTop = -1f;
    private float appliedFrameBottom = -1f;
    private Vector3 playFrameBasePos;
    private float playFrameBaseSize;
    private bool playFrameBaseCaptured;

    public void Awake()
    {
        aspectRate = (float)aspect.x / aspect.y;
        main = GetComponent<Camera>();
        backCamera = transform.parent.parent.Find("BackCamera").GetComponent<Camera>();
        UICamera = transform.parent.Find("UICamera").GetComponent<Camera>();
        frontCamera = transform.parent.Find("FrontCamera").GetComponent<Camera>();
        backImageCamera = transform.parent.Find("BackImageCamera").GetComponent<Camera>();

        if (Application.isPlaying)
        {
            // 額装の基準(フルスクリーン時)のカメラ姿勢。シーン値 (16,9,-10)/size9。
            playFrameBasePos = main.transform.position;
            playFrameBaseSize = main.orthographicSize;
            playFrameBaseCaptured = true;
        }

        CreateBackCamera();
        UpdateScreenRate();
    }

    private void Update()
    {
        ChangeSize();
        UpdatePlayFrame();

        if (IsChangeAspect()) return;
        UpdateScreenRate();
        main.ResetAspect();
    }

    /// <summary>額装の適用度(0-1、イーズ済み)。額縁UIのフェードと同期させる。</summary>
    public float PlayFrameEased => Mathf.SmoothStep(0f, 1f, playFrameBlend);

    /// <summary>プレイ領域の額装を有効/無効にする(0.35s で補間)。</summary>
    public void SetPlayFrame(bool on) => playFrameTarget = on ? 1f : 0f;

    private void UpdatePlayFrame()
    {
        if (!playFrameBaseCaptured || main == null) return;
        if (playFrameBlend != playFrameTarget)
            playFrameBlend = Mathf.MoveTowards(playFrameBlend, playFrameTarget, Time.unscaledDeltaTime / PlayFrameAnimDur);

        // 値が変わったフレームだけ書く(CameraShake の LateUpdate オフセット/復元と
        // 毎フレーム上書きで衝突しないための guard)。
        float eased = PlayFrameEased;
        if (eased == appliedFrameEased && playFrameTopPx == appliedFrameTop && playFrameBottomPx == appliedFrameBottom) return;
        appliedFrameEased = eased;
        appliedFrameTop = playFrameTopPx;
        appliedFrameBottom = playFrameBottomPx;

        if (eased <= 0f)
        {
            main.orthographicSize = playFrameBaseSize;
            main.transform.position = playFrameBasePos;
            return;
        }

        float topN = playFrameTopPx / 1080f * eased;
        float botN = playFrameBottomPx / 1080f * eased;
        float scale = Mathf.Max(0.05f, 1f - topN - botN);
        float ortho = playFrameBaseSize / scale;
        // フィールド下端(world y=0)が画面下から botN、上端が画面上から topN に
        // 来るよう、ズームアウトぶんだけカメラを上へ置き直す。
        float fieldBottomY = playFrameBasePos.y - playFrameBaseSize;
        main.orthographicSize = ortho;
        main.transform.position = new Vector3(
            playFrameBasePos.x,
            fieldBottomY + ortho * (1f - 2f * botN),
            playFrameBasePos.z);
    }

    private void CreateBackCamera()
    {
#if UNITY_EDITOR
        if (!UnityEditor.EditorApplication.isPlaying) return;

#endif
        Debug.Log("Set BackCamera");
        backCamera.transform.position = new Vector3(0, 0, -5);
        backCamera.depth = -99;
        backCamera.orthographic = true;
        backCamera.fieldOfView = 0;
        backCamera.farClipPlane = 10;
        backCamera.nearClipPlane = 1;
        backCamera.depthTextureMode = DepthTextureMode.None;
        backCamera.renderingPath = RenderingPath.VertexLit;
        backCamera.useOcclusionCulling = false;
        up.GetComponent<SpriteRenderer>().sprite = Sup;
        down.GetComponent<SpriteRenderer>().sprite = Sdown;
        right.GetComponent<SpriteRenderer>().sprite = Sright;
        left.GetComponent<SpriteRenderer>().sprite = Sleft;
        up.GetComponent<SpriteRenderer>().color = colorbase;
        down.GetComponent<SpriteRenderer>().color = colorbase;
        right.GetComponent<SpriteRenderer>().color = colorbase;
        left.GetComponent<SpriteRenderer>().color = colorbase;
    }

    private void UpdateScreenRate()
    {
        if (aspect.x <= 0 || aspect.y <= 0) return;
        if (Screen.width <= 0 || Screen.height <= 0) return;
        if (main == null || UICamera == null || frontCamera == null || backImageCamera == null) return;
        if (up == null || down == null || right == null || left == null) return;

        aspectRate = (float)aspect.x / aspect.y;
        float baseAspect = (float)aspect.y / aspect.x;
        float nowAspect = (float)Screen.height / Screen.width;

        if (float.IsNaN(baseAspect) || float.IsInfinity(baseAspect)) return;
        if (float.IsNaN(nowAspect) || float.IsInfinity(nowAspect)) return;
        
        if (baseAspect > nowAspect)
        {
            float change = nowAspect / baseAspect;
            Rect set = new Rect((1 - change) * 0.5f, 0, change, 1);
            main.rect = set;
            UICamera.rect = set;
            frontCamera.rect = set;
            backImageCamera.rect = set;

            float h = main.orthographicSize * 2;
            float w = h * aspectRate;
            float x = (h + w) / 2f;
            right.localScale = new Vector3(h, h, 0);
            right.position = new Vector3(x, 0, 0);
            left.localScale = new Vector3(h, h, 0);
            left.position = new Vector3(-x, 0, 0);
            up.localScale = new Vector3(w, w, 0);
            up.position = new Vector3(0, x, 0);
            down.localScale = new Vector3(w, w, 0);
            down.position = new Vector3(0, -x, 0);
        }
        else
        {
            float change = baseAspect / nowAspect;

            Rect set = new Rect(0, (1 - change) * 0.5f, 1, change);
            main.rect = set;
            UICamera.rect = set;
            frontCamera.rect = set;
            backImageCamera.rect = set;

            float h = change * main.orthographicSize * 2f;
            float w = h * aspectRate;
            float x = (h + w) / 2f;
            
            right.localScale = new Vector3(h, h, 0);
            right.position = new Vector3(x, 0, 0);
            left.localScale = new Vector3(h, h, 0);
            left.position = new Vector3(-x, 0, 0);
            up.localScale = new Vector3(w, w, 0);
            up.position = new Vector3(0, x, 0);
            down.localScale = new Vector3(w, w, 0);
            down.position = new Vector3(0, -x, 0);
        }
    }

    private bool IsChangeAspect() => main.aspect == aspectRate;

    private void ChangeSize()
    {
        if (cameraSize == oldSize) return;

        setTime += Time.deltaTime;
        float f;
        if (setTime < 0.4f) f = oldSize + (cameraSize - oldSize) * setTime / 0.4f;
        else
        {
            f = cameraSize;
            oldSize = cameraSize;
        }
        
        main.orthographicSize = f;
        UICamera.orthographicSize = f;
        frontCamera.orthographicSize = f;
        backCamera.orthographicSize = f;
    }

    public void SetCameraSize(float f)
    {
        if (f > 0)
        {
            oldSize = cameraSize;
            cameraSize = f;
            setTime = 0;
        }
    }

    public void SetCameraSizeImediately(float f)
    {
        if(f > 0)
        {
            cameraSize = f;
            oldSize = f;
            main.orthographicSize = f;
            UICamera.orthographicSize = f;
            frontCamera.orthographicSize = f;
            backCamera.orthographicSize = f;
        }
    }

    public Camera GetUICamera() => UICamera;
}
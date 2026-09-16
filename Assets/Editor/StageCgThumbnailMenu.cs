using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ステージ選択の右パネルに出す「ステージ CG のサムネ」を焼き直すメニュー(2026-09-16 U3)。
///
/// プレイ中の背景 CG を、プレイ開始時の通常姿勢(<see cref="StageCgController.normalPosition"/> +
/// 非対称フラスタム <see cref="StageCgController.NormalProjection"/>)で 1280x720 に 1 枚レンダリングし、
/// <c>Assets/StageData/&lt;stage&gt;/cg_thumb.png</c> へ書き出す。
///
/// Play Mode に入らず Edit Mode で完結する:
///   - ボス・弾・自機・HUD はすべて実行時に出るものなので、Edit Mode では最初から写らない。
///   - 露出・中央減光・色調整はプレイと同じ表示板シェーダ(StoneCG/Display)を通す
///     (<see cref="StageCgProfile"/> の値をそのまま流し込む)。導入の黒フェード(_Fade)と
///     終端の暗転(_CgFade)だけは 1 = 「出し切った状態」で焼く。
///
/// 触るのは cgCamera の姿勢と各 CG の sceneRoot の表示だけで、どちらも実行後に元へ戻す。
/// CG の材質・モデルには一切触らない。
/// </summary>
public static class StageCgThumbnailMenu
{
    private const int ThumbWidth = 1280;
    private const int ThumbHeight = 720;

    private static readonly int SunDirId = Shader.PropertyToID("_StoneCgSunDir");
    private static readonly int SunColorId = Shader.PropertyToID("_StoneCgSunColor");
    private static readonly int AmbientId = Shader.PropertyToID("_StoneCgAmbient");

    [MenuItem("Tools/Bullet Hell/Stage Select/Render CG Thumbnails")]
    public static void RenderAll()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("StageCgThumbnailMenu: Play Mode を止めてから実行してください。");
            return;
        }
        StageCgController ctl = Object.FindFirstObjectByType<StageCgController>(FindObjectsInactive.Include);
        if (ctl == null || ctl.cgCamera == null || ctl.displayQuad == null)
        {
            Debug.LogError("StageCgThumbnailMenu: StageCgRig(StageCgController)が見つかりません。Base.unity を開いてください。");
            return;
        }

        Camera cam = ctl.cgCamera;
        Material display = ctl.displayQuad.sharedMaterial;
        if (display == null)
        {
            Debug.LogError("StageCgThumbnailMenu: 表示板のマテリアルがありません。");
            return;
        }

        // ---- 退避 ----
        bool camWasActive = cam.gameObject.activeSelf;
        Vector3 camPos = cam.transform.position;
        Quaternion camRot = cam.transform.rotation;
        RenderTexture camRT = cam.targetTexture;
        Color camBg = cam.backgroundColor;
        float camNear = cam.nearClipPlane, camFar = cam.farClipPlane;
        bool camUsedMatrix = cam.usePhysicalProperties;
        Matrix4x4 camProj = cam.projectionMatrix;
        Vector4 sunDir = Shader.GetGlobalVector(SunDirId);
        Vector4 sunColor = Shader.GetGlobalVector(SunColorId);
        Vector4 ambient = Shader.GetGlobalVector(AmbientId);
        List<bool> rootActive = new List<bool>();
        foreach (StageCgProfile p in ctl.profiles)
            rootActive.Add(p != null && p.sceneRoot != null && p.sceneRoot.activeSelf);

        RenderTexture rawRT = new RenderTexture(ThumbWidth, ThumbHeight, 24, RenderTextureFormat.DefaultHDR)
        { name = "CgThumbRaw", antiAliasing = 1, wrapMode = TextureWrapMode.Clamp };
        RenderTexture outRT = new RenderTexture(ThumbWidth, ThumbHeight, 0, RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.sRGB)
        { name = "CgThumbOut", wrapMode = TextureWrapMode.Clamp };
        Material blit = new Material(display.shader) { hideFlags = HideFlags.DontSave };
        Texture2D readback = new Texture2D(ThumbWidth, ThumbHeight, TextureFormat.RGBA32, false);
        List<string> written = new List<string>();

        try
        {
            cam.gameObject.SetActive(true);
            cam.transform.SetPositionAndRotation(ctl.normalPosition, Quaternion.identity);
            cam.nearClipPlane = ctl.nearClip;
            cam.farClipPlane = ctl.farClip;
            cam.projectionMatrix = ctl.NormalProjection();
            // RT のアルファは「ボスの被覆率」。ボスは Edit Mode に居ないので透明の黒で消す。
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.targetTexture = rawRT;

            for (int i = 0; i < ctl.profiles.Length; i++)
            {
                StageCgProfile p = ctl.profiles[i];
                if (p == null || p.sceneRoot == null || string.IsNullOrEmpty(p.stageDirectory)) continue;

                // 対象の CG だけを出す(他のステージの CG が同じ空間に重なっているため)。
                for (int j = 0; j < ctl.profiles.Length; j++)
                {
                    StageCgProfile q = ctl.profiles[j];
                    if (q != null && q.sceneRoot != null) q.sceneRoot.SetActive(i == j);
                }

                // ライティングはプロファイルの値をそのまま(StageCgController.ApplyGlobals と同じ式)。
                Vector3 toLight = (p.sunFrom - p.sunTo).normalized;
                Shader.SetGlobalVector(SunDirId, new Vector4(toLight.x, toLight.y, toLight.z, 0f));
                Shader.SetGlobalVector(SunColorId, new Vector4(
                    p.sunColorLinear.x * p.sunIntensity,
                    p.sunColorLinear.y * p.sunIntensity,
                    p.sunColorLinear.z * p.sunIntensity, 0f));
                Shader.SetGlobalVector(AmbientId, new Vector4(p.ambientLinear.x, p.ambientLinear.y, p.ambientLinear.z, 0f));

                cam.Render();

                // 表示板と同じ色作り(露出・中央減光・色相/彩度/被せ)。ボスは無いので _BossSplit=0。
                blit.SetTexture("_MainTex", rawRT);
                blit.SetTexture("_BossTex", Texture2D.blackTexture);
                blit.SetFloat("_BossSplit", 0f);
                blit.SetFloat("_Exposure", p.exposure);
                blit.SetFloat("_CenterDarken", p.centerDarken);
                blit.SetColor("_Tint", Color.white);
                blit.SetFloat("_BossBrightness", p.bossBrightness);
                blit.SetFloat("_Fade", 1f);
                blit.SetFloat("_CgFade", 1f);
                blit.SetFloat("_HueShift", p.hueShiftDeg);
                blit.SetFloat("_Saturation", p.saturation);
                blit.SetColor("_TintColor", p.tintColor);
                blit.SetFloat("_TintAmount", p.tintAmount);
                blit.SetFloat("_Flash", 0f);
                Graphics.Blit(rawRT, outRT, blit);

                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = outRT;
                readback.ReadPixels(new Rect(0f, 0f, ThumbWidth, ThumbHeight), 0, 0);
                readback.Apply();
                RenderTexture.active = prev;

                string dir = Path.Combine(Application.dataPath, "StageData", p.stageDirectory);
                if (!Directory.Exists(dir))
                {
                    Debug.LogWarning("StageCgThumbnailMenu: " + dir + " が無いので飛ばします。");
                    continue;
                }
                string path = Path.Combine(dir, "cg_thumb.png");
                File.WriteAllBytes(path, readback.EncodeToPNG());
                written.Add(p.stageDirectory + " -> " + path);
            }
        }
        finally
        {
            // ---- 復帰 ----
            cam.targetTexture = camRT;
            cam.transform.SetPositionAndRotation(camPos, camRot);
            cam.backgroundColor = camBg;
            cam.nearClipPlane = camNear;
            cam.farClipPlane = camFar;
            cam.usePhysicalProperties = camUsedMatrix;
            cam.projectionMatrix = camProj;
            cam.gameObject.SetActive(camWasActive);
            for (int i = 0; i < ctl.profiles.Length && i < rootActive.Count; i++)
            {
                StageCgProfile p = ctl.profiles[i];
                if (p != null && p.sceneRoot != null) p.sceneRoot.SetActive(rootActive[i]);
            }
            Shader.SetGlobalVector(SunDirId, sunDir);
            Shader.SetGlobalVector(SunColorId, sunColor);
            Shader.SetGlobalVector(AmbientId, ambient);

            Object.DestroyImmediate(blit);
            Object.DestroyImmediate(readback);
            rawRT.Release(); Object.DestroyImmediate(rawRT);
            outRT.Release(); Object.DestroyImmediate(outRT);
        }

        AssetDatabase.Refresh();
        Debug.Log("StageCgThumbnailMenu: " + written.Count + " 枚を書き出しました。\n" + string.Join("\n", written));
    }
}

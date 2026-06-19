using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public class ScreenNoiseBlurFeature : ScriptableRendererFeature
{
    [Serializable]
    public class ScreenNoiseBlurSettings
    {
        public Shader shader;
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        public bool applyInSceneView;
        public bool applyToOverlayCameras = true;
        public string[] targetCameraNames = { "MainCamera" };
        public string[] excludedCameraNames = { "UICamera" };
        [Range(0f, 64f)] public float maxBlurPixels = 32f;
    }

    [SerializeField] private ScreenNoiseBlurSettings settings = new ScreenNoiseBlurSettings();
    [SerializeField] private int debugAddRenderPassCount;
    [SerializeField] private int debugRecordRenderGraphCount;
    [SerializeField] private string debugLastCameraName;
    [SerializeField] private string debugLastSkipReason;

    private ScreenNoiseBlurPass pass;
    private Material material;

    public override void Create()
    {
        Shader shader = settings.shader != null
            ? settings.shader
            : Shader.Find("Hidden/BulletHell/ScreenNoiseBlur");

        if (shader != null && material == null)
        {
            material = CoreUtils.CreateEngineMaterial(shader);
        }

        pass = new ScreenNoiseBlurPass();
        pass.renderPassEvent = settings.renderPassEvent;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (material == null || pass == null)
        {
            debugLastSkipReason = "No material or pass";
            return;
        }

        if (!ShouldRender(renderingData.cameraData))
        {
            debugLastSkipReason = "Camera skipped";
            return;
        }

        CManager cameraManager = CManager.Current;
        if (cameraManager == null || !cameraManager.HasScreenNoise)
        {
            debugLastSkipReason = cameraManager == null ? "No CManager.Current" : "No active screen noise";
            return;
        }

        debugAddRenderPassCount++;
        debugLastCameraName = renderingData.cameraData.camera != null ? renderingData.cameraData.camera.name : "";
        debugLastSkipReason = "";
        pass.Setup(
            material,
            cameraManager.CurrentBlurPixels,
            cameraManager.CurrentJitterPixels,
            cameraManager.CurrentStrength,
            settings.maxBlurPixels,
            OnRecordRenderGraphPass
        );
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(material);
        material = null;
    }

    private bool ShouldRender(CameraData cameraData)
    {
        if (cameraData.isPreviewCamera)
        {
            return false;
        }

        if (cameraData.isSceneViewCamera && !settings.applyInSceneView)
        {
            return false;
        }

        if (!settings.applyToOverlayCameras && cameraData.renderType == CameraRenderType.Overlay)
        {
            return false;
        }

        Camera camera = cameraData.camera;
        if (camera == null)
        {
            return true;
        }

        if (settings.targetCameraNames != null && settings.targetCameraNames.Length > 0)
        {
            bool isTargetCamera = false;
            for (int i = 0; i < settings.targetCameraNames.Length; i++)
            {
                string targetName = settings.targetCameraNames[i];
                if (!string.IsNullOrEmpty(targetName) && camera.name == targetName)
                {
                    isTargetCamera = true;
                    break;
                }
            }

            if (!isTargetCamera)
            {
                return false;
            }
        }

        if (settings.excludedCameraNames == null)
        {
            return true;
        }

        for (int i = 0; i < settings.excludedCameraNames.Length; i++)
        {
            string excludedName = settings.excludedCameraNames[i];
            if (!string.IsNullOrEmpty(excludedName) && camera.name == excludedName)
            {
                return false;
            }
        }

        return true;
    }

    private void OnRecordRenderGraphPass()
    {
        debugRecordRenderGraphCount++;
    }

    private class ScreenNoiseBlurPass : ScriptableRenderPass
    {
        private static readonly int BlurParamsId = Shader.PropertyToID("_ScreenNoiseBlurParams");
        private static readonly int JitterParamsId = Shader.PropertyToID("_ScreenNoiseJitterParams");

        private const string PassName = "Screen Noise Blur";

        private Material material;
        private Vector2 blurPixels;
        private Vector2 jitterPixels;
        private float strength;
        private float maxBlurPixels;
        private Action onRecordRenderGraphPass;

        public void Setup(Material material, Vector2 blurPixels, Vector2 jitterPixels, float strength, float maxBlurPixels, Action onRecordRenderGraphPass)
        {
            this.material = material;
            this.blurPixels = blurPixels;
            this.jitterPixels = jitterPixels;
            this.strength = strength;
            this.maxBlurPixels = Mathf.Max(0f, maxBlurPixels);
            this.onRecordRenderGraphPass = onRecordRenderGraphPass;
            requiresIntermediateTexture = true;
            ConfigureInput(ScriptableRenderPassInput.Color);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (material == null || strength <= 0f || maxBlurPixels <= 0f)
            {
                return;
            }

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer)
            {
                return;
            }

            ApplyMaterialParameters();

            TextureHandle source = resourceData.activeColorTexture;
            TextureDesc destinationDesc = renderGraph.GetTextureDesc(source);
            destinationDesc.name = "CameraColor-ScreenNoiseBlur";
            destinationDesc.clearBuffer = false;
            TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

            RenderGraphUtils.BlitMaterialParameters parameters = new RenderGraphUtils.BlitMaterialParameters(source, destination, material, 0);
            renderGraph.AddBlitPass(parameters, PassName);

            resourceData.cameraColor = destination;
            onRecordRenderGraphPass?.Invoke();
        }

        private void ApplyMaterialParameters()
        {
            Vector2 clampedBlur = new Vector2(
                Mathf.Min(Mathf.Abs(blurPixels.x), maxBlurPixels),
                Mathf.Min(Mathf.Abs(blurPixels.y), maxBlurPixels)
            );
            Vector2 clampedJitter = new Vector2(
                Mathf.Clamp(jitterPixels.x, -maxBlurPixels, maxBlurPixels),
                Mathf.Clamp(jitterPixels.y, -maxBlurPixels, maxBlurPixels)
            );

            material.SetVector(BlurParamsId, new Vector4(clampedBlur.x, clampedBlur.y, strength, 0f));
            material.SetVector(JitterParamsId, new Vector4(clampedJitter.x, clampedJitter.y, 0f, 0f));
        }
    }
}

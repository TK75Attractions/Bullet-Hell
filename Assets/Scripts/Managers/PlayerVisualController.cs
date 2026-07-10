using UnityEngine;

/// <summary>
/// プレイヤーの横方向の実移動量に応じて、neutral / left / right GIF フレームを再生する。
/// 色は 2 色パレットシェーダーの Color1 / Color2 として毎フレーム指定できる。
/// </summary>
[DisallowMultipleComponent]
public class PlayerVisualController : MonoBehaviour
{
    private const float HorizontalMovementThreshold = 0.001f;

    private SpriteRenderer spriteRenderer;
    private PlayerVisualSetAsset visualSet;
    private PlayerVisualClipAsset currentClip;
    private Material runtimeMaterial;
    private int frameIndex;
    private float frameTimer;

    public Color Color1 { get; private set; }
    public Color Color2 { get; private set; }

    public void Initialize(SpriteRenderer renderer, Color color1, Color color2)
    {
        spriteRenderer = renderer;
        visualSet = Resources.Load<PlayerVisualSetAsset>("PlayerVisualSet");
        if (visualSet == null)
        {
            Debug.LogWarning("PlayerVisualSet could not be loaded from Resources.", this);
            return;
        }

        Material template = Resources.Load<Material>("PlayerPaletteSwap");
        if (template == null)
        {
            Debug.LogWarning("PlayerPaletteSwap material could not be loaded from Resources.", this);
        }
        else if (spriteRenderer != null)
        {
            runtimeMaterial = new Material(template) { name = "PlayerPaletteSwap (Runtime)" };
            spriteRenderer.material = runtimeMaterial;
        }

        SetColors(color1, color2);
        SetClip(visualSet.neutral);
    }

    public void UpdateVisual(float dt, float horizontalVelocity, Color color1, Color color2)
    {
        if (visualSet == null)
        {
            return;
        }

        SetColors(color1, color2);

        PlayerVisualClipAsset desiredClip = horizontalVelocity > HorizontalMovementThreshold
            ? visualSet.right
            : horizontalVelocity < -HorizontalMovementThreshold
                ? visualSet.left
                : visualSet.neutral;

        if (desiredClip != currentClip)
        {
            SetClip(desiredClip);
        }

        UpdateFrames(dt);
    }

    public void SetColors(Color color1, Color color2)
    {
        Color1 = color1;
        Color2 = color2;

        if (runtimeMaterial == null)
        {
            return;
        }

        // テクスチャは Linear 色空間でサンプリングされるため、Inspector / API から渡す
        // sRGB 色をここで Linear に変換してシェーダーのパレット比較と揃える。
        runtimeMaterial.SetColor("_Color1", Color1.linear);
        runtimeMaterial.SetColor("_Color2", Color2.linear);
    }

    public void ResetAnimation()
    {
        if (visualSet != null)
        {
            SetClip(visualSet.neutral);
        }
    }

    private void SetClip(PlayerVisualClipAsset clip)
    {
        currentClip = clip;
        frameIndex = 0;
        frameTimer = 0f;
        ApplyFrame();
    }

    private void UpdateFrames(float dt)
    {
        if (currentClip == null || currentClip.frames == null || currentClip.frames.Count <= 1)
        {
            return;
        }

        frameTimer += dt;
        while (frameTimer >= currentClip.GetFrameDuration(frameIndex))
        {
            frameTimer -= currentClip.GetFrameDuration(frameIndex);
            frameIndex = (frameIndex + 1) % currentClip.frames.Count;
            ApplyFrame();
        }
    }

    private void ApplyFrame()
    {
        if (spriteRenderer == null || currentClip == null || currentClip.frames == null || currentClip.frames.Count == 0)
        {
            return;
        }

        spriteRenderer.sprite = currentClip.frames[Mathf.Clamp(frameIndex, 0, currentClip.frames.Count - 1)];
    }

    private void OnDestroy()
    {
        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
        }
    }
}

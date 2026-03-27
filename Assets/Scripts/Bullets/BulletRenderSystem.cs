using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using System.Runtime.InteropServices;

public class BulletRenderSystem : MonoBehaviour
{
    public Mesh quadMesh;
    public Material material;

    const int MaxBullets = 65536;

    private ComputeBuffer bulletBuffer;
    private ComputeBuffer argsBuffer;
    private readonly uint[] cachedArgs = new uint[5];
    private int lastInstanceCount = -1;

    Texture2DArray textureArray;
    Texture2DArray maskArray;
    NativeArray<BulletRenderData> renderArray;


    #region //BulletRenderData Struct
    public void Init()
    {
        Debug.Log("Initializing BulletRenderSystem...");
        InitTextureArray();
        InitMaskArray();
        InitBuffers();

        renderArray = new NativeArray<BulletRenderData>(
            MaxBullets,
            Allocator.Persistent
        );
    }
    private void InitTextureArray()
    {
        Texture2D[] textures = GManager.Control.BTDB.GetBaseTextures();
        if (textures == null || textures.Length == 0)
        {
            Debug.LogWarning("BaseTextures is null or empty! Textures will not work.");
            return;
        }

        int w = textures[0].width;
        int h = textures[0].height;

        textureArray = new Texture2DArray(
            w, h, textures.Length,
            TextureFormat.RGBA32,
            false
        );
        textureArray.filterMode = FilterMode.Bilinear;
        textureArray.wrapMode = TextureWrapMode.Clamp;

        for (int i = 0; i < textures.Length; i++)
        {
            if (textures[i] != null)
            {
                Color[] pixels = GetPixelsForArraySlice(textures[i], w, h);
                textureArray.SetPixels(pixels, i);
            }
        }
        textureArray.Apply(false);

        material.SetTexture("_MainArray", textureArray);
    }
    private void InitMaskArray()
    {
        Texture2D[] maskTextures = GManager.Control.BTDB.GetMaskTextures();
        if (maskTextures == null || maskTextures.Length == 0)
        {
            Debug.LogWarning("MaskTextures is null or empty! Mask will not work.");
            return;
        }

        int w = maskTextures[0].width;
        int h = maskTextures[0].height;

        Debug.Log($"Initializing MaskArray: {maskTextures.Length} textures, size {w}x{h}");

        maskArray = new Texture2DArray(
            w, h, maskTextures.Length,
            TextureFormat.RGBA32,
            false
        );
        maskArray.filterMode = FilterMode.Bilinear;
        maskArray.wrapMode = TextureWrapMode.Clamp;

        for (int i = 0; i < maskTextures.Length; i++)
        {
            if (maskTextures[i] != null)
            {
                Color[] pixels = GetPixelsForArraySlice(maskTextures[i], w, h);

                // デバッグ: 最初のピクセルのRチャンネル値を確認
                if (i == 0 && pixels.Length > 0)
                {
                    Debug.Log($"First mask pixel R channel: {pixels[0].r}, Full color: {pixels[0]}");

                    // Rチャンネルの統計を取る
                    float minR = 1f, maxR = 0f, avgR = 0f;
                    foreach (var pixel in pixels)
                    {
                        avgR += pixel.r;
                        if (pixel.r < minR) minR = pixel.r;
                        if (pixel.r > maxR) maxR = pixel.r;
                    }
                    avgR /= pixels.Length;
                    Debug.Log($"Mask R channel - Min: {minR}, Max: {maxR}, Avg: {avgR}");
                }

                maskArray.SetPixels(pixels, i);
            }
            else
            {
                Debug.LogWarning($"MaskTexture at index {i} is null!");
            }
        }
        maskArray.Apply(false);

        material.SetTexture("_MaskArray", maskArray);
        Debug.Log("MaskArray initialized and set to material");
    }

    private Color[] GetPixelsForArraySlice(Texture2D source, int targetWidth, int targetHeight)
    {
        if (source.width == targetWidth && source.height == targetHeight)
        {
            return source.GetPixels();
        }

        Debug.LogWarning(
            $"Texture size mismatch for {source.name}. Resampling {source.width}x{source.height} -> {targetWidth}x{targetHeight}"
        );

        Color[] result = new Color[targetWidth * targetHeight];
        float invW = 1f / targetWidth;
        float invH = 1f / targetHeight;

        for (int y = 0; y < targetHeight; y++)
        {
            float v = (y + 0.5f) * invH;
            for (int x = 0; x < targetWidth; x++)
            {
                float u = (x + 0.5f) * invW;
                result[y * targetWidth + x] = source.GetPixelBilinear(u, v);
            }
        }

        return result;
    }
    private void InitBuffers()
    {
        bulletBuffer = new ComputeBuffer(
            MaxBullets,
            Marshal.SizeOf(typeof(BulletRenderData)),
            ComputeBufferType.Structured
        );

        argsBuffer = new ComputeBuffer(
            1,
            5 * sizeof(uint),
            ComputeBufferType.IndirectArguments
        );

        cachedArgs[0] = quadMesh.GetIndexCount(0);
        cachedArgs[1] = 0;
        cachedArgs[2] = quadMesh.GetIndexStart(0);
        cachedArgs[3] = quadMesh.GetBaseVertex(0);
        cachedArgs[4] = 0;
        argsBuffer.SetData(cachedArgs);
        lastInstanceCount = 0;
    }
    #endregion

    /// <summary>
    /// Burst Job 用のレンダーデータ作成
    /// </summary>
    public void BuildRenderData(NativeArray<BulletData> bullets, int count)
    {
        if (bullets.Length == 0 || count == 0)
        {
            UpdateInstanceCount(0);
            return;
        }

        if (count > MaxBullets)
        {
            Debug.LogWarning($"BuildRenderData count exceeded MaxBullets. Clamped {count} -> {MaxBullets}");
            count = MaxBullets;
        }

        int writeIndex = AppendRenderData(bullets, count, 0, count);
        bulletBuffer.SetData(renderArray, 0, 0, writeIndex);
        UpdateInstanceCount(writeIndex);
    }

    public void BuildRenderData(
        NativeArray<BulletData> enemyBullets,
        int enemyCount,
        NativeArray<BulletData> playerBullets,
        int playerCount)
    {
        int safeEnemyCount = math.max(0, enemyCount);
        int safePlayerCount = math.max(0, playerCount);
        int totalCount = safeEnemyCount + safePlayerCount;

        if (totalCount == 0)
        {
            UpdateInstanceCount(0);
            return;
        }

        if (totalCount > MaxBullets)
        {
            Debug.LogWarning($"Total bullet count exceeded MaxBullets. Clamped {totalCount} -> {MaxBullets}");
            totalCount = MaxBullets;
        }

        int writeIndex = 0;
        if (safeEnemyCount > 0 && enemyBullets.IsCreated)
        {
            writeIndex = AppendRenderData(enemyBullets, safeEnemyCount, writeIndex, totalCount);
        }

        if (safePlayerCount > 0 && playerBullets.IsCreated && writeIndex < totalCount)
        {
            writeIndex = AppendRenderData(playerBullets, safePlayerCount, writeIndex, totalCount);
        }

        bulletBuffer.SetData(renderArray, 0, 0, writeIndex);
        UpdateInstanceCount(writeIndex);
    }

    private int AppendRenderData(NativeArray<BulletData> bullets, int count, int startIndex, int maxCount)
    {
        int writeIndex = startIndex;
        int activeCount = 0;

        for (int i = 0; i < bullets.Length && writeIndex < maxCount; i++)
        {
            var b = bullets[i];
            if (!b.isActive) continue;
            
            var type = GManager.Control.BTDB.types[b.typeId];

            renderArray[writeIndex] = new BulletRenderData
            {
                pos = b.position,
                angle = b.angle,
                size = b.size * type.baseSize,
                texIndex = b.typeId,
                maskIndex = b.typeId,
                color = b.color,
            };
            writeIndex++;
            activeCount++;
            if (activeCount >= count) break;
        }

        return writeIndex;
    }

    private void UpdateInstanceCount(int count)
    {
        if (count == lastInstanceCount) return;
        cachedArgs[1] = (uint)count;
        argsBuffer.SetData(cachedArgs);
        lastInstanceCount = count;
    }

    public void Draw()
    {
        if (material == null || quadMesh == null || bulletBuffer == null || argsBuffer == null)
        {
            Debug.LogError($"Draw failed - Material:{material != null}, Mesh:{quadMesh != null}, BulletBuffer:{bulletBuffer != null}, ArgsBuffer:{argsBuffer != null}");
            return;
        }

        if (!material.shader.isSupported)
        {
            Debug.LogError($"Shader not supported: {material.shader.name}");
            return;
        }

        material.SetBuffer("_BulletBuffer", bulletBuffer);

        Bounds bounds = new Bounds(
            new Vector3(0, 0, 0),
            new Vector3(10000, 10000, 10000)
        );

        Graphics.DrawMeshInstancedIndirect(
            quadMesh,
            0,
            material,
            bounds,
            argsBuffer,
            0,
            null,
            UnityEngine.Rendering.ShadowCastingMode.Off,
            false,
            0,
            null
        );
    }

    void OnDestroy()
    {
        bulletBuffer?.Release();
        argsBuffer?.Release();
        if (renderArray.IsCreated) renderArray.Dispose();
    }
}
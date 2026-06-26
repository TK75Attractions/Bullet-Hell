using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using System;
using System.Runtime.InteropServices;

public class BulletRenderSystem : MonoBehaviour
{
    public Mesh quadMesh;
    public Material material;

    private const int MaxBullets = 65536;
    private const float disappearDuration = 0.1f; // 弾が完全に消えるまでの時間（秒）
    private const float appearBeatBaseAlpha = 0.2f; // a
    private const float appearBeatSinCoeff = 0.3f; // k
    private static readonly int CounterMaskTexelSizeId = Shader.PropertyToID("_CounterMaskTexelSize");

    private ComputeBuffer bulletBuffer;
    private ComputeBuffer argsBuffer;
    private readonly uint[] cachedArgs = new uint[5];
    private int lastInstanceCount = -1;

    Texture2DArray textureArray;
    Texture2DArray maskArray;
    BulletRenderData[] renderArray;
    BulletRenderData[] sortedRenderArray;
    int[] uniqueRenderPriorities;


    #region //BulletRenderData Struct
    public void Init()
    {
        Debug.Log("Initializing BulletRenderSystem...");
        InitTextureArray();
        InitMaskArray();
        InitBuffers();

        renderArray = new BulletRenderData[MaxBullets];
        sortedRenderArray = new BulletRenderData[MaxBullets];
        uniqueRenderPriorities = new int[MaxBullets];
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
        textureArray.filterMode = FilterMode.Point;
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
        maskArray.filterMode = FilterMode.Point;
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
        material.SetFloat(CounterMaskTexelSizeId, 1f / Mathf.Max(w, h));
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
        SortRenderData(writeIndex);
        bulletBuffer.SetData(renderArray, 0, 0, writeIndex);
        UpdateInstanceCount(writeIndex);
    }

    public void BuildRenderData(
        NativeArray<BulletData> enemyBullets,
        int enemyCount,
        NativeArray<BulletData> warpZones,
        int warpZoneCount,
        NativeArray<CounterBullet> counterBullets,
        int counterCount)
    {
        int safeEnemyCount = math.max(0, enemyCount);
        int safeWarpZoneCount = math.max(0, warpZoneCount);
        int safeCounterCount = math.max(0, counterCount);
        int totalCount = safeEnemyCount + safeWarpZoneCount + safeCounterCount * (CounterBullet.TrailCapacity + 1);

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

        if (safeWarpZoneCount > 0 && warpZones.IsCreated && writeIndex < totalCount)
        {
            writeIndex = AppendRenderData(warpZones, safeWarpZoneCount, writeIndex, totalCount);
        }

        if (safeCounterCount > 0 && counterBullets.IsCreated && writeIndex < totalCount)
        {
            writeIndex = AppendRenderData(counterBullets, writeIndex, totalCount);
        }

        SortRenderData(writeIndex);
        bulletBuffer.SetData(renderArray, 0, 0, writeIndex);
        UpdateInstanceCount(writeIndex);
    }

    private int AppendRenderData(NativeArray<BulletData> bullets, int count, int startIndex, int maxCount)
    {
        int writeIndex = startIndex;
        int activeCount = 0;
        float beatValueSin = GetBeatValueSin();

        for (int i = 0; i < bullets.Length && writeIndex < maxCount; i++)
        {
            var b = bullets[i];
            if (!b.isActive && !b.isClearing) continue;

            var type = GManager.Control.BTDB.types[b.typeId];

            float appear = 1f;
            float fadeIn = 1f;
            float fadeOut = 1f;
            float clearFade = b.isClearing ? b.GetClearFadeFactor() : 1f;
            if (b.appearDuration > 0f)
            {
                float appearStart = b.appearTime - b.appearDuration;
                if (b.time < appearStart)
                {
                    fadeIn = 0f;
                }
                else if (b.time < b.appearTime)
                {
                    fadeIn = math.saturate(appearBeatBaseAlpha + appearBeatSinCoeff * beatValueSin);
                }
                else
                {
                    fadeIn = 1f;
                }
            }
            else if (b.time < b.appearTime)
            {
                fadeIn = 0f;
            }

            if (b.life > 0f)
            {
                if (disappearDuration > 0f)
                {
                    float fadeOutStart = b.life - disappearDuration;
                    fadeOut = math.saturate((b.time - fadeOutStart) / disappearDuration);
                    fadeOut = 1f - fadeOut;
                }
            }

            appear = fadeIn * fadeOut * clearFade;


            // appear <= 0f でもisActiveなら必ず描画配列に入れる（透明度0で描画し、徐々に現れる）

            renderArray[writeIndex] = new BulletRenderData
            {
                // position は BulletDataUpdateJob でノイズ込みに更新済みの値を使う
                pos = b.position,
                angle = b.GetRotationAngle(),
                scale = b.scale * type.baseSize,
                texIndex = b.typeId,
                maskIndex = b.typeId,
                appear = appear,
                color = new float4(b.color.x, b.color.y, b.color.z, b.color.w * clearFade),
                renderPriority = type.renderPriority,
                renderMode = GetRenderMode(type),
            };
            writeIndex++;
            activeCount++;
            if (activeCount >= count) break;
        }

        return writeIndex;
    }

    private static float GetRenderMode(BulletType type)
    {
        if (type == null) return BulletRenderData.DefaultRenderMode;
        if (BulletData.IsWarpZoneTypeName(type.typeName)) return BulletRenderData.WarpZoneRenderMode;
        if (BulletData.IsAttentionTypeName(type.typeName)) return BulletRenderData.AttentionRenderMode;
        return BulletRenderData.DefaultRenderMode;
    }

    private float GetBeatValueSin()
    {
        if (GManager.Control == null || GManager.Control.BManager == null)
        {
            return 0f;
        }

        return math.saturate(GManager.Control.BManager.BeatValueSin);
    }

    private int AppendRenderData(NativeArray<CounterBullet> bullets, int startIndex, int maxCount)
    {
        int writeIndex = startIndex;

        for (int i = 0; i < bullets.Length && writeIndex < maxCount; i++)
        {
            var b = bullets[i];
            if (!b.isActive) continue;

            float headSize = CounterBullet.GetSize(b.damage);
            float appear = 1f;
            int renderPriority = GetRenderPriority(CounterBullet.TypeId);
            float4 counterColor = GetCounterBulletColor(1f);
            renderArray[writeIndex] = new BulletRenderData
            {
                pos = b.position,
                angle = math.atan2(b.velocity.y, b.velocity.x),
                scale = new float2(headSize, headSize),
                texIndex = CounterBullet.TypeId,
                maskIndex = CounterBullet.TypeId,
                appear = appear,
                color = counterColor,
                renderPriority = renderPriority,
                renderMode = BulletRenderData.CounterBulletRenderMode,
            };
            writeIndex++;

            float2 previousPoint = b.position;
            for (int trailIndex = 0; trailIndex < b.trailCount && writeIndex < maxCount; trailIndex++)
            {
                if (!b.TryGetTrailPoint(trailIndex, out float2 currentPoint)) break;

                float2 segment = previousPoint - currentPoint;
                float segmentLength = math.length(segment);
                if (segmentLength <= 1e-4f)
                {
                    previousPoint = currentPoint;
                    continue;
                }

                float tNorm = (trailIndex + 1f) / (b.trailCount + 1f);
                float taper = math.pow(1f - tNorm, 0.45f);
                float fade = math.pow(1f - tNorm, 1.8f);
                float segmentSize = math.max(headSize * (0.85f * taper + 0.08f), segmentLength * 1.4f);
                float4 trailColor = GetCounterBulletColor(fade);
                renderArray[writeIndex] = new BulletRenderData
                {
                    pos = (previousPoint + currentPoint) * 0.5f,
                    angle = math.atan2(segment.y, segment.x),
                    scale = new float2(segmentSize, segmentSize),
                    texIndex = CounterBullet.TypeId,
                    maskIndex = CounterBullet.TypeId,
                    appear = fade,
                    color = trailColor,
                    renderPriority = renderPriority,
                    renderMode = BulletRenderData.CounterBulletRenderMode,
                };
                writeIndex++;
                previousPoint = currentPoint;
            }
        }

        return writeIndex;
    }

    private static float4 GetCounterBulletColor(float alphaMultiplier)
    {
        Color color = GManager.Control != null ? GManager.Control.playerColor : Color.white;
        return new float4(color.r, color.g, color.b, math.saturate(color.a * alphaMultiplier));
    }

    private void SortRenderData(int count)
    {
        if (count <= 1) return;

        int priorityCount = 0;
        for (int i = 0; i < count; i++)
        {
            int priority = renderArray[i].renderPriority;
            int insertIndex = 0;
            while (insertIndex < priorityCount && uniqueRenderPriorities[insertIndex] < priority)
            {
                insertIndex++;
            }

            if (insertIndex < priorityCount && uniqueRenderPriorities[insertIndex] == priority)
            {
                continue;
            }

            for (int j = priorityCount; j > insertIndex; j--)
            {
                uniqueRenderPriorities[j] = uniqueRenderPriorities[j - 1];
            }
            uniqueRenderPriorities[insertIndex] = priority;
            priorityCount++;
        }

        if (priorityCount <= 1) return;

        int writeIndex = 0;
        for (int priorityIndex = 0; priorityIndex < priorityCount; priorityIndex++)
        {
            int priority = uniqueRenderPriorities[priorityIndex];
            for (int i = 0; i < count; i++)
            {
                if (renderArray[i].renderPriority != priority) continue;
                sortedRenderArray[writeIndex] = renderArray[i];
                writeIndex++;
            }
        }

        Array.Copy(sortedRenderArray, renderArray, count);
    }

    private int GetRenderPriority(int typeId)
    {
        BulletTypeDataBase typeDataBase = GManager.Control != null ? GManager.Control.BTDB : null;
        if (typeDataBase == null || typeDataBase.types == null) return 0;
        if (typeId < 0 || typeId >= typeDataBase.types.Length) return 0;

        BulletType type = typeDataBase.types[typeId];
        return type != null ? type.renderPriority : 0;
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
    }
}

using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using System.Collections.Generic;

public class BulletClipManager
{
    private List<BulletBuffer> bulletBuffers = new List<BulletBuffer>();

    private class BulletBuffer
    {
        public string name;
        public List<BulletData> bullets;
        public bool isLaser;

        public BulletBuffer(string name, List<BulletData> bullets, bool isLaser = false)
        {
            this.name = name;
            this.bullets = bullets;
            this.isLaser = isLaser;
        }
    }

    public void Init()
    {
        bulletBuffers.Clear();
        bulletBuffers.AddRange(Rumia());
        bulletBuffers.Add(Line());
        bulletBuffers.Add(LineLaser());
        bulletBuffers.Add(Circle());
    }

    #region Bullet Clips
    private List<BulletBuffer> Rumia()
    {
        List<BulletBuffer> buffers = new List<BulletBuffer>();
        List<BulletData> ru0 = new List<BulletData>();
        List<BulletData> ru1 = new List<BulletData>();

        for (int i = 0; i < 16; i++)
        {
            BulletData b = new BulletData(
                new float2(0, 0),
                new float2(0, 0),
                4.2f + 0.25f * i,
                0,
                0,
                0,
                new float2(1, 0.14f * i - 0.56f),
                0,
                0,
                0,
                new float4(0, 0, 0, 0),
                1,
                new float4(0, 0, 0.5f, 1)
            );
            BulletData b1 = b;
            b1.speed -= 0.7f;
            BulletData b2 = b;
            b2.speed -= 1.4f;

            ru0.Add(b);
            ru0.Add(b1);
            ru0.Add(b2);

            BulletData b3 = new BulletData(
                new float2(0, 0),
                new float2(0, 0),
                4.2f + 0.25f * i,
                0,
                0,
                0,
                new float2(1, -0.14f * i + 0.56f),
                0,
                0,
                0,
                new float4(0, 0, 0, 0),
                1,
                new float4(0.1f, 0.4f, 0.6f, 1)
            );
            BulletData b4 = b3;
            b4.speed -= 0.7f;
            BulletData b5 = b3;
            b5.speed -= 1.4f;
            ru1.Add(b3);
            ru1.Add(b4);
            ru1.Add(b5);
        }

        buffers.Add(new BulletBuffer("Rumia_0", ru0));
        buffers.Add(new BulletBuffer("Rumia_1", ru1));
        return buffers;
    }

    private BulletBuffer Line()
    {
        List<BulletData> line = new List<BulletData>();
        for (int i = 0; i < 16; i++)
        {
            BulletData b = new BulletData(
                new float2(0, 0),
                new float2(0, 0),
                3 + 0.1f * i,
                0,
                0,
                0,
                new float2(1 + 0.1f * i, 0),
                0,
                0,
                0,
                new float4(0, 0, 0, 0),
                2,
                new float4(0.6f, 0, 0, 1)
            );
            line.Add(b);
        }

        return new BulletBuffer("Line", line);
    }

    private BulletBuffer LineLaser()
    {
        BulletData b = new BulletData(
            new float2(0, 0),
            new float2(0, 0),
            3,
            0,
            0,
            0,
            new float2(1, 0),
            0,
            0,
            0,
            new float4(0, 0, 0, 0),
            2,
            new float4(1, 0.5f, 0, 1)
        );

        return new BulletBuffer("LineLaser", new List<BulletData> { b }, true);
    }

    private BulletBuffer Circle()
    {
        List<BulletData> circle = new List<BulletData>();
        for (int i = 0; i < 16; i++)
        {
            BulletData b = new BulletData(
                new float2(0, 0),
                new float2(0, 0),
                0,
                0,
                0,
                0,
                new float2(1, 2 * math.PI / 16 * i),
                0,
                4,
                0,
                new float4(0, 0, 0, 0),
                2,
                new float4(1, 0.5f, 0, 1)
            );
            b.startPos = new(-3, 0);
            circle.Add(b);
        }

        return new BulletBuffer("Circle", circle);
    }
    #endregion

    public bool TryGetBulletClipIndex(string name, out int index)
    {
        index = -1;
        for (int i = 0; i < bulletBuffers.Count; i++)
        {
            if (bulletBuffers[i].name == name)
            {
                index = i;
                return true;
            }
        }
        return false;
    }

    public List<BulletData> GetBulletClip(
        int index,
        float2 pPos,
        float2 _vlc,
        float angle,
        out bool isLaser,
        float4 color = new float4(),
        float speedOverride = 0f,
        float sizeOverride = 0f,
        int laserCount = 1,
        float laserSpacing = 0f,
        float laserLengthOverride = 0f)
    {
        isLaser = false;
        if (bulletBuffers.Count == 0)
        {
            Debug.LogError("BulletClipManager is not initialized.");
            return default;
        }

        if (index >= 0 && index < bulletBuffers.Count)
        {
            isLaser = bulletBuffers[index].isLaser;
            List<BulletData> templateBullets = bulletBuffers[index].bullets;
            float angleRad = angle / 180f * math.PI;
            float4 tint = NormalizeColor(color);
            int parallelCount = isLaser ? math.max(1, laserCount) : 1;
            float2 parallelNormal = new float2(-math.sin(angleRad), math.cos(angleRad));
            float parallelCenter = (parallelCount - 1) * 0.5f;
            List<BulletData> bullets = new List<BulletData>(templateBullets.Count * parallelCount);

            for (int parallelIndex = 0; parallelIndex < parallelCount; parallelIndex++)
            {
                float2 spawnPos = pPos;
                if (isLaser && laserSpacing != 0f)
                {
                    spawnPos += parallelNormal * ((parallelIndex - parallelCenter) * laserSpacing);
                }

                for (int i = 0; i < templateBullets.Count; i++)
                {
                    BulletData source = ApplyOverrides(
                        templateBullets[i],
                        speedOverride,
                        sizeOverride,
                        isLaser ? laserLengthOverride : 0f
                    );
                    BulletData bullet = new BulletData(source, spawnPos, _vlc, angleRad + source.polarForm.y, tint);
                    bullet.startPos = source.startPos;
                    bullets.Add(bullet);
                }
            }

            return bullets;
        }
        else
        {
            Debug.LogError($"Bullet clip index out of range: {index}");
            return default;
        }
    }

    private static BulletData ApplyOverrides(BulletData source, float speedOverride, float sizeOverride, float laserLengthOverride)
    {
        if (speedOverride > 0f) source.speed = speedOverride;
        if (laserLengthOverride > 0f) source.size = laserLengthOverride;
        else if (sizeOverride > 0f) source.size = sizeOverride;
        return source;
    }

    private static float4 NormalizeColor(float4 color)
    {
        if (color.x == 0f && color.y == 0f && color.z == 0f && color.w == 0f)
        {
            return new float4(1f, 1f, 1f, 1f);
        }
        return color;
    }
}

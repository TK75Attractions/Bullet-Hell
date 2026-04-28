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
    }

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
                3,
                0,
                0,
                0,
                new float2(1 + 0.1f * i, 0.08f * i),
                0,
                0,
                0,
                new float4(0, 0, 0, 0),
                2,
                new float4(0.6f, 0, 0, 1)
            );
            line.Add(b);
        }

        return new BulletBuffer("Line", line, true);
    }

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

    public List<BulletData> GetBulletClip(int index, float angle, out bool isLaser)
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
            List<BulletData> bullets = bulletBuffers[index].bullets;

            for (int i = 0; i < bullets.Count; i++)
            {
                BulletData b = bullets[i];
                bullets[i] = new BulletData(b, b.position, angle / 180 * math.PI + b.polarForm.y, b.color);
            }

            return bullets;
        }
        else
        {
            Debug.LogError($"Bullet clip index out of range: {index}");
            return default;
        }
    }
}

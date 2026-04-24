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
        public NativeArray<BulletData> bullets;

        public BulletBuffer(string name, NativeArray<BulletData> bullets)
        {
            this.name = name;
            this.bullets = bullets;
        }
    }

    public void Init()
    {
        DisposeBulletBuffers();
        bulletBuffers = Rumia();
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

        NativeArray<BulletData> ru0Native = new NativeArray<BulletData>(ru0.ToArray(), Allocator.Persistent);
        NativeArray<BulletData> ru1Native = new NativeArray<BulletData>(ru1.ToArray(), Allocator.Persistent);
        buffers.Add(new BulletBuffer("Rumia_0", ru0Native));
        buffers.Add(new BulletBuffer("Rumia_1", ru1Native));
        return buffers;
    }

    // Returned NativeArray is a shared template owned by this manager; caller must not Dispose it.
    public bool TryGetBulletClip(string name, ref int index, out NativeArray<BulletData> bullets)
    {
        bullets = default;
        if (bulletBuffers.Count == 0)
        {
            Debug.LogError("BulletClipManager is not initialized.");
            return false;
        }

        if (index >= 0 && index < bulletBuffers.Count)
        {
            if (bulletBuffers[index].name == name)
            {
                bullets = bulletBuffers[index].bullets;
                return true;
            }

            index = -1;
        }

        for (int i = 0; i < bulletBuffers.Count; i++)
        {
            if (bulletBuffers[i].name == name)
            {
                index = i;
                bullets = bulletBuffers[i].bullets;
                return true;
            }
        }

        Debug.LogError($"Bullet clip '{name}' not found.");
        return false;
    }

    public NativeArray<BulletData> GetBulletClip(string name, ref int index)
    {
        if (TryGetBulletClip(name, ref index, out NativeArray<BulletData> bullets))
        {
            return bullets;
        }

        return default;
    }

    private void OnDestroy()
    {
        DisposeBulletBuffers();
    }

    private void DisposeBulletBuffers()
    {
        for (int i = 0; i < bulletBuffers.Count; i++)
        {
            if (bulletBuffers[i].bullets.IsCreated)
            {
                bulletBuffers[i].bullets.Dispose();
            }
        }

        bulletBuffers.Clear();
    }
}

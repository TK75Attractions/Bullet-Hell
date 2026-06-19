using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;
using System;

public class MultiBullet
{
    public int arrayIndex = 0;
    public bool isActive = false;
    private bool isReady = false;

    public BulletBufferEmission bulletEmission = new BulletBufferEmission();
    public List<BulletBufferEmission> bulletBufferTriggers = new List<BulletBufferEmission>();
    private List<BulletChache> bulletChaches = new List<BulletChache>();

    [Serializable]
    private class BulletChache
    {
        public List<int> indexes = new List<int>();
        public float time = 0;
        public int clipCount;

        public BulletChache(List<int> _ind, float _time, int _clipCount)
        {
            indexes = _ind;
            time = _time;
            clipCount = _clipCount;
        }
    }

    public float time = 0;


    public void Init(int index, MultiBulletSpawner spawner)
    {
        arrayIndex = index;
        time = 0f;
        isReady = false;

        bulletEmission = spawner.bulletEmission ?? new BulletBufferEmission();
        bulletBufferTriggers = spawner.bulletBufferTriggers ?? new List<BulletBufferEmission>();
        bulletChaches.Clear();

        isActive = true;
    }

    public void UpdateMultiBullet(float dt)
    {
        time += dt;

        if (isActive)
        {
            Shot(dt);
            UpdateChache(dt);
        }
    }

    private void Shot(float dt)
    {
        if (!isReady)
        {

        }
        else
        {
            List<int> emitted = EmitInitialBullets(dt);
            QueueNextBufferTrigger(emitted, 0);
        }
    }

    private List<int> EmitInitialBullets(float dt)
    {
        if (bulletEmission != null && bulletEmission.HasResolvedClip)
        {

        }

        return new List<int>();
    }

    private void UpdateChache(float dt)
    {
        UpdateBufferChache(dt);
    }

    private void UpdateBufferChache(float dt)
    {
        for (int i = bulletChaches.Count - 1; i >= 0; i--)
        {
            BulletChache chache = bulletChaches[i];
            chache.time -= dt;
            if (chache.time <= 0)
            {
                if (chache.clipCount >= 0 && chache.clipCount < bulletBufferTriggers.Count)
                {
                    BulletBufferEmission trigger = bulletBufferTriggers[chache.clipCount];
                    List<int> updatedIndexes = EmitFromCachedBullets(chache.indexes, trigger, dt);
                    int nextClip = chache.clipCount + 1;

                    if (updatedIndexes != null && updatedIndexes.Count > 0 && nextClip < bulletBufferTriggers.Count)
                    {
                        QueueNextBufferTrigger(updatedIndexes, nextClip);
                    }
                }
                bulletChaches.RemoveAt(i);
            }
        }
    }

    private List<int> EmitFromCachedBullets(List<int> indexes, BulletBufferEmission trigger, float dt)
    {
        List<int> emittedIndexes = new List<int>();
        if (indexes == null || trigger == null || !trigger.HasResolvedClip) return emittedIndexes;

        for (int i = 0; i < indexes.Count; i++)
        {
            int index = indexes[i];
            if (!GManager.Control.QOrder.TryGetEnemyBulletData(index, out BulletData source)) continue;
            if (!source.isActive || source.isClearing) continue;

            List<int> emitted = GManager.Control.QOrder.EmitBulletBuffer(trigger, source);
            if (emitted != null && emitted.Count > 0)
            {
                emittedIndexes.AddRange(emitted);
            }

            if (trigger.deactivateSource)
            {
                GManager.Control.QOrder.SetEnemyBulletActive(index, false);
            }
        }

        return emittedIndexes;
    }

    private void QueueNextBufferTrigger(List<int> indexes, int triggerIndex)
    {
        if (indexes == null || indexes.Count == 0) return;
        if (bulletBufferTriggers == null || triggerIndex < 0 || triggerIndex >= bulletBufferTriggers.Count) return;

        BulletChache chache = new BulletChache(indexes, bulletBufferTriggers[triggerIndex].time, triggerIndex);
        bulletChaches.Add(chache);
    }
}

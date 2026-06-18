using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;
using System;

public class MultiBullet : MonoBehaviour
{
    public int arrayIndex = 0;
    public Transform trans;
    private float interval = 0;
    private float startInterval = 1.6f;
    private int bulletCount = 0;
    public bool isActive = false;
    private bool isReady = false;
    private int count = 0;

    public BulletBufferEmission bulletEmission = new BulletBufferEmission();
    public List<BulletBufferEmission> bulletBufferTriggers = new List<BulletBufferEmission>();
    public BulletClip bulletClip = new BulletClip();
    public List<BulletChangeClip> bulletChangeClips = new List<BulletChangeClip>();
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
        count = 0;
        isReady = false;

        interval = spawner.bulletInterval;
        bulletCount = spawner.bulletCount;
        bulletEmission = spawner.bulletEmission ?? new BulletBufferEmission();
        bulletBufferTriggers = spawner.bulletBufferTriggers ?? new List<BulletBufferEmission>();
        bulletClip = spawner.bulletClip;
        bulletChangeClips = spawner.bulletChangeClips;
        bulletChaches.Clear();

        trans = transform;
        trans.localScale = new Vector3(spawner.orbit.scale.x, spawner.orbit.scale.y, 1);
        startInterval = spawner.bulletEmitTime;

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
            if (time > startInterval)
            {
                isReady = true;
                time = 0;
            }
        }
        else
        {
            if (time > interval)
            {
                time = 0;
                if (count < bulletCount)
                {
                    List<int> emitted = EmitInitialBullets(dt);
                    if (UsesBulletBufferTriggers())
                    {
                        QueueNextBufferTrigger(emitted, 0);
                    }
                    else
                    {
                        QueueNextLegacyChangeClip(emitted, 0);
                    }
                    count++;
                }
            }
        }
    }

    private List<int> EmitInitialBullets(float dt)
    {
        if (bulletEmission != null && bulletEmission.HasResolvedClip)
        {
            if (GManager.Control.QOrder.TryGetMultiBulletOrbitData(arrayIndex, out BulletData orbit))
            {
                return GManager.Control.QOrder.EmitBulletBuffer(bulletEmission, orbit, dt);
            }

            return new List<int>();
        }

        if (bulletClip.number > 0)
        {
            return GManager.Control.QOrder.EmitEnemyBullet(bulletClip, arrayIndex);
        }

        return new List<int>();
    }

    private void UpdateChache(float dt)
    {
        if (UsesBulletBufferTriggers())
        {
            UpdateBufferChache(dt);
            return;
        }

        UpdateLegacyChache(dt);
    }

    private bool UsesBulletBufferTriggers()
    {
        return bulletBufferTriggers != null && bulletBufferTriggers.Count > 0;
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

            List<int> emitted = GManager.Control.QOrder.EmitBulletBuffer(trigger, source, dt);
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

    private void QueueNextLegacyChangeClip(List<int> indexes, int clipIndex)
    {
        if (indexes == null || indexes.Count == 0) return;
        if (bulletChangeClips == null || clipIndex < 0 || clipIndex >= bulletChangeClips.Count) return;

        BulletChache chache = new BulletChache(indexes, bulletChangeClips[clipIndex].time, clipIndex);
        bulletChaches.Add(chache);
    }

    private void UpdateLegacyChache(float dt)
    {
        for (int i = bulletChaches.Count - 1; i >= 0; i--)
        {
            BulletChache chache = bulletChaches[i];
            chache.time -= dt;
            if (chache.time <= 0)
            {
                if (bulletChangeClips != null && chache.clipCount >= 0 && chache.clipCount < bulletChangeClips.Count)
                {
                    List<int> updatedIndexes = GManager.Control.QOrder.UpdateBulletData(chache.indexes, bulletChangeClips[chache.clipCount].clip);
                    int nextClip = chache.clipCount + 1;

                    if (updatedIndexes != null && updatedIndexes.Count > 0 && nextClip < bulletChangeClips.Count)
                    {
                        float nextTime = bulletChangeClips[chache.clipCount].time;
                        if (nextTime <= 0)
                        {
                            nextTime = bulletChangeClips[nextClip].time;
                        }

                        if (nextTime > 0)
                        {
                            BulletChache nextChache = new BulletChache(updatedIndexes, nextTime, nextClip);
                            bulletChaches.Add(nextChache);
                        }
                    }
                }
                bulletChaches.RemoveAt(i);
            }
        }
    }

    public void Destroy()
    {
        Destroy(this.gameObject);
    }
}

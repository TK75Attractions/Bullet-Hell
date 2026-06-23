using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;
using System;

public class MultiBullet
{
    public int arrayIndex = 0;
    public bool isActive = false;
    private bool hasEmittedInitial = false;
    private float2 pos;

    public BulletBufferEmission bulletEmission = new BulletBufferEmission();
    public List<BulletBufferEmission> bulletBufferTriggers = new List<BulletBufferEmission>();
    private List<BulletChache> bulletChaches = new List<BulletChache>();

    [Serializable]
    private class BulletChache
    {
        public List<ManagedBulletHandle> handles = new List<ManagedBulletHandle>();
        public float time = 0;
        public int clipCount;

        public BulletChache(List<ManagedBulletHandle> _handles, float _time, int _clipCount)
        {
            handles = _handles;
            time = _time;
            clipCount = _clipCount;
        }
    }

    public float time = 0;


    public void Init(int index, MultiBulletSpawner spawner)
    {
        arrayIndex = index;
        time = 0f;
        hasEmittedInitial = false;
        pos = spawner.pos;

        bulletEmission = spawner.bulletEmission ?? new BulletBufferEmission();
        bulletBufferTriggers = spawner.bulletBufferTriggers ?? new List<BulletBufferEmission>();
        bulletChaches.Clear();

        isActive = true;
    }

    public void UpdateMultiBullet(float dt)
    {
        time += dt;

        if (!isActive) return;

        Shot(dt);
        UpdateChache(dt);

        if (hasEmittedInitial && bulletChaches.Count == 0)
        {
            isActive = false;
        }
    }

    private void Shot(float dt)
    {
        if (hasEmittedInitial) return;

        hasEmittedInitial = true;
        List<ManagedBulletHandle> emitted = EmitInitialBullets(dt);
        QueueNextBufferTrigger(emitted, 0);
    }

    private List<ManagedBulletHandle> EmitInitialBullets(float dt)
    {
        if (bulletEmission != null && bulletEmission.HasResolvedClip)
        {
            BulletData source = default;
            source.position = pos;
            source.velocity = new float2(0f, 0f);
            source.angle = 0f;
            source.isActive = true;
            return GManager.Control.QOrder.EmitManagedBulletBuffer(bulletEmission, source, dt);
        }

        return new List<ManagedBulletHandle>();
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
                    List<ManagedBulletHandle> updatedHandles = EmitFromCachedBullets(chache.handles, trigger, dt);
                    int nextClip = chache.clipCount + 1;

                    if (updatedHandles != null && updatedHandles.Count > 0 && nextClip < bulletBufferTriggers.Count)
                    {
                        QueueNextBufferTrigger(updatedHandles, nextClip);
                    }
                }
                bulletChaches.RemoveAt(i);
            }
        }
    }

    private List<ManagedBulletHandle> EmitFromCachedBullets(List<ManagedBulletHandle> handles, BulletBufferEmission trigger, float dt)
    {
        List<ManagedBulletHandle> emittedHandles = new List<ManagedBulletHandle>();
        if (handles == null || trigger == null || !trigger.HasResolvedClip) return emittedHandles;

        if (trigger.applyBulletOrbit)
        {
            GManager.Control.QOrder.ApplyBulletOrbit(handles, trigger);
            return handles;
        }

        for (int i = 0; i < handles.Count; i++)
        {
            ManagedBulletHandle handle = handles[i];
            if (!GManager.Control.QOrder.TryGetManagedBulletData(handle, out BulletData source)) continue;
            if (!source.isActive || source.isClearing) continue;

            List<ManagedBulletHandle> emitted = GManager.Control.QOrder.EmitManagedBulletBuffer(trigger, source, dt);
            if (emitted != null && emitted.Count > 0) emittedHandles.AddRange(emitted);

            if (trigger.deactivateSource)
            {
                GManager.Control.QOrder.SetManagedBulletActive(handle, false);
            }
        }

        return emittedHandles;
    }

    private void QueueNextBufferTrigger(List<ManagedBulletHandle> handles, int triggerIndex)
    {
        if (handles == null || handles.Count == 0) return;
        if (bulletBufferTriggers == null || triggerIndex < 0 || triggerIndex >= bulletBufferTriggers.Count) return;

        BulletChache chache = new BulletChache(handles, bulletBufferTriggers[triggerIndex].time, triggerIndex);
        bulletChaches.Add(chache);
    }
}

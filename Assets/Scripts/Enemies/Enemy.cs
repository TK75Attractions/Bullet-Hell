using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;
using System;

public class Enemy : MonoBehaviour
{
    public readonly static float startInterval = 1.6f;
    public int id = 0;
    public int arrayIndex = 0;
    public Transform trans;
    private SpriteRenderer SR = null;
    private float interval = 0;
    private int BulletCount = 0;
    public bool isActive = false;
    private bool isReady = false;
    private int count = 0;

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


    public void Init(int index, EnemySpawner spawner)
    {
        id = spawner.id;
        arrayIndex = index;

        interval = spawner.interval;
        BulletCount = spawner.bulletCount;
        bulletClip = spawner.bulletClip;
        bulletChangeClips = spawner.bulletChangeClips;

        trans = transform;
        trans.localScale = new Vector3(spawner.orbit.size, spawner.orbit.size, 1);
        SR = GetComponent<SpriteRenderer>();
        SR.sprite = GManager.Control.EDB.GetSprite(spawner.id);

        isActive = true;
    }

    public void UpdateEnemy(float dt)
    {
        time += dt;

        if (isActive)
        {
            Shot();
            UpdateChache(dt);
        }
    }

    private void Shot()
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
                if (count < BulletCount)
                {
                    BulletChache chache = new BulletChache(GManager.Control.QOrder.EmitEnemyBullet(bulletClip, arrayIndex), bulletChangeClips[0].time, 0);
                    bulletChaches.Add(chache);
                    count++;
                }
            }
        }
    }

    private void UpdateChache(float dt)
    {
        for (int i = bulletChaches.Count - 1; i >= 0; i--)
        {
            BulletChache chache = bulletChaches[i];
            chache.time -= dt;
            if (chache.time <= 0)
            {
                GManager.Control.QOrder.UpdateBulletData(chache.indexes, bulletChangeClips[chache.clipCount].clip);
                bulletChaches.RemoveAt(i);
            }
        }
    }

    public void Destroy()
    {
        Destroy(this.gameObject);
    }
}

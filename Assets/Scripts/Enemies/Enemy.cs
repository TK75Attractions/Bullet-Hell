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

    [Serializable]
    private class BulletChache
    {
        public List<int> indexes = new List<int>();
        public int clipCount;

        BulletChache(int index)
        {
            indexes.Add(index);
            clipCount = 0;
        }
    }

    public float time = 0;


    public void Init(int index, EnemySpawner spawner)
    {
        id = spawner.id;
        arrayIndex = index;

        interval = spawner.bulletClip.interval;
        BulletCount = spawner.bulletClip.count;
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
        if (!isReady)
        {
            if(time > startInterval)
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
                if(count < BulletCount)
                {
                    Debug.Log("Enemy " + arrayIndex + " Emit Bullet");
                    GManager.Control.QOrder.EmitEnemyBullet(bulletClip, arrayIndex);
                    count++;
                }
            }
        }

    }

    public void Destroy()
    {
        Destroy(this.gameObject);
    }
}

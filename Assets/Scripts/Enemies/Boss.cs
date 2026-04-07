using UnityEngine;
using Unity.Mathematics;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;

public class Boss : MonoBehaviour
{
    public int bossId;
    public string bossName;
    public Sprite bossImage;

    private enum BossState
    {
        Idle,
        Attack,
        Move
    }

    [SerializeField] private BossState state;

    public float2 pos;
    public float2 distination;

    private bool ready = false;
    private float time;
    private float move;
    private int count;
    [SerializeField] private int repeat;
    [SerializeField] private List<BossBulletBuffer> bulletDatas = new List<BossBulletBuffer>();
    private List<NativeArray<BulletData>> bulletDatasNative = new List<NativeArray<BulletData>>();

    [System.Serializable]
    private class BossBulletBuffer
    {
        public List<BulletData> bullets = new List<BulletData>();
        public float spawnInterval;
    }

    public void Init()
    {
        time = 0;
        count = 0;
        state = BossState.Attack;

        bulletDatas[0].bullets.Clear();
        bulletDatas[1].bullets.Clear();
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
            bulletDatas[0].bullets.Add(b);
            bulletDatas[0].bullets.Add(b1);
            bulletDatas[0].bullets.Add(b2);

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
            bulletDatas[1].bullets.Add(b3);
            bulletDatas[1].bullets.Add(b4);
            bulletDatas[1].bullets.Add(b5);
        }

        bulletDatas[0].spawnInterval = 0.6f;
        bulletDatas[1].spawnInterval = 0.6f;

        for (int i = 0; i < bulletDatas.Count; i++)
        {
            NativeArray<BulletData> spawnBufferNative = new NativeArray<BulletData>(bulletDatas[i].bullets.Count, Allocator.Persistent);
            for (int j = 0; j < bulletDatas[i].bullets.Count; j++) spawnBufferNative[j] = bulletDatas[i].bullets[j];
            bulletDatasNative.Add(spawnBufferNative);
        }
    }

    public void UpdateBoss(float dt)
    {
        time += dt;

        if (ready == false && time > 3)
        {
            ready = true;
            time = 0;
        }
        if (!ready) return;

        //Attack
        if (time > bulletDatas[count % bulletDatas.Count].spawnInterval && state == BossState.Attack)
        {
            time -= bulletDatas[count % bulletDatas.Count].spawnInterval;
            GManager.Control.QOrder.AddEnemyHomingBullets(bulletDatasNative[count % bulletDatas.Count], pos);
            count++;

            if (count % repeat == 0)
            {
                state = BossState.Move;
                double t = GManager.Control.PRandom.Noise(count);
                float2 d = new float2(math.cos((float)t * 2 * math.PI), math.sin((float)t * 2 * math.PI)) * move;
                distination = pos + d;
                state = BossState.Move;
            }
            return;
        }

        /*
        if (time < 1 && state == BossState.Move)
        {
            pos = math.lerp(pos, distination, time);
        }
        else if (state == BossState.Move)
        {
            pos = distination;
            time = 0;
            state = BossState.Attack;
        }
        */
    }

    private void OnDestroy()
    {
        for (int i = 0; i < bulletDatasNative.Count; i++) bulletDatasNative[i].Dispose();
    }
}

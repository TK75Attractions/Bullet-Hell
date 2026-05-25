using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct BulletCollisionJob : IJobParallelFor
{
    private const float CrossEpsilon = 1e-5f;

    public NativeArray<BulletData> bullets;
    [ReadOnly]
    public NativeArray<float2> bVerts;
    [ReadOnly]
    public NativeArray<int2> bVertRanges;
    [ReadOnly]
    public NativeArray<float> bPowers;
    public float2 pPos;
    public bool isPlayerDash;
    public float grazeRange;

    [NativeDisableParallelForRestriction]
    public NativeArray<int> isCollided;

    [NativeDisableParallelForRestriction]
    public NativeArray<float> attackPower;

    public void Execute(int index)
    {
        BulletData bullet = bullets[index];
        if (isCollided[0] != 0 && !isPlayerDash) return;
        if (bullet.isClearing) return;
        if (!bullet.isActive) return;
        if (bullet.appearTime > bullet.time) return; // 弾が完全に表示される前は衝突判定を行わない
        if (bullet.typeId < 0 || bullet.typeId >= bVertRanges.Length) return;

        if (isPlayerDash)
        {
            if (bullet.unCounterable) return; // カウンター不可の弾はダッシュで消せない
            float2 dis = pPos - bullet.position;
            float distSq = math.dot(dis, dis);
            if (grazeRange > distSq)
            {
                float uniformScale = math.cmax(math.abs(bullet.scale));
                attackPower[0] += bPowers[bullet.typeId] * uniformScale;
                bullet.isActive = false; // 弾を消す
                bullets[index] = bullet; // 変更を反映
            }
        }
        else
        {
            int2 range = bVertRanges[bullet.typeId];
            if (range.x < 0 || range.y < 3) return;
            if (range.x >= bVerts.Length) return;
            if (range.x + range.y > bVerts.Length) return;

            float2 v = new float2(math.cos(bullet.angle), math.sin(bullet.angle));
            float2 n = new float2(-v.y, v.x);
            float2 dis = pPos - bullet.position;

            float px = math.dot(dis, v);
            float py = math.dot(dis, n);

            for (int i = 0; i < range.y; i++)
            {
                float2 absScale = math.abs(bullet.scale);
                float2 vert0 = bVerts[range.x + i] * absScale;
                float2 vert1 = bVerts[range.x + ((i + 1) % range.y)] * absScale;

                float d = (py - vert0.y) * (vert1.x - vert0.x) - (px - vert0.x) * (vert1.y - vert0.y);
                if (d < -CrossEpsilon) return; // 衝突していない
            }

            isCollided[0] = 1; // 衝突した
        }
    }
}

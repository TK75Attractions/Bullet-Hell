using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct BulletCollisionJob : IJobParallelFor
{
    private const float CrossEpsilon = 1e-5f;

    [ReadOnly]
    public NativeArray<BulletData> bullets;
    [ReadOnly]
    public NativeArray<float2> bVerts;
    [ReadOnly]
    public NativeArray<int2> bVertRanges;
    public float2 pPos;

    [NativeDisableParallelForRestriction]
    public NativeArray<int> isCollided;

    public void Execute(int index)
    {
        BulletData bullet = bullets[index];
        if (!bullet.isActive) return;
        if (isCollided[0] != 0) return;
        if (bullet.typeId < 0 || bullet.typeId >= bVertRanges.Length) return;

        int2 range = bVertRanges[bullet.typeId];
        if (range.x < 0 || range.y < 3) return;
        if (range.x >= bVerts.Length) return;
        if (range.x + range.y > bVerts.Length) return;

        float2 v = new float2(math.cos(bullet.angle), math.sin(bullet.angle));
        float2 n = new float2(-v.y, v.x);
        float2 dis = pPos - bullet.position;

        float px = math.dot(dis, v);
        float py = math.dot(dis, n);

        // 衝突判定のロジックをここに追加
        for (int i = 0; i < range.y; i++)
        {
            float2 vert0 = bVerts[range.x + i] * bullet.size;
            float2 vert1 = bVerts[range.x + ((i + 1) % range.y)] * bullet.size;

            float d = (py - vert0.y) * (vert1.x - vert0.x) - (px - vert0.x) * (vert1.y - vert0.y);
            if (d < -CrossEpsilon) return; // 衝突していない
        }

        isCollided[0] = 1; // 衝突した
    }
}

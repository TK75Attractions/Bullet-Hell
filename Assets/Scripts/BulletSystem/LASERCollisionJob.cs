using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]

public struct LASERCollisionJob : IJobParallelFor
{
    private const float CrossEpsilon = 1e-5f;
    public float2 pPos;

    [ReadOnly] public NativeArray<LASERCell> laserCells;

    [NativeDisableParallelForRestriction]
    public NativeArray<int> isCollided;

    public void Execute(int index)
    {
        if (isCollided[0] != 0) return;

        LASERCell cell = laserCells[index];
        float d = (pPos.y - cell.vert0.y) * (cell.vert1.x - cell.vert0.x) - (pPos.x - cell.vert0.x) * (cell.vert1.y - cell.vert0.y);
        if (d < -CrossEpsilon) return;
        d = (pPos.y - cell.vert1.y) * (cell.vert2.x - cell.vert1.x) - (pPos.x - cell.vert1.x) * (cell.vert2.y - cell.vert1.y);
        if (d < -CrossEpsilon) return;
        d = (pPos.y - cell.vert2.y) * (cell.vert0.x - cell.vert2.x) - (pPos.x - cell.vert2.x) * (cell.vert0.y - cell.vert2.y);
        if (d < -CrossEpsilon) return;

        
        isCollided[0] = 1;
    }
}

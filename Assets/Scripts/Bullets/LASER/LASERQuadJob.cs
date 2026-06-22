using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]

public struct LASERQuadJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float2> vertsSet;
    [WriteOnly] public NativeArray<int> vertCellIndices;
    public QuadGrid grid;

    public void Execute(int index)
    {
        float2 pos = vertsSet[index];
        vertCellIndices[index] = grid.GetTreeNum(pos);
    }
}

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]

public struct LASERQuadJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float2> vertsSet;
    [WriteOnly] public NativeArray<int> vertCellIndices;
    public float cellSize;
    public int cellCount;

    public void Execute(int index)
    {
        float2 pos = vertsSet[index];
        vertCellIndices[index] = GetTreeNum(pos);
    }

    private int GetTreeNum(float2 pos)
    {
        if (pos.x < 0f || pos.y < 0f) return -1;
        int nx = (int)math.floor(pos.x / cellSize);
        int ny = (int)math.floor(pos.y / cellSize);

        int result = BitSeparate32(nx) | (BitSeparate32(ny) << 1);
        if (result >= 0 && result < cellCount) return result;
        return -1;
    }

    private int BitSeparate32(int n)
    {
        n = (n | n << 8) & 0x00ff00ff;
        n = (n | n << 4) & 0x0f0f0f0f;
        n = (n | n << 2) & 0x33333333;
        return (n | n << 1) & 0x55555555;
    }
}

using Unity.Mathematics;

public struct QuadGrid
{
    public float2 origin;
    public float cellSize;
    public int gridResolution;
    public int cellCount;

    public QuadGrid(float2 origin, float cellSize, int gridResolution, int cellCount)
    {
        this.origin = origin;
        this.cellSize = cellSize;
        this.gridResolution = gridResolution;
        this.cellCount = cellCount;
    }

    public bool IsValid => cellSize > 0f && gridResolution > 0 && cellCount > 0;

    public int GetTreeNum(float2 worldPos)
    {
        if (!TryGetCellCoords(worldPos, out int2 cell)) return -1;
        return GetTreeNum(cell.x, cell.y);
    }

    public int GetTreeNum(int x, int y)
    {
        if (!IsValid) return -1;
        if (x < 0 || y < 0) return -1;
        if (x >= gridResolution || y >= gridResolution) return -1;

        int result = BitSeparate32(x) | (BitSeparate32(y) << 1);
        if (result >= 0 && result < cellCount) return result;
        return -1;
    }

    public bool TryGetCellCoords(float2 worldPos, out int2 cell)
    {
        cell = new int2(-1, -1);
        if (!IsValid) return false;

        float2 gridPos = WorldToGridPosition(worldPos);
        if (gridPos.x < 0f || gridPos.y < 0f) return false;

        int x = (int)math.floor(gridPos.x / cellSize);
        int y = (int)math.floor(gridPos.y / cellSize);
        if (x < 0 || y < 0 || x >= gridResolution || y >= gridResolution) return false;

        cell = new int2(x, y);
        return true;
    }

    public bool TryGetCellRange(float2 worldCenter, float radius, out int2 minCell, out int2 maxCell)
    {
        minCell = new int2(-1, -1);
        maxCell = new int2(-1, -1);
        if (!IsValid) return false;

        float2 minGridPos = WorldToGridPosition(worldCenter - new float2(radius, radius));
        float2 maxGridPos = WorldToGridPosition(worldCenter + new float2(radius, radius));

        int minX = (int)math.floor(minGridPos.x / cellSize);
        int maxX = (int)math.floor(maxGridPos.x / cellSize);
        int minY = (int)math.floor(minGridPos.y / cellSize);
        int maxY = (int)math.floor(maxGridPos.y / cellSize);

        if (maxX < 0 || maxY < 0 || minX >= gridResolution || minY >= gridResolution)
        {
            return false;
        }

        minCell = new int2(math.clamp(minX, 0, gridResolution - 1), math.clamp(minY, 0, gridResolution - 1));
        maxCell = new int2(math.clamp(maxX, 0, gridResolution - 1), math.clamp(maxY, 0, gridResolution - 1));
        return true;
    }

    public float2 WorldToGridPosition(float2 worldPos)
    {
        return worldPos - origin;
    }

    public static int BitSeparate32(int n)
    {
        n = (n | n << 8) & 0x00ff00ff;
        n = (n | n << 4) & 0x0f0f0f0f;
        n = (n | n << 2) & 0x33333333;
        return (n | n << 1) & 0x55555555;
    }

    public static int2 BitCompact32(int n)
    {
        int x = n & 0x55555555;
        int y = (n >> 1) & 0x55555555;

        x = (x | x >> 1) & 0x33333333;
        x = (x | x >> 2) & 0x0f0f0f0f;
        x = (x | x >> 4) & 0x00ff00ff;
        x = (x | x >> 8) & 0x0000ffff;

        y = (y | y >> 1) & 0x33333333;
        y = (y | y >> 2) & 0x0f0f0f0f;
        y = (y | y >> 4) & 0x00ff00ff;
        y = (y | y >> 8) & 0x0000ffff;

        return new int2(x, y);
    }
}

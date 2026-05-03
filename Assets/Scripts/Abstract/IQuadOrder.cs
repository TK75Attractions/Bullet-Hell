using Unity.Collections;
using System.Collections.Generic;
using Unity.Mathematics;

public interface IQuadOrder
{
    void UpdateLASERVerts(NativeList<float2> vertsSet, ref List<List<int>> quadVerts);
}
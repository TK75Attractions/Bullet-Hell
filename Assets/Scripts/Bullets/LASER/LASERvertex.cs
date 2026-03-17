using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public struct LASERvertex
{
    public float2 point;
    public float2 nutral;
    public float magnitude 
    {
        get 
        {
            if (nutral.x == 0 && nutral.y == 0) return 1;
            return math.sqrt(nutral.x * nutral.x + nutral.y * nutral.y); 
        }
    }// |(f'(x), -1)|

    public LASERvertex(float2 _p, float2 _n)
    {
        point = _p;
        nutral = _n;
    }
}

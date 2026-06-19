using System;
using System.Collections.Generic;
using Unity.Mathematics;

[Serializable]
public class MultiBulletSpawner
{
    public float2 pos = new(0, 0);
    public float time = 0f;
    public BulletBufferEmission bulletEmission = new BulletBufferEmission();
    public List<BulletBufferEmission> bulletBufferTriggers = new List<BulletBufferEmission>();
}

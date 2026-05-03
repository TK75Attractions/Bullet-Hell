using System;
using System.Collections.Generic;

[Serializable]
public class BulletChache
{
    public List<int> indexes = new List<int>();
    public float time = 0;
    public int clipCount;

    public BulletChache(List<int> _ind, float _time, int _clipCount)
    {
        indexes = _ind;
        time = _time;
        clipCount = _clipCount;
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BulletChangeClip
{
    public List<int> indexes = new List<int>();

    public BulletClip clip;

    public float time = 0;

    public float interval = 0;

    public BulletChangeClip(float _t, float _interval)
    {
        time = _t;
        interval = _interval;
    }
}
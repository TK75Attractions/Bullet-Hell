using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct BulletClip
{
    public BulletData data;
    public int number;
    public float interval;
    public int count;
    public float disRad;
    public bool homing;
    public int generateType;
}
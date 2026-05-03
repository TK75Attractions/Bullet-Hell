using System;
using UnityEngine;

[Serializable]
public class NativeBulletDebugEntry
{
    public int index;
    public bool isActive;
    public int areaNum;
    public int typeId;
    public float time;
    public float angle;
    public float size;
    public Vector2 position;
    public Vector2 velocity;
    public Vector2 originPos;
    public Vector2 originVlc;
    public Vector2 polarForm;
}
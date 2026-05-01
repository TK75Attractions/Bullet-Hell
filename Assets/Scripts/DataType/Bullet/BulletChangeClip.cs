using System;

[Serializable]
public class BulletChangeClip : IBulletChangeClip
{
    public BulletClip clip { get; private set; }

    public float time { get; private set; } = 0;

    public float interval { get; private set; } = 0;

    public BulletChangeClip(float _t, float _interval)
    {
        time = _t;
        interval = _interval;
    }
}
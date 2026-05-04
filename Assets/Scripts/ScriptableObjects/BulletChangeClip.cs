using System;

namespace BulletHell.Bullets
{
    [Serializable]
    public class BulletChangeClip : IBulletChangeClip
    {
        public BulletClip clip;

        public float time = 0;

        public float interval = 0;

        public BulletClip GetClip() => clip;
        public float GetTime() => time;
        public float GetInterval() => interval;

        public BulletChangeClip(float _t, float _interval)
        {
            time = _t;
            interval = _interval;
        }
    }
}
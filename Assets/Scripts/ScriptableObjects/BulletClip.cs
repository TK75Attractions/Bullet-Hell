using System;

namespace BulletHell.Bullets
{
    [Serializable]
    public struct BulletClip
    {
        public BulletData data;
        public int number;
        public float disRad;
        public bool homing;
        public int generateType;
    }
}
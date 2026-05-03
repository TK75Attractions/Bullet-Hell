using System;

namespace BulletHell.Bullets
{
    [Serializable]
    public struct BulletClip
    {
        public BulletData data { get; set; }
        public int number { get; set; }
        public float disRad { get; set; }
        public bool homing { get; set; }
        public int generateType { get; set; }
    }
}
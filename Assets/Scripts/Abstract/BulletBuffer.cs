using System.Collections.Generic;

namespace BulletHell.Bullets
{
    public class BulletBuffer
    {
        public string name;
        public List<BulletData> bullets;
        public bool isLaser;

        public BulletBuffer(string name, List<BulletData> bullets, bool isLaser = false)
        {
            this.name = name;
            this.bullets = bullets;
            this.isLaser = isLaser;
        }
    }
}
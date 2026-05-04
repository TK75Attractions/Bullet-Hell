using BulletHell.Bullets;
using UnityEngine;
namespace BulletHell.Database
{
    public class BulletTypeLoader
    {
        public BulletType[] LoadBulletTypes()
        {
            return Resources.LoadAll<BulletType>("Bullet/BulletTypes");
        }
    }
}
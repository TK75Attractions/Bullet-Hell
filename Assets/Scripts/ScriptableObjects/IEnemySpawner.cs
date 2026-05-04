using System.Collections.Generic;

using BulletHell.Bullets;

namespace BulletHell.Enemies
{
    public interface IEnemySpawner
    {
        int Getid();
        int Getcount();
        float GetInterval();
        float GetTime();
        int GetBulletCount();
        BulletData GetOrbit();
        BulletClip GetBulletClip();
        List<IBulletChangeClip> GetBulletChangeClips();
    }
}
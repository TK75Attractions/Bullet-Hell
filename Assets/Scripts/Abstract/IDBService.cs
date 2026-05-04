using BulletHell.Audio;
using BulletHell.Bullets;
using BulletHell.Enemies;
using BulletHell.Stages;

namespace BulletHell.Database
{
    public interface IDBService
    {
        public IBulletTypeDB BTDB { get; }

        public IStageDB SDB { get; }
        public ISoundEffectDB<ISEData> SEDB { get; }
        public IEnemyDB EDB { get; }
    }
}
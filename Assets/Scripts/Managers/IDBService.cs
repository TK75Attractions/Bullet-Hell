using BulletHell.Audio;
using BulletHell.Bullets;
using BulletHell.Enemies;
using BulletHell.Stages;

namespace BulletHell.Data
{
    public interface IDBService
    {
        public IBulletTypeDB BTDB { get; }

        public IStageDB<IStageData> SDB { get; }
        public ISoundEffectDB<ISEData> SEDB { get; }
        public IEnemyDB EDB { get; }
    }
}
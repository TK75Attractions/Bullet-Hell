using BulletHell.Enemies;
using BulletHell.Stages;
using BulletHell.Audio;
using BulletHell.Bullets;

namespace BulletHell.Database
{
    public class DBService : IDBService
    {
        public IBulletTypeDB BTDB { get; }

        public IStageDB SDB { get; }
        public ISoundEffectDB<ISEData> SEDB { get; }
        public IEnemyDB EDB { get; }

        public DBService(IBulletTypeDB bulletTypeDB, IStageDB stageDB, ISoundEffectDB<ISEData> seDB, IEnemyDB enemyDB)
        {
            BTDB = bulletTypeDB;
            SDB = stageDB;
            SEDB = seDB;
            EDB = enemyDB;
        }

        public void Init()
        {
            BTDB.Init();
            SDB.Init();
            EDB.Init();
            SEDB.Init();
        }
    }
}
public class DBService : IDBService
{
    public IBulletTypeDB BTDB { get; }

    public IStageDB<IStageData> SDB { get; }
    public ISoundEffectDB<ISEData> SEDB { get; }
    public IEnemyDB EDB { get; }

    public DBService(IBulletTypeDB bulletTypeDB, IStageDB<IStageData> stageDB, ISoundEffectDB<ISEData> seDB, IEnemyDB enemyDB)
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
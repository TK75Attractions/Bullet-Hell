using UnityEngine;

public class Installer : MonoBehaviour
{
    IDBService DBService;

    public void Awake()
    {
        var bulletTypeDB = ScriptableObject.CreateInstance<BulletTypeDataBase>();
        var stageDB = ScriptableObject.CreateInstance<StageDataBase>();
        var seDB = ScriptableObject.CreateInstance<SEDataBase>();
        var enemyDB = ScriptableObject.CreateInstance<EnemyDataBase>();

        DBService = new DBService(bulletTypeDB, stageDB, seDB, enemyDB);
        GManager.Control.DBService = DBService;
    }
}
    
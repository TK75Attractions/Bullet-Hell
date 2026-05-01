using UnityEngine;

public class Installer : MonoBehaviour
{
    IDBService DBService;
    AudioManager AManager;
    PlayerController PController;

    public GameObject PlayerObj;

    public void Awake()
    {
        var bulletTypeDB = ScriptableObject.CreateInstance<BulletTypeDataBase>();
        var stageDB = ScriptableObject.CreateInstance<StageDataBase>();
        var seDB = ScriptableObject.CreateInstance<SEDataBase>();
        var enemyDB = ScriptableObject.CreateInstance<EnemyDataBase>();

        DBService = new DBService(bulletTypeDB, stageDB, seDB, enemyDB);

        AManager = transform.parent.Find("AManager").GetComponent<AudioManager>();
        AManager.Init(seDB);

        PController = new PlayerController();
        GameObject ptemp = Instantiate(PlayerObj);
        PController.Init(ptemp);

        GManager.Control.Construct(DBService,AManager,PController);
    }
}
    
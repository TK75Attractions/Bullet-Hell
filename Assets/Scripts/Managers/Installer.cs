using UnityEngine;
using Unity.Collections;

public class Installer : MonoBehaviour
{
    IDBService DBService;
    AudioManager AManager;
    PlayerController PController;

    StageSelectManager SSManager;

    StageReader SReader;

    IGameStarter Starter;

    IInputService InputService;

    IQuadGrid QuadGrid;

    IQuadBulletStore QuadBulletStore;

    QuadOrder QuadOrder;

    public GameObject PlayerObj;

    public LaserEmitter LaserEmitter;

    public void Awake()
    {

        var bulletTypeDB = ScriptableObject.CreateInstance<BulletTypeDataBase>();
        var stageDB = ScriptableObject.CreateInstance<StageDataBase>();
        var seDB = ScriptableObject.CreateInstance<SEDataBase>();
        var enemyDB = ScriptableObject.CreateInstance<EnemyDataBase>();

        DBService = new DBService(bulletTypeDB, stageDB, seDB, enemyDB);

        AManager = transform.parent.Find("AManager").GetComponent<AudioManager>();
        AManager.Init(seDB);

        InputService = new InputService();

        PController = new PlayerController(InputService);
        GameObject ptemp = Instantiate(PlayerObj);
        PController.Init(ptemp);
        
        QuadGrid = new QuadGrid();
        QuadGrid.Init();

        QuadBulletStore = new QuadBulletStore();
        QuadBulletStore.Init();

        

        LaserEmitter = transform.parent.Find("GManager").GetComponent<LaserEmitter>();
        LaserEmitter.Init(PController);

        QuadOrder = transform.parent.Find("GManager").GetComponent<QuadOrder>();
        QuadOrder.Init(DBService,PController, QuadGrid,QuadBulletStore, LaserEmitter);
        QuadOrder.AwakeSetting();

        SReader = transform.parent.Find("GManager").GetComponent<StageReader>();
        SReader.Initialize(AManager);


        SSManager = transform.parent.Find("Canvases").Find("StageCanvas").Find("StageBoxParent").GetComponent<StageSelectManager>();
        Starter = GManager.Control.GetComponent<GManager>();
        SSManager.Init(DBService.SDB, Starter);
        
        GManager.Control.Construct(DBService,PController,SReader,SSManager, QuadOrder);
    }
}
    
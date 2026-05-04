using UnityEngine;
using BulletHell.Core;
using BulletHell.Audio;
using BulletHell.Bullets;
using BulletHell.Enemies;
using BulletHell.Stages;
using BulletHell.Database;
using BulletHell.Player;
using BulletHell.UI.StageSelect;
using BulletHell.Core.Services;

namespace BulletHell.App
{
    public class Installer : MonoBehaviour
    {
        IDBService DBService;
        AudioManager AManager;
        IBulletPaternProvider bulletPaternProvider;
        PlayerController PController;

        StageSelectManager SSManager;

        StageReader SReader;

        IGameStarter Starter;

        IInputService InputService;

        IGameStateService GameStateService;

        IQuadGrid QuadGrid;

        IQuadBulletStore QuadBulletStore;

        QuadOrder QuadOrder;

        GManager GManager;

        public GameObject PlayerObj;

        LaserEmitter LaserEmitter;
        BulletTypeDataBase bulletTypeDB;

        [SerializeField] StageDataBase stageDB;
        [SerializeField] SEDataBase seDB;
        [SerializeField] EnemyDataBase enemyDB;

        public void Awake()
        {
            bulletTypeDB = new BulletTypeDataBase(new BulletTypeLoader());
            bulletTypeDB.Init();
            stageDB.Init();
            if (seDB != null)seDB.Init();
            enemyDB.Init();

            DBService = new DBService(bulletTypeDB, stageDB, seDB, enemyDB);

            AManager = transform.parent.Find("AManager").GetComponent<AudioManager>();
            AManager.Init(seDB);

            bulletPaternProvider = new BulletBufferManager();
            bulletPaternProvider.Init();

            InputService = new InputService();

            GameStateService = transform.parent.Find("GManager").GetComponent<GManager>();
            
            QuadGrid = new QuadGrid();
            QuadGrid.Init();

            QuadBulletStore = new QuadBulletStore();
            QuadBulletStore.Init();

            PController = new PlayerController(InputService,QuadBulletStore);
            GameObject ptemp = Instantiate(PlayerObj);
            PController.Init(ptemp);

            LaserEmitter = transform.parent.Find("GManager").GetComponent<LaserEmitter>();
            LaserEmitter.Init(PController,QuadGrid);

            BulletCollisionService bulletCollisionService = new BulletCollisionService(DBService, QuadGrid);
            LaserCollisionService laserCollisionService = new LaserCollisionService(LaserEmitter,QuadGrid);

            QuadOrder = transform.parent.Find("GManager").GetComponent<QuadOrder>();
            QuadOrder.Init(DBService,PController, QuadGrid,QuadBulletStore, LaserEmitter, bulletPaternProvider, bulletCollisionService, laserCollisionService, GameStateService);
            
            
            QuadOrder.AwakeSetting();

            GManager = transform.parent.Find("GManager").GetComponent<GManager>();
            EnemyService EService = transform.parent.Find("GManager").GetComponent<EnemyService>();
            EService.Init(GManager.EnemyObj, QuadBulletStore, QuadOrder, DBService,  GameStateService);

            SReader = transform.parent.Find("GManager").GetComponent<StageReader>();
            SReader.Initialize(AManager,EService,QuadOrder,bulletPaternProvider);


            SSManager = transform.parent.Find("Canvases").Find("StageCanvas").Find("StageBoxParent").GetComponent<StageSelectManager>();
            Starter = transform.parent.Find("GManager").GetComponent<GManager>();
            SSManager.Init(DBService.SDB, Starter);
            
            GManager.Construct(DBService,PController,SReader,SSManager, QuadOrder,QuadBulletStore,EService);
        }
    }
}

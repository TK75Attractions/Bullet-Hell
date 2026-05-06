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
        public GameObject EnemyObj;
        IDBService DBService;
        AudioManager AManager;
        IBulletPaternProvider bulletPaternProvider;
        PlayerController PController;

        StageSelectManager SSManager;

        StageReader SReader;

        IGameStarter Starter;

        IInputService InputService;

        BulletRenderSystem BulletRenderSystem;

        IGameStateService GameStateService;

        IQuadGrid QuadGrid;

        IQuadBulletStore QuadBulletStore;

        QuadOrder QuadOrder;

        GManager GManager;

        public GameObject PlayerObj;

        LaserEmitter LaserEmitter;
        BulletTypeDataBase bulletTypeDB;
        BeatManager BManager;

        [SerializeField] StageDataBase stageDB;
        [SerializeField] SEDataBase seDB;
        EnemyDataBase enemyDB;
        IUserSettingService userSetting;

        public void Awake()
        {
            userSetting = new UserSettingService();
            UpdateService updateService = transform.parent.Find("GManager").GetComponent<UpdateService>();

            bulletTypeDB = new BulletTypeDataBase(new BulletTypeLoader());
            bulletTypeDB.Init();

            
            

            stageDB.Init();
            if (seDB != null) seDB.Init();

            enemyDB = new EnemyDataBase(new EnemyDataLoader());
            enemyDB.Init();

            DBService = new DBService(bulletTypeDB, stageDB, seDB, enemyDB);

            AManager = transform.parent.Find("AManager").GetComponent<AudioManager>();
            AManager.Init(seDB);

            BManager = transform.parent.Find("BManager").GetComponent<BeatManager>();
            BManager.Init(userSetting);

            bulletPaternProvider = new BulletBufferManager();
            bulletPaternProvider.Init();

            InputService = new InputService();
            InputService.Init();
            

            GameStateService = transform.parent.Find("GManager").GetComponent<GManager>();
            
            QuadGrid = new QuadGrid();
            QuadGrid.Init();

            QuadBulletStore = new QuadBulletStore();
            QuadBulletStore.Init();

            BulletRenderSystem = transform.parent.Find("GManager").GetComponent<BulletRenderSystem>();
            BulletRenderSystem.Init(bulletTypeDB,QuadBulletStore);


            GameObject temp = Instantiate(PlayerObj);
            PController = new PlayerController(InputService,QuadBulletStore,temp);

            LaserEmitter = transform.parent.Find("GManager").GetComponent<LaserEmitter>();
            LaserEmitter.Init(PController,QuadGrid);

            BulletCollisionService bulletCollisionService = new BulletCollisionService(DBService, QuadGrid);
            LaserCollisionService laserCollisionService = new LaserCollisionService(LaserEmitter,QuadGrid);
            BulletUpdateService bulletUpdateService = new();

            QuadOrder = transform.parent.Find("GManager").GetComponent<QuadOrder>();
            QuadOrder.Init(DBService,PController, QuadGrid,QuadBulletStore,LaserEmitter, bulletPaternProvider, bulletCollisionService, laserCollisionService, GameStateService, bulletUpdateService);
            
            
            QuadOrder.AwakeSetting();

            GManager = transform.parent.Find("GManager").GetComponent<GManager>();
            EnemyService EService = transform.parent.Find("GManager").GetComponent<EnemyService>();
            EService.Init(EnemyObj, QuadBulletStore, QuadOrder, DBService,  GameStateService);

            SReader = transform.parent.Find("GManager").GetComponent<StageReader>();
            SReader.Initialize(AManager,EService,QuadOrder,bulletPaternProvider,BManager,userSetting);


            SSManager = transform.parent.Find("Canvases").Find("StageCanvas").Find("StageBoxParent").GetComponent<StageSelectManager>();
            Starter = transform.parent.Find("GManager").GetComponent<GManager>();
            SSManager.Init(DBService.SDB, Starter, InputService);

            updateService.Register(QuadOrder);
            updateService.Register(InputService);
            updateService.Register(EService);
            updateService.Register(GManager);
            updateService.Register(PController);
            updateService.Register(SReader);
            updateService.Register(SSManager);
            updateService.Register(BManager);

            updateService.LateRegister(BulletRenderSystem);
        
            GManager.Construct(DBService,SReader, QuadOrder,InputService);

            updateService.SetReady();
        }
    }
}

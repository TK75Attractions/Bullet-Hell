using UnityEngine;
using BulletHell.Core;
using BulletHell.Audio;
using BulletHell.Bullets;
using BulletHell.Enemies;
using BulletHell.Stages;
using BulletHell.Data;
using BulletHell.Player;
using BulletHell.UI.StageSelect;
using BulletHell.Core.Services;

namespace BulletHell.App
{
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

        GManager GManager;

        public GameObject PlayerObj;

        public LaserEmitter LaserEmitter;

        [SerializeField] BulletTypeDataBase bulletTypeDB;
        [SerializeField] StageDataBase stageDB;
        [SerializeField] SEDataBase seDB;
        [SerializeField] EnemyDataBase enemyDB;

        public void Awake()
        {

            bulletTypeDB.Init();
            stageDB.Init();
            seDB.Init();
            enemyDB.Init();

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
            LaserEmitter.Init(PController,QuadGrid);

            QuadOrder = transform.parent.Find("GManager").GetComponent<QuadOrder>();
            QuadOrder.Init(DBService,PController, QuadGrid,QuadBulletStore, LaserEmitter);
            
            
            QuadOrder.AwakeSetting();

            

            SReader = transform.parent.Find("GManager").GetComponent<StageReader>();
            SReader.Initialize(AManager);


            SSManager = transform.parent.Find("Canvases").Find("StageCanvas").Find("StageBoxParent").GetComponent<StageSelectManager>();
            GManager = transform.parent.Find("GManager").GetComponent<GManager>();
            Starter = transform.parent.Find("GManager").GetComponent<GManager>();
            SSManager.Init(DBService.SDB, Starter);
            
            GManager.Construct(DBService,PController,SReader,SSManager, QuadOrder);
        }
    }
}
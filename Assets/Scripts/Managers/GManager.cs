using System;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.InputSystem;

using BulletHell.Bullets;
using BulletHell.Database;
using BulletHell.Core;
using BulletHell.Core.Services;
using BulletHell.Stages;

namespace BulletHell.App
{
    public class GManager : MonoBehaviour,IGameStarter,IGameStateService,IUpdatable
    {

        public IDBService DBService { get; private set; }

        public GameState state { get; set; } = GameState.Title;

        private IInputService IManager;
        private StageReader SReader;
        private IQuadOrder QOrder;

        private float beatTime;
        public bool ready = false;

        private readonly BulletData[] spawnBuffer = new BulletData[6];

        public BulletEvent testEvent = new BulletEvent();

        public void Construct(
            IDBService dbService,
            StageReader stageReader,
            IQuadOrder quadOrder,
            IInputService inputService
            )
        {
            DBService = dbService;
            SReader = stageReader;
            QOrder = quadOrder;
            IManager = inputService;

            ready = true;
        }

        public void Awake()
        {   
            InitSpawnBuffer();
            state = GameState.Title;
        }

        private void InitSpawnBuffer()
        {
            spawnBuffer[0] = new BulletData(new float2(5, 5), new float2(1, 0), 2f, 0, 0, 0, new float2(1, 0), 0, 2, 0, new float4(1, 0, 0, 0), 0, new float4(1, 0, 0, 1));
            spawnBuffer[1] = new BulletData(new float2(5, 5), new float2(1, 0), 2f, 0, 0, 0, new float2(1, math.PI / 3), 0, 2, 0, new float4(1, 0, 0, 0), 0, new float4(1, 0, 0, 1));
            spawnBuffer[2] = new BulletData(new float2(5, 5), new float2(1, 0), 2f, 0, 0, 0, new float2(1, 2 * math.PI / 3), 0, 2, 0, new float4(1, 0, 0, 0), 0, new float4(1, 0, 0, 1));
            spawnBuffer[3] = new BulletData(new float2(5, 5), new float2(1, 0), 2f, 0, 0, 0, new float2(1, 3 * math.PI / 3), 0, 2, 0, new float4(1, 0, 0, 0), 0, new float4(1, 0, 0, 1));
            spawnBuffer[4] = new BulletData(new float2(5, 5), new float2(1, 0), 2f, 0, 0, 0, new float2(1, 4 * math.PI / 3), 0, 2, 0, new float4(1, 0, 0, 0), 0, new float4(1, 0, 0, 1));
            spawnBuffer[5] = new BulletData(new float2(5, 5), new float2(1, 0), 2f, 0, 0, 0, new float2(1, 5 * math.PI / 3), 0, 2, 0, new float4(1, 0, 0, 0), 0, new float4(1, 0, 0, 1));
        }

        public void Tick(float dt)
        {

            if (Keyboard.current != null && Keyboard.current.aKey.wasPressedThisFrame)
            {
                /*
                Debug.Log("aaa");
                NativeArray<BulletData> tempBullets = new NativeArray<BulletData>(
                    spawnBuffer,
                    Allocator.TempJob
                );
                List<int> indexes = QOrder.AddEnemyBullets(tempBullets);
                foreach (int index in indexes)
                {
                    Debug.Log($"Spawned Bullet at index: {index}");
                }
                tempBullets.Dispose();
                */
                QOrder.StartBulletEvent(testEvent);
            }

            if (IManager.buttonPressed && state == GameState.Title)
            {
                state = GameState.ChoosingStage;
                // Transition to stage selection screen here
            }

            if (Keyboard.current != null && Keyboard.current.sKey.wasPressedThisFrame)
            {
                Debug.Log($"{beatTime}");
            }
        }


        float GetAngleDeg(float x, float y)
        {
            double rad = Math.Atan2(y, x);
            double deg = rad * 180.0 / Math.PI;

            if (deg < 0) deg += 360.0;
            return (float)deg;
        }

        public async void GoGame(int index)
        {
            StageData stage = DBService.SDB.GetStage(index);
            if (stage != null)
            {
                await SReader.Init(stage);
                state = GameState.Playing;
                Debug.Log($"Started Stage: {stage.stageName}");
            }
            else
            {
                Debug.LogError($"Stage with index {index} not found!");
            }
        }
    }
}
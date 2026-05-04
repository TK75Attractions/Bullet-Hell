using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using BulletHell.Core;
using BulletHell.Bullets;
using BulletHell.Database;


namespace BulletHell.Enemies
{
    public class EnemyService : MonoBehaviour, IEnemyService
    {
        private GameObject EnemyObj; 
        private IGameStateService state; //修正対象

        private List<IEnemy> enemies = new();
        private IQuadBulletStore quadBulletStore;
        private IQuadOrder quadOrder;
        private IDBService DBService;

        public void Init(
            GameObject enemyObj,
            IQuadBulletStore _quadBulletStore,
            IQuadOrder quadOrder,
            IDBService _DBService,
            IGameStateService state
            )
        {
            EnemyObj = enemyObj;
            quadBulletStore = _quadBulletStore;
            this.quadOrder = quadOrder;
            DBService = _DBService;
            this.state = state;
        }

        public async void AddEnemy(IEnemySpawner spawner)
        {
            float t = 0;

            for (int i = 0; i < spawner.count; i++)
            {
                while (t < spawner.interval * i)
                {
                    await Task.Yield();
                    t += Time.deltaTime;
                    if (state.state != GameState.Playing) return;
                }

                Enemy enemy = Instantiate(EnemyObj).GetComponent<Enemy>();
                enemy.Init(enemies.Count, quadOrder, spawner, DBService.EDB);
                enemies.Add(enemy);
                //Debug.Log($"Spawned enemy: {spawner.orbit.speed}");
                quadBulletStore.AddEnemiesOrbitBullet(spawner.orbit);
            }
        }

        public void UpdateEnemy(float dt)
        {
            if (state.state != GameState.Playing) return;
            
            UpdateEnemyPos(dt);
        }

        private void UpdateEnemyPos(float dt)
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemyBulletData = quadBulletStore.GetEnemyOrbitsBulletData(i);
                enemies[i].trans.position = new Vector3(enemyBulletData.position.x, enemyBulletData.position.y, 0);
                enemies[i].trans.rotation = Quaternion.Euler(0, 0, enemyBulletData.angle * Mathf.Rad2Deg);
                enemies[i].UpdateEnemy(dt);
            }
        }
    }
}

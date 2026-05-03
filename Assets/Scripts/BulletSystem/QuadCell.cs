using System;
using System.Collections.Generic;

using BulletHell.Enemies;

namespace BulletHell.Bullets
{
[Serializable]
public class QuadCell
{
    public List<IEnemy<IEnemyDB>> enemies = new List<IEnemy<IEnemyDB>>();
    public List<BulletData> enemyBullets = new List<BulletData>();
    public List<BulletData> playerBullets = new List<BulletData>();
    public void AddEnemy(IEnemy<IEnemyDB> enemy) => enemies.Add(enemy);
    public void ClearEnemies() => enemies.Clear();

    public void ClearAllBullets()
    {
        enemyBullets.Clear();
        playerBullets.Clear();
    }

    public void AddEnemyBullet(BulletData bullet) => enemyBullets.Add(bullet);
    public void ClearEnemyBullets() => enemyBullets.Clear();
    public int GetEnemyBulletCount() => enemyBullets.Count;
    public List<BulletData> GetEnemyBullets() => enemyBullets;
    public void AddPlayerBullet(BulletData bullet) => playerBullets.Add(bullet);
    public void ClearPlayerBullets() => playerBullets.Clear();
    public int GetPlayerBulletCount() => playerBullets.Count;
    public List<BulletData> GetPlayerBullets() => playerBullets;
}
}
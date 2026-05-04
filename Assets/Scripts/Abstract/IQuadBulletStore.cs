using System;
using Unity.Collections;
using Unity.Mathematics;
using System.Collections.Generic;

namespace BulletHell.Bullets
{
    public interface IQuadBulletStore : IDisposable
    {
        NativeArray<BulletData> playerBullets { get; }
        NativeArray<BulletData> enemyBullets { get; }
        NativeArray<BulletData> enemiesOrbitBullets { get; }

        #region GetCount Methods
        int GetPlayerBulletCount();
        int GetEnemyBulletCount();
        int GetEnemiesOrbitBulletCount();

        #endregion

        #region Bullets Return Methods
        NativeArray<BulletData> GetPlayerBullets();
        NativeArray<BulletData> GetEnemyBullets();

        #endregion

        #region Bullet Return Methods
        BulletData GetEnemyBulletData(int index);
        (float2 position, float angle) GetEnemyOrbitsBulletData(int index);
        #endregion


        #region Bullet Addition Methods

        void AddPlayerBullets(NativeArray<BulletData> newBullets);
        List<int> AddEnemyBullets(NativeArray<BulletData> newBullets, float2 fromPos);
        List<int> AddEnemyHomingBullets(NativeArray<BulletData> newBullets, float2 fromPos, float2 playerPos);
        void AddEnemyBulletsBySpawner(NativeArray<BulletData> newBullets);
        void AddEnemiesOrbitBullet(BulletData newBullet);
        #endregion

        List<int> EmitEnemyBullet(BulletClip clip, float2 pPos, float2 playerPos);

        List<int> UpdateBulletData(List<int> indexes, BulletClip clip, float2 playerPos);
        
        void Init(int capacity = 256);
    }
}
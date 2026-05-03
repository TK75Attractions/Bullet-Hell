using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using System.Collections.Generic;

using BulletHell.Enemies;

namespace BulletHell.Bullets
{
    public interface IQuadGrid
    {
        QuadCell[] cells { get; set; }

        float cellSize { get; set; }
        int separateLevel { get; set; }

        int cellCount { get; set; }

        void Init();

        int GetTreeNum(float2 pos);

        int GetTreeNum(int x, int y);

        int BitSeparate32(int n);

        Vector2Int BitCompact32(int n);

        void ClearAllCells();

        void RebuildCellsFromBullets(
            NativeArray<BulletData> playerBullets,
            NativeArray<BulletData> enemyBullets,
            NativeArray<BulletData> enemiesOrbitBullets,
            List<IEnemy<IEnemyDB>> enemies
        );
    }
}
using System;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using System.Collections.Generic;

using BulletHell.Enemies;

namespace BulletHell.Bullets
{
public class QuadGrid : IQuadGrid
{
    public QuadCell[] cells { get; set; } = Array.Empty<QuadCell>();

    public float cellSize { get; set; } = 0.8f;
    public int separateLevel { get; set; } = 6;

    public int cellCount { get; set; }

    public void Init()
    {
        
        int n = 1;
        for (int i = 0; i < separateLevel; i++) n *= 2;
        n = n * n;
        cellCount = n;
        QuadCell[] t = new QuadCell[n];
        for (int i = 0; i < n; i++) t[i] = new();
        cells = t;

    }

    public int GetTreeNum(float2 pos)
    {
        if (pos.x < 0 || pos.y < 0) return -1;
        int nx = Mathf.FloorToInt(pos.x / cellSize);
        int ny = Mathf.FloorToInt(pos.y / cellSize);

        int result = BitSeparate32(nx) | (BitSeparate32(ny) << 1);
        if (result >= 0 && result < cells.Length) return result;
        return -1;
    }

    public int GetTreeNum(int x, int y)
    {
        if (x < 0 || y < 0) return -1;
        return BitSeparate32(x) | (BitSeparate32(y) << 1);
    }

    public int BitSeparate32(int n)
    {
        n = (n | n << 8) & 0x00ff00ff;
        n = (n | n << 4) & 0x0f0f0f0f;
        n = (n | n << 2) & 0x33333333;
        return (n | n << 1) & 0x55555555;
    }

    public Vector2Int BitCompact32(int n)
    {
        int y = (n >> 1);
        n = (n & 0x55555555);               // 1ビットおきに残す
        n = (n | n >> 1) & 0x33333333;      // 2ビットおきに集約
        n = (n | n >> 2) & 0x0f0f0f0f;      // 4ビットおきに集約
        n = (n | n >> 4) & 0x00ff00ff;      // 8ビットおきに集約
        n = (n | n >> 8) & 0x0000ffff;      // 16ビットおきに集約


        y = (y & 0x55555555);               // 1ビットおきに残す
        y = (y | y >> 1) & 0x33333333;      // 2ビットおきに集約
        y = (y | y >> 2) & 0x0f0f0f0f;      // 4ビットおきに集約
        y = (y | y >> 4) & 0x00ff00ff;      // 8ビットおきに集約
        y = (y | y >> 8) & 0x0000ffff;      // 16ビットおきに集約

        return new Vector2Int(n, y);
    }

    public void ClearAllCells()
    {
        for (int i = 0; i < cells.Length; i++)
        {
            cells[i].ClearAllBullets();
        }
    }

    public void RebuildCellsFromBullets(
        NativeArray<BulletData> playerBullets,
        NativeArray<BulletData> enemyBullets,
        NativeArray<BulletData> enemiesOrbitBullets,
        List<IEnemy<IEnemyDB>> enemies
    )
    {
        ClearAllCells();

        if (enemyBullets.IsCreated)
        {
            for (int i = 0; i < enemyBullets.Length; i++)
            {
                BulletData bullet = enemyBullets[i];
                if (!bullet.isActive) continue;
                int areaNum = bullet.areaNum;
                if (areaNum < 0 || areaNum >= cells.Length) continue;
                cells[areaNum].enemyBullets.Add(bullet);
            }
        }

        if (playerBullets.IsCreated)
        {
            for (int i = 0; i < playerBullets.Length; i++)
            {
                BulletData bullet = playerBullets[i];
                if (!bullet.isActive) continue;
                int areaNum = bullet.areaNum;
                if (areaNum < 0 || areaNum >= cells.Length) continue;
                cells[areaNum].playerBullets.Add(bullet);
            }
        }

        if (enemiesOrbitBullets.IsCreated)
        {
            for (int i = 0; i < enemiesOrbitBullets.Length; i++)
            {
                BulletData bullet = enemiesOrbitBullets[i];
                if (!bullet.isActive) continue;
                int areaNum = bullet.areaNum;
                if (areaNum < 0 || areaNum >= cells.Length) continue;
                cells[areaNum].enemies.Add(enemies[i]);
            }
        }
    }
}
}
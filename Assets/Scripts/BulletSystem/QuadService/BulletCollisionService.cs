using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using System.Collections.Generic;
using Unity.Mathematics;
using System;

using BulletHell.Data;

namespace BulletHell.Bullets
{
    public class BulletCollisionService : IDisposable
    {
        private IQuadGrid grid;
        private IDBService DBService;
        private bool collisionDataDirty = true;

        private NativeArray<float2> collisionVerts;
        private NativeArray<int2> collisionVertRanges;
        private NativeArray<int> collisionHitFlag;
        private NativeList<BulletData> collisionCheckBullets;
        private List<int> collisionCheckCells = new List<int>(9);

        private List<Vector2Int> cellOffsets = new List<Vector2Int>()
        {
            new Vector2Int(0, 0),
            new Vector2Int(1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(1, 1),
            new Vector2Int(-1, 0),
            new Vector2Int(0, -1),
            new Vector2Int(-1, -1),
            new Vector2Int(1, -1),
            new Vector2Int(-1, 1)
        };

        public BulletCollisionService(IDBService dbService, IQuadGrid grid)
        {
            DBService = dbService;
            this.grid = grid;
        }

        public void Init()
        {
            if (!collisionHitFlag.IsCreated)
            {
                collisionHitFlag = new NativeArray<int>(1, Allocator.Persistent);
            }
            if (!collisionCheckBullets.IsCreated)
            {
                collisionCheckBullets = new NativeList<BulletData>(256, Allocator.Persistent);
            }
        }


        public void BuildCollisionData()
        {
            if (collisionVerts.IsCreated) collisionVerts.Dispose();
            if (collisionVertRanges.IsCreated) collisionVertRanges.Dispose();

            var bulletTypeDB = DBService.BTDB;
            if (bulletTypeDB == null || bulletTypeDB.GetTypes() == null)
            {
                collisionVerts = new NativeArray<float2>(0, Allocator.Persistent);
                collisionVertRanges = new NativeArray<int2>(0, Allocator.Persistent);
                collisionDataDirty = false;
                return;
            }

            int typeCount = bulletTypeDB.GetTypes().Length;
            List<float2[]> vertsByType = bulletTypeDB.bVerts;

            int totalVertCount = 0;
            for (int i = 0; i < typeCount; i++)
            {
                if (vertsByType != null && i < vertsByType.Count && vertsByType[i] != null)
                {
                    totalVertCount += vertsByType[i].Length;
                }
            }

            collisionVertRanges = new NativeArray<int2>(typeCount, Allocator.Persistent);
            collisionVerts = new NativeArray<float2>(totalVertCount, Allocator.Persistent);

            int offset = 0;
            for (int i = 0; i < typeCount; i++)
            {
                float2[] verts = (vertsByType != null && i < vertsByType.Count) ? vertsByType[i] : null;
                int length = verts != null ? verts.Length : 0;
                collisionVertRanges[i] = new int2(offset, length);

                for (int j = 0; j < length; j++)
                {
                    collisionVerts[offset + j] = verts[j];
                }

                offset += length;
            }

            collisionDataDirty = false;
        }

        public void CheckCollisionWithEnemy(float2 pPos)
        {
            if (collisionDataDirty || !collisionVerts.IsCreated || !collisionVertRanges.IsCreated) BuildCollisionData();
            if (!collisionHitFlag.IsCreated) collisionHitFlag = new NativeArray<int>(1, Allocator.Persistent);
            if (!collisionCheckBullets.IsCreated) collisionCheckBullets = new NativeList<BulletData>(256, Allocator.Persistent);

            Vector2Int pCell = grid.BitCompact32(grid.GetTreeNum(pPos));
            collisionCheckCells.Clear();
            int bulletCount = 0;
            foreach (var cell in cellOffsets)
            {
                Vector2Int checkCell = pCell + cell;
                int treeNum = grid.GetTreeNum(checkCell.x, checkCell.y);
                if (treeNum < 0 || treeNum >= grid.cells.Length) continue;
                collisionCheckCells.Add(treeNum);
                bulletCount += grid.cells[treeNum].enemyBullets.Count;
            }

            if (bulletCount == 0) return;

            collisionCheckBullets.Clear();
            if (collisionCheckBullets.Capacity < bulletCount)
            {
                collisionCheckBullets.Capacity = bulletCount;
            }

            for (int i = 0; i < collisionCheckCells.Count; i++)
            {
                int treeNum = collisionCheckCells[i];
                for (int j = 0; j < grid.cells[treeNum].enemyBullets.Count; j++)
                {
                    collisionCheckBullets.Add(grid.cells[treeNum].enemyBullets[j]);
                }
            }

            NativeArray<BulletData> checkBullets = collisionCheckBullets.AsArray();
            collisionHitFlag[0] = 0;

            BulletCollisionJob collisionJob = new()
            {
                bullets = checkBullets,
                bVerts = collisionVerts,
                bVertRanges = collisionVertRanges,
                pPos = pPos,
                isCollided = collisionHitFlag
            };

            JobHandle handle = collisionJob.Schedule(checkBullets.Length, 64);
            handle.Complete();
        }

        public void MarkCollisionDataDirty()
        {
            collisionDataDirty = true;
        }

        public void Dispose()
        {
            if (collisionVerts.IsCreated) collisionVerts.Dispose();
            if (collisionVertRanges.IsCreated) collisionVertRanges.Dispose();
            if (collisionHitFlag.IsCreated) collisionHitFlag.Dispose();
            if (collisionCheckBullets.IsCreated) collisionCheckBullets.Dispose();
        }
    }
}

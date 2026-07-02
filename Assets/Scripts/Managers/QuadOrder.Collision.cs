using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

// S7 QuadOrder split (delegation-only): player/bullet collision, the cached
// collision-vertex/power tables, broadphase cell registration, and counter
// bullets. All backing NativeArrays/lists remain owned by the core QuadOrder
// partial; nothing here changes data ownership, the public API, or the logic.
public partial class QuadOrder
{
    private void BuildCollisionData()
    {
        if (collisionVerts.IsCreated) collisionVerts.Dispose();
        if (collisionVertRanges.IsCreated) collisionVertRanges.Dispose();
        if (bulletPowers.IsCreated) bulletPowers.Dispose();

        var bulletTypeDB = GManager.Control.BTDB;
        if (bulletTypeDB == null || bulletTypeDB.types == null)
        {
            collisionVerts = new NativeArray<float2>(0, Allocator.Persistent);
            collisionVertRanges = new NativeArray<int2>(0, Allocator.Persistent);
            bulletPowers = new NativeArray<float>(0, Allocator.Persistent);
            collisionDataDirty = false;
            return;
        }

        int typeCount = bulletTypeDB.types.Length;
        List<float2[]> vertsByType = bulletTypeDB.bVerts;
        List<float> powersByType = bulletTypeDB.bPower;

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
        bulletPowers = new NativeArray<float>(typeCount, Allocator.Persistent);

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

            bulletPowers[i] = (powersByType != null && i < powersByType.Count) ? powersByType[i] : 0f;
            offset += length;
        }

        collisionDataDirty = false;
    }

    public void MarkCollisionDataDirty()
    {
        collisionDataDirty = true;
    }

    private void RegisterBulletToCollisionCells(BulletData bullet)
    {
        if (gridResolution <= 0 || cellSize <= 0f)
        {
            int fallbackCell = bullet.areaNum;
            if (fallbackCell >= 0 && fallbackCell < cells.Length)
            {
                cells[fallbackCell].enemyBullets.Add(bullet);
            }
            return;
        }

        float radius = GetCollisionBroadphaseRadius(bullet);
        if (radius <= 0f)
        {
            int fallbackCell = bullet.areaNum;
            if (fallbackCell >= 0 && fallbackCell < cells.Length)
            {
                cells[fallbackCell].enemyBullets.Add(bullet);
            }
            return;
        }

        int minX = Mathf.FloorToInt((bullet.position.x - radius) / cellSize);
        int maxX = Mathf.FloorToInt((bullet.position.x + radius) / cellSize);
        int minY = Mathf.FloorToInt((bullet.position.y - radius) / cellSize);
        int maxY = Mathf.FloorToInt((bullet.position.y + radius) / cellSize);

        if (maxX < 0 || maxY < 0 || minX >= gridResolution || minY >= gridResolution) return;

        minX = Mathf.Clamp(minX, 0, gridResolution - 1);
        maxX = Mathf.Clamp(maxX, 0, gridResolution - 1);
        minY = Mathf.Clamp(minY, 0, gridResolution - 1);
        maxY = Mathf.Clamp(maxY, 0, gridResolution - 1);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                int areaNum = GetTreeNum(x, y);
                if (areaNum < 0 || areaNum >= cells.Length) continue;
                cells[areaNum].enemyBullets.Add(bullet);
            }
        }
    }

    private float GetCollisionBroadphaseRadius(BulletData bullet)
    {
        float uniformScale = math.cmax(math.abs(bullet.scale));
        if (!collisionVertRanges.IsCreated || !collisionVerts.IsCreated)
        {
            return uniformScale;
        }

        if (bullet.typeId < 0 || bullet.typeId >= collisionVertRanges.Length)
        {
            return uniformScale;
        }

        int2 range = collisionVertRanges[bullet.typeId];
        if (range.x < 0 || range.y <= 0 || range.x + range.y > collisionVerts.Length)
        {
            return uniformScale;
        }

        float2 absScale = math.abs(bullet.scale);
        float maxLenSq = 0f;
        for (int i = 0; i < range.y; i++)
        {
            float2 scaled = collisionVerts[range.x + i] * absScale;
            maxLenSq = math.max(maxLenSq, math.lengthsq(scaled));
        }

        return maxLenSq > 0f ? math.sqrt(maxLenSq) : uniformScale;
    }

    private int CountActiveBullets(NativeList<BulletData> bullets)
    {
        if (!bullets.IsCreated || bullets.Length == 0) return 0;

        int count = 0;
        for (int i = 0; i < bullets.Length; i++)
        {
            if (bullets[i].isActive) count++;
        }

        return count;
    }

    private int CountActiveCounterBullets(NativeList<CounterBullet> bullets)
    {
        if (!bullets.IsCreated || bullets.Length == 0) return 0;

        int count = 0;
        for (int i = 0; i < bullets.Length; i++)
        {
            if (bullets[i].isActive) count++;
        }

        return count;
    }

    private void SpawnCounterBullet(BulletData sourceBullet, float dt)
    {
        if (!counterBullets.IsCreated)
        {
            counterBullets = new NativeList<CounterBullet>(256, Allocator.Persistent);
        }

        float invDt = dt > 1e-5f ? 1f / dt : 0f;

        CounterBullet counterBullet = new CounterBullet
        {
            position = sourceBullet.position,
            velocity = sourceBullet.velocity * invDt,
            damage = bulletPowers.IsCreated && sourceBullet.typeId >= 0 && sourceBullet.typeId < bulletPowers.Length
                ? bulletPowers[sourceBullet.typeId] * math.cmax(math.abs(sourceBullet.scale))
                : 0f,
            isActive = true,
            homingElapsed = 0f,
        };

        counterBullets.Add(counterBullet);
    }

    private void UpdateCounterBullets(float dt)
    {
        if (!counterBullets.IsCreated || counterBullets.Length == 0) return;
        int activeBeforeUpdate = CountActiveCounterBullets(counterBullets);

        float2 bossPos = boss != null
            ? new float2(boss.transform.position.x, boss.transform.position.y)
            : new float2(GManager.Control.PController.pos.x, GManager.Control.PController.pos.y);

        CounterBulletUpdateJob job = new CounterBulletUpdateJob
        {
            bullets = counterBullets.AsArray(),
            bossPos = bossPos,
            dt = dt
        };

        JobHandle handle = job.Schedule(counterBullets.Length, 64);
        handle.Complete();

        if (boss != null)
        {
            int activeAfterUpdate = CountActiveCounterBullets(counterBullets);
            int hitCount = activeBeforeUpdate - activeAfterUpdate;
            if (hitCount > 0)
            {
                GManager.Control?.AddCounterHitBossCount(hitCount);
            }
        }
    }

    private void NotifyPlayerHit(string source)
    {
        PlayerController player = GManager.Control?.PController;
        if (player == null) return;
        if (!player.TryHit()) return;

        GManager.Control?.AddPlayerHitCount();

        Debug.Log($"{source} collision detected.");
    }

    public void CheckCollisionWithEnemy(float2 pPos, float dt)
    {
        if (collisionDataDirty || !collisionVerts.IsCreated || !collisionVertRanges.IsCreated || !bulletPowers.IsCreated) BuildCollisionData();
        if (!collisionHitFlag.IsCreated) collisionHitFlag = new NativeArray<int>(1, Allocator.Persistent);
        if (!grazePower.IsCreated) grazePower = new NativeArray<float>(1, Allocator.Persistent);
        if (!collisionCheckBullets.IsCreated) collisionCheckBullets = new NativeList<BulletData>(256, Allocator.Persistent);
        if (!bulletPowers.IsCreated) bulletPowers = new NativeArray<float>(0, Allocator.Persistent);

        bool isPlayerDash = GManager.Control.PController.invincible;
        NativeArray<BulletData> checkBullets;

        if (isPlayerDash)
        {
            if (!enemyBullets.IsCreated || enemyBullets.Length == 0) return;
            if (!dashCollisionActiveFlags.IsCreated)
            {
                dashCollisionActiveFlags = new NativeList<byte>(math.max(256, enemyBullets.Length), Allocator.Persistent);
            }
            if (dashCollisionActiveFlags.Capacity < enemyBullets.Length)
            {
                dashCollisionActiveFlags.Capacity = enemyBullets.Length;
            }
            dashCollisionActiveFlags.ResizeUninitialized(enemyBullets.Length);
            for (int i = 0; i < enemyBullets.Length; i++)
            {
                dashCollisionActiveFlags[i] = enemyBullets[i].isActive ? (byte)1 : (byte)0;
            }
            // Dash中は実体の弾配列を直接処理して、isActive変更を反映する
            checkBullets = enemyBullets.AsArray();
        }
        else
        {
            Vector2Int pCell = BitCompact32(GetTreeNum(pPos));
            collisionCheckCells.Clear();
            int bulletCount = 0;
            foreach (var cell in cellOffsets)
            {
                Vector2Int checkCell = pCell + cell;
                int treeNum = GetTreeNum(checkCell.x, checkCell.y);
                if (treeNum < 0 || treeNum >= cells.Length) continue;
                collisionCheckCells.Add(treeNum);
                bulletCount += cells[treeNum].enemyBullets.Count;
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
                for (int j = 0; j < cells[treeNum].enemyBullets.Count; j++)
                {
                    collisionCheckBullets.Add(cells[treeNum].enemyBullets[j]);
                }
            }

            checkBullets = collisionCheckBullets.AsArray();
        }
        collisionHitFlag[0] = 0;
        grazePower[0] = 0f;

        BulletCollisionJob collisionJob = new()
        {
            bullets = checkBullets,
            bVerts = collisionVerts,
            bVertRanges = collisionVertRanges,
            bPowers = bulletPowers,
            pPos = pPos,
            grazeRangeSq = 10f, // squared distance threshold => effective radius sqrt(10) ~= 3.16
            isPlayerDash = isPlayerDash,
            isCollided = collisionHitFlag,
            attackPower = grazePower
        };

        if (isPlayerDash)
        {
            collisionJob.Run(checkBullets.Length);

            for (int i = 0; i < enemyBullets.Length && i < dashCollisionActiveFlags.Length; i++)
            {
                if (dashCollisionActiveFlags[i] == 0) continue;
                if (enemyBullets[i].isActive) continue;
                SpawnCounterBullet(enemyBullets[i], dt);
            }
        }
        else
        {
            JobHandle handle = collisionJob.Schedule(checkBullets.Length, 64);
            handle.Complete();
        }

        if (collisionHitFlag[0] != 0)
        {
            NotifyPlayerHit("Enemy bullet");
        }

        if (grazePower[0] > 0f)
        {
            Debug.Log($"Graze detected. Graze power: {grazePower[0]}");
        }
    }
}

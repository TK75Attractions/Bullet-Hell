using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

// P4 stage-seek support. Debug-only entry points used by StageReader.SeekTo to
// jump the stage clock: a hard clear of all live enemy danmaku/lasers/enemies and
// a headstart enemy re-spawn with the orbit advanced to the seeked time. Data
// ownership is unchanged; everything operates on the lists owned by the core
// QuadOrder partial. Not used by the normal play path.
public partial class QuadOrder
{
    /// <summary>
    /// Instantly clears every managed enemy danmaku, laser, spawned enemy and
    /// counter bullet with no fade — the seek target is meant to start visually
    /// empty. Bumps the spawn/bullet generations so outstanding async spawns and
    /// stale handles are invalidated, matching <see cref="ClearManagedEnemyDanmaku"/>.
    /// </summary>
    public void SeekClearAll()
    {
        enemySpawnGeneration++;
        enemyBulletGeneration++;
        bulletEvents.Clear();

        if (enemyBullets.IsCreated)
        {
            for (int i = 0; i < enemyBullets.Length; i++)
            {
                BulletData bullet = enemyBullets[i];
                bullet.isActive = false;
                bullet.isClearing = false;
                enemyBullets[i] = bullet;
            }
        }

        for (int i = 0; i < allLASERs.Count; i++)
        {
            if (allLASERs[i] != null) allLASERs[i].Destroy();
        }
        allLASERs.Clear();

        for (int i = enemyEntries.Count - 1; i >= 0; i--)
        {
            MultiBullet multiBullet = enemyEntries[i].multiBullet;
            if (multiBullet == null) continue;
            multiBullet.isActive = false;
            UnityEngine.Object.Destroy(multiBullet.gameObject);
        }
        enemyEntries.Clear();

        if (multiBulletOrbitBullets.IsCreated) multiBulletOrbitBullets.Clear();
        if (warpZones.IsCreated) warpZones.Clear();
        if (counterBullets.IsCreated) counterBullets.Clear();
        if (collisionCheckBullets.IsCreated) collisionCheckBullets.Clear();
        GManager.Control.CManager?.StopScreenNoise();

        ClearAllCells();
        SyncNativeBulletDebugViews(forceRefresh: true);
    }

    /// <summary>
    /// Spawns a single enemy for a seek target, advancing its orbit bullet by
    /// <paramref name="elapsed"/> seconds so it appears where it would be at the
    /// seeked time. The MultiBullet firing cycle starts fresh from the seek moment
    /// (past shots are intentionally skipped). Off-screen or lifetime-expired
    /// orbits deactivate exactly as the runtime job would.
    /// </summary>
    public void SeekSpawnEnemy(EnemySpawner spawner, float elapsed)
    {
        GameObject multiBulletObject = Instantiate(GManager.Control.MultiBulletObj);
        MultiBullet multiBullet = multiBulletObject.GetComponent<MultiBullet>();
        if (multiBullet == null)
        {
            Debug.LogError("[QuadOrder] SeekSpawnEnemy: MultiBulletObj is missing a MultiBullet component.");
            UnityEngine.Object.Destroy(multiBulletObject);
            return;
        }

        int orbitIndex = multiBulletOrbitBullets.Length; // == enemyEntries.Count; grow in lockstep
        multiBullet.Init(orbitIndex, spawner);

        Boss bossDisplay = multiBulletObject.GetComponent<Boss>();
        if (bossDisplay != null)
        {
            bossDisplay.Init(spawner, elapsed);
        }

        BulletData orbit = SimulateOrbitForward(spawner.orbit, elapsed);
        multiBulletOrbitBullets.Add(orbit);
        enemyEntries.Add(new EnemyEntry { multiBullet = multiBullet, boss = bossDisplay, orbitIndex = orbitIndex });

        if (!orbit.isActive)
        {
            multiBulletObject.SetActive(false);
        }
        else
        {
            multiBullet.trans.position = new Vector3(orbit.position.x, orbit.position.y, 0f);
            multiBullet.trans.rotation = multiBullet.keepVisualUpright
                ? Quaternion.identity
                : Quaternion.Euler(0, 0, orbit.angle * Mathf.Rad2Deg);
        }
    }

    /// <summary>
    /// Steps a copy of an orbit bullet forward by <paramref name="elapsed"/> seconds
    /// through the exact runtime motion update (BulletDataUpdateJob) in fixed
    /// substeps, so its position/angle/lifetime match a real play that reached the
    /// same time. Curved paths accumulate a small integration difference vs. the
    /// original frame timeline; straight orbits reproduce exactly.
    /// </summary>
    private BulletData SimulateOrbitForward(BulletData orbit, float elapsed)
    {
        if (elapsed <= 0f) return orbit;

        int steps = Mathf.Clamp(Mathf.CeilToInt(elapsed * 60f), 1, 4000);
        float dt = elapsed / steps;

        NativeArray<BulletData> tmp = new NativeArray<BulletData>(1, Allocator.Temp);
        tmp[0] = orbit;
        BulletDataUpdateJob job = new BulletDataUpdateJob
        {
            bullets = tmp,
            dt = dt,
            cellSize = cellSize,
            totalCellCount = cells.Length,
            playerVelocity = new float2(0f, 0f)
        };

        for (int s = 0; s < steps; s++)
        {
            // Direct Execute (not scheduled): identical math, no per-step job
            // scheduling overhead, and safe for a single element.
            job.Execute(0);
            if (!tmp[0].isActive) break;
        }

        BulletData result = tmp[0];
        tmp.Dispose();
        return result;
    }
}

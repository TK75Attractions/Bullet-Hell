using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;

// S7 QuadOrder split (delegation-only): spawned-enemy (MultiBullet) lifecycle and
// the bullet-change ("generate") path. The enemyEntries list and the orbit
// NativeList remain owned by the core QuadOrder partial.
public partial class QuadOrder
{
    #region //MultiBulletMethods
    public async void AddMultiBullet(EnemySpawner spawner)
    {
        float t = 0;
        int spawnGeneration = enemySpawnGeneration;

        for (int i = 0; i < spawner.count; i++)
        {
            while (t < spawner.enemyInterval * i)
            {
                await Task.Yield();
                t += Time.deltaTime;
                if (spawnGeneration != enemySpawnGeneration) return;
                if (GManager.Control.state != GManager.GameState.Playing) return;
            }

            if (spawnGeneration != enemySpawnGeneration) return;

            GameObject multiBulletObject = Instantiate(GManager.Control.MultiBulletObj);
            MultiBullet multiBullet = multiBulletObject.GetComponent<MultiBullet>();
            if (multiBullet == null)
            {
                Debug.LogError("MultiBulletObj is missing a MultiBullet component.");
                UnityEngine.Object.Destroy(multiBulletObject);
                return;
            }

            int orbitIndex = multiBulletOrbitBullets.Length; // == enemyEntries.Count; the two grow in lockstep
            multiBullet.Init(orbitIndex, spawner);

            Boss bossDisplay = multiBulletObject.GetComponent<Boss>();
            if (bossDisplay != null)
            {
                bossDisplay.Init(spawner);
            }

            //Debug.Log($"Spawned multi bullet: {spawner.orbit.speed}");
            multiBulletOrbitBullets.Add(spawner.orbit);
            enemyEntries.Add(new EnemyEntry { multiBullet = multiBullet, boss = bossDisplay, orbitIndex = orbitIndex });
        }
    }

    private void UpdateMultiBulletPos(float dt)
    {
        for (int i = 0; i < enemyEntries.Count; i++)
        {
            EnemyEntry entry = enemyEntries[i];
            MultiBullet multiBullet = entry.multiBullet;
            if (multiBullet == null) continue;
            if (entry.orbitIndex < 0 || entry.orbitIndex >= multiBulletOrbitBullets.Length) continue;

            BulletData orbit = multiBulletOrbitBullets[entry.orbitIndex];
            if (!orbit.isActive)
            {
                if (multiBullet.gameObject.activeSelf) multiBullet.gameObject.SetActive(false);
                continue;
            }
            multiBullet.trans.position = new Vector3(orbit.position.x, orbit.position.y, 0);
            multiBullet.trans.rotation = multiBullet.keepVisualUpright
                ? Quaternion.identity
                : Quaternion.Euler(0, 0, orbit.angle * Mathf.Rad2Deg);
            multiBullet.UpdateMultiBullet(dt);

            if (entry.boss != null)
            {
                entry.boss.UpdateBoss(dt);
            }
        }
    }
    #endregion

    #region //GenerateMethods
    public List<int> UpdateBulletData(List<int> indexes, BulletClip clip)
    {
        if (indexes == null || indexes.Count == 0) return new();

        if (indexes.Count == 1 && clip.number == 1)
        {
            if (indexes[0] >= 0 && indexes[0] < enemyBullets.Length)
            {
                BulletData data = new BulletData(clip.data, new float2(0, 0), enemyBullets[indexes[0]].position, 0);
                enemyBullets[indexes[0]] = data;
                return new List<int>() { indexes[0] };
            }
        }
        else
        {
            List<int> temp = new();
            for (int i = 0; i < indexes.Count; i++)
            {
                int index = indexes[i];
                if (index < 0 || index >= enemyBullets.Length) continue;
                float angle = enemyBullets[index].angle;
                temp.Add(EmitEnemyBullet(clip, enemyBullets[index].position, angle)[0]);
                BulletData data = enemyBullets[index];
                data.isActive = false;
                enemyBullets[index] = data;
            }
            return temp;

        }
        return new();
    }

    #endregion
}

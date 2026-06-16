using System.Collections.Generic;
using UnityEngine;

public class BossManager : MonoBehaviour
{
    private readonly List<BossSpawner> spawners = new List<BossSpawner>();
    private readonly List<ActiveBoss> activeBosses = new List<ActiveBoss>();
    private StageReader stageReader;
    private int spawnIndex;
    private Transform bossParent;

    private class ActiveBoss
    {
        public GameObject gameObject;
        public Boss boss;
        public BossMover mover;
        public float spawnTime;
        public float lifeTime;
    }

    public void Init(StageData stageData, StageReader reader)
    {
        Clear();
        stageReader = reader;
        spawnIndex = 0;

        if (stageData != null && stageData.bossSpawners != null)
        {
            spawners.AddRange(stageData.bossSpawners);
            spawners.RemoveAll(spawner => spawner == null);
            spawners.Sort((a, b) => a.appearTime.CompareTo(b.appearTime));
        }

        if (bossParent == null)
        {
            GameObject parentObject = new GameObject("Bosses");
            parentObject.transform.SetParent(transform);
            bossParent = parentObject.transform;
        }
    }

    public void UpdateBosses(float dt, float stageTime)
    {
        while (spawnIndex < spawners.Count && spawners[spawnIndex].appearTime <= stageTime)
        {
            Spawn(spawners[spawnIndex], stageTime);
            spawnIndex++;
        }

        for (int i = activeBosses.Count - 1; i >= 0; i--)
        {
            ActiveBoss activeBoss = activeBosses[i];
            if (activeBoss == null || activeBoss.gameObject == null)
            {
                activeBosses.RemoveAt(i);
                continue;
            }

            float elapsed = stageTime - activeBoss.spawnTime;
            if (activeBoss.lifeTime >= 0f && elapsed >= activeBoss.lifeTime)
            {
                Destroy(activeBoss.gameObject);
                activeBosses.RemoveAt(i);
                continue;
            }

            activeBoss.mover?.UpdateMover(dt, elapsed);
            activeBoss.boss?.UpdateBoss(dt);
        }
    }

    public void Clear()
    {
        for (int i = activeBosses.Count - 1; i >= 0; i--)
        {
            if (activeBosses[i]?.gameObject != null)
            {
                Destroy(activeBosses[i].gameObject);
            }
        }

        activeBosses.Clear();
        spawners.Clear();
        spawnIndex = 0;
    }

    private void Spawn(BossSpawner spawner, float stageTime)
    {
        GameObject bossObject = new GameObject(string.IsNullOrWhiteSpace(spawner.bossId) ? "Boss" : $"Boss_{spawner.bossId}");
        bossObject.transform.SetParent(bossParent);
        bossObject.transform.position = new Vector3(spawner.startPos.x, spawner.startPos.y, 0f);
        bossObject.transform.localScale = new Vector3(
            spawner.scale.x == 0f ? 1f : spawner.scale.x,
            spawner.scale.y == 0f ? 1f : spawner.scale.y,
            1f);
        bossObject.transform.rotation = Quaternion.Euler(0f, 0f, spawner.angle);

        SpriteRenderer spriteRenderer = bossObject.AddComponent<SpriteRenderer>();
        EnemyVisualSetRuntime visualSet = stageReader != null ? stageReader.GetEnemyVisual(spawner.visualId) : null;
        Sprite fallbackSprite = visualSet != null ? visualSet.fallbackSprite : null;
        if (fallbackSprite != null)
        {
            spriteRenderer.sprite = fallbackSprite;
        }

        Boss boss = bossObject.AddComponent<Boss>();
        boss.Init(
            visualSet,
            BossAnimationPlan.Normalize(spawner.animation).ToEnemyAnimationPlan(),
            fallbackSprite,
            spawner.bossId,
            spawner.bossName);

        BossMover mover = bossObject.AddComponent<BossMover>();
        mover.Init(spawner.moves);

        activeBosses.Add(new ActiveBoss
        {
            gameObject = bossObject,
            boss = boss,
            mover = mover,
            spawnTime = stageTime,
            lifeTime = spawner.lifeTime
        });
    }

    private void OnDestroy()
    {
        Clear();
    }
}

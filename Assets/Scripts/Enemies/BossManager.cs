using System.Collections.Generic;
using UnityEngine;

public class BossManager : MonoBehaviour
{
    private readonly List<BossSpawner> spawners = new List<BossSpawner>();
    private readonly List<ActiveBoss> activeBosses = new List<ActiveBoss>();
    private StageReader stageReader;
    private int spawnIndex;
    private Transform bossParent;

    public Boss CurrentBoss { get; private set; }
    public bool IsBossDefeated => CurrentBoss != null && CurrentBoss.IsDefeated;

    private class ActiveBoss
    {
        public GameObject gameObject;
        public Boss boss;
        public BossMover mover;
        public float spawnTime;
        public float lifeTime;
        public SpriteRenderer spriteRenderer;
        public float fadeInSec;
        public float fadeOutSec;
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
                bool wasCurrentBoss = activeBoss != null && activeBoss.boss == CurrentBoss;
                activeBosses.RemoveAt(i);
                if (wasCurrentBoss) RefreshCurrentBossTarget();
                continue;
            }

            float elapsed = stageTime - activeBoss.spawnTime;
            if (activeBoss.lifeTime >= 0f && elapsed >= activeBoss.lifeTime)
            {
                bool wasCurrentBoss = activeBoss.boss == CurrentBoss;
                Destroy(activeBoss.gameObject);
                activeBosses.RemoveAt(i);
                if (wasCurrentBoss) RefreshCurrentBossTarget();
                continue;
            }

            activeBoss.mover?.UpdateMover(dt, elapsed);
            activeBoss.boss?.UpdateBoss(dt);
            ApplyBossFade(activeBoss, elapsed);
        }
    }

    // 出現/退場フェード(marron の enemySpawner fadeInSec/fadeOutSec 相当、統合 Stage4 で復活)。
    // アニメがフレーム毎に sprite を差し替えるため、boss 更新の後で alpha だけ上書きする。
    // fadeIn/fadeOut が両方 0 の通常ボスは alpha=1 のまま(SpriteRenderer 既定色)で no-op。
    private static void ApplyBossFade(ActiveBoss activeBoss, float elapsed)
    {
        if (activeBoss.spriteRenderer == null) return;
        float alpha = 1f;
        if (activeBoss.fadeInSec > 0f && elapsed < activeBoss.fadeInSec)
        {
            alpha = Mathf.Clamp01(elapsed / activeBoss.fadeInSec);
        }
        if (activeBoss.fadeOutSec > 0f && activeBoss.lifeTime > 0f)
        {
            float remaining = activeBoss.lifeTime - elapsed;
            if (remaining < activeBoss.fadeOutSec)
            {
                alpha = Mathf.Min(alpha, Mathf.Clamp01(remaining / activeBoss.fadeOutSec));
            }
        }
        Color c = activeBoss.spriteRenderer.color;
        if (!Mathf.Approximately(c.a, alpha))
        {
            c.a = alpha;
            activeBoss.spriteRenderer.color = c;
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
        CurrentBoss = null;
        GManager.Control?.QOrder?.SetBossTarget(null);
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
        spriteRenderer.sortingOrder = spawner.sortingOrder;
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
            spawner.bossName,
            spawner.maxHp);

        BossMover mover = bossObject.AddComponent<BossMover>();
        mover.Init(spawner.moves);

        activeBosses.Add(new ActiveBoss
        {
            gameObject = bossObject,
            boss = boss,
            mover = mover,
            spawnTime = stageTime,
            lifeTime = spawner.lifeTime,
            spriteRenderer = spriteRenderer,
            fadeInSec = spawner.fadeInSec,
            fadeOutSec = spawner.fadeOutSec
        });

        CurrentBoss = boss;
        GManager.Control?.QOrder?.SetBossTarget(boss);
    }

    private void RefreshCurrentBossTarget()
    {
        CurrentBoss = null;
        for (int i = activeBosses.Count - 1; i >= 0; i--)
        {
            if (activeBosses[i]?.boss == null) continue;
            CurrentBoss = activeBosses[i].boss;
            break;
        }

        GManager.Control?.QOrder?.SetBossTarget(CurrentBoss);
    }

    private void OnDestroy()
    {
        Clear();
    }
}

using UnityEngine;
using Unity.Mathematics;

public class Boss : MonoBehaviour
{
    public int bossId;
    public string bossName;
    public Sprite bossImage;
    public float2 pos;

    private SpriteRenderer spriteRenderer;
    private EnemyVisualPlayer visualPlayer = new EnemyVisualPlayer();
    private float visualTime;
    private bool initialized;

    public void Init()
    {
        initialized = true;
        visualTime = 0f;
        spriteRenderer = GetComponent<SpriteRenderer>();
        UpdatePosition();
        UpdateBossImage();
    }

    public void Init(EnemySpawner spawner)
    {
        initialized = true;
        bossId = spawner != null ? spawner.id : -1;
        bossName = spawner != null ? spawner.enemyName : "";
        visualTime = 0f;

        spriteRenderer = GetComponent<SpriteRenderer>();
        Sprite fallbackSprite = GetFallbackSprite(spawner);
        EnemyVisualSetRuntime visualSet = ResolveVisualSet(spawner);

        visualPlayer = new EnemyVisualPlayer();
        visualPlayer.Init(spriteRenderer, visualSet, spawner != null ? spawner.animation : null, fallbackSprite);
        UpdatePosition();
        UpdateBossImage(fallbackSprite);
    }

    private Sprite GetFallbackSprite(EnemySpawner spawner)
    {
        if (spawner == null || spawner.id < 0 || GManager.Control == null || GManager.Control.EDB == null)
        {
            return bossImage;
        }

        Sprite fallbackSprite = GManager.Control.EDB.GetSprite(spawner.id);
        return fallbackSprite != null ? fallbackSprite : bossImage;
    }

    private EnemyVisualSetRuntime ResolveVisualSet(EnemySpawner spawner)
    {
        if (spawner == null || GManager.Control == null || GManager.Control.SReader == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(spawner.visualId))
        {
            EnemyVisualSetRuntime visualSet = GManager.Control.SReader.GetEnemyVisual(spawner.visualId);
            if (visualSet != null)
            {
                return visualSet;
            }
        }

        if (!string.IsNullOrWhiteSpace(spawner.enemyName))
        {
            return GManager.Control.SReader.GetEnemyVisual(spawner.enemyName);
        }

        return null;
    }

    public void UpdateBoss(float dt)
    {
        UpdatePosition();
        if (!initialized)
        {
            return;
        }

        visualTime += dt;
        visualPlayer?.Update(dt, visualTime);
        UpdateBossImage();
    }

    private void UpdatePosition()
    {
        pos = new float2(transform.position.x, transform.position.y);
    }

    private void UpdateBossImage(Sprite fallbackSprite = null)
    {
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            bossImage = spriteRenderer.sprite;
        }
        else if (fallbackSprite != null)
        {
            bossImage = fallbackSprite;
        }
    }
}

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
    private float fadeElapsed; // シーク起動時に経過済みの秒数(通常スポーンは 0)
    private float fadeLife;
    private float fadeInSec;
    private float fadeOutSec;

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
        Init(spawner, 0f);
    }

    public void Init(EnemySpawner spawner, float elapsed)
    {
        initialized = true;
        bossId = spawner != null ? spawner.id : -1;
        bossName = spawner != null ? spawner.enemyName : "";
        visualTime = 0f;
        fadeElapsed = Mathf.Max(0f, elapsed);
        fadeLife = spawner != null ? spawner.orbit.life : 0f;
        fadeInSec = spawner != null ? spawner.fadeInSec : 0f;
        fadeOutSec = spawner != null ? spawner.fadeOutSec : 0f;

        spriteRenderer = GetComponent<SpriteRenderer>();
        Sprite fallbackSprite = GetFallbackSprite(spawner);
        EnemyVisualSetRuntime visualSet = ResolveVisualSet(spawner);
        bool hasExternalVisual = visualSet != null;

        SetPrefabMarkerVisible(!hasExternalVisual);
        if (hasExternalVisual && spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = spawner != null && spawner.sortingOrder != 0
                ? spawner.sortingOrder
                : Mathf.Max(spriteRenderer.sortingOrder, 10);
            spriteRenderer.color = Color.white;
        }

        visualPlayer = new EnemyVisualPlayer();
        visualPlayer.Init(spriteRenderer, visualSet, spawner != null ? spawner.animation : null, fallbackSprite);
        ApplyFadeAlpha();
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

    private void SetPrefabMarkerVisible(bool visible)
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer childRenderer = renderers[i];
            if (childRenderer != null && childRenderer != spriteRenderer && childRenderer.name == "Circle")
            {
                childRenderer.enabled = visible;
            }
        }
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
        ApplyFadeAlpha();
        UpdateBossImage();
    }

    // fadeInSec/fadeOutSec が指定された敵のみ、経過時間(シーク分込み)に応じて
    // スプライトαを補間する。石工の形態変化クロスフェード用。
    private void ApplyFadeAlpha()
    {
        if (spriteRenderer == null || (fadeInSec <= 0f && fadeOutSec <= 0f))
        {
            return;
        }

        float t = fadeElapsed + visualTime;
        float alpha = 1f;
        if (fadeInSec > 0f)
        {
            alpha = Mathf.Min(alpha, Mathf.Clamp01(t / fadeInSec));
        }
        if (fadeOutSec > 0f && fadeLife > 0f)
        {
            alpha = Mathf.Min(alpha, Mathf.Clamp01((fadeLife - t) / fadeOutSec));
        }

        Color color = spriteRenderer.color;
        color.a = alpha;
        spriteRenderer.color = color;
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

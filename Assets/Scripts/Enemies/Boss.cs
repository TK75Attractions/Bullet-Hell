using System;
using UnityEngine;
using Unity.Mathematics;

public class Boss : MonoBehaviour
{
    public string bossId;
    public string bossName;
    public Sprite bossImage;
    public float2 pos;

    [SerializeField, Min(0.01f)] private float maxHp = 100f;
    [SerializeField] private float currentHp = 100f;

    public float MaxHp => maxHp;
    public float CurrentHp => currentHp;
    public bool IsDefeated => currentHp <= 0f;
    public event Action<Boss> Defeated;

    private SpriteRenderer spriteRenderer;
    private EnemyVisualPlayer visualPlayer = new EnemyVisualPlayer();
    private float visualTime;
    private bool initialized;
    private float fadeLife;
    private float fadeInSec;
    private float fadeOutSec;

    public void Init()
    {
        initialized = true;
        visualTime = 0f;
        spriteRenderer = GetComponent<SpriteRenderer>();
        InitializeHealth(maxHp);
        UpdatePosition();
        UpdateBossImage();
    }

    public void Init(
        EnemyVisualSetRuntime visualSet,
        EnemyAnimationPlan animationPlan = null,
        Sprite fallbackSprite = null,
        string bossId = "",
        string bossName = "",
        float maxHp = 100f,
        float lifeTime = -1f,
        float fadeInSec = 0f,
        float fadeOutSec = 0f)
    {
        initialized = true;
        this.bossId = bossId;
        this.bossName = bossName;
        visualTime = 0f;
        fadeLife = lifeTime;
        this.fadeInSec = Mathf.Max(0f, fadeInSec);
        this.fadeOutSec = Mathf.Max(0f, fadeOutSec);

        spriteRenderer = GetComponent<SpriteRenderer>();
        InitializeHealth(maxHp);
        visualPlayer = new EnemyVisualPlayer();
        visualPlayer.Init(spriteRenderer, visualSet, animationPlan, fallbackSprite);
        ApplyFadeAlpha();
        UpdatePosition();
        UpdateBossImage(fallbackSprite);
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

    public void ApplyDamage(float damage)
    {
        if (damage <= 0f || IsDefeated) return;

        currentHp = Mathf.Max(0f, currentHp - damage);
        if (IsDefeated)
        {
            Defeated?.Invoke(this);
        }
    }

    private void InitializeHealth(float value)
    {
        maxHp = Mathf.Max(0.01f, value);
        currentHp = maxHp;
    }

    private void UpdatePosition()
    {
        pos = new float2(transform.position.x, transform.position.y);
    }

    private void ApplyFadeAlpha()
    {
        if (spriteRenderer == null || (fadeInSec <= 0f && fadeOutSec <= 0f))
        {
            return;
        }

        float alpha = 1f;
        if (fadeInSec > 0f)
        {
            alpha = Mathf.Min(alpha, Mathf.Clamp01(visualTime / fadeInSec));
        }

        if (fadeOutSec > 0f && fadeLife > 0f)
        {
            alpha = Mathf.Min(alpha, Mathf.Clamp01((fadeLife - visualTime) / fadeOutSec));
        }

        Color color = spriteRenderer.color;
        color.a = alpha;
        spriteRenderer.color = color;
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

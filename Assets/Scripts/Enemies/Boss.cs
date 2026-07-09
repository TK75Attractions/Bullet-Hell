using UnityEngine;
using Unity.Mathematics;

public class Boss : MonoBehaviour
{
    public string bossId;
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

    public void Init(
        EnemyVisualSetRuntime visualSet,
        EnemyAnimationPlan animationPlan = null,
        Sprite fallbackSprite = null,
        string bossId = "",
        string bossName = "")
    {
        initialized = true;
        this.bossId = bossId;
        this.bossName = bossName;
        visualTime = 0f;

        spriteRenderer = GetComponent<SpriteRenderer>();
        visualPlayer = new EnemyVisualPlayer();
        visualPlayer.Init(spriteRenderer, visualSet, animationPlan, fallbackSprite);
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

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
        float maxHp = 100f)
    {
        initialized = true;
        this.bossId = bossId;
        this.bossName = bossName;
        visualTime = 0f;

        spriteRenderer = GetComponent<SpriteRenderer>();
        InitializeHealth(maxHp);
        visualPlayer = new EnemyVisualPlayer();
        visualPlayer.Init(spriteRenderer, visualSet, animationPlan, fallbackSprite);
        UpdatePosition();
        UpdateBossImage(fallbackSprite);
    }

    /// <summary>
    /// 石工 v41: アニメのイベント時刻は <see cref="Time.deltaTime"/> の積算ではなく
    /// ステージ時計（BGM の dsp に同期・BossManager が渡す appearTime からの経過秒）で進める。
    /// BossManager の lifeTime 判定と同じ時計になるので、
    /// 「老人の消滅」と「ゴーレムの騎乗版への切替」が必ず同じフレームで起きる
    /// （dt 積算だとフレームレート次第で 1 コマ二重に写ったり 1 コマ欠けたりしていた）。
    /// クリップ内のコマ送り（frameTimer）は従来どおり dt なので、
    /// dsp が 21.33ms 単位で進むことによるカクつきは出ない。
    /// </summary>
    public void UpdateBoss(float dt, float stageElapsed)
    {
        UpdatePosition();
        if (!initialized)
        {
            return;
        }

        visualTime = stageElapsed;
        visualPlayer?.Update(dt, visualTime);
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

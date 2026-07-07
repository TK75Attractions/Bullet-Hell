using Unity.Mathematics;

public struct CounterBullet
{
    public float2 position;
    public float2 velocity;
    public float2 startPosition;
    public float2 controlPosition;
    public float2 targetPosition;
    public float damage;
    public bool isActive;
    public bool launched;
    public float spawnElapsed;
    public float spawnDelay;
    public float curveElapsed;
    public float curveDuration;
    public int sourceTypeId;
    public float2 sourceScale;
    public float sourceAngle;
    public float4 sourceColor;

    public const int TypeId = 18;
    public const int TrailCapacity = 16;
    public const float Speed = 20f;
    public const float SpawnDelay = 0.24f;
    public const float CurveMinDuration = 0.22f;
    public const float CurveMaxDuration = 0.85f;
    public const float CurveOffsetRatio = 0.25f;
    public const float CurveOffsetMin = 0.8f;
    public const float CurveOffsetMax = 6f;
    public const float TrailProgressSpan = 0.48f;
    public const float HeadAlpha = 0.72f;
    public const float TrailWidthScale = 0.65f;
    public const float TrailAlpha = 0.6f;

    public static float GetSize(float damage)
    {
        return math.clamp(0.16f + damage * 0.05f, 0.16f, 1.2f) * 8f;
    }
}

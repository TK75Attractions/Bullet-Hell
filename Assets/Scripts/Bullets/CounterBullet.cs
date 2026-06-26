using Unity.Mathematics;

public struct CounterBullet
{
    public float2 position;
    public float2 velocity;
    public float damage;
    public bool isActive;
    public float homingElapsed;

    public int trailCount;
    public float2 trail0;
    public float2 trail1;
    public float2 trail2;
    public float2 trail3;
    public float2 trail4;
    public float2 trail5;
    public float2 trail6;
    public float2 trail7;
    public float2 trail8;
    public float2 trail9;
    public float2 trail10;
    public float2 trail11;
    public float2 trail12;
    public float2 trail13;
    public float2 trail14;
    public float2 trail15;

    public const int TypeId = 18;
    public const int TrailCapacity = 16;
    public const float Speed = 20f;
    public const float HomingStrength = 8f;
    public const float InitialHomingFactor = -0.3f;
    public const float HomingRampDuration = 0.85f;

    public static float GetSize(float damage)
    {
        return math.clamp(0.16f + damage * 0.05f, 0.16f, 1.2f) * 10;
    }

    public void PushTrailSample(float2 sample)
    {
        trail15 = trail14;
        trail14 = trail13;
        trail13 = trail12;
        trail12 = trail11;
        trail11 = trail10;
        trail10 = trail9;
        trail9 = trail8;
        trail8 = trail7;
        trail7 = trail6;
        trail6 = trail5;
        trail5 = trail4;
        trail4 = trail3;
        trail3 = trail2;
        trail2 = trail1;
        trail1 = trail0;
        trail0 = sample;
        trailCount = math.min(trailCount + 1, TrailCapacity);
    }

    public bool TryGetTrailPoint(int index, out float2 point)
    {
        point = default;
        if (index < 0 || index >= trailCount) return false;

        switch (index)
        {
            case 0:
                point = trail0;
                return true;
            case 1:
                point = trail1;
                return true;
            case 2:
                point = trail2;
                return true;
            case 3:
                point = trail3;
                return true;
            case 4:
                point = trail4;
                return true;
            case 5:
                point = trail5;
                return true;
            case 6:
                point = trail6;
                return true;
            case 7:
                point = trail7;
                return true;
            case 8:
                point = trail8;
                return true;
            case 9:
                point = trail9;
                return true;
            case 10:
                point = trail10;
                return true;
            case 11:
                point = trail11;
                return true;
            case 12:
                point = trail12;
                return true;
            case 13:
                point = trail13;
                return true;
            case 14:
                point = trail14;
                return true;
            case 15:
                point = trail15;
                return true;
            default:
                return false;
        }
    }
}

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct CounterBulletUpdateJob : IJobParallelFor
{
    public NativeArray<CounterBullet> bullets;
    public float2 bossPos;
    public float dt;

    public void Execute(int index)
    {
        CounterBullet bullet = bullets[index];
        if (!bullet.isActive) return;

        if (!bullet.launched)
<<<<<<< HEAD
=======
        {
            bullet.spawnElapsed += dt;
            if (bullet.spawnElapsed < bullet.spawnDelay)
            {
                bullets[index] = bullet;
                return;
            }

            bullet.launched = true;
            bullet.homingElapsed = 0f;
            bullet.trailCount = 0;
            bullets[index] = bullet;
            return;
        }

        bullet.homingElapsed += dt;
        bullet.PushTrailSample(bullet.position);

        float2 toBoss = bossPos - bullet.position;
        float distanceSq = math.dot(toBoss, toBoss);
        if (distanceSq < 0.25f * 0.25f)
>>>>>>> origin/main
        {
            bullet.spawnElapsed += dt;
            if (bullet.spawnElapsed < bullet.spawnDelay)
            {
                bullets[index] = bullet;
                return;
            }

            bullet.launched = true;
            BeginCurve(ref bullet, index, bossPos);
            bullets[index] = bullet;
            return;
        }

        bullet.curveElapsed += dt;
        float progress = GetCurveProgress(bullet);
        float easedProgress = EaseCurveProgress(progress);
        float2 previousPosition = bullet.position;

        bullet.position = EvaluateBezier(
            bullet.startPosition,
            bullet.controlPosition,
            bullet.targetPosition,
            easedProgress
        );

        float2 delta = bullet.position - previousPosition;
        if (math.dot(delta, delta) > 1e-8f)
        {
            bullet.velocity = delta / math.max(dt, 1e-5f);
        }
        else
        {
            bullet.velocity = EvaluateBezierDerivative(
                bullet.startPosition,
                bullet.controlPosition,
                bullet.targetPosition,
                easedProgress
            );
        }

        if (progress >= 1f)
        {
            bullet.isActive = false;
        }

        bullets[index] = bullet;
    }
<<<<<<< HEAD

    private static void BeginCurve(ref CounterBullet bullet, int index, float2 targetPosition)
    {
        float2 startPosition = bullet.position;
        float2 toTarget = targetPosition - startPosition;
        float distance = math.length(toTarget);
        float2 direction = distance > 1e-5f ? toTarget / distance : new float2(1f, 0f);
        float2 normal = new float2(-direction.y, direction.x);
        float side = (index & 1) == 0 ? 1f : -1f;
        float curveOffset = math.clamp(
            distance * CounterBullet.CurveOffsetRatio,
            CounterBullet.CurveOffsetMin,
            CounterBullet.CurveOffsetMax
        );

        bullet.startPosition = startPosition;
        bullet.targetPosition = targetPosition;
        bullet.controlPosition = (startPosition + targetPosition) * 0.5f + normal * curveOffset * side;
        bullet.curveElapsed = 0f;
        bullet.curveDuration = math.clamp(
            distance / CounterBullet.Speed,
            CounterBullet.CurveMinDuration,
            CounterBullet.CurveMaxDuration
        );
        bullet.velocity = EvaluateBezierDerivative(
            bullet.startPosition,
            bullet.controlPosition,
            bullet.targetPosition,
            0f
        );
    }

    private static float GetCurveProgress(CounterBullet bullet)
    {
        return bullet.curveDuration > 1e-5f
            ? math.saturate(bullet.curveElapsed / bullet.curveDuration)
            : 1f;
    }

    private static float EaseCurveProgress(float progress)
    {
        return math.smoothstep(0f, 1f, math.saturate(progress));
    }

    private static float2 EvaluateBezier(float2 start, float2 control, float2 target, float t)
    {
        float invT = 1f - t;
        return invT * invT * start + 2f * invT * t * control + t * t * target;
    }

    private static float2 EvaluateBezierDerivative(float2 start, float2 control, float2 target, float t)
    {
        return 2f * (1f - t) * (control - start) + 2f * t * (target - control);
    }
=======
>>>>>>> origin/main
}

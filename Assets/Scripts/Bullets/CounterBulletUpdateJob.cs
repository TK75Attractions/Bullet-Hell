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
        {
            bullet.isActive = false;
            bullets[index] = bullet;
            return;
        }

        float distance = math.sqrt(distanceSq);
        float2 desiredVelocity = distance > 1e-5f ? toBoss / distance * CounterBullet.Speed : new float2(0f, 0f);
        float ramp = math.saturate(bullet.homingElapsed / CounterBullet.HomingRampDuration);
        float homingFactor = math.lerp(CounterBullet.InitialHomingFactor, 1f, math.smoothstep(0f, 1f, ramp));
        float t = math.saturate(CounterBullet.HomingStrength * homingFactor * dt);

        bullet.velocity = math.lerp(bullet.velocity, desiredVelocity, t);

        bullet.position += bullet.velocity * dt;
        bullets[index] = bullet;
    }
}

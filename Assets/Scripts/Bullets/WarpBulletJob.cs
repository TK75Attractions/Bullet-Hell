using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct WarpBulletJob : IJobParallelFor
{
    public NativeArray<BulletData> bullets;

    [ReadOnly]
    public NativeArray<BulletData> warpZones;

    public float dt;
    public float warpCooldown;
    public QuadGrid grid;
    public int reflectXTypeId;
    public int reflectYTypeId;

    public void Execute(int index)
    {
        BulletData bullet = bullets[index];
        if (!bullet.isActive || bullet.isClearing) return;
        if (bullet.appearTime > bullet.time) return;
        if (bullet.warpCooldown > 0f) return;
        if (warpZones.Length < 2) return;

        for (int zoneIndex = 0; zoneIndex < warpZones.Length; zoneIndex++)
        {
            int targetIndex = zoneIndex ^ 1;
            if (targetIndex >= warpZones.Length) continue;

            BulletData sourceZone = warpZones[zoneIndex];
            BulletData targetZone = warpZones[targetIndex];
            if (!sourceZone.isActive || !targetZone.isActive) continue;
            if (!Contains(sourceZone, bullet.position)) continue;

            Warp(ref bullet, sourceZone, targetZone);
            bullets[index] = bullet;
            return;
        }
    }

    private void Warp(ref BulletData bullet, BulletData sourceZone, BulletData targetZone)
    {
        float2 local = bullet.position - sourceZone.position;
        float2 exitPosition = targetZone.position + local;
        float2 exitVelocity = dt > 1e-5f ? bullet.velocity / dt : new float2(0f, 0f);
        exitVelocity = TransformVelocity(exitVelocity, sourceZone);

        bullet.position = exitPosition;
        bullet.velocity = exitVelocity * dt;
        bullet.originPos = exitPosition;
        bullet.originVlc = exitVelocity;

        bullet.speed = 0f;
        bullet.gravity = new float2(0f, 0f);
        bullet.radiusVlc = 0f;
        bullet.thetaVlc = 0f;
        bullet.radiusAccel = 0f;
        bullet.thetaAccel = 0f;
        bullet.playerInfluence = new float2(0f, 0f);
        bullet.random = 0f;

        bullet.startX = 0f;
        bullet.nowCalculateX = 0f;
        bullet.startPos = new float2(0f, 0f);
        bullet.nowCalculateVlc = new float2(0f, 0f);
        bullet.polynomial = new float4(0f, 0f, 0f, 0f);
        bullet.polarForm = new float2(1f, 0f);

        bullet.warpCooldown = math.max(0f, warpCooldown);
        bullet.angle = GetAngleRad(exitVelocity.x, exitVelocity.y);
        bullet.areaNum = grid.GetTreeNum(exitPosition);

        if (bullet.areaNum == -1)
        {
            bullet.isActive = false;
        }
    }

    private bool Contains(BulletData zone, float2 position)
    {
        float2 halfSize = math.abs(zone.scale) * 0.5f;
        float angle = zone.angle + zone.initialAngle;
        float cos = math.cos(angle);
        float sin = math.sin(angle);
        float2 delta = position - zone.position;
        float2 local = new float2(
            delta.x * cos + delta.y * sin,
            -delta.x * sin + delta.y * cos
        );

        return math.abs(local.x) <= halfSize.x
            && math.abs(local.y) <= halfSize.y;
    }

    private float2 TransformVelocity(float2 velocity, BulletData sourceZone)
    {
        if (reflectXTypeId >= 0 && sourceZone.typeId == reflectXTypeId)
        {
            velocity.x = -velocity.x;
        }

        if (reflectYTypeId >= 0 && sourceZone.typeId == reflectYTypeId)
        {
            velocity.y = -velocity.y;
        }

        return velocity;
    }

    private float GetAngleRad(float x, float y)
    {
        float rad = math.atan2(y, x);
        if (rad < 0) rad += 2 * math.PI;
        return (float)rad;
    }
}

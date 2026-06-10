using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public struct WarpBulletJob : IJobParallelFor
{
    public NativeArray<BulletData> bullets;

    [ReadOnly]
    public NativeArray<BulletData> warpZones;

    public float dt;
    public float warpCooldown;
    public float cellSize;
    public int totalCellCount;

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

        bullet.position = exitPosition;
        bullet.velocity = exitVelocity * dt;
        bullet.originPos = exitPosition;
        bullet.originVlc = exitVelocity;

        bullet.speed = 0f;
        bullet.gravity = 0f;
        bullet.radiusVlc = 0f;
        bullet.thetaVlc = 0f;
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
        bullet.areaNum = GetTreeNum(exitPosition);

        if (bullet.areaNum == -1)
        {
            bullet.isActive = false;
        }
    }

    private bool Contains(BulletData zone, float2 position)
    {
        float2 halfSize = math.abs(zone.scale) * 0.5f;
        float2 min = zone.position - halfSize;
        float2 max = zone.position + halfSize;
        return position.x >= min.x
            && position.x <= max.x
            && position.y >= min.y
            && position.y <= max.y;
    }

    private int GetTreeNum(float2 pos)
    {
        if (pos.x < 0 || pos.y < 0) return -1;
        int nx = Mathf.FloorToInt(pos.x / cellSize);
        int ny = Mathf.FloorToInt(pos.y / cellSize);

        int result = BitSeparate32(nx) | (BitSeparate32(ny) << 1);
        if (result >= 0 && result < totalCellCount) return result;
        return -1;
    }

    private int BitSeparate32(int n)
    {
        n = (n | n << 8) & 0x00ff00ff;
        n = (n | n << 4) & 0x0f0f0f0f;
        n = (n | n << 2) & 0x33333333;
        return (n | n << 1) & 0x55555555;
    }

    private float GetAngleRad(float x, float y)
    {
        double rad = math.atan2(y, x);
        if (rad < 0) rad += 2 * math.PI;
        return (float)rad;
    }
}

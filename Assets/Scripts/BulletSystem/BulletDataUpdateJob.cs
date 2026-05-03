using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public struct BulletDataUpdateJob : IJobParallelFor
{
    private const float NoiseFrequency = 1f;

    public NativeArray<BulletData> bullets;
    public float dt;
    public float cellSize;
    public int cellCount;
    public int totalCellCount;

    public void Execute(int index)
    {
        BulletData bullet = bullets[index];
        if (bullet.isActive == false) return;
        bullet.time += dt;

        //ベースの座標を更新
        bullet.originPos += bullet.originVlc * dt;

        //random を揺らぎ量として、線形補間したスムーズノイズを原点に加える
        float2 noisyOriginPos = bullet.originPos;
        if (bullet.random > 0f)
        {
            float noiseTime = bullet.time * NoiseFrequency;
            float2 noise = GetSmoothNoise(index, noiseTime) * bullet.random;
            noisyOriginPos += noise;
        }

        //弾の見かけの原点からのベクトルを計算
        float2 delta = bullet.nowCalculateVlc * dt;
        float x = bullet.nowCalculateX + delta.x;
        bullet.nowCalculateX = x;
        float y = 0;
        y += bullet.polynomial.x * x;
        y += bullet.polynomial.y * x * x;
        y += bullet.polynomial.z * x * x * x;
        y += bullet.polynomial.w * x * x * x * x;
        float2 disVector = new float2(x, y) - bullet.startPos;

        //弾の多項式計算上の接戦ベクトルを計算
        float tan = 0;
        tan += 1 * bullet.polynomial.x;
        tan += 2 * bullet.polynomial.y * x;
        tan += 3 * bullet.polynomial.z * x * x;
        tan += 4 * bullet.polynomial.w * x * x * x;
        float2 vec = new float2(1, tan);
        float magnitude = math.sqrt(1 + tan * tan);
        bullet.nowCalculateVlc = vec / magnitude * bullet.speed;

        //算出したベクトルの回転計算
        bullet.polarForm = new float2(bullet.radiusVlc * dt, bullet.thetaVlc * dt) + bullet.polarForm;
        float cos = math.cos(bullet.polarForm.y);
        float sin = math.sin(bullet.polarForm.y);
        float2 rotatedVector = bullet.polarForm.x * new float2(disVector.x * cos - disVector.y * sin, disVector.x * sin + disVector.y * cos);

        //位置を計算
        float2 unGravitatedPos = rotatedVector + noisyOriginPos;

        //重力の影響を加算
        if (bullet.gravity != 0)
        {
            float h = bullet.gravity * bullet.time * bullet.time / 2;
            float2 gravitatedPos = unGravitatedPos - new float2(0, h);
            bullet.velocity = gravitatedPos - bullet.position;
            bullet.position = gravitatedPos;
        }
        else
        {
            bullet.velocity = unGravitatedPos - bullet.position;
            bullet.position = unGravitatedPos;
        }

        //角度を計算
        float a = GetAngleRad(bullet.velocity.x, bullet.velocity.y);
        bullet.angle = a + bullet.angleSpeed * bullet.time;

        //四分木秩序に変換
        int n = GetTreeNum(new float2(bullet.position.x, bullet.position.y));
        bullet.areaNum = n;

        //範囲外の弾を非アクティブに設定
        if (n == -1) bullet.isActive = false;
        bullets[index] = bullet;
    }

    public int GetTreeNum(float2 pos)
    {
        if (pos.x < 0 || pos.y < 0) return -1;
        int nx = Mathf.FloorToInt(pos.x / cellSize);
        int ny = Mathf.FloorToInt(pos.y / cellSize);

        int result = BitSeparate32(nx) | (BitSeparate32(ny) << 1);
        int maxCellCount = totalCellCount > 0 ? totalCellCount : cellCount;
        if (result >= 0 && result < maxCellCount) return result;
        return -1;
    }

    public int BitSeparate32(int n)
    {
        n = (n | n << 8) & 0x00ff00ff;
        n = (n | n << 4) & 0x0f0f0f0f;
        n = (n | n << 2) & 0x33333333;
        return (n | n << 1) & 0x55555555;
    }

    public float GetAngleRad(float x, float y)
    {
        double rad = math.atan2(y, x);
        if (rad < 0) rad += 2 * math.PI;
        return (float)rad;
    }

    private float2 GetSmoothNoise(int index, float time)
    {
        float baseTime = math.floor(time);
        float t = time - baseTime;
        t = t * t * (3f - 2f * t);

        float seed = index * 17.0f;
        float2 noiseA = new float2(HashSigned(seed + baseTime), HashSigned(seed + baseTime + 53.0f));
        float2 noiseB = new float2(HashSigned(seed + baseTime + 1.0f), HashSigned(seed + baseTime + 54.0f));
        return math.lerp(noiseA, noiseB, t);
    }

    private float HashSigned(float value)
    {
        return Hash01(value) * 2f - 1f;
    }

    private float Hash01(float value)
    {
        return math.frac(math.sin(value) * 43758.5453f);
    }
}
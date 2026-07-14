using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct BulletDataUpdateJob : IJobParallelFor
{
    private const float NoiseFrequency = 1f;
    private const float AngleVelocityEpsilonSq = 1e-10f;

    public NativeArray<BulletData> bullets;
    public float dt;
    public QuadGrid grid;
    public float2 playerVelocity;

    public void Execute(int index)
    {
        BulletData bullet = bullets[index];
        if (!bullet.isActive && !bullet.isClearing) return;
        bullet.time += dt;
        if (bullet.warpCooldown > 0f)
        {
            bullet.warpCooldown = math.max(0f, bullet.warpCooldown - dt);
        }
        if (bullet.isClearing)
        {
            bullet.clearTime += dt;
        }

        // life を超えた弾は更新と描画対象から外す
        if (!bullet.isClearing && bullet.life > 0f && bullet.time >= bullet.life)
        {
            bullet.isActive = false;
            bullets[index] = bullet;
            return;
        }

        if (bullet.appearTime > bullet.time)
        {
            // appearDuration > 0 の場合のみ原点追従の微小更新を行う
            // appearDuration == 0 の場合は time が appearTime に達するまで位置を更新しない
            if (bullet.appearDuration >= 0f)
            {
                bullet = Update(bullet, index, dt * 0.0001f);
            }
            if (bullet.isClearing && (bullet.clearDuration <= 0f || bullet.clearTime >= bullet.clearDuration))
            {
                bullet.isClearing = false;
                bullet.isActive = false;
            }
            bullets[index] = bullet;
            return;
        }

        bullet = Update(bullet, index, dt);

        //四分木秩序に変換(areaNum。生存域内でも grid 外の margin 弾は -1 になるが、
        //当たり判定側は areaNum<0 をガード済みなので安全)
        int n = grid.GetTreeNum(bullet.position);
        bullet.areaNum = n;

        //範囲外の弾を非アクティブに設定。collision grid の origin 由来のカリング域
        //(x∈[0,36)/y∈[-9,27))だと左端・上端へ飛ぶ破片が marron より早く消える regression に
        //なるため、marron 準拠の生存境界 [-2,36)² を明示適用する(grid の -1 では判定しない)。
        float2 p = bullet.position;
        if (!bullet.ignoreOutOfBoundsCulling
            && (p.x < -2f || p.y < -2f || p.x >= 36f || p.y >= 36f))
        {
            bullet.isActive = false;
        }
        if (bullet.isClearing && (bullet.clearDuration <= 0f || bullet.clearTime >= bullet.clearDuration))
        {
            bullet.isClearing = false;
            bullet.isActive = false;
        }
        bullets[index] = bullet;
    }

    public BulletData Update(BulletData bullet, int index, float dt)
    {
        //ベースの座標を更新
        bullet.originPos += bullet.originVlc * dt;
        bullet.originPos += playerVelocity * bullet.playerInfluence * dt;
        float lapse = bullet.time - bullet.appearTime;

        float2 noisyOriginPos = bullet.originPos;
        if (bullet.random > 0f)
        {
            float noiseTime = bullet.time * NoiseFrequency;
            float2 noise = GetSmoothNoise(index, noiseTime) * bullet.random;
            noisyOriginPos += noise;
        }

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
        float2 polarVelocity = new float2(bullet.radiusVlc, bullet.thetaVlc);
        float2 polarAccel = new float2(bullet.radiusAccel, bullet.thetaAccel);
        bullet.polarForm += polarVelocity * dt + 0.5f * polarAccel * dt * dt;
        bullet.radiusVlc += bullet.radiusAccel * dt;
        bullet.thetaVlc += bullet.thetaAccel * dt;
        double cos = math.cos(bullet.polarForm.y);
        double sin = math.sin(bullet.polarForm.y);
        float2 rotatedVector = bullet.polarForm.x * new float2((float)(disVector.x * cos - disVector.y * sin), (float)(disVector.x * sin + disVector.y * cos));

        //位置を計算
        float2 unGravitatedPos = rotatedVector + noisyOriginPos;

        //重力の影響を加算
        if (bullet.gravity.x != 0f && lapse > 0f)
        {
            float h = bullet.gravity.x * lapse * lapse / 2f;
            float2 gravityDirection = new float2(math.cos(bullet.gravity.y), math.sin(bullet.gravity.y));
            float2 gravitatedPos = unGravitatedPos + gravityDirection * h;
            bullet.velocity = gravitatedPos - bullet.position;
            bullet.position = gravitatedPos;
        }
        else
        {
            bullet.velocity = unGravitatedPos - bullet.position;
            bullet.position = unGravitatedPos;
        }

        //角度を計算
        if (math.lengthsq(bullet.velocity) > AngleVelocityEpsilonSq)
        {
            float a = GetAngleRad(bullet.velocity.x, bullet.velocity.y);
            bullet.angle = a + bullet.angleSpeed * lapse;
        }

        return bullet;
    }
    public float GetAngleRad(float x, float y)
    {
        float rad = math.atan2(y, x);
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

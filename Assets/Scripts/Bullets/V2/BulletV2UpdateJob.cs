using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// v2 運動レーン専任の更新 Job(SPEC-RUNTIME-V2.md P1-a/b)。
/// <see cref="BulletData.HasV2Motion"/> が false の弾は素通りし、従来の
/// <see cref="BulletDataUpdateJob"/> が処理する。両 Job は同じ NativeArray&lt;BulletData&gt;
/// を Schedule→Complete で逐次実行するため、対象弾の重複更新は発生しない。
///
/// 運動モデル:
/// - segments が非空: 閉形式の積分(ステートレス)。区間の等速度ベクトルを thetaVlc で
///   連続回転させた解析解 + 等加速度(gravity)を appearTime からの経過時間(lapse)だけから
///   毎フレーム計算し直す。区間境界では前区間の終端変位を積算した基準位置から続くため、
///   位置は数式的に連続(誤差はfloat演算のみ)。区間合計時間が life を下回る場合、
///   最終区間の終端で静止する(著者側で life と一致させる想定)。
/// - segments が空(homingのみ): ステートフルな数値積分。polarForm.y を「現在の進行角」として
///   使い回し、毎フレーム自機方向へ homingTurnRate*dt だけ旋回してから speed 分だけ前進する。
///   homingDuration 経過後は旋回を止め、そのときの方向のまま直進を続ける。
/// </summary>
[BurstCompile]
public struct BulletV2UpdateJob : IJobParallelFor
{
    private const float AngleVelocityEpsilonSq = 1e-10f;
    private const float ThetaVlcEpsilon = 1e-6f;

    public NativeArray<BulletData> bullets;
    public float dt;
    public QuadGrid grid;
    public float2 playerVelocity;
    public float2 playerPosition;

    public void Execute(int index)
    {
        BulletData bullet = bullets[index];
        if (!bullet.HasV2Motion) return; // v1 レーンの弾は BulletDataUpdateJob が専任で処理する
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

        if (!bullet.isClearing && bullet.life > 0f && bullet.time >= bullet.life)
        {
            bullet.isActive = false;
            bullets[index] = bullet;
            return;
        }

        if (bullet.appearTime > bullet.time)
        {
            if (bullet.appearDuration >= 0f)
            {
                bullet = Update(bullet, dt * 0.0001f);
            }
            if (bullet.isClearing && (bullet.clearDuration <= 0f || bullet.clearTime >= bullet.clearDuration))
            {
                bullet.isClearing = false;
                bullet.isActive = false;
            }
            bullets[index] = bullet;
            return;
        }

        bullet = Update(bullet, dt);

        int n = grid.GetTreeNum(bullet.position);
        bullet.areaNum = n;

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

    private BulletData Update(BulletData bullet, float stepDt)
    {
        bullet.originPos += bullet.originVlc * stepDt;
        bullet.originPos += playerVelocity * bullet.playerInfluence * stepDt;

        float2 newPosition;
        float2 velocity;
        float lapse = bullet.time - bullet.appearTime;

        if (bullet.v2Segments.Length > 0)
        {
            EvaluateSegments(bullet.v2Segments, math.max(0f, lapse), out float2 localOffset, out velocity);
            newPosition = bullet.originPos + localOffset;
        }
        else
        {
            bool homingActive = lapse >= 0f && lapse < bullet.homingDuration;
            if (homingActive)
            {
                float2 currentPos = bullet.originPos + bullet.v2LocalOffset;
                float angleToPlayer = math.atan2(playerPosition.y - currentPos.y, playerPosition.x - currentPos.x);
                float maxStep = math.abs(bullet.homingTurnRate * stepDt);
                float delta = math.clamp(WrapPi(angleToPlayer - bullet.polarForm.y), -maxStep, maxStep);
                bullet.polarForm.y += delta;
            }

            float2 direction = new float2(math.cos(bullet.polarForm.y), math.sin(bullet.polarForm.y));
            bullet.v2LocalOffset += direction * bullet.speed * stepDt;
            velocity = direction * bullet.speed;
            newPosition = bullet.originPos + bullet.v2LocalOffset;
        }

        bullet.velocity = velocity;
        bullet.position = newPosition;

        if (math.lengthsq(bullet.velocity) > AngleVelocityEpsilonSq)
        {
            float a = GetAngleRad(bullet.velocity.x, bullet.velocity.y);
            bullet.angle = a + bullet.angleSpeed * lapse;
        }

        return bullet;
    }

    private static void EvaluateSegments(in FixedList128Bytes<BulletV2Segment> segments, float lapse, out float2 offset, out float2 velocity)
    {
        float2 baseOffset = float2.zero;
        float cumulative = 0f;
        int lastIndex = segments.Length - 1;

        for (int i = 0; i <= lastIndex; i++)
        {
            BulletV2Segment segment = segments[i];
            bool isLast = i == lastIndex;
            bool infinite = segment.duration <= 0f;
            float localLapse = lapse - cumulative;

            if (!infinite && !isLast && localLapse >= segment.duration)
            {
                baseOffset += SegmentDisplacement(segment, segment.duration);
                cumulative += segment.duration;
                continue;
            }

            float localT = infinite ? localLapse : math.clamp(localLapse, 0f, segment.duration);
            localT = math.max(0f, localT);
            offset = baseOffset + SegmentDisplacement(segment, localT);
            velocity = SegmentVelocity(segment, localT);
            return;
        }

        // segments.Length > 0 は呼び出し元で保証済みのため、ここには到達しない防御的既定値。
        offset = baseOffset;
        velocity = float2.zero;
    }

    private static float2 SegmentDisplacement(in BulletV2Segment segment, float t)
    {
        float2 disp = RotatedVelocityDisplacement(segment.vlc, segment.thetaVlc, t);
        if (segment.gravity.x != 0f)
        {
            float2 gravityDir = new float2(math.cos(segment.gravity.y), math.sin(segment.gravity.y));
            disp += gravityDir * (segment.gravity.x * t * t * 0.5f);
        }
        return disp;
    }

    private static float2 SegmentVelocity(in BulletV2Segment segment, float t)
    {
        float2 vel = RotatedVelocity(segment.vlc, segment.thetaVlc, t);
        if (segment.gravity.x != 0f)
        {
            float2 gravityDir = new float2(math.cos(segment.gravity.y), math.sin(segment.gravity.y));
            vel += gravityDir * (segment.gravity.x * t);
        }
        return vel;
    }

    // v0 を角速度 omega で連続回転させた速度ベクトルの、時刻 t までの変位の閉形式解。
    // omega->0 の極限は v0*t(直線)に一致する。
    private static float2 RotatedVelocityDisplacement(float2 v0, float omega, float t)
    {
        if (math.abs(omega) < ThetaVlcEpsilon)
        {
            return v0 * t;
        }

        float sinWt = math.sin(omega * t);
        float cosWt = math.cos(omega * t);
        float x = v0.x * sinWt / omega - v0.y * (1f - cosWt) / omega;
        float y = v0.x * (1f - cosWt) / omega + v0.y * sinWt / omega;
        return new float2(x, y);
    }

    private static float2 RotatedVelocity(float2 v0, float omega, float t)
    {
        if (math.abs(omega) < ThetaVlcEpsilon)
        {
            return v0;
        }

        float cosWt = math.cos(omega * t);
        float sinWt = math.sin(omega * t);
        return new float2(
            v0.x * cosWt - v0.y * sinWt,
            v0.x * sinWt + v0.y * cosWt
        );
    }

    private static float WrapPi(float angle)
    {
        float wrapped = (angle + math.PI) % (2f * math.PI);
        if (wrapped < 0f) wrapped += 2f * math.PI;
        return wrapped - math.PI;
    }

    public float GetAngleRad(float x, float y)
    {
        float rad = math.atan2(y, x);
        if (rad < 0) rad += 2 * math.PI;
        return rad;
    }
}

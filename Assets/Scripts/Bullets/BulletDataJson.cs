using System;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public class BulletDataJson
{
    public Vector2 originPos; //原点位置
    public Vector2 originVlc; //原点の移動速度
    public Vector2 playerInfluence;

    public float startX;
    public float speed; //弾丸の速度
    public Vector2 gravity;//弾丸にかかる重力加速度
    public float initialAngle;//弾丸の初期角度
    public float angleSpeed;//弾丸の角速度
    public bool useVelocityAngle = true;//描画/衝突判定の角度に velocity 由来の angle を使うか

    public Vector2 polarForm; //原点中心に回転させる虚数(r0,theta0);
    public float radiusVlc; //r の速さ R(t) = r0 + radiusVlc * t 
    public float radiusAccel;
    public float thetaVlc; //theta の速さ Theta(t) = theta0 + thetaVlc * t
    public float thetaAccel;

    public Vector2 startPos; //多項式の計算を始める x 座標（見かけの原点）
    public Vector4 polynomial;//y = ax + bx^2 + cx^3 + dx^4 の係数

    public string typeName;
    public Vector2 scale;
    public Vector4 color;
    public float appearTime;//通常弾: 表示時間。レーザー: 当たり判定の太さ
    public float appearDuration;//通常弾: appearTime直前の演出時間。レーザー: 描画の太さ
    public float life;
    public float random;
    public float warpCooldown;
    public bool unCounterable;

    public BulletData ToBulletData()
    {
        float2 resolvedScale = new float2(scale.x, scale.y);
        if (resolvedScale.x == 0f && resolvedScale.y == 0f)
        {
            resolvedScale = new float2(1f, 1f);
        }

        BulletData b = new BulletData
        {
            originPos = new float2(this.originPos.x, this.originPos.y),
            originVlc = new float2(this.originVlc.x, this.originVlc.y),
            playerInfluence = new float2(this.playerInfluence.x, this.playerInfluence.y),
            startX = this.startX,
            speed = this.speed,
            gravity = new float2(this.gravity.x, this.gravity.y),
            initialAngle = this.initialAngle,
            angleSpeed = this.angleSpeed,
            useVelocityAngle = this.useVelocityAngle,
            polarForm = new float2(this.polarForm.x, this.polarForm.y),
            radiusVlc = this.radiusVlc,
            radiusAccel = this.radiusAccel,
            thetaVlc = this.thetaVlc,
            thetaAccel = this.thetaAccel,
            startPos = new float2(this.startPos.x, this.startPos.y),
            polynomial = new float4(this.polynomial.x, this.polynomial.y, this.polynomial.z, this.polynomial.w),
            typeId = BulletData.ResolveTypeId(this.typeName, GManager.Control.BTDB),
            scale = resolvedScale,
            color = new float4(this.color.x, this.color.y, this.color.z, this.color.w),
            appearTime = this.appearTime,
            appearDuration = this.appearDuration >= 0f ? this.appearDuration : BulletData.DefaultAppearDuration,
            life = this.life,
            random = this.random,
            warpCooldown = this.warpCooldown,
            unCounterable = this.unCounterable,
            isActive = true
        };
        b.ResetTrajectoryState(syncPosition: true);
        return b;
    }
}

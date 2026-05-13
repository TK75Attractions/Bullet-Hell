using System;
using Unity.Mathematics;

[Serializable]
public struct BulletDataJson
{
    public float2 originPos; //原点位置
    public float2 originVlc; //原点の移動速度

    public float startX;
    public float speed; //弾丸の速度
    public float accel;//弾丸の加速度
    public float gravity;//弾丸にかかる重力加速度
    public float angleSpeed;//弾丸の角速度

    public float2 polarForm; //原点中心に回転させる虚数(r0,theta0);
    public float radiusVlc; //r の速さ R(t) = r0 + radiusVlc * t 
    public float thetaVlc; //theta の速さ Theta(t) = theta0 + thetaVlc * t

    public float2 startPos; //多項式の計算を始める x 座標（見かけの原点）
    public float4 polynomial;//y = ax + bx^2 + cx^3 + dx^4 の係数

    public string typeName;
    public float size;
    public float4 color;
    public float appearTime;//弾幕を表示する時間、レーザーでは太さを指定
    public float life;
    public float random;

    public BulletData ToBulletData()
    {
        BulletData b = new BulletData
        {
            originPos = this.originPos,
            originVlc = this.originVlc,
            startX = this.startX,
            speed = this.speed,
            accel = this.accel,
            gravity = this.gravity,
            angleSpeed = this.angleSpeed,
            polarForm = this.polarForm,
            radiusVlc = this.radiusVlc,
            thetaVlc = this.thetaVlc,
            startPos = this.startPos,
            polynomial = this.polynomial,
            typeId = GManager.Control.BTDB.GetTypeId(this.typeName),
            size = this.size,
            color = this.color,
            appearTime = this.appearTime,
            life = this.life,
            random = this.random,
            isActive = true
        };
        return b;
    }
}
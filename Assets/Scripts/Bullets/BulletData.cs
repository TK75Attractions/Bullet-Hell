using System;
using TMPro;
using Unity.Mathematics;
using UnityEngine.UIElements;

[Serializable]
public struct BulletData
{
    public float2 position;//弾の位置
    public float2 velocity;//弾の変位
    public float angle;//弾の角度

    public float2 originPos; //原点位置
    public float2 originVlc; //原点の移動速度
    public float startX;
    public float speed; //弾丸の速度
    public float acccel;//弾丸の加速
    public float gravity;//弾丸にかかる重力加速度
    public float angleSpeed;//弾丸の速度


    public float2 polarForm;//原点中心に回転させる虚数(r,t);
    public float radiusVlc;//r の速さ
    public float thetaVlc;//theta の速さ

    public float2 startPos;//多項式の計算を始める x 座標（見かけの原点）
    public float2 nowCalculateVlc;//接線速度
    public float nowCalculateX;
    public float4 polynomial;

    public int typeId;
    public float size;
    public float4 color;

    public int areaNum;
    public float time;
    public bool isActive;

    /// <summary>
    /// 弾幕のデータ
    /// </summary>
    /// <param name="_pos">原点座標 originPos</param>
    /// <param name="_vlc">原点速度 originVlc</param>
    /// <param name="_s">スピード speed</param>
    /// <param name="_acc">加速度 accel</param>
    /// <param name="_g">重力加速度 gravity</param>
    /// <param name="_as">角速度 angleSpeed</param>
    /// <param name="_polar">極形式回転 polar</param>
    /// <param name="_absV">絶対値速度 radiusVlc</param>
    /// <param name="_theV">回転角速度 thetaVlc</param>
    /// <param name="_start">計算開始 x 座標 startValue</param>
    /// <param name="_poly">多項式係数 polynomial</param>
    /// <param name="type">弾のタイプID typeId</param>
    /// <param name="_size">弾のサイズ size</param>
    /// <param name="_color">弾の色 color</param>
    public BulletData(float2 _pos, float2 _vlc, float _s, float _acc, float _g, float _as, float2 _polar, float _absV, float _theV, float _start, float4 _poly, int type, float _size, float4 _color)
    {
        position = _pos;
        velocity = new(0, 0);
        angle = 0;
        originPos = position;
        originVlc = _vlc;
        speed = _s;
        acccel = _acc;
        gravity = _g;
        angleSpeed = _as;
        polarForm = _polar;
        radiusVlc = _absV;
        thetaVlc = _theV;
        polynomial = _poly;
        nowCalculateX = _start;

        time = 0;
        typeId = type;
        areaNum = 0;
        size = _size;
        isActive = true;
        color = _color;

        float x = _start;
        startX = x;
        float y = 0;
        y += polynomial.x * x;
        y += polynomial.y * x * x;
        y += polynomial.z * x * x * x;
        y += polynomial.w * x * x * x * x;
        startPos = new float2(x, y);

        float tan = 0;
        tan += 1 * polynomial.x;
        tan += 2 * polynomial.y * x;
        tan += 3 * polynomial.z * x * x;
        tan += 4 * polynomial.w * x * x * x;

        float2 vec = new float2(1, tan);
        float magnitude = math.sqrt(1 + tan * tan);
        nowCalculateVlc = vec / magnitude * speed;
    }

    public BulletData(BulletData data, float2 _pos)
    {
        position = _pos;
        velocity = data.velocity;
        angle = data.angle;
        originPos = _pos;
        originVlc = data.originVlc;
        speed = data.speed;
        acccel = data.acccel;
        gravity = data.gravity;
        angleSpeed = data.angleSpeed;
        polarForm = data.polarForm;
        radiusVlc = data.radiusVlc;
        thetaVlc = data.thetaVlc;
        nowCalculateX = data.startX;
        polynomial = data.polynomial;
        typeId = data.typeId;
        size = data.size;
        color = data.color;

        areaNum = 0;
        time = 0;
        isActive = data.isActive;

        startX = data.startX;
        float x = data.startX;
        float y = 0;
        y += polynomial.x * x;
        y += polynomial.y * x * x;
        y += polynomial.z * x * x * x;
        y += polynomial.w * x * x * x * x;
        startPos = new float2(x, y);

        float tan = 0;
        tan += 1 * polynomial.x;
        tan += 2 * polynomial.y * x;
        tan += 3 * polynomial.z * x * x;
        tan += 4 * polynomial.w * x * x * x;

        float2 vec = new float2(1, tan);
        float magnitude = math.sqrt(1 + tan * tan);
        nowCalculateVlc = vec / magnitude * speed;
    }

    public void Init(float2 _pos)
    {
        originPos = _pos;
        velocity = new(0, 0);
        angle = 0;
        time = 0;
        areaNum = 0;
        isActive = true;

        float x = startX;
        float y = 0;
        y += polynomial.x * x;
        y += polynomial.y * x * x;
        y += polynomial.z * x * x * x;
        y += polynomial.w * x * x * x * x;
        startPos = new float2(x, y);

        float tan = 0;
        tan += 1 * polynomial.x;
        tan += 2 * polynomial.y * x;
        tan += 3 * polynomial.z * x * x;
        tan += 4 * polynomial.w * x * x * x;

        float2 vec = new float2(1, tan);
        float magnitude = math.sqrt(1 + tan * tan);
        nowCalculateVlc = vec / magnitude * speed;
    }
}

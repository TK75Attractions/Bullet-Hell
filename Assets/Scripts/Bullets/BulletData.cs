using System;
using TMPro;
using Unity.Mathematics;
using UnityEngine.UIElements;

[Serializable]
public struct BulletData
{
    public const float DefaultAppearDuration = 1.2f;

    public float2 position;//弾の位置
    public float2 velocity;//弾の変位
    public float angle;//弾の角度

    public float2 originPos; //原点位置
    public float2 originVlc; //原点の移動速度
    public float2 playerInfluence;

    public float startX;
    public float speed; //弾丸の速度
    public float gravity;//弾丸にかかる重力加速度
    public float angleSpeed;//弾丸の角速度
    public float initialAngle;//弾丸の初期角度

    public float2 polarForm; //原点中心に回転させる虚数(r0,theta0);
    public float radiusVlc; //r の速さ R(t) = r0 + radiusVlc * t 
    public float thetaVlc; //theta の速さ Theta(t) = theta0 + thetaVlc * t

    public float2 startPos; //多項式の計算を始める x 座標（見かけの原点）
    public float2 nowCalculateVlc;//接線速度
    public float nowCalculateX;
    public float4 polynomial;//y = ax + bx^2 + cx^3 + dx^4 の係数

    public int typeId;
    public float2 scale;
    public float4 color;

    public int areaNum;
    public float time;
    public float appearTime;//弾幕を表示する時間、レーザーでは太さを指定
    public float appearDuration;//appearTime直前に演出を適用する時間
    public float life;
    public float random;
    public bool isActive;
    public bool isClearing;
    public float clearTime;
    public float clearDuration;
    public bool unCounterable;

    /// <summary>
    /// 弾幕のデータ
    /// </summary>
    /// <param name="_pos">原点座標 originPos</param>
    /// <param name="_vlc">原点速度 originVlc</param>
    /// <param name="_s">スピード speed</param>
    /// <param name="_g">重力加速度 gravity</param>
    /// <param name="_as">角速度 angleSpeed</param>
    /// <param name="_initialAngle">初期角度 initialAngle</param>
    /// <param name="_polar">極形式回転 polar</param>
    /// <param name="_absV">絶対値速度 radiusVlc</param>
    /// <param name="_theV">回転角速度 thetaVlc</param>
    /// <param name="_start">計算開始 x 座標 startValue</param>
    /// <param name="_poly">多項式係数 polynomial</param>
    /// <param name="type">弾のタイプID typeId</param>
    /// <param name="_scale">弾の表示スケール scale(x,y)</param>
    /// <param name="_color">弾の色 color</param>
    /// <param name="_random">ランダム値 random</param>
    /// <param name="_life">弾の寿命 life</param>
    /// <param name="_unCounterable">カウンター不可かどうか unCounterable</param>
    public BulletData(float2 _pos, float2 _vlc, float _s, float _g, float _as, float _initialAngle, float2 _polar, float _absV, float _theV, float _start, float4 _poly, int type, float4 _color, float2 _scale = default, float _random = 0, float _appear = 0, float _appearDuration = DefaultAppearDuration, float _life = 255, bool _unCounterable = false, float2 _playerInfluence = default)
    {
        position = _pos;
        velocity = new(0, 0);
        angle = 0;
        originPos = position;
        originVlc = _vlc;
        playerInfluence = _playerInfluence;
        speed = _s;
        gravity = _g;
        angleSpeed = _as;
        initialAngle = _initialAngle;
        polarForm = _polar;
        radiusVlc = _absV;
        thetaVlc = _theV;
        polynomial = _poly;
        nowCalculateX = _start;
        random = _random;
        appearTime = _appear;
        appearDuration = _appearDuration >= 0f ? _appearDuration : DefaultAppearDuration;
        life = _life;

        time = 0;
        typeId = type;
        areaNum = 0;
        scale = (_scale.x == 0f && _scale.y == 0f) ? new float2(1f, 1f) : _scale;
        isActive = true;
        isClearing = false;
        clearTime = 0f;
        clearDuration = 0f;
        color = _color;
        unCounterable = _unCounterable;

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

    public BulletData(BulletData data, float2 _pos, float2 _vlc, float _theta, float4 _color = new float4(), bool _unCounterable = false)
    {
        position = _pos;
        velocity = data.velocity;
        angle = data.angle;
        originPos = _pos + data.originPos;
        originVlc = _vlc + data.originVlc;
        playerInfluence = data.playerInfluence;
        speed = data.speed;
        gravity = data.gravity;
        angleSpeed = data.angleSpeed;
        initialAngle = data.initialAngle;
        polarForm = new float2(data.polarForm.x, data.polarForm.y + _theta);
        radiusVlc = data.radiusVlc;
        thetaVlc = data.thetaVlc;
        nowCalculateX = data.startX;
        polynomial = data.polynomial;
        typeId = data.typeId;
        scale = data.scale;
        color = new float4(data.color.x * _color.x, data.color.y * _color.y, data.color.z * _color.z, data.color.w * _color.w);
        appearTime = data.appearTime;
        appearDuration = data.appearDuration >= 0f ? data.appearDuration : DefaultAppearDuration;
        life = data.life;
        // Keep source flag when cloning; optional arg can force uncounterable.
        unCounterable = data.unCounterable || _unCounterable;

        areaNum = 0;
        time = 0;
        isActive = data.isActive;
        isClearing = false;
        clearTime = 0f;
        clearDuration = 0f;
        random = data.random;

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
        originPos = _pos + originPos;
        velocity = new(0, 0);
        angle = 0;
        time = 0;
        areaNum = 0;
        isActive = true;
        isClearing = false;
        clearTime = 0f;
        clearDuration = 0f;

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

    public void BeginClearFade(float duration)
    {
        isClearing = true;
        clearTime = 0f;
        clearDuration = duration > 0f ? duration : 0.0001f;
    }

    public float GetClearFadeFactor()
    {
        if (!isClearing || clearDuration <= 0f) return 1f;
        return math.saturate(1f - clearTime / clearDuration);
    }
}

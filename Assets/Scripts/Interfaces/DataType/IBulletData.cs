using System;
using Unity.Mathematics;

public interface IBulletData
{
    float2 position { get; } //弾の位置
    float2 velocity { get; } //弾の変位
    float angle { get; }

    float2 originPos { get; } //原点位置
    float2 originVlc { get; } //原点の移動速度
    float startX { get; }
    float speed { get; } //弾丸の速度
    float acccel { get; }//弾丸の加速
    float gravity { get; }//弾丸にかかる重力加速度
    float angleSpeed { get; }//弾丸の速度

    


    float2 polarForm { get; }//原点中心に回転させる虚数(r,t);
    float radiusVlc { get; }//r の速さ
    float thetaVlc { get; }//theta の速さ

    float2 startPos { get; }//多項式の計算を始める x 座標（見かけの原点）
    float2 nowCalculateVlc { get; }//接線速度
    float nowCalculateX { get; }
    float4 polynomial { get; }

    int typeId { get; }
    float size { get; }
    float4 color { get; }

    int areaNum { get; }
    float time { get; }
    float random { get; }
    bool isActive { get; }

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
    /// <param name="_random">ランダム値 random</param>

    public void Init(float2 _pos);

    void Initialize();
}

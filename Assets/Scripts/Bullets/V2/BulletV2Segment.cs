using System;
using Unity.Mathematics;

/// <summary>
/// v2 運動レーンのネイティブ区間(SPEC-RUNTIME-V2.md P1-a)。
/// 弾1発は最大 <see cref="BulletData.v2Segments"/> の Capacity 個までこの区間列を持ち、
/// 区間境界で位置が連続するようランタイム(<see cref="BulletV2UpdateJob"/>)が閉形式で積分する。
/// gravitySeq のような appearTime/life 連鎖による偽装(弾数3〜4倍)を置き換えるためのもの。
/// </summary>
[Serializable]
public struct BulletV2Segment
{
    /// <summary>区間の長さ(秒)。0以下は「最終区間として life まで継続」を意味する。</summary>
    public float duration;
    /// <summary>区間内の等速度成分(ワールド座標系、単位/秒)。thetaVlc で回転する基準ベクトル。</summary>
    public float2 vlc;
    /// <summary>区間内の等加速度(x=大きさ, y=方向ラジアン)。BulletData.gravity と同じ規約。</summary>
    public float2 gravity;
    /// <summary>vlc を区間内で連続回転させる角速度(rad/s)。0 なら直線、非0 なら弧を描く。</summary>
    public float thetaVlc;
}

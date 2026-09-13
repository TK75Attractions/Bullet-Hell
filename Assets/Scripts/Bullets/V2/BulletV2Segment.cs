using System;
using Unity.Mathematics;

/// <summary>
/// v2 運動レーンのネイティブ区間(SPEC-RUNTIME-V2.md P1-a)。
/// 弾1発は最大 <see cref="BulletData.v2Segments"/> の Capacity 個(v32 現在 18。easing 追加で 21 から減)までこの区間列を持ち、
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
    /// <summary>
    /// v32: 等速度成分の変位に掛けるイージング種別。
    /// 0=linear(従来。既定値なので既存データの挙動は不変) / 1=easeIn(3次) / 2=easeOut(3次) /
    /// 3=easeInOut(3次) / 4=smoothstep / 5=bounce(減衰3回の跳ね)。
    /// 変位は D(t) = 「時刻 T*E(t/T) までの等速度変位」、速度はその導関数で評価する
    /// (thetaVlc=0 のとき D(t) = vlc * T * E(t/T) に一致する)。
    /// 制約: duration<=0(無期限区間)では t/T が定義できないため linear 扱い。
    ///       gravity(等加速度)を持つ区間では gravity を優先し easing は無視する
    ///       (両立させると変位の分解が一意でなくなるため。著者側で併用しない前提)。
    /// </summary>
    public int easing;
}

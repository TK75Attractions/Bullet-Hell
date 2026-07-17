using System;
using UnityEngine;

/// <summary>
/// BulletBuffer JSON 上の <see cref="BulletV2Segment"/> 表現。全フィールド省略可(欠損は0)。
/// </summary>
[Serializable]
public class BulletV2SegmentJson
{
    public float duration;
    public Vector2 vlc;
    public Vector2 gravity;
    public float thetaVlc;

    public BulletV2Segment ToSegment()
    {
        return new BulletV2Segment
        {
            duration = duration,
            vlc = new Unity.Mathematics.float2(vlc.x, vlc.y),
            gravity = new Unity.Mathematics.float2(gravity.x, gravity.y),
            thetaVlc = thetaVlc
        };
    }
}

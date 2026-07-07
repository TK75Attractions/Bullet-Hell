using System;
using Unity.Mathematics;

[Serializable]
public class BulletBufferEmission
{
    public string clipName = "";
    public int index = -1;
    public float time = 0f;
    public float angleOffset = 0f;
    public bool applyBulletOrbit = false;
    public bool inheritSourceAngle = true;
    public bool inheritSourceVelocity = false;
    public bool deactivateSource = false;
    public float2 originVlc = new float2(0f, 0f);
    public float4 color = new float4(1f, 1f, 1f, 1f);

    public bool HasClipName => !string.IsNullOrWhiteSpace(clipName);
    public bool HasResolvedClip => index >= 0 || index == -3;
}

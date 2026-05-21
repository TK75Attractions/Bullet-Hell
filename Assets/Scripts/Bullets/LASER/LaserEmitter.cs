using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class LaserEmitter : MonoBehaviour
{
    public GameObject LASEROrigin;

    public List<LASER> EmitLASER(List<BulletData> data, float2 pPos)
    {
        List<LASER> laS = new();
        for (int i = 0; i < data.Count; i++)
        {
            BulletData d = data[i];
            LASER laser = Instantiate(LASEROrigin).GetComponent<LASER>();
            laser.transform.position = Vector3.zero;
            laser.AwakeSetting(pPos, d.originVlc, d.thetaVlc, d.speed, new float2(1, d.polarForm.y), d.startX, d.startPos, new float[4] { d.polynomial.x, d.polynomial.y, d.polynomial.z, d.polynomial.w }, d.size, d.appearTime, d.life, d.color, GManager.Control.QOrder.cellCount);
            laS.Add(laser);
        }
        return laS;
    }

    public List<LASER> EmitLASER(BulletClip clip, float2 pPos)
    {
        List<LASER> laS = new();
        float2 dis = new float2(GManager.Control.PController.pos.x, GManager.Control.PController.pos.y) - pPos;
        float rad = math.atan2(dis.y, dis.x);
        float4 p = clip.data.polynomial;
        float[] poly = new float[4] { p.x, p.y, p.z, p.w };

        if (clip.homing)
        {
            float range = (clip.number - 1) * clip.disRad / 2;

            for (int i = 0; i < clip.number; i++)
            {
                LASER laser = Instantiate(LASEROrigin).GetComponent<LASER>();
                laser.transform.position = Vector3.zero;
                laser.AwakeSetting(pPos, clip.data.originVlc, clip.data.thetaVlc, clip.data.speed, new float2(1, rad - range + clip.disRad * i), clip.data.startX, clip.data.startPos, poly, 4, clip.data.appearTime, clip.data.life, clip.data.color, GManager.Control.QOrder.cellCount);
                laS.Add(laser);
            }
        }
        else
        {
            float range = 2 * math.PI / clip.number;

            for (int i = 0; i < clip.number; i++)
            {
                LASER laser = Instantiate(LASEROrigin).GetComponent<LASER>();
                laser.transform.position = Vector3.zero;
                laser.AwakeSetting(pPos, clip.data.originVlc, clip.data.thetaVlc, clip.data.speed, new float2(1, range * i), clip.data.startX, clip.data.startPos, poly, 4, clip.data.appearTime, clip.data.life, clip.data.color, GManager.Control.QOrder.cellCount);
                laS.Add(laser);
            }
        }
        return laS;
    }
}

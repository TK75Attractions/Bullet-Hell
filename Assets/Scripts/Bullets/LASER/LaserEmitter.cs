using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class LaserEmitter : MonoBehaviour
{
    public GameObject LASEROrigin;

    public List<LASER> EmitLASER(List<BulletData> data)
    {
        List<LASER> laS = new();
        for (int i = 0; i < data.Count; i++)
        {
            BulletData d = data[i];
            LASER laser = Instantiate(LASEROrigin).GetComponent<LASER>();
            laser.transform.position = Vector3.zero;
            float laserScale = math.cmax(math.abs(d.scale));
            laser.AwakeSetting(d.originPos, d.originVlc, d.thetaVlc, d.speed, new float2(1, d.polarForm.y), d.startX, d.startPos, new float[4] { d.polynomial.x, d.polynomial.y, d.polynomial.z, d.polynomial.w }, laserScale, d.appearTime, d.life, d.color, GManager.Control.QOrder.cellCount);
            laS.Add(laser);
        }
        return laS;
    }

}

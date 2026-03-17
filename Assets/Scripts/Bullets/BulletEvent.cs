using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class BulletEvent
{
    /// <summary>
    /// �e���̃X�v���C�g
    /// </summary>
    public int type = 0;
    public List<Transform> bullets = new();
    public List<BulletClip> clips = new();
    public List<float> times = new();

    public bool Update(float dt, out BulletEvent eve)
    {
        eve = null;
        if (times.Count == 0 || clips.Count == 0) return true;

        times[0] -= dt;
        if (times[0] > 0) return false;
        else
        {
            if (clips.Count == 0) Debug.Log("NO");
            List<Transform> child = new();
            if (bullets.Count == 0)
            {
                //child.AddRange(GManager.Control.ShootingManager.BOrder.Generate(type, clips[0], clips[0].data.originPos));
            }
            else
            {
                for (int i = 0; i < bullets.Count; i++)
                {
                    Vector2 v = bullets[i].position;
                    //child.AddRange(GManager.Control.ShootingManager.BOrder.Generate(type, clips[0], new float2(v.x, v.y)));
                }
            }

            if (times.Count == 1 || clips.Count == 1) { }
            else
            {
                List<BulletClip> temp = clips.GetRange(1, clips.Count - 1);
                List<float> vs = times.GetRange(1, times.Count - 1);
                eve = new BulletEvent()
                {
                    bullets = child,
                    type = type,
                    clips = temp,
                    times = vs
                };
            }

            //GManager.Control.ShootingManager.BOrder.Destroy(bullets);

            return true;
        }
    }
}
using UnityEngine;
using BulletHell.Core;
using System.Collections.Generic;

namespace BulletHell.App
{
    public class UpdateService : MonoBehaviour
    {
        List<IUpdatable> _targets = new();
        List<ILateUpdatable> _lateTargets = new();
        bool ready = false;
        public float gameTime = 0;

        public void Register(IUpdatable target)
        {
            if(_targets.Contains(target) || target == null) return;
            _targets.Add(target);
        }

        public void LateRegister(ILateUpdatable target)
        {
            if(_lateTargets.Contains(target) || target == null) return;
            _lateTargets.Add(target);
        }

        public void SetReady()
        {
            ready = true;
        }

        public void Update()
        {
            if (!ready) return;
            float t = Time.deltaTime;
            gameTime += t;

            for (int i = 0; i < _targets.Count; i++)
            {
                _targets[i].Tick(t);
            }
        }

        public void LateUpdate()
        {
            if (!ready) return;

            for (int i = 0; i < _lateTargets.Count; i++)
            {
                _lateTargets[i].LateTick();
            }
        }
    }
}
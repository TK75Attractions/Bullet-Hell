using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BulletEvent
{
    public List<int> bullets = new();
    [SerializeField] private List<Transition> transitions = new();

    [Serializable]
    private class Transition
    {
        public BulletData bulletData = new();
        public float time = 0;
    }

    public bool Evoke()
    {
        Shot();
        return transitions.Count == 0;
    }

    public bool Update(float dt)
    {
        if (transitions.Count == 0) return true;

        transitions[0].time -= dt;
        if (transitions[0].time > 0) return false;
        else
        {
            Shot();
            transitions.RemoveAt(0);
            return transitions.Count == 0;
        }
    }

    private void Shot()
    {
        List<int> child = new();
        if (bullets.Count == 0)
        {
            return;
        }
        else
        {
            for (int i = 0; i < bullets.Count; i++)
            {
                int index = bullets[i];
                if (index < 0 || index >= GManager.Control.QOrder.GetEnemyBulletCount())
                {
                    Debug.LogError($"Bullet index {index} is out of range.");
                    continue;
                }
            }
        }

    }
}
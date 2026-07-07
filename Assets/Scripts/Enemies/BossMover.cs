using System;
using System.Collections.Generic;
using UnityEngine;

public class BossMover : MonoBehaviour
{
    private readonly List<BossMoveEvent> moves = new List<BossMoveEvent>();
    private int eventIndex;
    private Vector2 velocity;
    private ActiveMove activeMove;

    private class ActiveMove
    {
        public BossMoveType type;
        public Vector2 from;
        public Vector2 to;
        public Vector2 control;
        public float duration;
        public float elapsed;
        public string easing;
    }

    public void Init(IEnumerable<BossMoveEvent> moveEvents)
    {
        moves.Clear();
        eventIndex = 0;
        velocity = Vector2.zero;
        activeMove = null;

        if (moveEvents != null)
        {
            foreach (BossMoveEvent moveEvent in moveEvents)
            {
                if (moveEvent != null)
                {
                    moves.Add(moveEvent);
                }
            }
        }

        moves.Sort((a, b) => a.time.CompareTo(b.time));
    }

    public void UpdateMover(float dt, float elapsedTime)
    {
        while (eventIndex < moves.Count && moves[eventIndex].time <= elapsedTime)
        {
            ApplyEvent(moves[eventIndex]);
            eventIndex++;
        }

        if (activeMove != null)
        {
            UpdateActiveMove(dt);
        }
        else if (velocity != Vector2.zero)
        {
            Vector3 position = transform.position;
            position.x += velocity.x * dt;
            position.y += velocity.y * dt;
            transform.position = position;
        }
    }

    private void ApplyEvent(BossMoveEvent moveEvent)
    {
        Vector2 current = transform.position;
        Vector2 target = moveEvent.relative ? current + moveEvent.to : moveEvent.to;

        switch (moveEvent.type)
        {
            case BossMoveType.SetPosition:
                activeMove = null;
                SetPosition(target);
                break;
            case BossMoveType.MoveTo:
            case BossMoveType.BezierTo:
                if (moveEvent.duration <= 0f)
                {
                    activeMove = null;
                    SetPosition(target);
                    break;
                }

                activeMove = new ActiveMove
                {
                    type = moveEvent.type,
                    from = current,
                    to = target,
                    control = moveEvent.relative ? current + moveEvent.control : moveEvent.control,
                    duration = moveEvent.duration,
                    elapsed = 0f,
                    easing = moveEvent.easing
                };
                break;
            case BossMoveType.AddVelocity:
                velocity = moveEvent.to;
                break;
            case BossMoveType.Stop:
                velocity = Vector2.zero;
                activeMove = null;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void UpdateActiveMove(float dt)
    {
        activeMove.elapsed += dt;
        float t = Mathf.Clamp01(activeMove.elapsed / activeMove.duration);
        float eased = Ease(t, activeMove.easing);

        Vector2 position = activeMove.type == BossMoveType.BezierTo
            ? QuadraticBezier(activeMove.from, activeMove.control, activeMove.to, eased)
            : Vector2.LerpUnclamped(activeMove.from, activeMove.to, eased);

        SetPosition(position);

        if (activeMove.elapsed >= activeMove.duration)
        {
            SetPosition(activeMove.to);
            activeMove = null;
        }
    }

    private void SetPosition(Vector2 position)
    {
        transform.position = new Vector3(position.x, position.y, transform.position.z);
    }

    private static Vector2 QuadraticBezier(Vector2 from, Vector2 control, Vector2 to, float t)
    {
        float oneMinusT = 1f - t;
        return oneMinusT * oneMinusT * from + 2f * oneMinusT * t * control + t * t * to;
    }

    private static float Ease(float t, string easing)
    {
        if (string.IsNullOrWhiteSpace(easing))
        {
            return t;
        }

        switch (easing.Trim().ToLowerInvariant())
        {
            case "easeincubic":
                return t * t * t;
            case "easeoutcubic":
                float oneMinusT = 1f - t;
                return 1f - oneMinusT * oneMinusT * oneMinusT;
            case "easeinoutcubic":
                return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
            case "easeinoutsine":
                return -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;
            case "linear":
            default:
                return t;
        }
    }
}

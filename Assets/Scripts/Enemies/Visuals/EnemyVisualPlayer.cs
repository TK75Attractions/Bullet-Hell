using System.Collections.Generic;
using UnityEngine;

public class EnemyVisualPlayer
{
    private SpriteRenderer spriteRenderer;
    private EnemyVisualSetRuntime visualSet;
    private EnemyAnimationPlan animationPlan;
    private EnemyVisualClipRuntime currentClip;
    private string currentNextOverride = "";
    private bool hasLoopOverride;
    private bool loopOverride;
    private int frameIndex;
    private int eventIndex;
    private float frameTimer;
    // 石工 v34: コマ停止。holdFrame へ達したらそこで止まり、resume イベントが来るまで進まない。
    // 既定は無効（holdActive=false）なので、hold を書かない既存の敵・ボスの挙動は不変。
    private bool holdActive;
    private int holdFrame;
    private List<EnemyAnimationEventData> sortedEvents = new List<EnemyAnimationEventData>();

    public void Init(SpriteRenderer renderer, EnemyVisualSetRuntime set, EnemyAnimationPlan plan, Sprite fallbackSprite)
    {
        spriteRenderer = renderer;
        visualSet = set;
        animationPlan = plan;
        frameIndex = 0;
        eventIndex = 0;
        frameTimer = 0f;
        holdActive = false;
        holdFrame = 0;
        currentClip = null;
        currentNextOverride = "";
        hasLoopOverride = false;
        sortedEvents = new List<EnemyAnimationEventData>();

        if (animationPlan != null && animationPlan.events != null)
        {
            sortedEvents.AddRange(animationPlan.events);
            sortedEvents.RemoveAll(animationEvent => animationEvent == null);
            sortedEvents.Sort((a, b) => a.time.CompareTo(b.time));
        }

        if (visualSet == null)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = fallbackSprite;
            }
            return;
        }

        if (fallbackSprite == null)
        {
            fallbackSprite = visualSet.fallbackSprite;
        }

        string initialClip = animationPlan != null ? animationPlan.initialClip : "";
        if (!PlayClip(initialClip))
        {
            EnemyVisualClipRuntime defaultClip = visualSet.GetDefaultClip();
            if (defaultClip != null)
            {
                SetClip(defaultClip, "", false, false);
            }
            else if (spriteRenderer != null)
            {
                spriteRenderer.sprite = fallbackSprite;
            }
        }
    }

    public void Update(float dt, float elapsedTime)
    {
        if (visualSet == null)
        {
            return;
        }

        while (eventIndex < sortedEvents.Count && sortedEvents[eventIndex].time <= elapsedTime)
        {
            EnemyAnimationEventData animationEvent = sortedEvents[eventIndex];
            if (animationEvent != null)
            {
                if (animationEvent.resume)
                {
                    // 止めていたコマから続きを再生する（クリップは差し替えない）。
                    holdActive = false;
                    frameTimer = 0f;
                }
                if (!string.IsNullOrWhiteSpace(animationEvent.clip)
                    && PlayClip(animationEvent.clip, animationEvent.next, animationEvent.overrideLoop, animationEvent.loop))
                {
                    holdActive = animationEvent.hold;
                    holdFrame = animationEvent.holdFrame;
                }
            }
            eventIndex++;
        }

        UpdateFrames(dt);
    }

    public void Trigger(string triggerName)
    {
        if (string.IsNullOrWhiteSpace(triggerName) || animationPlan == null || animationPlan.triggers == null)
        {
            return;
        }

        for (int i = 0; i < animationPlan.triggers.Count; i++)
        {
            EnemyAnimationTriggerData trigger = animationPlan.triggers[i];
            if (trigger == null || !string.Equals(trigger.trigger, triggerName, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            PlayClip(trigger.clip, trigger.next, trigger.overrideLoop, trigger.loop);
            return;
        }
    }

    private bool PlayClip(string clipName)
    {
        return PlayClip(clipName, "", false, false);
    }

    private bool PlayClip(string clipName, string nextOverride, bool overrideLoop, bool loop)
    {
        if (visualSet == null || !visualSet.TryGetClip(clipName, out EnemyVisualClipRuntime clip))
        {
            return false;
        }

        SetClip(clip, nextOverride, overrideLoop, loop);
        return true;
    }

    private void SetClip(EnemyVisualClipRuntime clip, string nextOverride, bool overrideLoop, bool loop)
    {
        currentClip = clip;
        currentNextOverride = nextOverride;
        hasLoopOverride = overrideLoop;
        loopOverride = loop;
        frameIndex = 0;
        frameTimer = 0f;
        ApplyFrame();
    }

    private void UpdateFrames(float dt)
    {
        if (currentClip == null || currentClip.frames == null || currentClip.frames.Length <= 1)
        {
            return;
        }

        if (holdActive && frameIndex >= holdFrame)
        {
            // 指定コマに達したらそこで固定する（resume が来るまで進めない）。
            frameTimer = 0f;
            return;
        }

        frameTimer += dt;
        while (frameTimer >= currentClip.GetFrameDuration(frameIndex))
        {
            frameTimer -= currentClip.GetFrameDuration(frameIndex);
            frameIndex++;

            if (frameIndex >= currentClip.frames.Length)
            {
                bool shouldLoop = hasLoopOverride ? loopOverride : currentClip.loop;
                if (shouldLoop)
                {
                    frameIndex = 0;
                }
                else
                {
                    string nextClipName = string.IsNullOrWhiteSpace(currentNextOverride) ? currentClip.next : currentNextOverride;
                    if (!string.IsNullOrWhiteSpace(nextClipName) && PlayClip(nextClipName))
                    {
                        return;
                    }

                    frameIndex = currentClip.frames.Length - 1;
                    frameTimer = 0f;
                    break;
                }
            }

            ApplyFrame();

            if (holdActive && frameIndex >= holdFrame)
            {
                frameTimer = 0f;
                return;
            }
        }
    }

    private void ApplyFrame()
    {
        if (spriteRenderer == null || currentClip == null || currentClip.frames == null || currentClip.frames.Length == 0)
        {
            return;
        }

        int safeIndex = Mathf.Clamp(frameIndex, 0, currentClip.frames.Length - 1);
        spriteRenderer.sprite = currentClip.frames[safeIndex];
    }
}

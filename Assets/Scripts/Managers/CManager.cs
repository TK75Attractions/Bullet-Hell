using System.Collections.Generic;
using UnityEngine;

public class CManager : MonoBehaviour
{
    public static CManager Current { get; private set; }

    [SerializeField] private float noiseFrequency = 30f;
    [SerializeField] private float blurPixelsPerScaleUnit = 6f;
    [SerializeField] private int debugScheduledNoiseCount;
    [SerializeField] private int debugActiveNoiseCount;
    [SerializeField] private Vector2 debugCurrentBlurPixels;
    [SerializeField] private Vector2 debugCurrentJitterPixels;

    private readonly List<ScreenNoise> screenNoises = new List<ScreenNoise>();
    private bool menuBlurActive;

    public Vector2 CurrentBlurPixels { get; private set; }
    public Vector2 CurrentJitterPixels { get; private set; }
    public float CurrentStrength { get; private set; }
    public bool HasScreenNoise => CurrentStrength > 0.0001f;

    private class ScreenNoise
    {
        public float delay;
        public float duration;
        public float elapsed;
        public Vector2 amplitude;
        public float seed;
    }

    private void Awake()
    {
        if (Current == null || Current == this)
        {
            Current = this;
        }
    }

    private void OnDestroy()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

    private void Update()
    {
        UpdateScreenNoise(Time.deltaTime);
    }

    private void OnDisable()
    {
        screenNoises.Clear();
        menuBlurActive = false;
        CurrentBlurPixels = Vector2.zero;
        CurrentJitterPixels = Vector2.zero;
        CurrentStrength = 0f;
        debugScheduledNoiseCount = 0;
        debugActiveNoiseCount = 0;
        debugCurrentBlurPixels = Vector2.zero;
        debugCurrentJitterPixels = Vector2.zero;
    }

    public void StartScreenNoise(BulletData bullet)
    {
        float delay = Mathf.Max(0f, bullet.appearTime);
        float duration = bullet.life - delay;
        if (duration <= 0f)
        {
            return;
        }

        screenNoises.Add(new ScreenNoise
        {
            delay = delay,
            duration = duration,
            elapsed = 0f,
            amplitude = new Vector2(Mathf.Abs(bullet.scale.x), Mathf.Abs(bullet.scale.y)) * blurPixelsPerScaleUnit,
            seed = Time.time * 97.31f + screenNoises.Count * 13.17f
        });
        debugScheduledNoiseCount = screenNoises.Count;
    }

    public void StopScreenNoise()
    {
        screenNoises.Clear();
        CurrentBlurPixels = Vector2.zero;
        CurrentJitterPixels = Vector2.zero;
        CurrentStrength = 0f;
        debugScheduledNoiseCount = 0;
        debugActiveNoiseCount = 0;
        debugCurrentBlurPixels = Vector2.zero;
        debugCurrentJitterPixels = Vector2.zero;
    }

    public void SetMenuBlur(bool enabled)
    {
        menuBlurActive = enabled;
        UpdateScreenNoise(0f);
    }

    private void UpdateScreenNoise(float dt)
    {
        Vector2 blurPixels = Vector2.zero;
        Vector2 jitterPixels = Vector2.zero;
        float strength = 0f;
        int activeNoiseCount = 0;

        for (int i = 0; i < screenNoises.Count; i++)
        {
            ScreenNoise noise = screenNoises[i];
            noise.elapsed += dt;

            float activeTime = noise.elapsed - noise.delay;
            if (activeTime < 0f)
            {
                continue;
            }

            if (activeTime >= noise.duration)
            {
                screenNoises.RemoveAt(i);
                i--;
                continue;
            }

            float envelope = GetEnvelope(activeTime, noise.duration);
            activeNoiseCount++;
            float sampleTime = activeTime * noiseFrequency;
            float x = Mathf.PerlinNoise(noise.seed, sampleTime) * 2f - 1f;
            float y = Mathf.PerlinNoise(noise.seed + 41.37f, sampleTime) * 2f - 1f;

            blurPixels += noise.amplitude * envelope;
            jitterPixels += new Vector2(x * noise.amplitude.x, y * noise.amplitude.y) * envelope;
            strength = Mathf.Max(strength, envelope);
        }

        if (menuBlurActive)
        {
            blurPixels = new Vector2(18f, 10f);
            jitterPixels = Vector2.zero;
            strength = 1f;
        }

        CurrentBlurPixels = blurPixels;
        CurrentJitterPixels = jitterPixels;
        CurrentStrength = Mathf.Clamp01(strength);
        debugScheduledNoiseCount = screenNoises.Count;
        debugActiveNoiseCount = activeNoiseCount;
        debugCurrentBlurPixels = CurrentBlurPixels;
        debugCurrentJitterPixels = CurrentJitterPixels;
    }

    private float GetEnvelope(float activeTime, float duration)
    {
        const float fadeTime = 0.08f;
        if (duration <= fadeTime * 2f)
        {
            return 1f;
        }

        float fadeIn = Mathf.Clamp01(activeTime / fadeTime);
        float fadeOut = Mathf.Clamp01((duration - activeTime) / fadeTime);
        return Mathf.Min(fadeIn, fadeOut);
    }
}

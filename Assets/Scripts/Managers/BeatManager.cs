using System;
using System.Collections.Generic;
using UnityEngine;

public class BeatManager : MonoBehaviour
{
    [SerializeField] private readonly List<int> beatSamples = new();
    [SerializeField] private float beatValueSin;
    [SerializeField] private float beatValuePoly;
    private bool ready = false;
    private int beatCount = 0;
    private int nextBeatSample = 0;
    private AudioClip musicClip;
    private double startDspTime;
    private AudioSource audioSource;
    [SerializeField] private int offsetSamples = 0;
    [SerializeField] private float debug;
    [SerializeField] private float toleranceTime = 0.5f; // Time window for beat detection

    public float BeatValueSin => beatValueSin;
    public float BeatValuePoly => beatValuePoly;
    public event Action<int> BeatTriggered;

    public void SetBeat(AudioSource bgmSource, AudioClip clip, List<StageData.MusicEvent> musicEvents, double scheduledDspTime, float delayTime)
    {
        ResetState();
        audioSource = bgmSource;
        musicClip = clip;
        startDspTime = scheduledDspTime;

        if (musicClip == null || musicEvents == null || musicEvents.Count == 0)
        {
            ready = false;
            return;
        }

        offsetSamples = Mathf.RoundToInt(delayTime * musicClip.frequency);
        HashSet<int> uniqueBeatSamples = new();

        float end = 0;
        for (int i = 0; i < musicEvents.Count; i++)
        {
            StageData.MusicEvent musicEvent = musicEvents[i];
            if (!TryGetNormalizedBeatTimings(musicEvent, out List<int> normalizedBeatTimings)) continue;

            if (musicEvent.BPM <= 0f || musicEvent.measure <= 0 || musicEvent.barCount <= 0)
            {
                Debug.LogWarning($"Invalid MusicEvent was skipped. BPM={musicEvent.BPM}, measure={musicEvent.measure}, barCount={musicEvent.barCount}");
                continue;
            }

            float beatInterval = 60f / musicEvent.BPM;
            float barStartOffsetSeconds = musicEvent.barStartOffsetBeats * beatInterval;
            List<float> beatTimesInMeasure = new(normalizedBeatTimings.Count);
            for (int k = 0; k < normalizedBeatTimings.Count; k++)
            {
                int beatTiming = normalizedBeatTimings[k];
                float beatTime = end + beatTiming * beatInterval + barStartOffsetSeconds;
                beatTimesInMeasure.Add(beatTime);
            }

            for (int k = 0; k < musicEvent.barCount; k++)
            {
                for (int j = 0; j < beatTimesInMeasure.Count; j++)
                {
                    float beatTime = beatTimesInMeasure[j] + k * musicEvent.measure * beatInterval;
                    int beatSample = Mathf.RoundToInt(beatTime * musicClip.frequency);
                    uniqueBeatSamples.Add(beatSample);
                }
            }
            end += musicEvent.barCount * musicEvent.measure * beatInterval;
        }

        beatSamples.AddRange(uniqueBeatSamples);
        beatSamples.Sort();
        ready = beatSamples.Count > 0;
        if (ready) nextBeatSample = beatSamples[0];
    }

    private void ResetState()
    {
        beatSamples.Clear();
        beatCount = 0;
        nextBeatSample = 0;
        beatValueSin = 0f;
        beatValuePoly = 0f;
        ready = false;
    }

    private static bool TryGetNormalizedBeatTimings(StageData.MusicEvent musicEvent, out List<int> normalizedBeatTimings)
    {
        normalizedBeatTimings = null;
        if (musicEvent == null || musicEvent.beatTimings == null || musicEvent.beatTimings.Count == 0) return false;

        HashSet<int> uniqueTimings = new();
        for (int i = 0; i < musicEvent.beatTimings.Count; i++)
        {
            int timing = musicEvent.beatTimings[i];
            if (timing < 0 || timing >= musicEvent.measure) continue;
            uniqueTimings.Add(timing);
        }

        if (uniqueTimings.Count == 0) return false;

        normalizedBeatTimings = new List<int>(uniqueTimings);
        normalizedBeatTimings.Sort();
        return true;
    }

    public void UpdateBeat()
    {
        if (!ready) return;

        int currentSample = GetCurrentSample();

        while (beatCount < beatSamples.Count && currentSample >= nextBeatSample)
        {
            OnBeat();
            beatCount++;
            if (beatCount < beatSamples.Count) nextBeatSample = beatSamples[beatCount];
        }

        ValueUpdate(currentSample);
    }

    private int GetCurrentSample()
    {
        int debugSamples = musicClip == null ? 0 : Mathf.RoundToInt(debug * musicClip.frequency);

        // Use AudioSource.timeSamples for accurate playback position tracking
        // This ensures beat calculation matches actual audio playback
        if (audioSource != null && audioSource.isPlaying && musicClip != null)
        {
            return audioSource.timeSamples + debugSamples - offsetSamples;
        }

        // Fallback to DSP time calculation if AudioSource is not available
        double elapsedTime = AudioSettings.dspTime - startDspTime;
        if (elapsedTime <= 0 || musicClip == null) return debugSamples - offsetSamples;

        int elapsedSamples = Mathf.RoundToInt((float)(elapsedTime * musicClip.frequency));
        return elapsedSamples + debugSamples - offsetSamples;
    }

    private void OnBeat()
    {
        BeatTriggered?.Invoke(beatCount);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        //Debug.Log($"Beat! {beatCount}");
#endif
    }

    private void ValueUpdate(int currentSample)
    {
        if (beatCount == 0 || beatSamples.Count == 0) return;
        if (beatCount > beatSamples.Count) beatCount = beatSamples.Count;

        int toleranceSamples = musicClip == null ? 0 : Mathf.RoundToInt(toleranceTime * musicClip.frequency);
        if (toleranceSamples <= 0)
        {
            beatValueSin = 0;
            beatValuePoly = 0;
            return;
        }

        int previousBeatSample = beatSamples[beatCount - 1];
        int nextSampleDistance = beatCount < beatSamples.Count ? beatSamples[beatCount] - currentSample : int.MaxValue;
        int previousSampleDistance = currentSample - previousBeatSample;

        if (previousSampleDistance > toleranceSamples && (nextSampleDistance > toleranceSamples || nextSampleDistance < 0))
        {
            beatValueSin = 0;
            beatValuePoly = 0;
            return;
        }

        int nearestSampleDistance = Mathf.Min(previousSampleDistance, nextSampleDistance);
        float normalizedDistance = nearestSampleDistance / (float)toleranceSamples;

        if (nearestSampleDistance < toleranceSamples * 0.1f) beatValueSin = 1;
        else if (nearestSampleDistance < toleranceSamples) beatValueSin = Mathf.Sin((1 - normalizedDistance) * Mathf.PI / 2);
        else beatValueSin = 0;

        if (nearestSampleDistance < toleranceSamples) beatValuePoly = 1 - normalizedDistance * normalizedDistance;
        else beatValuePoly = 0;
    }

    /// <summary>
    /// Get the current beat timing offset in samples (negative = ahead, positive = behind)
    /// </summary>
    public int GetBeatOffsetSamples()
    {
        if (!ready || beatSamples.Count == 0) return 0;

        int currentSample = GetCurrentSample();
        int previousDistance = int.MaxValue;
        int nextDistance = int.MaxValue;

        if (beatCount > 0)
        {
            int previousBeatSample = beatSamples[beatCount - 1];
            previousDistance = currentSample - previousBeatSample;
        }

        if (beatCount < beatSamples.Count)
        {
            int nextBeatSampleLocal = beatSamples[beatCount];
            nextDistance = nextBeatSampleLocal - currentSample;
        }

        if (Mathf.Abs(previousDistance) <= Mathf.Abs(nextDistance)) return previousDistance;
        return -nextDistance;
    }

    /// <summary>
    /// Get the current beat timing offset in seconds (negative = ahead, positive = behind)
    /// </summary>
    public float GetBeatOffsetSeconds()
    {
        if (musicClip == null) return 0f;
        return GetBeatOffsetSamples() / (float)musicClip.frequency;
    }

    /// <summary>
    /// Get the current beat timing offset in milliseconds (negative = ahead, positive = behind)
    /// </summary>
    public float GetBeatOffsetMs()
    {
        return GetBeatOffsetSeconds() * 1000f;
    }

    /// <summary>
    /// Get diagnostic info about current beat state and timing
    /// </summary>
    public string GetDiagnosticInfo()
    {
        if (!ready) return "[BeatManager] Not ready";

        int currentSample = GetCurrentSample();
        float offsetMs = GetBeatOffsetMs();
        int previousSampleDelta = beatCount > 0 ? currentSample - beatSamples[beatCount - 1] : 0;
        int nextBeatIdx = beatCount < beatSamples.Count ? beatCount : beatSamples.Count - 1;
        int nextBeatSample = nextBeatIdx < beatSamples.Count ? beatSamples[nextBeatIdx] : 0;
        int samplesUntilNextBeat = nextBeatSample - currentSample;
        float timeUntilNextBeat = musicClip != null ? samplesUntilNextBeat / (float)musicClip.frequency : 0f;
        float previousAgoMs = musicClip != null && beatCount > 0 ? previousSampleDelta * 1000f / musicClip.frequency : 0f;

        return $"[BeatManager] " +
               $"Beat={beatCount}/{beatSamples.Count} " +
               $"Offset={offsetMs:F2}ms " +
               $"PrevAgo={previousAgoMs:F2}ms " +
               $"NextIn={timeUntilNextBeat:F3}s " +
               $"Samples={currentSample}/{(musicClip != null ? musicClip.samples : 0)}";
    }
}

using System.Collections.Generic;
using UnityEngine;
using BulletHell.Core;

namespace BulletHell.Audio
{
    public class BeatManager : MonoBehaviour, IUpdatable
    {
        private IUserSettingService userSetting;
        
        [SerializeField] private readonly List<int> beatSamples = new();
        public float beatValueSin;
        public float beatValuePoly;
        private bool ready = false;
        private int beatCount = 0;
        private int nextBeatSample = 0;
        private AudioClip musicClip;
        private double startDspTime;
        [SerializeField] private int offsetSamples = 0;
        [SerializeField] private float debug;
        [SerializeField] private float toleranceTime = 0.5f; // Time window for beat detection

        public void Init(IUserSettingService userSetting)
        {
            this.userSetting = userSetting;
        }

        public void SetBeat(AudioClip clip, List<IMusicEvent> musicEvents, double scheduledDspTime, float delayTime)
        {
            beatSamples.Clear();
            beatCount = 0;
            nextBeatSample = 0;
            beatValueSin = 0;
            beatValuePoly = 0;
            musicClip = clip;
            startDspTime = scheduledDspTime;

            if (musicClip == null)
            {
                ready = false;
                return;
            }

            offsetSamples = Mathf.RoundToInt(delayTime * musicClip.frequency);
            for (int i = 0; i < musicEvents.Count; i++) musicEvents[i].Refresh();

            for (int i = 0; i < musicEvents.Count; i++)
            {
                if (musicEvents[i].GetbeatTimings().Count == 0) continue;

                float beatInterval = 60f / musicEvents[i].GetBPM();
                List<float> temp = new List<float>();
                for (int k = 0; k < musicEvents[i].GetbeatTimings().Count; k++)
                {
                    int beatTiming = musicEvents[i].GetbeatTimings()[k];
                    float beatTime = beatTiming * beatInterval;
                    temp.Add(beatTime);
                }

                for (int k = 0; k < musicEvents[i].GetbarCount(); k++)
                {
                    for (int j = 0; j < temp.Count; j++)
                    {
                        float beatTime = temp[j] + k * musicEvents[i].Getmeasure() * beatInterval;
                        int beatSample = Mathf.RoundToInt(beatTime * musicClip.frequency) - offsetSamples;
                        beatSamples.Add(beatSample);
                    }
                }
            }

            beatSamples.Sort();
            //foreach (int beatSample in beatSamples) Debug.Log($"Beat sample: {beatSample}");
            ready = beatSamples.Count > 0;
            if (ready) nextBeatSample = beatSamples[0];
        }

        public void Tick(float dt)
        {
            if (!ready || !userSetting.GetMusicOn()) return;

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
            double elapsedTime = AudioSettings.dspTime - startDspTime;
            int debugSamples = musicClip == null ? 0 : Mathf.RoundToInt(debug * musicClip.frequency);
            if (elapsedTime <= 0 || musicClip == null) return debugSamples - offsetSamples;

            int elapsedSamples = Mathf.RoundToInt((float)(elapsedTime * musicClip.frequency));
            return elapsedSamples + debugSamples - offsetSamples;
        }

        private void OnBeat()
        {
            Debug.Log($"Beat! {beatCount}");
        }

        private void ValueUpdate(int currentSample)
        {
            if (beatCount == 0) return;

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
    }
}
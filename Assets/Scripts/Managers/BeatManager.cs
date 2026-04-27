using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;
using NUnit.Framework.Constraints;

public class BeatManager : MonoBehaviour
{
    private List<float> beatTimings; // List of beat timings in seconds
    public float beatValueSin;
    public float beatValuePoly;
    private bool ready = false;
    private int beatCount = 0;
    private float nextBeatTime = 0;
    [SerializeField] private float debug;
    [SerializeField] private float toleranceTime = 0.5f; // Time window for beat detection

    public void SetBeat(List<StageData.MusicEvent> musicEvents)
    {
        beatTimings = new List<float>();
        for (int i = 0; i < musicEvents.Count; i++) musicEvents[i].Refresh();

        for (int i = 0; i < musicEvents.Count; i++)
        {
            if (musicEvents[i].beatTimings.Count == 0) continue;

            float beatInterval = 60f / musicEvents[i].BPM;
            List<float> temp = new List<float>();
            for (int k = 0; k < musicEvents[i].beatTimings.Count; k++)
            {
                int beatTiming = musicEvents[i].beatTimings[k];
                float beatTime = beatTiming * beatInterval;
                temp.Add(beatTime);
            }

            for (int k = 0; k < musicEvents[i].barCount; k++)
            {
                for (int j = 0; j < temp.Count; j++)
                {
                    float beatTime = temp[j] + k * musicEvents[i].measure * beatInterval;
                    beatTimings.Add(beatTime);
                }
            }
        }

        beatTimings.Sort();
        foreach (float beatTime in beatTimings) Debug.Log($"Beat timing: {beatTime}");
        if (beatTimings.Count > 0) nextBeatTime = beatTimings[0];
        ready = true;
    }

    public void UpdateBeat()
    {
        if (!ready) return;

        float bt = GManager.Control.beatTime;
        bt += debug;

        if (beatCount < beatTimings.Count && bt >= nextBeatTime)
        {
            OnBeat();
            beatCount++;
            if (beatCount < beatTimings.Count) nextBeatTime = beatTimings[beatCount];
        }

        ValueUpdate(bt);
    }

    private void OnBeat()
    {
        Debug.Log($"Beat! {beatCount}");
    }

    private void ValueUpdate(float bt)
    {
        if (beatCount == 0) return;

        float pre = bt - beatTimings[beatCount - 1];
        float next = nextBeatTime - bt;
        if (pre > toleranceTime && (next > toleranceTime || next < 0))
        {
            beatValueSin = 0;
            beatValuePoly = 0;
            return;
        }

        float f = Mathf.Min(pre, next);

        if (f < toleranceTime * 0.1f) beatValueSin = 1;
        else if (f < toleranceTime) beatValueSin = Mathf.Sin((1 - f / toleranceTime) * Mathf.PI / 2);
        else beatValueSin = 0;

        if (f < toleranceTime) beatValuePoly = (-f * f + toleranceTime * toleranceTime) / (toleranceTime * toleranceTime);
        else beatValuePoly = 0;
    }
}

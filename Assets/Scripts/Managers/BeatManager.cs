using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;

public class BeatManager : MonoBehaviour
{
    private List<float> beatTimings; // List of beat timings in seconds
    private bool ready = false;
    private int beatCount = 0;

    public void Init(List<StageData.MusicEvent> musicEvents)
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
        ready = true;
    }

    public void UpdateBeat()
    {
        if (!ready) return;

        float bt = GManager.Control.beatTime;

        if (beatCount < beatTimings.Count && bt >= beatTimings[beatCount])
        {
            OnBeat();
            beatCount++;
        }

    }

    private void OnBeat()
    {
        Debug.Log($"Beat! {beatCount}");
    }
}

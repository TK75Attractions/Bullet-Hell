using System;
using System.Collections.Generic;

[Serializable]
public class MusicEvent
{
    public int barCount;
    public float BPM;
    public List<int> beatTimings;
    public int measure;

    public void Refresh()
    {
        List<int> newBeatTimings = new List<int>();
        for (int i = 0; i < beatTimings.Count; i++)
        {
            if (beatTimings[i] < measure)
            {
                if (newBeatTimings.Contains(beatTimings[i])) continue;
                newBeatTimings.Add(beatTimings[i]);
            }
            else continue;
        }
        beatTimings = newBeatTimings;
    }
}
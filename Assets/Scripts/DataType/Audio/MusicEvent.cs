using System;
using System.Collections.Generic;

[Serializable]
public class MusicEvent : IMusicEvent
{
    public int barCount { get; set; }
    public float BPM { get; set; }
    public List<int> beatTimings { get; set; }
    public int measure { get; set; }

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
using System;
using System.Collections.Generic;

namespace BulletHell.Audio
{
    [Serializable]
    public class MusicEvent : IMusicEvent
    {
        public int barCount;
        public float BPM;
        public List<int> beatTimings;
        public int measure;

        public int GetbarCount() => barCount;
        public float GetBPM() => BPM;
        public List<int> GetbeatTimings() => beatTimings;
        public int Getmeasure() => measure;

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
}
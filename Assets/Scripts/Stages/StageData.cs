using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using System.Collections.Generic;
using UnityEngine.Video;

[System.Serializable]
public class StageData
{
    public string stageName;
    public VideoClip videoClip;
    public AudioClip audioClip;
    public List<MusicEvent> MusicEvents;

    [System.Serializable]
    public class MusicEvent
    {
        public int barCount;
        public float BPM;
        public List<int> beatTimings;
        public int measure;
        public int barStartOffsetBeats = 0;

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

    public float delayTime;//Delay time before the stage starts, in seconds

    [TextArea]
    public string stageDescription;

    public List<EnemySpawner> enemySpawners = new List<EnemySpawner>();

    public List<BulletSpawner> bulletSpawners = new List<BulletSpawner>();
}
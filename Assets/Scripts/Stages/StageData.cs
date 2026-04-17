using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "StageData", menuName = "Stage/StageData")]
public class StageData : ScriptableObject
{
    public int stageId;
    public int difficulty;//0:easy 1:normal 2:hard 3:lunatic
    [TextArea]
    public string stageDescription;
    public string stageName;
    public string musicName;
    public Sprite StageImage;
    public AudioClip audioClip;
    public List<MusicEvent> BPMChanges;

    [System.Serializable]
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

    public List<EnemySpawner> enemySpawners = new List<EnemySpawner>();
}
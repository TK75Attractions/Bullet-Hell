using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Video;

[CreateAssetMenu(fileName = "StageData", menuName = "Stage/StageData")]
public class StageData : ScriptableObject
{
    public int stageId;
    public int difficulty;//0:easy 1:normal 2:hard 3:lunatic
    public string stageName;
    public VideoClip videoClip;
    public AudioClip audioClip;
    public List<MusicEvent> MusicEvents;

    public float delayTime;//Delay time before the stage starts, in seconds

    [TextArea]
    public string stageDescription;

    public List<EnemySpawner> enemySpawners = new List<EnemySpawner>();

    public List<BulletSpawner> bulletSpawners = new List<BulletSpawner>();
}
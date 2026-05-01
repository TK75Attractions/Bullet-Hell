using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Video;

[CreateAssetMenu(fileName = "StageData", menuName = "Stage/StageData")]
public class StageData : ScriptableObject, IStageData
{
    public int stageId { get; set; }
    public int difficulty { get; set; } //0:easy 1:normal 2:hard 3:lunatic
    public string stageName { get; set; }
    public VideoClip videoClip { get; set; }
    public AudioClip audioClip { get; set; }
    public List<IMusicEvent> MusicEvents { get; set; }

    public float delayTime { get; set; }//Delay time before the stage starts, in seconds

    [TextArea]
    public string stageDescription { get; set; }

    public List<IEnemySpawner> enemySpawners { get; set; } = new List<IEnemySpawner>();

    public List<BulletSpawner> bulletSpawners { get; set; } = new List<BulletSpawner>();
}
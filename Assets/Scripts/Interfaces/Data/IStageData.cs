using UnityEngine;
using UnityEngine.Video;
using System.Collections.Generic;
public interface IStageData
{
    public int stageId { get; }
    public int difficulty { get; } //0:easy 1:normal 2:hard 3:lunatic
    public string stageName { get; }
    public VideoClip videoClip { get; }
    public AudioClip audioClip { get; }
    public List<IMusicEvent> MusicEvents { get; }

    public float delayTime { get; }//Delay time before the stage starts, in seconds

    public string stageDescription { get; }

    public List<IEnemySpawner> enemySpawners { get; }

    public List<BulletSpawner> bulletSpawners { get; }
}
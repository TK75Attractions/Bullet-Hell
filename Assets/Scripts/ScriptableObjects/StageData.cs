using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Video;

using BulletHell.Audio;
using BulletHell.Enemies;
using BulletHell.Bullets;
using System.Linq;

namespace BulletHell.Stages
{
    [CreateAssetMenu(fileName = "StageData", menuName = "Stage/StageData")]
    public class StageData : ScriptableObject
    {
        public int stageId;
        public int difficulty; //0:easy 1:normal 2:hard 3:lunatic
        public string stageName;
        public VideoClip videoClip;
        public AudioClip audioClip;
        public List<MusicEvent> MusicEvents;

        public List<IMusicEvent> GetMusicEvents() => MusicEvents.Cast<IMusicEvent>().ToList();

        public float delayTime;//Delay time before the stage starts, in seconds

        [TextArea]
        public string stageDescription;

        public List<EnemySpawner> enemySpawners;
        public List<IEnemySpawner> GetEnemySpawners() => enemySpawners.Cast<IEnemySpawner>().ToList();


        public List<BulletSpawner> bulletSpawners;

    }
}
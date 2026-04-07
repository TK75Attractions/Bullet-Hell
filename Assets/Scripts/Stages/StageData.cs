using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using System.Collections.Generic;

[CreateAssetMenu(fileName ="StageData",menuName ="Stage/StageData")]
public class StageData : ScriptableObject
{
    public int stageId;
    public int difficulty;//0:easy 1:normal 2:hard 3:lunatic
    public string stageName;
    public Sprite StageImage;

    [TextArea]
    public string stageDescription;

    public List<EnemySpawner> enemySpawners = new List<EnemySpawner>();
}
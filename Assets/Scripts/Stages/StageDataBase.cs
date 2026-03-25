using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Stage/StageDataBase", fileName = "StageDataBase")]
public class StageDataBase : ScriptableObject
{
    public List<StageData> stages;
}

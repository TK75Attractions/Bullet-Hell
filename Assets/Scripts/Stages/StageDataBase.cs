using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Stage/StageDataBase", fileName = "StageDataBase")]
public class StageDataBase : ScriptableObject
{
    public List<StageData> stages;

    public void Init()
    {
        
    }

    public StageData GetStage(int index)
    {
        if (stages == null || index < 0 || index >= stages.Count)
        {
            Debug.LogWarning($"StageData at index {index} is out of range! Returning null.");
            return null;
        }
        return stages[index];
    }
}

using UnityEngine;
using System.Collections.Generic;
using System;

[Serializable]
public class StageDataBase
{
    [SerializeField] private List<StageData> stages;
    private StageDataManager stageDataManager;

    public void Init()
    {
        if (stages == null)
        {
            stageDataManager = new StageDataManager();
            stages = stageDataManager.GetAllStageData();
            Debug.Log($"Loaded {stages.Count} stages from JSON");
        }
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

    public int GetStageCount()
    {
        if (stages == null) return 0;
        return stages.Count;
    }

    public List<StageData> GetAllStages()
    {
        if (stages == null) return new List<StageData>();
        return stages;
    }
}

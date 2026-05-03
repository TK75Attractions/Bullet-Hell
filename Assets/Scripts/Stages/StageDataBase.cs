using UnityEngine;
using System.Collections.Generic;

namespace BulletHell.Stages
{
    [CreateAssetMenu(menuName = "Stage/StageDataBase", fileName = "StageDataBase")]
    public class StageDataBase : ScriptableObject, IStageDB<IStageData>
    {
        public List<IStageData> stages {get; private set;} = new List<IStageData>();

        public void Init()
        {
            
        }

        public IStageData GetStage(int index)
        {
            if (stages == null || index < 0 || index >= stages.Count)
            {
                Debug.LogWarning($"StageData at index {index} is out of range! Returning null.");
                return null;
            }
            return stages[index];
        }
    }
}
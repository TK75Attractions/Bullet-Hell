using System.Collections.Generic;

namespace BulletHell.Stages
{
    public interface IStageDB
    {
        public List<StageData> GetStages();
        public void Init();
        public StageData GetStage(int index);
    }
}
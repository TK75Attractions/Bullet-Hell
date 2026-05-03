using System.Collections.Generic;

namespace BulletHell.Stages
{
    public interface IStageDB<T>
    {
        public List<T> stages { get; }
        public void Init();
        public T GetStage(int index);
    }
}
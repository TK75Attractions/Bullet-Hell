using System.Collections.Generic;

namespace BulletHell.Audio
{
    public interface IMusicEvent
    {
        int GetbarCount();
        float GetBPM();
        List<int> GetbeatTimings();
        int Getmeasure();

        void Refresh();
    }
}
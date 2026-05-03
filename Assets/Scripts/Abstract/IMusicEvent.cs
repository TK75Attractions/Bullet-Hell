using System.Collections.Generic;

namespace BulletHell.Audio
{
    public interface IMusicEvent
    {
        int barCount { get; }
        float BPM { get; }
        List<int> beatTimings { get; }
        int measure { get; }

        void Refresh();
    }
}
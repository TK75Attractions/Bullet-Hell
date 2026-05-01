using System.Collections.Generic;
public interface IMusicEvent
{
    int barCount { get; }
    float BPM { get; }
    List<int> beatTimings { get; }
    int measure { get; }

    void Refresh();
}
using UnityEngine;
namespace BulletHell.Audio
{
    public interface ISEData
    {
        AudioClip SeClip { get; }
        string SeName { get; }
        float Volume { get; }
    }
}
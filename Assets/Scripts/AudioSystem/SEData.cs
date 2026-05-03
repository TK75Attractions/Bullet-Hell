using UnityEngine;

namespace BulletHell.Audio
{
    [CreateAssetMenu(fileName = "SEData", menuName = "Audio/SEData")]
    public class SEData : ScriptableObject, ISEData
    {
        public AudioClip SeClip { get; }
        public string SeName { get; }
        public float Volume { get; } = 1.0f;
    }
}
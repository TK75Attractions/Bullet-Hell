using UnityEngine;

[CreateAssetMenu(fileName = "SEData", menuName = "Audio/SEData")]
public class SEData : ScriptableObject
{
    public AudioClip SeClip;
    public string SeName;
    public float Volume = 1.0f;
}

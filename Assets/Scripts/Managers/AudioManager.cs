using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    private Transform SEParent;
    [SerializeField] private GameObject audioSourcePrefab;
    private List<AudioSource> SEPool = new();
    private AudioSource BGMSource;
    private bool isready = false;

    public void Init()
    {
        SEParent = transform.Find("SEPool");
        SEPool = new();

        for (int i = 0; i < 6; i++)
        {
            GameObject go = Instantiate(audioSourcePrefab, SEParent);
            AudioSource source = go.GetComponent<AudioSource>();
            SEPool.Add(source);
        }

        BGMSource = transform.Find("BGM").GetComponent<AudioSource>();
        isready = true;
    }

    public async Task<AudioSource> PlayBGM(AudioClip clip, float volume = 1.0f)
    {
        if (!isready) return null;
        clip.LoadAudioData();
        while (clip.loadState != AudioDataLoadState.Loaded)
        {
            await Task.Yield();
        }
        BGMSource.clip = clip;
        BGMSource.volume = volume;
        return BGMSource;
    }

    public int PlaySE(string seName)
    {
        if (!isready) return -1;

        int index = GManager.Control.SEDB.GetSEData(seName);
        if (index == -1)
        {
            Debug.LogError($"SE '{seName}' not found in SEDataBase.");
            return -1;
        }

        SEData seData = GManager.Control.SEDB.GetSEData(index);
        AudioSource source = GetAvailableAudioSource();
        if (source != null)
        {
            source.clip = seData.SeClip;
            source.volume = seData.Volume;
            source.Play();
        }
        return index;
    }

    private AudioSource GetAvailableAudioSource()
    {
        foreach (AudioSource source in SEPool)
        { if (!source.isPlaying) return source; }

        GameObject go = Instantiate(audioSourcePrefab, SEParent);
        AudioSource so = go.GetComponent<AudioSource>();
        SEPool.Add(so);
        return so;
    }
}
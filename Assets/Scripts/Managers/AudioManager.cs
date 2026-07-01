using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    private const float AudioLoadTimeoutSeconds = 8f;
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
        if (BGMSource == null)
        {
            Debug.LogError("BGM AudioSource not found. Ensure a child named 'BGM' with AudioSource exists.");
            return null;
        }
        if (clip == null)
        {
            Debug.LogError("BGM clip is null. Stage audio may have failed to load (unsupported format/import settings).", this);
            return null;
        }

        BGMSource.Stop();
        BGMSource.clip = clip;
        BGMSource.time = 0f;
        BGMSource.volume = volume;
        bool loadRequested = clip.LoadAudioData();
        float startTime = Time.realtimeSinceStartup;
        while (clip.loadState == AudioDataLoadState.Loading)
        {
            if (Time.realtimeSinceStartup - startTime > AudioLoadTimeoutSeconds)
            {
                Debug.LogError(
                    $"BGM clip load timed out: {clip.name}, state={clip.loadState}, loadRequested={loadRequested}",
                    this);
                return null;
            }

            await Task.Yield();
        }

        if (clip.loadState != AudioDataLoadState.Loaded)
        {
            Debug.LogError(
                $"BGM clip failed to load: {clip.name}, state={clip.loadState}, loadRequested={loadRequested}",
                this);
            return null;
        }

        BGMSource.timeSamples = 0;
        BGMSource.time = 0f;
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

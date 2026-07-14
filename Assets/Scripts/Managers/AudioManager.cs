using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    private const float AudioLoadTimeoutSeconds = 8f;
    private Transform SEParent;
    [SerializeField] private GameObject audioSourcePrefab;
    private List<AudioSource> SEPool = new();
    private AudioSource BGMSource;
    private bool isready = false;
    // 画面遷移で BGM をぶつ切りにせずフェードさせるためのコルーチンハンドル
    // (2026-07-13 指摘)。フェード中に新しい再生/フェードが来たら必ず止める。
    private Coroutine bgmFadeRoutine;

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

        // 再生を確定させる前に、走っているフェードを止める(フェードが新しい
        // 再生の音量を 0 に潰すのを防ぐ)。
        StopBgmFade();
        BGMSource.Stop();
        BGMSource.loop = false;   // ステージ BGM はループしない(タイトル BGM でループを立てた後の持ち越し対策)
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

    // タイトル/曲選択などの UI 画面向け: BGM をロードして即ループ再生する。
    // ステージのような PlayScheduled(DSP 同期)は不要な画面用。戻り値の
    // AudioSource で再生位置(source.time)を読めば映像側を音に同期できる。
    public async Task<AudioSource> PlayLoopingBGM(AudioClip clip, float volume = 1.0f)
    {
        AudioSource src = await PlayBGM(clip, volume);
        if (src == null) return null;
        src.loop = true;
        src.Play();
        return src;
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

    // UI の「決定」効果音。SEDB 経路(未配線)は使わず、Resources/SE から遅延ロードして
    // キャッシュし、常駐 SE プールで PlayOneShot する(BGM とは独立)。タイトル/選択/
    // リザルトの確定ボタンから呼ぶ。出典: 効果音ラボ(決定ボタンを押す16)。
    private const float DecisionSeVolume = 0.7f;
    private AudioClip decisionSeClip;
    private bool decisionSeLoadFailed;

    public void PlayDecisionSE()
    {
        if (!isready) return;
        if (decisionSeClip == null && !decisionSeLoadFailed)
        {
            decisionSeClip = Resources.Load<AudioClip>("SE/ui_decide");
            if (decisionSeClip == null)
            {
                decisionSeLoadFailed = true;
                Debug.LogWarning("Decision SE 'Resources/SE/ui_decide' not found.");
                return;
            }
        }
        if (decisionSeClip == null) return;
        AudioSource source = GetAvailableAudioSource();
        if (source != null) source.PlayOneShot(decisionSeClip, DecisionSeVolume);
    }


public void StopBGM()
    {
        if (BGMSource == null) return;
        StopBgmFade();
        BGMSource.Stop();
        BGMSource.clip = null;
    }

    // 実行中の BGM フェードコルーチンを止める。
    private void StopBgmFade()
    {
        if (bgmFadeRoutine != null)
        {
            StopCoroutine(bgmFadeRoutine);
            bgmFadeRoutine = null;
        }
    }

    // 共有 BGMSource を duration 秒でフェードアウトしてから停止する(ぶつ切り防止)。
    // 画面遷移(ゲーム開始・リザルト入場など)の覆い演出と重ねて使う。
    public void FadeOutAndStopBGM(float duration = 0.6f)
    {
        if (!isready || BGMSource == null) return;
        StopBgmFade();
        if (!BGMSource.isPlaying || duration <= 0f) { StopBGM(); return; }
        bgmFadeRoutine = StartCoroutine(FadeOutRoutine(duration));
    }

    private IEnumerator FadeOutRoutine(float duration)
    {
        float startVol = BGMSource != null ? BGMSource.volume : 1f;
        float t = 0f;
        while (t < duration && BGMSource != null)
        {
            t += Time.unscaledDeltaTime;
            BGMSource.volume = Mathf.Lerp(startVol, 0f, Mathf.Clamp01(t / duration));
            yield return null;
        }
        if (BGMSource != null)
        {
            BGMSource.Stop();
            BGMSource.clip = null;
            BGMSource.volume = startVol;   // 次の再生に備えて戻す(PlayBGM でも上書きされる)
        }
        bgmFadeRoutine = null;
    }

    // 共有 BGMSource を 0 から target まで duration 秒でフェードインする。
    // タイトル/選択 BGM を静かに復帰させる用途(リザルト後の無音対策)。
    public void FadeInBGM(float target, float duration)
    {
        if (!isready || BGMSource == null) return;
        StopBgmFade();
        if (duration <= 0f) { BGMSource.volume = target; return; }
        BGMSource.volume = 0f;
        bgmFadeRoutine = StartCoroutine(FadeInRoutine(target, duration));
    }

    private IEnumerator FadeInRoutine(float target, float duration)
    {
        float t = 0f;
        while (t < duration && BGMSource != null)
        {
            t += Time.unscaledDeltaTime;
            BGMSource.volume = Mathf.Lerp(0f, target, Mathf.Clamp01(t / duration));
            yield return null;
        }
        if (BGMSource != null) BGMSource.volume = target;
        bgmFadeRoutine = null;
    }
}

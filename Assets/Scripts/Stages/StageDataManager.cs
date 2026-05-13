using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;
using UnityEngine;
using UnityEngine.Video;
using Unity.Mathematics;

public class StageDataManager : MonoBehaviour
{
    private class StageDataJson
    {
        public string stageName = "";
        public List<MusicEventJson> MusicEvents = new();
        public float delayTime;
        public string stageDescription = "";
        public List<EnemySpawnerJson> enemySpawners = new();
        public List<BulletSpawnerJson> bulletSpawners = new();
    }

    private class MusicEventJson
    {
        public int barCount;
        public float BPM;
        public List<int> beatTimings = new();
        public int measure;
        public int barStartOffsetBeats = 0;

        public StageData.MusicEvent ToMusicEvent()
        {
            StageData.MusicEvent musicEvent = new StageData.MusicEvent
            {
                barCount = barCount,
                BPM = BPM,
                beatTimings = beatTimings,
                measure = measure,
                barStartOffsetBeats = barStartOffsetBeats
            };
            musicEvent.Refresh();
            return musicEvent;
        }
    }

    private class EnemySpawnerJson
    {
        public string enemyName = ""; //描画するエネミー(弾源)の名前, Unity 側で一致するエネミーを見つけたら、そのスプライトを描画する
        public int count; //飛ばすエネミーの数
        public float enemyInterval; //飛ばす時間間隔(0 なら同時)
        public float enemyAppearTime; //エネミーの出現時刻
        public float bulletEmitTime; //エネミーがスポーンしてから弾を飛ばすまでの時間
        public int bulletCount; //弾を飛ばす回数
        public float life;
        public BulletDataJson orbit;
        public BulletClipJson bulletClip;
        public List<BulletChangeClipJson> bulletChangeClips = new List<BulletChangeClipJson>();

        public EnemySpawner ToEnemySpawner()
        {
            List<BulletChangeClip> changeClips = new List<BulletChangeClip>();
            for (int i = 0; i < bulletChangeClips.Count; i++)
            {
                changeClips.Add(bulletChangeClips[i].ToBulletChangeClip());
            }
            return new EnemySpawner
            {
                id = GManager.Control.EDB.GetEnemyId(enemyName),
                count = count,
                enemyInterval = enemyInterval,
                enemyAppearTime = enemyAppearTime,
                bulletEmitTime = bulletEmitTime,
                bulletCount = bulletCount,
                bulletInterval = bulletEmitTime / bulletCount,
                orbit = orbit.ToBulletData(),
                bulletClip = bulletClip.ToBulletClip(),
                bulletChangeClips = changeClips
            };
        }
    }

    private struct BulletChangeClipJson
    {
        public BulletClipJson clip;
        public float time;//弾が発射されてから time 秒後に弾が変化する。

        public BulletChangeClip ToBulletChangeClip()
        {
            return new BulletChangeClip
            {
                clip = clip.ToBulletClip(),
                time = time
            };
        }
    }

    private struct BulletClipJson
    {
        public BulletDataJson data;//弾の軌道
        public int number;//弾の数
        public float disRad;//弾の同士のなす角
        public bool homing;//true なら Homing する

        public BulletClip ToBulletClip()
        {
            return new BulletClip
            {
                data = data.ToBulletData(),
                number = number,
                disRad = disRad,
                homing = homing
            };
        }
    }

    private struct BulletSpawnerJson
    {
        public string clipName; // 呼び出すクリップの名前
        public int count;//飛ばす Bullet Buffer の数
        public float interval; // 飛ばす時間間隔
        public float time; // 飛ばす時刻
        public float2 pos; //どこから飛ばすかの座標
        public float2 originVlc; // originVlc を上書き出来ます(時間進展と共に平衡移動可能)
        public float angle; // 飛ばす角度 (オイラー角 0 <= 角度 < 360)
        public float angleInterval; //Buffer 毎に角度をずらす (1発目を 0, 2発目を 30°, 3発目を 60°... みたいな事が出来る)
        public float4 color; // 色(和訳)

        public BulletSpawner ToBulletSpawner()
        {
            return new BulletSpawner
            {
                clipName = clipName,
                count = count,
                interval = interval,
                time = time,
                pos = pos,
                originVlc = originVlc,
                angle = angle,
                angleInterval = angleInterval,
                color = color
            };
        }
    }

    private StageData ReadStageDataFromDirectory(string name)
    {
        string directoryPath = Path.Combine(Application.dataPath, "StageData", name);
        StageData data = ScriptableObject.CreateInstance<StageData>();

        if (!Directory.Exists(directoryPath))
        {
            Debug.LogError($"Stage directory not found: {directoryPath}");
            return data;
        }

        // JSON 読み込み
        string jsonPath = Path.Combine(directoryPath, name + ".json");
        if (File.Exists(jsonPath))
        {
            try
            {
                string json = File.ReadAllText(jsonPath);
                StageDataJson jsonData = JsonUtility.FromJson<StageDataJson>(json);

                // StageData にセット
                data.stageName = jsonData.stageName;
                data.delayTime = jsonData.delayTime;
                data.stageDescription = jsonData.stageDescription;

                // MusicEvents を変換
                data.MusicEvents = new List<StageData.MusicEvent>(jsonData.MusicEvents.Count);
                foreach (var musicEvent in jsonData.MusicEvents)
                {
                    data.MusicEvents.Add(musicEvent.ToMusicEvent());
                }

                // enemySpawners を変換
                data.enemySpawners = new List<EnemySpawner>(jsonData.enemySpawners.Count);
                foreach (var spawner in jsonData.enemySpawners)
                {
                    data.enemySpawners.Add(spawner.ToEnemySpawner());
                }

                // bulletSpawners を変換
                data.bulletSpawners = new List<BulletSpawner>(jsonData.bulletSpawners.Count);
                foreach (var spawner in jsonData.bulletSpawners)
                {
                    data.bulletSpawners.Add(spawner.ToBulletSpawner());
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to read or parse JSON file: {jsonPath}\n{ex.Message}");
            }
        }
        else
        {
            Debug.LogWarning($"JSON file not found: {jsonPath}");
        }

        // 音源ファイル読み込み
        string[] audioExtensions = { ".wav", ".mp3", ".ogg" };
        foreach (var ext in audioExtensions)
        {
            string audioPath = Path.Combine(directoryPath, name + ext);
            if (File.Exists(audioPath))
            {
                try
                {
                    if (ext == ".wav")
                    {
                        data.audioClip = LoadWavFile(audioPath);
                        break;
                    }
                    else
                    {
                        // MP3/OGG はエディタ時 AssetDatabase、実行時は UnityWebRequest で別途読み込み
#if UNITY_EDITOR
                        string relativePath = "Assets" + audioPath.Substring(Application.dataPath.Length);
                        data.audioClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(relativePath);
#endif
                        if (data.audioClip != null)
                            break;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to load audio file: {audioPath}\n{ex.Message}");
                }
            }
        }

        // 動画ファイル読み込み
        string[] videoExtensions = { ".mp4", ".webm" };
        foreach (var ext in videoExtensions)
        {
            string videoPath = Path.Combine(directoryPath, name + ext);
            if (File.Exists(videoPath))
            {
                try
                {
#if UNITY_EDITOR
                    string relativePath = "Assets" + videoPath.Substring(Application.dataPath.Length);
                    data.videoClip = UnityEditor.AssetDatabase.LoadAssetAtPath<VideoClip>(relativePath);
#else
                    // 実行時は別途 UnityWebRequest で非同期読み込みが必要
                    Debug.LogWarning($"Video file found at runtime: {videoPath}. Use async loading with UnityWebRequest.");
#endif
                    if (data.videoClip != null)
                        break;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to load video file: {videoPath}\n{ex.Message}");
                }
            }
        }

        return data;
    }

    private AudioClip LoadWavFile(string path)
    {
        using (var file = File.OpenRead(path))
        using (var reader = new BinaryReader(file))
        {
            // WAV ヘッダ解析
            string riffHeader = new string(reader.ReadChars(4));
            if (riffHeader != "RIFF")
            {
                throw new System.Exception("Invalid WAV file: RIFF header not found");
            }

            reader.ReadInt32(); // ファイルサイズ
            string waveHeader = new string(reader.ReadChars(4));
            if (waveHeader != "WAVE")
            {
                throw new System.Exception("Invalid WAV file: WAVE header not found");
            }

            // fmt チャンク検索
            int channels = 0, sampleRate = 0, bitsPerSample = 0;
            while (file.Position < file.Length)
            {
                string chunkId = new string(reader.ReadChars(4));
                int chunkSize = reader.ReadInt32();

                if (chunkId == "fmt ")
                {
                    reader.ReadInt16(); // AudioFormat (1 = PCM)
                    channels = reader.ReadInt16();
                    sampleRate = reader.ReadInt32();
                    reader.ReadInt32(); // ByteRate
                    reader.ReadInt16(); // BlockAlign
                    bitsPerSample = reader.ReadInt16();

                    if (chunkSize > 16)
                        reader.ReadBytes(chunkSize - 16);
                }
                else if (chunkId == "data")
                {
                    byte[] data = reader.ReadBytes(chunkSize);
                    float[] samples = new float[chunkSize / (bitsPerSample / 8)];

                    for (int i = 0; i < samples.Length; i++)
                    {
                        if (bitsPerSample == 16)
                        {
                            short sample = BitConverter.ToInt16(data, i * 2);
                            samples[i] = sample / 32768f;
                        }
                        else if (bitsPerSample == 8)
                        {
                            samples[i] = (data[i] - 128) / 128f;
                        }
                    }

                    AudioClip clip = AudioClip.Create(Path.GetFileNameWithoutExtension(path), samples.Length / channels, channels, sampleRate, false);
                    clip.SetData(samples, 0);
                    return clip;
                }
                else
                {
                    reader.ReadBytes(chunkSize);
                }
            }
        }

        throw new System.Exception("Invalid WAV file: data chunk not found");
    }
}
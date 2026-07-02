using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Video;
using Unity.Mathematics;
using UnityEngine.Networking;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

[Serializable]
public class StageDataManager
{
    private const string StageDataDirectoryName = "StageData";
    private const string StageDataJsonLabel = "stage-data-json";
    private const string ModsDirectoryName = "Mods";
    private const string ModManifestFileName = "mod.json";
    private const bool UseAddressablesInEditor = false;
    private static readonly string[] AudioExtensions = { ".wav", ".mp3", ".ogg", ".m4a" };
    private static readonly string[] VideoExtensions = { ".mp4", ".webm" };
    private static readonly string[] OfficialStageOrder =
    {
        "25",
        "captain",
        "debug",
        "debug(nature)",
        "stone",
        "mirror"
    };

    [Serializable]
    private struct BulletDataJsonDeserializer
    {
        public Vector2 originPos;
        public Vector2 originVlc;
        public float startX;
        public float speed;
        public float gravity;
        public float initialAngle;
        public float angleSpeed;
        public Vector2 polarForm;
        public float radiusVlc;
        public float thetaVlc;
        public Vector2 startPos;
        public Vector4 polynomial;
        public string typeName;
        public Vector2 scale;
        public float size;
        public Vector4 color;
        public float appearTime;
        public float appearDuration;
        public float life;
        public float random;
        public bool unCounterable;
        public bool lockRotation;

        public BulletDataJson ToBulletDataJson()
        {
            float2 resolvedScale = new float2(scale.x, scale.y);
            if (resolvedScale.x == 0f && resolvedScale.y == 0f)
            {
                float legacy = size > 0f ? size : 1f;
                resolvedScale = new float2(legacy, legacy);
            }

            return new BulletDataJson
            {
                originPos = new Vector2(originPos.x, originPos.y),
                originVlc = new Vector2(originVlc.x, originVlc.y),
                startX = startX,
                speed = speed,
                gravity = gravity,
                initialAngle = initialAngle,
                angleSpeed = angleSpeed,
                polarForm = new Vector2(polarForm.x, polarForm.y),
                radiusVlc = radiusVlc,
                thetaVlc = thetaVlc,
                startPos = new Vector2(startPos.x, startPos.y),
                polynomial = new float4(polynomial.x, polynomial.y, polynomial.z, polynomial.w),
                typeName = typeName,
                scale = new Vector2(resolvedScale.x, resolvedScale.y),
                size = size,
                color = new float4(color.x, color.y, color.z, color.w),
                appearTime = appearTime,
                appearDuration = appearDuration,
                life = life,
                random = random,
                unCounterable = unCounterable,
                lockRotation = lockRotation
            };
        }
    }

    [Serializable]
    private class StageDataJson
    {
        public string stageName = "";
        public List<MusicEventJson> MusicEvents = new();
        public float delayTime;
        public string stageDescription = "";
        public List<EnemyVisualDefinition> enemyVisuals = new();
        public List<EnemySpawnerJson> enemySpawners = new();
        public List<BulletSpawnerJson> bulletSpawners = new();
        // New P3 runtime pattern events. PatternEventData is a flat [Serializable]
        // type, so JsonUtility fills it directly; absent in old stage.json => null.
        public List<PatternEventData> patternEvents = new();
    }

    [Serializable]
    private class ModManifestJson
    {
        public int schemaVersion = 1;
        public string modId = "";
        public string displayName = "";
        public string author = "";
        public List<ModStageJson> stages = new();
    }

    [Serializable]
    private class ModStageJson
    {
        public string stageId = "";
        public string stageJson = "";
        public string bgm = "";
        public string video = "";
        public string bulletBufferDirectory = "";
    }

    [Serializable]
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

    [Serializable]
    private class EnemySpawnerJson
    {
        public string enemyName = ""; //描画するエネミー(弾源)の名前, Unity 側で一致するエネミーを見つけたら、そのスプライトを描画する
        public string visualId = "";
        public EnemyAnimationPlan animation = new EnemyAnimationPlan();
        public int count; //飛ばすエネミーの数
        public float enemyInterval; //飛ばす時間間隔(0 なら同時)
        public float enemyAppearTime; //エネミーの出現時刻
        public float bulletEmitTime; //エネミーがスポーンしてから弾を飛ばすまでの時間
        public int bulletCount; //弾を飛ばす回数
        public float life;
        public BulletDataJsonDeserializer orbit;
        public BulletClipJson bulletClip;
        public List<BulletChangeClipJson> bulletChangeClips = new List<BulletChangeClipJson>();

        public EnemySpawner ToEnemySpawner()
        {
            List<BulletChangeClip> changeClips = new List<BulletChangeClip>();
            if (bulletChangeClips == null)
            {
                bulletChangeClips = new List<BulletChangeClipJson>();
            }

            for (int i = 0; i < bulletChangeClips.Count; i++)
            {
                changeClips.Add(bulletChangeClips[i].ToBulletChangeClip());
            }
            return new EnemySpawner
            {
                id = GManager.Control.EDB.GetEnemyId(enemyName),
                enemyName = enemyName,
                visualId = visualId,
                animation = NormalizeAnimationPlan(animation),
                count = count,
                enemyInterval = enemyInterval,
                enemyAppearTime = enemyAppearTime,
                bulletEmitTime = bulletEmitTime,
                bulletCount = bulletCount,
                bulletInterval = bulletCount > 0 ? bulletEmitTime / bulletCount : 0f,
                orbit = orbit.ToBulletDataJson().ToBulletData(),
                bulletClip = bulletClip.ToBulletClip(),
                bulletChangeClips = changeClips
            };
        }
    }

    [Serializable]
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

    [Serializable]
    private struct BulletClipJson
    {
        public BulletDataJsonDeserializer data;//弾の軌道
        public int number;//弾の数
        public float disRad;//弾の同士のなす角
        public bool homing;//true なら Homing する

        public BulletClip ToBulletClip()
        {
            return new BulletClip
            {
                data = data.ToBulletDataJson().ToBulletData(),
                number = number,
                disRad = disRad,
                homing = homing
            };
        }
    }

    [Serializable]
    private struct BulletSpawnerJson
    {
        public string clipName; // 呼び出すクリップの名前
        public int count;//飛ばす Bullet Buffer の数
        public float interval; // 飛ばす時間間隔
        public float time; // 飛ばす時刻
        public Vector2 pos; //どこから飛ばすかの座標
        public Vector2 originVlc; // originVlc を上書き出来ます(時間進展と共に平衡移動可能)
        public float angle; // 飛ばす角度 (オイラー角 0 <= 角度 < 360)
        public float angleInterval; //Buffer 毎に角度をずらす (1発目を 0, 2発目を 30°, 3発目を 60°... みたいな事が出来る)
        public Vector4 color; // 色(和訳)

        // P5 difficulty modifiers (flat; empty/0 => no modification). Absent in old
        // stage.json => defaults, so legacy stages behave identically.
        public string minDifficulty;
        public int thinEasy;
        public int thinNormal;

        public BulletSpawner ToBulletSpawner()
        {
            return new BulletSpawner
            {
                clipName = clipName,
                count = count,
                interval = interval,
                time = time,
                pos = (float2)pos,
                originVlc = (float2)originVlc,
                angle = angle,
                angleInterval = angleInterval,
                color = (float4)color,
                minDifficulty = minDifficulty,
                thinEasy = thinEasy,
                thinNormal = thinNormal
            };
        }
    }

    public async Task<List<StageData>> GetAllStageDataAsync()
    {
        List<StageData> addressableStages = ShouldUseAddressables()
            ? await TryGetAllStageDataFromAddressablesAsync()
            : new List<StageData>();
        List<StageData> stages;
        if (addressableStages.Count > 0)
        {
            stages = addressableStages;
        }
        else
        {
            stages = GetAllOfficialStageData();
        }

        stages.AddRange(GetAllModStageData());
        return stages;
    }

    public List<StageData> GetAllStageData()
    {
        List<StageData> stages = GetAllOfficialStageData();
        stages.AddRange(GetAllModStageData());
        return stages;
    }

    private List<StageData> GetAllOfficialStageData()
    {
        List<StageData> stageDataList = new List<StageData>();
        string stageDataPath = Path.Combine(Application.dataPath, StageDataDirectoryName);

        if (!Directory.Exists(stageDataPath))
        {
            Debug.LogError($"StageData directory not found: {stageDataPath}");
            return stageDataList;
        }

        string[] stageDirectories = Directory.GetDirectories(stageDataPath);
        Array.Sort(stageDirectories, CompareOfficialStageDirectories);
        foreach (string dir in stageDirectories)
        {
            string stageName = Path.GetFileName(dir);
            StageData data = ReadStageDataFromDirectory(stageName);
            stageDataList.Add(data);
            Debug.Log($"Loaded stage data: {stageName}");
        }

        return stageDataList;
    }

    private static int CompareOfficialStageDirectories(string left, string right)
    {
        string leftName = Path.GetFileName(left);
        string rightName = Path.GetFileName(right);
        int leftIndex = GetOfficialStageOrderIndex(leftName);
        int rightIndex = GetOfficialStageOrderIndex(rightName);

        if (leftIndex >= 0 && rightIndex >= 0)
        {
            return leftIndex.CompareTo(rightIndex);
        }

        if (leftIndex >= 0)
        {
            return -1;
        }

        if (rightIndex >= 0)
        {
            return 1;
        }

        return string.Compare(leftName, rightName, StringComparison.OrdinalIgnoreCase);
    }

    private static int GetOfficialStageOrderIndex(string stageDirectoryName)
    {
        for (int i = 0; i < OfficialStageOrder.Length; i++)
        {
            if (string.Equals(OfficialStageOrder[i], stageDirectoryName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private async Task<List<StageData>> TryGetAllStageDataFromAddressablesAsync()
    {
        List<StageData> stageDataList = new List<StageData>();
        AsyncOperationHandle<IList<IResourceLocation>> locationsHandle = default;

        try
        {
            locationsHandle = Addressables.LoadResourceLocationsAsync(StageDataJsonLabel, typeof(TextAsset));
            await WaitForAddressable(locationsHandle);

            if (locationsHandle.Status != AsyncOperationStatus.Succeeded ||
                locationsHandle.Result == null ||
                locationsHandle.Result.Count == 0)
            {
                return stageDataList;
            }

            List<IResourceLocation> locations = new List<IResourceLocation>(locationsHandle.Result);
            locations.Sort((a, b) => string.Compare(
                GetStageDirectoryNameFromAddress(a.PrimaryKey),
                GetStageDirectoryNameFromAddress(b.PrimaryKey),
                StringComparison.OrdinalIgnoreCase));

            foreach (IResourceLocation location in locations)
            {
                AsyncOperationHandle<TextAsset> jsonHandle = Addressables.LoadAssetAsync<TextAsset>(location);
                await WaitForAddressable(jsonHandle);

                if (jsonHandle.Status != AsyncOperationStatus.Succeeded || jsonHandle.Result == null)
                {
                    Debug.LogWarning($"Failed to load addressable stage json: {location.PrimaryKey}");
                    if (jsonHandle.IsValid()) Addressables.Release(jsonHandle);
                    continue;
                }

                string directoryName = GetStageDirectoryNameFromAddress(location.PrimaryKey);
                StageData data = ReadStageDataFromJson(directoryName, jsonHandle.Result.text);
                if (jsonHandle.IsValid()) Addressables.Release(jsonHandle);

                await LoadStageMediaFromAddressablesAsync(data, directoryName);
                if (data.audioClip == null) LoadStageAudioFromDirectory(data, directoryName);
                if (data.videoClip == null) LoadStageVideoFromDirectory(data, directoryName);

                stageDataList.Add(data);
                Debug.Log($"Loaded addressable stage data: {directoryName}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Addressable StageData load failed. Falling back to file system. {ex.Message}");
            stageDataList.Clear();
        }
        finally
        {
            if (locationsHandle.IsValid())
            {
                Addressables.Release(locationsHandle);
            }
        }

        return stageDataList;
    }

    private bool ShouldUseAddressables()
    {
#if UNITY_EDITOR
        return UseAddressablesInEditor;
#else
        return true;
#endif
    }

    public IEnumerable<string> GetModRootDirectories()
    {
        HashSet<string> directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string persistentModsPath = Path.Combine(Application.persistentDataPath, ModsDirectoryName);
        directories.Add(Path.GetFullPath(persistentModsPath));

        string applicationRootModsPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ModsDirectoryName));
        directories.Add(applicationRootModsPath);

        return directories;
    }

    public List<StageData> GetAllModStageData()
    {
        List<StageData> modStages = new List<StageData>();

        foreach (string modsRoot in GetModRootDirectories())
        {
            if (!Directory.Exists(modsRoot))
            {
                continue;
            }

            foreach (string modDirectory in Directory.GetDirectories(modsRoot))
            {
                modStages.AddRange(ReadModStageDataFromDirectory(modDirectory));
            }
        }

        return modStages;
    }

    public async Task EnsureRuntimeMediaLoadedAsync(StageData data)
    {
        if (data == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(data.audioPath) || !File.Exists(data.audioPath))
        {
            return;
        }

        bool shouldLoadExternalAudio = data.source == StageData.StageSource.Mod || data.audioClip == null;

        if (!shouldLoadExternalAudio)
        {
            return;
        }

        AudioClip externalClip = await LoadExternalAudioClipAsync(data.audioPath);
        if (externalClip != null)
        {
            data.audioClip = externalClip;
        }
    }

    private List<StageData> ReadModStageDataFromDirectory(string modDirectory)
    {
        List<StageData> stages = new List<StageData>();
        string manifestPath = Path.Combine(modDirectory, ModManifestFileName);

        if (!File.Exists(manifestPath))
        {
            return stages;
        }

        try
        {
            ModManifestJson manifest = JsonUtility.FromJson<ModManifestJson>(File.ReadAllText(manifestPath));
            if (manifest == null)
            {
                Debug.LogWarning($"Failed to parse mod manifest: {manifestPath}");
                return stages;
            }

            if (string.IsNullOrWhiteSpace(manifest.modId))
            {
                manifest.modId = Path.GetFileName(modDirectory);
            }

            if (manifest.stages == null)
            {
                return stages;
            }

            foreach (ModStageJson stageEntry in manifest.stages)
            {
                StageData stage = ReadModStageData(modDirectory, manifest.modId, stageEntry);
                if (stage != null)
                {
                    stages.Add(stage);
                }
            }

            Debug.Log($"Loaded {stages.Count} mod stages from {manifest.modId}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Exception while reading mod manifest {manifestPath}: {ex.Message}");
        }

        return stages;
    }

    private StageData ReadModStageData(string modDirectory, string modId, ModStageJson stageEntry)
    {
        if (stageEntry == null)
        {
            return null;
        }

        string stageJson = stageEntry.stageJson;
        if (string.IsNullOrWhiteSpace(stageJson) && !string.IsNullOrWhiteSpace(stageEntry.stageId))
        {
            stageJson = Path.Combine("Stages", stageEntry.stageId, stageEntry.stageId + ".json");
        }

        string stageJsonPath = ResolveModPath(modDirectory, stageJson);
        if (string.IsNullOrWhiteSpace(stageJsonPath) || !File.Exists(stageJsonPath))
        {
            Debug.LogWarning($"Mod stage json not found: {stageJsonPath}");
            return null;
        }

        try
        {
            string stageId = string.IsNullOrWhiteSpace(stageEntry.stageId)
                ? Path.GetFileNameWithoutExtension(stageJsonPath)
                : stageEntry.stageId;

            StageData data = ReadStageDataFromJson(stageId, File.ReadAllText(stageJsonPath));
            data.source = StageData.StageSource.Mod;
            data.modId = modId;
            data.baseDirectory = modDirectory;
            data.audioPath = ResolveModPath(modDirectory, stageEntry.bgm);
            data.videoPath = ResolveModPath(modDirectory, stageEntry.video);
            data.bulletBufferDirectory = ResolveModPath(modDirectory, stageEntry.bulletBufferDirectory);

            if (string.IsNullOrWhiteSpace(data.audioPath))
            {
                data.audioPath = FindSiblingAsset(stageJsonPath, AudioExtensions);
            }

            if (string.IsNullOrWhiteSpace(data.videoPath))
            {
                data.videoPath = FindSiblingAsset(stageJsonPath, VideoExtensions);
            }

            if (string.IsNullOrWhiteSpace(data.bulletBufferDirectory))
            {
                data.bulletBufferDirectory = Path.Combine(modDirectory, "BulletBuffers", stageId);
            }

            return data;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Exception while reading mod stage {stageJsonPath}: {ex.Message}");
            return null;
        }
    }

    private string ResolveModPath(string modDirectory, string relativeOrAbsolutePath)
    {
        if (string.IsNullOrWhiteSpace(relativeOrAbsolutePath))
        {
            return null;
        }

        if (Path.IsPathRooted(relativeOrAbsolutePath))
        {
            return Path.GetFullPath(relativeOrAbsolutePath);
        }

        return Path.GetFullPath(Path.Combine(modDirectory, relativeOrAbsolutePath));
    }

    private string FindSiblingAsset(string stageJsonPath, IEnumerable<string> extensions)
    {
        string directory = Path.GetDirectoryName(stageJsonPath);
        string fileName = Path.GetFileNameWithoutExtension(stageJsonPath);

        foreach (string ext in extensions)
        {
            string candidate = Path.Combine(directory, fileName + ext);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private StageData ReadStageDataFromDirectory(string name)
    {
        string directoryPath = Path.Combine(Application.dataPath, StageDataDirectoryName, name);
        StageData data = new StageData { stageDirectoryName = name };

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
                data.enemyVisuals = NormalizeEnemyVisualDefinitions(jsonData.enemyVisuals);

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

                data.patternEvents = NormalizePatternEvents(jsonData.patternEvents);
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
        foreach (var ext in AudioExtensions)
        {
            string audioPath = Path.Combine(directoryPath, name + ext);
            if (File.Exists(audioPath))
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(data.audioPath))
                    {
                        data.audioPath = audioPath;
                    }

                    if (ext == ".wav")
                    {
                        data.audioClip = LoadWavFile(audioPath);
                        break;
                    }
                    else
                    {
                        // MP3/OGG/M4A はエディタ時 AssetDatabase、実行時は UnityWebRequest で別途読み込み
#if UNITY_EDITOR
                        string relativePath = "Assets" + audioPath.Substring(Application.dataPath.Length);
                        data.audioClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(relativePath);
                        if (data.audioClip == null)
                        {
                            Debug.LogWarning($"Audio file exists but was not imported as AudioClip: {relativePath}. If this is .m4a, convert to .mp3/.ogg or check import support.");
                        }
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

    private StageData ReadStageDataFromJson(string directoryName, string json)
    {
        StageData data = new StageData
        {
            stageDirectoryName = directoryName,
            stageName = directoryName,
            MusicEvents = new List<StageData.MusicEvent>(),
            enemySpawners = new List<EnemySpawner>(),
            bulletSpawners = new List<BulletSpawner>()
        };

        StageDataJson jsonData = JsonUtility.FromJson<StageDataJson>(json);
        if (jsonData == null)
        {
            Debug.LogWarning($"Failed to parse stage json: {directoryName}");
            return data;
        }

        data.stageName = string.IsNullOrWhiteSpace(jsonData.stageName) ? directoryName : jsonData.stageName;
        data.delayTime = jsonData.delayTime;
        data.stageDescription = jsonData.stageDescription;
        data.enemyVisuals = NormalizeEnemyVisualDefinitions(jsonData.enemyVisuals);

        if (jsonData.MusicEvents != null)
        {
            data.MusicEvents = new List<StageData.MusicEvent>(jsonData.MusicEvents.Count);
            foreach (MusicEventJson musicEvent in jsonData.MusicEvents)
            {
                data.MusicEvents.Add(musicEvent.ToMusicEvent());
            }
        }

        if (jsonData.enemySpawners != null)
        {
            data.enemySpawners = new List<EnemySpawner>(jsonData.enemySpawners.Count);
            foreach (EnemySpawnerJson spawner in jsonData.enemySpawners)
            {
                data.enemySpawners.Add(spawner.ToEnemySpawner());
            }
        }

        if (jsonData.bulletSpawners != null)
        {
            data.bulletSpawners = new List<BulletSpawner>(jsonData.bulletSpawners.Count);
            foreach (BulletSpawnerJson spawner in jsonData.bulletSpawners)
            {
                data.bulletSpawners.Add(spawner.ToBulletSpawner());
            }
        }

        data.patternEvents = NormalizePatternEvents(jsonData.patternEvents);

        return data;
    }

    private static List<PatternEventData> NormalizePatternEvents(List<PatternEventData> events)
    {
        List<PatternEventData> normalized = new List<PatternEventData>();
        if (events == null)
        {
            return normalized;
        }

        for (int i = 0; i < events.Count; i++)
        {
            PatternEventData ev = events[i];
            if (ev == null || string.IsNullOrEmpty(ev.patternType))
            {
                continue;
            }
            if (ev.args == null)
            {
                ev.args = new PatternParamsJson();
            }
            normalized.Add(ev);
        }

        return normalized;
    }

    private static List<EnemyVisualDefinition> NormalizeEnemyVisualDefinitions(List<EnemyVisualDefinition> definitions)
    {
        List<EnemyVisualDefinition> normalized = new List<EnemyVisualDefinition>();
        if (definitions == null)
        {
            return normalized;
        }

        for (int i = 0; i < definitions.Count; i++)
        {
            EnemyVisualDefinition definition = definitions[i];
            if (definition == null)
            {
                continue;
            }

            if (definition.clips == null)
            {
                definition.clips = new List<EnemyVisualClipDefinition>();
            }

            if (definition.pixelsPerUnit <= 0f)
            {
                definition.pixelsPerUnit = 100f;
            }

            normalized.Add(definition);
        }

        return normalized;
    }

    private static EnemyAnimationPlan NormalizeAnimationPlan(EnemyAnimationPlan animation)
    {
        if (animation == null)
        {
            animation = new EnemyAnimationPlan();
        }

        if (animation.events == null)
        {
            animation.events = new List<EnemyAnimationEventData>();
        }

        if (animation.triggers == null)
        {
            animation.triggers = new List<EnemyAnimationTriggerData>();
        }

        if (string.IsNullOrWhiteSpace(animation.initialClip))
        {
            animation.initialClip = "idle";
        }

        return animation;
    }

    private async Task LoadStageMediaFromAddressablesAsync(StageData data, string directoryName)
    {
        data.audioClip = await TryLoadAddressableAssetAsync<AudioClip>(GetStageAssetAddressCandidates(directoryName, AudioExtensions));
        data.videoClip = await TryLoadAddressableAssetAsync<VideoClip>(GetStageAssetAddressCandidates(directoryName, VideoExtensions));
    }

    private IEnumerable<string> GetStageAssetAddressCandidates(string directoryName, IEnumerable<string> extensions)
    {
        foreach (string ext in extensions)
        {
            yield return $"Assets/{StageDataDirectoryName}/{directoryName}/{directoryName}{ext}";
            yield return $"{StageDataDirectoryName}/{directoryName}/{directoryName}{ext}";
        }
    }

    private async Task<T> TryLoadAddressableAssetAsync<T>(IEnumerable<string> addresses) where T : UnityEngine.Object
    {
        foreach (string address in addresses)
        {
            AsyncOperationHandle<IList<IResourceLocation>> locationsHandle = default;
            try
            {
                locationsHandle = Addressables.LoadResourceLocationsAsync(address, typeof(T));
                await WaitForAddressable(locationsHandle);

                if (locationsHandle.Status != AsyncOperationStatus.Succeeded ||
                    locationsHandle.Result == null ||
                    locationsHandle.Result.Count == 0)
                {
                    continue;
                }

                AsyncOperationHandle<T> assetHandle = Addressables.LoadAssetAsync<T>(locationsHandle.Result[0]);
                await WaitForAddressable(assetHandle);

                if (assetHandle.Status == AsyncOperationStatus.Succeeded && assetHandle.Result != null)
                {
                    return assetHandle.Result;
                }

                if (assetHandle.IsValid())
                {
                    Addressables.Release(assetHandle);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Addressable load failed: {address}. {ex.Message}");
            }
            finally
            {
                if (locationsHandle.IsValid())
                {
                    Addressables.Release(locationsHandle);
                }
            }
        }

        return null;
    }

    private void LoadStageAudioFromDirectory(StageData data, string directoryName)
    {
        string directoryPath = Path.Combine(Application.dataPath, StageDataDirectoryName, directoryName);
        foreach (string ext in AudioExtensions)
        {
            string audioPath = Path.Combine(directoryPath, directoryName + ext);
            if (!File.Exists(audioPath))
            {
                continue;
            }

            try
            {
                if (string.IsNullOrWhiteSpace(data.audioPath))
                {
                    data.audioPath = audioPath;
                }

                if (ext == ".wav")
                {
                    data.audioClip = LoadWavFile(audioPath);
                    return;
                }

#if UNITY_EDITOR
                string relativePath = "Assets" + audioPath.Substring(Application.dataPath.Length);
                data.audioClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(relativePath);
                if (data.audioClip == null)
                {
                    Debug.LogWarning($"Audio file exists but was not imported as AudioClip: {relativePath}. If this is .m4a, convert to .mp3/.ogg or check import support.");
                }
#endif
                if (data.audioClip != null)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load audio file: {audioPath}\n{ex.Message}");
            }
        }
    }

    private void LoadStageVideoFromDirectory(StageData data, string directoryName)
    {
        string directoryPath = Path.Combine(Application.dataPath, StageDataDirectoryName, directoryName);
        foreach (string ext in VideoExtensions)
        {
            string videoPath = Path.Combine(directoryPath, directoryName + ext);
            if (!File.Exists(videoPath))
            {
                continue;
            }

            try
            {
#if UNITY_EDITOR
                string relativePath = "Assets" + videoPath.Substring(Application.dataPath.Length);
                data.videoClip = UnityEditor.AssetDatabase.LoadAssetAtPath<VideoClip>(relativePath);
#else
                Debug.LogWarning($"Video file found at runtime: {videoPath}. Use Addressables for runtime video loading.");
#endif
                if (data.videoClip != null)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load video file: {videoPath}\n{ex.Message}");
            }
        }
    }

    private async Task<AudioClip> LoadExternalAudioClipAsync(string path)
    {
        AudioType audioType = GetAudioTypeFromPath(path);
        if (audioType == AudioType.UNKNOWN)
        {
            Debug.LogWarning($"Unsupported mod audio format: {path}");
            return null;
        }

        using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(GetFileUri(path), audioType))
        {
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"Failed to load mod audio: {path}. {request.error}");
                return null;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
            if (clip != null)
            {
                clip.name = Path.GetFileNameWithoutExtension(path);
            }

            return clip;
        }
    }

    private AudioType GetAudioTypeFromPath(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        switch (ext)
        {
            case ".wav":
                return AudioType.WAV;
            case ".mp3":
                return AudioType.MPEG;
            case ".ogg":
                return AudioType.OGGVORBIS;
            case ".m4a":
                return AudioType.ACC;
            default:
                return AudioType.UNKNOWN;
        }
    }

    private string GetFileUri(string path)
    {
        return new Uri(Path.GetFullPath(path)).AbsoluteUri;
    }

    private string GetStageDirectoryNameFromAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return string.Empty;
        }

        string normalized = address.Replace('\\', '/');
        string marker = $"/{StageDataDirectoryName}/";
        int markerIndex = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0 && normalized.StartsWith(StageDataDirectoryName + "/", StringComparison.OrdinalIgnoreCase))
        {
            markerIndex = -1;
            marker = StageDataDirectoryName + "/";
        }

        if (markerIndex >= 0 || marker == StageDataDirectoryName + "/")
        {
            int start = markerIndex >= 0 ? markerIndex + marker.Length : marker.Length;
            string rest = normalized.Substring(start);
            int slashIndex = rest.IndexOf('/');
            if (slashIndex > 0)
            {
                return rest.Substring(0, slashIndex);
            }
        }

        return Path.GetFileNameWithoutExtension(normalized);
    }

    private async Task WaitForAddressable<T>(AsyncOperationHandle<T> handle)
    {
        while (!handle.IsDone)
        {
            await Task.Yield();
        }
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

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



    [Serializable]

    private class BulletDataJsonDeserializer

    {

        public Vector2 originPos;

        public Vector2 originVlc;

        public float startX;

        public float speed;

        public Vector2 gravity;

        public float initialAngle;

        public float angleSpeed;

        public bool useVelocityAngle = true;

        public Vector2 polarForm;

        public float radiusVlc;

        public float radiusAccel;

        public float thetaVlc;

        public float thetaAccel;

        public Vector2 startPos;

        public Vector4 polynomial;
        public string typeName;
        public Vector2 scale;
        public Vector4 color;
        public float appearTime;

        public float appearDuration;

        public float life;

        public float random;

        public bool unCounterable;



        public BulletDataJson ToBulletDataJson()

        {

            return new BulletDataJson
            {
                originPos = new Vector2(originPos.x, originPos.y),

                originVlc = new Vector2(originVlc.x, originVlc.y),

                startX = startX,

                speed = speed,

                gravity = new Vector2(gravity.x, gravity.y),

                initialAngle = initialAngle,

                angleSpeed = angleSpeed,

                useVelocityAngle = useVelocityAngle,

                polarForm = new Vector2(polarForm.x, polarForm.y),

                radiusVlc = radiusVlc,

                radiusAccel = radiusAccel,

                thetaVlc = thetaVlc,

                thetaAccel = thetaAccel,

                startPos = new Vector2(startPos.x, startPos.y),
                polynomial = new float4(polynomial.x, polynomial.y, polynomial.z, polynomial.w),
                typeName = typeName,
                scale = scale,
                color = new float4(color.x, color.y, color.z, color.w),
                appearTime = appearTime,

                appearDuration = appearDuration,

                life = life,

                random = random,

                unCounterable = unCounterable

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

        public List<StageDifficultyDataJson> difficulties = new();

        public List<MultiBulletSpawnerJson> multiBulletSpawners = new();



        public List<BossSpawnerJson> bossSpawners = new();

        public List<BulletSpawnerJson> bulletSpawners = new();

    }


    [Serializable]

    private class StageDifficultyDataJson

    {

        public string difficulty = "";

        public string displayName = "";

        public List<MultiBulletSpawnerJson> multiBulletSpawners = new();

        public List<BossSpawnerJson> bossSpawners = new();

        public List<BulletSpawnerJson> bulletSpawners = new();

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

    private class MultiBulletSpawnerJson

    {

        public Vector2 pos;

        public float time;

        public BulletBufferEmissionJson bulletEmission;

        public List<BulletBufferEmissionJson> bulletBufferTriggers = new List<BulletBufferEmissionJson>();



        public MultiBulletSpawner ToMultiBulletSpawner()

        {

            List<BulletBufferEmission> bufferTriggers = new List<BulletBufferEmission>();

            if (bulletBufferTriggers != null)

            {

                for (int i = 0; i < bulletBufferTriggers.Count; i++)

                {

                    if (bulletBufferTriggers[i] == null) continue;

                    bufferTriggers.Add(bulletBufferTriggers[i].ToBulletBufferEmission());

                }

            }





            return new MultiBulletSpawner

            {

                pos = (float2)pos,

                time = time,

                bulletEmission = bulletEmission != null ? bulletEmission.ToBulletBufferEmission() : new BulletBufferEmission(),

                bulletBufferTriggers = bufferTriggers
            };

        }

    }



    [Serializable]

    private class BossSpawnerJson

    {

        public string bossId = "";

        public string bossName = "";

        public string visualId = "";

        public float appearTime;

        public float lifeTime = -1f;

        public Vector2 startPos;

        public Vector2 scale = Vector2.one;

        public float angle;

        public BossAnimationPlan animation = new BossAnimationPlan();

        public List<BossMoveEventJson> moves = new List<BossMoveEventJson>();



        public BossSpawner ToBossSpawner()

        {

            List<BossMoveEvent> moveEvents = new List<BossMoveEvent>();

            if (moves != null)

            {

                for (int i = 0; i < moves.Count; i++)

                {

                    moveEvents.Add(moves[i].ToBossMoveEvent());

                }

            }



            Vector2 resolvedScale = scale;

            if (resolvedScale.x == 0f && resolvedScale.y == 0f)

            {

                resolvedScale = Vector2.one;

            }



            return new BossSpawner

            {

                bossId = bossId,

                bossName = bossName,

                visualId = visualId,

                appearTime = appearTime,

                lifeTime = lifeTime,

                startPos = startPos,

                scale = resolvedScale,

                angle = angle,

                animation = BossAnimationPlan.Normalize(animation),

                moves = moveEvents

            };

        }

    }



    [Serializable]

    private struct BossMoveEventJson

    {

        public float time;

        public float duration;

        public string type;

        public Vector2 to;

        public Vector2 control;

        public string easing;

        public bool relative;



        public BossMoveEvent ToBossMoveEvent()

        {

            return new BossMoveEvent

            {

                time = time,

                duration = duration,

                type = ParseBossMoveType(type),

                to = to,

                control = control,

                easing = string.IsNullOrWhiteSpace(easing) ? "linear" : easing,

                relative = relative

            };

        }



        private static BossMoveType ParseBossMoveType(string value)

        {

            if (string.IsNullOrWhiteSpace(value))

            {

                return BossMoveType.MoveTo;

            }



            switch (value.Trim().ToLowerInvariant())

            {

                case "setposition":

                case "set":

                    return BossMoveType.SetPosition;

                case "bezierto":

                case "bezier":

                    return BossMoveType.BezierTo;

                case "addvelocity":

                case "velocity":

                    return BossMoveType.AddVelocity;

                case "stop":

                    return BossMoveType.Stop;

                case "moveto":

                case "move":

                default:

                    return BossMoveType.MoveTo;

            }

        }

    }



    [Serializable]

    private class BulletBufferEmissionJson

    {

        public string clipName = "";

        public float time;

        public Vector2 originVlc;

        public float angleOffset;

        public string angleMode = "";

        public bool inheritSourceVelocity;

        public bool applyBulletOrbit;

        public bool deactivateSource;

        public Vector4 color;



        public BulletBufferEmission ToBulletBufferEmission()

        {

            return new BulletBufferEmission

            {

                clipName = clipName,

                time = time,

                originVlc = (float2)originVlc,

                angleOffset = angleOffset,
                inheritSourceAngle = !IsAbsoluteAngleMode(angleMode),

                inheritSourceVelocity = inheritSourceVelocity,

                applyBulletOrbit = applyBulletOrbit,

                deactivateSource = deactivateSource,

                color = NormalizeEmissionColor(color)

            };

        }

    }



    private static bool IsAbsoluteAngleMode(string angleMode)

    {

        if (string.IsNullOrWhiteSpace(angleMode)) return false;



        string normalized = angleMode.Trim().ToLowerInvariant();

        return normalized == "absolute"

            || normalized == "fixed"

            || normalized == "none";

    }



    private static float4 NormalizeEmissionColor(Vector4 color)

    {

        if (color.x == 0f && color.y == 0f && color.z == 0f && color.w == 0f)

        {

            return new float4(1f, 1f, 1f, 1f);

        }



        return new float4(color.x, color.y, color.z, color.w);

    }



    [Serializable]

    private struct BulletSpawnerJson

    {

        public string clipName; // 蜻ｼ縺ｳ蜃ｺ縺吶け繝ｪ繝・・縺ｮ蜷榊燕

        public int count;//鬟帙・縺・Bullet Buffer 縺ｮ謨ｰ

        public float interval; // 鬟帙・縺呎凾髢馴俣髫・

        public float time; // 鬟帙・縺呎凾蛻ｻ

        public Vector2 pos; //縺ｩ縺薙°繧蛾｣帙・縺吶°縺ｮ蠎ｧ讓・

        public Vector2 originVlc; // originVlc 繧剃ｸ頑嶌縺榊・譚･縺ｾ縺・譎る俣騾ｲ螻輔→蜈ｱ縺ｫ蟷ｳ陦｡遘ｻ蜍募庄閭ｽ)

        public float angle; // 鬟帙・縺呵ｧ貞ｺｦ (繧ｪ繧､繝ｩ繝ｼ隗・0 <= 隗貞ｺｦ < 360)

        public float angleInterval; //Buffer 豈弱↓隗貞ｺｦ繧偵★繧峨☆ (1逋ｺ逶ｮ繧・0, 2逋ｺ逶ｮ繧・30ﾂｰ, 3逋ｺ逶ｮ繧・60ﾂｰ... 縺ｿ縺溘＞縺ｪ莠九′蜃ｺ譚･繧・

        public Vector4 color; // 濶ｲ(蜥瑚ｨｳ)



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

                color = (float4)color

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

        foreach (string dir in stageDirectories)

        {

            string stageName = Path.GetFileName(dir);

            StageData data = ReadStageDataFromDirectory(stageName);

            stageDataList.Add(data);

            Debug.Log($"Loaded stage data: {stageName}");

        }



        return stageDataList;

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

        if (data == null || data.source != StageData.StageSource.Mod || data.audioClip != null)

        {

            return;

        }



        if (string.IsNullOrWhiteSpace(data.audioPath) || !File.Exists(data.audioPath))

        {

            return;

        }



        data.audioClip = await LoadExternalAudioClipAsync(data.audioPath);

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



        // JSON 隱ｭ縺ｿ霎ｼ縺ｿ

        string jsonPath = Path.Combine(directoryPath, name + ".json");

        if (File.Exists(jsonPath))

        {

            try

            {

                string json = File.ReadAllText(jsonPath);

                StageDataJson jsonData = JsonUtility.FromJson<StageDataJson>(json);
                NormalizeStageDataJsonLists(jsonData);


                // StageData 縺ｫ繧ｻ繝・ヨ

                data.stageName = jsonData.stageName;

                data.delayTime = jsonData.delayTime;

                data.stageDescription = jsonData.stageDescription;

                data.enemyVisuals = NormalizeEnemyVisualDefinitions(jsonData.enemyVisuals);



                // MusicEvents 繧貞､画鋤

                data.MusicEvents = new List<StageData.MusicEvent>(jsonData.MusicEvents.Count);

                foreach (var musicEvent in jsonData.MusicEvents)

                {

                    data.MusicEvents.Add(musicEvent.ToMusicEvent());

                }



                // multiBulletSpawners を変換

                data.multiBulletSpawners = ConvertMultiBulletSpawners(jsonData);



                data.bossSpawners = ConvertBossSpawners(jsonData.bossSpawners);



                // bulletSpawners 繧貞､画鋤

                data.bulletSpawners = new List<BulletSpawner>(jsonData.bulletSpawners.Count);

                foreach (var spawner in jsonData.bulletSpawners)

                {

                    data.bulletSpawners.Add(spawner.ToBulletSpawner());

                }

                ApplyStageDataJson(data, jsonData, name);

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



        // 髻ｳ貅舌ヵ繧｡繧､繝ｫ隱ｭ縺ｿ霎ｼ縺ｿ

        string[] audioExtensions = { ".wav", ".mp3", ".ogg", ".m4a" };

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

                        // MP3/OGG/M4A 縺ｯ繧ｨ繝・ぅ繧ｿ譎・AssetDatabase縲∝ｮ溯｡梧凾縺ｯ UnityWebRequest 縺ｧ蛻･騾碑ｪｭ縺ｿ霎ｼ縺ｿ

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



        // 蜍慕判繝輔ぃ繧､繝ｫ隱ｭ縺ｿ霎ｼ縺ｿ

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

                    // 螳溯｡梧凾縺ｯ蛻･騾・UnityWebRequest 縺ｧ髱槫酔譛溯ｪｭ縺ｿ霎ｼ縺ｿ縺悟ｿ・ｦ・

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

            multiBulletSpawners = new List<MultiBulletSpawner>(),

            bossSpawners = new List<BossSpawner>(),

            bulletSpawners = new List<BulletSpawner>()

        };



        StageDataJson jsonData = JsonUtility.FromJson<StageDataJson>(json);
        NormalizeStageDataJsonLists(jsonData);
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



        data.multiBulletSpawners = ConvertMultiBulletSpawners(jsonData);



        data.bossSpawners = ConvertBossSpawners(jsonData.bossSpawners);



        if (jsonData.bulletSpawners != null)

        {

            data.bulletSpawners = new List<BulletSpawner>(jsonData.bulletSpawners.Count);

            foreach (BulletSpawnerJson spawner in jsonData.bulletSpawners)

            {

                data.bulletSpawners.Add(spawner.ToBulletSpawner());

            }

        }



        ApplyStageDataJson(data, jsonData, directoryName);
        return data;

    }



    private static void NormalizeStageDataJsonLists(StageDataJson jsonData)
    {
        if (jsonData == null) return;

        if (jsonData.MusicEvents == null) jsonData.MusicEvents = new List<MusicEventJson>();
        if (jsonData.enemyVisuals == null) jsonData.enemyVisuals = new List<EnemyVisualDefinition>();
        if (jsonData.difficulties == null) jsonData.difficulties = new List<StageDifficultyDataJson>();
        if (jsonData.multiBulletSpawners == null) jsonData.multiBulletSpawners = new List<MultiBulletSpawnerJson>();
        if (jsonData.bossSpawners == null) jsonData.bossSpawners = new List<BossSpawnerJson>();
        if (jsonData.bulletSpawners == null) jsonData.bulletSpawners = new List<BulletSpawnerJson>();
    }

    private static void ApplyStageDataJson(StageData data, StageDataJson jsonData, string fallbackStageName)
    {
        if (data == null || jsonData == null) return;

        NormalizeStageDataJsonLists(jsonData);

        data.stageName = string.IsNullOrWhiteSpace(jsonData.stageName) ? fallbackStageName : jsonData.stageName;
        data.delayTime = jsonData.delayTime;
        data.stageDescription = jsonData.stageDescription;
        data.enemyVisuals = NormalizeEnemyVisualDefinitions(jsonData.enemyVisuals);
        data.MusicEvents = ConvertMusicEvents(jsonData.MusicEvents);
        data.difficulties = ConvertStageDifficulties(jsonData);

        if (data.difficulties.Count == 0 && HasLegacySpawnerData(jsonData))
        {
            data.difficulties.Add(new StageDifficultyData
            {
                difficulty = Difficulty.Lunatic,
                difficultyId = DifficultyUtility.GetId(Difficulty.Lunatic),
                displayName = DifficultyUtility.GetDisplayName(Difficulty.Lunatic),
                multiBulletSpawners = ConvertMultiBulletSpawners(jsonData.multiBulletSpawners),
                bossSpawners = ConvertBossSpawners(jsonData.bossSpawners),
                bulletSpawners = ConvertBulletSpawners(jsonData.bulletSpawners)
            });
        }

        data.SetActiveDifficultyForPreview(Difficulty.Lunatic);
    }

    private static List<StageData.MusicEvent> ConvertMusicEvents(List<MusicEventJson> source)
    {
        List<StageData.MusicEvent> musicEvents = new List<StageData.MusicEvent>();
        if (source == null) return musicEvents;

        for (int i = 0; i < source.Count; i++)
        {
            MusicEventJson musicEvent = source[i];
            if (musicEvent == null) continue;

            musicEvents.Add(musicEvent.ToMusicEvent());
        }

        return musicEvents;
    }

    private static List<StageDifficultyData> ConvertStageDifficulties(StageDataJson jsonData)
    {
        List<StageDifficultyData> difficulties = new List<StageDifficultyData>();
        if (jsonData == null || jsonData.difficulties == null) return difficulties;

        for (int i = 0; i < jsonData.difficulties.Count; i++)
        {
            StageDifficultyDataJson difficultyJson = jsonData.difficulties[i];
            if (difficultyJson == null) continue;

            Difficulty difficulty = ParseDifficulty(difficultyJson.difficulty, Difficulty.Normal);
            string difficultyId = DifficultyUtility.NormalizeId(difficultyJson.difficulty);
            if (string.IsNullOrWhiteSpace(difficultyId))
            {
                difficultyId = DifficultyUtility.GetId(difficulty);
            }

            difficulties.Add(new StageDifficultyData
            {
                difficulty = difficulty,
                difficultyId = difficultyId,
                displayName = string.IsNullOrWhiteSpace(difficultyJson.displayName)
                    ? DifficultyUtility.GetDisplayName(difficultyId)
                    : difficultyJson.displayName.Trim(),
                multiBulletSpawners = ConvertMultiBulletSpawners(difficultyJson.multiBulletSpawners),
                bossSpawners = ConvertBossSpawners(difficultyJson.bossSpawners),
                bulletSpawners = ConvertBulletSpawners(difficultyJson.bulletSpawners)
            });
        }

        return difficulties;
    }

    private static Difficulty ParseDifficulty(string value, Difficulty fallback)
    {
        if (DifficultyUtility.TryParseOfficial(value, out Difficulty parsedDifficulty))
        {
            return parsedDifficulty;
        }

        return fallback;
    }

    private static bool HasLegacySpawnerData(StageDataJson jsonData)
    {
        if (jsonData == null) return false;

        return (jsonData.multiBulletSpawners != null && jsonData.multiBulletSpawners.Count > 0)
            || (jsonData.bossSpawners != null && jsonData.bossSpawners.Count > 0)
            || (jsonData.bulletSpawners != null && jsonData.bulletSpawners.Count > 0);
    }

    private static List<MultiBulletSpawner> ConvertMultiBulletSpawners(List<MultiBulletSpawnerJson> source)
    {
        List<MultiBulletSpawner> spawners = new List<MultiBulletSpawner>();

        if (source == null)
        {
            return spawners;
        }

        for (int i = 0; i < source.Count; i++)
        {
            MultiBulletSpawnerJson spawner = source[i];

            if (spawner != null)
            {
                spawners.Add(spawner.ToMultiBulletSpawner());
            }
        }

        return spawners;
    }

    private static List<BulletSpawner> ConvertBulletSpawners(List<BulletSpawnerJson> source)
    {
        List<BulletSpawner> spawners = new List<BulletSpawner>();

        if (source == null)
        {
            return spawners;
        }

        for (int i = 0; i < source.Count; i++)
        {
            spawners.Add(source[i].ToBulletSpawner());
        }

        return spawners;
    }

    private static List<MultiBulletSpawner> ConvertMultiBulletSpawners(StageDataJson jsonData)

    {

        List<MultiBulletSpawnerJson> source = jsonData.multiBulletSpawners;

        List<MultiBulletSpawner> spawners = new List<MultiBulletSpawner>();

        if (source == null)

        {

            return spawners;

        }



        for (int i = 0; i < source.Count; i++)

        {

            MultiBulletSpawnerJson spawner = source[i];

            if (spawner != null)

            {

                spawners.Add(spawner.ToMultiBulletSpawner());

            }

        }



        return spawners;

    }



    private static List<BossSpawner> ConvertBossSpawners(List<BossSpawnerJson> source)

    {

        List<BossSpawner> bossSpawners = new List<BossSpawner>();

        if (source == null)

        {

            return bossSpawners;

        }



        for (int i = 0; i < source.Count; i++)

        {

            BossSpawnerJson spawner = source[i];

            if (spawner != null)

            {

                bossSpawners.Add(spawner.ToBossSpawner());

            }

        }



        return bossSpawners;

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

            // WAV 繝倥ャ繝隗｣譫・

            string riffHeader = new string(reader.ReadChars(4));

            if (riffHeader != "RIFF")

            {

                throw new System.Exception("Invalid WAV file: RIFF header not found");

            }



            reader.ReadInt32(); // 繝輔ぃ繧､繝ｫ繧ｵ繧､繧ｺ

            string waveHeader = new string(reader.ReadChars(4));

            if (waveHeader != "WAVE")

            {

                throw new System.Exception("Invalid WAV file: WAVE header not found");

            }



            // fmt 繝√Ε繝ｳ繧ｯ讀懃ｴ｢

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


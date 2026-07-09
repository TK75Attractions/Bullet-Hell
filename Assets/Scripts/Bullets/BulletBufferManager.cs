using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using System.Collections.Generic;
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

[Serializable]
public class BulletBufferManager
{
    private const string BulletBufferDirectoryName = "BulletBuffers";
    private const string CommonBulletBufferLabel = "bullet-buffer-common";
    private const string StageBulletBufferLabelPrefix = "bullet-buffer-stage-";
    private const bool UseAddressablesInEditor = false;
    private static readonly HashSet<string> CommonDirectoryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "common", "debug" };

    [SerializeField] private List<BulletBuffer> bulletBuffers = new List<BulletBuffer>();
    [NonSerialized] private HashSet<string> loadedDirectoryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    [Serializable]
    private class BulletBufferJson
    {
        public string name;
        public List<BulletDataJson> bullets;
        public bool homing;
        public bool isLaser;
    }

    [Serializable]
    private class BulletBuffer
    {
        public string name;
        public List<BulletData> bullets;
        public bool homing;
        public bool isLaser;

        public BulletBuffer(string name, List<BulletData> bullets, bool homing = false, bool isLaser = false)
        {
            this.name = name;
            this.bullets = bullets;
            this.homing = homing;
            this.isLaser = isLaser;
        }
    }

    public async Task InitAsync()
    {
        await LoadBaseBulletBuffersAsync();
    }

    public async Task ReloadForStageBulletBuffersAsync(string stageDirectoryName)
    {
        await LoadBaseBulletBuffersAsync();
        await LoadStageBulletBuffersAsync(stageDirectoryName);
    }

    public async Task ReloadForModStageBulletBuffersAsync(StageData stageData)
    {
        await LoadBaseBulletBuffersAsync();
        LoadModStageBulletBuffers(stageData);
    }

    public void UnloadAllBulletBuffers()
    {
        bulletBuffers.Clear();
        EnsureLoadedDirectorySet();
        loadedDirectoryNames.Clear();
    }

    public async Task LoadStageBulletBuffersAsync(string stageDirectoryName)
    {
        if (string.IsNullOrWhiteSpace(stageDirectoryName) || CommonDirectoryNames.Contains(stageDirectoryName))
        {
            return;
        }

        EnsureLoadedDirectorySet();
        if (loadedDirectoryNames.Contains(stageDirectoryName))
        {
            return;
        }

        int loadedCount = ShouldUseAddressables()
            ? await ReadBulletBuffersFromAddressablesAsync(StageBulletBufferLabelPrefix + stageDirectoryName)
            : 0;
        if (loadedCount == 0)
        {
            loadedCount = ReadBulletBuffersFromDirectory(stageDirectoryName);
        }

        loadedDirectoryNames.Add(stageDirectoryName);
        Debug.Log($"Loaded {loadedCount} stage bullet buffers for '{stageDirectoryName}'");
    }

    public void LoadModStageBulletBuffers(StageData stageData)
    {
        if (stageData == null || stageData.source != StageData.StageSource.Mod)
        {
            return;
        }

        string directoryPath = stageData.bulletBufferDirectory;
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            return;
        }

        EnsureLoadedDirectorySet();
        string key = Path.GetFullPath(directoryPath);
        if (loadedDirectoryNames.Contains(key))
        {
            return;
        }

        int loadedCount = ReadBulletBuffersFromAbsoluteDirectory(directoryPath);
        loadedDirectoryNames.Add(key);
        Debug.Log($"Loaded {loadedCount} mod bullet buffers for '{stageData.stageName}' from {directoryPath}");
    }

    private void ResetBuffers()
    {
        bulletBuffers.Clear();
        EnsureLoadedDirectorySet();
        loadedDirectoryNames.Clear();

        bulletBuffers.AddRange(Rumia());
        bulletBuffers.Add(Line());
        bulletBuffers.Add(LineLaser());
        bulletBuffers.Add(Circle());
    }

    private async Task LoadBaseBulletBuffersAsync()
    {
        ResetBuffers();

        int addressableLoadCount = ShouldUseAddressables()
            ? await ReadBulletBuffersFromAddressablesAsync(CommonBulletBufferLabel)
            : 0;
        if (addressableLoadCount == 0)
        {
            ReadCommonBulletBuffersFromDirectory();
        }

        ReadCommonModBulletBuffersFromDirectories();
    }

    private void EnsureLoadedDirectorySet()
    {
        if (loadedDirectoryNames == null)
        {
            loadedDirectoryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void ReadCommonBulletBuffersFromDirectory()
    {
        foreach (string directoryName in CommonDirectoryNames)
        {
            ReadBulletBuffersFromDirectory(directoryName);
            loadedDirectoryNames.Add(directoryName);
        }
    }

    private void ReadCommonModBulletBuffersFromDirectories()
    {
        foreach (string modsRoot in GetModRootDirectories())
        {
            if (!Directory.Exists(modsRoot))
            {
                continue;
            }

            foreach (string modDirectory in Directory.GetDirectories(modsRoot))
            {
                foreach (string commonDirectoryName in CommonDirectoryNames)
                {
                    string commonDirectory = Path.Combine(modDirectory, BulletBufferDirectoryName, commonDirectoryName);
                    if (!Directory.Exists(commonDirectory))
                    {
                        continue;
                    }

                    string key = Path.GetFullPath(commonDirectory);
                    if (loadedDirectoryNames.Contains(key))
                    {
                        continue;
                    }

                    int loadedCount = ReadBulletBuffersFromAbsoluteDirectory(commonDirectory);
                    loadedDirectoryNames.Add(key);
                    Debug.Log($"Loaded {loadedCount} mod common bullet buffers from {commonDirectory}");
                }
            }
        }
    }

    private IEnumerable<string> GetModRootDirectories()
    {
        HashSet<string> directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        directories.Add(Path.GetFullPath(Path.Combine(Application.persistentDataPath, "Mods")));
        directories.Add(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Mods")));
        return directories;
    }

    private int ReadBulletBuffersFromDirectory(string directoryName)
    {
        string directoryPath = Path.Combine(Application.dataPath, BulletBufferDirectoryName, directoryName);
        if (!Directory.Exists(directoryPath))
        {
            Debug.LogWarning($"Bullet buffer directory not found: {directoryPath}");
            return 0;
        }

        string[] jsonFiles = Directory.GetFiles(directoryPath, "*.json", SearchOption.AllDirectories);
        int loadedCount = 0;

        for (int i = 0; i < jsonFiles.Length; i++)
        {
            BulletBuffer buffer = ReadBulletBufferFromFile(jsonFiles[i]);
            if (buffer == null)
            {
                continue;
            }

            AddOrReplaceBulletBuffer(buffer);
            loadedCount++;
        }

        Debug.Log($"Loaded {loadedCount}/{jsonFiles.Length} bullet buffer json files from {directoryPath}");
        return loadedCount;
    }

    private int ReadBulletBuffersFromAbsoluteDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            Debug.LogWarning($"Bullet buffer directory not found: {directoryPath}");
            return 0;
        }

        string[] jsonFiles = Directory.GetFiles(directoryPath, "*.json", SearchOption.AllDirectories);
        int loadedCount = 0;

        for (int i = 0; i < jsonFiles.Length; i++)
        {
            BulletBuffer buffer = ReadBulletBufferFromFile(jsonFiles[i]);
            if (buffer == null)
            {
                continue;
            }

            AddOrReplaceBulletBuffer(buffer);
            loadedCount++;
        }

        return loadedCount;
    }

    private async Task<int> ReadBulletBuffersFromAddressablesAsync(string label)
    {
        AsyncOperationHandle<IList<IResourceLocation>> locationsHandle = default;
        int loadedCount = 0;

        try
        {
            locationsHandle = Addressables.LoadResourceLocationsAsync(label, typeof(TextAsset));
            await WaitForAddressable(locationsHandle);

            if (locationsHandle.Status != AsyncOperationStatus.Succeeded ||
                locationsHandle.Result == null ||
                locationsHandle.Result.Count == 0)
            {
                return 0;
            }

            foreach (IResourceLocation location in locationsHandle.Result)
            {
                AsyncOperationHandle<TextAsset> jsonHandle = Addressables.LoadAssetAsync<TextAsset>(location);
                await WaitForAddressable(jsonHandle);

                if (jsonHandle.Status != AsyncOperationStatus.Succeeded || jsonHandle.Result == null)
                {
                    Debug.LogWarning($"Failed to load addressable bullet buffer json: {location.PrimaryKey}");
                    if (jsonHandle.IsValid()) Addressables.Release(jsonHandle);
                    continue;
                }

                BulletBuffer buffer = ReadBulletBufferFromJson(location.PrimaryKey, jsonHandle.Result.text);
                if (jsonHandle.IsValid()) Addressables.Release(jsonHandle);

                if (buffer == null)
                {
                    continue;
                }

                AddOrReplaceBulletBuffer(buffer);
                loadedCount++;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Addressable bullet buffer load failed for label '{label}'. {ex.Message}");
            return 0;
        }
        finally
        {
            if (locationsHandle.IsValid())
            {
                Addressables.Release(locationsHandle);
            }
        }

        return loadedCount;
    }

    private bool ShouldUseAddressables()
    {
#if UNITY_EDITOR
        return UseAddressablesInEditor;
#else
        return true;
#endif
    }

    private BulletBuffer ReadBulletBufferFromFile(string fileName)
    {
        string filePath = Path.IsPathRooted(fileName)
            ? fileName
            : Path.Combine(Application.dataPath, BulletBufferDirectoryName, fileName);

        if (!File.Exists(filePath))
        {
            Debug.LogWarning($"Bullet buffer file not found: {filePath}");
            return null;
        }

        try
        {
            string json = File.ReadAllText(filePath);
            return ReadBulletBufferFromJson(filePath, json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Exception while reading bullet buffer file {filePath}: {ex.Message}");
            return null;
        }
    }

    private BulletBuffer ReadBulletBufferFromJson(string sourceName, string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogWarning($"Bullet buffer json is empty: {sourceName}");
            return null;
        }

        BulletBufferJson data = JsonUtility.FromJson<BulletBufferJson>(json);
        if (data == null)
        {
            Debug.LogWarning($"Failed to parse bullet buffer json: {sourceName}");
            return null;
        }
        ApplyBulletDataJsonDefaults(data, json);

        if (string.IsNullOrWhiteSpace(data.name))
        {
            data.name = Path.GetFileNameWithoutExtension(sourceName);
        }

        if (data.bullets == null)
        {
            Debug.LogWarning($"Bullet list is missing in json: {sourceName}");
            return null;
        }

        return new BulletBuffer(data.name, data.bullets.ConvertAll(b => b.ToBulletData()), data.homing, data.isLaser);
    }

    private static void ApplyBulletDataJsonDefaults(BulletBufferJson data, string json)
    {
        if (data == null || data.bullets == null || data.bullets.Count == 0) return;

        List<string> bulletJsonObjects = ExtractJsonArrayObjects(json, "bullets");
        if (bulletJsonObjects.Count != data.bullets.Count)
        {
            if (!ContainsJsonProperty(json, nameof(BulletDataJson.useVelocityAngle)))
            {
                SetDefaultUseVelocityAngle(data.bullets);
            }
            return;
        }

        for (int i = 0; i < data.bullets.Count; i++)
        {
            if (data.bullets[i] == null) continue;

            if (!ContainsJsonProperty(bulletJsonObjects[i], nameof(BulletDataJson.useVelocityAngle)))
            {
                data.bullets[i].useVelocityAngle = true;
            }
        }
    }

    private static void SetDefaultUseVelocityAngle(List<BulletDataJson> bullets)
    {
        for (int i = 0; i < bullets.Count; i++)
        {
            if (bullets[i] == null) continue;
            bullets[i].useVelocityAngle = true;
        }
    }

    private static List<string> ExtractJsonArrayObjects(string json, string arrayPropertyName)
    {
        List<string> objects = new List<string>();
        if (string.IsNullOrEmpty(json)) return objects;

        int propertyIndex = FindJsonPropertyIndex(json, arrayPropertyName);
        if (propertyIndex < 0) return objects;

        int colonIndex = json.IndexOf(':', propertyIndex);
        if (colonIndex < 0) return objects;

        int arrayStart = json.IndexOf('[', colonIndex + 1);
        if (arrayStart < 0) return objects;

        bool inString = false;
        bool escaped = false;
        int objectDepth = 0;
        int objectStart = -1;

        for (int i = arrayStart + 1; i < json.Length; i++)
        {
            char c = json[i];
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (c == '\\')
                {
                    escaped = true;
                }
                else if (c == '"')
                {
                    inString = false;
                }
                continue;
            }

            if (c == '"')
            {
                inString = true;
                continue;
            }

            if (c == '{')
            {
                if (objectDepth == 0) objectStart = i;
                objectDepth++;
                continue;
            }

            if (c == '}')
            {
                objectDepth--;
                if (objectDepth == 0 && objectStart >= 0)
                {
                    objects.Add(json.Substring(objectStart, i - objectStart + 1));
                    objectStart = -1;
                }
                continue;
            }

            if (c == ']' && objectDepth == 0)
            {
                break;
            }
        }

        return objects;
    }

    private static bool ContainsJsonProperty(string json, string propertyName)
    {
        return FindJsonPropertyIndex(json, propertyName) >= 0;
    }

    private static int FindJsonPropertyIndex(string json, string propertyName)
    {
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(propertyName)) return -1;

        string quotedPropertyName = $"\"{propertyName}\"";
        int searchIndex = 0;
        while (searchIndex < json.Length)
        {
            int propertyIndex = json.IndexOf(quotedPropertyName, searchIndex, StringComparison.Ordinal);
            if (propertyIndex < 0) return -1;

            int afterPropertyName = propertyIndex + quotedPropertyName.Length;
            while (afterPropertyName < json.Length && char.IsWhiteSpace(json[afterPropertyName]))
            {
                afterPropertyName++;
            }

            if (afterPropertyName < json.Length && json[afterPropertyName] == ':')
            {
                return propertyIndex;
            }

            searchIndex = propertyIndex + quotedPropertyName.Length;
        }

        return -1;
    }

    private void AddOrReplaceBulletBuffer(BulletBuffer buffer)
    {
        if (TryGetBulletBufferIndex(buffer.name, out int existingIndex))
        {
            bulletBuffers[existingIndex] = buffer;
        }
        else
        {
            bulletBuffers.Add(buffer);
        }
    }

    private async Task WaitForAddressable<T>(AsyncOperationHandle<T> handle)
    {
        while (!handle.IsDone)
        {
            await Task.Yield();
        }
    }


    #region Bullet Clips
    private List<BulletBuffer> Rumia()
    {
        List<BulletBuffer> buffers = new List<BulletBuffer>();
        List<BulletData> ru0 = new List<BulletData>();
        List<BulletData> ru1 = new List<BulletData>();

        for (int i = 0; i < 16; i++)
        {
            BulletData b = new BulletData(
                new float2(0, 0),
                new float2(0, 0),
                4.2f + 0.25f * i,
                0,
                0,
                0,
                new float2(1, 0.14f * i - 0.56f),
                0,
                0,
                0,
                new float4(0, 0, 0, 0),
                1,
                new float4(0, 0, 0.5f, 1)
            );
            BulletData b1 = b;
            b1.speed -= 0.7f;
            BulletData b2 = b;
            b2.speed -= 1.4f;

            ru0.Add(b);
            ru0.Add(b1);
            ru0.Add(b2);

            BulletData b3 = new BulletData(
                new float2(0, 0),
                new float2(0, 0),
                4.2f + 0.25f * i,
                0,
                0,
                0,
                new float2(1, -0.14f * i + 0.56f),
                0,
                0,
                0,
                new float4(0, 0, 0, 0),
                1,
                new float4(0.1f, 0.4f, 0.6f, 1)
            );
            BulletData b4 = b3;
            b4.speed -= 0.7f;
            BulletData b5 = b3;
            b5.speed -= 1.4f;
            ru1.Add(b3);
            ru1.Add(b4);
            ru1.Add(b5);
        }

        buffers.Add(new BulletBuffer("Rumia_0", ru0));
        buffers.Add(new BulletBuffer("Rumia_1", ru1));
        return buffers;
    }

    private BulletBuffer Line()
    {
        List<BulletData> line = new List<BulletData>();
        for (int i = 0; i < 16; i++)
        {
            BulletData b = new BulletData(
                new float2(0, 0),
                new float2(0, 0),
                3,
                0,
                0,
                0,
                new float2(1 + 0.1f * i, 0),
                0,
                0,
                0,
                new float4(0, 0, 0, 0),
                2,
                new float4(0.6f, 0, 0, 1)
            );
            line.Add(b);
        }

        return new BulletBuffer("Line", line);
    }

    private BulletBuffer LineLaser()
    {
        BulletData b = new BulletData(
            new float2(0, 0),
            new float2(0, 0),
            20,
            0,
            0,
            0,
            new float2(1, 0),
            0,
            0,
            0,
            new float4(0, 0, 0, 0),
            2,
            new float4(1, 0.5f, 0, 1),
            new float2(10, 10)
        );


        return new BulletBuffer("LineLaser", new List<BulletData> { b }, false, true);
    }

    private BulletBuffer Circle()
    {
        List<BulletData> circle = new List<BulletData>();
        for (int i = 0; i < 16; i++)
        {
            BulletData b = new BulletData(
                new float2(0, 0),
                new float2(0, 0),
                0,
                0,
                0,
                0,
                new float2(1, 2 * math.PI / 16 * i),
                0,
                4,
                0,
                new float4(0, 0, 0, 0),
                2,
                new float4(1, 0.5f, 0, 1)
            );
            b.startPos = new(-3, 0);
            circle.Add(b);
        }

        return new BulletBuffer("Circle", circle);
    }
    #endregion

    public bool TryGetBulletBufferIndex(string name, out int index)
    {
        index = -1;
        for (int i = 0; i < bulletBuffers.Count; i++)
        {
            if (bulletBuffers[i].name == name)
            {
                index = i;
                return true;
            }
        }
        return false;
    }

    public List<BulletData> CreateSpawnedBullets(int index, float2 pPos, float2 emitPos, float2 _vlc, float angle, float4 _color, out bool isLaser)
    {
        isLaser = false;
        if (bulletBuffers.Count == 0)
        {
            Debug.LogError("BulletBufferManager is not initialized.");
            return default;
        }

        if (index >= 0 && index < bulletBuffers.Count)
        {
            isLaser = bulletBuffers[index].isLaser;
            List<BulletData> templateBullets = bulletBuffers[index].bullets;
            List<BulletData> spawnedBullets = new List<BulletData>(templateBullets.Count);


            if (bulletBuffers[index].homing)
            {
                for (int i = 0; i < templateBullets.Count; i++)
                {
                    angle = math.atan2(pPos.y - emitPos.y, pPos.x - emitPos.x);
                    BulletData template = templateBullets[i];
                    float2 dis = -template.startPos;
                    BulletData spawned = new BulletData(template, emitPos, _vlc, angle, _color);
                    spawned.startPos -= dis;
                    spawned.position = spawned.GetInitialPosition();
                    spawned.velocity = new float2(0f, 0f);
                    spawnedBullets.Add(spawned);
                }

                return spawnedBullets;

            }
            else
            {
                for (int i = 0; i < templateBullets.Count; i++)
                {
                    BulletData template = templateBullets[i];
                    float2 dis = -template.startPos;
                    BulletData spawned = new BulletData(template, emitPos, _vlc, angle / 180 * math.PI, _color);
                    spawned.startPos -= dis;
                    spawned.position = spawned.GetInitialPosition();
                    spawned.velocity = new float2(0f, 0f);
                    spawnedBullets.Add(spawned);
                }

                return spawnedBullets;
            }


        }
        else
        {
            if (index == -3) return default; // "Clear" という特別なインデックスは、空の弾リストを返す
            Debug.LogError($"Bullet clip index out of range: {index}");
            return default;
        }
    }

    public bool TryGetBulletBuffer(int index, out BulletData buffer)
    {
        buffer = new();
        if (index >= 0 && index < bulletBuffers.Count && bulletBuffers[index].bullets != null && bulletBuffers[index].bullets.Count > 0)
        {
            buffer = bulletBuffers[index].bullets[0];
            return true;
        }
        return false;
    }
}

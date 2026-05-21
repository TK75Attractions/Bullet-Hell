using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using System.Collections.Generic;
using System;
using System.IO;

[Serializable]
public class BulletBufferManager
{
    [SerializeField] private List<BulletBuffer> bulletBuffers = new List<BulletBuffer>();

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

    public void Init()
    {
        bulletBuffers.Clear();
        bulletBuffers.AddRange(Rumia());
        bulletBuffers.Add(Line());
        bulletBuffers.Add(LineLaser());
        bulletBuffers.Add(Circle());

        ReadBulletBufferFromDirectory();
    }

    public void ReadBulletBufferFromDirectory()
    {
        string directoryPath = Path.Combine(Application.dataPath, "BulletBuffers");
        if (!Directory.Exists(directoryPath))
        {
            Debug.LogWarning($"Bullet buffer directory not found: {directoryPath}");
            return;
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

            if (TryGetBulletClipIndex(buffer.name, out int existingIndex))
            {
                bulletBuffers[existingIndex] = buffer;
            }
            else
            {
                bulletBuffers.Add(buffer);
            }

            loadedCount++;
        }

        Debug.Log($"Loaded {loadedCount}/{jsonFiles.Length} bullet buffer json files from {directoryPath}");
    }

    private BulletBuffer ReadBulletBufferFromFile(string fileName)
    {
        string filePath = Path.IsPathRooted(fileName)
            ? fileName
            : Path.Combine(Application.dataPath, "BulletBuffers", fileName);

        if (!File.Exists(filePath))
        {
            Debug.LogWarning($"Bullet buffer file not found: {filePath}");
            return null;
        }

        try
        {
            string json = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogWarning($"Bullet buffer json is empty: {filePath}");
                return null;
            }

            BulletBufferJson data = JsonUtility.FromJson<BulletBufferJson>(json);
            if (data == null)
            {
                Debug.LogWarning($"Failed to parse bullet buffer json: {filePath}");
                return null;
            }

            if (string.IsNullOrWhiteSpace(data.name))
            {
                data.name = Path.GetFileNameWithoutExtension(filePath);
            }

            if (data.bullets == null)
            {
                Debug.LogWarning($"Bullet list is missing in json: {filePath}");
                return null;
            }

            return new BulletBuffer(data.name, data.bullets.ConvertAll(b => b.ToBulletData()), data.homing, data.isLaser);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Exception while reading bullet buffer file {filePath}: {ex.Message}");
            return null;
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
            10
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

    public bool TryGetBulletClipIndex(string name, out int index)
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

    public List<BulletData> GetBulletClip(int index, float2 pPos, float2 emitPos, float2 _vlc, float angle, float4 _color, out bool isLaser)
    {
        isLaser = false;
        if (bulletBuffers.Count == 0)
        {
            Debug.LogError("BulletClipManager is not initialized.");
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
                    BulletData spawned = new BulletData(template, emitPos, _vlc, angle + template.polarForm.y, _color);
                    spawned.startPos -= dis;
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
                    BulletData spawned = new BulletData(template, emitPos, _vlc, angle / 180 * math.PI + template.polarForm.y, _color);
                    spawned.startPos -= dis;
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
}

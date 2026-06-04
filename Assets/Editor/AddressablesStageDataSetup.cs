using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

public static class AddressablesStageDataSetup
{
    private const string StageDataRoot = "Assets/StageData";
    private const string BulletBufferRoot = "Assets/BulletBuffers";
    private const string StageDataJsonLabel = "stage-data-json";
    private const string StageDataMediaLabel = "stage-data-media";
    private const string CommonBulletBufferLabel = "bullet-buffer-common";
    private const string StageBulletBufferLabelPrefix = "bullet-buffer-stage-";

    private static readonly HashSet<string> CommonBulletBufferDirectories =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "common", "debug" };

    private static readonly HashSet<string> StageMediaExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".wav", ".mp3", ".ogg", ".m4a", ".mp4", ".webm" };

    [MenuItem("Tools/Bullet Hell/Addressables/Configure Stage Data")]
    public static void ConfigureStageDataAddressables()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        if (settings == null)
        {
            Debug.LogError("AddressableAssetSettings could not be created.");
            return;
        }

        RegisterStageData(settings);
        RegisterBulletBuffers(settings);

        AssetDatabase.SaveAssets();
        Debug.Log("Configured Addressables for StageData and BulletBuffers.");
    }

    private static void RegisterStageData(AddressableAssetSettings settings)
    {
        if (!Directory.Exists(StageDataRoot))
        {
            Debug.LogWarning($"StageData directory not found: {StageDataRoot}");
            return;
        }

        foreach (string directory in Directory.GetDirectories(StageDataRoot))
        {
            string stageDirectory = ToAssetPath(directory);
            string stageName = Path.GetFileName(stageDirectory);
            AddressableAssetGroup group = GetOrCreateGroup(settings, $"StageData_{SanitizeGroupName(stageName)}");

            foreach (string file in Directory.GetFiles(stageDirectory, "*", SearchOption.TopDirectoryOnly))
            {
                string assetPath = ToAssetPath(file);
                string extension = Path.GetExtension(assetPath);
                if (extension.Equals(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                AddressableAssetEntry entry = CreateOrMoveEntry(settings, group, assetPath);
                if (entry == null)
                {
                    continue;
                }

                if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
                {
                    AddLabel(settings, entry, StageDataJsonLabel);
                }
                else if (StageMediaExtensions.Contains(extension))
                {
                    AddLabel(settings, entry, StageDataMediaLabel);
                }

                AddLabel(settings, entry, $"stage-{stageName}");
            }
        }
    }

    private static void RegisterBulletBuffers(AddressableAssetSettings settings)
    {
        if (!Directory.Exists(BulletBufferRoot))
        {
            Debug.LogWarning($"BulletBuffers directory not found: {BulletBufferRoot}");
            return;
        }

        AddressableAssetGroup commonGroup = GetOrCreateGroup(settings, "BulletBuffers_Common");

        foreach (string directory in Directory.GetDirectories(BulletBufferRoot))
        {
            string bulletBufferDirectory = ToAssetPath(directory);
            string directoryName = Path.GetFileName(bulletBufferDirectory);
            bool isCommon = CommonBulletBufferDirectories.Contains(directoryName);
            string label = isCommon ? CommonBulletBufferLabel : StageBulletBufferLabelPrefix + directoryName;
            AddressableAssetGroup group = isCommon
                ? commonGroup
                : GetOrCreateGroup(settings, $"BulletBuffers_{SanitizeGroupName(directoryName)}");

            foreach (string file in Directory.GetFiles(bulletBufferDirectory, "*.json", SearchOption.AllDirectories))
            {
                string assetPath = ToAssetPath(file);
                AddressableAssetEntry entry = CreateOrMoveEntry(settings, group, assetPath);
                if (entry == null)
                {
                    continue;
                }

                AddLabel(settings, entry, label);
            }
        }
    }

    private static AddressableAssetEntry CreateOrMoveEntry(
        AddressableAssetSettings settings,
        AddressableAssetGroup group,
        string assetPath)
    {
        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrWhiteSpace(guid))
        {
            Debug.LogWarning($"Asset guid was not found: {assetPath}");
            return null;
        }

        AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group);
        entry.address = assetPath;
        return entry;
    }

    private static AddressableAssetGroup GetOrCreateGroup(AddressableAssetSettings settings, string groupName)
    {
        AddressableAssetGroup group = settings.FindGroup(groupName);
        if (group != null)
        {
            return group;
        }

        List<AddressableAssetGroupSchema> schemas = settings.DefaultGroup != null
            ? settings.DefaultGroup.Schemas
            : null;
        return settings.CreateGroup(groupName, false, false, true, schemas);
    }

    private static void AddLabel(
        AddressableAssetSettings settings,
        AddressableAssetEntry entry,
        string label)
    {
        settings.AddLabel(label);
        entry.SetLabel(label, true, true);
    }

    private static string ToAssetPath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static string SanitizeGroupName(string value)
    {
        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalidChar, '_');
        }

        return value.Replace(' ', '_');
    }
}

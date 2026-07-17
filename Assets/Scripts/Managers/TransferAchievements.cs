using System;
using System.Collections.Generic;
using UnityEngine;

// 方向シーケンス引き継ぎコードの元になる実績データ(1P専用。2P は SPEC §1.4 により
// 引き継ぎ対象外)。PlayHistory(プレイ/クリア回数)とは別の永続領域に、
// ステージ×難易度のクリアフラグとステージ単位のノーミスフラグだけを持つ。
//
// 保存先: PlayerPrefs キー "transferAchievements.v1"。JSON:
//   { "v":1, "clear":[bool x12], "noMiss":[bool x4] }
public static class TransferAchievements
{
    private const string PrefsKey = "transferAchievements.v1";

    [Serializable]
    private class Model
    {
        public int v = 1;
        public bool[] clear = new bool[DirectionTransferCode.ClearBits];
        public bool[] noMiss = new bool[DirectionTransferCode.NoMissBits];
    }

    private static Model cache;

    private static void Load()
    {
        if (cache != null) return;
        cache = new Model();
        string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            Model loaded = JsonUtility.FromJson<Model>(json);
            if (loaded == null) return;
            if (loaded.clear != null && loaded.clear.Length == DirectionTransferCode.ClearBits)
                cache.clear = loaded.clear;
            if (loaded.noMiss != null && loaded.noMiss.Length == DirectionTransferCode.NoMissBits)
                cache.noMiss = loaded.noMiss;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[TransferAchievements] Failed to parse stored data: {ex.Message}");
            cache = new Model();
        }
    }

    private static void Save()
    {
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(cache));
        PlayerPrefs.Save();
    }

    // stageDirectoryName が DirectionTransferCode.StageOrder に無い(デバッグ/未公式
    // ステージ)場合は何もしない。difficulty は 0=Easy/1=Normal/2=Lunatic。
    public static void RecordClear(string stageDirectoryName, int difficulty, bool noMiss)
    {
        int stageIndex = Array.IndexOf(DirectionTransferCode.StageOrder, stageDirectoryName);
        if (stageIndex < 0) return;
        Load();

        int diffIndex = Mathf.Clamp(difficulty, 0, DirectionTransferCode.DifficultyCount - 1);
        int clearIndex = stageIndex * DirectionTransferCode.DifficultyCount + diffIndex;
        cache.clear[clearIndex] = true;
        if (noMiss) cache.noMiss[stageIndex] = true;
        Save();
    }

    public static bool HasAnyAchievement
    {
        get
        {
            Load();
            foreach (bool b in cache.clear) if (b) return true;
            foreach (bool b in cache.noMiss) if (b) return true;
            return false;
        }
    }

    public static DirectionTransferCode.Payload BuildPayload()
    {
        Load();
        return new DirectionTransferCode.Payload
        {
            Clear = (bool[])cache.clear.Clone(),
            NoMiss = (bool[])cache.noMiss.Clone()
        };
    }

    // 取り込みは既存実績とのOR合成(引き継ぎで進捗が後退/消失しない)。
    public static void ApplyPayload(DirectionTransferCode.Payload payload)
    {
        Load();
        for (int i = 0; i < DirectionTransferCode.ClearBits; i++)
        {
            if (payload.Clear != null && i < payload.Clear.Length && payload.Clear[i]) cache.clear[i] = true;
        }
        for (int i = 0; i < DirectionTransferCode.NoMissBits; i++)
        {
            if (payload.NoMiss != null && i < payload.NoMiss.Length && payload.NoMiss[i]) cache.noMiss[i] = true;
        }
        Save();
    }

    public static void ClearAll()
    {
        cache = new Model();
        PlayerPrefs.DeleteKey(PrefsKey);
        PlayerPrefs.Save();
    }

    private static readonly Dictionary<string, string> DisplayNames = new Dictionary<string, string>
    {
        { "captain", "艦長" },
        { "stone", "石工" },
        { "vagrant", "浮浪者" },
        { "mirror", "領主様の姿見" }
    };

    public static string StageDisplayName(string stageDirectoryName)
    {
        return DisplayNames.TryGetValue(stageDirectoryName, out string name) ? name : stageDirectoryName;
    }

    // リザルト/引き継ぎ確認画面向けの要約行(「艦長 NORMAL クリア済み」等)。
    // クリア済みの組み合わせが無ければ空配列。
    public static List<string> SummaryLines(DirectionTransferCode.Payload payload)
    {
        List<string> lines = new List<string>();
        for (int s = 0; s < DirectionTransferCode.StageCount; s++)
        {
            List<string> clearedDiffs = new List<string>();
            for (int d = 0; d < DirectionTransferCode.DifficultyCount; d++)
            {
                int idx = s * DirectionTransferCode.DifficultyCount + d;
                if (payload.Clear != null && idx < payload.Clear.Length && payload.Clear[idx])
                {
                    clearedDiffs.Add(DifficultyUtility.GetDisplayName((Difficulty)d));
                }
            }
            if (clearedDiffs.Count == 0) continue;

            string stageName = StageDisplayName(DirectionTransferCode.StageOrder[s]);
            string noMissTag = (payload.NoMiss != null && s < payload.NoMiss.Length && payload.NoMiss[s])
                ? "(ノーミス達成)" : "";
            lines.Add($"{stageName}: {string.Join("/", clearedDiffs)} クリア済み{noMissTag}");
        }
        return lines;
    }
}

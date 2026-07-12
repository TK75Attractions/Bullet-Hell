using UnityEngine;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

[Serializable]
public class StageDataBase
{
    [SerializeField] private List<StageData> stages;
    private StageDataManager stageDataManager;

    // 一覧の先頭に固定表示する主要ステージ(2026-07-12 指摘)。stageDirectoryName で
    // 照合する。進捗/引き継ぎコードは PlayHistory が stageDirectoryName でキー付け
    // するため、この並べ替えはセーブデータに一切影響しない(index 非依存)。
    private static readonly string[] PreferredOrder = { "stone", "captain", "vagrant" };

    public void Init()
    {
        if (stages == null)
        {
            stageDataManager = new StageDataManager();
            stages = stageDataManager.GetAllStageData();
            ApplyPreferredOrder(stages);
            Debug.Log($"Loaded {stages.Count} stages from JSON");
        }
    }

    public async Task InitAsync()
    {
        if (stages == null)
        {
            stageDataManager = new StageDataManager();
            stages = await stageDataManager.GetAllStageDataAsync();
            ApplyPreferredOrder(stages);
            Debug.Log($"Loaded {stages.Count} stages");
        }
    }

    // PreferredOrder のステージを、その並び順で一覧の先頭へ移動する。残りは元の
    // 相対順を保つ(安定並べ替え)。見つからないステージは無視する。
    private static void ApplyPreferredOrder(List<StageData> list)
    {
        if (list == null || list.Count == 0) return;
        for (int i = PreferredOrder.Length - 1; i >= 0; i--)
        {
            int idx = list.FindIndex(s => s != null && s.stageDirectoryName == PreferredOrder[i]);
            if (idx > 0)
            {
                StageData s = list[idx];
                list.RemoveAt(idx);
                list.Insert(0, s);
            }
        }
    }

    public StageData GetStage(int index)
    {
        if (stages == null || index < 0 || index >= stages.Count)
        {
            Debug.LogWarning($"StageData at index {index} is out of range! Returning null.");
            return null;
        }
        return stages[index];
    }

    public int GetStageCount()
    {
        if (stages == null) return 0;
        return stages.Count;
    }

    public List<StageData> GetAllStages()
    {
        if (stages == null) return new List<StageData>();
        return stages;
    }

    public async Task EnsureRuntimeMediaLoadedAsync(StageData data)
    {
        if (stageDataManager == null)
        {
            stageDataManager = new StageDataManager();
        }

        await stageDataManager.EnsureRuntimeMediaLoadedAsync(data);
    }
}

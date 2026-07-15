using UnityEngine;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

[Serializable]
public class StageDataBase
{
    [SerializeField] private List<StageData> stages;
    private StageDataManager stageDataManager;

    // 一覧に表示するステージと表示順(2026-07-13 指摘: ①艦長 ②石工 ③浮浪者 ④姿見)。
    // stageDirectoryName で照合し、この配列に無いステージは一覧から除外(非表示)する。
    // 削除ではなく非表示なので、JSON は一切消さず、この配列を戻せば全ステージが再表示
    // される。進捗/引き継ぎコードは PlayHistory が stageDirectoryName でキー付けするため、
    // この並べ替え・絞り込みはセーブデータに一切影響しない(index 非依存)。
    // ※ mirror(領主様の姿見)は現状スタブ(単一レーザー・difficulties 未設定)。
    private static readonly string[] VisibleOrder = { "captain", "stone", "vagrant", "mirror" };

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

    // VisibleOrder に載っているステージだけを、その並び順で一覧に残す(それ以外は
    // 非表示)。見つからないステージは無視する。万一 1 つも該当しない場合は安全側で
    // 元の一覧をそのまま残す(空一覧で選択画面が壊れるのを防ぐ)。
    private static void ApplyPreferredOrder(List<StageData> list)
    {
        if (list == null || list.Count == 0) return;
        List<StageData> ordered = new List<StageData>(VisibleOrder.Length);
        foreach (string dir in VisibleOrder)
        {
            int idx = list.FindIndex(s => s != null && s.stageDirectoryName == dir);
            if (idx >= 0) ordered.Add(list[idx]);
        }
        if (ordered.Count == 0) return;
        list.Clear();
        list.AddRange(ordered);
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

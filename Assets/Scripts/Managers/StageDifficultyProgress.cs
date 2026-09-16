using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 「ステージ × 難易度」ごとの挑戦/クリアの記録(2026-09-16 U5)。
/// ステージ選択の右パネルの STATUS 行(EASY / NORMAL / LUNATIC の菱形)だけが使う。
///
/// 既存の保存領域とは別に持つ:
///   - <see cref="PlayHistory"/> はステージ単位のプレイ/クリア回数しか持たない(難易度を区別しない)。
///   - <see cref="TransferAchievements"/> は難易度別だが「クリア」だけで、しかも 1P 専用。
/// ここは 1P/2P を合算した「挑戦したか / クリアしたか」の 2 ビットを難易度ごとに持つ
/// (2026-09-16 ユーザー指示「2P は別扱いにせず合算でよい」)。
///
/// 保存先: PlayerPrefs キー "stageDiffProgress.v1"。JSON:
///   { "v":1, "e":[ {"k":"stone","p":[true,false,false],"c":[true,false,false]} ] }
///
/// クリアの読み出しは、この記録が無いときだけ <see cref="TransferAchievements"/> を見る
/// (この機能より前の 1P クリア履歴を STATUS 行に出すため)。
/// </summary>
public static class StageDifficultyProgress
{
    private const string PrefsKey = "stageDiffProgress.v1";

    /// <summary>難易度の数(0=EASY / 1=NORMAL / 2=LUNATIC)。</summary>
    public const int DifficultyCount = 3;

    [Serializable]
    private class EntryJson
    {
        public string k;
        public bool[] p = new bool[DifficultyCount];
        public bool[] c = new bool[DifficultyCount];
    }

    [Serializable]
    private class ModelJson
    {
        public int v = 1;
        public List<EntryJson> e = new List<EntryJson>();
    }

    private static ModelJson cache;

    private static void Load()
    {
        if (cache != null) return;
        cache = new ModelJson();
        string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            ModelJson loaded = JsonUtility.FromJson<ModelJson>(json);
            if (loaded == null || loaded.e == null) return;
            foreach (EntryJson e in loaded.e)
            {
                if (e == null || string.IsNullOrWhiteSpace(e.k)) continue;
                if (e.p == null || e.p.Length != DifficultyCount) e.p = new bool[DifficultyCount];
                if (e.c == null || e.c.Length != DifficultyCount) e.c = new bool[DifficultyCount];
                cache.e.Add(e);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[StageDifficultyProgress] 保存データを読めませんでした: " + ex.Message);
            cache = new ModelJson();
        }
    }

    private static EntryJson Find(string stageDirectoryName)
    {
        Load();
        foreach (EntryJson e in cache.e)
        {
            if (e.k == stageDirectoryName) return e;
        }
        return null;
    }

    private static EntryJson FindOrCreate(string stageDirectoryName)
    {
        EntryJson e = Find(stageDirectoryName);
        if (e != null) return e;
        e = new EntryJson { k = stageDirectoryName };
        cache.e.Add(e);
        return e;
    }

    private static void Save()
    {
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(cache));
        PlayerPrefs.Save();
    }

    private static bool Valid(string dir, int difficulty)
    {
        return !string.IsNullOrWhiteSpace(dir) && difficulty >= 0 && difficulty < DifficultyCount;
    }

    /// <summary>その難易度に挑戦したことを記録する(ステージ開始時)。</summary>
    public static void RecordPlay(string stageDirectoryName, int difficulty)
    {
        if (!Valid(stageDirectoryName, difficulty)) return;
        EntryJson e = FindOrCreate(stageDirectoryName);
        if (e.p[difficulty]) return;
        e.p[difficulty] = true;
        Save();
    }

    /// <summary>その難易度をクリアしたことを記録する(リザルトの記録時)。</summary>
    public static void RecordClear(string stageDirectoryName, int difficulty)
    {
        if (!Valid(stageDirectoryName, difficulty)) return;
        EntryJson e = FindOrCreate(stageDirectoryName);
        if (e.p[difficulty] && e.c[difficulty]) return;
        e.p[difficulty] = true;
        e.c[difficulty] = true;
        Save();
    }

    /// <summary>その難易度に一度でも挑戦したか。</summary>
    public static bool HasPlayed(string stageDirectoryName, int difficulty)
    {
        if (!Valid(stageDirectoryName, difficulty)) return false;
        EntryJson e = Find(stageDirectoryName);
        if (e != null && e.p[difficulty]) return true;
        // この記録より前のクリア履歴(1P の実績)はプレイ済みでもある。
        return HasCleared(stageDirectoryName, difficulty);
    }

    /// <summary>その難易度をクリアしたか。記録が無ければ 1P の実績(引き継ぎ用)を見る。</summary>
    public static bool HasCleared(string stageDirectoryName, int difficulty)
    {
        if (!Valid(stageDirectoryName, difficulty)) return false;
        EntryJson e = Find(stageDirectoryName);
        if (e != null && e.c[difficulty]) return true;
        return TransferAchievements.IsCleared(stageDirectoryName, difficulty);
    }

    /// <summary>検証用。保存を消す。</summary>
    public static void ClearAll()
    {
        cache = new ModelJson();
        PlayerPrefs.DeleteKey(PrefsKey);
        PlayerPrefs.Save();
    }

    /// <summary>検証用。次のアクセスで PlayerPrefs から読み直す。</summary>
    public static void InvalidateCache()
    {
        cache = null;
    }
}

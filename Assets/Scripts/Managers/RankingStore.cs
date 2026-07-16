using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// アーケード式ランキング(Instructions/ranking-transfer/SPEC.md §2)。
// 追記専用のエントリログとして persistentDataPath/ranking.v1.json に保存し、
// Top10 はロード時にエントリ集合から算出するビューにする(§2.3: 複数筐体の
// 同期は「エントリ集合の和集合」で衝突概念なく成立させるため)。
public static class RankingStore
{
    private const string FileName = "ranking.v1.json";
    private const string ExportPrefix = "ranking-export-";
    private const string CabinetIdPrefsKey = "cabinetId";
    private const string CabinetNamePrefsKey = "cabinetDisplayName";
    public const int TopCount = 10;
    public const int NameLength = 3;

    // アーケード式イニシャル文字集合(順序固定。上下スクロールはこの並びを送る)。SPEC §2.1。
    public const string NameCharset = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.♥";

    [Serializable]
    public class Entry
    {
        public string entryId;
        public string cabinetId;
        public string name;
        public int score;
        public string stage;
        public int difficulty;
        public string mode; // "1P" or "2P"
        public string dateTime; // "yyyy-MM-dd HH:mm:ss"
    }

    [Serializable]
    private class FileModel
    {
        public int v = 1;
        public List<Entry> entries = new List<Entry>();
    }

    private static List<Entry> cache;

    private static string FilePath =>
        Path.Combine(Application.persistentDataPath, FileName);

    // ---- 筐体ID -------------------------------------------------------------

    public static string CabinetId
    {
        get
        {
            string id = PlayerPrefs.GetString(CabinetIdPrefsKey, string.Empty);
            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString("N").Substring(0, 8);
                PlayerPrefs.SetString(CabinetIdPrefsKey, id);
                PlayerPrefs.Save();
            }
            return id;
        }
    }

    // 表示名(例:「1号機」)。未設定ならCabinetIdをそのまま使う。設定UIは今回スコープ外。
    public static string CabinetDisplayName
    {
        get
        {
            string name = PlayerPrefs.GetString(CabinetNamePrefsKey, string.Empty);
            return string.IsNullOrEmpty(name) ? CabinetId : name;
        }
        set
        {
            PlayerPrefs.SetString(CabinetNamePrefsKey, value ?? string.Empty);
            PlayerPrefs.Save();
        }
    }

    // ---- ロード/保存 ----------------------------------------------------------

    private static void Load()
    {
        if (cache != null) return;
        cache = new List<Entry>();
        if (!File.Exists(FilePath)) return;

        try
        {
            string json = File.ReadAllText(FilePath);
            FileModel model = JsonUtility.FromJson<FileModel>(json);
            if (model?.entries != null) cache = model.entries;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[RankingStore] Failed to read {FilePath}: {ex.Message}");
            cache = new List<Entry>();
        }
    }

    private static void Save()
    {
        try
        {
            FileModel model = new FileModel { entries = cache };
            File.WriteAllText(FilePath, JsonUtility.ToJson(model, true));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[RankingStore] Failed to write {FilePath}: {ex.Message}");
        }
    }

    public static List<Entry> LoadAll()
    {
        Load();
        return new List<Entry>(cache);
    }

    // 新規エントリを1件追記する。entryId は自動採番(GUID)。
    public static Entry AddEntry(string name, int score, string stage, int difficulty, string mode, DateTime timestamp)
    {
        Load();
        Entry entry = new Entry
        {
            entryId = Guid.NewGuid().ToString(),
            cabinetId = CabinetId,
            name = string.IsNullOrEmpty(name) ? "???" : name,
            score = score,
            stage = stage,
            difficulty = difficulty,
            mode = mode,
            dateTime = timestamp.ToString("yyyy-MM-dd HH:mm:ss")
        };
        cache.Add(entry);
        Save();
        return entry;
    }

    // 指定条件(ステージ/難易度/モード)の上位N件(スコア降順、同点は先着=dateTime昇順)。
    public static List<Entry> GetTop(string stage, int difficulty, string mode, int count = TopCount)
    {
        Load();
        return cache
            .Where(e => e != null && e.stage == stage && e.difficulty == difficulty && e.mode == mode)
            .OrderByDescending(e => e.score)
            .ThenBy(e => e.dateTime, StringComparer.Ordinal)
            .Take(count)
            .ToList();
    }

    // score が現在の Top10 に入るか(スコアのみでの判定。同点は「入れる」側に寄せる)。
    public static bool QualifiesForTop(string stage, int difficulty, string mode, int score, int count = TopCount)
    {
        List<Entry> top = GetTop(stage, difficulty, mode, count);
        if (top.Count < count) return true;
        return score >= top[top.Count - 1].score;
    }

    public static void ClearAll()
    {
        cache = new List<Entry>();
        try
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[RankingStore] Failed to delete {FilePath}: {ex.Message}");
        }
    }

    // ---- エクスポート/インポート(§2.3: USBメモリ経由の手動同期) ----------------

    // 現在の全エントリを1ファイルへ書き出す(差分でなく全件。和集合マージなので
    // 何度読み込んでも冪等)。戻り値は書き出したファイルパス(失敗時は null)。
    public static string ExportToFile(string destinationFolder)
    {
        Load();
        try
        {
            Directory.CreateDirectory(destinationFolder);
            string fileName = $"{ExportPrefix}{CabinetId}-{DateTime.Now:yyyyMMdd-HHmmss}.json";
            string path = Path.Combine(destinationFolder, fileName);
            FileModel model = new FileModel { entries = cache };
            File.WriteAllText(path, JsonUtility.ToJson(model, true));
            return path;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[RankingStore] Export failed: {ex.Message}");
            return null;
        }
    }

    public static string ExportToDesktop()
    {
        return ExportToFile(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
    }

    // 指定ファイルを取り込み、entryId で和集合マージする(既存分はスキップ)。
    // 戻り値は新規追加件数(失敗時は0)。
    public static int ImportFromFile(string path)
    {
        Load();
        try
        {
            if (!File.Exists(path)) return 0;
            string json = File.ReadAllText(path);
            FileModel model = JsonUtility.FromJson<FileModel>(json);
            if (model?.entries == null) return 0;

            HashSet<string> known = new HashSet<string>(cache.Select(e => e.entryId));
            int added = 0;
            foreach (Entry e in model.entries)
            {
                if (e == null || string.IsNullOrEmpty(e.entryId) || known.Contains(e.entryId)) continue;
                cache.Add(e);
                known.Add(e.entryId);
                added++;
            }
            if (added > 0) Save();
            return added;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[RankingStore] Import failed ({path}): {ex.Message}");
            return 0;
        }
    }

    // フォルダ内の "ranking-export-*.json" を全て走査して取り込む(決め打ちフォルダ走査)。
    // 戻り値は新規追加件数の合計。
    public static int ImportFromFolder(string folder)
    {
        int total = 0;
        try
        {
            if (!Directory.Exists(folder)) return 0;
            foreach (string file in Directory.GetFiles(folder, $"{ExportPrefix}*.json"))
            {
                total += ImportFromFile(file);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[RankingStore] Folder scan failed ({folder}): {ex.Message}");
        }
        return total;
    }

    public static int ImportFromDesktop()
    {
        return ImportFromFolder(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
    }
}

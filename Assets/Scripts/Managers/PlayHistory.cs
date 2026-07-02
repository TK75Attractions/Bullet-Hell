using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Server-less play history for the festival exhibit. The whole save is the
// "transfer code": a short Crockford-Base32 string a repeat visitor can write
// down and re-enter to carry their play/clear counts across sessions.
//
// Storage: PlayerPrefs key "playHistory.v1" holding compact JSON
//   { "v":1, "stages":[ {"k":"stone","p":3,"c":1}, ... ] }
// per-stage p = play count, c = clear count, both clamped 0..15 (4 bits each).
//
// Code bit layout (MSB-first):
//   version   : 4 bits (=1)
//   slot0..7  : 8 slots x (p:4bit + c:4bit) = 64 bits   (slot order = stage order)
//   crc8      : 8 bits  (poly 0x07, init 0x00, over the preceding 68 bits)
//   ---------------------------------------------------------------
//   total 76 bits -> zero padded to 80 -> 16 Base32 symbols
//   displayed grouped as XXXX-XXXX-XXXX-XXXX
public static class PlayHistory
{
    private const string PrefsKey = "playHistory.v1";
    private const int MaxSlots = 8;
    private const int Version = 1;

    // Crockford Base32 alphabet (excludes I, L, O, U for legibility).
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    // Fixed transfer-code slot assignment keyed by stageDirectoryName. Pinning the
    // slots (instead of following the live stage-database order) keeps previously
    // issued codes valid when the difficulty feature or new stages shift that order.
    // These indices match the historical stage-database order, so existing codes are
    // unchanged. Unknown stages are assigned to the remaining empty slots in
    // discovery order (slot 7 first), never displacing a fixed stage.
    private static readonly Dictionary<string, int> FixedSlotMap = new Dictionary<string, int>
    {
        { "25", 0 },
        { "captain", 1 },
        { "debug", 2 },
        { "debug(nature)", 3 },
        { "stone", 4 },
        { "mirror", 5 },
        { "pattern_demo", 6 }
    };

    // dir -> {play, clear}
    private static Dictionary<string, int[]> cache;

    [Serializable]
    private class SlotJson
    {
        public string k;
        public int p;
        public int c;
    }

    [Serializable]
    private class HistoryJson
    {
        public int v = Version;
        public List<SlotJson> stages = new List<SlotJson>();
    }

    // ---- Aggregation --------------------------------------------------------

    public static bool HasHistory
    {
        get
        {
            Load();
            foreach (int[] v in cache.Values)
            {
                if (v[0] > 0 || v[1] > 0) return true;
            }
            return false;
        }
    }

    public static int TotalPlays
    {
        get
        {
            Load();
            int total = 0;
            foreach (int[] v in cache.Values) total += v[0];
            return total;
        }
    }

    public static int TotalClears
    {
        get
        {
            Load();
            int total = 0;
            foreach (int[] v in cache.Values) total += v[1];
            return total;
        }
    }

    // ---- Recording ----------------------------------------------------------

    public static void RecordPlay(string stageDirectoryName)
    {
        if (string.IsNullOrWhiteSpace(stageDirectoryName)) return;
        Load();
        int[] v = GetOrCreate(stageDirectoryName);
        v[0] = Mathf.Min(15, v[0] + 1);
        Save();
    }

    public static void RecordClear(string stageDirectoryName)
    {
        if (string.IsNullOrWhiteSpace(stageDirectoryName)) return;
        Load();
        int[] v = GetOrCreate(stageDirectoryName);
        v[1] = Mathf.Min(15, v[1] + 1);
        Save();
    }

    public static void ClearAll()
    {
        cache = new Dictionary<string, int[]>();
        PlayerPrefs.DeleteKey(PrefsKey);
        PlayerPrefs.Save();
    }

    // ---- Transfer code ------------------------------------------------------

    // Always returns a valid 16-symbol code (even for empty history). Callers
    // that want to hide an empty code should test HasHistory first.
    public static string ExportCode()
    {
        Load();
        List<string> order = GetStageOrder();

        List<int> bits = new List<int>(80);
        WriteBits(bits, Version, 4);
        for (int i = 0; i < MaxSlots; i++)
        {
            int p = 0, c = 0;
            if (i < order.Count && cache.TryGetValue(order[i], out int[] v))
            {
                p = Mathf.Clamp(v[0], 0, 15);
                c = Mathf.Clamp(v[1], 0, 15);
            }
            WriteBits(bits, p, 4);
            WriteBits(bits, c, 4);
        }

        int crc = Crc8(bits); // over the 68 payload bits
        WriteBits(bits, crc, 8);

        while (bits.Count % 5 != 0) bits.Add(0); // 76 -> 80

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < bits.Count; i += 5)
        {
            int value = 0;
            for (int b = 0; b < 5; b++) value = (value << 1) | bits[i + b];
            sb.Append(Alphabet[value]);
        }

        return FormatCode(sb.ToString());
    }

    public static bool TryImportCode(string input, out string error)
    {
        error = null;
        string normalized = Normalize(input);
        if (normalized.Length != 16)
        {
            error = "コードが正しくありません";
            return false;
        }

        List<int> bits = new List<int>(80);
        for (int i = 0; i < normalized.Length; i++)
        {
            int value = Alphabet.IndexOf(normalized[i]);
            if (value < 0)
            {
                error = "コードが正しくありません";
                return false;
            }
            for (int b = 4; b >= 0; b--) bits.Add((value >> b) & 1);
        }

        int version = ReadBits(bits, 0, 4);
        if (version != Version)
        {
            error = "コードが正しくありません";
            return false;
        }

        int storedCrc = ReadBits(bits, 68, 8);
        int calcCrc = Crc8(bits.GetRange(0, 68));
        if (storedCrc != calcCrc)
        {
            error = "コードが正しくありません";
            return false;
        }

        List<string> order = GetStageOrder();
        Dictionary<string, int[]> imported = new Dictionary<string, int[]>();
        for (int i = 0; i < MaxSlots; i++)
        {
            int p = ReadBits(bits, 4 + i * 8, 4);
            int c = ReadBits(bits, 4 + i * 8 + 4, 4);
            if ((p > 0 || c > 0) && i < order.Count)
            {
                imported[order[i]] = new int[] { p, c };
            }
        }

        cache = imported;
        Save();
        return true;
    }

    // ---- Internals ----------------------------------------------------------

    private static int[] GetOrCreate(string key)
    {
        if (!cache.TryGetValue(key, out int[] v))
        {
            v = new int[] { 0, 0 };
            cache[key] = v;
        }
        return v;
    }

    private static void Load()
    {
        if (cache != null) return;
        cache = new Dictionary<string, int[]>();

        string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            HistoryJson model = JsonUtility.FromJson<HistoryJson>(json);
            if (model?.stages == null) return;
            foreach (SlotJson slot in model.stages)
            {
                if (slot == null || string.IsNullOrEmpty(slot.k)) continue;
                cache[slot.k] = new int[] { Mathf.Clamp(slot.p, 0, 15), Mathf.Clamp(slot.c, 0, 15) };
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[PlayHistory] Failed to parse stored history: {ex.Message}");
            cache = new Dictionary<string, int[]>();
        }
    }

    private static void Save()
    {
        HistoryJson model = new HistoryJson();
        foreach (KeyValuePair<string, int[]> kv in cache)
        {
            if (kv.Value[0] == 0 && kv.Value[1] == 0) continue;
            model.stages.Add(new SlotJson { k = kv.Key, p = kv.Value[0], c = kv.Value[1] });
        }
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(model));
        PlayerPrefs.Save();
    }

    // Returns the slot->stageDirectoryName assignment (length MaxSlots). Fixed
    // stages sit at their pinned index; unknown stages fill remaining empty slots in
    // discovery order. Empty slots are "" (they never match a cached stage key, so
    // they contribute zero play/clear counts to the code).
    private static List<string> GetStageOrder()
    {
        string[] slots = new string[MaxSlots];
        foreach (KeyValuePair<string, int> kv in FixedSlotMap)
        {
            if (kv.Value >= 0 && kv.Value < MaxSlots) slots[kv.Value] = kv.Key;
        }

        List<StageData> stages = GManager.Control?.SDB?.GetAllStages();
        if (stages != null)
        {
            foreach (StageData stage in stages)
            {
                if (stage == null) continue;
                string dir = string.IsNullOrWhiteSpace(stage.stageDirectoryName)
                    ? stage.stageName
                    : stage.stageDirectoryName;
                if (string.IsNullOrWhiteSpace(dir) || FixedSlotMap.ContainsKey(dir)) continue;

                int slot = FirstEmptySlot(slots);
                if (slot < 0) break; // all slots taken
                slots[slot] = dir;
            }
        }

        List<string> order = new List<string>(MaxSlots);
        for (int i = 0; i < MaxSlots; i++) order.Add(slots[i] ?? string.Empty);
        return order;
    }

    private static int FirstEmptySlot(string[] slots)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (string.IsNullOrEmpty(slots[i])) return i;
        }
        return -1;
    }

    private static string Normalize(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        StringBuilder sb = new StringBuilder(input.Length);
        foreach (char raw in input)
        {
            char ch = char.ToUpperInvariant(raw);
            switch (ch)
            {
                case 'I': ch = '1'; break;
                case 'L': ch = '1'; break;
                case 'O': ch = '0'; break;
                default: break;
            }
            if ((ch >= '0' && ch <= '9') || (ch >= 'A' && ch <= 'Z'))
            {
                sb.Append(ch);
            }
            // Everything else (hyphen, space, etc.) is dropped.
        }
        return sb.ToString();
    }

    private static string FormatCode(string raw)
    {
        StringBuilder sb = new StringBuilder(raw.Length + 3);
        for (int i = 0; i < raw.Length; i++)
        {
            if (i > 0 && i % 4 == 0) sb.Append('-');
            sb.Append(raw[i]);
        }
        return sb.ToString();
    }

    private static void WriteBits(List<int> bits, int value, int count)
    {
        for (int i = count - 1; i >= 0; i--) bits.Add((value >> i) & 1);
    }

    private static int ReadBits(List<int> bits, int start, int count)
    {
        int value = 0;
        for (int i = 0; i < count; i++) value = (value << 1) | bits[start + i];
        return value;
    }

    // MSB-first bitwise CRC-8 (poly 0x07, init 0x00) matching the byte-wise
    // table variant when the input length is a multiple of 8.
    private static int Crc8(List<int> bits)
    {
        int crc = 0;
        foreach (int b in bits)
        {
            int top = ((crc >> 7) & 1) ^ (b & 1);
            crc = (crc << 1) & 0xFF;
            if (top == 1) crc ^= 0x07;
        }
        return crc;
    }
}

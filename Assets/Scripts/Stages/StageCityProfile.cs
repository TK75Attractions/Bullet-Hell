using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ステージ(stageDirectoryName)と「城壁の街」の区画番号(1..9)の対応表。
///
/// 区画は Instructions/ステージ選択/cg/v2_notes.md の 9 区画:
/// 1 市場 / 2 地下 / 3 石切り場 / 4 廃屋 / 5 大河 / 6 地下墓地 / 7 宝物館 / 8 聖堂前 / 9 大聖堂。
///
/// 資産 <c>Assets/Resources/StageCityProfile.asset</c> があればそれを使い、無ければ
/// <see cref="Defaults"/> の内蔵表で動く。後からステージを増やすときは資産に 1 行足すだけでよい
/// (資産が無い環境でも既存 3 ステージは動く)。
/// </summary>
[CreateAssetMenu(fileName = "StageCityProfile", menuName = "Bullet Hell/Stage City Profile")]
public class StageCityProfile : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        [Tooltip("StageData.stageDirectoryName")]
        public string stageDirectoryName;
        [Tooltip("城壁の街の区画番号(1..9)。0 で未割当=選択リストに出さない。")]
        [Range(0, 9)] public int district;
        [Tooltip("街モードで▼のラベルに出す日本語名。空なら StageData.stageName を使う。")]
        public string displayName;
    }

    public List<Entry> entries = new List<Entry>();

    private const string ResourcePath = "StageCityProfile";

    // 資産が無いときに使う内蔵表(2026-09-09 時点の割当)。
    private static readonly (string dir, int district, string displayName)[] Defaults =
    {
        ("stone", 3, "石工"),      // 石切り場(東)
        ("captain", 5, "艦長"),    // 大河(西)。StageData 側は "Captain" のままなのでここで補う
        ("vagrant", 6, "浮浪者"),  // 地下墓地(北西)
    };

    // 区画ごとの「色の基調」。1 を中立とする倍率で、寄っているあいだだけ画面全体へ薄く被せる
    // (石切り場=黄土 / 大河=青灰 / 地下墓地=青緑 / 未実装=紫灰)。
    private static readonly Color[] DistrictTints =
    {
        Color.white,
        new Color(1.00f, 0.95f, 1.08f, 1f),   // 01 市場(未実装・紫灰)
        new Color(1.00f, 0.95f, 1.08f, 1f),   // 02 地下(未実装)
        new Color(1.14f, 1.03f, 0.82f, 1f),   // 03 石切り場(黄土)
        new Color(1.00f, 0.95f, 1.08f, 1f),   // 04 廃屋(未実装)
        new Color(0.90f, 0.97f, 1.12f, 1f),   // 05 大河(青灰)
        new Color(0.86f, 1.06f, 1.02f, 1f),   // 06 地下墓地(青緑)
        new Color(1.00f, 0.95f, 1.08f, 1f),   // 07 宝物館(未実装)
        new Color(1.00f, 0.95f, 1.08f, 1f),   // 08 聖堂前(未実装)
        new Color(1.00f, 0.95f, 1.08f, 1f),   // 09 大聖堂(未実装)
    };

    /// <summary>区画の基調色(1 を中立とする倍率)。範囲外は白。</summary>
    public static Color TintOf(int district)
    {
        return district >= 1 && district < DistrictTints.Length ? DistrictTints[district] : Color.white;
    }

    /// <summary>街モードで出す日本語の表示名。資産 → 内蔵表 → StageData.stageName の順で拾う。</summary>
    public static string DisplayNameOf(StageData data)
    {
        if (data == null) return "";
        string dir = data.stageDirectoryName;
        StageCityProfile profile = Load();
        if (profile != null && profile.entries != null && !string.IsNullOrEmpty(dir))
        {
            foreach (Entry e in profile.entries)
            {
                if (e != null && e.stageDirectoryName == dir && !string.IsNullOrWhiteSpace(e.displayName))
                    return e.displayName;
            }
        }
        if (!string.IsNullOrEmpty(dir))
        {
            foreach ((string d, int _, string name) in Defaults)
            {
                if (d == dir && !string.IsNullOrWhiteSpace(name)) return name;
            }
        }
        return data.stageName != null ? data.stageName : "";
    }

    // 仮のままのステージ説明(英語のプレースホルダ)は街モードでは空欄にする。
    private static readonly string[] PlaceholderDescriptions =
    {
        "Stage Description Here",
    };

    /// <summary>仮文なら空文字を返す説明。</summary>
    public static string DescriptionOf(StageData data)
    {
        string desc = data != null ? data.stageDescription : null;
        if (string.IsNullOrWhiteSpace(desc)) return "";
        foreach (string ph in PlaceholderDescriptions)
        {
            if (string.Equals(desc.Trim(), ph, System.StringComparison.OrdinalIgnoreCase)) return "";
        }
        return desc;
    }

    private static StageCityProfile loaded;
    private static bool loadTried;

    public static StageCityProfile Load()
    {
        if (!loadTried)
        {
            loadTried = true;
            loaded = Resources.Load<StageCityProfile>(ResourcePath);
        }
        return loaded;
    }

    /// <summary>ステージの区画番号。割当が無ければ 0。</summary>
    public static int DistrictOf(StageData data)
    {
        return data != null ? DistrictOf(data.stageDirectoryName) : 0;
    }

    public static int DistrictOf(string stageDirectoryName)
    {
        if (string.IsNullOrEmpty(stageDirectoryName)) return 0;
        StageCityProfile profile = Load();
        if (profile != null && profile.entries != null)
        {
            foreach (Entry e in profile.entries)
            {
                if (e != null && e.stageDirectoryName == stageDirectoryName)
                    return Mathf.Clamp(e.district, 0, CityMapController.DistrictCount);
            }
        }
        foreach ((string dir, int district, string _) in Defaults)
        {
            if (dir == stageDirectoryName) return district;
        }
        return 0;
    }

    /// <summary>区画 1..9 のうち、ステージが割り当たっているものを true にした長さ 10 の配列。</summary>
    public static bool[] BuildAvailability(StageDataBase sdb)
    {
        bool[] flags = new bool[CityMapController.DistrictCount + 1];
        if (sdb == null) return flags;
        int count = sdb.GetStageCount();
        for (int i = 0; i < count; i++)
        {
            int d = DistrictOf(sdb.GetStage(i));
            if (d >= 1 && d < flags.Length) flags[d] = true;
        }
        return flags;
    }
}

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
    }

    public List<Entry> entries = new List<Entry>();

    private const string ResourcePath = "StageCityProfile";

    // 資産が無いときに使う内蔵表(2026-09-09 時点の割当)。
    private static readonly (string dir, int district)[] Defaults =
    {
        ("stone", 3),      // 石切り場(東)
        ("captain", 5),    // 大河(西)
        ("vagrant", 6),    // 地下墓地(北西)
    };

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
        foreach ((string dir, int district) in Defaults)
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

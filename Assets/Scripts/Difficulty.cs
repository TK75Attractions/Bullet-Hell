using System;
using System.Collections.Generic;

public enum Difficulty
{
    Easy = 0,
    Normal = 1,
    Lunatic = 2
}

[Serializable]
public struct DifficultySelection
{
    public string id;
    public string displayName;
    public bool isOfficial;
    public Difficulty officialDifficulty;

    public DifficultySelection(string id, string displayName, bool isOfficial, Difficulty officialDifficulty)
    {
        this.id = DifficultyUtility.NormalizeId(id);
        this.displayName = string.IsNullOrWhiteSpace(displayName)
            ? DifficultyUtility.GetDisplayName(this.id)
            : displayName.Trim();
        this.isOfficial = isOfficial;
        this.officialDifficulty = officialDifficulty;
    }

    public static DifficultySelection FromOfficial(Difficulty difficulty)
    {
        string id = DifficultyUtility.GetId(difficulty);
        return new DifficultySelection(id, DifficultyUtility.GetDisplayName(difficulty), true, difficulty);
    }

    public static DifficultySelection FromCustom(string id, string displayName = "")
    {
        if (DifficultyUtility.TryParseOfficial(id, out Difficulty officialDifficulty))
        {
            return FromOfficial(officialDifficulty);
        }

        return new DifficultySelection(id, displayName, false, Difficulty.Normal);
    }

    public static DifficultySelection FromId(string id, string displayName = "")
    {
        return DifficultyUtility.TryParseOfficial(id, out Difficulty officialDifficulty)
            ? FromOfficial(officialDifficulty)
            : FromCustom(id, displayName);
    }

    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(id);
    }

    public override string ToString()
    {
        return string.IsNullOrWhiteSpace(displayName) ? id : displayName;
    }
}

public static class DifficultyUtility
{
    public static readonly Difficulty[] OfficialDifficulties =
    {
        Difficulty.Easy,
        Difficulty.Normal,
        Difficulty.Lunatic
    };

    public static List<DifficultySelection> GetOfficialSelections()
    {
        List<DifficultySelection> selections = new List<DifficultySelection>();
        for (int i = 0; i < OfficialDifficulties.Length; i++)
        {
            selections.Add(DifficultySelection.FromOfficial(OfficialDifficulties[i]));
        }

        return selections;
    }

    public static string GetId(Difficulty difficulty)
    {
        return difficulty.ToString();
    }

    public static string NormalizeId(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        string trimmed = value.Trim();
        return TryParseOfficial(trimmed, out Difficulty officialDifficulty)
            ? GetId(officialDifficulty)
            : trimmed;
    }

    public static string GetDisplayName(Difficulty difficulty)
    {
        return difficulty.ToString().ToUpperInvariant();
    }

    public static string GetDisplayName(string difficultyId)
    {
        if (TryParseOfficial(difficultyId, out Difficulty officialDifficulty))
        {
            return GetDisplayName(officialDifficulty);
        }

        return string.IsNullOrWhiteSpace(difficultyId) ? string.Empty : difficultyId.Trim();
    }

    public static bool TryParseOfficial(string value, out Difficulty difficulty)
    {
        difficulty = Difficulty.Normal;
        if (string.IsNullOrWhiteSpace(value)) return false;

        string trimmed = value.Trim();
        if (Enum.TryParse(trimmed, true, out Difficulty parsedDifficulty)
            && Enum.IsDefined(typeof(Difficulty), parsedDifficulty))
        {
            difficulty = parsedDifficulty;
            return true;
        }

        if (int.TryParse(trimmed, out int difficultyValue)
            && Enum.IsDefined(typeof(Difficulty), difficultyValue))
        {
            difficulty = (Difficulty)difficultyValue;
            return true;
        }

        return false;
    }
}

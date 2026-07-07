using System;
using UnityEngine;

[Serializable]
public sealed class GameScoreBreakdown
{
    public int counterScore;
    public int noHitScore;
    public int bossDefeatScore;
    public int totalScore;
}

[Serializable]
public sealed class GameResultData
{
    public int stageIndex;
    public string stageName;
    public string difficultyId;
    public string difficultyDisplayName;
    public float endTime;
    public float elapsedTime;
    public bool isClear;
    public bool bossDefeated;
    public float bossCurrentHp;
    public float bossMaxHp;
    public int counterCount;
    public int playerHitCount;
    public float longestNoHitDuration;
    public GameScoreBreakdown score;
    public string recordedAtUtc;
}

public static class GameScoreCalculator
{
    public const int PointsPerCounter = 100;
    public const int PointsPerNoHitSecond = 10;
    public const int BossDefeatBonus = 10000;

    public static GameScoreBreakdown Calculate(int counterCount, float longestNoHitDuration, bool bossDefeated)
    {
        int safeCounterCount = Mathf.Max(0, counterCount);
        float safeNoHitDuration = Mathf.Max(0f, longestNoHitDuration);

        GameScoreBreakdown result = new GameScoreBreakdown
        {
            counterScore = safeCounterCount * PointsPerCounter,
            noHitScore = Mathf.FloorToInt(safeNoHitDuration * PointsPerNoHitSecond),
            bossDefeatScore = bossDefeated ? BossDefeatBonus : 0
        };
        result.totalScore = result.counterScore + result.noHitScore + result.bossDefeatScore;
        return result;
    }

    public static GameResultData Create(
        int stageIndex,
        StageData stageData,
        DifficultySelection difficulty,
        float elapsedTime,
        int counterCount,
        int playerHitCount,
        float longestNoHitDuration,
        Boss boss)
    {
        bool bossDefeated = boss != null && boss.IsDefeated;
        return new GameResultData
        {
            stageIndex = stageIndex,
            stageName = stageData != null ? stageData.stageName : string.Empty,
            difficultyId = difficulty.id,
            difficultyDisplayName = difficulty.displayName,
            endTime = stageData != null ? stageData.endTime : elapsedTime,
            elapsedTime = elapsedTime,
            isClear = bossDefeated,
            bossDefeated = bossDefeated,
            bossCurrentHp = boss != null ? boss.CurrentHp : 0f,
            bossMaxHp = boss != null ? boss.MaxHp : 0f,
            counterCount = Mathf.Max(0, counterCount),
            playerHitCount = Mathf.Max(0, playerHitCount),
            longestNoHitDuration = Mathf.Max(0f, longestNoHitDuration),
            score = Calculate(counterCount, longestNoHitDuration, bossDefeated),
            recordedAtUtc = DateTime.UtcNow.ToString("O")
        };
    }
}

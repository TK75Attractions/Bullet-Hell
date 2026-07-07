using UnityEngine;
using TMPro;

public class ResultUIManager : MonoBehaviour
{
    [SerializeField] private GameObject resultRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text detailText;
    [SerializeField] private bool hideRootOnAwake = false;

    public GameResultData CurrentResult { get; private set; }

    private void Awake()
    {
        Init();
    }

    public void Init()
    {
        ResolveTextReferences();

        if (hideRootOnAwake && resultRoot != null)
        {
            resultRoot.SetActive(false);
        }
    }

    public void ShowResult(GameResultData result)
    {
        if (result == null) return;

        ResolveTextReferences();
        CurrentResult = result;

        if (resultRoot != null)
        {
            resultRoot.SetActive(true);
        }

        string title = result.isClear ? "CLEAR" : "RESULT";
        string score = result.score != null ? result.score.totalScore.ToString() : "0";
        string details = FormatDetails(result);
        bool singleTextMode = detailText == null && (titleText == null || scoreText == null);

        if (singleTextMode)
        {
            TMP_Text targetText = titleText != null ? titleText : scoreText;
            if (targetText != null)
            {
                targetText.text = $"{title}\nScore: {score}\n{details}";
            }

            return;
        }

        if (titleText != null)
        {
            titleText.text = title;
        }

        if (scoreText != null)
        {
            scoreText.text = score;
        }

        if (detailText != null)
        {
            detailText.text = details;
        }
    }

    public void Hide()
    {
        if (resultRoot != null)
        {
            resultRoot.SetActive(false);
        }
    }

    private string FormatDetails(GameResultData result)
    {
        GameScoreBreakdown score = result.score ?? new GameScoreBreakdown();

        return
            $"Stage: {result.stageName}\n" +
            $"Difficulty: {result.difficultyDisplayName}\n" +
            $"Time: {result.elapsedTime:F1} / {result.endTime:F1}\n" +
            $"Clear: {(result.isClear ? "Yes" : "No")}\n" +
            $"Counter: {result.counterCount} (+{score.counterScore})\n" +
            $"No Hit: {result.longestNoHitDuration:F1}s (+{score.noHitScore})\n" +
            $"Boss Defeat: {(result.bossDefeated ? "Yes" : "No")} (+{score.bossDefeatScore})\n" +
            $"Hit: {result.playerHitCount}\n" +
            $"Boss HP: {result.bossCurrentHp:F0} / {result.bossMaxHp:F0}";
    }

    private void ResolveTextReferences()
    {
        if (titleText == null)
        {
            titleText = FindChildText("Title", "ResultTitle", "Header");
        }

        if (scoreText == null)
        {
            scoreText = FindChildText("Score", "ScoreText", "TotalScore");
        }

        if (detailText == null)
        {
            detailText = FindChildText("Detail", "Details", "ResultDetail", "ResultDetails");
        }

        if (titleText == null && scoreText == null && detailText == null)
        {
            titleText = GetComponentInChildren<TMP_Text>(true);
        }
    }

    private TMP_Text FindChildText(params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            Transform child = transform.Find(names[i]);
            if (child != null && child.TryGetComponent(out TMP_Text text))
            {
                return text;
            }
        }

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null) continue;

            for (int k = 0; k < names.Length; k++)
            {
                if (string.Equals(text.name, names[k], System.StringComparison.OrdinalIgnoreCase))
                {
                    return text;
                }
            }
        }

        return null;
    }
}

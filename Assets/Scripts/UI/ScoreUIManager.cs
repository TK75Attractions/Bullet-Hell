using UnityEngine;
using TMPro;

public class ScoreUIManager : MonoBehaviour
{
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text detailText;
    [SerializeField] private bool clearWhenNotPlaying = true;

    private int displayedScore = int.MinValue;
    private int displayedCounterCount = int.MinValue;
    private int displayedHitCount = int.MinValue;
    private float displayedNoHitDuration = -1f;
    private GManager.GameState displayedState;

    private void Awake()
    {
        Init();
    }

    public void Init()
    {
        ResolveTextReferences();
        Refresh(true);
    }

    private void Update()
    {
        Refresh(false);
    }

    private void Refresh(bool force)
    {
        GManager manager = GManager.Control;
        if (manager == null || !manager.ready)
        {
            ClearText(force);
            return;
        }

        if (manager.state != GManager.GameState.Playing)
        {
            if (clearWhenNotPlaying)
            {
                ClearText(force || displayedState != manager.state);
            }
            displayedState = manager.state;
            return;
        }

        GameScoreBreakdown score = manager.GetCurrentScoreBreakdown();
        int counterCount = manager.counterHitBossCount;
        int hitCount = manager.playerHitCount;
        float noHitDuration = manager.LongestNoHitDuration;

        bool changed = force
            || displayedState != manager.state
            || displayedScore != score.totalScore
            || displayedCounterCount != counterCount
            || displayedHitCount != hitCount
            || !Mathf.Approximately(displayedNoHitDuration, noHitDuration);

        if (!changed) return;

        displayedState = manager.state;
        displayedScore = score.totalScore;
        displayedCounterCount = counterCount;
        displayedHitCount = hitCount;
        displayedNoHitDuration = noHitDuration;

        if (scoreText != null)
        {
            scoreText.text = score.totalScore.ToString();
        }

        if (detailText != null)
        {
            detailText.text =
                $"Counter: {counterCount}\n" +
                $"No Hit: {noHitDuration:F1}s\n" +
                $"Hit: {hitCount}";
        }
    }

    private void ClearText(bool force)
    {
        if (!force && displayedScore == 0) return;

        displayedScore = 0;
        displayedCounterCount = 0;
        displayedHitCount = 0;
        displayedNoHitDuration = 0f;

        if (scoreText != null) scoreText.text = string.Empty;
        if (detailText != null) detailText.text = string.Empty;
    }

    private void ResolveTextReferences()
    {
        if (scoreText == null)
        {
            scoreText = FindChildText("Score", "ScoreText", "TotalScore");
        }

        if (detailText == null)
        {
            detailText = FindChildText("Detail", "Details", "ScoreDetail", "ScoreDetails");
        }

        if (scoreText == null)
        {
            scoreText = GetComponentInChildren<TMP_Text>(true);
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

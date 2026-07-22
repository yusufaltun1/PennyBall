using System.Collections;
using TMPro;
using UnityEngine;

public class MatchScoreboardPresenter : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI _playerScoreText;
    [SerializeField] TextMeshProUGUI _opponentScoreText;

    bool _handleMatchEnd;

    void Awake()
    {
        _handleMatchEnd = GetComponentInParent<ResultPanelController>(true) == null;
    }

    void Start()
    {
        StartCoroutine(BindWhenReady());
    }

    void OnDestroy()
    {
        if (LeagueMatchController.Instance != null)
        {
            LeagueMatchController.Instance.ScoresChanged -= RefreshDisplay;
            LeagueMatchController.Instance.MatchTimerExpired -= OnMatchTimerExpired;
        }
    }

    IEnumerator BindWhenReady()
    {
        while (LeagueMatchController.Instance == null)
            yield return null;

        LeagueMatchController.Instance.ScoresChanged -= RefreshDisplay;
        LeagueMatchController.Instance.ScoresChanged += RefreshDisplay;

        if (_handleMatchEnd)
        {
            LeagueMatchController.Instance.MatchTimerExpired -= OnMatchTimerExpired;
            LeagueMatchController.Instance.MatchTimerExpired += OnMatchTimerExpired;
        }

        RefreshDisplay();
    }

    public void RefreshDisplay()
    {
        GetScores(out int playerScore, out int opponentScore);

        if (_playerScoreText != null)
            _playerScoreText.SetText(playerScore.ToString());
        if (_opponentScoreText != null)
            _opponentScoreText.SetText(opponentScore.ToString());
    }

    void GetScores(out int playerScore, out int opponentScore)
    {
        if (LeagueMatchController.Instance != null && LeagueMatchController.Instance.IsMatchActive)
        {
            playerScore = LeagueMatchController.Instance.PlayerGoals;
            opponentScore = LeagueMatchController.Instance.OpponentGoals;
            return;
        }

        if (!_handleMatchEnd)
        {
            playerScore = MatchSessionContext.PlayerGoalsAtEnd;
            opponentScore = MatchSessionContext.OpponentGoalsAtEnd;
            return;
        }

        playerScore = 0;
        opponentScore = 0;
    }

    public static void RefreshAll()
    {
        MatchScoreboardPresenter[] presenters =
            FindObjectsByType<MatchScoreboardPresenter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < presenters.Length; i++)
            presenters[i].RefreshDisplay();
    }

    void OnMatchTimerExpired()
    {
        CoinInputHandler[] inputs = FindObjectsByType<CoinInputHandler>(FindObjectsSortMode.None);
        for (int i = 0; i < inputs.Length; i++)
            inputs[i].enabled = false;

        OpponentBotController.Instance?.FreezeMatch();
        GameFeedback.EnsureInstance()?.PlayWhistle();

        bool isForfeit = MatchSessionTracker.HasPendingAbandon;

        MatchResultType result = MatchResultType.Draw;
        if (LeagueMatchController.Instance != null)
            result = LeagueMatchController.Instance.CompleteMatchFromTimer();

        RefreshAll();

        if (isForfeit)
        {
            // Hükmen mağlup paneli geçici kapalı — normal sonuç paneline düş.
            // HukmenMaglupController forfeitPanel = HukmenMaglupController.FindPanel();
            // if (forfeitPanel != null)
            // {
            //     forfeitPanel.Show();
            //     return;
            // }
            //
            // Debug.LogWarning("[Match] Hükmen mağlup paneli (HukmenMaglup) sahnede bulunamadı.");
        }

        ResultPanelController resultPanel = FindNormalResultPanel();
        resultPanel?.ShowResult(result);
    }

    static ResultPanelController FindNormalResultPanel()
    {
        ResultPanelController[] panels =
            FindObjectsByType<ResultPanelController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < panels.Length; i++)
        {
            if (panels[i] != null && panels[i].gameObject.name == "ResultPanel")
            {
                return panels[i];
            }
        }

        for (int i = 0; i < panels.Length; i++)
        {
            if (panels[i] != null && panels[i].gameObject.name != "HukmenMaglup")
            {
                return panels[i];
            }
        }

        return null;
    }
}

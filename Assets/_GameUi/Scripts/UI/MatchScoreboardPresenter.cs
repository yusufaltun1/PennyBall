using System.Collections;
using TMPro;
using UnityEngine;

public class MatchScoreboardPresenter : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI _playerScoreText;
    [SerializeField] TextMeshProUGUI _opponentScoreText;

    int _playerScore;
    int _opponentScore;

    void Start()
    {
        StartCoroutine(BindWhenReady());
    }

    void OnDestroy()
    {
        if (GameRulesManager.Instance != null)
            GameRulesManager.Instance.PlayerGoalScored -= OnPlayerGoal;

        if (OpponentBotController.Instance != null)
            OpponentBotController.Instance.OpponentGoalScored -= OnOpponentGoal;

        MatchTimerPresenter timer = FindAnyObjectByType<MatchTimerPresenter>();
        if (timer != null)
            timer.TimerExpired -= OnTimerExpired;
    }

    IEnumerator BindWhenReady()
    {
        while (GameRulesManager.Instance == null || OpponentBotController.Instance == null)
            yield return null;

        GameRulesManager.Instance.PlayerGoalScored += OnPlayerGoal;
        OpponentBotController.Instance.OpponentGoalScored += OnOpponentGoal;

        MatchTimerPresenter timer = FindAnyObjectByType<MatchTimerPresenter>();
        if (timer != null)
            timer.TimerExpired += OnTimerExpired;

        UpdateDisplay();
    }

    void OnPlayerGoal()
    {
        _playerScore++;
        UpdateDisplay();
    }

    void OnOpponentGoal()
    {
        _opponentScore++;
        UpdateDisplay();
    }

    void OnTimerExpired()
    {
        CoinInputHandler[] inputs = FindObjectsByType<CoinInputHandler>(FindObjectsSortMode.None);
        for (int i = 0; i < inputs.Length; i++)
            inputs[i].enabled = false;

        OpponentBotController.Instance?.FreezeMatch();

        MatchResultType result;
        if (_playerScore > _opponentScore)
            result = MatchResultType.Win;
        else if (_playerScore < _opponentScore)
            result = MatchResultType.Loss;
        else
            result = MatchResultType.Draw;

        LeagueService.Instance?.RegisterMatchResult(result);

        ResultPanelController resultPanel = FindAnyObjectByType<ResultPanelController>(FindObjectsInactive.Include);
        resultPanel?.ShowResult(result);
    }

    void UpdateDisplay()
    {
        if (_playerScoreText != null)
            _playerScoreText.SetText(_playerScore.ToString());
        if (_opponentScoreText != null)
            _opponentScoreText.SetText(_opponentScore.ToString());
    }
}

using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class MatchTimerPresenter : MonoBehaviour
{
    [SerializeField] float _totalSeconds = LeagueConfig.MatchDurationSeconds;

    public event Action TimerExpired;

    TextMeshProUGUI _label;
    float _remaining;
    bool _isPaused;

    void Awake()
    {
        _label = GetComponent<TextMeshProUGUI>();
        if (_label == null)
        {
            _label = GetComponentInChildren<TextMeshProUGUI>();
        }
    }

    void Start()
    {
        _remaining = _totalSeconds;
        UpdateDisplay(Mathf.CeilToInt(_remaining));
        StartCoroutine(StartCountdownWhenReady());
        StartCoroutine(BindWhenReady());
    }

    IEnumerator StartCountdownWhenReady()
    {
        while (MatchBeginningCountdownController.IsActive)
        {
            yield return null;
        }

        yield return CountdownRoutine();
    }

    void OnDestroy()
    {
        if (GameRulesManager.Instance != null)
        {
            GameRulesManager.Instance.PlayerGoalScored -= OnGoalScoredPause;
            GameRulesManager.Instance.RoundReset -= OnRoundResetResume;
        }

        if (OpponentBotController.Instance != null)
        {
            OpponentBotController.Instance.OpponentGoalScored -= OnGoalScoredPause;
        }
    }

    IEnumerator BindWhenReady()
    {
        while (GameRulesManager.Instance == null || OpponentBotController.Instance == null)
        {
            yield return null;
        }

        GameRulesManager.Instance.PlayerGoalScored -= OnGoalScoredPause;
        GameRulesManager.Instance.PlayerGoalScored += OnGoalScoredPause;
        OpponentBotController.Instance.OpponentGoalScored -= OnGoalScoredPause;
        OpponentBotController.Instance.OpponentGoalScored += OnGoalScoredPause;
        GameRulesManager.Instance.RoundReset -= OnRoundResetResume;
        GameRulesManager.Instance.RoundReset += OnRoundResetResume;
    }

    void OnGoalScoredPause()
    {
        Pause();
    }

    void OnRoundResetResume()
    {
        Resume();
    }

    public void Pause()
    {
        _isPaused = true;
    }

    public void Resume()
    {
        _isPaused = false;
    }

    IEnumerator CountdownRoutine()
    {
        while (_remaining > 0f)
        {
            yield return null;

            if (_isPaused)
            {
                continue;
            }

            _remaining -= Time.deltaTime;
            UpdateDisplay(Mathf.Max(0, Mathf.CeilToInt(_remaining)));
        }

        UpdateDisplay(0);
        TimerExpired?.Invoke();
    }

    void UpdateDisplay(int seconds)
    {
        if (_label != null)
        {
            _label.SetText(seconds.ToString());
        }
    }
}

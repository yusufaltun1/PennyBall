using System.Collections;
using UnityEngine;

public class LeagueMatchController : MonoBehaviour
{
    public static LeagueMatchController Instance { get; private set; }

    public event System.Action<MatchResultType> MatchCompleted;

    int _playerGoals;
    int _opponentGoals;
    float _matchTimeRemaining;
    bool _matchActive;
    bool _matchReported;
    bool _matchTimerPaused;
    Coroutine _matchTimerRoutine;

    public int PlayerGoals => _playerGoals;
    public int OpponentGoals => _opponentGoals;
    public float MatchTimeRemaining => Mathf.Max(0f, _matchTimeRemaining);
    public bool IsMatchActive => _matchActive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureController()
    {
        if (Instance != null)
        {
            return;
        }

        var controllerObject = new GameObject("LeagueMatchController");
        Instance = controllerObject.AddComponent<LeagueMatchController>();
        DontDestroyOnLoad(controllerObject);
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        StartCoroutine(InitializeWhenReady());
    }

    void OnDisable()
    {
        Unsubscribe();
        StopMatchTimer();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    IEnumerator InitializeWhenReady()
    {
        while (GameRulesManager.Instance == null)
        {
            yield return null;
        }

        while (OpponentBotController.Instance == null)
        {
            yield return null;
        }

        Unsubscribe();
        Subscribe();
        BeginMatch();
    }

    void Subscribe()
    {
        GameRulesManager.Instance.PlayerGoalScored += OnPlayerGoal;
        GameRulesManager.Instance.PlayerGoalScored += OnGoalScoredPauseTimer;
        GameRulesManager.Instance.RoundReset += OnRoundResetResumeTimer;
        OpponentBotController.Instance.OpponentGoalScored += OnOpponentGoal;
        OpponentBotController.Instance.OpponentGoalScored += OnGoalScoredPauseTimer;
    }

    void Unsubscribe()
    {
        if (GameRulesManager.Instance != null)
        {
            GameRulesManager.Instance.PlayerGoalScored -= OnPlayerGoal;
            GameRulesManager.Instance.PlayerGoalScored -= OnGoalScoredPauseTimer;
            GameRulesManager.Instance.RoundReset -= OnRoundResetResumeTimer;
        }

        if (OpponentBotController.Instance != null)
        {
            OpponentBotController.Instance.OpponentGoalScored -= OnOpponentGoal;
            OpponentBotController.Instance.OpponentGoalScored -= OnGoalScoredPauseTimer;
        }
    }

    void OnGoalScoredPauseTimer()
    {
        _matchTimerPaused = true;
    }

    void OnRoundResetResumeTimer()
    {
        _matchTimerPaused = false;
    }

    public void BeginMatch()
    {
        StopMatchTimer();

        _playerGoals = 0;
        _opponentGoals = 0;
        _matchTimeRemaining = LeagueConfig.MatchDurationSeconds;
        _matchActive = true;
        _matchReported = false;
        _matchTimerPaused = false;

        if (LeagueService.Instance == null)
        {
            return;
        }

        BotPlayerEntry opponent = LeagueService.Instance.GetCurrentOpponent()
            ?? LeagueService.Instance.PickOpponentForNextMatch();

        if (opponent != null)
        {
            MatchSessionContext.SetOpponent(opponent);
            if (OpponentBotController.Instance != null)
            {
                OpponentBotController.Instance.ApplySessionOpponentDifficulty();
            }
        }

        _matchTimerRoutine = StartCoroutine(MatchTimerRoutine());
    }

    IEnumerator MatchTimerRoutine()
    {
        while (_matchActive && !_matchReported && _matchTimeRemaining > 0f)
        {
            if (!_matchTimerPaused)
            {
                _matchTimeRemaining -= Time.deltaTime;
            }

            yield return null;
        }

        if (_matchActive && !_matchReported)
        {
            CompleteMatch(ResolveResultByScore());
        }
    }

    void OnPlayerGoal()
    {
        if (!_matchActive || _matchReported)
        {
            return;
        }

        _playerGoals++;
    }

    void OnOpponentGoal()
    {
        if (!_matchActive || _matchReported)
        {
            return;
        }

        _opponentGoals++;
    }

    static MatchResultType ResolveResultByScore(int playerGoals, int opponentGoals)
    {
        if (playerGoals > opponentGoals)
        {
            return MatchResultType.Win;
        }

        if (playerGoals < opponentGoals)
        {
            return MatchResultType.Loss;
        }

        return MatchResultType.Draw;
    }

    MatchResultType ResolveResultByScore()
    {
        return ResolveResultByScore(_playerGoals, _opponentGoals);
    }

    void CompleteMatch(MatchResultType result)
    {
        if (_matchReported)
        {
            return;
        }

        _matchReported = true;
        _matchActive = false;
        StopMatchTimer();

        if (LeagueService.Instance != null)
        {
            LeagueService.Instance.RegisterMatchResult(result);
            LeagueService.Instance.PickOpponentForNextMatch();
        }

        if (OpponentBotController.Instance != null)
        {
            OpponentBotController.Instance.ApplySessionOpponentDifficulty();
        }

        MatchCompleted?.Invoke(result);
        BeginMatch();
    }

    void StopMatchTimer()
    {
        if (_matchTimerRoutine != null)
        {
            StopCoroutine(_matchTimerRoutine);
            _matchTimerRoutine = null;
        }
    }
}

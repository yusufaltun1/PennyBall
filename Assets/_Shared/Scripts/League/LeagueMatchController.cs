using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LeagueMatchController : MonoBehaviour
{
    public static LeagueMatchController Instance { get; private set; }

    public event Action MatchStarted;
    public event Action MatchTimerExpired;
    public event Action ScoresChanged;
    public event Action<MatchResultType> MatchCompleted;

    int _playerGoals;
    int _opponentGoals;
    float _matchTimeRemaining;
    bool _matchActive;
    bool _matchReported;
    bool _matchTimerPaused;
    bool _applicationPaused;
    bool _matchTimerLoopActive;
    Coroutine _matchTimerRoutine;
    Coroutine _initializeRoutine;

    const float MaxTimerDeltaSeconds = 0.1f;

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
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;

        if (SceneManager.GetActiveScene().name == GameSceneNames.Game)
        {
            RequestInitialize();
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != GameSceneNames.Game)
        {
            return;
        }

        RequestInitialize();
    }

    void OnSceneUnloaded(Scene scene)
    {
        if (scene.name != GameSceneNames.Game)
        {
            return;
        }

        Unsubscribe();
        StopMatchTimer();
        MatchSessionContext.SetFinalScore(0, 0);
    }

    void RequestInitialize()
    {
        MatchSessionContext.SetFinalScore(0, 0);
        _playerGoals = 0;
        _opponentGoals = 0;
        _matchTimeRemaining = LeagueConfig.MatchDurationSeconds;
        ScoresChanged?.Invoke();

        if (_initializeRoutine != null)
        {
            StopCoroutine(_initializeRoutine);
        }

        _initializeRoutine = StartCoroutine(InitializeWhenReady());
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;

        if (Instance == this)
        {
            Instance = null;
        }
    }

    void OnApplicationPause(bool paused)
    {
        SetApplicationPaused(paused);
    }

    void OnApplicationFocus(bool hasFocus)
    {
        // Mobilde bazen sadece focus gelir; pause(false) atlanabiliyor.
        SetApplicationPaused(!hasFocus);
    }

    void SetApplicationPaused(bool paused)
    {
        if (_applicationPaused == paused)
        {
            return;
        }

        _applicationPaused = paused;

        if (!paused && _matchActive && !_matchReported)
        {
            if (!IsGoalFlowBlockingTimer() && !MatchBeginningCountdownController.IsActive)
            {
                _matchTimerPaused = false;
            }

            EnsureTimerRunning();
            ScoresChanged?.Invoke();
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
        _initializeRoutine = null;
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
        _applicationPaused = false;
        MatchSessionContext.SetFinalScore(0, 0);

        if (LeagueService.Instance == null)
        {
            ScoresChanged?.Invoke();
            _matchTimerRoutine = StartCoroutine(MatchTimerRoutine());
            MatchStarted?.Invoke();
            return;
        }

        BotPlayerEntry opponent = LeagueService.Instance.GetCurrentOpponent()
            ?? LeagueService.Instance.PickOpponentForNextMatch();

        if (opponent != null)
        {
            MatchSessionContext.SetOpponent(opponent);
            OpponentBotController.Instance?.ApplySessionOpponentDifficulty();
        }

        ScoresChanged?.Invoke();
        _matchTimerRoutine = StartCoroutine(MatchTimerRoutine());
        MatchStarted?.Invoke();
    }

    public void AbandonActiveMatch(string reason)
    {
        if (!_matchActive || _matchReported)
        {
            return;
        }

        MatchSessionTracker.MarkAbandon(reason);
        CompleteMatch(ResolveResultByScore(), startNextMatch: false);
    }

    IEnumerator MatchTimerRoutine()
    {
        _matchTimerLoopActive = true;

        try
        {
            while (MatchBeginningCountdownController.IsActive)
            {
                yield return null;
            }

            while (_matchActive && !_matchReported && _matchTimeRemaining > 0f)
            {
                if (IsGoalFlowBlockingTimer())
                {
                    yield return null;
                    continue;
                }

                if (!_matchTimerPaused && !_applicationPaused)
                {
                    float delta = Mathf.Min(Time.unscaledDeltaTime, MaxTimerDeltaSeconds);
                    _matchTimeRemaining -= delta;
                }

                yield return null;
            }

            if (_matchActive && !_matchReported)
            {
                _matchTimeRemaining = 0f;
                MatchTimerExpired?.Invoke();
            }
        }
        finally
        {
            _matchTimerLoopActive = false;
            _matchTimerRoutine = null;
        }
    }

    void EnsureTimerRunning()
    {
        if (!_matchActive || _matchReported || _matchTimerLoopActive)
        {
            return;
        }

        _matchTimerRoutine = StartCoroutine(MatchTimerRoutine());
    }

    static bool IsGoalFlowBlockingTimer()
    {
        return GameRulesManager.Instance != null && GameRulesManager.Instance.IsMatchLockedForInput;
    }

    public MatchResultType CompleteMatchFromTimer()
    {
        if (_matchReported)
        {
            return ResolveResultByScore(
                MatchSessionContext.PlayerGoalsAtEnd,
                MatchSessionContext.OpponentGoalsAtEnd);
        }

        if (!_matchActive)
        {
            return ResolveResultByScore(
                MatchSessionContext.PlayerGoalsAtEnd,
                MatchSessionContext.OpponentGoalsAtEnd);
        }

        MatchResultType result = ResolveResultByScore();
        CompleteMatch(result, startNextMatch: false);
        return result;
    }

    void OnPlayerGoal()
    {
        if (!_matchActive || _matchReported)
        {
            return;
        }

        _playerGoals++;
        ScoresChanged?.Invoke();
    }

    void OnOpponentGoal()
    {
        if (!_matchActive || _matchReported)
        {
            return;
        }

        _opponentGoals++;
        ScoresChanged?.Invoke();
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

    void CompleteMatch(MatchResultType result, bool startNextMatch = true)
    {
        if (_matchReported)
        {
            return;
        }

        _matchReported = true;
        _matchActive = false;
        StopMatchTimer();

        MatchSessionContext.SetFinalScore(_playerGoals, _opponentGoals);
        ScoresChanged?.Invoke();

        if (LeagueService.Instance != null)
        {
            bool registered = LeagueService.Instance.RegisterMatchResult(result);
            if (registered)
            {
                LeagueService.Instance.PickOpponentForNextMatch();
            }
            else
            {
                return;
            }
        }

        OpponentBotController.Instance?.ApplySessionOpponentDifficulty();
        MatchCompleted?.Invoke(result);

        if (startNextMatch)
        {
            BeginMatch();
        }
    }

    void StopMatchTimer()
    {
        _matchTimerLoopActive = false;

        if (_matchTimerRoutine != null)
        {
            StopCoroutine(_matchTimerRoutine);
            _matchTimerRoutine = null;
        }
    }
}

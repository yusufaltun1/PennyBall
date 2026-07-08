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
    Coroutine _prepareMatchRoutine;

    const float MaxTimerDeltaSeconds = 0.1f;
    const float BackgroundForfeitThresholdSeconds = 10f;
    const int BackgroundForfeitPlayerGoals = 0;
    const int BackgroundForfeitOpponentGoals = 3;

    long _backgroundEnteredUtcTicks;
    bool _backgroundTimeTracked;

    public int PlayerGoals => _playerGoals;
    public int OpponentGoals => _opponentGoals;
    public float MatchTimeRemaining => Mathf.Max(0f, _matchTimeRemaining);
    public bool IsMatchActive => _matchActive;

    public bool AddMatchTime(float seconds)
    {
        if (!_matchActive || _matchReported || seconds <= 0f)
        {
            return false;
        }

        _matchTimeRemaining += seconds;
        return true;
    }

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
        SceneManager.sceneLoaded += OnSceneLoaded;
        QueuePrepareAndBeginMatch();
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Unsubscribe();
        StopMatchTimer();

        if (_prepareMatchRoutine != null)
        {
            StopCoroutine(_prepareMatchRoutine);
            _prepareMatchRoutine = null;
        }
    }

    void OnDestroy()
    {
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

        if (paused)
        {
            _applicationPaused = true;

            if (CanForfeitFromBackground())
            {
                _backgroundEnteredUtcTicks = DateTime.UtcNow.Ticks;
                _backgroundTimeTracked = true;
            }

            return;
        }

        _applicationPaused = false;

        if (CanForfeitFromBackground() && _backgroundTimeTracked)
        {
            _backgroundTimeTracked = false;
            double elapsedSeconds = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - _backgroundEnteredUtcTicks).TotalSeconds;

            if (elapsedSeconds >= BackgroundForfeitThresholdSeconds)
            {
                Debug.Log(
                    $"[Match] Arka plan {elapsedSeconds:F1}s (≥{BackgroundForfeitThresholdSeconds:F0}s) — " +
                    $"hükmen {BackgroundForfeitOpponentGoals}-{BackgroundForfeitPlayerGoals} mağlubiyet.");
                ForfeitMatchFromBackground();
                return;
            }

            Debug.Log($"[Match] Arka plan {elapsedSeconds:F1}s — maç devam ediyor.");
        }
        else
        {
            _backgroundTimeTracked = false;
        }

        if (!_matchActive || _matchReported)
        {
            return;
        }

        if (!IsGoalFlowBlockingTimer() && !MatchBeginningCountdownController.IsActive)
        {
            _matchTimerPaused = false;
        }

        EnsureTimerRunning();
        ScoresChanged?.Invoke();
    }

    static bool IsInLeagueGameScene()
    {
        return SceneManager.GetActiveScene().name == GameSceneNames.Game;
    }

    bool CanForfeitFromBackground()
    {
        return IsInLeagueGameScene() && _matchActive && !_matchReported && !ExerciseRuntime.IsActive;
    }

    void ForfeitMatchFromBackground()
    {
        if (!CanForfeitFromBackground())
        {
            return;
        }

        _playerGoals = BackgroundForfeitPlayerGoals;
        _opponentGoals = BackgroundForfeitOpponentGoals;
        _matchTimeRemaining = 0f;
        _matchTimerPaused = false;

        StopMatchTimer();
        MatchSessionTracker.MarkAbandon("background_forfeit");
        ScoresChanged?.Invoke();
        MatchTimerExpired?.Invoke();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsGameplayScene(scene.name))
        {
            ResetMatchStateForNewScene();
            QueuePrepareAndBeginMatch();
            return;
        }

        if (scene.name == GameSceneNames.MainMenu)
        {
            ResetForMenu();
        }
    }

    void ResetMatchStateForNewScene()
    {
        StopMatchTimer();
        _playerGoals = 0;
        _opponentGoals = 0;
        _matchTimeRemaining = GetConfiguredMatchDuration();
        _matchActive = false;
        _matchReported = false;
        _matchTimerPaused = false;
        _applicationPaused = false;
        _backgroundTimeTracked = false;
        MatchSessionContext.Clear();
        ScoresChanged?.Invoke();
    }

    static float GetConfiguredMatchDuration() =>
        ExerciseRuntime.IsActive
            ? LeagueConfig.ExerciseMatchDurationSeconds
            : LeagueConfig.MatchDurationSeconds;

    static bool IsGameplayScene(string sceneName) =>
        sceneName == GameSceneNames.Game || sceneName == GameSceneNames.Exercise;

    void QueuePrepareAndBeginMatch()
    {
        if (!IsGameplayScene(SceneManager.GetActiveScene().name))
        {
            return;
        }

        if (_prepareMatchRoutine != null)
        {
            StopCoroutine(_prepareMatchRoutine);
        }

        _prepareMatchRoutine = StartCoroutine(PrepareAndBeginMatchRoutine());
    }

    void ResetForMenu()
    {
        StopMatchTimer();
        _matchActive = false;
        _matchReported = true;
        _playerGoals = 0;
        _opponentGoals = 0;
        _matchTimeRemaining = 0f;
        MatchSessionContext.Clear();
    }

    IEnumerator PrepareAndBeginMatchRoutine()
    {
        ResetMatchStateForNewScene();

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
        _prepareMatchRoutine = null;
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
        _matchTimeRemaining = GetConfiguredMatchDuration();
        _matchActive = true;
        _matchReported = false;
        _matchTimerPaused = false;
        _applicationPaused = false;
        _backgroundTimeTracked = false;

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

        if (ExerciseRuntime.IsActive)
        {
            MatchSessionTracker.TryConsumeResult(out _, out _, out _);
            OpponentBotController.Instance?.ApplySessionOpponentDifficulty();
            MatchCompleted?.Invoke(result);
            return;
        }

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

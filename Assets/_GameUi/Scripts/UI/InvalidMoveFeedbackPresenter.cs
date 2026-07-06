using System.Collections;
using TMPro;
using UnityEngine;

public class InvalidMoveFeedbackPresenter : MonoBehaviour
{
    const int StreakResetThreshold = 6;

    [SerializeField] GameObject _invalidMoveRoot;
    [SerializeField] TextMeshProUGUI _counterText;
    [SerializeField] GameObject _resettingGameRoot;
    [SerializeField] float _hideDelayAfterRollback = 1f;
    [SerializeField] float _resettingGameDisplayDuration = 1f;

    int _consecutiveInvalidMoves;
    Coroutine _feedbackRoutine;
    bool _isPerformingStreakReset;

    public static void SuppressForGoal()
    {
        InvalidMoveFeedbackPresenter presenter =
            FindFirstObjectByType<InvalidMoveFeedbackPresenter>(FindObjectsInactive.Include);
        presenter?.SuppressImmediately();
    }

    public void SuppressImmediately()
    {
        if (_feedbackRoutine != null)
        {
            StopCoroutine(_feedbackRoutine);
            _feedbackRoutine = null;
        }

        _isPerformingStreakReset = false;
        HideAllImmediate();
    }

    void Awake()
    {
        ResolveReferences();
        HideAllImmediate();
    }

    void OnEnable()
    {
        StartCoroutine(SubscribeWhenReady());
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    IEnumerator SubscribeWhenReady()
    {
        while (GameRulesManager.Instance == null)
        {
            yield return null;
        }

        Unsubscribe();
        GameRulesManager.Instance.InvalidMoveRollbackStarted += OnInvalidMoveRollbackStarted;
        GameRulesManager.Instance.InvalidMoveRollbackFinished += OnInvalidMoveRollbackFinished;
        GameRulesManager.Instance.ValidShotCommitted += OnValidShotCommitted;
        GameRulesManager.Instance.RoundReset += OnRoundReset;
        GameRulesManager.Instance.GoalCelebrationStarted += SuppressImmediately;

        while (OpponentBotController.Instance == null)
        {
            yield return null;
        }

        OpponentBotController.Instance.InvalidMoveRollbackStarted += OnInvalidMoveRollbackStarted;
        OpponentBotController.Instance.InvalidMoveRollbackFinished += OnInvalidMoveRollbackFinished;
        OpponentBotController.Instance.ValidShotCommitted += OnValidShotCommitted;
    }

    void Unsubscribe()
    {
        if (GameRulesManager.Instance != null)
        {
            GameRulesManager.Instance.InvalidMoveRollbackStarted -= OnInvalidMoveRollbackStarted;
            GameRulesManager.Instance.InvalidMoveRollbackFinished -= OnInvalidMoveRollbackFinished;
            GameRulesManager.Instance.ValidShotCommitted -= OnValidShotCommitted;
            GameRulesManager.Instance.RoundReset -= OnRoundReset;
            GameRulesManager.Instance.GoalCelebrationStarted -= SuppressImmediately;
        }

        if (OpponentBotController.Instance != null)
        {
            OpponentBotController.Instance.InvalidMoveRollbackStarted -= OnInvalidMoveRollbackStarted;
            OpponentBotController.Instance.InvalidMoveRollbackFinished -= OnInvalidMoveRollbackFinished;
            OpponentBotController.Instance.ValidShotCommitted -= OnValidShotCommitted;
        }
    }

    void OnInvalidMoveRollbackStarted(CoinTeam team)
    {
        if (ShouldSuppressForGoal())
        {
            return;
        }

        _consecutiveInvalidMoves++;
        UpdateCounterText();
        ShowInvalidMove();
    }

    void OnInvalidMoveRollbackFinished(CoinTeam team)
    {
        if (ShouldSuppressForGoal())
        {
            SuppressImmediately();
            return;
        }
        if (_consecutiveInvalidMoves >= StreakResetThreshold)
        {
            if (_feedbackRoutine != null)
            {
                StopCoroutine(_feedbackRoutine);
            }

            _feedbackRoutine = StartCoroutine(ResetPositionsRoutine());
            return;
        }

        if (_feedbackRoutine != null)
        {
            StopCoroutine(_feedbackRoutine);
        }

        _feedbackRoutine = StartCoroutine(HideInvalidMoveAfterDelay());
    }

    void OnValidShotCommitted(CoinTeam team)
    {
        _consecutiveInvalidMoves = 0;
    }

    void OnRoundReset()
    {
        _consecutiveInvalidMoves = 0;

        if (_isPerformingStreakReset)
        {
            return;
        }

        if (_feedbackRoutine != null)
        {
            StopCoroutine(_feedbackRoutine);
            _feedbackRoutine = null;
        }

        HideAllImmediate();
    }

    IEnumerator HideInvalidMoveAfterDelay()
    {
        float delay = _hideDelayAfterRollback;
        OnboardingGuideController onboarding = OnboardingGuideController.Instance;
        if (onboarding != null && onboarding.ShouldUseExtendedInvalidMoveHideDelay)
        {
            delay *= 2f;
        }

        yield return new WaitForSeconds(delay);
        HideInvalidMove();
        _feedbackRoutine = null;
    }

    IEnumerator ResetPositionsRoutine()
    {
        _isPerformingStreakReset = true;
        HideInvalidMove();
        ShowResettingGame();
        float shownAt = Time.unscaledTime;

        if (OpponentBotController.Instance != null)
        {
            OpponentBotController.Instance.FreezeMatch();
        }

        if (GameRulesManager.Instance != null)
        {
            yield return GameRulesManager.Instance.ResetAllCoinPositionsRoutine();
        }

        float remaining = _resettingGameDisplayDuration - (Time.unscaledTime - shownAt);
        if (remaining > 0f)
        {
            yield return new WaitForSecondsRealtime(remaining);
        }

        _consecutiveInvalidMoves = 0;
        HideResettingGame();
        _isPerformingStreakReset = false;
        _feedbackRoutine = null;
    }

    void UpdateCounterText()
    {
        if (_counterText != null)
        {
            _counterText.text = _consecutiveInvalidMoves.ToString();
        }
    }

    void ShowInvalidMove()
    {
        if (_invalidMoveRoot != null)
        {
            _invalidMoveRoot.SetActive(true);
        }

        PlayWhistle();
    }

    void HideInvalidMove()
    {
        if (_invalidMoveRoot != null)
        {
            _invalidMoveRoot.SetActive(false);
        }
    }

    void ShowResettingGame()
    {
        if (_resettingGameRoot != null)
        {
            _resettingGameRoot.SetActive(true);
        }

        PlayWhistle();
    }

    void HideResettingGame()
    {
        if (_resettingGameRoot != null)
        {
            _resettingGameRoot.SetActive(false);
        }
    }

    void HideAllImmediate()
    {
        HideInvalidMove();
        HideResettingGame();
    }

    bool ShouldSuppressForGoal()
    {
        if (GameRulesManager.Instance == null)
        {
            return false;
        }

        return GameRulesManager.Instance.IsGoalSequenceActive
            || GameRulesManager.Instance.HasPendingGoalFreeze;
    }

    void ResolveReferences()
    {
        if (_invalidMoveRoot == null)
        {
            GameObject invalidMove = GameObject.Find("InvalidMove");
            if (invalidMove != null)
            {
                _invalidMoveRoot = invalidMove;
            }
        }

        if (_counterText == null && _invalidMoveRoot != null)
        {
            Transform counter = _invalidMoveRoot.transform.Find("Counter");
            if (counter != null)
            {
                _counterText = counter.GetComponent<TextMeshProUGUI>();
            }
        }

        if (_resettingGameRoot == null)
        {
            GameObject resettingGame = GameObject.Find("ResettingGame");
            if (resettingGame != null)
            {
                _resettingGameRoot = resettingGame;
            }
        }
    }

    void PlayWhistle()
    {
        if (GameFeedback.Instance != null)
        {
            GameFeedback.Instance.PlayWhistle();
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class OnboardingController : MonoBehaviour
{
    [SerializeField] OnboardingCoin[] _coins;
    [SerializeField] OnboardingCoin _gateCoinA;
    [SerializeField] OnboardingCoin _gateCoinB;
    [SerializeField] OnboardingGoalDetector _goalDetector;
    [SerializeField] OnboardingGuideView _guideView;
    [SerializeField] OnboardingStepDefinition[] _steps;
    [SerializeField] float _gateMargin = 0.09f;
    [SerializeField] float _resetDuration = 0.45f;
    [SerializeField] Color _inactiveCoinColor = new(0.45f, 0.45f, 0.45f, 1f);

    readonly List<Vector3> _pathSamples = new(64);
    readonly Dictionary<OnboardingCoin, Vector3> _initialPositions = new();

    const int ThirdCoinGoalStepIndex = 2;
    const int FirstCoinGoalStepIndex = 3;

    int _currentStepIndex;
    bool _isBusy;
    bool _hasBegunFirstStep;
    bool _goalEnteredDuringShot;
    bool _isCompletingOnboarding;
    bool _scoreGoalFinishConsumed;
    Vector3 _shotStartPosition;
    Coroutine _resolveRoutine;

    public bool IsBusy => _isBusy;

    public int CurrentStepIndex => _currentStepIndex;

    public void EnsureConfigured(
        OnboardingCoin[] coins,
        OnboardingCoin gateCoinA,
        OnboardingCoin gateCoinB,
        OnboardingGoalDetector goalDetector,
        OnboardingGuideView guideView,
        OnboardingStepDefinition[] steps)
    {
        _coins = coins;
        _gateCoinA = gateCoinA;
        _gateCoinB = gateCoinB;
        _goalDetector = goalDetector;
        _guideView = guideView;
        EnsureStepsCatalog();
        _isCompletingOnboarding = false;
        SubscribeLaunchHandlers();

        if (!_hasBegunFirstStep)
        {
            BootstrapFirstStep();
        }
    }

    void Start()
    {
        if (!_hasBegunFirstStep && _steps != null && _steps.Length > 0)
        {
            BootstrapFirstStep();
        }
    }

    void BootstrapFirstStep()
    {
        CacheInitialPositions();
        SubscribeLaunchHandlers();

        if (_goalDetector != null)
        {
            _goalDetector.GoalScored -= OnGoalScored;
            _goalDetector.GoalScored += OnGoalScored;
        }

        BeginStep(0);
    }

    void SubscribeLaunchHandlers()
    {
        if (_coins == null)
        {
            return;
        }

        for (int i = 0; i < _coins.Length; i++)
        {
            OnboardingCoin coin = _coins[i];
            if (coin == null)
            {
                continue;
            }

            OnboardingCoinDragController dragController = coin.DragController;
            dragController.BindController(this);
        }
    }

    static OnboardingController _activeController;

    public static OnboardingController ActiveController => _activeController;

    void Awake()
    {
        if (_activeController != null && _activeController != this)
        {
            Destroy(gameObject);
            return;
        }

        _activeController = this;
    }

    void OnDestroy()
    {
        if (_activeController == this)
        {
            _activeController = null;
        }

        if (_goalDetector != null)
        {
            _goalDetector.GoalScored -= OnGoalScored;
        }

        UnsubscribeLaunchHandlers();
    }

    void UnsubscribeLaunchHandlers()
    {
        if (_coins == null)
        {
            return;
        }

        for (int i = 0; i < _coins.Length; i++)
        {
            OnboardingCoin coin = _coins[i];
            if (coin == null)
            {
                continue;
            }

            coin.DragController.BindController(null);
        }
    }

    void CacheInitialPositions()
    {
        _initialPositions.Clear();
        if (_coins == null)
        {
            return;
        }

        for (int i = 0; i < _coins.Length; i++)
        {
            OnboardingCoin coin = _coins[i];
            if (coin != null)
            {
                _initialPositions[coin] = coin.transform.position;
            }
        }
    }

    public bool CanSelectCoin(OnboardingCoin coin)
    {
        if (_isBusy || coin == null || !coin.IsSelectable)
        {
            return false;
        }

        OnboardingStepDefinition step = GetCurrentStep();
        return step != null && coin.CoinIndex == step.activeCoinIndex;
    }

    public bool ValidateAim(OnboardingCoinDragController dragController, out OnboardingAimFeedback feedback)
    {
        feedback = default;
        if (dragController == null)
        {
            return false;
        }

        OnboardingStepDefinition step = GetCurrentStep();
        if (step == null)
        {
            return false;
        }

        if (!dragController.TryGetLaunchData(out Vector3 launchDirection, out float pullDistance))
        {
            return true;
        }

        Vector3 targetDirection = GetTargetDirectionForStep(step, dragController.transform.position);
        feedback = OnboardingAimValidator.Evaluate(
            launchDirection,
            pullDistance,
            targetDirection,
            step.targetPullDistance,
            step.directionToleranceDegrees,
            step.pullTolerance);
        return true;
    }

    public void OnShotReleased(OnboardingCoin coin, OnboardingCoinDragController dragController)
    {
        if (_activeController != this || _isBusy || coin == null || dragController == null)
        {
            return;
        }

        OnboardingStepDefinition step = GetCurrentStep();
        if (step != null && coin.CoinIndex != step.activeCoinIndex)
        {
            return;
        }

        _shotStartPosition = coin.transform.position;
        _scoreGoalFinishConsumed = false;
        SetInactiveCoinsFrozen(true);

        if (step != null && step.stepType == OnboardingStepType.GuidedShot)
        {
            AdvanceFromGuidedShot();
            SetInactiveCoinsFrozen(false);
            return;
        }

        if (_resolveRoutine != null)
        {
            StopCoroutine(_resolveRoutine);
        }

        _resolveRoutine = StartCoroutine(ResolveShotRoutine(coin, dragController));
    }

    void AdvanceFromGuidedShot()
    {
        EnsureStepsCatalog();

        if (_currentStepIndex + 1 >= _steps.Length)
        {
            CompleteOnboarding();
            return;
        }

        OnboardingStepDefinition previousStep = GetCurrentStep();
        int previousActiveCoinIndex = previousStep != null ? previousStep.activeCoinIndex : -1;

        _currentStepIndex++;
        OnboardingStepDefinition nextStep = GetCurrentStep();
        if (nextStep == null)
        {
            return;
        }

        if (previousActiveCoinIndex != nextStep.activeCoinIndex)
        {
            PrepareActiveCoinAtHome(nextStep.activeCoinIndex);
        }

        ApplyCoinStates(nextStep);
        ShowGuideForStep(nextStep);
    }

    void PrepareActiveCoinAtHome(int activeCoinIndex)
    {
        ResetCoinToHome(activeCoinIndex);
    }

    void ResetCoinToHome(int coinIndex)
    {
        OnboardingCoin coin = GetCoinByIndex(coinIndex);
        if (coin == null)
        {
            return;
        }

        if (!_initialPositions.TryGetValue(coin, out Vector3 homePosition))
        {
            return;
        }

        coin.DragController.ResetToPosition(homePosition);
    }

    IEnumerator ResolveShotRoutine(OnboardingCoin coin, OnboardingCoinDragController dragController)
    {
        _isBusy = true;
        _goalEnteredDuringShot = false;
        int shotStepIndex = _currentStepIndex;
        OnboardingStepDefinition step = GetStepAtIndex(shotStepIndex);
        _pathSamples.Clear();
        _pathSamples.Add(coin.transform.position);

        yield return new WaitForSeconds(0.05f);

        while (dragController.IsSliding)
        {
            _pathSamples.Add(coin.transform.position);

            if (step != null && step.stepType == OnboardingStepType.ScoreGoal)
            {
                TrackGoalDuringShot(coin);

                if (_goalEnteredDuringShot)
                {
                    FinishScoreGoalStep();
                    SetInactiveCoinsFrozen(false);
                    yield break;
                }
            }

            yield return null;
        }

        _pathSamples.Add(coin.transform.position);
        yield return new WaitForSeconds(0.1f);
        _pathSamples.Add(coin.transform.position);

        if (_currentStepIndex != shotStepIndex)
        {
            _isBusy = false;
            _resolveRoutine = null;
            SetInactiveCoinsFrozen(false);
            yield break;
        }

        if (step != null && step.stepType == OnboardingStepType.ScoreGoal)
        {
            TrackGoalDuringShot(coin);

            if (!_goalEnteredDuringShot)
            {
                yield return new WaitForSeconds(0.2f);
                TrackGoalDuringShot(coin);
            }

            if (_goalEnteredDuringShot)
            {
                FinishScoreGoalStep();
                SetInactiveCoinsFrozen(false);
                yield break;
            }

            _isBusy = false;
            _resolveRoutine = null;
            SetInactiveCoinsFrozen(false);
            yield break;
        }

        bool stepPassed = EvaluateStep(step, coin);
        if (!stepPassed)
        {
            yield return ResetCoinRoutine(coin, _shotStartPosition);
            _isBusy = false;
            SetInactiveCoinsFrozen(false);
            yield break;
        }

        if (shotStepIndex == 1)
        {
            EnterThirdCoinGoalStep();
            _isBusy = false;
            _resolveRoutine = null;
            SetInactiveCoinsFrozen(false);
            yield break;
        }

        if (_currentStepIndex + 1 < (_steps?.Length ?? 0))
        {
            yield return AdvanceToStepRoutine(_currentStepIndex + 1);
        }
        else
        {
            _isBusy = false;
            CompleteOnboarding();
        }

        SetInactiveCoinsFrozen(false);
    }

    void EnterThirdCoinGoalStep()
    {
        ResetAllCoinsToHome();
        EnterStep(ThirdCoinGoalStepIndex);
    }

    void EnterStep(int stepIndex)
    {
        EnsureStepsCatalog();

        if (stepIndex < 0 || stepIndex >= _steps.Length)
        {
            return;
        }

        _currentStepIndex = stepIndex;
        _goalEnteredDuringShot = false;
        _scoreGoalFinishConsumed = false;

        OnboardingStepDefinition step = GetCurrentStep();
        if (step == null)
        {
            return;
        }

        ApplyCoinStates(step);
        ShowGuideForStep(step);
    }

    void ResetAllCoinsToHome()
    {
        if (_coins == null)
        {
            return;
        }

        for (int i = 0; i < _coins.Length; i++)
        {
            ResetCoinToHome(i);
        }
    }

    void SetInactiveCoinsFrozen(bool frozen)
    {
        if (_coins == null)
        {
            return;
        }

        OnboardingStepDefinition step = GetCurrentStep();
        int activeCoinIndex = step != null ? step.activeCoinIndex : -1;

        for (int i = 0; i < _coins.Length; i++)
        {
            OnboardingCoin coin = _coins[i];
            if (coin == null || coin.CoinIndex == activeCoinIndex)
            {
                continue;
            }

            Rigidbody rigidbody = coin.DragController.GetComponent<Rigidbody>();
            if (rigidbody == null)
            {
                continue;
            }

            if (frozen)
            {
                rigidbody.linearVelocity = Vector3.zero;
                rigidbody.angularVelocity = Vector3.zero;
            }

            rigidbody.isKinematic = frozen;
        }
    }

    IEnumerator AdvanceToStepRoutine(int nextStepIndex)
    {
        OnboardingStepDefinition previousStep = GetCurrentStep();
        int previousActiveCoinIndex = previousStep != null ? previousStep.activeCoinIndex : -1;

        _currentStepIndex = Mathf.Clamp(nextStepIndex, 0, _steps.Length - 1);
        OnboardingStepDefinition step = GetCurrentStep();
        if (step == null)
        {
            _isBusy = false;
            yield break;
        }

        if (previousActiveCoinIndex != step.activeCoinIndex)
        {
            OnboardingCoin activeCoin = GetCoinByIndex(step.activeCoinIndex);
            if (activeCoin != null && _initialPositions.TryGetValue(activeCoin, out Vector3 homePosition))
            {
                yield return ResetCoinRoutine(activeCoin, homePosition);
            }
        }

        ApplyCoinStates(step);
        ShowGuideForStep(step);
        _isBusy = false;
    }

    void ShowStepSuccess(string message)
    {
        if (_guideView != null)
        {
            _guideView.Show(message);
        }
    }

    bool EvaluateStep(OnboardingStepDefinition step, OnboardingCoin coin)
    {
        if (step == null)
        {
            return false;
        }

        switch (step.stepType)
        {
            case OnboardingStepType.GuidedShot:
                return true;
            case OnboardingStepType.PassBetween:
            {
                OnboardingCoin gateA = GetCoinByIndex(step.gateCoinAIndex);
                OnboardingCoin gateB = GetCoinByIndex(step.gateCoinBIndex);
                if (gateA == null || gateB == null)
                {
                    return false;
                }

                return OnboardingPassBetweenValidator.DidPassBetweenAlongPath(
                    _pathSamples,
                    gateA.transform.position,
                    gateB.transform.position,
                    _gateMargin);
            }
            case OnboardingStepType.ScoreGoal:
                return _goalEnteredDuringShot;
            default:
                return false;
        }
    }

    void OnGoalScored(OnboardingCoin coin)
    {
        OnboardingStepDefinition step = GetCurrentStep();
        if (step == null || step.stepType != OnboardingStepType.ScoreGoal)
        {
            return;
        }

        if (coin.CoinIndex != step.activeCoinIndex)
        {
            return;
        }

        _goalEnteredDuringShot = true;

        if (_isBusy && !_scoreGoalFinishConsumed)
        {
            FinishScoreGoalStep();
        }
    }

    void TrackGoalDuringShot(OnboardingCoin coin)
    {
        if (coin == null)
        {
            return;
        }

        if (IsCoinInGoal(coin))
        {
            _goalEnteredDuringShot = true;
        }
    }

    bool IsCoinInGoal(OnboardingCoin coin)
    {
        if (_goalDetector != null && _goalDetector.ContainsCoin(coin, 0.75f))
        {
            return true;
        }

        Collider goalCollider = GetGoalTriggerCollider();
        if (goalCollider != null)
        {
            if (OnboardingGoalValidator.IsCoinInsideGoal(coin.transform.position, goalCollider, 0.85f))
            {
                return true;
            }

            if (OnboardingGoalValidator.DidPathEnterGoal(_pathSamples, goalCollider, 0.85f))
            {
                return true;
            }

            Collider[] coinColliders = coin.GetComponentsInChildren<Collider>();
            for (int i = 0; i < coinColliders.Length; i++)
            {
                Collider coinCollider = coinColliders[i];
                if (coinCollider == null || coinCollider.isTrigger)
                {
                    continue;
                }

                Vector3 sample = coinCollider.bounds.center;
                if (OnboardingGoalValidator.IsCoinInsideGoal(sample, goalCollider, 0.85f))
                {
                    return true;
                }
            }
        }

        return IsNearGoalArea(coin.transform.position);
    }

    static bool IsNearGoalArea(Vector3 worldPosition)
    {
        GameObject kaleE = GameObject.Find("Kale_E");
        if (kaleE == null)
        {
            return false;
        }

        Transform goalTrigger = kaleE.transform.Find("GoalTrigger");
        if (goalTrigger != null)
        {
            Collider collider = goalTrigger.GetComponent<Collider>();
            if (collider != null)
            {
                Bounds bounds = collider.bounds;
                bounds.Expand(0.55f);
                if (bounds.Contains(worldPosition))
                {
                    return true;
                }
            }
        }

        Vector3 goalCenter = goalTrigger != null ? goalTrigger.position : kaleE.transform.position;
        Vector3 delta = worldPosition - goalCenter;
        delta.y = 0f;
        return delta.sqrMagnitude <= 1.1f * 1.1f;
    }

    void EnsureStepsCatalog()
    {
        if (_steps == null || _steps.Length < OnboardingDefaultSteps.Count)
        {
            _steps = OnboardingDefaultSteps.Create();
        }
    }

    OnboardingStepDefinition GetStepAtIndex(int stepIndex)
    {
        EnsureStepsCatalog();
        if (stepIndex < 0 || stepIndex >= _steps.Length)
        {
            return null;
        }

        return _steps[stepIndex];
    }

    void FinishScoreGoalStep()
    {
        if (_isCompletingOnboarding || _scoreGoalFinishConsumed)
        {
            return;
        }

        EnsureStepsCatalog();

        OnboardingStepDefinition step = GetCurrentStep();
        if (step == null || step.stepType != OnboardingStepType.ScoreGoal)
        {
            return;
        }

        _scoreGoalFinishConsumed = true;
        StopShotCoroutines();
        _isBusy = false;
        SetInactiveCoinsFrozen(false);

        if (step.isFinalStep || _currentStepIndex >= FirstCoinGoalStepIndex)
        {
            _isCompletingOnboarding = true;
            ShowStepSuccess("Gol!");
            StartCoroutine(CompleteOnboardingAfterDelay(0.75f));
            return;
        }

        ShowStepSuccess("Gol!");
        if (_currentStepIndex == ThirdCoinGoalStepIndex)
        {
            ResetAllCoinsToHome();
            EnterStep(FirstCoinGoalStepIndex);
            return;
        }

        EnterStep(_currentStepIndex + 1);
    }

    void StopShotCoroutines()
    {
        if (_resolveRoutine != null)
        {
            StopCoroutine(_resolveRoutine);
            _resolveRoutine = null;
        }
    }

    IEnumerator CompleteOnboardingAfterDelay(float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);
        CompleteOnboarding();
    }

    Collider GetGoalTriggerCollider()
    {
        if (_goalDetector != null)
        {
            Collider detectorCollider = _goalDetector.GetComponent<Collider>();
            if (detectorCollider != null)
            {
                return detectorCollider;
            }
        }

        GameObject kaleE = GameObject.Find("Kale_E");
        if (kaleE == null)
        {
            return null;
        }

        Transform goalTrigger = kaleE.transform.Find("GoalTrigger");
        return goalTrigger != null ? goalTrigger.GetComponent<Collider>() : null;
    }

    void BeginStep(int stepIndex)
    {
        EnsureStepsCatalog();
        _hasBegunFirstStep = true;
        EnterStep(Mathf.Clamp(stepIndex, 0, _steps.Length - 1));
    }

    void ApplyCoinStates(OnboardingStepDefinition step)
    {
        if (_coins == null)
        {
            return;
        }

        for (int i = 0; i < _coins.Length; i++)
        {
            OnboardingCoin coin = _coins[i];
            if (coin == null)
            {
                continue;
            }

            bool isActive = coin.CoinIndex == step.activeCoinIndex;
            coin.SetSelectable(isActive);
            SetCoinDimmed(coin, !isActive);
            coin.DragController.HideGuide();

            if (!isActive)
            {
                continue;
            }

            Vector3 direction = GetTargetDirectionForStep(step, coin.transform.position);
            coin.DragController.ShowGuide(
                direction,
                step.targetPullDistance,
                step.pullTolerance,
                step.directionToleranceDegrees);
        }
    }

    OnboardingCoin GetCoinByIndex(int coinIndex)
    {
        if (_coins == null)
        {
            return null;
        }

        for (int i = 0; i < _coins.Length; i++)
        {
            OnboardingCoin coin = _coins[i];
            if (coin != null && coin.CoinIndex == coinIndex)
            {
                return coin;
            }
        }

        return null;
    }

    void ShowGuideForStep(OnboardingStepDefinition step)
    {
        if (_guideView == null)
        {
            return;
        }

        _guideView.Show(step.instructionText);
    }

    Vector3 GetTargetDirectionForStep(OnboardingStepDefinition step, Vector3 coinPosition)
    {
        Vector3 direction = GetGoalDirection(coinPosition);

        if (step != null && Mathf.Abs(step.targetDirectionYawOffsetDegrees) > 0.01f)
        {
            direction = Quaternion.Euler(0f, step.targetDirectionYawOffsetDegrees, 0f) * direction;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                direction.Normalize();
            }
        }

        return direction;
    }

    Vector3 GetGoalDirection(Vector3 coinPosition)
    {
        GameObject goal = GameObject.Find("Kale_E");
        if (goal != null)
        {
            Vector3 toGoal = goal.transform.position - coinPosition;
            toGoal.y = 0f;
            if (toGoal.sqrMagnitude > 0.0001f)
            {
                return toGoal.normalized;
            }
        }

        return Vector3.forward;
    }

    IEnumerator ResetCoinRoutine(OnboardingCoin coin, Vector3 targetPosition)
    {
        OnboardingCoinDragController dragController = coin.DragController;
        Rigidbody rigidbody = dragController.GetComponent<Rigidbody>();

        if (dragController.IsAiming)
        {
            dragController.CancelAim();
        }

        rigidbody.isKinematic = true;
        rigidbody.linearVelocity = Vector3.zero;
        rigidbody.angularVelocity = Vector3.zero;

        Vector3 startPosition = coin.transform.position;
        float elapsed = 0f;

        while (elapsed < _resetDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _resetDuration);
            coin.transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            yield return null;
        }

        coin.transform.position = targetPosition;
        rigidbody.isKinematic = false;
    }

    void SetCoinDimmed(OnboardingCoin coin, bool dimmed)
    {
        Renderer[] renderers = GetCoinVisualRenderers(coin);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            int materialCount = renderer.sharedMaterials.Length;
            if (!dimmed)
            {
                for (int materialIndex = 0; materialIndex < materialCount; materialIndex++)
                {
                    renderer.SetPropertyBlock(null, materialIndex);
                }

                continue;
            }

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            for (int materialIndex = 0; materialIndex < materialCount; materialIndex++)
            {
                block.Clear();
                block.SetColor("_BaseColor", _inactiveCoinColor);
                renderer.SetPropertyBlock(block, materialIndex);
            }
        }
    }

    static Renderer[] GetCoinVisualRenderers(OnboardingCoin coin)
    {
        if (coin == null)
        {
            return System.Array.Empty<Renderer>();
        }

        Transform coinObject = coin.transform.Find("Coin_Object");
        if (coinObject == null)
        {
            return coin.GetComponentsInChildren<Renderer>();
        }

        return coinObject.GetComponentsInChildren<Renderer>();
    }

    OnboardingStepDefinition GetCurrentStep()
    {
        EnsureStepsCatalog();
        if (_currentStepIndex < 0 || _currentStepIndex >= _steps.Length)
        {
            return null;
        }

        return _steps[_currentStepIndex];
    }

    void CompleteOnboarding()
    {
        OnboardingProgress.MarkCompleted();
        SceneManager.LoadScene(OnboardingSceneNames.MainMenu);
    }
}

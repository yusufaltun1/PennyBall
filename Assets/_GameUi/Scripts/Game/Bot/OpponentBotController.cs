using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rakip bot — kurallar TeamRulesService üzerinden, atış ve çözümleme burada.
/// </summary>
public class OpponentBotController : MonoBehaviour
{
    public static OpponentBotController Instance { get; private set; }

    [Header("AI Gücü")]
    [Tooltip("Kapalıyken varsayılan strength (7) veya level-up boost kullanılır.")]
    [SerializeField] [Range(1, OpponentBotDifficulty.MaxStrengthLevel)] int _aiStrength = 7;
    [SerializeField] bool _useInspectorAiStrength;

    [Header("Hamle Tempo")]
    [Tooltip("Test için manuel süre. Kapalıyken maç sayısı veya level-up boost algoritması kullanılır.")]
    [SerializeField] [Min(0f)] float _turnThinkDelaySeconds = 2.4f;
    [SerializeField] bool _useInspectorTurnDelay;
    [Tooltip("Maç başındaki ilk AI hamlesi, Turn Think Delay'in bu oranını kullanır.")]
    [SerializeField] [Range(0.05f, 1f)] float _firstMatchThinkDelayScale = 0.35f;

    [Header("Oyun Kuralları")]
    [SerializeField] OpponentBotDifficulty _difficulty = new() { Level = 7 };
    [SerializeField] float _gateMargin        = 0.02f;
    [SerializeField] float _rollbackDuration  = 0.45f;
    [SerializeField] float _coinStopTimeout   = 8f;
    [SerializeField] float _coinBlockRadius   = 0.07f;   // yol engeli tespiti için coin yarıçapı

    readonly TeamRoundState _state = new();
    readonly List<Vector3> _pathSamples = new(64);

    CoinIdentity _resolvingCoin;
    Vector3 _shotStartPosition;
    Vector3 _gateAAtShotStart;
    Vector3 _gateBAtShotStart;
    bool _hasGateSnapshot;
    bool _goalEligibleThisShot;
    bool _scoredGoalThisShot;
    bool _isResolving;
    bool _isOpeningShot;
    int  _roundShotNumber = 1;   // bu turdaki atış sırası (1, 2, 3, 4+)
    int  _consecutiveInvalidGateFails;
    CoinIdentity _lastFailedGateShooter;
    Coroutine _playLoopRoutine;
    bool _resumePlayPending;
    bool _useShortFirstMatchThink;
    BotLevelUpBoostPolicy.AiConfig _aiConfig;
    bool _sessionBoostLogged;

    public event Action OpponentGoalScored;

    public event Action<CoinTeam> InvalidMoveRollbackStarted;
    public event Action<CoinTeam> InvalidMoveRollbackFinished;
    public event Action<CoinTeam> ValidShotCommitted;

    public bool IsResolving => _isResolving;

    public string OpponentDisplayName =>
        MatchSessionContext.HasOpponent ? MatchSessionContext.CurrentOpponent.displayName : "Opponent";

    public int OpponentAvatarIndex =>
        MatchSessionContext.HasOpponent ? MatchSessionContext.CurrentOpponent.avatarIndex : 0;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (GameFeedback.Instance != null)
        {
            GameFeedback.Instance.RefreshEventSubscriptions();
        }
    }

    void Start()
    {
        ApplySessionOpponentDifficulty();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        SyncAiStrength();
    }
#endif

    public void ApplySessionOpponentDifficulty()
    {
        _sessionBoostLogged = false;
        SyncAiStrength(logSessionEvaluation: true);
    }

    void SyncAiStrength(bool logSessionEvaluation = false)
    {
        if (_useInspectorAiStrength)
        {
            _aiStrength = Mathf.Clamp(_aiStrength, 1, OpponentBotDifficulty.MaxStrengthLevel);
            _difficulty.Level = _aiStrength;
            _aiConfig = default;

            if (!Application.isPlaying)
            {
                return;
            }

            if (logSessionEvaluation)
            {
                // BotLevelUpBoostPolicy.LogAiConfigInspector(_aiStrength, GetTurnThinkDelay());
            }

            // Debug.Log(
            //     $"[Bot] AI gücü={_aiStrength} (Inspector) | Think={GetTurnThinkDelay():F2}s | " +
            //     $"AimNoise={_difficulty.AimNoiseDegrees:F1}° | PullNoise={_difficulty.PullNoise:F3} | " +
            //     $"MaxPull={_difficulty.MaxPullScale:P0} | GoalPull={_difficulty.GoalFinishPullScale:P0} | " +
            //     $"GoalFocus={_difficulty.GoalFocus:F2}");
            return;
        }

        _aiConfig = BotLevelUpBoostPolicy.Evaluate(_useInspectorTurnDelay, _turnThinkDelaySeconds);
        _aiStrength = _aiConfig.AppliedStrength;
        _difficulty.Level = _aiStrength;

        if (!Application.isPlaying)
        {
            return;
        }

        if (logSessionEvaluation || !_sessionBoostLogged)
        {
            // BotLevelUpBoostPolicy.LogAiConfig(_aiStrength, GetTurnThinkDelay(), _aiConfig);
            _sessionBoostLogged = true;
        }

        // Debug.Log(
        //     $"[Bot] AI gücü={_aiStrength} | Mod={_aiConfig.Mode} | Think={GetTurnThinkDelay():F2}s | " +
        //     $"AimNoise={_difficulty.AimNoiseDegrees:F1}° | PullNoise={_difficulty.PullNoise:F3} | " +
        //     $"MaxPull={_difficulty.MaxPullScale:P0} | GoalPull={_difficulty.GoalFinishPullScale:P0} | " +
        //     $"GoalFocus={_difficulty.GoalFocus:F2}");
    }

    float GetTurnThinkDelay()
    {
        if (_useInspectorTurnDelay)
        {
            return _turnThinkDelaySeconds;
        }

        if (!_useInspectorAiStrength
            && _aiConfig.Mode == BotLevelUpBoostPolicy.AiConfigMode.LevelUpBoost)
        {
            return BotLevelUpBoostPolicy.BoostThinkDelaySeconds;
        }

        return BotTurnThinkDelay.GetDelayForCurrentPlayer();
    }

    float ConsumeThinkDelayForNextShot()
    {
        float delay = GetTurnThinkDelay();
        if (!_useShortFirstMatchThink)
        {
            return delay;
        }

        _useShortFirstMatchThink = false;
        return delay * _firstMatchThinkDelayScale;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void ResetRoundState(bool isMatchOpening = false)
    {
        StopPlayLoop();
        ClearResolvingState();
        _roundShotNumber = 1;
        _consecutiveInvalidGateFails = 0;
        _lastFailedGateShooter = null;
        if (isMatchOpening)
        {
            _useShortFirstMatchThink = true;
        }

        TeamRulesService.BeginNewRound(_state);
        TeamRulesService.DiscoverCoins(_state, "_E");
        PrepareOpeningTurn();
        BeginPlayLoop();
    }

    public void NotifyGoalEntered(CoinIdentity coin)
    {
        if (!_isResolving || coin == null || coin != _resolvingCoin)
        {
            return;
        }

        if (!_goalEligibleThisShot)
        {
            return;
        }

        if (_scoredGoalThisShot)
        {
            return;
        }

        _scoredGoalThisShot = true;
        Debug.Log($"GoalArea: Entered | {coin.gameObject.name}");
        GameRulesManager.Instance?.PreviewFreezeForPossibleGoal();
        coin.DragController.ForceStopSliding();
    }

    void BeginPlayLoop()
    {
        StopPlayLoop();
        _playLoopRoutine = StartCoroutine(PlayLoopRoutine());
    }

    void StopPlayLoop()
    {
        if (_playLoopRoutine != null)
        {
            StopCoroutine(_playLoopRoutine);
            _playLoopRoutine = null;
        }
    }

    public void FreezeMatch()
    {
        StopPlayLoop();
        ClearResolvingState();
        _resumePlayPending = false;
    }

    public void PauseForRoundReset()
    {
        StopPlayLoop();
        ClearResolvingState();
    }

    public void ResumePlayIfIdle()
    {
        if (_playLoopRoutine != null)
        {
            return;
        }

        if (!CanResumePlayLoop())
        {
            _resumePlayPending = true;
            return;
        }

        _resumePlayPending = false;
        BeginPlayLoop();
    }

    bool CanResumePlayLoop()
    {
        GameRulesManager rules = GameRulesManager.Instance;
        return rules == null || !rules.IsGoalSequenceActive;
    }

    void Update()
    {
        if (!_resumePlayPending || _playLoopRoutine != null)
        {
            return;
        }

        if (!CanResumePlayLoop())
        {
            return;
        }

        _resumePlayPending = false;
        BeginPlayLoop();
    }

    void ClearResolvingState()
    {
        _resolvingCoin = null;
        _isResolving = false;
        _goalEligibleThisShot = false;
        _scoredGoalThisShot = false;
    }

    IEnumerator PlayLoopRoutine()
    {
        while (true)
        {
            while (_isResolving
                   || (GameRulesManager.Instance != null && GameRulesManager.Instance.IsMatchLockedForInput))
            {
                yield return null;
            }

            SyncAiStrength();
            // if (!_useInspectorAiStrength)
            // {
            //     BotLevelUpBoostPolicy.LogAiConfig(_aiStrength, GetTurnThinkDelay(), _aiConfig, _roundShotNumber);
            // }

            yield return new WaitForSeconds(ConsumeThinkDelayForNextShot());

            if (!OpponentBotBrain.TryChooseShot(
                    _state, _difficulty, _isResolving, _gateMargin,
                    _roundShotNumber, _coinBlockRadius,
                    out OpponentBotBrain.ShotPlan plan,
                    out bool pathBlocked,
                    _lastFailedGateShooter,
                    _consecutiveInvalidGateFails))
            {
                yield return new WaitForSeconds(pathBlocked ? 5f : 0.5f);
                continue;
            }

            float launchPull = plan.PullDistance;
            if (!CoinShotLauncher.TryLaunch(plan.Coin.DragController, plan.Direction, launchPull)
                || !plan.Coin.DragController.IsSliding)
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            Debug.Log($"[Bot] {plan.Coin.name} fırlatıldı | atış#{_roundShotNumber} | {plan.Kind} | " +
                      $"+{plan.GoalAdvanceMeters:F2}m | pull={launchPull:F3}/{plan.Coin.DragController.MaxPullDistance:F3} | skor={plan.Score:F2}");

            _resolvingCoin = plan.Coin;
            GoalZone playerGoal = GoalZone.FindPlayerGoalArea();
            bool overlappingAtStart = playerGoal != null && playerGoal.OverlapsCoin(plan.Coin);
            _goalEligibleThisShot = !overlappingAtStart;
            _scoredGoalThisShot = false;
            if (_goalEligibleThisShot)
            {
                playerGoal?.ClearTrackingForCoin(plan.Coin);
            }

            _isOpeningShot = _roundShotNumber == 1;
            _shotStartPosition = plan.Coin.transform.position;
            _hasGateSnapshot = false;
            if (!_isOpeningShot
                && TeamRulesService.TryGetGateCoins(_state, plan.Coin, out CoinIdentity gateA, out CoinIdentity gateB))
            {
                _gateAAtShotStart = gateA.transform.position;
                _gateBAtShotStart = gateB.transform.position;
                _hasGateSnapshot = true;
            }

            if (!_goalEligibleThisShot)
            {
                Debug.Log($"GoalArea: Eligible=false (atış başında overlap) | {plan.Coin.name}");
            }

            yield return ResolveShotRoutine(plan.Coin);
        }
    }

    void PrepareOpeningTurn()
    {
        // Önce hepsini pasife al
        for (int i = 0; i < _state.Coins.Count; i++)
            SetCoinPassive(_state.Coins[i], true);

        if (_state.Coins.Count == 0) return;

        // 1. atış: ortadaki coin (Coins[Count/2], X'e göre sıralı)
        int midIdx = _state.Coins.Count / 2;
        _state.OpeningCoin = _state.Coins[midIdx];
        SetCoinPassive(_state.Coins[midIdx], false);
        Debug.Log($"[Bot] OpeningCoin = {_state.Coins[midIdx].name} (ortadaki, idx={midIdx})");
    }

    IEnumerator ResolveShotRoutine(CoinIdentity coin)
    {
        _isResolving = true;
        // Açılış muafiyeti: yalnızca turun ilk hamlesi (orta para).
        bool isOpeningShot = _state.IsFirstMove;

        _pathSamples.Clear();
        yield return WaitUntilCoinStops(coin.DragController, coin, _pathSamples);

        if (coin.DragController != null && coin.DragController.SlidePath.Count >= 2)
        {
            coin.DragController.CopySlidePathTo(_pathSamples);
        }

        TryCaptureBotGoalOverlap(coin);

        bool shotValid;
        bool pendingInvalidRollbackFinished = false;
        if (isOpeningShot)
        {
            _state.IsFirstMove = false;
            shotValid = true;
            TeamRulesService.UnlockOpeningSideCoins(_state, SetCoinPassive);
            Debug.Log($"[Bot] {coin.gameObject.name} açılış hamlesi geçerli");
        }
        else
        {
            float traveled = Vector3.Distance(_shotStartPosition, coin.transform.position);
            bool pathReliable = _pathSamples.Count >= 3 || traveled <= 0.12f;

            bool passedBetween = false;
            if (pathReliable && _pathSamples.Count >= 2)
            {
                if (_hasGateSnapshot)
                {
                    passedBetween = PassBetweenValidator.DidPassBetweenAlongPath(
                        _pathSamples, _gateAAtShotStart, _gateBAtShotStart, _gateMargin);
                }
                else
                {
                    passedBetween = TeamRulesService.ValidatePassBetween(
                        _state, coin, _pathSamples, _gateMargin);
                }
            }

            Debug.Log(
                $"[Bot] Kapı kontrolü | {coin.name} | atış#{_roundShotNumber} | " +
                $"path={_pathSamples.Count} | traveled={traveled:F2} | reliable={pathReliable} | geçti={passedBetween}");

            if (!passedBetween)
            {
                Debug.Log($"[Bot] {coin.gameObject.name} GEÇERSİZ — kapıdan geçemedi | " +
                          $"son pozisyon={coin.transform.position:F2}");
                _consecutiveInvalidGateFails++;
                _lastFailedGateShooter = coin;
                InvalidMoveRollbackStarted?.Invoke(CoinTeam.Opponent);
                yield return RollbackCoin(coin, _shotStartPosition);
                pendingInvalidRollbackFinished = true;
                shotValid = false;
            }
            else
            {
                shotValid = true;
                _consecutiveInvalidGateFails = 0;
                _lastFailedGateShooter = null;
                Debug.Log($"[Bot] {coin.gameObject.name} geçerli — kapıdan geçti");
            }
        }

        if (shotValid)
        {
            if (!isOpeningShot)
            {
                ValidShotCommitted?.Invoke(CoinTeam.Opponent);
            }

            _roundShotNumber++;
        }

        if (shotValid && _scoredGoalThisShot)
        {
            _resolvingCoin = null;
            _isResolving = false;

            if (GameRulesManager.Instance == null
                || !GameRulesManager.Instance.TryBeginGoalSequence(pauseOpponent: false))
            {
                yield break;
            }

            InvokeOpponentGoalScoredSafely();
            GameRulesManager.Instance.HandleEnemyGoalCelebration();
            StopPlayLoop();
            yield break;
        }

        if (GameRulesManager.Instance != null && GameRulesManager.Instance.HasPendingGoalFreeze)
        {
            GameRulesManager.Instance.UnfreezeAllRoundCoins();
            ResumePlayIfIdle();
        }

        _resolvingCoin = null;
        _isResolving = false;

        if (pendingInvalidRollbackFinished)
        {
            InvalidMoveRollbackFinished?.Invoke(CoinTeam.Opponent);
        }

        TeamRulesService.UnlockAllCoins(_state, SetCoinPassive);
        TeamRulesService.EnsureAtLeastOneSelectable(_state, SetCoinPassive);
    }

    IEnumerator WaitUntilCoinStops(CoinDragController dragController, CoinIdentity shotCoin, List<Vector3> pathSamples)
    {
        // Player (GameRulesManager.WaitUntilCoinStops) ile aynı örnekleme.
        pathSamples.Clear();
        pathSamples.Add(dragController.transform.position);

        yield return new WaitForSeconds(0.05f);

        float elapsed = 0f;
        while (dragController.IsSliding)
        {
            pathSamples.Add(dragController.transform.position);

            if (shotCoin != null && _scoredGoalThisShot)
            {
                GameRulesManager.Instance?.PreviewFreezeForPossibleGoal();
                dragController.ForceStopSliding();
                break;
            }

            if (shotCoin != null
                && _goalEligibleThisShot
                && !_scoredGoalThisShot
                && IsOverlappingPlayerGoalArea(shotCoin))
            {
                _scoredGoalThisShot = true;
                Debug.Log($"GoalArea: Entered | {shotCoin.gameObject.name}");
                GameRulesManager.Instance?.PreviewFreezeForPossibleGoal();
                dragController.ForceStopSliding();
                break;
            }

            elapsed += Time.deltaTime;
            if (elapsed >= _coinStopTimeout)
            {
                Debug.LogWarning($"[Bot] {dragController.gameObject.name} timeout — coin durduruluyor");
                dragController.ForceStopSliding();
                break;
            }

            yield return null;
        }

        pathSamples.Add(dragController.transform.position);
        yield return new WaitForSeconds(0.1f);
        pathSamples.Add(dragController.transform.position);

        if (shotCoin != null)
        {
            TryCaptureBotGoalOverlap(shotCoin);
        }
    }

    void TryCaptureBotGoalOverlap(CoinIdentity coin)
    {
        if (coin == null || !_goalEligibleThisShot || _scoredGoalThisShot)
        {
            return;
        }

        if (!IsOverlappingPlayerGoalArea(coin))
        {
            return;
        }

        _scoredGoalThisShot = true;
        Debug.Log($"GoalArea: Entered (capture) | {coin.gameObject.name}");
        GameRulesManager.Instance?.PreviewFreezeForPossibleGoal();
        coin.DragController.ForceStopSliding();
    }

    IEnumerator RollbackCoin(CoinIdentity coin, Vector3 targetPosition)
    {
        if (GameRulesManager.Instance == null)
        {
            yield break;
        }

        yield return GameRulesManager.Instance.AnimateCoinToPosition(coin, targetPosition, _rollbackDuration);
    }

    static bool IsOverlappingPlayerGoalArea(CoinIdentity coin, bool useVisualMesh = true)
    {
        if (coin == null)
        {
            return false;
        }

        GoalZone zone = GoalZone.FindPlayerGoalArea();
        return zone != null && zone.OverlapsCoin(coin, useVisualMesh);
    }

    void SetCoinPassive(CoinIdentity coin, bool passive)
    {
        coin.SetPassive(passive);
    }

    void InvokeOpponentGoalScoredSafely()
    {
        if (OpponentGoalScored == null)
        {
            return;
        }

        foreach (Action handler in OpponentGoalScored.GetInvocationList())
        {
            try
            {
                handler();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }
}

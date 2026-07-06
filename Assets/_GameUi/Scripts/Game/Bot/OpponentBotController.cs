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

    [Header("Oyun Kuralları")]
    [SerializeField] OpponentBotDifficulty _difficulty = new() { Level = 7 };
    [SerializeField] float _gateMargin        = 0.09f;
    [SerializeField] float _rollbackDuration  = 0.45f;
    [SerializeField] float _coinStopTimeout   = 8f;
    [SerializeField] float _coinBlockRadius   = 0.07f;   // yol engeli tespiti için coin yarıçapı

    readonly TeamRoundState _state = new();
    readonly List<Vector3> _pathSamples = new(64);

    CoinIdentity _resolvingCoin;
    Vector3 _shotStartPosition;
    bool _goalEnteredDuringShot;
    bool _isResolving;
    bool _isOpeningShot;
    int  _roundShotNumber = 1;   // bu turdaki atış sırası (1, 2, 3, 4+)
    Coroutine _playLoopRoutine;
    BotLevelUpBoostPolicy.AiConfig _aiConfig;

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
        SyncAiStrength();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        SyncAiStrength();
    }
#endif

    public void ApplySessionOpponentDifficulty()
    {
        SyncAiStrength(logSessionEvaluation: true);
    }

    void SyncAiStrength(bool logSessionEvaluation = false, int shotNumber = 0)
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

            if (logSessionEvaluation || shotNumber > 0)
            {
                BotLevelUpBoostPolicy.LogAiConfigInspector(_aiStrength, GetTurnThinkDelay());
            }

            return;
        }

        _aiConfig = BotLevelUpBoostPolicy.Evaluate(_useInspectorTurnDelay, _turnThinkDelaySeconds);
        _aiStrength = _aiConfig.AppliedStrength;
        _difficulty.Level = _aiStrength;

        if (!Application.isPlaying)
        {
            return;
        }

        if (logSessionEvaluation || shotNumber > 0)
        {
            BotLevelUpBoostPolicy.LogAiConfig(_aiStrength, GetTurnThinkDelay(), _aiConfig, shotNumber);
        }
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
            return BotLevelUpBoostPolicy.MilestoneThinkDelaySeconds;
        }

        return BotTurnThinkDelay.GetDelayForCurrentPlayer();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void ResetRoundState()
    {
        StopPlayLoop();
        ClearResolvingState();
        _roundShotNumber = 1;
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

        _goalEnteredDuringShot = true;
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

        if (GameRulesManager.Instance != null
            && (GameRulesManager.Instance.IsGoalSequenceActive
                || GameRulesManager.Instance.IsResolvingMove))
        {
            return;
        }

        BeginPlayLoop();
    }

    void ClearResolvingState()
    {
        _resolvingCoin = null;
        _isResolving = false;
        _goalEnteredDuringShot = false;
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

            SyncAiStrength(shotNumber: _roundShotNumber);

            yield return new WaitForSeconds(GetTurnThinkDelay());

            if (!OpponentBotBrain.TryChooseShot(
                    _state, _difficulty, _isResolving, _gateMargin,
                    _roundShotNumber, _coinBlockRadius,
                    out OpponentBotBrain.ShotPlan plan,
                    out bool pathBlocked))
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

            _resolvingCoin          = plan.Coin;
            _goalEnteredDuringShot  = false;
            _isOpeningShot          = _roundShotNumber == 1;   // yalnızca 1. atış gate validation'dan muaf
            _shotStartPosition      = plan.Coin.transform.position;

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
        bool isOpeningShot = _isOpeningShot;

        _pathSamples.Clear();
        yield return WaitUntilCoinStops(coin.DragController, coin, _pathSamples);

        bool requiresGateValidation = !isOpeningShot;
        bool goalTriggered = _goalEnteredDuringShot || IsCoinInPlayerGoal(coin);

        if (requiresGateValidation && _state.LastCommittedShotCoin == coin)
        {
            yield return RollbackInvalidBotShot(coin, "aynı coin üst üste atılamaz");
            yield break;
        }

        if (goalTriggered)
        {
            if (requiresGateValidation
                && !TeamRulesService.ValidatePassBetween(_state, coin, _pathSamples, _gateMargin))
            {
                yield return RollbackInvalidBotShot(coin, "kapıdan geçmeden gol");
                yield break;
            }

            Debug.Log($"[Bot] {coin.gameObject.name} GOL");
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
            TeamRulesService.TryGetGateCoins(_state, coin, out CoinIdentity dbgA, out CoinIdentity dbgB);
            Debug.Log($"[Bot] Validasyon | atar={coin.name} " +
                      $"gateA={dbgA?.name}@{(dbgA != null ? dbgA.transform.position.ToString("F2") : "null")} " +
                      $"gateB={dbgB?.name}@{(dbgB != null ? dbgB.transform.position.ToString("F2") : "null")} " +
                      $"pathSamples={_pathSamples.Count}");

            bool passedBetween = TeamRulesService.ValidatePassBetween(_state, coin, _pathSamples, _gateMargin);
            if (!passedBetween)
            {
                Debug.Log($"[Bot] {coin.gameObject.name} GEÇERSİZ — kapıdan geçemedi | " +
                          $"son pozisyon={coin.transform.position:F2}");
                GameRulesManager.Instance?.CancelPendingGoalPreview();
                InvalidMoveRollbackStarted?.Invoke(CoinTeam.Opponent);
                yield return RollbackCoin(coin, _shotStartPosition);
                pendingInvalidRollbackFinished = true;
                shotValid = false;
            }
            else
            {
                shotValid = true;
                Debug.Log($"[Bot] {coin.gameObject.name} geçerli — kapıdan geçti");
            }
        }

        if (shotValid)
        {
            _state.LastCommittedShotCoin = coin;

            if (!isOpeningShot)
            {
                ValidShotCommitted?.Invoke(CoinTeam.Opponent);
            }

            _roundShotNumber++;
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

    IEnumerator RollbackInvalidBotShot(CoinIdentity coin, string reason)
    {
        Debug.Log($"[Bot] {coin.gameObject.name} GEÇERSİZ — {reason}");
        GameRulesManager.Instance?.CancelPendingGoalPreview();
        InvalidMoveRollbackStarted?.Invoke(CoinTeam.Opponent);
        yield return RollbackCoin(coin, _shotStartPosition);

        _resolvingCoin = null;
        _isResolving = false;
        InvalidMoveRollbackFinished?.Invoke(CoinTeam.Opponent);
        TeamRulesService.UnlockAllCoins(_state, SetCoinPassive);
        TeamRulesService.EnsureAtLeastOneSelectable(_state, SetCoinPassive);
    }

    IEnumerator WaitUntilCoinStops(CoinDragController dragController, CoinIdentity shotCoin, List<Vector3> pathSamples)
    {
        pathSamples.Clear();
        pathSamples.Add(dragController.transform.position);

        float elapsed = 0f;
        bool everSlid = false;
        while (true)
        {
            yield return null;
            pathSamples.Add(dragController.transform.position);

            if (shotCoin != null && (_goalEnteredDuringShot || IsCoinInPlayerGoal(shotCoin)))
            {
                GameRulesManager.Instance?.PreviewFreezeForPossibleGoal();
                dragController.ForceStopSliding();
                break;
            }

            if (dragController.IsSliding)
            {
                everSlid = true;
                elapsed += Time.deltaTime;
                if (elapsed >= _coinStopTimeout)
                {
                    Debug.LogWarning($"[Bot] {dragController.gameObject.name} timeout — coin durduruluyor");
                    dragController.ForceStopSliding();
                    break;
                }
            }
            else if (everSlid)
            {
                break;
            }
            else if (elapsed > 0.3f)
            {
                break;
            }
            else
            {
                elapsed += Time.deltaTime;
            }
        }

        pathSamples.Add(dragController.transform.position);
        yield return new WaitForSeconds(0.1f);
        pathSamples.Add(dragController.transform.position);
    }

    IEnumerator RollbackCoin(CoinIdentity coin, Vector3 targetPosition)
    {
        if (GameRulesManager.Instance == null)
        {
            yield break;
        }

        yield return GameRulesManager.Instance.AnimateCoinToPosition(coin, targetPosition, _rollbackDuration);
    }

    static bool IsCoinInPlayerGoal(CoinIdentity coin)
    {
        GoalZone[] zones = FindObjectsByType<GoalZone>(FindObjectsSortMode.None);
        for (int i = 0; i < zones.Length; i++)
        {
            GoalZone zone = zones[i];
            if (zone.transform.parent != null
                && zone.transform.parent.name.Contains("_P")
                && (zone.IsCoinInside(coin) || zone.ContainsWorldPosition(coin.transform.position)))
            {
                return true;
            }
        }

        return false;
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

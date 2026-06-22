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

    [SerializeField] OpponentBotDifficulty _difficulty = new() { Level = 5 };
    [SerializeField] float _gateMargin = 0.09f;
    [SerializeField] float _rollbackDuration = 0.45f;
    [SerializeField] float _interShotDelay = 0.35f;

    readonly TeamRoundState _state = new();
    readonly List<Vector3> _pathSamples = new(64);

    CoinIdentity _resolvingCoin;
    Vector3 _shotStartPosition;
    bool _goalEnteredDuringShot;
    bool _isResolving;
    bool _isOpeningShot;
    Coroutine _playLoopRoutine;

    public event Action OpponentGoalScored;

    public bool IsResolving => _isResolving;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
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

    IEnumerator PlayLoopRoutine()
    {
        while (true)
        {
            while (_isResolving)
            {
                yield return null;
            }

            yield return new WaitForSeconds(_interShotDelay);

            if (!OpponentBotBrain.TryChooseShot(_state, _difficulty, _isResolving, _gateMargin, out OpponentBotBrain.ShotPlan plan))
            {
                continue;
            }

            if (!CoinShotLauncher.TryLaunch(plan.Coin.DragController, plan.Direction, plan.PullDistance)
                || !plan.Coin.DragController.IsSliding)
            {
                continue;
            }

            Debug.Log($"[Bot] {plan.Coin.gameObject.name} fırlatıldı | kuralUyumu={plan.RespectsRules}");

            _resolvingCoin = plan.Coin;
            _goalEnteredDuringShot = false;
            _isOpeningShot = _state.IsFirstMove;
            _shotStartPosition = plan.Coin.transform.position;
            TeamRulesService.LockCoinUntilOthersMoved(_state, plan.Coin, SetCoinPassive);

            yield return ResolveShotRoutine(plan.Coin);
        }
    }

    void PrepareOpeningTurn()
    {
        for (int i = 0; i < _state.Coins.Count; i++)
        {
            SetCoinPassive(_state.Coins[i], false);
        }

        TeamRulesService.ApplyOpeningRestrictions(_state, SetCoinPassive);
    }

    IEnumerator ResolveShotRoutine(CoinIdentity coin)
    {
        _isResolving = true;
        bool isOpeningShot = _isOpeningShot;

        _pathSamples.Clear();
        yield return WaitUntilCoinStops(coin.DragController, _pathSamples);

        bool shotValid;
        if (isOpeningShot)
        {
            _state.IsFirstMove = false;
            shotValid = true;
            TeamRulesService.RegisterSuccessfulShot(_state, coin, SetCoinPassive);
            TeamRulesService.UnlockOpeningSideCoins(_state, SetCoinPassive);
            Debug.Log($"[Bot] {coin.gameObject.name} açılış hamlesi geçerli");
        }
        else
        {
            bool passedBetween = TeamRulesService.ValidatePassBetween(_state, coin, _pathSamples, _gateMargin);
            if (!passedBetween)
            {
                Debug.Log($"[Bot] {coin.gameObject.name} geçersiz — kapıdan geçmedi");
                TeamRulesService.UnlockCoin(_state, coin, SetCoinPassive);
                yield return RollbackCoin(coin, _shotStartPosition);
                shotValid = false;
            }
            else
            {
                TeamRulesService.RegisterSuccessfulShot(_state, coin, SetCoinPassive);
                shotValid = true;
                Debug.Log($"[Bot] {coin.gameObject.name} geçerli — kapıdan geçti");
            }
        }

        bool inGoal = IsCoinInPlayerGoal(coin);
        if (shotValid && (_goalEnteredDuringShot || inGoal))
        {
            Debug.Log($"[Bot] {coin.gameObject.name} GOL");
            OpponentGoalScored?.Invoke();
            _resolvingCoin = null;
            _isResolving = false;
            StopPlayLoop();
            if (GameRulesManager.Instance != null)
            {
                GameRulesManager.Instance.RequestRoundReset();
            }

            yield break;
        }

        _resolvingCoin = null;
        _isResolving = false;
    }

    IEnumerator WaitUntilCoinStops(CoinDragController coin, List<Vector3> pathSamples)
    {
        pathSamples.Clear();
        pathSamples.Add(coin.transform.position);

        yield return new WaitForSeconds(0.05f);

        while (coin.IsSliding)
        {
            pathSamples.Add(coin.transform.position);
            yield return null;
        }

        pathSamples.Add(coin.transform.position);
        yield return new WaitForSeconds(0.1f);
        pathSamples.Add(coin.transform.position);
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

        CoinVisualState visualState = coin.GetComponent<CoinVisualState>();
        if (visualState != null)
        {
            visualState.SetPassiveVisual(passive);
        }
    }
}

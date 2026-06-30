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

    public event Action OpponentGoalScored;

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
    }

    void Start()
    {
        ApplySessionOpponentDifficulty();
    }

    public void ApplySessionOpponentDifficulty()
    {
        if (!MatchSessionContext.HasOpponent)
        {
            return;
        }

        _difficulty.Level = Mathf.Clamp(MatchSessionContext.CurrentOpponent.difficultyLevel, 1, 10);
        Debug.Log(
            $"[Bot] Ayarlar uygulandı | Rakip={OpponentDisplayName} | Zorluk={_difficulty.Level} | " +
            $"AimNoise={_difficulty.AimNoiseDegrees:F1} | RuleCompliance={_difficulty.RuleCompliance:F2}");
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
    }

    IEnumerator PlayLoopRoutine()
    {
        while (true)
        {
            while (_isResolving) yield return null;

            yield return new WaitForSeconds(_difficulty.ThinkDelaySeconds);

            if (!OpponentBotBrain.TryChooseShot(
                    _state, _difficulty, _isResolving, _gateMargin,
                    _roundShotNumber, _coinBlockRadius,
                    out OpponentBotBrain.ShotPlan plan,
                    out bool pathBlocked))
            {
                // Yol doluysa 5 sn, değilse 0.5 sn bekle
                yield return new WaitForSeconds(pathBlocked ? 5f : 0.5f);
                continue;
            }

            if (!CoinShotLauncher.TryLaunch(plan.Coin.DragController, plan.Direction, plan.PullDistance)
                || !plan.Coin.DragController.IsSliding)
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            Debug.Log($"[Bot] {plan.Coin.name} fırlatıldı | atış#{_roundShotNumber}");

            _resolvingCoin          = plan.Coin;
            _goalEnteredDuringShot  = false;
            _isOpeningShot          = _roundShotNumber <= 2;   // 1. ve 2. atış gate validation'dan muaf
            _shotStartPosition      = plan.Coin.transform.position;
            TeamRulesService.LockCoinUntilOthersMoved(_state, plan.Coin, SetCoinPassive);

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

        if (shotValid) _roundShotNumber++;

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

        // Her frame'de sample al — 0.05s gap yerine anlık başla
        float elapsed = 0f;
        bool everSlid = false;
        while (true)
        {
            yield return null;
            pathSamples.Add(coin.transform.position);

            if (coin.IsSliding)
            {
                everSlid = true;
                elapsed += Time.deltaTime;
                if (elapsed >= _coinStopTimeout)
                {
                    Debug.LogWarning($"[Bot] {coin.gameObject.name} timeout — coin durduruluyor");
                    break;
                }
            }
            else if (everSlid)
            {
                // Hareket etti ve durdu
                break;
            }
            else if (elapsed > 0.3f)
            {
                // Hiç kaymadı, 0.3s içinde başlamazsa vazgeç
                break;
            }
            else
            {
                elapsed += Time.deltaTime;
            }
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

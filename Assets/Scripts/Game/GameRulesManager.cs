using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameRulesManager : MonoBehaviour
{
    public static GameRulesManager Instance { get; private set; }

    [SerializeField] float _gateMargin = 0.09f;
    [SerializeField] float _rollbackDuration = 0.45f;
    [SerializeField] float _goalResetDuration = 0.55f;

    readonly List<CoinIdentity> _playerCoins = new(3);
    readonly List<CoinIdentity> _roundCoins = new(6);
    readonly List<Vector3> _shotPathSamples = new(64);
    readonly Dictionary<CoinIdentity, Vector3> _initialPositions = new();
    readonly Dictionary<CoinIdentity, HashSet<CoinIdentity>> _waitingForOthers = new();

    CoinIdentity _shotCoin;
    CoinIdentity _resolvingShotCoin;
    CoinIdentity _openingCoin;
    Vector3 _shotStartPosition;
    bool _goalEnteredDuringShot;
    bool _isResolvingMove;
    bool _isFirstPlayerMove = true;
    bool _isOpeningShot;
    Coroutine _resolveRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSingleton()
    {
        Instance = null;
    }

    public bool IsResolvingMove => _isResolvingMove;

    /// <summary>Oyuncu hamlesi bittiğinde (gol reset hariç) tetiklenir.</summary>
    public event Action<CoinTeam> MoveResolved;

    /// <summary>Tüm coinler başlangıca döndüğünde tetiklenir.</summary>
    public event Action RoundReset;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BeginNewRound();
        DiscoverPlayerCoins();
    }

    void Start()
    {
        if (_initialPositions.Count == 0)
        {
            CacheInitialPositions();
        }

        DiscoverPlayerCoins();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void BeginNewRound()
    {
        _isFirstPlayerMove = true;
        _isOpeningShot = false;
        _waitingForOthers.Clear();
        _shotCoin = null;
        _resolvingShotCoin = null;
        _goalEnteredDuringShot = false;
    }

    void DiscoverPlayerCoins()
    {
        _playerCoins.Clear();

        CoinIdentity[] coins = FindObjectsByType<CoinIdentity>(FindObjectsSortMode.None);
        for (int i = 0; i < coins.Length; i++)
        {
            CoinIdentity coin = coins[i];
            if (coin.Team == CoinTeam.Player && IsPlayablePlayerCoin(coin))
            {
                _playerCoins.Add(coin);
            }
        }

        ResolveOpeningCoin();

        if (_isFirstPlayerMove)
        {
            for (int i = 0; i < _playerCoins.Count; i++)
            {
                SetCoinPassiveState(_playerCoins[i], false);
            }

            ApplyOpeningRestrictions();
        }
    }

    void CacheInitialPositions()
    {
        _roundCoins.Clear();
        _initialPositions.Clear();

        CoinIdentity[] coins = FindObjectsByType<CoinIdentity>(FindObjectsSortMode.None);
        for (int i = 0; i < coins.Length; i++)
        {
            CoinIdentity coin = coins[i];
            if (!IsRoundCoin(coin))
            {
                continue;
            }

            _roundCoins.Add(coin);
            _initialPositions[coin] = coin.transform.position;
        }
    }

    void ApplyOpeningRestrictions()
    {
        if (_openingCoin == null)
        {
            return;
        }

        SetCoinPassiveState(_openingCoin, false);

        for (int i = 0; i < _playerCoins.Count; i++)
        {
            CoinIdentity coin = _playerCoins[i];
            if (coin == _openingCoin)
            {
                continue;
            }

            SetCoinPassiveState(coin, true);
        }
    }

    void UnlockOpeningSideCoins()
    {
        if (_openingCoin == null)
        {
            return;
        }

        for (int i = 0; i < _playerCoins.Count; i++)
        {
            CoinIdentity coin = _playerCoins[i];
            if (coin == _openingCoin || _waitingForOthers.ContainsKey(coin))
            {
                continue;
            }

            SetCoinPassiveState(coin, false);
        }
    }

    static bool IsPlayablePlayerCoin(CoinIdentity coin)
    {
        return coin != null && coin.gameObject.name.Contains("_P");
    }

    static bool IsRoundCoin(CoinIdentity coin)
    {
        if (coin == null)
        {
            return false;
        }

        string objectName = coin.gameObject.name;
        return objectName.Contains("_P") || objectName.Contains("_E");
    }

    void ResolveOpeningCoin()
    {
        _openingCoin = null;

        if (_playerCoins.Count == 0)
        {
            return;
        }

        if (_playerCoins.Count == 1)
        {
            _openingCoin = _playerCoins[0];
            return;
        }

        _playerCoins.Sort(CompareCoinHorizontalPosition);
        _openingCoin = _playerCoins[_playerCoins.Count / 2];
    }

    static int CompareCoinHorizontalPosition(CoinIdentity a, CoinIdentity b)
    {
        float delta = a.transform.position.x - b.transform.position.x;
        if (Mathf.Abs(delta) > 0.001f)
        {
            return delta < 0f ? -1 : 1;
        }

        delta = a.transform.position.z - b.transform.position.z;
        return delta < 0f ? -1 : delta > 0f ? 1 : 0;
    }

    public bool CanSelectCoin(CoinIdentity coin)
    {
        if (_isResolvingMove || coin == null)
        {
            return false;
        }

        if (!coin.CanBeSelectedByPlayer())
        {
            return false;
        }

        if (coin.DragController.IsSliding || coin.DragController.IsAiming)
        {
            return false;
        }

        if (_isFirstPlayerMove && _openingCoin != null && coin != _openingCoin)
        {
            return false;
        }

        return true;
    }

    public void OnShotReleased(CoinIdentity coin)
    {
        if (coin == null || coin.Team != CoinTeam.Player)
        {
            return;
        }

        if (!coin.DragController.IsSliding)
        {
            return;
        }

        if (_isFirstPlayerMove && _openingCoin != null && coin != _openingCoin)
        {
            return;
        }

        _shotCoin = coin;
        _resolvingShotCoin = coin;
        _goalEnteredDuringShot = false;
        _isOpeningShot = _isFirstPlayerMove;
        _shotStartPosition = coin.transform.position;
        LockCoinUntilOthersMoved(coin);

        Debug.Log(
            $"[Shot] {coin.gameObject.name} fırlatıldı | açılış={_isOpeningShot} | ilkHamle={_isFirstPlayerMove} | pos={_shotStartPosition}");

        if (_resolveRoutine != null)
        {
            StopCoroutine(_resolveRoutine);
        }

        _resolveRoutine = StartCoroutine(ResolveShotRoutine(coin));
    }

    public void NotifyCoinEnteredGoal(CoinIdentity coin)
    {
        if (!_isResolvingMove || coin == null || coin != _resolvingShotCoin)
        {
            return;
        }

        _goalEnteredDuringShot = true;
    }

    IEnumerator ResolveShotRoutine(CoinIdentity coin)
    {
        _isResolvingMove = true;
        bool isOpeningShot = _isOpeningShot;

        _shotPathSamples.Clear();
        yield return WaitUntilCoinStops(coin.DragController, _shotPathSamples);

        bool shotValid;
        if (isOpeningShot)
        {
            _isFirstPlayerMove = false;
            _isOpeningShot = false;
            shotValid = true;
            RegisterSuccessfulShot(coin);
            UnlockOpeningSideCoins();
            Debug.Log($"[Shot] {coin.gameObject.name} açılış hamlesi geçerli");
        }
        else
        {
            bool passedBetween = ValidatePassBetween(coin, _shotPathSamples);
            if (!passedBetween)
            {
                Debug.Log($"[Shot] {coin.gameObject.name} geçersiz — diğer iki coin arasından geçmedi");
                UnlockCoin(coin);
                yield return RollbackCoin(coin, _shotStartPosition);
                shotValid = false;
            }
            else
            {
                RegisterSuccessfulShot(coin);
                shotValid = true;
                Debug.Log($"[Shot] {coin.gameObject.name} geçerli — kapıdan geçti");
            }
        }

        bool inGoal = IsCoinInOpponentGoal(coin);
        if (shotValid && (_goalEnteredDuringShot || inGoal))
        {
            Debug.Log($"[Shot] {coin.gameObject.name} GOL | trigger={_goalEnteredDuringShot} | içerde={inGoal}");
            yield return ResetRoundAfterGoalRoutine();
            _resolvingShotCoin = null;
            _resolveRoutine = null;
            yield break;
        }

        _resolvingShotCoin = null;
        _shotCoin = null;
        _isResolvingMove = false;
        _resolveRoutine = null;
        MoveResolved?.Invoke(CoinTeam.Player);
    }

    static bool IsCoinInOpponentGoal(CoinIdentity coin)
    {
        GoalZone[] goalZones = FindObjectsByType<GoalZone>(FindObjectsSortMode.None);
        for (int i = 0; i < goalZones.Length; i++)
        {
            GoalZone goalZone = goalZones[i];
            if (goalZone.IsOpponentGoal && (goalZone.IsCoinInside(coin) || goalZone.ContainsWorldPosition(coin.transform.position)))
            {
                return true;
            }
        }

        return false;
    }

    void RegisterSuccessfulShot(CoinIdentity movedCoin)
    {
        for (int i = 0; i < _playerCoins.Count; i++)
        {
            CoinIdentity waitingCoin = _playerCoins[i];
            if (waitingCoin == movedCoin || !waitingCoin.IsPassive)
            {
                continue;
            }

            if (!_waitingForOthers.TryGetValue(waitingCoin, out HashSet<CoinIdentity> movedOthers))
            {
                continue;
            }

            movedOthers.Add(movedCoin);
            if (movedOthers.Count >= 2)
            {
                UnlockCoin(waitingCoin);
            }
        }
    }

    void LockCoinUntilOthersMoved(CoinIdentity coin)
    {
        _waitingForOthers[coin] = new HashSet<CoinIdentity>();
        SetCoinPassiveState(coin, true);
    }

    void UnlockCoin(CoinIdentity coin)
    {
        _waitingForOthers.Remove(coin);
        SetCoinPassiveState(coin, false);
    }

    void SetCoinPassiveState(CoinIdentity coin, bool passive)
    {
        coin.SetPassive(passive);

        CoinVisualState visualState = coin.GetComponent<CoinVisualState>();
        if (visualState != null)
        {
            visualState.SetPassiveVisual(passive);
        }
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

    bool ValidatePassBetween(CoinIdentity movingCoin, IReadOnlyList<Vector3> pathSamples)
    {
        if (!TryGetGateCoins(movingCoin, out CoinIdentity gateCoinA, out CoinIdentity gateCoinB))
        {
            return false;
        }

        Vector3 gateA = gateCoinA.transform.position;
        Vector3 gateB = gateCoinB.transform.position;

        return PassBetweenValidator.DidPassBetweenAlongPath(
            pathSamples,
            gateA,
            gateB,
            _gateMargin);
    }

    bool TryGetGateCoins(CoinIdentity movingCoin, out CoinIdentity gateA, out CoinIdentity gateB)
    {
        gateA = null;
        gateB = null;

        int found = 0;
        for (int i = 0; i < _playerCoins.Count; i++)
        {
            CoinIdentity coin = _playerCoins[i];
            if (coin == movingCoin)
            {
                continue;
            }

            if (found == 0)
            {
                gateA = coin;
                found++;
            }
            else
            {
                gateB = coin;
                return true;
            }
        }

        return false;
    }

    IEnumerator RollbackCoin(CoinIdentity coin, Vector3 targetPosition)
    {
        yield return AnimateCoinsToPositions(
            new[] { coin },
            new[] { targetPosition },
            _rollbackDuration);
    }

    IEnumerator ResetRoundAfterGoalRoutine()
    {
        _isResolvingMove = true;
        _shotCoin = null;

        if (_initialPositions.Count == 0)
        {
            CacheInitialPositions();
        }

        var targetPositions = new Vector3[_roundCoins.Count];
        for (int i = 0; i < _roundCoins.Count; i++)
        {
            CoinIdentity coin = _roundCoins[i];
            if (_initialPositions.TryGetValue(coin, out Vector3 initialPosition))
            {
                targetPositions[i] = initialPosition;
            }
            else
            {
                targetPositions[i] = coin.transform.position;
            }
        }

        yield return AnimateCoinsToPositions(_roundCoins, targetPositions, _goalResetDuration);

        BeginNewRound();
        DiscoverPlayerCoins();

        _isResolvingMove = false;
        RoundReset?.Invoke();
    }

    public void RequestRoundReset()
    {
        if (_resolveRoutine != null)
        {
            StopCoroutine(_resolveRoutine);
            _resolveRoutine = null;
        }

        StartCoroutine(ResetRoundAfterGoalRoutine());
    }

    public IEnumerator AnimateCoinToPosition(CoinIdentity coin, Vector3 targetPosition, float duration)
    {
        yield return AnimateCoinsToPositions(
            new[] { coin },
            new[] { targetPosition },
            duration);
    }

    IEnumerator AnimateCoinsToPositions(
        IReadOnlyList<CoinIdentity> coins,
        IReadOnlyList<Vector3> targetPositions,
        float duration)
    {
        if (coins == null || targetPositions == null || coins.Count == 0 || coins.Count != targetPositions.Count)
        {
            yield break;
        }

        var rigidbodies = new Rigidbody[coins.Count];
        var startPositions = new Vector3[coins.Count];

        for (int i = 0; i < coins.Count; i++)
        {
            CoinIdentity coin = coins[i];
            CoinDragController dragController = coin.DragController;
            Rigidbody rigidbody = dragController.GetComponent<Rigidbody>();

            if (dragController.IsAiming)
            {
                dragController.CancelAim();
            }

            if (!rigidbody.isKinematic)
            {
                rigidbody.linearVelocity = Vector3.zero;
                rigidbody.angularVelocity = Vector3.zero;
            }

            rigidbody.isKinematic = true;

            rigidbodies[i] = rigidbody;
            startPositions[i] = rigidbody.position;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            for (int i = 0; i < coins.Count; i++)
            {
                Vector3 nextPosition = Vector3.Lerp(startPositions[i], targetPositions[i], t);
                nextPosition.y = targetPositions[i].y;
                rigidbodies[i].position = nextPosition;
            }

            yield return null;
        }

        for (int i = 0; i < coins.Count; i++)
        {
            Vector3 finalPosition = targetPositions[i];
            finalPosition.y = targetPositions[i].y;
            rigidbodies[i].position = finalPosition;
            rigidbodies[i].isKinematic = false;
        }
    }
}

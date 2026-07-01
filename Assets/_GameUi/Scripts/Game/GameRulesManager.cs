using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameRulesManager : MonoBehaviour
{
    public static GameRulesManager Instance { get; private set; }

    /// <summary>Yalnızca 1. atış (orta para) gate kuralından muaftır.</summary>
    public const int OpeningShotCount = 1;

    [SerializeField] float _gateMargin = 0.09f;
    [SerializeField] float _rollbackDuration = 0.45f;
    [SerializeField] float _goalResetDuration = 0.55f;
    [SerializeField] float _coinStopTimeout = 8f;

    readonly List<CoinIdentity> _playerCoins = new(3);
    readonly List<CoinIdentity> _roundCoins = new(6);
    readonly List<Vector3> _shotPathSamples = new(64);
    readonly Dictionary<CoinIdentity, Vector3> _initialPositions = new();

    CoinIdentity _shotCoin;
    CoinIdentity _resolvingShotCoin;
    CoinIdentity _openingCoin;
    Vector3 _shotStartPosition;
    bool _goalEnteredDuringShot;
    bool _isResolvingMove;
    bool _isFirstPlayerMove = true;
    bool _isOpeningShot;
    int  _playerShotNumber = 1;   // bu turdaki atış sırası; yalnızca 1. atış gate validation'dan muaf
    Coroutine _resolveRoutine;
    Coroutine _roundResetRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSingleton()
    {
        Instance = null;
    }

    public bool IsResolvingMove => _isResolvingMove;
    public int PlayerShotNumber => _playerShotNumber;

    /// <summary>Sıradaki atış gate kuralına tabi mi (2. atış ve sonrası).</summary>
    public bool RequiresGateValidationForNextShot => _playerShotNumber > OpeningShotCount;

    /// <summary>GateIndicator 2. atıştan itibaren gösterilir.</summary>
    public bool ShouldShowGateIndicatorForNextShot => RequiresGateValidationForNextShot;

    public static bool IsOpeningShotNumber(int shotNumber) => shotNumber <= OpeningShotCount;

    public bool TryGetGateCoins(CoinIdentity shooter, out CoinIdentity gateA, out CoinIdentity gateB)
    {
        return TryGetGateCoinsInternal(shooter, out gateA, out gateB);
    }

    /// <summary>Oyuncu hamlesi bittiğinde (gol reset hariç) tetiklenir.</summary>
    public event Action<CoinTeam> MoveResolved;

    /// <summary>Tüm coinler başlangıca döndüğünde tetiklenir.</summary>
    public event Action RoundReset;

    /// <summary>Oyuncu gol attığında tetiklenir.</summary>
    public event Action PlayerGoalScored;

    /// <summary>Oyuncu atışı çözüldüğünde tetiklenir (gol değilse).</summary>
    public event Action<CoinIdentity, bool> PlayerShotResolved;

    /// <summary>Geçersiz hamle rollback animasyonu başladığında.</summary>
    public event Action<CoinTeam> InvalidMoveRollbackStarted;

    /// <summary>Geçersiz hamle rollback animasyonu bittiğinde.</summary>
    public event Action<CoinTeam> InvalidMoveRollbackFinished;

    /// <summary>Gate kuralına uyan geçerli atış tamamlandığında.</summary>
    public event Action<CoinTeam> ValidShotCommitted;

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

        if (GetComponent<GateIndicator>() == null)
        {
            gameObject.AddComponent<GateIndicator>();
        }

        if (GetComponent<GameFeedback>() == null)
        {
            gameObject.AddComponent<GameFeedback>();
        }

    }

    void Start()
    {
        if (_initialPositions.Count == 0)
        {
            CacheInitialPositions();
        }

        DiscoverPlayerCoins();
    }

    /// <summary>Yeni maç / sahne yüklemesinde kuralları ve coin referanslarını sıfırlar.</summary>
    public void PrepareForNewMatch()
    {
        if (_roundResetRoutine != null)
        {
            StopCoroutine(_roundResetRoutine);
            _roundResetRoutine = null;
        }

        CancelActiveShotResolution();
        BeginNewRound();
        CacheInitialPositions();
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
        _isResolvingMove = false;
        _playerShotNumber = 1;
        _shotCoin = null;
        _resolvingShotCoin = null;
        _goalEnteredDuringShot = false;
        GateIndicator.Instance?.Hide();
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
        ReconcilePlayerCoinPassiveState();
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
            if (coin == _openingCoin)
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

        if (_resolveRoutine != null)
        {
            CancelActiveShotResolution();
        }

        _shotCoin = coin;
        _resolvingShotCoin = coin;
        _goalEnteredDuringShot = false;
        _isOpeningShot = IsOpeningShotNumber(_playerShotNumber);
        _isResolvingMove = true;
        _shotStartPosition = coin.transform.position;

        Debug.Log(
            $"[Shot] {coin.gameObject.name} fırlatıldı | açılış={_isOpeningShot} | ilkHamle={_isFirstPlayerMove} | pos={_shotStartPosition}");

        _resolveRoutine = StartCoroutine(ResolveShotRoutine(coin));
    }

    public void NotifyCoinEnteredGoal(CoinIdentity coin)
    {
        if (coin == null || coin.Team != CoinTeam.Player)
        {
            return;
        }

        if (coin != _resolvingShotCoin && coin != _shotCoin)
        {
            return;
        }

        _goalEnteredDuringShot = true;
    }

    void CancelActiveShotResolution()
    {
        if (_resolveRoutine != null)
        {
            StopCoroutine(_resolveRoutine);
            _resolveRoutine = null;
        }

        _shotCoin = null;
        _resolvingShotCoin = null;
        _goalEnteredDuringShot = false;
        _isResolvingMove = false;
        _isOpeningShot = false;
        GateIndicator.Instance?.Hide();
    }

    IEnumerator ResolveShotRoutine(CoinIdentity coin)
    {
        bool isOpeningShot = _isOpeningShot;

        _shotPathSamples.Clear();
        yield return WaitUntilCoinStops(coin.DragController, coin, _shotPathSamples);

        bool shotValid;
        bool pendingInvalidRollbackFinished = false;
        if (isOpeningShot)
        {
            _isFirstPlayerMove = false;
            _isOpeningShot = false;
            shotValid = true;
            UnlockOpeningSideCoins();
            Debug.Log($"[Shot] {coin.gameObject.name} açılış #{_playerShotNumber} geçerli");
        }
        else
        {
            bool passedBetween = ValidatePassBetween(coin, _shotPathSamples);
            if (!passedBetween)
            {
                Debug.Log($"[Shot] {coin.gameObject.name} geçersiz — diğer iki coin arasından geçmedi");
                InvalidMoveRollbackStarted?.Invoke(CoinTeam.Player);
                yield return RollbackCoin(coin, _shotStartPosition);
                pendingInvalidRollbackFinished = true;
                shotValid = false;
            }
            else
            {
                shotValid = true;
                Debug.Log($"[Shot] {coin.gameObject.name} geçerli — kapıdan geçti");
            }
        }

        bool inGoal = IsCoinInOpponentGoal(coin);
        bool scoredGoal = shotValid && (_goalEnteredDuringShot || inGoal);

        if (shotValid && !scoredGoal)
        {
            if (!isOpeningShot)
            {
                ValidShotCommitted?.Invoke(CoinTeam.Player);
            }

            _playerShotNumber++;
        }

        if (scoredGoal)
        {
            Debug.Log($"[Shot] {coin.gameObject.name} GOL | trigger={_goalEnteredDuringShot} | içerde={inGoal}");
            HandlePlayerGoalCelebration();
            _resolvingShotCoin = null;
            _resolveRoutine = null;
            yield break;
        }

        _resolvingShotCoin = null;
        _shotCoin = null;
        _isResolvingMove = false;
        _resolveRoutine = null;

        if (pendingInvalidRollbackFinished)
        {
            InvalidMoveRollbackFinished?.Invoke(CoinTeam.Player);
        }

        PlayerShotResolved?.Invoke(coin, shotValid);
        MoveResolved?.Invoke(CoinTeam.Player);
        UnlockAllPlayerCoins();
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

    void UnlockAllPlayerCoins()
    {
        if (_isFirstPlayerMove)
        {
            ApplyOpeningRestrictions();
            return;
        }

        for (int i = 0; i < _playerCoins.Count; i++)
        {
            SetCoinPassiveState(_playerCoins[i], false);
        }
    }

    void ReconcilePlayerCoinPassiveState()
    {
        if (_playerCoins.Count == 0)
        {
            return;
        }

        if (_isFirstPlayerMove)
        {
            ApplyOpeningRestrictions();
        }
        else
        {
            UnlockAllPlayerCoins();
        }
    }

    void SetCoinPassiveState(CoinIdentity coin, bool passive)
    {
        coin.SetPassive(passive);
    }

    IEnumerator WaitUntilCoinStops(CoinDragController dragController, CoinIdentity shotCoin, List<Vector3> pathSamples)
    {
        pathSamples.Clear();
        pathSamples.Add(dragController.transform.position);

        yield return new WaitForSeconds(0.05f);

        float elapsed = 0f;
        while (dragController.IsSliding)
        {
            pathSamples.Add(dragController.transform.position);

            if (shotCoin != null && (_goalEnteredDuringShot || IsCoinInOpponentGoal(shotCoin)))
            {
                dragController.ForceStopSliding();
                break;
            }

            elapsed += Time.deltaTime;
            if (elapsed >= _coinStopTimeout)
            {
                Debug.LogWarning($"[Shot] {dragController.gameObject.name} timeout — coin durduruluyor");
                dragController.ForceStopSliding();
                break;
            }

            yield return null;
        }

        pathSamples.Add(dragController.transform.position);
        yield return new WaitForSeconds(0.1f);
        pathSamples.Add(dragController.transform.position);
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

    bool TryGetGateCoinsInternal(CoinIdentity movingCoin, out CoinIdentity gateA, out CoinIdentity gateB)
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

    void HandlePlayerGoalCelebration()
    {
        OpponentBotController.Instance?.PauseForRoundReset();
        GameFeedback.EnsureInstance()?.PlayGoal();
        InvokePlayerGoalScoredSafely();

        PlayerGoalEffectController effect = PlayerGoalEffectController.EnsureInstance();
        if (effect != null && effect.CanPlay())
        {
            effect.Play(BeginRoundResetAfterGoal);
            return;
        }

        BeginRoundResetAfterGoal();
    }

    public void HandleEnemyGoalCelebration()
    {
        OpponentBotController.Instance?.PauseForRoundReset();
        GameFeedback.EnsureInstance()?.PlayEnemyGoal();

        EnemyGoalEffectController effect = EnemyGoalEffectController.EnsureInstance();
        if (effect != null && effect.CanPlay())
        {
            effect.Play(BeginRoundResetAfterGoal);
            return;
        }

        BeginRoundResetAfterGoal();
    }

    public void BeginRoundResetAfterGoal()
    {
        OpponentBotController.Instance?.PauseForRoundReset();

        if (_roundResetRoutine != null)
        {
            StopCoroutine(_roundResetRoutine);
            _roundResetRoutine = null;
        }

        _resolveRoutine = null;
        _shotCoin = null;
        _roundResetRoutine = StartCoroutine(RunRoundResetAfterGoal());
    }

    void InvokePlayerGoalScoredSafely()
    {
        if (PlayerGoalScored == null)
        {
            return;
        }

        foreach (Action handler in PlayerGoalScored.GetInvocationList())
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

    IEnumerator ResetRoundAfterGoalRoutine(bool resetMatchProgress)
    {
        _isResolvingMove = true;
        _shotCoin = null;

        PruneDestroyedRoundCoins();

        if (_roundCoins.Count == 0 || _initialPositions.Count == 0)
        {
            CacheInitialPositions();
        }

        var activeCoins = new List<CoinIdentity>(_roundCoins.Count);
        var targetPositions = new List<Vector3>(_roundCoins.Count);
        for (int i = 0; i < _roundCoins.Count; i++)
        {
            CoinIdentity coin = _roundCoins[i];
            if (coin == null)
            {
                continue;
            }

            activeCoins.Add(coin);
            if (_initialPositions.TryGetValue(coin, out Vector3 initialPosition))
            {
                targetPositions.Add(initialPosition);
            }
            else
            {
                targetPositions.Add(coin.transform.position);
            }
        }

        if (activeCoins.Count > 0)
        {
            yield return AnimateCoinsToPositions(activeCoins, targetPositions, _goalResetDuration);
        }

        if (resetMatchProgress)
        {
            BeginNewRound();
        }
        else
        {
            _playerShotNumber = 1;
            _isFirstPlayerMove = true;
            _isOpeningShot = false;
            GateIndicator.Instance?.Hide();
        }

        DiscoverPlayerCoins();
        _isResolvingMove = false;
        _roundResetRoutine = null;
        RoundReset?.Invoke();
    }

    void PruneDestroyedRoundCoins()
    {
        for (int i = _roundCoins.Count - 1; i >= 0; i--)
        {
            if (_roundCoins[i] == null)
            {
                _roundCoins.RemoveAt(i);
            }
        }

        var validPositions = new Dictionary<CoinIdentity, Vector3>(_initialPositions.Count);
        foreach (KeyValuePair<CoinIdentity, Vector3> entry in _initialPositions)
        {
            if (entry.Key != null)
            {
                validPositions[entry.Key] = entry.Value;
            }
        }

        _initialPositions.Clear();
        foreach (KeyValuePair<CoinIdentity, Vector3> entry in validPositions)
        {
            _initialPositions[entry.Key] = entry.Value;
        }
    }

    IEnumerator ResetRoundAfterGoalRoutine()
    {
        yield return ResetRoundAfterGoalRoutine(resetMatchProgress: true);
    }

    public void RequestRoundReset()
    {
        CancelActiveShotResolution();
        BeginRoundResetAfterGoal();
    }

    IEnumerator RunRoundResetAfterGoal()
    {
        yield return ResetRoundAfterGoalRoutine(resetMatchProgress: true);
    }

    public IEnumerator ResetAllCoinPositionsRoutine()
    {
        CancelActiveShotResolution();

        yield return ResetRoundAfterGoalRoutine(resetMatchProgress: false);
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

        var rigidbodies = new List<Rigidbody>(coins.Count);
        var animatedCoins = new List<CoinIdentity>(coins.Count);
        var startPositions = new List<Vector3>(coins.Count);
        var endPositions = new List<Vector3>(coins.Count);

        for (int i = 0; i < coins.Count; i++)
        {
            CoinIdentity coin = coins[i];
            if (coin == null)
            {
                continue;
            }

            CoinDragController dragController = coin.DragController;
            if (dragController == null)
            {
                continue;
            }

            Rigidbody rigidbody = dragController.GetComponent<Rigidbody>();
            if (rigidbody == null)
            {
                continue;
            }

            if (dragController.IsAiming)
            {
                dragController.CancelAim();
            }

            dragController.ForceStopSliding();
            dragController.ResetVisualRotation();
            rigidbody.isKinematic = true;

            rigidbodies.Add(rigidbody);
            animatedCoins.Add(coin);
            startPositions.Add(rigidbody.position);
            endPositions.Add(targetPositions[i]);
        }

        if (rigidbodies.Count == 0)
        {
            yield break;
        }

        int animatedCount = rigidbodies.Count;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            for (int i = 0; i < animatedCount; i++)
            {
                Vector3 nextPosition = Vector3.Lerp(startPositions[i], endPositions[i], t);
                nextPosition.y = endPositions[i].y;
                rigidbodies[i].position = nextPosition;
            }

            yield return null;
        }

        for (int i = 0; i < animatedCount; i++)
        {
            Vector3 finalPosition = endPositions[i];
            finalPosition.y = endPositions[i].y;
            rigidbodies[i].position = finalPosition;
            rigidbodies[i].isKinematic = false;
            animatedCoins[i].DragController.ResetVisualRotation();
        }
    }
}

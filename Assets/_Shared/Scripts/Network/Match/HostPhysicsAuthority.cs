using UnityEngine;

/// <summary>
/// Online maçlarda host-authoritative fizik: host, tahtadaki 6 paranın pozisyonunu hareket
/// halindeyken periyodik olarak misafire yayınlar (Faz 1) ve misafirin atışları için TEK
/// otoriter gol hakemliğini yapar (Faz 2) — iki istemcinin bağımsız PhysX simülasyonlarının
/// zincirleme çarpışmalarda birbirinden sapıp "bence gol / bence değil" anlaşmazlığına
/// düşmesini önler. Misafir tarafında bu sınıf sadece gelen pozisyon snapshot'larını uygular.
/// </summary>
public class HostPhysicsAuthority : MonoBehaviour
{
    public static HostPhysicsAuthority Instance { get; private set; }

    static readonly string[] PlayerCoinNames = { "Coin_P1", "Coin_P2", "Coin_P3" };
    static readonly string[] EnemyCoinNames = { "Coin_E1", "Coin_E2", "Coin_E3" };

    const float SnapshotRateHz = 15f;
    const float SnapshotIntervalSeconds = 1f / SnapshotRateHz;

    MatchShotNetworkRelay _boundRelay;

    // Host: pozisyon yayını
    bool _wasAtRest = true;
    float _snapshotTimer;
    int _batchSeq;
    Transform[] _cachedPlayerCoins;
    Transform[] _cachedEnemyCoins;

    // Host: gol hakemliği (misafirin atışları için)
    CoinIdentity _pendingGuestShotCoin;
    int _pendingGuestShotSeq = -1;

    // Misafir: snapshot dedup
    int _lastAppliedBatchSeq = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Ensure()
    {
        if (Instance != null)
        {
            return;
        }

        var go = new GameObject("HostPhysicsAuthority");
        Instance = go.AddComponent<HostPhysicsAuthority>();
        DontDestroyOnLoad(go);
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
        OnlineMatchSession.SessionStarted += ResetMatchState;
        OnlineMatchSession.SessionCleared += ResetMatchState;
    }

    void OnDisable()
    {
        OnlineMatchSession.SessionStarted -= ResetMatchState;
        OnlineMatchSession.SessionCleared -= ResetMatchState;
    }

    void ResetMatchState()
    {
        _wasAtRest = true;
        _snapshotTimer = 0f;
        _cachedPlayerCoins = null;
        _cachedEnemyCoins = null;
        _pendingGuestShotCoin = null;
        _pendingGuestShotSeq = -1;
    }

    void Update()
    {
        if (!OnlineMatchSession.IsOnlineMatch || OnlineMatchSession.Channel == null)
        {
            return;
        }

        EnsureRelayBound();

        if (OnlineMatchSession.Channel.IsHost)
        {
            HostUpdate();
        }
    }

    /// <summary>
    /// MatchShotNetworkRelay.Instance her maçta YENİ bir nesne olarak spawn ediliyor — bir
    /// bool bayrak yerine gerçek referansı karşılaştırıyoruz, yoksa ikinci maçta (Instance
    /// değiştiğinde) olaylara hiç yeniden abone olunmaz (bu oturumda birkaç kez karşılaşılan
    /// bir bug sınıfı).
    /// </summary>
    void EnsureRelayBound()
    {
        if (ReferenceEquals(_boundRelay, MatchShotNetworkRelay.Instance))
        {
            return;
        }

        if (_boundRelay != null)
        {
            _boundRelay.CoinSnapshotReceived -= OnCoinSnapshotReceived;
            _boundRelay.ShotReceived -= OnHostObservedShotReceived;
            _boundRelay.ShotRollbackReceived -= OnHostObservedRollbackReceived;
        }

        _boundRelay = MatchShotNetworkRelay.Instance;

        if (_boundRelay != null)
        {
            _boundRelay.CoinSnapshotReceived += OnCoinSnapshotReceived;
            _boundRelay.ShotReceived += OnHostObservedShotReceived;
            _boundRelay.ShotRollbackReceived += OnHostObservedRollbackReceived;
        }
    }

    // ===================== HOST: pozisyon yayını (Faz 1) =====================

    void HostUpdate()
    {
        if (GameRulesManager.Instance == null)
        {
            return;
        }

        bool atRest = GameRulesManager.Instance.AreAllRoundCoinsAtRest();

        if (atRest)
        {
            if (!_wasAtRest)
            {
                // hareket → durgun: son bir snapshot gönderip yayını durdur (bandwidth tasarrufu).
                BroadcastSnapshot();
                _wasAtRest = true;
                _snapshotTimer = 0f;
            }

            EvaluatePendingGuestGoalArbitration();
            return;
        }

        _wasAtRest = false;
        _snapshotTimer += Time.deltaTime;
        if (_snapshotTimer >= SnapshotIntervalSeconds)
        {
            _snapshotTimer = 0f;
            BroadcastSnapshot();
        }
    }

    void BroadcastSnapshot()
    {
        if (_boundRelay == null)
        {
            return;
        }

        EnsureCoinCache();

        Vector3 p1 = PositionOf(_cachedPlayerCoins, 0);
        Vector3 p2 = PositionOf(_cachedPlayerCoins, 1);
        Vector3 p3 = PositionOf(_cachedPlayerCoins, 2);
        Vector3 e1 = PositionOf(_cachedEnemyCoins, 0);
        Vector3 e2 = PositionOf(_cachedEnemyCoins, 1);
        Vector3 e3 = PositionOf(_cachedEnemyCoins, 2);

        _batchSeq++;
        Debug.Log(
            $"[HostPhysics] SEND batchSeq={_batchSeq} p1={p1:F2} p2={p2:F2} p3={p3:F2} " +
            $"e1={e1:F2} e2={e2:F2} e3={e3:F2}");
        _boundRelay.TrySendCoinSnapshot(_batchSeq,
            p1.x, p1.z, p2.x, p2.z, p3.x, p3.z,
            e1.x, e1.z, e2.x, e2.z, e3.x, e3.z);
    }

    void EnsureCoinCache()
    {
        // Maç başında coin'ler henüz sahnede değilse null kalabilir — hepsi bulunana kadar
        // her çağrıda tekrar dener, sonra kalıcı olarak önbelleğe alır.
        if (_cachedPlayerCoins == null || HasNullEntry(_cachedPlayerCoins))
        {
            _cachedPlayerCoins = ResolveCoinTransforms(PlayerCoinNames);
        }

        if (_cachedEnemyCoins == null || HasNullEntry(_cachedEnemyCoins))
        {
            _cachedEnemyCoins = ResolveCoinTransforms(EnemyCoinNames);
        }
    }

    static bool HasNullEntry(Transform[] transforms)
    {
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] == null)
            {
                return true;
            }
        }

        return false;
    }

    static Transform[] ResolveCoinTransforms(string[] names)
    {
        var result = new Transform[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            CoinIdentity coin = FindCoinByName(names[i]);
            result[i] = coin != null ? coin.transform : null;
            if (coin == null)
            {
                Debug.LogWarning($"[HostPhysics] Coin bulunamadı (cache): {names[i]}");
            }
        }

        return result;
    }

    static Vector3 PositionOf(Transform[] cache, int index)
    {
        Transform t = cache != null && index < cache.Length ? cache[index] : null;
        return t != null ? t.position : Vector3.zero;
    }

    /// <summary>
    /// RemoteOpponentController.FindRemoteMappedCoin ile AYNI desen: GameObject.Find'ı tek
    /// başına güvenmek yerine önce CoinIdentity tipine göre arar (bu projede isim-bazlı
    /// GameObject.Find'ın tek başına güvenilmez olduğu zaten biliniyordu).
    /// </summary>
    static CoinIdentity FindCoinByName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        CoinIdentity[] coins = FindObjectsByType<CoinIdentity>(FindObjectsSortMode.None);
        for (int i = 0; i < coins.Length; i++)
        {
            if (coins[i] != null && coins[i].gameObject.name == name)
            {
                return coins[i];
            }
        }

        GameObject go = GameObject.Find(name);
        return go != null ? go.GetComponent<CoinIdentity>() : null;
    }

    // ===================== HOST: misafirin atışları için gol hakemliği (Faz 2) =====================

    void OnHostObservedShotReceived(ShotIntentMessage shot)
    {
        if (shot == null || OnlineMatchSession.Channel == null || !OnlineMatchSession.Channel.IsHost)
        {
            return;
        }

        string localName = RemoteOpponentController.FlipTeamToken(shot.coinObjectName ?? string.Empty);
        CoinIdentity identity = FindCoinByName(localName);
        if (identity == null)
        {
            Debug.LogWarning($"[GoalArbiter] Coin bulunamadı: {localName}");
            return;
        }

        _pendingGuestShotCoin = identity;
        _pendingGuestShotSeq = shot.seq;
        Debug.Log($"[GoalArbiter] Pending guest shot coin={identity.name} shotSeq={shot.seq}");
    }

    void OnHostObservedRollbackReceived(ShotRollbackMessage rollback)
    {
        if (rollback == null || _pendingGuestShotSeq != rollback.seq)
        {
            return;
        }

        // Misafirin kendi kapı-kuralı ihlali — host bunu asla ezip gol saymamalı.
        Debug.Log($"[GoalArbiter] Rollback alındı — bekleyen hakemlik golsüz iptal shotSeq={rollback.seq}");
        _pendingGuestShotCoin = null;
        _pendingGuestShotSeq = -1;
    }

    void EvaluatePendingGuestGoalArbitration()
    {
        if (_pendingGuestShotCoin == null)
        {
            return;
        }

        CoinIdentity coin = _pendingGuestShotCoin;
        int shotSeq = _pendingGuestShotSeq;
        _pendingGuestShotCoin = null;
        _pendingGuestShotSeq = -1;

        GoalZone myGoal = GoalZone.FindPlayerGoalArea();
        bool scored = myGoal != null && myGoal.OverlapsCoin(coin);

        Debug.Log($"[GoalArbiter] Evaluate shotSeq={shotSeq} coin={coin.name} scored={scored}");

        if (scored)
        {
            _boundRelay?.TrySendGoal(hostScored: false, shotSeq: shotSeq);
        }

        // scored=false: hiçbir RPC gönderilmez — misafirin bekleme süresinin zaman aşımına
        // uğraması "gol değil" sinyalinin kendisidir (bkz. GameRulesManager.WaitForHostGoalConfirmation).
    }

    // ===================== MİSAFİR: gelen pozisyon snapshot'ını uygula (Faz 1) =====================

    void OnCoinSnapshotReceived(int batchSeq,
        float p1x, float p1z, float p2x, float p2z, float p3x, float p3z,
        float e1x, float e1z, float e2x, float e2z, float e3x, float e3z)
    {
        if (OnlineMatchSession.Channel != null && OnlineMatchSession.Channel.IsHost)
        {
            return;
        }

        if (batchSeq <= _lastAppliedBatchSeq)
        {
            return;
        }

        _lastAppliedBatchSeq = batchSeq;

        Debug.Log(
            $"[GuestReconcile] RECV batchSeq={batchSeq} p1=({p1x:F2},{p1z:F2}) p2=({p2x:F2},{p2z:F2}) " +
            $"p3=({p3x:F2},{p3z:F2}) e1=({e1x:F2},{e1z:F2}) e2=({e2x:F2},{e2z:F2}) e3=({e3x:F2},{e3z:F2})");

        ApplySnapshotSlot(PlayerCoinNames[0], p1x, p1z);
        ApplySnapshotSlot(PlayerCoinNames[1], p2x, p2z);
        ApplySnapshotSlot(PlayerCoinNames[2], p3x, p3z);
        ApplySnapshotSlot(EnemyCoinNames[0], e1x, e1z);
        ApplySnapshotSlot(EnemyCoinNames[1], e2x, e2z);
        ApplySnapshotSlot(EnemyCoinNames[2], e3x, e3z);
    }

    /// <summary>
    /// senderLocalName host'un kendi yerel adıdır ("Coin_P1" vb.) — aynalanmış tahtalar
    /// yüzünden FlipTeamToken ile bizim yerel adımıza, Z ekseni negatif çevrilerek bizim
    /// yerel koordinatımıza çeviriyoruz (ShotIntentMessage/rollback ile aynı sözleşme).
    /// </summary>
    static void ApplySnapshotSlot(string senderLocalName, float x, float z)
    {
        string localName = RemoteOpponentController.FlipTeamToken(senderLocalName);
        CoinIdentity identity = FindCoinByName(localName);
        if (identity == null)
        {
            Debug.LogWarning($"[GuestReconcile] Coin bulunamadı: {localName}");
            return;
        }

        CoinDragController drag = identity.DragController;
        if (drag != null && drag.IsAiming)
        {
            // Tam o anda nişan alınıyorsa dokunma — eski/geç gelmiş bir snapshot olabilir.
            return;
        }

        GameObject go = identity.gameObject;
        Vector3 targetPosition = new(x, go.transform.position.y, -z);

        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Velocity BİLEREK sıfırlanmıyor — yoksa her ~66ms'de görünür bir dur-kalk
            // sarsıntısı olur. Sadece pozisyon periyodik olarak gerçeğe çekiliyor,
            // RigidbodyInterpolation.Interpolate (CoinDragController.ConfigureRigidbody)
            // aradaki görsel geçişi zaten yumuşatıyor.
            rb.position = targetPosition;
        }
        else
        {
            go.transform.position = targetPosition;
        }
    }
}

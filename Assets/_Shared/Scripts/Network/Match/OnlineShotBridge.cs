using UnityEngine;

/// <summary>
/// Local şut release → ShotIntent; geçersiz hamle → ShotRollback.
/// DontDestroyOnLoad: her Game sahnesinde GameRulesManager'a yeniden abone olur.
/// </summary>
public class OnlineShotBridge : MonoBehaviour
{
    public static OnlineShotBridge Instance { get; private set; }

    Vector3 _pendingStartPos;
    CoinIdentity _pendingCoin;
    int _pendingSeq;
    bool _hasPending;
    GameRulesManager _boundRules;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Ensure()
    {
        if (Instance != null)
        {
            return;
        }

        var go = new GameObject("OnlineShotBridge");
        Instance = go.AddComponent<OnlineShotBridge>();
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

    void Update()
    {
        EnsureRulesBound();
    }

    void OnDestroy()
    {
        UnbindRules();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void EnsureRulesBound()
    {
        GameRulesManager rules = GameRulesManager.Instance;
        if (rules == null)
        {
            if (_boundRules != null)
            {
                UnbindRules();
            }

            return;
        }

        if (_boundRules == rules)
        {
            return;
        }

        UnbindRules();
        _boundRules = rules;
        _boundRules.InvalidMoveRollbackStarted += OnInvalidMoveRollbackStarted;
        _boundRules.PlayerShotResolved += OnPlayerShotResolved;
        Debug.Log("[OnlineShot] GameRulesManager'a abone olundu.");
    }

    void UnbindRules()
    {
        if (_boundRules == null)
        {
            return;
        }

        _boundRules.InvalidMoveRollbackStarted -= OnInvalidMoveRollbackStarted;
        _boundRules.PlayerShotResolved -= OnPlayerShotResolved;
        _boundRules = null;
    }

    /// <summary>CoinDragController launch öncesi — şutu anında gönder.</summary>
    public void CaptureLocalShot(CoinIdentity coin, Vector3 direction, float pullDistance)
    {
        if (coin == null || !IsOnline())
        {
            return;
        }

        EnsureRulesBound();

        _pendingCoin = coin;
        _pendingStartPos = coin.transform.position;
        _pendingSeq = Time.frameCount;
        _hasPending = true;

        Debug.Log(
            $"[OnlineShot] Capture {coin.name} pull={pullDistance:F3} " +
            $"channel={(OnlineMatchSession.Channel != null)}");

        if (RemoteOpponentController.Instance != null)
        {
            int seq = RemoteOpponentController.Instance.BroadcastLocalShot(
                coin, direction, pullDistance);
            if (seq > 0)
            {
                _pendingSeq = seq;
            }
        }
        else
        {
            BroadcastDirect(coin, direction, pullDistance, _pendingSeq);
        }
    }

    /// <summary>Invalid başlar başlamaz rollback gönder (PlayerShotResolved'a güvenme).</summary>
    void OnInvalidMoveRollbackStarted(CoinTeam team)
    {
        if (team != CoinTeam.Player || !_hasPending || _pendingCoin == null)
        {
            return;
        }

        if (!IsOnline())
        {
            return;
        }

        BroadcastRollback(_pendingCoin, _pendingStartPos, _pendingSeq);
        // PlayerShotResolved tekrar göndermesin
        _hasPending = false;
    }

    void OnPlayerShotResolved(CoinIdentity coin, bool valid)
    {
        // Invalid path InvalidMoveRollbackStarted ile gitti; burada yedek.
        if (!_hasPending || _pendingCoin != coin)
        {
            return;
        }

        _hasPending = false;

        if (valid || !IsOnline())
        {
            return;
        }

        BroadcastRollback(_pendingCoin, _pendingStartPos, _pendingSeq);
    }

    static bool IsOnline()
    {
        return OnlineMatchSession.IsOnlineMatch
            || MatchSessionContext.IsOnlineMatch
            || OnlineMatchSession.Channel != null;
    }

    static void BroadcastDirect(CoinIdentity coin, Vector3 direction, float pullDistance, int seq)
    {
        if (OnlineMatchSession.Channel == null)
        {
            Debug.LogWarning("[OnlineShot] BroadcastDirect — channel null.");
            return;
        }

        Vector3 pos = coin.transform.position;
        OnlineMatchSession.Channel.SendShot(new ShotIntentMessage
        {
            senderUserId = OnlineMatchSession.Channel.LocalUserId,
            coinObjectName = coin.gameObject.name,
            dirX = direction.x,
            dirZ = direction.z,
            pullDistance = pullDistance,
            seq = seq,
            posX = pos.x,
            posY = pos.y,
            posZ = pos.z,
            hasPosition = true
        });
    }

    static void BroadcastRollback(CoinIdentity coin, Vector3 startPos, int seq)
    {
        if (coin == null || OnlineMatchSession.Channel == null)
        {
            Debug.LogWarning("[OnlineShot] Rollback broadcast atlandı — channel/coin yok.");
            return;
        }

        var msg = new ShotRollbackMessage
        {
            senderUserId = OnlineMatchSession.Channel.LocalUserId,
            coinObjectName = coin.gameObject.name,
            posX = startPos.x,
            posY = startPos.y,
            posZ = startPos.z,
            seq = seq
        };

        Debug.Log($"[OnlineShot] Rollback broadcast {msg.coinObjectName} seq={msg.seq} pos={startPos}");
        OnlineMatchSession.Channel.SendShotRollback(msg);
    }
}

using UnityEngine;

/// <summary>
/// Local şut release anında online kanala ShotIntent yollar.
/// CoinInputHandler / CoinDragController'a bağlanır.
/// </summary>
public class OnlineShotBridge : MonoBehaviour
{
    public static OnlineShotBridge Instance { get; private set; }

    Vector3 _pendingDir;
    float _pendingPull;
    CoinIdentity _pendingCoin;
    bool _hasPending;

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

    void OnEnable()
    {
        TrySubscribeRules();
    }

    void Update()
    {
        if (!_subscribedRules)
        {
            TrySubscribeRules();
        }
    }

    void OnDisable()
    {
        if (GameRulesManager.Instance != null)
        {
            GameRulesManager.Instance.PlayerShotResolved -= OnPlayerShotResolved;
        }

        _subscribedRules = false;
    }

    bool _subscribedRules;

    void TrySubscribeRules()
    {
        if (_subscribedRules || GameRulesManager.Instance == null)
        {
            return;
        }

        GameRulesManager.Instance.PlayerShotResolved += OnPlayerShotResolved;
        _subscribedRules = true;
    }

    /// <summary>
    /// CoinDragController launch öncesi input handler çağırır.
    /// </summary>
    public void CaptureLocalShot(CoinIdentity coin, Vector3 direction, float pullDistance)
    {
        if (coin == null)
        {
            return;
        }

        bool online = OnlineMatchSession.IsOnlineMatch
            || MatchSessionContext.IsOnlineMatch
            || OnlineMatchSession.Channel != null;
        if (!online)
        {
            return;
        }

        _pendingCoin = coin;
        _pendingDir = direction;
        _pendingPull = pullDistance;
        _hasPending = true;

        Debug.Log(
            $"[OnlineShot] Capture {coin.name} pull={pullDistance:F3} " +
            $"channel={(OnlineMatchSession.Channel != null)} remote={(RemoteOpponentController.Instance != null)}");

        if (RemoteOpponentController.Instance != null)
        {
            RemoteOpponentController.Instance.BroadcastLocalShot(coin, direction, pullDistance);
            _hasPending = false;
        }
        else
        {
            BroadcastDirect(coin, direction, pullDistance);
            _hasPending = false;
        }
    }

    void OnPlayerShotResolved(CoinIdentity coin, bool valid)
    {
        if (!_hasPending || !OnlineMatchSession.IsOnlineMatch)
        {
            return;
        }

        if (_pendingCoin != coin)
        {
            return;
        }

        BroadcastDirect(_pendingCoin, _pendingDir, _pendingPull);
        _hasPending = false;
    }

    static void BroadcastDirect(CoinIdentity coin, Vector3 direction, float pullDistance)
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
            seq = Time.frameCount,
            posX = pos.x,
            posY = pos.y,
            posZ = pos.z,
            hasPosition = true
        });
    }
}

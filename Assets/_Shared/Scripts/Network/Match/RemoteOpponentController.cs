using UnityEngine;

/// <summary>
/// Uzak insan rakip — gelen ShotIntent'i CoinShotLauncher ile uygular; bot AI kapalı.
/// </summary>
public class RemoteOpponentController : MonoBehaviour, IOpponentController
{
    public static RemoteOpponentController Instance { get; private set; }

    int _shotSeq;
    int _lastAppliedSeq = -1;
    string _lastAppliedSender;

    public string DisplayName => OnlineMatchSession.OpponentDisplayName ?? "Opponent";
    public int AvatarIndex => OnlineMatchSession.OpponentAvatarIndex;
    public bool IsHuman => true;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void OnEnable()
    {
        TryBindChannel();
        OnlineMatchSession.SessionStarted += TryBindChannel;
        OnlineMatchSession.SessionCleared += UnbindChannel;
    }

    void OnDisable()
    {
        UnbindChannel();
        OnlineMatchSession.SessionStarted -= TryBindChannel;
        OnlineMatchSession.SessionCleared -= UnbindChannel;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void TryBindChannel()
    {
        UnbindChannel();
        if (OnlineMatchSession.Channel == null)
        {
            Debug.LogWarning("[RemoteOpponent] Channel yok — shot bind ertelendi.");
            return;
        }

        OnlineMatchSession.Channel.ShotReceived += OnShotReceived;
        OnlineMatchSession.Channel.OpponentLeft += OnOpponentLeft;
        Debug.Log("[RemoteOpponent] ShotReceived bağlandı.");
    }

    void UnbindChannel()
    {
        if (OnlineMatchSession.Channel == null)
        {
            return;
        }

        OnlineMatchSession.Channel.ShotReceived -= OnShotReceived;
        OnlineMatchSession.Channel.OpponentLeft -= OnOpponentLeft;
    }

    public void OnMatchStarted()
    {
        OpponentBotController.Instance?.DisableForOnlineMatch();
        TryBindChannel();
    }

    public void OnRoundReset() { }

    public void OnMatchEnded()
    {
        UnbindChannel();
    }

    void OnShotReceived(ShotIntentMessage shot)
    {
        if (shot == null)
        {
            return;
        }

        // Host relay + P2P aynı mesajı iki kez getirebilir.
        if (shot.seq == _lastAppliedSeq && shot.senderUserId == _lastAppliedSender)
        {
            return;
        }

        _lastAppliedSeq = shot.seq;
        _lastAppliedSender = shot.senderUserId;

        CoinDragController coin = FindRemoteMappedCoin(shot.coinObjectName);
        if (coin == null)
        {
            Debug.LogWarning(
                $"[RemoteOpponent] Coin map edilemedi: '{shot.coinObjectName}' → '{FlipTeamToken(shot.coinObjectName)}'");
            return;
        }

        // Aynı world layout: sol/sağ (X) aynı, hücum ekseni (Z) ters.
        // Tam 180° → yan yön bozuluyordu; flip yok → ileri/geri ters oluyordu.
        Vector3 localDirection = new(shot.dirX, 0f, -shot.dirZ);

        bool launched = CoinShotLauncher.TryLaunch(coin, localDirection, shot.pullDistance);
        Debug.Log(
            $"[RemoteOpponent] Shot apply coin={coin.name} pull={shot.pullDistance:F3} " +
            $"dir={localDirection} launched={launched} seq={shot.seq}");

        CoinIdentity identity = coin.GetComponent<CoinIdentity>();
        if (identity != null
            && identity.Team == CoinTeam.Player
            && GameRulesManager.Instance != null)
        {
            GameRulesManager.Instance.OnShotReleased(identity);
        }
    }

    void OnOpponentLeft(string reason)
    {
        Debug.Log($"[RemoteOpponent] Opponent left: {reason}");
        if (LeagueMatchController.Instance != null && LeagueMatchController.Instance.IsMatchActive)
        {
            MatchSessionTracker.MarkAbandon("opponent_disconnect");
        }
    }

    public void BroadcastLocalShot(CoinIdentity coin, Vector3 direction, float pullDistance)
    {
        if (OnlineMatchSession.Channel == null || coin == null)
        {
            Debug.LogWarning("[RemoteOpponent] Broadcast atlandı — channel/coin yok.");
            return;
        }

        if (!OnlineMatchSession.IsOnlineMatch && !MatchSessionContext.IsOnlineMatch)
        {
            Debug.LogWarning("[RemoteOpponent] Broadcast atlandı — online flag yok.");
            return;
        }

        Vector3 pos = coin.transform.position;
        _shotSeq++;
        var msg = new ShotIntentMessage
        {
            senderUserId = OnlineMatchSession.Channel.LocalUserId,
            coinObjectName = coin.gameObject.name,
            dirX = direction.x,
            dirZ = direction.z,
            pullDistance = pullDistance,
            seq = _shotSeq,
            posX = pos.x,
            posY = pos.y,
            posZ = pos.z,
            hasPosition = true
        };
        Debug.Log($"[RemoteOpponent] Broadcast {msg.coinObjectName} seq={msg.seq} pull={pullDistance:F3}");
        OnlineMatchSession.Channel.SendShot(msg);
    }

    static CoinDragController FindRemoteMappedCoin(string senderObjectName)
    {
        if (string.IsNullOrEmpty(senderObjectName))
        {
            return null;
        }

        string localName = FlipTeamToken(senderObjectName);

        CoinIdentity[] coins = FindObjectsByType<CoinIdentity>(FindObjectsSortMode.None);
        for (int i = 0; i < coins.Length; i++)
        {
            CoinIdentity identity = coins[i];
            if (identity != null && identity.gameObject.name == localName)
            {
                return identity.GetComponent<CoinDragController>();
            }
        }

        // Fallback: GameObject.Find
        GameObject go = GameObject.Find(localName);
        return go != null ? go.GetComponent<CoinDragController>() : null;
    }

    static string FlipTeamToken(string objectName)
    {
        if (objectName.Contains("_P"))
        {
            return objectName.Replace("_P", "_E");
        }

        if (objectName.Contains("_E"))
        {
            return objectName.Replace("_E", "_P");
        }

        return objectName;
    }
}

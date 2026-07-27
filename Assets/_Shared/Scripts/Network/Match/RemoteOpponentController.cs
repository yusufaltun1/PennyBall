using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Uzak insan rakip — ShotIntent uygular; ShotRollback ile invalid move geri alır.
/// Rollback hedefi: atış öncesi LOKAL coin pozisyonu (Z mirror hatası olmaz).
/// </summary>
public class RemoteOpponentController : MonoBehaviour, IOpponentController
{
    public static RemoteOpponentController Instance { get; private set; }

    const float RemoteRollbackDuration = 0.45f;

    int _shotSeq;
    int _lastAppliedSeq = -1;
    string _lastAppliedSender;
    int _lastRollbackSeq = -1;
    string _lastRollbackSender;
    Coroutine _rollbackRoutine;

    /// <summary>seq → atış öncesi lokal pozisyon.</summary>
    readonly Dictionary<int, Vector3> _shotStartBySeq = new Dictionary<int, Vector3>();
    /// <summary>seq → mapped local coin name.</summary>
    readonly Dictionary<int, string> _shotCoinBySeq = new Dictionary<int, string>();

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
        if (_rollbackRoutine != null)
        {
            StopCoroutine(_rollbackRoutine);
            _rollbackRoutine = null;
        }

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
        OnlineMatchSession.Channel.ShotRollbackReceived += OnShotRollbackReceived;
        OnlineMatchSession.Channel.OpponentLeft += OnOpponentLeft;
        Debug.Log("[RemoteOpponent] ShotReceived + ShotRollback bağlandı.");
    }

    void UnbindChannel()
    {
        if (OnlineMatchSession.Channel == null)
        {
            return;
        }

        OnlineMatchSession.Channel.ShotReceived -= OnShotReceived;
        OnlineMatchSession.Channel.ShotRollbackReceived -= OnShotRollbackReceived;
        OnlineMatchSession.Channel.OpponentLeft -= OnOpponentLeft;
    }

    public void OnMatchStarted()
    {
        OpponentBotController.Instance?.DisableForOnlineMatch();
        TryBindChannel();
    }

    public void OnRoundReset()
    {
        _shotStartBySeq.Clear();
        _shotCoinBySeq.Clear();
    }

    public void OnMatchEnded()
    {
        UnbindChannel();
        _shotStartBySeq.Clear();
        _shotCoinBySeq.Clear();
    }

    void OnShotReceived(ShotIntentMessage shot)
    {
        if (shot == null)
        {
            return;
        }

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

        // Rollback için atış ÖNCESİ lokal pozisyonu sakla.
        _shotStartBySeq[shot.seq] = coin.transform.position;
        _shotCoinBySeq[shot.seq] = coin.gameObject.name;

        Vector3 localDirection = new(shot.dirX, 0f, -shot.dirZ);

        bool launched = CoinShotLauncher.TryLaunch(coin, localDirection, shot.pullDistance);
        Debug.Log(
            $"[RemoteOpponent] Shot apply coin={coin.name} pull={shot.pullDistance:F3} " +
            $"dir={localDirection} launched={launched} seq={shot.seq} start={_shotStartBySeq[shot.seq]}");

        CoinIdentity identity = coin.GetComponent<CoinIdentity>();
        if (identity != null
            && identity.Team == CoinTeam.Player
            && GameRulesManager.Instance != null)
        {
            GameRulesManager.Instance.OnShotReleased(identity);
        }
    }

    void OnShotRollbackReceived(ShotRollbackMessage rollback)
    {
        if (rollback == null)
        {
            return;
        }

        if (rollback.seq == _lastRollbackSeq && rollback.senderUserId == _lastRollbackSender)
        {
            return;
        }

        _lastRollbackSeq = rollback.seq;
        _lastRollbackSender = rollback.senderUserId;

        CoinIdentity identity = null;
        Vector3 target;

        if (_shotStartBySeq.TryGetValue(rollback.seq, out Vector3 storedStart)
            && _shotCoinBySeq.TryGetValue(rollback.seq, out string coinName))
        {
            target = storedStart;
            GameObject go = GameObject.Find(coinName);
            identity = go != null ? go.GetComponent<CoinIdentity>() : null;
            Debug.Log($"[RemoteOpponent] Rollback seq={rollback.seq} storedStart={storedStart} coin={coinName}");
        }
        else
        {
            CoinDragController coin = FindRemoteMappedCoin(rollback.coinObjectName);
            if (coin == null)
            {
                Debug.LogWarning(
                    $"[RemoteOpponent] Rollback coin map edilemedi: '{rollback.coinObjectName}' seq={rollback.seq}");
                return;
            }

            identity = coin.GetComponent<CoinIdentity>();
            // Fallback: gönderen pozisyonunu Z mirror ile dene
            target = new Vector3(rollback.posX, rollback.posY, -rollback.posZ);
            Debug.LogWarning(
                $"[RemoteOpponent] Rollback stored start yok — mirror fallback target={target}");
        }

        if (identity == null)
        {
            Debug.LogWarning("[RemoteOpponent] Rollback identity null.");
            return;
        }

        if (_rollbackRoutine != null)
        {
            StopCoroutine(_rollbackRoutine);
        }

        _rollbackRoutine = StartCoroutine(ApplyRemoteRollbackRoutine(identity, target));
    }

    IEnumerator ApplyRemoteRollbackRoutine(CoinIdentity coin, Vector3 targetPosition)
    {
        Debug.Log($"[RemoteOpponent] Rollback animate {coin.name} → {targetPosition}");
        GameRulesManager.Instance?.NotifyRemoteInvalidMoveStarted(CoinTeam.Opponent);

        coin.DragController?.ForceStopSliding();

        if (GameRulesManager.Instance != null)
        {
            yield return GameRulesManager.Instance.AnimateCoinToPosition(
                coin, targetPosition, RemoteRollbackDuration);
        }
        else
        {
            coin.transform.position = targetPosition;
        }

        GameRulesManager.Instance?.NotifyRemoteInvalidMoveFinished(CoinTeam.Opponent);
        _rollbackRoutine = null;
    }

    void OnOpponentLeft(string reason)
    {
        Debug.Log($"[RemoteOpponent] Opponent left: {reason}");
        if (LeagueMatchController.Instance != null && LeagueMatchController.Instance.IsMatchActive)
        {
            MatchSessionTracker.MarkAbandon("opponent_disconnect");
        }
    }

    public int BroadcastLocalShot(CoinIdentity coin, Vector3 direction, float pullDistance)
    {
        if (OnlineMatchSession.Channel == null || coin == null)
        {
            Debug.LogWarning("[RemoteOpponent] Broadcast atlandı — channel/coin yok.");
            return -1;
        }

        if (!OnlineMatchSession.IsOnlineMatch && !MatchSessionContext.IsOnlineMatch)
        {
            Debug.LogWarning("[RemoteOpponent] Broadcast atlandı — online flag yok.");
            return -1;
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
        return msg.seq;
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

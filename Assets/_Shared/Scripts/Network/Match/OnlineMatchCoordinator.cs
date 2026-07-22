using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Online maçta shot broadcast + skor sync + bot disable.
/// </summary>
public class OnlineMatchCoordinator : MonoBehaviour
{
    public static OnlineMatchCoordinator Instance { get; private set; }

    [SerializeField] bool _broadcastSnapshots;
    [SerializeField] float _snapshotInterval = 0.5f;
    [SerializeField] float _timeSyncInterval = 0.5f;

    float _snapshotTimer;
    float _timeSyncTimer;
    int _lastSentPlayerGoals = -1;
    int _lastSentOpponentGoals = -1;
    bool _timeSyncBound;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Ensure()
    {
        if (Instance != null)
        {
            return;
        }

        var go = new GameObject("OnlineMatchCoordinator");
        Instance = go.AddComponent<OnlineMatchCoordinator>();
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
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnlineMatchSession.SessionStarted += OnSessionStarted;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        OnlineMatchSession.SessionStarted -= OnSessionStarted;
        UnsubscribeChannel();
        UnsubscribeRules();
    }

    void OnSessionStarted()
    {
        BindChannel();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != GameSceneNames.Game)
        {
            return;
        }

        // Photon join gecikse bile bot'u hemen kapat.
        if (PendingPhotonSession.HasPending
            || MatchSessionContext.IsOnlineMatch
            || OnlineMatchSession.IsOnlineMatch)
        {
            DisableBot();
        }

        // Play QM: MainMenu'de zaten join edildi.
        if (OnlineMatchSession.IsOnlineMatch && OnlineMatchSession.Channel != null)
        {
            // Erken Authorize varsa geri al — Ready/MatchStart beklenir.
            OnlineMatchSession.RevokeMatchPlayAuthorization();
            EnsureRemoteOpponent();
            DisableBot();
            SubscribeRules();
            BindChannel();

            if (OnlineMatchSession.Channel is PhotonFusionMatchChannel fusion)
            {
                _ = fusion.RefreshAfterSceneLoadAsync();
            }

            return;
        }

        if (PendingPhotonSession.HasPending)
        {
            _ = ConnectPendingPhotonAsync();
            return;
        }

        if (!OnlineMatchSession.IsOnlineMatch && !MatchSessionContext.IsOnlineMatch)
        {
            return;
        }

        EnsureRemoteOpponent();
        SubscribeRules();
        BindChannel();
    }

    async System.Threading.Tasks.Task ConnectPendingPhotonAsync()
    {
        string session = PendingPhotonSession.SessionName;
        string matchId = PendingPhotonSession.MatchId;
        string opponentUserId = PendingPhotonSession.OpponentUserId;
        string opponentName = PendingPhotonSession.OpponentDisplayName;
        int avatar = PendingPhotonSession.OpponentAvatarIndex;
        bool isTest = PendingPhotonSession.IsTestRoom;
        PendingPhotonSession.Clear();

        NetworkBootstrap bootstrap = NetworkBootstrap.Instance;
        if (bootstrap == null)
        {
            var go = new GameObject("NetworkBootstrap");
            bootstrap = go.AddComponent<NetworkBootstrap>();
        }

        IMatchRealtimeChannel channel = MatchRealtimeFactory.Create(bootstrap);
        if (channel == null)
        {
            Debug.LogError("[OnlineMatch] Photon channel yok — AppId kontrol et.");
            MatchSessionContext.SetOnlineMatch(false, null, null);
            // Countdown IsActive kilidini aç (JoinAsync içindeki authorize Begin ile siliniyordu).
            OnlineMatchSession.AuthorizeMatchPlay();
            return;
        }

        try
        {
            await channel.JoinAsync(session);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[OnlineMatch] Photon Join failed: {ex.Message}");
            channel.Dispose();
            MatchSessionContext.SetOnlineMatch(false, null, null);
            OnlineMatchSession.AuthorizeMatchPlay();
            return;
        }

        if (isTest)
        {
            OnlineMatchSession.BeginTestRoom(session, channel, opponentName);
        }
        else
        {
            var result = MatchmakingResult.Human(matchId, session, opponentUserId, opponentName, avatar);
            OnlineMatchSession.BeginHumanMatch(result, channel);
        }

        // Join sonrası erken authorize yok — Ready/MatchStart.
        MatchSessionContext.SetOnlineMatch(true, opponentUserId, matchId);
        EnsureRemoteOpponent();
        DisableBot();
        SubscribeRules();
        BindChannel();

        if (channel is PhotonFusionMatchChannel fusion)
        {
            await fusion.RefreshAfterSceneLoadAsync();
        }
    }

    void Update()
    {
        if (!OnlineMatchSession.IsOnlineMatch || OnlineMatchSession.Channel == null)
        {
            return;
        }

        OnlineMatchGoalSync.EnsureBound();
        EnsureTimeSyncBound();
        SyncScoreIfNeeded();
        SyncTimeIfNeeded();

        if (!_broadcastSnapshots || !OnlineMatchSession.Channel.IsHost)
        {
            return;
        }

        _snapshotTimer += Time.deltaTime;
        if (_snapshotTimer >= _snapshotInterval)
        {
            _snapshotTimer = 0f;
            BroadcastCoinSnapshot();
        }
    }

    void EnsureTimeSyncBound()
    {
        if (MatchShotNetworkRelay.Instance == null)
        {
            _timeSyncBound = false;
            return;
        }

        if (_timeSyncBound)
        {
            return;
        }

        MatchShotNetworkRelay.Instance.TimeSyncReceived -= OnTimeSyncReceived;
        MatchShotNetworkRelay.Instance.TimeSyncReceived += OnTimeSyncReceived;
        _timeSyncBound = true;
    }

    void SyncTimeIfNeeded()
    {
        if (!OnlineMatchSession.Channel.IsHost)
        {
            return;
        }

        if (LeagueMatchController.Instance == null || !LeagueMatchController.Instance.IsMatchActive)
        {
            return;
        }

        if (MatchBeginningCountdownController.IsActive)
        {
            return;
        }

        _timeSyncTimer += Time.unscaledDeltaTime;
        if (_timeSyncTimer < _timeSyncInterval)
        {
            return;
        }

        _timeSyncTimer = 0f;
        MatchShotNetworkRelay.Instance?.TrySendTimeSync(
            LeagueMatchController.Instance.MatchTimeRemaining,
            LeagueMatchController.Instance.IsMatchTimerPaused);
    }

    void OnTimeSyncReceived(float remaining, bool paused)
    {
        if (OnlineMatchSession.Channel != null && OnlineMatchSession.Channel.IsHost)
        {
            return;
        }

        LeagueMatchController.Instance?.ApplyNetworkTimeRemaining(remaining, paused);
    }

    void EnsureRemoteOpponent()
    {
        if (RemoteOpponentController.Instance != null)
        {
            RemoteOpponentController.Instance.OnMatchStarted();
            return;
        }

        var go = new GameObject("RemoteOpponentController");
        var remote = go.AddComponent<RemoteOpponentController>();
        remote.OnMatchStarted();
    }

    static void DisableBot()
    {
        var bot = OpponentBotController.Instance;
        if (bot == null)
        {
            bot = FindFirstObjectByType<OpponentBotController>();
        }

        bot?.DisableForOnlineMatch();
    }

    void SubscribeRules()
    {
        UnsubscribeRules();
        if (GameRulesManager.Instance == null)
        {
            return;
        }

        GameRulesManager.Instance.PlayerShotResolved += OnPlayerShotResolved;
        OnlineMatchGoalSync.EnsureBound();
    }

    void UnsubscribeRules()
    {
        if (GameRulesManager.Instance == null)
        {
            return;
        }

        GameRulesManager.Instance.PlayerShotResolved -= OnPlayerShotResolved;
    }

    void OnPlayerShotResolved(CoinIdentity coin, bool valid)
    {
        // Shot zaten Launch anında gönderilmeli; burada yedek yok.
        // Asıl broadcast CoinInputHandler / OnlineShotBridge üzerinden.
    }

    void BindChannel()
    {
        UnsubscribeChannel();
        if (OnlineMatchSession.Channel == null)
        {
            return;
        }

        OnlineMatchSession.Channel.ScoreReceived += OnScoreReceived;
        OnlineMatchSession.Channel.MatchEndReceived += OnMatchEndReceived;
        OnlineMatchSession.Channel.SnapshotReceived += OnSnapshotReceived;
        OnlineMatchSession.Channel.OpponentLeft += OnOpponentLeft;
    }

    void UnsubscribeChannel()
    {
        if (OnlineMatchSession.Channel == null)
        {
            return;
        }

        OnlineMatchSession.Channel.ScoreReceived -= OnScoreReceived;
        OnlineMatchSession.Channel.MatchEndReceived -= OnMatchEndReceived;
        OnlineMatchSession.Channel.SnapshotReceived -= OnSnapshotReceived;
        OnlineMatchSession.Channel.OpponentLeft -= OnOpponentLeft;
    }

    void SyncScoreIfNeeded()
    {
        if (LeagueMatchController.Instance == null || !OnlineMatchSession.Channel.IsHost)
        {
            return;
        }

        int p = LeagueMatchController.Instance.PlayerGoals;
        int o = LeagueMatchController.Instance.OpponentGoals;
        if (p == _lastSentPlayerGoals && o == _lastSentOpponentGoals)
        {
            return;
        }

        _lastSentPlayerGoals = p;
        _lastSentOpponentGoals = o;
        OnlineMatchSession.Channel.SendScore(new ScoreSyncMessage
        {
            playerGoals = p,
            opponentGoals = o,
            hostUserId = OnlineMatchSession.Channel.LocalUserId
        });
    }

    void OnScoreReceived(ScoreSyncMessage score)
    {
        // Non-host skor UI mirror — LeagueMatchController public setter yoksa context'e yaz.
        MatchSessionContext.SetFinalScore(score.playerGoals, score.opponentGoals);
    }

    void OnMatchEndReceived(MatchEndMessage end)
    {
        MatchSessionContext.SetFinalScore(end.playerGoals, end.opponentGoals);
    }

    void OnSnapshotReceived(CoinSnapshotBatchMessage batch)
    {
        if (OnlineMatchSession.Channel.IsHost || batch.coins == null)
        {
            return;
        }

        for (int i = 0; i < batch.coins.Length; i++)
        {
            CoinSnapshotMessage snap = batch.coins[i];
            GameObject go = GameObject.Find(snap.coinObjectName);
            if (go == null)
            {
                continue;
            }

            Rigidbody rb = go.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.position = new Vector3(snap.x, snap.y, snap.z);
                rb.rotation = Quaternion.Euler(0f, snap.rotY, 0f);
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            else
            {
                go.transform.SetPositionAndRotation(
                    new Vector3(snap.x, snap.y, snap.z),
                    Quaternion.Euler(0f, snap.rotY, 0f));
            }
        }
    }

    void OnOpponentLeft(string reason)
    {
        Debug.Log($"[OnlineMatch] Opponent left: {reason}");
    }

    void BroadcastCoinSnapshot()
    {
        CoinIdentity[] coins = FindObjectsByType<CoinIdentity>(FindObjectsSortMode.None);
        var list = new List<CoinSnapshotMessage>(coins.Length);
        for (int i = 0; i < coins.Length; i++)
        {
            Transform t = coins[i].transform;
            list.Add(new CoinSnapshotMessage
            {
                coinObjectName = coins[i].gameObject.name,
                x = t.position.x,
                y = t.position.y,
                z = t.position.z,
                rotY = t.eulerAngles.y
            });
        }

        OnlineMatchSession.Channel.SendSnapshot(new CoinSnapshotBatchMessage
        {
            coins = list.ToArray(),
            hostUserId = OnlineMatchSession.Channel.LocalUserId
        });
    }
}

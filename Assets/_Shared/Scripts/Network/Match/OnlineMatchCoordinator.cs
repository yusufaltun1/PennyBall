using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Online maçta shot broadcast + skor sync + bot disable.
/// </summary>
public class OnlineMatchCoordinator : MonoBehaviour
{
    public static OnlineMatchCoordinator Instance { get; private set; }

    [SerializeField] float _timeSyncInterval = 0.5f;

    float _timeSyncTimer;
    int _lastSentPlayerGoals = -1;
    int _lastSentOpponentGoals = -1;
    bool _connectInProgress;
    bool _timeSyncBound;
    LeagueMatchController _boundLeagueController;

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
        if (scene.name == GameSceneNames.MainMenu)
        {
            _connectInProgress = false;
            UnsubscribeChannel();
            UnsubscribeRules();
            _timeSyncBound = false;
            PhotonFusionCleanup.ForceShutdownAll();
            return;
        }

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
        if (_connectInProgress)
        {
            return;
        }

        _connectInProgress = true;

        string session = PendingPhotonSession.SessionName;
        string matchId = PendingPhotonSession.MatchId;
        string opponentUserId = PendingPhotonSession.OpponentUserId;
        string opponentName = PendingPhotonSession.OpponentDisplayName;
        int avatar = PendingPhotonSession.OpponentAvatarIndex;
        bool isTest = PendingPhotonSession.IsTestRoom;
        PendingPhotonSession.Clear();

        try
        {
            await PhotonFusionCleanup.ForceShutdownAllAsync();

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
        finally
        {
            _connectInProgress = false;
        }
    }

    void Update()
    {
        if (!OnlineMatchSession.IsOnlineMatch || OnlineMatchSession.Channel == null)
        {
            return;
        }

        OnlineMatchGoalSync.EnsureBound();
        BoosterNetworkSync.EnsureBound();
        EnsureTimeSyncBound();
        EnsureMatchCompletedBound();
        SyncScoreIfNeeded();
        SyncTimeIfNeeded();
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
        MatchShotNetworkRelay.Instance.MatchEndReceived -= OnRelayMatchEndReceived;
        MatchShotNetworkRelay.Instance.MatchEndReceived += OnRelayMatchEndReceived;
        MatchShotNetworkRelay.Instance.ForfeitReceived -= OnRelayForfeitReceived;
        MatchShotNetworkRelay.Instance.ForfeitReceived += OnRelayForfeitReceived;
        _timeSyncBound = true;
    }

    /// <summary>
    /// host perspektifinden gelir (playerGoals/opponentGoals host'a göre) — bize göre ters.
    /// </summary>
    void OnRelayMatchEndReceived(int hostPlayerGoals, int hostOpponentGoals, string reason)
    {
        LeagueMatchController.Instance?.CompleteMatchFromNetwork(hostOpponentGoals, hostPlayerGoals);
    }

    /// <summary>
    /// ExitMatchPanelController'dan karşı taraf "Evet, Çık" ile manuel pes etti — kasıtlı/kesin
    /// bir sinyal olduğu için (bağlantı kopması belirsizliği yok) RemoteOpponentController'ın
    /// 6sn'lik disconnect grace-period'ını hiç beklemeden ANINDA galibiyet alıyoruz.
    /// </summary>
    void OnRelayForfeitReceived()
    {
        LeagueMatchController.Instance?.EndMatchByOpponentDisconnect("opponent_forfeit");
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

    /// <summary>
    /// Host, süresi dolup kendi maçını bitirince SyncTimeIfNeeded artık hiç çalışmaz
    /// (IsMatchActive false olur) — bu yüzden non-host taraf son senkron değeri asla tam
    /// 0'a ulaşmayabilir ve kendi süresi hiç bitmeyebilir (sanki maç bitmemiş gibi takılı
    /// kalır). Host'un maçı bittiğinde bunu açıkça karşı tarafa bildiriyoruz.
    /// </summary>
    void EnsureMatchCompletedBound()
    {
        if (!ReferenceEquals(_boundLeagueController, LeagueMatchController.Instance))
        {
            if (_boundLeagueController != null)
            {
                _boundLeagueController.MatchCompleted -= OnLocalMatchCompleted;
            }

            _boundLeagueController = LeagueMatchController.Instance;
            if (_boundLeagueController != null)
            {
                _boundLeagueController.MatchCompleted += OnLocalMatchCompleted;
            }
        }
    }

    void OnLocalMatchCompleted(MatchResultType result)
    {
        if (OnlineMatchSession.Channel == null || !OnlineMatchSession.Channel.IsHost)
        {
            return;
        }

        int playerGoals = LeagueMatchController.Instance.PlayerGoals;
        int opponentGoals = LeagueMatchController.Instance.OpponentGoals;
        string reason = result.ToString();

        // Asıl teslim RPC_MatchEnd ile olur (TimeSync/Goal ile aynı güvenilir Fusion RPC
        // kanalı) — reliable-data custom kanalı Shared Mode'da bazı bağlantılarda hiç
        // teslim etmiyordu. Eski kanal da yedek olarak gönderilir, zararsız.
        MatchShotNetworkRelay.Instance?.TrySendMatchEnd(playerGoals, opponentGoals, reason);

        OnlineMatchSession.Channel.SendMatchEnd(new MatchEndMessage
        {
            playerGoals = playerGoals,
            opponentGoals = opponentGoals,
            reason = reason
        });
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
        // end.playerGoals/opponentGoals host perspektifinden — bize göre ters.
        LeagueMatchController.Instance?.CompleteMatchFromNetwork(end.opponentGoals, end.playerGoals);
    }

    void OnOpponentLeft(string reason)
    {
        // Maç sonlandırma RemoteOpponentController.OnOpponentLeft'te — burada çift tetiklemeyelim.
        Debug.Log($"[OnlineMatch] Opponent left: {reason}");
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using UnityEngine;

/// <summary>
/// Maç içi sync — Photon Fusion Shared Mode (ReliableData).
/// </summary>
public sealed class PhotonFusionMatchChannel : IMatchRealtimeChannel
{
    readonly NetworkConfig _config;
    readonly string _localUserId;
    readonly HashSet<PlayerRef> _remotePlayers = new HashSet<PlayerRef>();

    NetworkRunner _runner;
    PhotonFusionRunnerCallbacks _callbacks;
    GameObject _runnerObject;
    int _sendSeq;
    bool _relaySpawnRequested;
    NetworkObject _relayPrefab;

    public string MatchId { get; private set; }
    public bool IsConnected { get; private set; }
    public bool IsHost { get; private set; }
    public string LocalUserId => _localUserId;
    public int RemotePlayerCount => CountRemotePlayers();

    public event Action Connected;
    public event Action Disconnected;
    public event Action<ShotIntentMessage> ShotReceived;
    public event Action<ScoreSyncMessage> ScoreReceived;
    public event Action<MatchEndMessage> MatchEndReceived;
    public event Action<CoinSnapshotBatchMessage> SnapshotReceived;
    public event Action<string> OpponentLeft;
    /// <summary>Remote sayısı değişince (matchmaking wait için).</summary>
    public event Action RemotePlayersChanged;

    public PhotonFusionMatchChannel(NetworkConfig config, string localUserId)
    {
        _config = config;
        _localUserId = localUserId ?? "local";
    }

    public async Task JoinAsync(string matchIdOrRoom)
    {
        if (string.IsNullOrWhiteSpace(matchIdOrRoom))
        {
            throw new ArgumentException("Room name required", nameof(matchIdOrRoom));
        }

        ApplyPhotonAppSettings();

        await PhotonFusionCleanup.ForceShutdownAllAsync();
        await Task.Delay(150);

        if (_runner != null && _runner.IsRunning)
        {
            await LeaveAsync();
        }

        _runnerObject = new GameObject("PhotonFusionRunner");
        UnityEngine.Object.DontDestroyOnLoad(_runnerObject);

        _runner = _runnerObject.AddComponent<NetworkRunner>();
        _runner.ProvideInput = false;

        var sceneManager = _runnerObject.AddComponent<NetworkSceneManagerDefault>();
        var objectProvider = _runnerObject.AddComponent<NetworkObjectProviderDefault>();

        _callbacks = _runnerObject.AddComponent<PhotonFusionRunnerCallbacks>();
        _callbacks.Bind(this);
        _runner.AddCallbacks(_callbacks);

        string sessionName = SanitizeSessionName(matchIdOrRoom);
        MatchId = matchIdOrRoom;

        Debug.Log(
            $"[PhotonFusion] StartGame Shared session='{sessionName}' " +
            $"appId={MaskAppId(PhotonAppSettings.Global.AppSettings.AppIdFusion)} " +
            $"region='{PhotonAppSettings.Global.AppSettings.FixedRegion}'");

        // FusionBootstrap ile aynı imza — Scene boş (mevcut sahneyi koru).
        var sceneInfo = new NetworkSceneInfo();
        StartGameResult result = await _runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Shared,
            Address = NetAddress.Any(),
            Scene = sceneInfo,
            SessionName = sessionName,
            PlayerCount = 2,
            SceneManager = sceneManager,
            ObjectProvider = objectProvider,
            AuthValues = CreateAuthValues()
        });

        if (!result.Ok)
        {
            string detail = string.IsNullOrEmpty(result.ErrorMessage)
                ? result.ShutdownReason.ToString()
                : $"{result.ShutdownReason}: {result.ErrorMessage}";
            CleanupRunnerObject();
            throw new InvalidOperationException($"Fusion StartGame failed: {detail}");
        }

        IsConnected = true;
        RefreshHostFlag();
        SnapshotRemotePlayers("join_ok");
        Connected?.Invoke();

        Debug.Log(
            $"[PhotonFusion] Joined OK session='{sessionName}' " +
            $"localPlayer={_runner.LocalPlayer} host={IsHost} user={_localUserId} " +
            $"remotes={RemotePlayerCount} active={CountActivePlayers()} " +
            $"sessionPlayerCount={SafeSessionPlayerCount()}");

        OnlineMatchDiagnostics.LogPhotonJoin(sessionName, _runner);

        // ShotRelay'i burada spawn etme — MainMenu→Game SceneManager.LoadScene
        // NetworkObject'i öldürür / _relaySpawnRequested kilidi takılır.
        // Relay: RefreshAfterSceneLoadAsync (Game sahnesinde).
    }

    /// <summary>Game sahnesi yüklendikten sonra ShotRelay spawn/bind.</summary>
    public async Task RefreshAfterSceneLoadAsync()
    {
        if (_runner == null || !_runner.IsRunning)
        {
            return;
        }

        SnapshotRemotePlayers("scene_load");
        _relaySpawnRequested = false;
        DespawnStaleShotRelay();
        await EnsureShotRelayAsync();
        NotifyRelayHandshake();
    }

    void DespawnStaleShotRelay()
    {
        if (_runner == null || !_runner.IsRunning || !_runner.IsSharedModeMasterClient)
        {
            MatchShotNetworkRelay.ResetStaticState();
            return;
        }

        MatchShotNetworkRelay[] relays = UnityEngine.Object.FindObjectsByType<MatchShotNetworkRelay>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < relays.Length; i++)
        {
            MatchShotNetworkRelay relay = relays[i];
            if (relay == null || relay.Object == null || !relay.Object.IsValid)
            {
                continue;
            }

            Debug.Log("[PhotonFusion] Eski ShotRelay despawn (yeni maç handshake)");
            _runner.Despawn(relay.Object);
        }

        MatchShotNetworkRelay.ResetStaticState();
        _relaySpawnRequested = false;
    }

    void NotifyRelayHandshake()
    {
        OnlineMatchGoalSync.TryBindRelay();
        MatchShotNetworkRelay.Instance?.TrySendClientReady();
        MatchShotNetworkRelay.Instance?.TryEvaluateMatchStart();
    }

    AuthenticationValues CreateAuthValues()
    {
        var auth = new AuthenticationValues
        {
            AuthType = CustomAuthenticationType.None,
            UserId = _localUserId
        };
        return auth;
    }

    public async Task LeaveAsync()
    {
        if (MatchShotNetworkRelay.Instance != null)
        {
            MatchShotNetworkRelay.Instance.ShotReceived -= OnRelayShotReceived;
        }

        if (_runner != null && _runner.IsRunning)
        {
            await _runner.Shutdown();
        }

        CleanupRunnerObject();
        IsConnected = false;
        IsHost = false;
        _remotePlayers.Clear();
        Disconnected?.Invoke();
    }

    public void Dispose()
    {
        _ = LeaveAsync();
    }

    public void SendShot(ShotIntentMessage shot)
    {
        if (shot == null)
        {
            return;
        }

        // Shared Mode: RPC (ReliableData RECV bu setup'ta gelmiyor).
        if (MatchShotNetworkRelay.Instance != null
            && MatchShotNetworkRelay.Instance.TrySend(shot))
        {
            return;
        }

        Debug.LogWarning("[PhotonFusion] ShotRelay hazır değil — ReliableData fallback");
        SendJson(MatchRealtimeOp.ShotIntent, JsonUtility.ToJson(shot));
    }

    public void SendScore(ScoreSyncMessage score) =>
        SendJson(MatchRealtimeOp.ScoreSync, JsonUtility.ToJson(score));

    public void SendMatchEnd(MatchEndMessage end) =>
        SendJson(MatchRealtimeOp.MatchEnd, JsonUtility.ToJson(end));

    public void SendSnapshot(CoinSnapshotBatchMessage batch) =>
        SendJson(MatchRealtimeOp.CoinSnapshot, JsonUtility.ToJson(batch));

    public void SendForfeit(string reason) =>
        SendJson(MatchRealtimeOp.Forfeit, reason ?? "forfeit");

    async Task EnsureShotRelayAsync()
    {
        _relayPrefab = LoadShotRelayPrefab();
        if (_relayPrefab == null)
        {
            Debug.LogError(
                "[PhotonFusion] MatchShotRelay prefab yok. Unity'de bir kez: " +
                "PennyBall → Online → Create MatchShotRelay Prefab");
            return;
        }

        // Sahne geçişi Instance'ı null bırakmış olabilir.
        if (MatchShotNetworkRelay.Instance == null)
        {
            _relaySpawnRequested = false;
        }

        float elapsed = 0f;
        const float timeout = 8f;
        float nextSpawnAttempt = 0f;
        while (MatchShotNetworkRelay.Instance == null && elapsed < timeout)
        {
            if (elapsed >= nextSpawnAttempt)
            {
                TrySpawnShotRelay();
                nextSpawnAttempt = elapsed + 0.5f;
            }

            await Task.Yield();
            elapsed += Time.unscaledDeltaTime;

            // Spawned callback gecikirse sahnede ara
            if (MatchShotNetworkRelay.Instance == null)
            {
                MatchShotNetworkRelay found = UnityEngine.Object.FindFirstObjectByType<MatchShotNetworkRelay>();
                if (found != null)
                {
                    Debug.Log("[PhotonFusion] ShotRelay FindFirstObjectByType ile bulundu — bind");
                    found.ForceRegisterInstance();
                }
            }
        }

        BindShotRelay();

        if (MatchShotNetworkRelay.Instance == null)
        {
            Debug.LogWarning(
                $"[PhotonFusion] ShotRelay timeout — master={_runner != null && _runner.IsSharedModeMasterClient} " +
                $"prefab={(_relayPrefab != null)} requested={_relaySpawnRequested}");
        }
        else
        {
            NotifyRelayHandshake();
        }
    }

    void TrySendMatchStartIfReady()
    {
        NotifyRelayHandshake();
    }

    void TrySpawnShotRelay()
    {
        if (_runner == null || !_runner.IsRunning || _relayPrefab == null)
        {
            return;
        }

        if (MatchShotNetworkRelay.Instance != null)
        {
            return;
        }

        if (!_runner.IsSharedModeMasterClient)
        {
            return;
        }

        // Spawn çağrıldı, Spawned henüz gelmedi — ikinci kopya basma.
        if (_relaySpawnRequested)
        {
            return;
        }

        try
        {
            NetworkObject spawned = _runner.Spawn(_relayPrefab);
            _relaySpawnRequested = true;
            Debug.Log(
                $"[PhotonFusion] ShotRelay spawn (host) result={(spawned != null ? spawned.name : "null")} " +
                $"valid={(spawned != null && spawned.IsValid)}");

            if (spawned == null || !spawned.IsValid)
            {
                _relaySpawnRequested = false;
                Debug.LogError(
                    "[PhotonFusion] ShotRelay Spawn başarısız — Prefab Fusion table'da mı? " +
                    "PennyBall → Online → Create MatchShotRelay Prefab");
                return;
            }

            MatchShotNetworkRelay relay = spawned.GetComponent<MatchShotNetworkRelay>();
            if (relay != null)
            {
                relay.ForceRegisterInstance();
                Debug.Log("[PhotonFusion] ShotRelay Instance hemen register edildi");
            }
        }
        catch (Exception ex)
        {
            _relaySpawnRequested = false;
            Debug.LogError($"[PhotonFusion] ShotRelay Spawn exception: {ex.Message}");
        }
    }

    void BindShotRelay()
    {
        if (MatchShotNetworkRelay.Instance == null)
        {
            return;
        }

        MatchShotNetworkRelay.Instance.ShotReceived -= OnRelayShotReceived;
        MatchShotNetworkRelay.Instance.ShotReceived += OnRelayShotReceived;
        OnlineMatchGoalSync.TryBindRelay();
        Debug.Log("[PhotonFusion] ShotRelay bound");
    }

    void OnRelayShotReceived(ShotIntentMessage shot) => ShotReceived?.Invoke(shot);

    static NetworkObject LoadShotRelayPrefab()
    {
        GameObject go = Resources.Load<GameObject>("MatchShotRelay");
        if (go == null)
        {
            return null;
        }

        return go.GetComponent<NetworkObject>();
    }

    internal void HandleReliableData(PlayerRef player, ReliableKey key, ArraySegment<byte> data)
    {
        if (_runner == null || player == _runner.LocalPlayer)
        {
            return;
        }

        key.GetInts(out int opInt, out int seq, out _, out _);
        var op = (MatchRealtimeOp)opInt;
        string json = Encoding.UTF8.GetString(data.Array, data.Offset, data.Count);

        Debug.Log($"[PhotonFusion] RECV {op} from={player} seq={seq} bytes={data.Count}");
        DispatchOp(op, json);

        // Shared Mode: master, client→server gelen mesajı diğer peer'lara iletir (2p'de no-op).
        if (_runner.IsSharedModeMasterClient && op == MatchRealtimeOp.ShotIntent)
        {
            RelayToOtherRemotes(player, key, data);
        }
    }

    void DispatchOp(MatchRealtimeOp op, string json)
    {
        switch (op)
        {
            case MatchRealtimeOp.ShotIntent:
                ShotReceived?.Invoke(JsonUtility.FromJson<ShotIntentMessage>(json));
                break;
            case MatchRealtimeOp.ScoreSync:
                ScoreReceived?.Invoke(JsonUtility.FromJson<ScoreSyncMessage>(json));
                break;
            case MatchRealtimeOp.MatchEnd:
                MatchEndReceived?.Invoke(JsonUtility.FromJson<MatchEndMessage>(json));
                break;
            case MatchRealtimeOp.CoinSnapshot:
                SnapshotReceived?.Invoke(JsonUtility.FromJson<CoinSnapshotBatchMessage>(json));
                break;
            case MatchRealtimeOp.Forfeit:
                OpponentLeft?.Invoke(json);
                break;
        }
    }

    void RelayToOtherRemotes(PlayerRef source, ReliableKey key, ArraySegment<byte> data)
    {
        byte[] bytes = new byte[data.Count];
        Buffer.BlockCopy(data.Array, data.Offset, bytes, 0, data.Count);

        foreach (PlayerRef player in _runner.ActivePlayers)
        {
            if (player == _runner.LocalPlayer || player == source)
            {
                continue;
            }

            _runner.SendReliableDataToPlayer(player, key, bytes);
        }
    }

    internal void HandlePlayerJoined(PlayerRef player)
    {
        RefreshHostFlag();

        bool isRemote = _runner != null && player != _runner.LocalPlayer;
        if (isRemote)
        {
            _remotePlayers.Add(player);
        }

        Debug.Log(
            $"[PhotonFusion] PlayerJoined {player} remote={isRemote} " +
            $"remotes={_remotePlayers.Count} active={CountActivePlayers()} " +
            $"sessionPlayerCount={SafeSessionPlayerCount()} host={IsHost}");

        FlushPendingSends();
        TrySpawnShotRelay();
        BindShotRelay();
        NotifyRelayHandshake();
        RemotePlayersChanged?.Invoke();
    }

    internal void HandlePlayerLeft(PlayerRef player)
    {
        RefreshHostFlag();
        _remotePlayers.Remove(player);
        RemotePlayersChanged?.Invoke();

        if (_runner != null && player != _runner.LocalPlayer)
        {
            OpponentLeft?.Invoke("opponent_left");
        }
    }

    internal void HandleShutdown(ShutdownReason reason)
    {
        IsConnected = false;
        IsHost = false;
        Disconnected?.Invoke();
        Debug.LogWarning($"[PhotonFusion] Shutdown: {reason}");
    }

    readonly System.Collections.Generic.List<(MatchRealtimeOp op, byte[] bytes, int seq)> _pendingSends = new();

    void SendJson(MatchRealtimeOp op, string json)
    {
        if (_runner == null || !_runner.IsRunning || !IsConnected)
        {
            Debug.LogWarning($"[PhotonFusion] Send skipped (not connected): {op}");
            return;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(json ?? string.Empty);
        _sendSeq++;
        ReliableKey key = ReliableKey.FromInts((int)op, _sendSeq, 0, 0);

        int remoteCount = CountRemotePlayers();
        if (remoteCount == 0)
        {
            _pendingSends.Add((op, bytes, _sendSeq));
            Debug.LogWarning($"[PhotonFusion] Send queued (no remotes yet): {op} seq={_sendSeq}");
            return;
        }

        DeliverBytes(op, key, bytes, _sendSeq);
    }

    void FlushPendingSends()
    {
        if (_pendingSends.Count == 0 || CountRemotePlayers() == 0)
        {
            return;
        }

        for (int i = 0; i < _pendingSends.Count; i++)
        {
            (MatchRealtimeOp op, byte[] bytes, int seq) = _pendingSends[i];
            ReliableKey key = ReliableKey.FromInts((int)op, seq, 0, 0);
            DeliverBytes(op, key, bytes, seq);
        }

        _pendingSends.Clear();
    }

    void DeliverBytes(MatchRealtimeOp op, ReliableKey key, byte[] bytes, int seq)
    {
        int sent = 0;
        foreach (PlayerRef player in _runner.ActivePlayers)
        {
            if (player == _runner.LocalPlayer)
            {
                continue;
            }

            _runner.SendReliableDataToPlayer(player, key, bytes);
            sent++;
        }

        // Shared Mode: client→host yolu (P2P bazen sessiz kalabiliyor).
        if (!_runner.IsSharedModeMasterClient)
        {
            _runner.SendReliableDataToServer(key, bytes);
            Debug.Log($"[PhotonFusion] SEND {op} seq={seq} remotes={sent} +server bytes={bytes.Length}");
            return;
        }

        Debug.Log($"[PhotonFusion] SEND {op} seq={seq} remotes={sent} bytes={bytes.Length}");
    }

    int CountRemotePlayers()
    {
        SnapshotRemotePlayers(null);

        int fromSet = _remotePlayers.Count;
        int fromActive = 0;
        if (_runner != null)
        {
            foreach (PlayerRef player in _runner.ActivePlayers)
            {
                if (player != _runner.LocalPlayer)
                {
                    fromActive++;
                }
            }
        }

        int fromSession = Mathf.Max(0, SafeSessionPlayerCount() - 1);
        return Mathf.Max(fromSet, Mathf.Max(fromActive, fromSession));
    }

    int CountActivePlayers()
    {
        if (_runner == null)
        {
            return 0;
        }

        int count = 0;
        foreach (PlayerRef _ in _runner.ActivePlayers)
        {
            count++;
        }

        return count;
    }

    int SafeSessionPlayerCount()
    {
        if (_runner == null || !_runner.SessionInfo.IsValid)
        {
            return 0;
        }

        return _runner.SessionInfo.PlayerCount;
    }

    void SnapshotRemotePlayers(string reason)
    {
        if (_runner == null)
        {
            return;
        }

        int before = _remotePlayers.Count;
        foreach (PlayerRef player in _runner.ActivePlayers)
        {
            if (player != _runner.LocalPlayer)
            {
                _remotePlayers.Add(player);
            }
        }

        if (reason != null && _remotePlayers.Count != before)
        {
            Debug.Log(
                $"[PhotonFusion] Remote snapshot ({reason}): {_remotePlayers.Count} " +
                $"(active={CountActivePlayers()} session={SafeSessionPlayerCount()})");
            RemotePlayersChanged?.Invoke();
        }
    }

    void RefreshHostFlag()
    {
        IsHost = _runner != null && _runner.IsRunning && _runner.IsSharedModeMasterClient;
    }

    void ApplyPhotonAppSettings()
    {
        string appId = _config != null ? _config.photonFusionAppId : null;
        if (string.IsNullOrEmpty(appId))
        {
            appId = PhotonAppSettings.Global?.AppSettings?.AppIdFusion;
        }

        if (string.IsNullOrEmpty(appId))
        {
            throw new InvalidOperationException(
                "Photon Fusion AppId eksik. NetworkConfig veya PhotonAppSettings'e yaz.");
        }

        PhotonAppSettings.Global.AppSettings.AppIdFusion = appId;
        PhotonAppSettings.Global.AppSettings.FixedRegion =
            _config != null ? (_config.photonRegion ?? string.Empty) : string.Empty;

        if (string.IsNullOrEmpty(PhotonAppSettings.Global.AppSettings.AppVersion))
        {
            PhotonAppSettings.Global.AppSettings.AppVersion = "1.0.0";
        }
    }

    void CleanupRunnerObject()
    {
        if (_runner != null && _callbacks != null)
        {
            _runner.RemoveCallbacks(_callbacks);
        }

        _runner = null;
        _callbacks = null;
        _relaySpawnRequested = false;
        _relayPrefab = null;
        _remotePlayers.Clear();

        if (_runnerObject != null)
        {
            UnityEngine.Object.Destroy(_runnerObject);
            _runnerObject = null;
        }
    }

    static string MaskAppId(string appId)
    {
        if (string.IsNullOrEmpty(appId) || appId.Length < 8)
        {
            return appId;
        }

        return appId.Substring(0, 8) + "...";
    }

    static string SanitizeSessionName(string raw)
    {
        char[] buffer = new char[Mathf.Min(raw.Length, 64)];
        int n = 0;
        for (int i = 0; i < raw.Length && n < buffer.Length; i++)
        {
            char c = raw[i];
            buffer[n++] = char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_';
        }

        string name = new string(buffer, 0, n);
        return string.IsNullOrEmpty(name) ? "pb_" + UnityEngine.Random.Range(100000, 999999) : name;
    }
}

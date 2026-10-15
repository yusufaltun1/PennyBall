using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Shared Mode sync relay — shot / ready+match-start / goal+round-reset (RPC).
/// </summary>
public class MatchShotNetworkRelay : NetworkBehaviour
{
    public static MatchShotNetworkRelay Instance { get; private set; }

    public System.Action<ShotIntentMessage> ShotReceived;
    public System.Action<ShotRollbackMessage> ShotRollbackReceived;
    public System.Action MatchStartReceived;
    /// <summary>hostScored: host'un takımı mı attı (true) yoksa misafirin golü host tarafından
    /// onaylandı mı (false) — tek gönderen her zaman host'tur (bkz. RPC_Goal).</summary>
    public System.Action<bool, int> GoalReceived;
    public System.Action RoundResetReceived;
    public System.Action<float, bool> TimeSyncReceived;
    public System.Action<int, int, string> MatchEndReceived;
    public System.Action<int, float, float, float, float, float, float, float, float, float, float, float, float> CoinSnapshotReceived;
    /// <summary>Gönderen, karşı tarafı dondurma booster'ı kullandı — alıcı KENDİ CoinTeam.Player
    /// paralarını dondurmalı (bkz. BoosterNetworkSync).</summary>
    public System.Action<float> BoosterFreezeReceived;
    /// <summary>Gönderen uygulamayı arka plana aldı (true) / geri döndü (false) — alıcı
    /// (bekleyen taraf) bunu 10sn'lik hükmen mağlup geri sayımını başlatmak/iptal etmek için
    /// kullanır (bkz. BackgroundForfeitController).</summary>
    public System.Action<bool> OpponentBackgroundStateReceived;
    /// <summary>Gönderen (bekleyen taraf), karşı tarafın 10sn arka planda kaldığını tespit edip
    /// hükmen mağlubiyeti ilan etti — alıcı (arka plana alan taraf) KENDİ tarafında zorunlu
    /// Loss uygulamalı (bkz. LeagueMatchController.ForfeitActiveMatch). Gönderen aynı anda kendi
    /// tarafında zorunlu Win uygular (EndMatchByOpponentDisconnect) — host/misafir simetrik,
    /// host-only MatchEnd akışına (OnLocalMatchCompleted sadece host'ta gönderir) bağımlı değil.</summary>
    public System.Action BackgroundForfeitReceived;
    /// <summary>Gönderen "Çık" (ExitMatchPanelController) ile maçtan manuel pes etti — alıcı
    /// (karşı taraf) ANINDA galibiyet almalı, bekleme gerekmez (kasıtlı/kesin bir sinyal, 6sn'lik
    /// disconnect grace-period'ına gerek yok). Eskiden sadece custom reliable-data kanalından
    /// (IMatchRealtimeChannel.SendForfeit) gönderiliyordu — o kanal Shared Mode'da bazı
    /// bağlantılarda hiç teslim etmiyor (bu oturumda shot/goal/snapshot/match-end için de aynı
    /// sorun bulunup hepsi bu RPC kanalına taşınmıştı, forfeit unutulmuştu).</summary>
    public System.Action ForfeitReceived;

    readonly HashSet<PlayerRef> _readyPlayers = new HashSet<PlayerRef>();
    int _goalSeq;
    // RPC_RoundReset dedup sırası — SADECE authority tarafından atanır (ScheduleRoundReset).
    // _goalSeq'ten bilerek ayrı: _goalSeq her istemcide bağımsız arttığı için iki farklı
    // oyuncunun golleri aynı seq'i üretebilir (örn. ikisi de ilk golünde seq=1) ve bu da
    // aşağıdaki seq<=_lastRoundResetSeq korumasının ikinci reset'i sessizce düşürüp
    // _goalSequenceActive'i kalıcı kilitli bırakmasına yol açardı (1-1'de donma bug'ı).
    int _roundResetSeq;
    int _lastRoundResetSeq = -1;
    bool _matchStartSent;
    float _spawnedAt;
    Coroutine _delayedResetRoutine;

    const float GoalToResetDelaySeconds = 2.4f;

    public override void Spawned()
    {
        Instance = this;
        _readyPlayers.Clear();
        _matchStartSent = false;
        _spawnedAt = Time.unscaledTime;
        Debug.Log(
            $"[ShotRelay] Spawned HasStateAuthority={Object.HasStateAuthority} " +
            $"master={Runner != null && Runner.IsSharedModeMasterClient} local={Runner?.LocalPlayer}");

        TrySendClientReady();
        TryEvaluateMatchStart();
    }

    public void ForceRegisterInstance()
    {
        Instance = this;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (_delayedResetRoutine != null)
        {
            StopCoroutine(_delayedResetRoutine);
            _delayedResetRoutine = null;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>Maçlar arası runner kapanırken relay static referansını sıfırla.</summary>
    public static void ResetStaticState()
    {
        Instance = null;
    }

    public bool TrySend(ShotIntentMessage shot)
    {
        if (shot == null || Object == null || !Object.IsValid)
        {
            return false;
        }

        RPC_ApplyShot(
            shot.coinObjectName ?? string.Empty,
            shot.dirX,
            shot.dirZ,
            shot.pullDistance,
            shot.seq,
            shot.posX,
            shot.posY,
            shot.posZ,
            shot.hasPosition,
            shot.senderUserId ?? string.Empty);

        Debug.Log($"[ShotRelay] RPC_Send {shot.coinObjectName} seq={shot.seq}");
        return true;
    }

    public bool TrySendShotRollback(ShotRollbackMessage rollback)
    {
        if (rollback == null || Object == null || !Object.IsValid)
        {
            return false;
        }

        RPC_ApplyShotRollback(
            rollback.coinObjectName ?? string.Empty,
            rollback.posX,
            rollback.posY,
            rollback.posZ,
            rollback.seq,
            rollback.senderUserId ?? string.Empty);

        Debug.Log($"[ShotRelay] RPC_Send Rollback {rollback.coinObjectName} seq={rollback.seq}");
        return true;
    }

    public bool TrySendClientReady()
    {
        if (Object == null || !Object.IsValid || Runner == null)
        {
            return false;
        }

        RPC_ClientReady(Runner.LocalPlayer);
        return true;
    }

    public bool TrySendMatchStart()
    {
        if (Object == null || !Object.IsValid)
        {
            return false;
        }

        if (_matchStartSent)
        {
            return true;
        }

        // Shared Mode: StateAuthority yoksa RPC_MatchStart (StateAuthority source) sessizce düşerdi.
        // Master client RPC'yi All source ile basar.
        if (!IsMatchStartAuthority())
        {
            return false;
        }

        _matchStartSent = true;
        RPC_MatchStart();
        Debug.Log(
            $"[ShotRelay] RPC_MatchStart auth={Object.HasStateAuthority} " +
            $"master={Runner != null && Runner.IsSharedModeMasterClient}");
        return true;
    }

    /// <summary>
    /// 2 oyuncu odadaysa başlat.
    /// Ready handshake tercih edilir; 2 sn sonra Ready eksik olsa bile host zorlar.
    /// </summary>
    public void TryEvaluateMatchStart()
    {
        if (_matchStartSent || Object == null || !Object.IsValid || Runner == null)
        {
            return;
        }

        if (!IsMatchStartAuthority())
        {
            return;
        }

        int activeCount = CountActivePlayers();
        if (activeCount < 2)
        {
            return;
        }

        bool allReady = AllActivePlayersReady();
        bool enoughReady = _readyPlayers.Count >= 2;
        float sinceSpawn = Time.unscaledTime - _spawnedAt;
        bool forceAfterDelay = sinceSpawn >= 2f;

        if (!allReady && !enoughReady && !forceAfterDelay)
        {
            return;
        }

        Debug.Log(
            $"[ShotRelay] EvaluateMatchStart OK active={activeCount} ready={_readyPlayers.Count} " +
            $"allReady={allReady} force={forceAfterDelay && !allReady}");
        TrySendMatchStart();
    }

    void Update()
    {
        if (_matchStartSent || Object == null || !Object.IsValid)
        {
            return;
        }

        // Host: periyodik Ready + evaluate (RPC kaybı / geç join toparlansın).
        if (IsMatchStartAuthority() && Time.frameCount % 30 == 0)
        {
            TrySendClientReady();
            TryEvaluateMatchStart();
        }
    }

    bool IsMatchStartAuthority()
    {
        return Object.HasStateAuthority
            || (Runner != null && Runner.IsSharedModeMasterClient);
    }

    int CountActivePlayers()
    {
        if (Runner == null)
        {
            return 0;
        }

        int count = 0;
        foreach (PlayerRef _ in Runner.ActivePlayers)
        {
            count++;
        }

        return count;
    }

    bool AllActivePlayersReady()
    {
        if (Runner == null)
        {
            return false;
        }

        int activeCount = 0;
        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            activeCount++;
            if (!_readyPlayers.Contains(player))
            {
                return false;
            }
        }

        return activeCount >= 2;
    }

    /// <summary>
    /// Tek gönderen host'tur (RpcSources.StateAuthority) — hostScored=true host'un kendi golü
    /// (otoriter, doğrulamaya gerek yok), hostScored=false misafirin golü host tarafından
    /// onaylandığı anlamına gelir (bkz. HostPhysicsAuthority gol hakemliği).
    /// </summary>
    public bool TrySendGoal(bool hostScored, int shotSeq)
    {
        if (Object == null || !Object.IsValid)
        {
            return false;
        }

        _goalSeq++;
        RPC_Goal(hostScored, _goalSeq, shotSeq);
        Debug.Log($"[ShotRelay] RPC_Goal hostScored={hostScored} seq={_goalSeq} shotSeq={shotSeq}");

        if (Object.HasStateAuthority)
        {
            ScheduleRoundReset();
        }

        return true;
    }

    void ScheduleRoundReset()
    {
        if (_delayedResetRoutine != null)
        {
            StopCoroutine(_delayedResetRoutine);
        }

        int resetSeq = ++_roundResetSeq;
        _delayedResetRoutine = StartCoroutine(DelayedRoundResetRoutine(resetSeq));
    }

    IEnumerator DelayedRoundResetRoutine(int seq)
    {
        yield return new WaitForSecondsRealtime(GoalToResetDelaySeconds);
        _delayedResetRoutine = null;
        RPC_RoundReset(seq);
        Debug.Log($"[ShotRelay] RPC_RoundReset seq={seq}");
    }

    /// <summary>
    /// Host-authoritative fizik: 6 paranın X/Z pozisyonunu (Y sabit, Rigidbody constraint'i ile
    /// donuk) misafire yayınlar — bkz. HostPhysicsAuthority. Gönderen (host) kendi yerel
    /// adlandırmasıyla gönderir ("Coin_P1.."Coin_E3"), alıcı RemoteOpponentController.FlipTeamToken
    /// + Z ekseni negatif çevirmesiyle kendi yerel koordinatına uygular (aynalanmış tahtalar).
    /// </summary>
    public bool TrySendCoinSnapshot(int batchSeq,
        float p1x, float p1z, float p2x, float p2z, float p3x, float p3z,
        float e1x, float e1z, float e2x, float e2z, float e3x, float e3z)
    {
        if (Object == null || !Object.IsValid)
        {
            return false;
        }

        RPC_CoinSnapshot(batchSeq, p1x, p1z, p2x, p2z, p3x, p3z, e1x, e1z, e2x, e2z, e3x, e3z);
        return true;
    }

    /// <summary>Dondurma booster'ını karşı tarafa bildirir — alıcı kendi paralarını dondurur.</summary>
    public bool TrySendBoosterFreeze(float durationSeconds)
    {
        if (Object == null || !Object.IsValid)
        {
            return false;
        }

        RPC_BoosterFreeze(durationSeconds);
        return true;
    }

    public bool TrySendTimeSync(float remainingSeconds, bool paused)
    {
        if (Object == null || !Object.IsValid)
        {
            return false;
        }

        RPC_TimeSync(remainingSeconds, paused);
        return true;
    }

    public bool TrySendBackgroundState(bool backgrounded)
    {
        if (Object == null || !Object.IsValid)
        {
            return false;
        }

        RPC_PlayerBackground(backgrounded);
        return true;
    }

    public bool TrySendBackgroundForfeit()
    {
        if (Object == null || !Object.IsValid)
        {
            return false;
        }

        RPC_BackgroundForfeit();
        return true;
    }

    /// <summary>ExitMatchPanelController, "Evet, Çık" onaylanınca çağırır.</summary>
    public bool TrySendForfeit()
    {
        if (Object == null || !Object.IsValid)
        {
            return false;
        }

        RPC_Forfeit();
        Debug.Log("[ShotRelay] RPC_Send Forfeit");
        return true;
    }

    /// <summary>
    /// Maç sonu, host→guest'e özel reliable-data kanalı yerine bu RPC ile gönderilir:
    /// o kanal Shared Mode'da bazı bağlantılarda hiç teslim etmiyor (TimeSync/Goal gibi
    /// RPC'ler sorunsuz çalışırken), guest'in süresi host'un senkronu kesilince asla 0'a
    /// inmeyip maçın karşı tarafta hiç bitmemesine yol açıyordu.
    /// </summary>
    public bool TrySendMatchEnd(int playerGoals, int opponentGoals, string reason)
    {
        if (Object == null || !Object.IsValid)
        {
            return false;
        }

        RPC_MatchEnd(playerGoals, opponentGoals, reason ?? string.Empty);
        return true;
    }

    [Rpc(RpcSources.All, RpcTargets.All, InvokeLocal = true)]
    void RPC_ClientReady(PlayerRef player)
    {
        _readyPlayers.Add(player);
        Debug.Log(
            $"[ShotRelay] Ready player={player} ready={_readyPlayers.Count} " +
            $"active={CountActivePlayers()}");

        TryEvaluateMatchStart();
    }

    [Rpc(RpcSources.All, RpcTargets.All, InvokeLocal = false)]
    void RPC_ApplyShot(
        string coinObjectName,
        float dirX,
        float dirZ,
        float pullDistance,
        int seq,
        float posX,
        float posY,
        float posZ,
        NetworkBool hasPosition,
        string senderUserId)
    {
        var msg = new ShotIntentMessage
        {
            coinObjectName = coinObjectName,
            dirX = dirX,
            dirZ = dirZ,
            pullDistance = pullDistance,
            seq = seq,
            posX = posX,
            posY = posY,
            posZ = posZ,
            hasPosition = hasPosition,
            senderUserId = senderUserId
        };

        Debug.Log($"[ShotRelay] RPC_Recv shot {coinObjectName} seq={seq}");
        ShotReceived?.Invoke(msg);
    }

    [Rpc(RpcSources.All, RpcTargets.All, InvokeLocal = false)]
    void RPC_ApplyShotRollback(
        string coinObjectName,
        float posX,
        float posY,
        float posZ,
        int seq,
        string senderUserId)
    {
        var msg = new ShotRollbackMessage
        {
            coinObjectName = coinObjectName,
            posX = posX,
            posY = posY,
            posZ = posZ,
            seq = seq,
            senderUserId = senderUserId
        };

        Debug.Log($"[ShotRelay] RPC_Recv Rollback {coinObjectName} seq={seq}");
        ShotRollbackReceived?.Invoke(msg);
    }

    [Rpc(RpcSources.All, RpcTargets.All, InvokeLocal = true)]
    void RPC_MatchStart()
    {
        Debug.Log("[ShotRelay] RPC_Recv MatchStart");
        OnlineMatchSession.AuthorizeMatchPlay();
        MatchStartReceived?.Invoke();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, InvokeLocal = false)]
    void RPC_Goal(NetworkBool hostScored, int seq, int shotSeq)
    {
        Debug.Log($"[ShotRelay] RPC_Recv Goal hostScored={hostScored} seq={seq} shotSeq={shotSeq}");
        GoalReceived?.Invoke(hostScored, shotSeq);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, InvokeLocal = true)]
    void RPC_RoundReset(int seq)
    {
        if (seq <= _lastRoundResetSeq)
        {
            return;
        }

        _lastRoundResetSeq = seq;
        Debug.Log($"[ShotRelay] RPC_Recv RoundReset seq={seq}");
        RoundResetReceived?.Invoke();
    }

    [Rpc(RpcSources.All, RpcTargets.All, InvokeLocal = false)]
    void RPC_TimeSync(float remainingSeconds, NetworkBool paused)
    {
        TimeSyncReceived?.Invoke(remainingSeconds, paused);
    }

    [Rpc(RpcSources.All, RpcTargets.All, InvokeLocal = false)]
    void RPC_MatchEnd(int playerGoals, int opponentGoals, string reason)
    {
        Debug.Log($"[ShotRelay] RPC_Recv MatchEnd playerGoals={playerGoals} opponentGoals={opponentGoals} reason={reason}");
        MatchEndReceived?.Invoke(playerGoals, opponentGoals, reason);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, InvokeLocal = false)]
    void RPC_CoinSnapshot(int batchSeq,
        float p1x, float p1z, float p2x, float p2z, float p3x, float p3z,
        float e1x, float e1z, float e2x, float e2z, float e3x, float e3z)
    {
        CoinSnapshotReceived?.Invoke(batchSeq, p1x, p1z, p2x, p2z, p3x, p3z, e1x, e1z, e2x, e2z, e3x, e3z);
    }

    [Rpc(RpcSources.All, RpcTargets.All, InvokeLocal = false)]
    void RPC_BoosterFreeze(float durationSeconds)
    {
        Debug.Log($"[ShotRelay] RPC_Recv BoosterFreeze duration={durationSeconds}");
        BoosterFreezeReceived?.Invoke(durationSeconds);
    }

    [Rpc(RpcSources.All, RpcTargets.All, InvokeLocal = false)]
    void RPC_PlayerBackground(NetworkBool backgrounded)
    {
        Debug.Log($"[ShotRelay] RPC_Recv PlayerBackground backgrounded={backgrounded}");
        OpponentBackgroundStateReceived?.Invoke(backgrounded);
    }

    [Rpc(RpcSources.All, RpcTargets.All, InvokeLocal = false)]
    void RPC_BackgroundForfeit()
    {
        Debug.Log("[ShotRelay] RPC_Recv BackgroundForfeit");
        BackgroundForfeitReceived?.Invoke();
    }

    [Rpc(RpcSources.All, RpcTargets.All, InvokeLocal = false)]
    void RPC_Forfeit()
    {
        Debug.Log("[ShotRelay] RPC_Recv Forfeit");
        ForfeitReceived?.Invoke();
    }
}

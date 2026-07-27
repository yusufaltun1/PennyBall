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
    public System.Action<bool> GoalReceived; // true = gönderen gol attı → alıcı gol yedi
    public System.Action RoundResetReceived;
    public System.Action<float, bool> TimeSyncReceived;

    readonly HashSet<PlayerRef> _readyPlayers = new HashSet<PlayerRef>();
    int _goalSeq;
    int _lastRoundResetSeq = -1;
    bool _matchStartSent;
    Coroutine _delayedResetRoutine;

    const float GoalToResetDelaySeconds = 2.4f;

    public override void Spawned()
    {
        Instance = this;
        _readyPlayers.Clear();
        _matchStartSent = false;
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

        if (!IsMatchStartAuthority())
        {
            return false;
        }

        _matchStartSent = true;
        RPC_MatchStart();
        Debug.Log("[ShotRelay] RPC_MatchStart");
        return true;
    }

    /// <summary>Master: odadaki tüm oyuncular Ready ise MatchStart gönder.</summary>
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

        if (!AllActivePlayersReady())
        {
            return;
        }

        Debug.Log($"[ShotRelay] EvaluateMatchStart OK active={activeCount} ready={_readyPlayers.Count}");
        TrySendMatchStart();
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

    public bool TrySendGoal(bool localPlayerScored)
    {
        if (Object == null || !Object.IsValid)
        {
            return false;
        }

        _goalSeq++;
        RPC_Goal(localPlayerScored, _goalSeq);
        Debug.Log($"[ShotRelay] RPC_Goal scored={localPlayerScored} seq={_goalSeq}");

        if (Object.HasStateAuthority)
        {
            ScheduleRoundReset(_goalSeq);
        }

        return true;
    }

    void ScheduleRoundReset(int seq)
    {
        if (_delayedResetRoutine != null)
        {
            StopCoroutine(_delayedResetRoutine);
        }

        _delayedResetRoutine = StartCoroutine(DelayedRoundResetRoutine(seq));
    }

    IEnumerator DelayedRoundResetRoutine(int seq)
    {
        yield return new WaitForSecondsRealtime(GoalToResetDelaySeconds);
        _delayedResetRoutine = null;
        RPC_RoundReset(seq);
        Debug.Log($"[ShotRelay] RPC_RoundReset seq={seq}");
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

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, InvokeLocal = true)]
    void RPC_MatchStart()
    {
        Debug.Log("[ShotRelay] RPC_Recv MatchStart");
        OnlineMatchSession.AuthorizeMatchPlay();
        MatchStartReceived?.Invoke();
    }

    [Rpc(RpcSources.All, RpcTargets.All, InvokeLocal = false)]
    void RPC_Goal(NetworkBool senderScored, int seq)
    {
        Debug.Log($"[ShotRelay] RPC_Recv Goal senderScored={senderScored} seq={seq}");
        GoalReceived?.Invoke(senderScored);

        if (Object.HasStateAuthority && senderScored)
        {
            ScheduleRoundReset(seq);
        }
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
}

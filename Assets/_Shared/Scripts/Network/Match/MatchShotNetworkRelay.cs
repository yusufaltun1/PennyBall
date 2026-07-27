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
    public System.Action MatchStartReceived;
    public System.Action<bool> GoalReceived; // true = gönderen gol attı → alıcı gol yedi
    public System.Action RoundResetReceived;
    public System.Action<float, bool> TimeSyncReceived;

    readonly HashSet<int> _readyPlayerIds = new HashSet<int>();
    int _goalSeq;
    int _lastRoundResetSeq = -1;
    bool _matchStartSent;
    Coroutine _delayedResetRoutine;

    const float GoalToResetDelaySeconds = 2.4f;

    public override void Spawned()
    {
        Instance = this;
        _readyPlayerIds.Clear();
        _matchStartSent = false;
        Debug.Log($"[ShotRelay] Spawned HasStateAuthority={Object.HasStateAuthority} local={Runner.LocalPlayer}");
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

        if (!Object.HasStateAuthority)
        {
            return false;
        }

        _matchStartSent = true;
        RPC_MatchStart();
        Debug.Log("[ShotRelay] RPC_MatchStart");
        return true;
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

        // Master kendi golü: reset zamanla. Client golü: master RPC_Goal handler'da zamanlar.
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
        _readyPlayerIds.Add(player.PlayerId);
        Debug.Log($"[ShotRelay] Ready player={player} count={_readyPlayerIds.Count}");

        if (Object.HasStateAuthority && !_matchStartSent && _readyPlayerIds.Count >= 2)
        {
            TrySendMatchStart();
        }
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

        // Client gol attı → master burada reset zamanlar (kendi golünde TrySendGoal zamanlar).
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

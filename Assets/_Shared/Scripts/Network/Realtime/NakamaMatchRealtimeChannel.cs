using System;
using System.Text;
using System.Threading.Tasks;
using Nakama;
using UnityEngine;

/// <summary>
/// Photon Fusion SDK yokken / proto için Nakama realtime match kanalı.
/// Fusion AppId + PENNYBALL_FUSION tanımlanınca PhotonFusionMatchChannel tercih edilir.
/// </summary>
public sealed class NakamaMatchRealtimeChannel : IMatchRealtimeChannel
{
    readonly NakamaAuthService _auth;
    IMatch _match;
    bool _isHost;

    public string MatchId { get; private set; }
    public bool IsConnected => _match != null && _auth?.Socket != null && _auth.Socket.IsConnected;
    public bool IsHost => _isHost;
    public string LocalUserId => _auth?.UserId;
    public int RemotePlayerCount => 0;

    public event Action Connected;
    public event Action Disconnected;
    public event Action<ShotIntentMessage> ShotReceived;
    public event Action<ScoreSyncMessage> ScoreReceived;
    public event Action<MatchEndMessage> MatchEndReceived;
    public event Action<CoinSnapshotBatchMessage> SnapshotReceived;
    public event Action<string> OpponentLeft;

    public NakamaMatchRealtimeChannel(NakamaAuthService auth)
    {
        _auth = auth;
    }

    public async Task JoinAsync(string matchIdOrRoom)
    {
        if (_auth == null)
        {
            throw new InvalidOperationException("Nakama auth missing");
        }

        await _auth.EnsureSocketAsync();
        ISocket socket = _auth.Socket;

        socket.ReceivedMatchState += OnMatchState;
        socket.ReceivedMatchPresence += OnMatchPresence;

        // Matchmaker sonucu varsa doğru join API'si budur.
        if (NakamaMatchmakerPending.LastMatched != null
            && (string.IsNullOrEmpty(matchIdOrRoom)
                || NakamaMatchmakerPending.LastMatched.MatchId == matchIdOrRoom
                || NakamaMatchmakerPending.LastMatched.Token == matchIdOrRoom))
        {
            IMatchmakerMatched pending = NakamaMatchmakerPending.LastMatched;
            NakamaMatchmakerPending.LastMatched = null;
            _match = await socket.JoinMatchAsync(pending);
        }
        else
        {
            try
            {
                _match = await socket.JoinMatchAsync(matchIdOrRoom);
            }
            catch
            {
                _match = await socket.CreateMatchAsync(matchIdOrRoom);
            }
        }

        MatchId = _match.Id;
        _isHost = DetermineHost(_match);
        Connected?.Invoke();
    }

    public async Task LeaveAsync()
    {
        if (_auth?.Socket == null || _match == null)
        {
            return;
        }

        _auth.Socket.ReceivedMatchState -= OnMatchState;
        _auth.Socket.ReceivedMatchPresence -= OnMatchPresence;

        try
        {
            await _auth.Socket.LeaveMatchAsync(_match);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Realtime] Leave failed: {ex.Message}");
        }

        _match = null;
        Disconnected?.Invoke();
    }

    public void Dispose()
    {
        _ = LeaveAsync();
    }

    public void SendShot(ShotIntentMessage shot)
    {
        SendJson(MatchRealtimeOp.ShotIntent, JsonUtility.ToJson(shot));
    }

    public void SendScore(ScoreSyncMessage score)
    {
        SendJson(MatchRealtimeOp.ScoreSync, JsonUtility.ToJson(score));
    }

    public void SendMatchEnd(MatchEndMessage end)
    {
        SendJson(MatchRealtimeOp.MatchEnd, JsonUtility.ToJson(end));
    }

    public void SendSnapshot(CoinSnapshotBatchMessage batch)
    {
        SendJson(MatchRealtimeOp.CoinSnapshot, JsonUtility.ToJson(batch));
    }

    public void SendForfeit(string reason)
    {
        SendJson(MatchRealtimeOp.Forfeit, reason ?? "forfeit");
    }

    void SendJson(MatchRealtimeOp op, string json)
    {
        if (!IsConnected)
        {
            return;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(json);
        _ = _auth.Socket.SendMatchStateAsync(_match.Id, (long)op, bytes);
    }

    void OnMatchState(IMatchState state)
    {
        if (_match == null || state.MatchId != _match.Id)
        {
            return;
        }

        if (state.UserPresence != null && state.UserPresence.UserId == LocalUserId)
        {
            return;
        }

        string json = Encoding.UTF8.GetString(state.State);
        var op = (MatchRealtimeOp)state.OpCode;

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

    void OnMatchPresence(IMatchPresenceEvent presence)
    {
        if (_match == null || presence.MatchId != _match.Id)
        {
            return;
        }

        if (presence.Leaves == null)
        {
            return;
        }

        foreach (IUserPresence left in presence.Leaves)
        {
            if (left.UserId != LocalUserId)
            {
                OpponentLeft?.Invoke("opponent_left");
            }
        }
    }

    static bool DetermineHost(IMatch match)
    {
        // En düşük userId host kabul edilir (deterministik).
        string lowest = null;
        foreach (IUserPresence presence in match.Presences)
        {
            if (lowest == null || string.CompareOrdinal(presence.UserId, lowest) < 0)
            {
                lowest = presence.UserId;
            }
        }

        return lowest != null && match.Self != null && lowest == match.Self.UserId;
    }
}

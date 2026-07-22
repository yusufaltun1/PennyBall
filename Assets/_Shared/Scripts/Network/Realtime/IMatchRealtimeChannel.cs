using System;
using UnityEngine;

[Serializable]
public class ShotIntentMessage
{
    public string senderUserId;
    public string coinObjectName;
    public float dirX;
    public float dirZ;
    public float pullDistance;
    public int seq;
    public float posX;
    public float posY;
    public float posZ;
    public bool hasPosition;

    public Vector3 Direction => new(dirX, 0f, dirZ);
    public Vector3 Position => new(posX, posY, posZ);
}

[Serializable]
public struct ScoreSyncMessage
{
    public int playerGoals;
    public int opponentGoals;
    public string hostUserId;
}

[Serializable]
public struct MatchEndMessage
{
    public int playerGoals;
    public int opponentGoals;
    public string reason;
}

[Serializable]
public struct CoinSnapshotMessage
{
    public string coinObjectName;
    public float x;
    public float y;
    public float z;
    public float rotY;
}

[Serializable]
public struct CoinSnapshotBatchMessage
{
    public CoinSnapshotMessage[] coins;
    public string hostUserId;
}

public enum MatchRealtimeOp : byte
{
    ShotIntent = 1,
    ScoreSync = 2,
    MatchEnd = 3,
    CoinSnapshot = 4,
    TurnGrant = 5,
    Forfeit = 6,
    Ready = 7
}

public interface IMatchRealtimeChannel : IDisposable
{
    string MatchId { get; }
    bool IsConnected { get; }
    bool IsHost { get; }
    string LocalUserId { get; }
    int RemotePlayerCount { get; }

    event Action Connected;
    event Action Disconnected;
    event Action<ShotIntentMessage> ShotReceived;
    event Action<ScoreSyncMessage> ScoreReceived;
    event Action<MatchEndMessage> MatchEndReceived;
    event Action<CoinSnapshotBatchMessage> SnapshotReceived;
    event Action<string> OpponentLeft;

    System.Threading.Tasks.Task JoinAsync(string matchIdOrRoom);
    System.Threading.Tasks.Task LeaveAsync();

    void SendShot(ShotIntentMessage shot);
    void SendScore(ScoreSyncMessage score);
    void SendMatchEnd(MatchEndMessage end);
    void SendSnapshot(CoinSnapshotBatchMessage batch);
    void SendForfeit(string reason);
}

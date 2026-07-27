using Fusion;
using Fusion.Photon.Realtime;
using UnityEngine;

/// <summary>
/// Online maç takılma teşhisi — farklı cihaz / region / oda adı sorunlarını loglar.
/// </summary>
public static class OnlineMatchDiagnostics
{
    static float _nextLogAt;

    public static void LogWaitingState(string context)
    {
        if (Time.unscaledTime < _nextLogAt)
        {
            return;
        }

        _nextLogAt = Time.unscaledTime + 2f;

        string room = OnlineMatchSession.PhotonRoomName
            ?? PendingPhotonSession.SessionName
            ?? MatchSessionContext.OnlineMatchId
            ?? "-";

        int remotes = -1;
        bool connected = false;
        bool isHost = false;
        string region = ResolvePhotonRegion();

        if (OnlineMatchSession.Channel != null)
        {
            remotes = OnlineMatchSession.Channel.RemotePlayerCount;
            connected = OnlineMatchSession.Channel.IsConnected;
            isHost = OnlineMatchSession.Channel.IsHost;
        }

        Debug.Log(
            $"[OnlineDiag/{context}] room='{room}' region='{region}' " +
            $"connected={connected} host={isHost} remotes={remotes} " +
            $"relay={(MatchShotNetworkRelay.Instance != null)} " +
            $"authorized={OnlineMatchSession.MatchPlayAuthorized} " +
            $"matchmaking={OnlineFeatureFlags.OnlineMatchmakingEnabled}");
    }

    public static void LogPhotonJoin(string room, NetworkRunner runner)
    {
        string region = runner?.SessionInfo.IsValid == true
            ? runner.SessionInfo.Region
            : ResolvePhotonRegion();

        int players = runner?.SessionInfo.IsValid == true
            ? runner.SessionInfo.PlayerCount
            : 0;

        Debug.Log(
            $"[OnlineDiag] Photon joined room='{room}' region='{region}' " +
            $"sessionPlayers={players} local={runner?.LocalPlayer}");
    }

    static string ResolvePhotonRegion()
    {
        NetworkConfig config = NetworkConfig.LoadOrCreateDefaults();
        if (!string.IsNullOrEmpty(config.photonRegion))
        {
            return config.photonRegion;
        }

        return PhotonAppSettings.Global?.AppSettings?.FixedRegion ?? "auto";
    }
}

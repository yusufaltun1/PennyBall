using System;
using UnityEngine;

/// <summary>
/// Aktif online maç oturumu (matchmaking sonucu + realtime kanal).
/// </summary>
public static class OnlineMatchSession
{
    public static bool IsOnlineMatch { get; private set; }
    public static string MatchId { get; private set; }
    public static string PhotonRoomName { get; private set; }
    public static string OpponentUserId { get; private set; }
    public static string OpponentDisplayName { get; private set; }
    public static int OpponentAvatarIndex { get; private set; }
    public static IMatchRealtimeChannel Channel { get; private set; }

    /// <summary>İki oyuncu bağlandı + MatchStart RPC — countdown/timer bundan sonra akar.</summary>
    public static bool MatchPlayAuthorized { get; private set; }

    public static event Action SessionStarted;
    public static event Action SessionCleared;
    public static event Action MatchPlayAuthorizedChanged;

    public static void BeginHumanMatch(MatchmakingResult result, IMatchRealtimeChannel channel)
    {
        IsOnlineMatch = true;
        MatchId = result.MatchId;
        PhotonRoomName = result.PhotonRoomName;
        OpponentUserId = result.OpponentUserId;
        OpponentDisplayName = result.OpponentDisplayName;
        OpponentAvatarIndex = result.OpponentAvatarIndex;
        Channel = channel;
        MatchPlayAuthorized = false;
        SessionStarted?.Invoke();
    }

    public static void BeginTestRoom(string matchId, IMatchRealtimeChannel channel, string opponentName = "Test Opponent")
    {
        IsOnlineMatch = true;
        MatchId = matchId;
        PhotonRoomName = matchId;
        OpponentUserId = "test";
        OpponentDisplayName = opponentName;
        OpponentAvatarIndex = 0;
        Channel = channel;
        MatchPlayAuthorized = false;
        SessionStarted?.Invoke();
    }

    public static void AuthorizeMatchPlay()
    {
        if (MatchPlayAuthorized)
        {
            return;
        }

        MatchPlayAuthorized = true;
        Debug.Log("[OnlineMatch] MatchPlayAuthorized — countdown/timer başlayabilir.");
        MatchPlayAuthorizedChanged?.Invoke();
    }

    /// <summary>Game sahnesine girince 3-2-1'i Ready/MatchStart'a kadar kilitler.</summary>
    public static void RevokeMatchPlayAuthorization()
    {
        if (!MatchPlayAuthorized)
        {
            return;
        }

        MatchPlayAuthorized = false;
        Debug.Log("[OnlineMatch] MatchPlayAuthorized revoked — MatchStart bekleniyor.");
        MatchPlayAuthorizedChanged?.Invoke();
    }

    public static void Clear()
    {
        Channel = null;
        PhotonFusionCleanup.ForceShutdownAll();

        IsOnlineMatch = false;
        MatchId = null;
        PhotonRoomName = null;
        OpponentUserId = null;
        OpponentDisplayName = null;
        OpponentAvatarIndex = 0;
        MatchPlayAuthorized = false;
        SessionCleared?.Invoke();
    }
}

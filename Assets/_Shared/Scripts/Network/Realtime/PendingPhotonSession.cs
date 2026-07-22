using UnityEngine;

/// <summary>
/// Photon odasına Game sahnesi yüklendikten sonra join etmek için bekleyen session.
/// StartGame → hemen LoadScene sırası Fusion'da 104 ServerLogic üretebiliyor.
/// </summary>
public static class PendingPhotonSession
{
    public static string SessionName { get; private set; }
    public static bool IsTestRoom { get; private set; }
    public static string OpponentUserId { get; private set; }
    public static string OpponentDisplayName { get; private set; }
    public static int OpponentAvatarIndex { get; private set; }
    public static string MatchId { get; private set; }

    public static bool HasPending => !string.IsNullOrEmpty(SessionName);

    public static void SetForHumanMatch(MatchmakingResult result)
    {
        SessionName = result.PhotonRoomName ?? result.MatchId;
        MatchId = result.MatchId;
        IsTestRoom = false;
        OpponentUserId = result.OpponentUserId;
        OpponentDisplayName = result.OpponentDisplayName;
        OpponentAvatarIndex = result.OpponentAvatarIndex;
    }

    public static void SetForTestRoom(string sessionName, string opponentName, int avatarIndex)
    {
        SessionName = sessionName;
        MatchId = sessionName;
        IsTestRoom = true;
        OpponentUserId = "test";
        OpponentDisplayName = opponentName;
        OpponentAvatarIndex = avatarIndex;
    }

    public static void Clear()
    {
        SessionName = null;
        MatchId = null;
        IsTestRoom = false;
        OpponentUserId = null;
        OpponentDisplayName = null;
        OpponentAvatarIndex = 0;
    }
}

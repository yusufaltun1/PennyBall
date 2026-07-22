using System;

public enum MatchOpponentSource
{
    Bot,
    Human
}

public sealed class MatchmakingResult
{
    public bool Success;
    public MatchOpponentSource Source;
    public string MatchId;
    public string PhotonRoomName;
    public string OpponentUserId;
    public string OpponentDisplayName;
    public int OpponentAvatarIndex;
    public string FailReason;
    public bool TimedOut;

    public static MatchmakingResult BotFallback(BotPlayerEntry bot, string reason = null)
    {
        return new MatchmakingResult
        {
            Success = true,
            Source = MatchOpponentSource.Bot,
            OpponentDisplayName = bot != null ? bot.displayName : "Opponent",
            OpponentAvatarIndex = bot != null ? bot.avatarIndex : 0,
            FailReason = reason
        };
    }

    public static MatchmakingResult Human(
        string matchId,
        string photonRoomName,
        string opponentUserId,
        string displayName,
        int avatarIndex)
    {
        return new MatchmakingResult
        {
            Success = true,
            Source = MatchOpponentSource.Human,
            MatchId = matchId,
            PhotonRoomName = string.IsNullOrEmpty(photonRoomName) ? matchId : photonRoomName,
            OpponentUserId = opponentUserId,
            OpponentDisplayName = displayName,
            OpponentAvatarIndex = avatarIndex
        };
    }

    public static MatchmakingResult Fail(string reason, bool timedOut = false)
    {
        return new MatchmakingResult
        {
            Success = false,
            FailReason = reason,
            TimedOut = timedOut
        };
    }
}

public interface IMatchmaker
{
    System.Threading.Tasks.Task<MatchmakingResult> FindMatchAsync(
        float timeoutSeconds,
        IProgress<float> progress = null);
    void Cancel();
}

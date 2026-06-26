using System;

[Serializable]
public class LeagueSaveData
{
    public int playerLeague = 1;
    public string playerDisplayName = "Player";
    public int playerAvatarIndex;
    public int playerTotalGoals;
    public long seasonStartUtcTicks;
    public int currentOpponentBotId = -1;
    public LeagueStandingEntry[] standings = Array.Empty<LeagueStandingEntry>();
    public string lastSimulationDateUtc = string.Empty;
}

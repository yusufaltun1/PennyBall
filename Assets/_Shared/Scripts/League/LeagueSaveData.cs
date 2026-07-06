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

    // Sezon bittiğinde main menüde gösterilecek sonuç (consume edilene kadar kalır)
    public bool hasPendingSeasonResult;
    public bool pendingPromoted;
    public int pendingPreviousLeague;
    public int pendingNewLeague;
    public int pendingFinalRank;
    public int pendingRewardCoins;
    public int pendingRewardXp;
}

[Serializable]
public struct LeagueSeasonResult
{
    public bool Promoted;
    public int PreviousLeague;
    public int NewLeague;
    public int FinalRank;
    public int RewardCoins;
    public int RewardXp;
}

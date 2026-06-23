using System;

[Serializable]
public class LeagueStandingEntry
{
    public bool isPlayer;
    public int botId;
    public string displayName;
    public int avatarIndex;
    public int points;
    public int played;
    public int wins;
    public int draws;

    public static LeagueStandingEntry FromBot(BotPlayerEntry bot, int points = 0)
    {
        return new LeagueStandingEntry
        {
            isPlayer = false,
            botId = bot.id,
            displayName = bot.displayName,
            avatarIndex = bot.avatarIndex,
            points = points,
            played = 0,
            wins = 0,
            draws = 0
        };
    }

    public static LeagueStandingEntry ForPlayer(string displayName, int avatarIndex, int points = 0)
    {
        return new LeagueStandingEntry
        {
            isPlayer = true,
            botId = -1,
            displayName = displayName,
            avatarIndex = avatarIndex,
            points = points,
            played = 0,
            wins = 0,
            draws = 0
        };
    }
}

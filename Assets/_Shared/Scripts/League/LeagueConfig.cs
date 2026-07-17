public static class LeagueConfig
{
    public const int LeagueCount = 10;
    public const int StandingsSize = 20;
    public const int SeasonDurationHours = 72;
    public const int PointsWin = 3;
    public const int PointsDraw = 1;
    public const int MatchDurationSeconds = 90;
    public const int ExerciseMatchDurationSeconds = 30;

    public const string SaveKey = "pennyball.league.save";
    public const string BotDatabaseResourcePath = "League/BotPlayers";
    public const string AvatarLibraryResourcePath = "AvatarSpriteLibrary";

    static readonly string[] LeagueNames =
    {
        "Bronze League",
        "Silver League",
        "Gold League",
        "Platinum League",
        "Ruby League",
        "Sapphire League",
        "Diamond League",
        "Master League",
        "Champion League",
        "Legend League",
    };

    public static string GetLeagueName(int league)
    {
        if (league < 1)
        {
            return LeagueNames[0];
        }

        int index = league - 1;
        if (index >= LeagueNames.Length)
        {
            return LeagueNames[LeagueNames.Length - 1];
        }

        return LeagueNames[index];
    }
}

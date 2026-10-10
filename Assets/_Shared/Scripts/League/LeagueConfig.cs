using UnityEngine;

public static class LeagueConfig
{
    public const int LeagueCount = 10;
    public const int StandingsSize = 20;
    public const int SeasonDurationHours = 72;
    public const int PointsWin = 3;
    public const int PointsDraw = 1;
    public const int MatchDurationSeconds = 60;
    public const int ExerciseMatchDurationSeconds = 30;

    public const string SaveKey = "pennyball.league.save";
    public const string BotDatabaseResourcePath = "League/BotPlayers";
    public const string AvatarLibraryResourcePath = "AvatarSpriteLibrary";

    static readonly string[] LeagueNameKeys =
    {
        "league.name.bronze",
        "league.name.silver",
        "league.name.gold",
        "league.name.platinum",
        "league.name.ruby",
        "league.name.sapphire",
        "league.name.diamond",
        "league.name.master",
        "league.name.champion",
        "league.name.legend",
    };

    public static string GetLeagueName(int league)
    {
        int index = Mathf.Clamp(league - 1, 0, LeagueNameKeys.Length - 1);
        return LocalizationService.Get(LeagueNameKeys[index]);
    }
}

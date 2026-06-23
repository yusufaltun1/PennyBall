using System.Text;
using UnityEngine;

public static class LeagueStandingsLogger
{
    public static void LogLeagueStandings(LeagueSaveData save, bool isFirstLaunch = false)
    {
        if (save == null || save.standings == null || save.standings.Length == 0)
        {
            Debug.LogWarning("[League] Lig tablosu boş, loglanamadı.");
            return;
        }

        string launchTag = isFirstLaunch ? " | İLK AÇILIŞ" : string.Empty;
        var builder = new StringBuilder();
        builder.AppendLine($"[League] Lig {save.playerLeague} sıralaması{launchTag}");
        builder.AppendLine($"[League] Oyuncu: {save.playerDisplayName} | Sezon kalan: {FormatSeasonRemaining(save)}");

        for (int i = 0; i < save.standings.Length; i++)
        {
            LeagueStandingEntry entry = save.standings[i];
            string marker = entry.isPlayer ? " [SEN]" : string.Empty;
            builder.AppendLine(
                $"[League] #{i + 1} {entry.displayName}{marker} | {entry.points}p | {entry.played}M {entry.wins}G {entry.draws}B");
        }

        Debug.Log(builder.ToString());
    }

    static string FormatSeasonRemaining(LeagueSaveData save)
    {
        if (save == null)
        {
            return "0d 0h 0m";
        }

        System.DateTime seasonStart = new System.DateTime(save.seasonStartUtcTicks, System.DateTimeKind.Utc);
        System.DateTime seasonEnd = seasonStart.AddHours(LeagueConfig.SeasonDurationHours);
        System.TimeSpan remaining = seasonEnd - System.DateTime.UtcNow;
        if (remaining < System.TimeSpan.Zero)
        {
            remaining = System.TimeSpan.Zero;
        }

        return $"{remaining.Days}d {remaining.Hours}h {remaining.Minutes}m";
    }
}

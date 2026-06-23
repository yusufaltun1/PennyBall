using System;
using System.Collections.Generic;
using UnityEngine;

public static class LeagueSimulation
{
    public static void SimulateUntilNow(LeagueSaveData save)
    {
        if (save == null || save.standings == null || save.standings.Length == 0)
        {
            return;
        }

        DateTime today = DateTime.UtcNow.Date;
        DateTime lastDate = ParseDate(save.lastSimulationDateUtc);

        if (lastDate == default)
        {
            lastDate = new DateTime(save.seasonStartUtcTicks, DateTimeKind.Utc).Date;
        }

        while (lastDate < today)
        {
            SimulateDay(save, lastDate);
            lastDate = lastDate.AddDays(1);
        }

        save.lastSimulationDateUtc = today.ToString("yyyy-MM-dd");
    }

    static void SimulateDay(LeagueSaveData save, DateTime day)
    {
        int league = save.playerLeague;
        float activity = GetDailyActivityMultiplier(league, day);

        for (int i = 0; i < save.standings.Length; i++)
        {
            LeagueStandingEntry entry = save.standings[i];
            if (entry.isPlayer)
            {
                continue;
            }

            int matchesToday = UnityEngine.Random.Range(0, 3) < 1 ? 0 : UnityEngine.Random.Range(1, 3);
            for (int matchIndex = 0; matchIndex < matchesToday; matchIndex++)
            {
                ApplySimulatedMatchResult(entry, league, activity);
            }
        }

        Array.Sort(save.standings, CompareByPoints);
    }

    static void ApplySimulatedMatchResult(LeagueStandingEntry entry, int league, float activity)
    {
        float winChance = Mathf.Lerp(0.22f, 0.48f, activity);
        if (league <= 1)
        {
            winChance *= 0.55f;
        }

        float drawChance = 0.18f;
        float roll = UnityEngine.Random.value;

        entry.played++;
        if (roll < winChance)
        {
            entry.wins++;
            entry.points += LeagueConfig.PointsWin;
            return;
        }

        if (roll < winChance + drawChance)
        {
            entry.draws++;
            entry.points += LeagueConfig.PointsDraw;
        }
    }

    static float GetDailyActivityMultiplier(int league, DateTime day)
    {
        float leagueFactor = Mathf.Clamp01((league - 1) / 9f);
        int daySeed = day.Day + day.Month * 31 + league * 997;
        UnityEngine.Random.InitState(daySeed + 1337);
        float noise = UnityEngine.Random.Range(0.85f, 1.15f);
        return Mathf.Clamp01(leagueFactor * noise);
    }

    static int CompareByPoints(LeagueStandingEntry a, LeagueStandingEntry b)
    {
        int pointsDelta = b.points - a.points;
        if (pointsDelta != 0)
        {
            return pointsDelta;
        }

        return string.Compare(a.displayName, b.displayName, StringComparison.Ordinal);
    }

    static DateTime ParseDate(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return default;
        }

        if (DateTime.TryParse(value, out DateTime parsed))
        {
            return parsed.Date;
        }

        return default;
    }
}

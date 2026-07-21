using System;
using UnityEngine;

public enum LeagueSimulationTrigger
{
    Initialize,
    MainMenu,
    Leaderboard,
    AfterMatch,
    AppResume
}

public static class LeagueSimulation
{
    const float SessionMinIntervalSeconds = 8f;
    const float SecondsPerBotMatch = 45f;
    const int SessionMinMatchesPerRefresh = 2;
    const int SessionMaxMatchesPerRefresh = 16;
    const int AfterMatchBonusMatches = 4;

    const float BaseWinChanceMin = 0.22f;
    const float BaseWinChanceMax = 0.48f;
    const float DrawChance = 0.18f;

    public static void RefreshStandings(LeagueSaveData save, LeagueSimulationTrigger trigger)
    {
        if (save?.standings == null || save.standings.Length == 0)
        {
            return;
        }

        SimulateUntilNow(save);

        if (trigger != LeagueSimulationTrigger.Initialize)
        {
            TrySimulateSessionBatch(save, trigger);
        }

        SortStandings(save);
    }

    public static void SimulateUntilNow(LeagueSaveData save)
    {
        if (save?.standings == null || save.standings.Length == 0)
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

    static void TrySimulateSessionBatch(LeagueSaveData save, LeagueSimulationTrigger trigger)
    {
        long nowTicks = DateTime.UtcNow.Ticks;

        if (save.lastSessionSimulationUtcTicks <= 0)
        {
            save.lastSessionSimulationUtcTicks = nowTicks;
        }

        double elapsedSeconds = TimeSpan.FromTicks(nowTicks - save.lastSessionSimulationUtcTicks).TotalSeconds;
        bool forceRun = trigger == LeagueSimulationTrigger.AfterMatch;

        if (!forceRun && save.sessionSimulationCount > 0 && elapsedSeconds < SessionMinIntervalSeconds)
        {
            return;
        }

        int matchesToSimulate = forceRun
            ? AfterMatchBonusMatches
            : Mathf.Clamp(
                Mathf.Max(SessionMinMatchesPerRefresh, Mathf.FloorToInt((float)(elapsedSeconds / SecondsPerBotMatch))),
                SessionMinMatchesPerRefresh,
                SessionMaxMatchesPerRefresh);

        save.lastSessionSimulationUtcTicks = nowTicks;
        save.sessionSimulationCount++;
        SortStandings(save);

        int playerRank = GetPlayerRank(save);
        int league = save.playerLeague;
        float leagueActivity = GetLeagueActivityFactor(league);
        int pointsBefore = SumBotPoints(save);

        for (int matchIndex = 0; matchIndex < matchesToSimulate; matchIndex++)
        {
            if (!TryPickBotForSessionMatch(save, matchIndex, out LeagueStandingEntry entry, out int botRank))
            {
                continue;
            }

            InitSessionRng(save, entry.botId, matchIndex);
            ApplySimulatedMatchResult(
                entry,
                league,
                leagueActivity,
                botRank,
                playerRank,
                save.standings.Length,
                DateTime.UtcNow);
        }

        int pointsAfter = SumBotPoints(save);
        Debug.Log(
            $"[LeagueSim] {trigger} | {elapsedSeconds:F0}s geçti | " +
            $"{matchesToSimulate} bot maçı | puan Δ{pointsAfter - pointsBefore}");
    }

    static int SumBotPoints(LeagueSaveData save)
    {
        int total = 0;
        for (int i = 0; i < save.standings.Length; i++)
        {
            if (!save.standings[i].isPlayer)
            {
                total += save.standings[i].points;
            }
        }

        return total;
    }

    static void InitSessionRng(LeagueSaveData save, int botId, int matchIndex)
    {
        unchecked
        {
            int seed = (int)(
                save.seasonStartUtcTicks
                ^ save.lastSessionSimulationUtcTicks
                ^ (save.sessionSimulationCount * 1315423911L)
                ^ (botId * 7919L)
                ^ (matchIndex * 104729L));
            UnityEngine.Random.InitState(seed);
        }
    }

    static void SimulateDay(LeagueSaveData save, DateTime day)
    {
        SortStandings(save);

        int league = save.playerLeague;
        float leagueActivity = GetLeagueActivityFactor(league, day);
        int playerRank = GetPlayerRank(save);
        int totalEntries = save.standings.Length;

        for (int i = 0; i < save.standings.Length; i++)
        {
            LeagueStandingEntry entry = save.standings[i];
            if (entry.isPlayer)
            {
                continue;
            }

            int botRank = FindEntryRank(save, entry);
            BotPersona persona = GetPersona(entry.botId);
            float rubberBand = GetRubberBandMultiplier(botRank, playerRank, totalEntries);
            int matchesToday = RollDailyMatchCount(
                save.seasonStartUtcTicks,
                day,
                entry.botId,
                persona,
                leagueActivity,
                rubberBand);

            for (int matchIndex = 0; matchIndex < matchesToday; matchIndex++)
            {
                InitDeterministicRng(save.seasonStartUtcTicks, day, entry.botId, matchIndex);
                ApplySimulatedMatchResult(
                    entry,
                    league,
                    leagueActivity,
                    botRank,
                    playerRank,
                    totalEntries,
                    day);
            }
        }

        SortStandings(save);
    }

    static void ApplySimulatedMatchResult(
        LeagueStandingEntry entry,
        int league,
        float leagueActivity,
        int botRank,
        int playerRank,
        int totalEntries,
        DateTime playedAt)
    {
        BotPersona persona = GetPersona(entry.botId);
        float rubberBand = GetRubberBandMultiplier(botRank, playerRank, totalEntries);
        float winChance = Mathf.Lerp(BaseWinChanceMin, BaseWinChanceMax, leagueActivity);
        winChance += persona.SkillBias;
        winChance *= rubberBand > 1f ? 1.02f : rubberBand < 1f ? 0.98f : 1f;

        if (league <= 1)
        {
            winChance *= 0.55f;
        }

        if (BotPlayerCatalog.TryGetById(entry.botId, out BotPlayerEntry bot))
        {
            winChance += (bot.difficultyLevel - 5) * 0.012f;
        }

        winChance = Mathf.Clamp(winChance, 0.12f, 0.62f);

        float roll = UnityEngine.Random.value;
        entry.played++;
        entry.lastPlayedUtcTicks = playedAt.Ticks;

        if (roll < winChance)
        {
            entry.wins++;
            entry.points += LeagueConfig.PointsWin;
            return;
        }

        if (roll < winChance + DrawChance)
        {
            entry.draws++;
            entry.points += LeagueConfig.PointsDraw;
        }
    }

    static int RollDailyMatchCount(
        long seasonTicks,
        DateTime day,
        int botId,
        BotPersona persona,
        float leagueActivity,
        float rubberBand)
    {
        InitDeterministicRng(seasonTicks, day, botId, -1);

        float expected = persona.ActivityMultiplier * leagueActivity * rubberBand;
        if (expected < 0.35f)
        {
            return 0;
        }

        if (expected < 0.75f)
        {
            return UnityEngine.Random.value < expected ? 1 : 0;
        }

        int baseMatches = UnityEngine.Random.Range(1, 3);
        if (expected > 1.2f && UnityEngine.Random.value < 0.35f)
        {
            baseMatches++;
        }

        return Mathf.Clamp(baseMatches, 0, 3);
    }

    readonly struct BotPersona
    {
        public float ActivityMultiplier { get; }
        public float SkillBias { get; }

        public BotPersona(float activityMultiplier, float skillBias)
        {
            ActivityMultiplier = activityMultiplier;
            SkillBias = skillBias;
        }
    }

    static BotPersona GetPersona(int botId)
    {
        int bucket = PositiveHash(botId) % 100;

        if (bucket < 25)
        {
            return new BotPersona(0.55f, -0.04f);
        }

        if (bucket < 70)
        {
            return new BotPersona(1f, 0f);
        }

        return new BotPersona(1.45f, 0.05f);
    }

    static float GetRubberBandMultiplier(int botRank, int playerRank, int totalEntries)
    {
        if (botRank <= 0 || playerRank <= 0 || totalEntries <= 1)
        {
            return 1f;
        }

        int delta = botRank - playerRank;

        if (delta < 0)
        {
            float playerPressure = Mathf.Clamp01((playerRank - 3f) / 12f);
            return Mathf.Lerp(1f, 0.7f, playerPressure);
        }

        if (delta > 0)
        {
            float playerDominance = Mathf.Clamp01((4f - playerRank) / 3f);
            return Mathf.Lerp(1.1f, 0.85f, playerDominance);
        }

        return 1f;
    }

    static float GetLeagueActivityFactor(int league, DateTime? day = null)
    {
        float leagueFactor = Mathf.Clamp01((league - 1) / 9f);

        if (!day.HasValue)
        {
            return leagueFactor;
        }

        int daySeed = day.Value.Day + day.Value.Month * 31 + league * 997;
        InitDeterministicRng(daySeed, 1337, league, 0);
        float noise = UnityEngine.Random.Range(0.85f, 1.15f);
        return Mathf.Clamp01(leagueFactor * noise);
    }

    static bool TryPickBotForSessionMatch(
        LeagueSaveData save,
        int matchIndex,
        out LeagueStandingEntry entry,
        out int botRank)
    {
        entry = null;
        botRank = 0;

        int botCount = 0;
        for (int i = 0; i < save.standings.Length; i++)
        {
            if (!save.standings[i].isPlayer)
            {
                botCount++;
            }
        }

        if (botCount == 0)
        {
            return false;
        }

        int pick = NextSessionPick(save, botCount, matchIndex);
        int seen = 0;

        for (int i = 0; i < save.standings.Length; i++)
        {
            LeagueStandingEntry candidate = save.standings[i];
            if (candidate.isPlayer)
            {
                continue;
            }

            if (seen == pick)
            {
                entry = candidate;
                botRank = FindEntryRank(save, candidate);
                return true;
            }

            seen++;
        }

        return false;
    }

    static int GetPlayerRank(LeagueSaveData save)
    {
        if (save?.standings == null)
        {
            return 1;
        }

        for (int i = 0; i < save.standings.Length; i++)
        {
            if (save.standings[i].isPlayer)
            {
                return i + 1;
            }
        }

        return save.standings.Length;
    }

    static int FindEntryRank(LeagueSaveData save, LeagueStandingEntry target)
    {
        if (save?.standings == null || target == null)
        {
            return 1;
        }

        for (int i = 0; i < save.standings.Length; i++)
        {
            if (save.standings[i] == target)
            {
                return i + 1;
            }
        }

        return save.standings.Length;
    }

    static void SortStandings(LeagueSaveData save)
    {
        if (save?.standings == null)
        {
            return;
        }

        Array.Sort(save.standings, CompareByPoints);
    }

    static int CompareByPoints(LeagueStandingEntry a, LeagueStandingEntry b)
    {
        int pointsDelta = b.points - a.points;
        if (pointsDelta != 0)
        {
            return pointsDelta;
        }

        if (a.isPlayer)
        {
            return -1;
        }

        if (b.isPlayer)
        {
            return 1;
        }

        return string.Compare(a.displayName, b.displayName, StringComparison.Ordinal);
    }

    static void InitDeterministicRng(long seasonTicks, DateTime day, int botId, int salt)
    {
        unchecked
        {
            int seed = (int)(
                seasonTicks
                ^ day.Ticks
                ^ (botId * 7919L)
                ^ (salt * 104729L));
            UnityEngine.Random.InitState(seed);
        }
    }

    static void InitDeterministicRng(int a, int b, int c, int d)
    {
        unchecked
        {
            int seed = a ^ (b * 374761393) ^ (c * 668265263) ^ (int)(d * 2246822519L);
            UnityEngine.Random.InitState(seed);
        }
    }

    static int NextSessionPick(LeagueSaveData save, int botCount, int matchIndex)
    {
        InitSessionRng(save, 8800 + matchIndex, matchIndex);
        return UnityEngine.Random.Range(0, botCount);
    }

    static int NextRange(int minInclusive, int maxExclusive, long seasonTicks, int botId, int salt)
    {
        InitDeterministicRng(seasonTicks, DateTime.UtcNow.Date, botId, salt);
        return UnityEngine.Random.Range(minInclusive, maxExclusive);
    }

    static int PositiveHash(int value)
    {
        unchecked
        {
            uint hash = (uint)value;
            hash ^= hash >> 16;
            hash *= 0x7feb352d;
            hash ^= hash >> 15;
            hash *= 0x846ca68b;
            hash ^= hash >> 16;
            return (int)(hash % int.MaxValue);
        }
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

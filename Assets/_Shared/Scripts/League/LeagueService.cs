using System;
using System.Collections.Generic;
using UnityEngine;

public class LeagueService : MonoBehaviour
{
    public static LeagueService Instance { get; private set; }

    public event Action SeasonChanged;
    public event Action StandingsUpdated;
    public event Action<int> PlayerPromoted;

    LeagueSaveData _save;

    public LeagueSaveData Save => _save;
    public int PlayerLeague => _save?.playerLeague ?? 1;
    public int PlayerAvatarIndex => _save?.playerAvatarIndex ?? 0;
    public TimeSpan SeasonRemaining => GetSeasonRemaining();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void EnsureService()
    {
        if (Instance != null)
        {
            return;
        }

        var serviceObject = new GameObject("LeagueService");
        Instance = serviceObject.AddComponent<LeagueService>();
        DontDestroyOnLoad(serviceObject);
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Initialize();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void Initialize()
    {
        BotPlayerCatalog.Load();
        bool isFirstLaunch = !PlayerPrefs.HasKey(LeagueConfig.SaveKey);
        _save = LeagueRepository.Load();

        if (_save == null)
        {
            _save = CreateNewSave();
            LeagueRepository.Save(_save);
        }

        if (IsSeasonExpired())
        {
            ResolveSeasonEnd();
        }

        LeagueSimulation.SimulateUntilNow(_save);
        SortStandings();
        LeagueRepository.Save(_save);
        LeagueStandingsLogger.LogLeagueStandings(_save, isFirstLaunch);
        StandingsUpdated?.Invoke();
    }

    public BotPlayerEntry GetCurrentOpponent()
    {
        if (_save == null || _save.currentOpponentBotId < 0)
        {
            return null;
        }

        BotPlayerCatalog.TryGetById(_save.currentOpponentBotId, out BotPlayerEntry bot);
        return bot;
    }

    public BotPlayerEntry PickOpponentForNextMatch()
    {
        EnsureSeasonActive();

        // Standings'deki bot girişlerini topla (player hariç)
        var botEntries = new System.Collections.Generic.List<LeagueStandingEntry>(20);
        if (_save?.standings != null)
        {
            for (int i = 0; i < _save.standings.Length; i++)
            {
                LeagueStandingEntry e = _save.standings[i];
                if (!e.isPlayer && e.botId >= 0)
                    botEntries.Add(e);
            }
        }

        BotPlayerEntry opponent = null;
        if (botEntries.Count > 0)
        {
            LeagueStandingEntry picked = botEntries[UnityEngine.Random.Range(0, botEntries.Count)];
            BotPlayerCatalog.TryGetById(picked.botId, out opponent);
        }

        // Standings boşsa tam havuza düş
        if (opponent == null)
        {
            IReadOnlyList<BotPlayerEntry> pool = BotPlayerCatalog.GetBotsForLeague(_save.playerLeague);
            if (pool.Count == 0) return null;
            opponent = pool[UnityEngine.Random.Range(0, pool.Count)];
        }

        _save.currentOpponentBotId = opponent.id;
        LeagueRepository.Save(_save);
        MatchSessionContext.SetOpponent(opponent);
        return opponent;
    }

    public void RegisterMatchResult(MatchResultType result)
    {
        EnsureSeasonActive();

        LeagueStandingEntry playerEntry = FindPlayerStanding();
        if (playerEntry != null)
        {
            playerEntry.played++;
            switch (result)
            {
                case MatchResultType.Win:
                    playerEntry.wins++;
                    playerEntry.points += LeagueConfig.PointsWin;
                    break;
                case MatchResultType.Draw:
                    playerEntry.draws++;
                    playerEntry.points += LeagueConfig.PointsDraw;
                    break;
                case MatchResultType.Loss:
                    break;
            }
        }

        if (_save.currentOpponentBotId >= 0
            && TryGetBotStanding(_save.currentOpponentBotId, out LeagueStandingEntry botEntry))
        {
            ApplyOpponentMirrorResult(botEntry, result);
        }

        MatchSessionContext.SetRankBefore(GetPlayerRank());
        SortStandings();
        MatchSessionContext.SetRankAfter(GetPlayerRank());
        LeagueRepository.Save(_save);
        StandingsUpdated?.Invoke();
    }

    public int GetPlayerRank()
    {
        SortStandings();
        for (int i = 0; i < _save.standings.Length; i++)
        {
            if (_save.standings[i].isPlayer)
            {
                return i + 1;
            }
        }

        return _save.standings.Length;
    }

    LeagueSaveData CreateNewSave()
    {
        var save = new LeagueSaveData
        {
            playerLeague = 1,
            playerDisplayName = $"Player_{UnityEngine.Random.Range(100, 1000)}",
            playerAvatarIndex = 0,
            seasonStartUtcTicks = DateTime.UtcNow.Ticks,
            lastSimulationDateUtc = DateTime.UtcNow.Date.ToString("yyyy-MM-dd")
        };

        save.standings = BuildStandingsForLeague(save.playerLeague, save.playerDisplayName, save.playerAvatarIndex);
        save.currentOpponentBotId = PickInitialOpponentId(save);
        return save;
    }

    static LeagueStandingEntry[] BuildStandingsForLeague(int league, string playerName, int playerAvatarIndex)
    {
        IReadOnlyList<BotPlayerEntry> pool = BotPlayerCatalog.GetBotsForLeague(league);
        var selectedBots = new List<BotPlayerEntry>();
        var usedIds = new HashSet<int>();

        while (selectedBots.Count < LeagueConfig.StandingsSize - 1 && usedIds.Count < pool.Count)
        {
            BotPlayerEntry bot = pool[UnityEngine.Random.Range(0, pool.Count)];
            if (usedIds.Add(bot.id))
            {
                selectedBots.Add(bot);
            }
        }

        var standings = new LeagueStandingEntry[LeagueConfig.StandingsSize];
        standings[0] = LeagueStandingEntry.ForPlayer(playerName, playerAvatarIndex);

        for (int i = 0; i < selectedBots.Count; i++)
        {
            standings[i + 1] = LeagueStandingEntry.FromBot(selectedBots[i], GetInitialBotPoints(league));
        }

        for (int i = selectedBots.Count + 1; i < standings.Length; i++)
        {
            standings[i] = LeagueStandingEntry.FromBot(pool[UnityEngine.Random.Range(0, pool.Count)]);
        }

        Array.Sort(standings, (a, b) => b.points - a.points);
        return standings;
    }

    static int GetInitialBotPoints(int league)
    {
        if (league > 1)
        {
            return UnityEngine.Random.Range(0, 4);
        }

        return UnityEngine.Random.Range(0, 2);
    }

    static int PickInitialOpponentId(LeagueSaveData save)
    {
        for (int i = 0; i < save.standings.Length; i++)
        {
            LeagueStandingEntry entry = save.standings[i];
            if (!entry.isPlayer)
            {
                return entry.botId;
            }
        }

        return -1;
    }

    void EnsureSeasonActive()
    {
        if (IsSeasonExpired())
        {
            ResolveSeasonEnd();
            LeagueSimulation.SimulateUntilNow(_save);
            LeagueRepository.Save(_save);
            SeasonChanged?.Invoke();
            StandingsUpdated?.Invoke();
        }
    }

    bool IsSeasonExpired()
    {
        if (_save == null)
        {
            return false;
        }

        return GetSeasonRemaining() <= TimeSpan.Zero;
    }

    TimeSpan GetSeasonRemaining()
    {
        if (_save == null)
        {
            return TimeSpan.Zero;
        }

        DateTime seasonStart = new DateTime(_save.seasonStartUtcTicks, DateTimeKind.Utc);
        DateTime seasonEnd = seasonStart.AddHours(LeagueConfig.SeasonDurationHours);
        return seasonEnd - DateTime.UtcNow;
    }

    void ResolveSeasonEnd()
    {
        SortStandings();
        int playerRank = GetPlayerRank();

        if (playerRank == 1 && _save.playerLeague < LeagueConfig.LeagueCount)
        {
            _save.playerLeague++;
            PlayerPromoted?.Invoke(_save.playerLeague);
        }

        StartNewSeason();
    }

    void StartNewSeason()
    {
        _save.seasonStartUtcTicks = DateTime.UtcNow.Ticks;
        _save.lastSimulationDateUtc = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
        _save.standings = BuildStandingsForLeague(
            _save.playerLeague,
            _save.playerDisplayName,
            _save.playerAvatarIndex);
        _save.currentOpponentBotId = PickInitialOpponentId(_save);
    }

    void SortStandings()
    {
        if (_save?.standings == null)
        {
            return;
        }

        Array.Sort(_save.standings, (a, b) =>
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
        });
    }

    LeagueStandingEntry FindPlayerStanding()
    {
        if (_save?.standings == null)
        {
            return null;
        }

        for (int i = 0; i < _save.standings.Length; i++)
        {
            if (_save.standings[i].isPlayer)
            {
                return _save.standings[i];
            }
        }

        return null;
    }

    bool TryGetBotStanding(int botId, out LeagueStandingEntry entry)
    {
        entry = null;
        if (_save?.standings == null)
        {
            return false;
        }

        for (int i = 0; i < _save.standings.Length; i++)
        {
            if (!_save.standings[i].isPlayer && _save.standings[i].botId == botId)
            {
                entry = _save.standings[i];
                return true;
            }
        }

        return false;
    }

    static void ApplyOpponentMirrorResult(LeagueStandingEntry botEntry, MatchResultType playerResult)
    {
        botEntry.played++;
        switch (playerResult)
        {
            case MatchResultType.Win:
                break;
            case MatchResultType.Draw:
                botEntry.draws++;
                botEntry.points += LeagueConfig.PointsDraw;
                break;
            case MatchResultType.Loss:
                botEntry.wins++;
                botEntry.points += LeagueConfig.PointsWin;
                break;
        }
    }
}

public enum MatchResultType
{
    Win,
    Draw,
    Loss
}

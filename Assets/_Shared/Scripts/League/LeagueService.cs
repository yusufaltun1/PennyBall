using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LeagueService : MonoBehaviour
{
    public static LeagueService Instance { get; private set; }

    public event Action SeasonChanged;
    public event Action StandingsUpdated;
    public event Action<int> PlayerPromoted;
    public event Action SeasonResultPending;
    public event Action AvatarChanged;
    public event Action DisplayNameChanged;
    public event Action<MatchResultType, string, string, int> MatchResultRegistered;

    LeagueSaveData _save;
    Coroutine _seasonWatchRoutine;

    public LeagueSaveData Save => _save;
    public int PlayerLeague => _save?.playerLeague ?? 1;
    public int PlayerTotalMatches => _save?.playerTotalMatches ?? 0;
    public int PlayerAvatarIndex => _save?.playerAvatarIndex ?? 0;
    public TimeSpan SeasonRemaining => GetSeasonRemaining();
    public bool HasPendingSeasonResult => _save != null && _save.hasPendingSeasonResult;

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
        SceneManager.sceneLoaded += OnSceneLoaded;
        _seasonWatchRoutine = StartCoroutine(WatchSeasonExpiryRoutine());
        Initialize();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (_seasonWatchRoutine != null)
        {
            StopCoroutine(_seasonWatchRoutine);
            _seasonWatchRoutine = null;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    void OnApplicationPause(bool paused)
    {
        if (!paused)
        {
            TryRefreshOnAppResume();
        }
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            TryRefreshOnAppResume();
        }
    }

    void TryRefreshOnAppResume()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName == GameSceneNames.MainMenu)
        {
            RefreshStandingsSimulation(LeagueSimulationTrigger.AppResume);
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == GameSceneNames.MainMenu)
        {
            RefreshStandingsSimulation(LeagueSimulationTrigger.MainMenu);
            NotifyPendingSeasonResultIfNeeded();
        }
    }

    IEnumerator WatchSeasonExpiryRoutine()
    {
        var wait = new WaitForSeconds(1f);

        while (true)
        {
            yield return wait;

            if (_save == null)
            {
                continue;
            }

            string sceneName = SceneManager.GetActiveScene().name;
            if (sceneName != GameSceneNames.MainMenu)
            {
                continue;
            }

            if (IsSeasonExpired())
            {
                EnsureSeasonActive();
            }
        }
    }

    void NotifyPendingSeasonResultIfNeeded()
    {
        if (_save != null && _save.hasPendingSeasonResult)
        {
            SeasonResultPending?.Invoke();
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

        BackfillTotalMatchesIfNeeded();

        ResolveSeasonIfNeeded(invokeEvents: false);

        LeagueSimulation.RefreshStandings(_save, LeagueSimulationTrigger.Initialize);
        SortStandings();
        LeagueRepository.Save(_save);
        LeagueStandingsLogger.LogLeagueStandings(_save, isFirstLaunch);
        StandingsUpdated?.Invoke();
    }

    public void RefreshStandingsSimulation(LeagueSimulationTrigger trigger)
    {
        if (_save == null)
        {
            return;
        }

        EnsureSeasonActive();
        LeagueSimulation.RefreshStandings(_save, trigger);
        SortStandings();
        LeagueRepository.Save(_save);
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

    public bool RegisterMatchResult(MatchResultType result, string abandonReason = null)
    {
        if (!MatchSessionTracker.TryConsumeResult(out string matchId, out string trackedAbandonReason, out int durationSeconds))
        {
            return false;
        }

        if (string.IsNullOrEmpty(abandonReason))
        {
            abandonReason = trackedAbandonReason;
        }

        EnsureSeasonActive();

        // Puanlar değişmeden ÖNCE mevcut sırayı yakala
        MatchSessionContext.SetRankBefore(FindPlayerRankInArray());

        LeagueStandingEntry playerEntry = FindPlayerStanding();
        if (playerEntry != null)
        {
            playerEntry.played++;
            _save.playerTotalMatches++;
            playerEntry.lastPlayedUtcTicks = DateTime.UtcNow.Ticks;
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

        SortStandings();
        MatchSessionContext.SetRankAfter(FindPlayerRankInArray());

        LeagueSimulation.RefreshStandings(_save, LeagueSimulationTrigger.AfterMatch);
        SortStandings();

        // Hükmen mağlubiyet / abandon: coin ve XP yok. Normal kayıpta ödül devam eder.
        int coins = 0;
        int xp = 0;
        if (string.IsNullOrEmpty(abandonReason))
        {
            (coins, xp) = WalletService.GetReward(result);
        }

        int levelBefore = WalletService.Level;
        if (coins > 0 || xp > 0)
        {
            WalletService.AddReward(coins, xp);
        }

        MatchSessionContext.SetEarnedRewards(coins, xp, levelBefore, WalletService.Level);
        MatchSessionContext.SetPendingBoosterUnlock(
            BoosterConfig.GetUnlockReachedOnLevelUp(levelBefore, WalletService.Level));

        LeagueRepository.Save(_save);
        StandingsUpdated?.Invoke();
        MatchResultRegistered?.Invoke(result, abandonReason, matchId, durationSeconds);
        MatchAdTracker.RegisterMatchCompleted();
        return true;
    }

    // Sort tetiklemeden mevcut dizi sırasından rank döndürür
    int FindPlayerRankInArray()
    {
        if (_save?.standings == null) return -1;
        for (int i = 0; i < _save.standings.Length; i++)
            if (_save.standings[i].isPlayer) return i + 1;
        return -1;
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

    public void SetPlayerAvatar(int index)
    {
        if (_save == null) return;
        AvatarSpriteLibrary lib = AvatarSpriteLibrary.Load();
        int max = lib != null ? lib.Count - 1 : 0;
        _save.playerAvatarIndex = Mathf.Clamp(index, 0, max);

        LeagueStandingEntry player = FindPlayerStanding();
        if (player != null)
            player.avatarIndex = _save.playerAvatarIndex;

        LeagueRepository.Save(_save);
        AvatarChanged?.Invoke();
    }

    public void SetPlayerDisplayName(string displayName)
    {
        if (_save == null || string.IsNullOrWhiteSpace(displayName))
        {
            return;
        }

        string trimmed = displayName.Trim();
        const int maxLength = 20;
        if (trimmed.Length > maxLength)
        {
            trimmed = trimmed.Substring(0, maxLength);
        }

        if (_save.playerDisplayName == trimmed)
        {
            return;
        }

        _save.playerDisplayName = trimmed;

        LeagueStandingEntry player = FindPlayerStanding();
        if (player != null)
        {
            player.displayName = trimmed;
        }

        LeagueRepository.Save(_save);
        StandingsUpdated?.Invoke();
        DisplayNameChanged?.Invoke();
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

    public bool ResolveSeasonIfNeeded(bool invokeEvents = true)
    {
        if (_save == null || !IsSeasonExpired())
        {
            return false;
        }

        if (_save.hasPendingSeasonResult)
        {
            return false;
        }

        ResolveSeasonEnd();
        LeagueSimulation.RefreshStandings(_save, LeagueSimulationTrigger.Initialize);
        SortStandings();
        LeagueRepository.Save(_save);

        if (invokeEvents)
        {
            SeasonChanged?.Invoke();
            StandingsUpdated?.Invoke();
            SeasonResultPending?.Invoke();
        }

        return true;
    }

    void FinalizeExpiringSeasonStandings()
    {
        LeagueSimulation.SimulateUntilNow(_save);
        SortStandings();
    }

    public bool TryGetPendingSeasonResult(out LeagueSeasonResult result)
    {
        result = default;
        if (_save == null || !_save.hasPendingSeasonResult)
        {
            return false;
        }

        result = new LeagueSeasonResult
        {
            Promoted = _save.pendingPromoted,
            PreviousLeague = _save.pendingPreviousLeague,
            NewLeague = _save.pendingNewLeague,
            FinalRank = _save.pendingFinalRank,
            RewardCoins = _save.pendingRewardCoins,
            RewardXp = _save.pendingRewardXp
        };
        return true;
    }

    public bool ClaimPendingSeasonReward(int multiplier = 1)
    {
        if (_save == null || !_save.hasPendingSeasonResult)
        {
            return false;
        }

        int safeMultiplier = Mathf.Max(1, multiplier);
        int coins = _save.pendingRewardCoins * safeMultiplier;
        int xp = _save.pendingRewardXp * safeMultiplier;
        if (coins > 0 || xp > 0)
        {
            WalletService.AddReward(coins, xp);
        }

        ConsumePendingSeasonResult();
        return true;
    }

    public void ConsumePendingSeasonResult()
    {
        if (_save == null || !_save.hasPendingSeasonResult)
        {
            return;
        }

        _save.hasPendingSeasonResult = false;
        _save.pendingPromoted = false;
        _save.pendingPreviousLeague = 0;
        _save.pendingNewLeague = 0;
        _save.pendingFinalRank = 0;
        _save.pendingRewardCoins = 0;
        _save.pendingRewardXp = 0;
        LeagueRepository.Save(_save);
    }

    void EnsureSeasonActive()
    {
        ResolveSeasonIfNeeded(invokeEvents: true);
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>
    /// Test/debug: sezon bitişine kalan süreyi ayarlar.
    /// </summary>
    public void DebugSetSeasonRemaining(TimeSpan remaining)
    {
        if (_save == null)
        {
            return;
        }

        DateTime seasonEnd = DateTime.UtcNow.Add(remaining);
        DateTime seasonStart = seasonEnd.AddHours(-LeagueConfig.SeasonDurationHours);
        _save.seasonStartUtcTicks = seasonStart.Ticks;
        LeagueRepository.Save(_save);
    }

    /// <summary>
    /// Test/debug: sezon süresi dolmuşsa ResolveSeasonEnd akışını tetikler.
    /// </summary>
    public void DebugForceSeasonCheck()
    {
        EnsureSeasonActive();
        StandingsUpdated?.Invoke();
    }

    /// <summary>
    /// Test/debug: sezonu hemen bitmiş sayar ve kontrol eder.
    /// </summary>
    public void DebugExpireSeasonNow()
    {
        DebugSetSeasonRemaining(TimeSpan.FromSeconds(-1));
        ConsumePendingSeasonResult();
        DebugForceSeasonCheck();
    }

    /// <summary>
    /// Test/debug: oyuncuyu 1. sıraya alır (promotion senaryosu için).
    /// </summary>
    public void DebugForcePlayerFirstPlace()
    {
        if (_save?.standings == null)
        {
            return;
        }

        int maxPoints = 0;
        for (int i = 0; i < _save.standings.Length; i++)
        {
            maxPoints = Mathf.Max(maxPoints, _save.standings[i].points);
        }

        LeagueStandingEntry player = FindPlayerStanding();
        if (player != null)
        {
            player.points = maxPoints + 10;
        }

        SortStandings();
        LeagueRepository.Save(_save);
        StandingsUpdated?.Invoke();
    }

    /// <summary>
    /// Test/debug: oyuncunun lig puanını ayarlar.
    /// </summary>
    public void DebugSetPlayerLeaguePoints(int points)
    {
        if (_save?.standings == null)
        {
            return;
        }

        LeagueStandingEntry player = FindPlayerStanding();
        if (player == null)
        {
            return;
        }

        player.points = Mathf.Max(0, points);
        SortStandings();
        LeagueRepository.Save(_save);
        StandingsUpdated?.Invoke();
    }

    /// <summary>
    /// Test/debug: 1. sıraya al, sezonu bitir.
    /// </summary>
    public void DebugForceRankOneAndExpireSeason()
    {
        DebugForcePlayerFirstPlace();
        DebugExpireSeasonNow();
    }
#endif

    void ResolveSeasonEnd()
    {
        FinalizeExpiringSeasonStandings();
        int playerRank = GetPlayerRank();
        int previousLeague = _save.playerLeague;
        bool promoted = playerRank == 1 && previousLeague < LeagueConfig.LeagueCount;

        if (promoted)
        {
            _save.playerLeague++;
            PlayerPromoted?.Invoke(_save.playerLeague);
        }

        _save.hasPendingSeasonResult = true;
        _save.pendingPromoted = promoted;
        _save.pendingPreviousLeague = previousLeague;
        _save.pendingNewLeague = _save.playerLeague;
        _save.pendingFinalRank = playerRank;
        _save.pendingRewardCoins = promoted
            ? WalletService.LeaguePromotionCoins
            : WalletService.LeaguePromotionCoins / 2;
        _save.pendingRewardXp = promoted
            ? WalletService.LeaguePromotionXp
            : WalletService.LeaguePromotionXp / 2;

        StartNewSeason();
        LeagueRepository.Save(_save);
        SeasonResultPending?.Invoke();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            $"[League] Sezon bitti. Sıra={playerRank}, Promote={promoted}, " +
            $"Lig {previousLeague}→{_save.playerLeague}, Puan={FindPlayerStanding()?.points ?? 0}");
#endif
    }

    void StartNewSeason()
    {
        _save.seasonStartUtcTicks = DateTime.UtcNow.Ticks;
        _save.lastSimulationDateUtc = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
        _save.lastSessionSimulationUtcTicks = 0;
        _save.sessionSimulationCount = 0;
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

    void BackfillTotalMatchesIfNeeded()
    {
        if (_save == null || _save.playerTotalMatches > 0)
        {
            return;
        }

        LeagueStandingEntry player = FindPlayerStanding();
        if (player == null || player.played <= 0)
        {
            return;
        }

        _save.playerTotalMatches = player.played;
        LeagueRepository.Save(_save);
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
        botEntry.lastPlayedUtcTicks = DateTime.UtcNow.Ticks;
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

using System;
using System.Collections;
using System.Collections.Generic;
using ByteBrewSDK;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// PennyBall oyun olaylarını ByteBrew custom event olarak gönderir.
/// </summary>
public class ByteBrewGameAnalytics : MonoBehaviour
{
    static ByteBrewGameAnalytics _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureListener()
    {
        if (_instance != null)
        {
            return;
        }

        var analyticsObject = new GameObject("ByteBrewGameAnalytics");
        _instance = analyticsObject.AddComponent<ByteBrewGameAnalytics>();
        DontDestroyOnLoad(analyticsObject);
    }

    void OnEnable()
    {
        GameAnalytics.EventRequested += OnGameAnalyticsEvent;
        WalletService.LevelChanged += OnLevelChanged;
        StartCoroutine(InitializeWhenReady());
    }

    void OnDisable()
    {
        GameAnalytics.EventRequested -= OnGameAnalyticsEvent;
        WalletService.LevelChanged -= OnLevelChanged;
        UnsubscribeLeague();
    }

    void OnApplicationPause(bool paused)
    {
        if (!paused)
        {
            return;
        }

        TryAbandonActiveMatch("app_pause");
    }

    void OnApplicationQuit()
    {
        TryAbandonActiveMatch("app_quit");
    }

    IEnumerator InitializeWhenReady()
    {
        while (LeagueService.Instance == null)
        {
            yield return null;
        }

        yield return RecoverAbandonedMatchIfNeeded();

        UnsubscribeLeague();
        LeagueService.Instance.MatchResultRegistered += OnMatchResultRegistered;
        LeagueService.Instance.PlayerPromoted += OnPlayerPromoted;

        while (LeagueMatchController.Instance == null)
        {
            yield return null;
        }

        LeagueMatchController.Instance.MatchStarted += OnMatchStarted;

        SyncUserAttributes();

        if (SceneManager.GetActiveScene().name == GameSceneNames.Game)
        {
            TrackMatchStartedIfNeeded();
        }
    }

    void UnsubscribeLeague()
    {
        if (LeagueService.Instance != null)
        {
            LeagueService.Instance.MatchResultRegistered -= OnMatchResultRegistered;
            LeagueService.Instance.PlayerPromoted -= OnPlayerPromoted;
        }

        if (LeagueMatchController.Instance != null)
        {
            LeagueMatchController.Instance.MatchStarted -= OnMatchStarted;
        }
    }

    IEnumerator RecoverAbandonedMatchIfNeeded()
    {
        if (!MatchSessionTracker.TryRestorePersistedMatch("app_killed", out _))
        {
            yield break;
        }

        LeagueService.Instance.RegisterMatchResult(MatchResultType.Loss, "app_killed");
    }

    void TryAbandonActiveMatch(string reason)
    {
        if (SceneManager.GetActiveScene().name != GameSceneNames.Game)
        {
            return;
        }

        if (LeagueMatchController.Instance == null || !LeagueMatchController.Instance.IsMatchActive)
        {
            return;
        }

        LeagueMatchController.Instance.AbandonActiveMatch(reason);
    }

    void OnMatchStarted()
    {
        TrackMatchStartedIfNeeded();
    }

    void TrackMatchStartedIfNeeded()
    {
        if (!MatchSessionTracker.HasPendingResult)
        {
            MatchSessionTracker.BeginMatch();
        }

        var parameters = BuildMatchContextParameters();
        parameters["match_id"] = MatchSessionTracker.CurrentMatchId ?? "unknown";
        TrackEvent("match_started", parameters);
    }

    void OnMatchResultRegistered(MatchResultType result, string abandonReason, string matchId, int durationSeconds)
    {
        var parameters = BuildMatchContextParameters();
        parameters["match_id"] = matchId ?? "unknown";
        parameters["result"] = result.ToString().ToLowerInvariant();
        parameters["coins_earned"] = MatchSessionContext.EarnedCoins.ToString();
        parameters["xp_earned"] = MatchSessionContext.EarnedXp.ToString();
        parameters["leveled_up"] = MatchSessionContext.LeveledUp ? "true" : "false";
        parameters["rank_before"] = MatchSessionContext.RankBefore.ToString();
        parameters["rank_after"] = MatchSessionContext.RankAfter.ToString();
        parameters["duration_sec"] = durationSeconds.ToString();

        if (LeagueMatchController.Instance != null)
        {
            parameters["player_goals"] = LeagueMatchController.Instance.PlayerGoals.ToString();
            parameters["opponent_goals"] = LeagueMatchController.Instance.OpponentGoals.ToString();
            parameters["time_remaining_sec"] = Mathf.CeilToInt(LeagueMatchController.Instance.MatchTimeRemaining).ToString();
        }

        if (!string.IsNullOrEmpty(abandonReason))
        {
            parameters["reason"] = abandonReason;
            TrackEvent("match_abandoned", parameters);
        }
        else
        {
            TrackEvent("match_completed", parameters);
        }

        SyncUserAttributes();
    }

    void OnPlayerPromoted(int newLeague)
    {
        TrackEvent("league_promoted", new Dictionary<string, string>
        {
            { "league", newLeague.ToString() }
        });
        SyncUserAttributes();
    }

    void OnLevelChanged(int oldLevel, int newLevel)
    {
        TrackEvent("level_up", new Dictionary<string, string>
        {
            { "old_level", oldLevel.ToString() },
            { "new_level", newLevel.ToString() },
            { "total_xp", WalletService.TotalXp.ToString() }
        });
        SyncUserAttributes();
    }

    void OnGameAnalyticsEvent(string eventName, Dictionary<string, string> parameters)
    {
        TrackEvent(eventName, parameters);
    }

    static Dictionary<string, string> BuildMatchContextParameters()
    {
        var parameters = new Dictionary<string, string>
        {
            { "league", LeagueService.Instance != null ? LeagueService.Instance.PlayerLeague.ToString() : "1" },
            { "player_level", WalletService.Level.ToString() }
        };

        if (MatchSessionContext.HasOpponent)
        {
            parameters["opponent"] = MatchSessionContext.CurrentOpponent.displayName;
            parameters["opponent_id"] = MatchSessionContext.CurrentOpponent.id.ToString();
            parameters["difficulty"] = MatchSessionContext.CurrentOpponent.difficultyLevel.ToString();
        }

        return parameters;
    }

    static void SyncUserAttributes()
    {
        ByteBrew.SetCustomUserDataAttribute("player_level", WalletService.Level.ToString());
        ByteBrew.SetCustomUserDataAttribute("total_xp", WalletService.TotalXp.ToString());
        ByteBrew.SetCustomUserDataAttribute("total_coins", WalletService.TotalCoins.ToString());

        if (LeagueService.Instance != null)
        {
            ByteBrew.SetCustomUserDataAttribute("league", LeagueService.Instance.PlayerLeague.ToString());

            LeagueStandingEntry player = LeagueService.Instance.Save?.standings != null
                ? System.Array.Find(LeagueService.Instance.Save.standings, s => s.isPlayer)
                : null;

            if (player != null)
            {
                ByteBrew.SetCustomUserDataAttribute("league_points", player.points.ToString());
                ByteBrew.SetCustomUserDataAttribute("matches_played", player.played.ToString());
            }
        }

        ByteBrew.SetCustomUserDataAttribute("onboarding_completed", OnboardingProgress.IsCompleted ? "true" : "false");
    }

    static void TrackEvent(string eventName)
    {
        try
        {
            ByteBrew.NewCustomEvent(eventName);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ByteBrew] Event '{eventName}' gönderilemedi: {ex.Message}");
        }
    }

    static void TrackEvent(string eventName, Dictionary<string, string> parameters)
    {
        try
        {
            ByteBrew.NewCustomEvent(eventName, parameters);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ByteBrew] Event '{eventName}' gönderilemedi: {ex.Message}");
        }
    }
}

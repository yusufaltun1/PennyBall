using System.Collections;
using System.Collections.Generic;
using ByteBrewSDK;
using UnityEngine;

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
        StartCoroutine(SubscribeWhenReady());
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    IEnumerator SubscribeWhenReady()
    {
        while (GameRulesManager.Instance == null)
        {
            yield return null;
        }

        Unsubscribe();
        GameRulesManager.Instance.PlayerGoalScored += OnPlayerGoalScored;
        GameRulesManager.Instance.PlayerShotResolved += OnPlayerShotResolved;
        GameRulesManager.Instance.RoundReset += OnRoundReset;

        while (OpponentBotController.Instance == null)
        {
            yield return null;
        }

        OpponentBotController.Instance.OpponentGoalScored += OnOpponentGoalScored;

        if (LeagueMatchController.Instance != null)
        {
            LeagueMatchController.Instance.MatchCompleted += OnLeagueMatchCompleted;
        }

        if (LeagueService.Instance != null)
        {
            LeagueService.Instance.PlayerPromoted += OnPlayerPromoted;
        }

        TrackMatchStarted();
    }

    void Unsubscribe()
    {
        if (GameRulesManager.Instance != null)
        {
            GameRulesManager.Instance.PlayerGoalScored -= OnPlayerGoalScored;
            GameRulesManager.Instance.PlayerShotResolved -= OnPlayerShotResolved;
            GameRulesManager.Instance.RoundReset -= OnRoundReset;
        }

        if (OpponentBotController.Instance != null)
        {
            OpponentBotController.Instance.OpponentGoalScored -= OnOpponentGoalScored;
        }

        if (LeagueMatchController.Instance != null)
        {
            LeagueMatchController.Instance.MatchCompleted -= OnLeagueMatchCompleted;
        }

        if (LeagueService.Instance != null)
        {
            LeagueService.Instance.PlayerPromoted -= OnPlayerPromoted;
        }
    }

    void OnPlayerGoalScored()
    {
        TrackEvent("player_goal_scored");
    }

    void OnOpponentGoalScored()
    {
        TrackEvent("opponent_goal_scored");
    }

    void OnPlayerShotResolved(CoinIdentity coin, bool shotValid)
    {
        var parameters = new Dictionary<string, string>
        {
            { "valid", shotValid ? "true" : "false" },
            { "coin", coin != null ? coin.gameObject.name : "unknown" }
        };

        TrackEvent("player_shot_resolved", parameters);
    }

    void OnRoundReset()
    {
        TrackEvent("round_reset");
    }

    void OnLeagueMatchCompleted(MatchResultType result)
    {
        var parameters = new Dictionary<string, string>
        {
            { "result", result.ToString().ToLowerInvariant() },
            { "league", LeagueService.Instance != null ? LeagueService.Instance.PlayerLeague.ToString() : "1" }
        };

        if (LeagueMatchController.Instance != null)
        {
            parameters["player_goals"] = LeagueMatchController.Instance.PlayerGoals.ToString();
            parameters["opponent_goals"] = LeagueMatchController.Instance.OpponentGoals.ToString();
        }

        if (MatchSessionContext.HasOpponent)
        {
            parameters["opponent"] = MatchSessionContext.CurrentOpponent.displayName;
            parameters["opponent_id"] = MatchSessionContext.CurrentOpponent.id.ToString();
        }

        TrackEvent("league_match_completed", parameters);
    }

    void OnPlayerPromoted(int newLeague)
    {
        TrackEvent("league_promoted", new Dictionary<string, string>
        {
            { "league", newLeague.ToString() }
        });
    }

    void TrackMatchStarted()
    {
        var parameters = new Dictionary<string, string>
        {
            { "league", LeagueService.Instance != null ? LeagueService.Instance.PlayerLeague.ToString() : "1" }
        };

        if (MatchSessionContext.HasOpponent)
        {
            parameters["opponent"] = MatchSessionContext.CurrentOpponent.displayName;
            parameters["opponent_id"] = MatchSessionContext.CurrentOpponent.id.ToString();
            parameters["difficulty"] = MatchSessionContext.CurrentOpponent.difficultyLevel.ToString();
        }

        TrackEvent("match_started", parameters);
    }

    static void TrackEvent(string eventName)
    {
        ByteBrew.NewCustomEvent(eventName);
    }

    static void TrackEvent(string eventName, Dictionary<string, string> parameters)
    {
        ByteBrew.NewCustomEvent(eventName, parameters);
    }
}

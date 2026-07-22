using UnityEngine;

/// <summary>
/// Online gol sync: lokal gol → RPC; remote celebration; RoundReset herkese aynı anda.
/// </summary>
public static class OnlineMatchGoalSync
{
    static bool _rulesBound;
    static bool _applyingRemoteGoal;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        OnlineMatchSession.SessionStarted += EnsureBound;
        OnlineMatchSession.SessionCleared += Unbind;
    }

    public static void EnsureBound()
    {
        if (!OnlineMatchSession.IsOnlineMatch && !MatchSessionContext.IsOnlineMatch)
        {
            return;
        }

        if (!_rulesBound && GameRulesManager.Instance != null)
        {
            GameRulesManager.Instance.PlayerGoalScored -= OnLocalPlayerGoalScored;
            GameRulesManager.Instance.PlayerGoalScored += OnLocalPlayerGoalScored;
            _rulesBound = true;
        }

        TryBindRelay();
    }

    static void Unbind()
    {
        if (GameRulesManager.Instance != null)
        {
            GameRulesManager.Instance.PlayerGoalScored -= OnLocalPlayerGoalScored;
        }

        if (MatchShotNetworkRelay.Instance != null)
        {
            MatchShotNetworkRelay.Instance.GoalReceived -= OnRemoteGoal;
            MatchShotNetworkRelay.Instance.RoundResetReceived -= OnNetworkRoundReset;
        }

        _rulesBound = false;
    }

    public static void TryBindRelay()
    {
        if (MatchShotNetworkRelay.Instance == null)
        {
            return;
        }

        MatchShotNetworkRelay.Instance.GoalReceived -= OnRemoteGoal;
        MatchShotNetworkRelay.Instance.GoalReceived += OnRemoteGoal;
        MatchShotNetworkRelay.Instance.RoundResetReceived -= OnNetworkRoundReset;
        MatchShotNetworkRelay.Instance.RoundResetReceived += OnNetworkRoundReset;
    }

    static void OnLocalPlayerGoalScored()
    {
        if (_applyingRemoteGoal || !OnlineMatchSession.IsOnlineMatch)
        {
            return;
        }

        // Skor zaten LeagueMatchController.OnPlayerGoal ile arttı.
        // Celebration reset'siz; RoundReset RPC ile gelecek.
        MatchShotNetworkRelay.Instance?.TrySendGoal(localPlayerScored: true);
    }

    static void OnRemoteGoal(bool senderScored)
    {
        if (!senderScored)
        {
            return;
        }

        _applyingRemoteGoal = true;
        try
        {
            Debug.Log("[OnlineGoal] Remote scored — enemy celebration (reset RPC bekleniyor)");
            LeagueMatchController.Instance?.RegisterOpponentGoalFromNetwork();
            if (GameRulesManager.Instance != null)
            {
                if (!GameRulesManager.Instance.IsMatchLockedForInput)
                {
                    GameRulesManager.Instance.TryBeginGoalSequence(pauseOpponent: false);
                }

                GameRulesManager.Instance.HandleEnemyGoalCelebrationOnline();
            }
        }
        finally
        {
            _applyingRemoteGoal = false;
        }
    }

    static void OnNetworkRoundReset()
    {
        Debug.Log("[OnlineGoal] Network RoundReset — tahta senkron reset");
        if (GameRulesManager.Instance == null)
        {
            return;
        }

        if (!GameRulesManager.Instance.IsMatchLockedForInput)
        {
            GameRulesManager.Instance.TryBeginGoalSequence(pauseOpponent: false);
        }

        GameRulesManager.Instance.BeginRoundResetAfterGoal();
    }
}

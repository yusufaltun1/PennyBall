using UnityEngine;

/// <summary>
/// Online gol sync: lokal gol → RPC; remote celebration; RoundReset herkese aynı anda.
/// </summary>
public static class OnlineMatchGoalSync
{
    static bool _applyingRemoteGoal;
    static GameRulesManager _boundRules;

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

        // _boundRules, GameRulesManager.Instance ile karşılaştırılıyor — sahne yeniden
        // yüklenip (yeni maç) Instance değiştiğinde yeniden abone olunsun diye. Eskiden
        // bir kere-abone-ol bayrağı kullanılıyordu; bu, ikinci/sonraki her maçta golün
        // hiç ağa gönderilmemesine (eski, yok olmuş GameRulesManager'a abone kalındığı
        // için) ve dolayısıyla kalıcı donmaya yol açıyordu.
        if (!ReferenceEquals(_boundRules, GameRulesManager.Instance))
        {
            if (_boundRules != null)
            {
                _boundRules.PlayerGoalScored -= OnLocalPlayerGoalScored;
            }

            _boundRules = GameRulesManager.Instance;
            if (_boundRules != null)
            {
                _boundRules.PlayerGoalScored += OnLocalPlayerGoalScored;
            }
        }

        TryBindRelay();
    }

    static void Unbind()
    {
        if (_boundRules != null)
        {
            _boundRules.PlayerGoalScored -= OnLocalPlayerGoalScored;
            _boundRules = null;
        }

        if (MatchShotNetworkRelay.Instance != null)
        {
            MatchShotNetworkRelay.Instance.GoalReceived -= OnRemoteGoal;
            MatchShotNetworkRelay.Instance.RoundResetReceived -= OnNetworkRoundReset;
        }
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

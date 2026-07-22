using System;
using System.Threading.Tasks;
using Nakama;
using UnityEngine;

[Serializable]
public class MatchResultPayload
{
    public string matchId;
    public string localUserId;
    public string opponentUserId;
    public int localGoals;
    public int opponentGoals;
    public int durationSeconds;
    public string result; // win|draw|loss|forfeit
    public bool isHost;
}

[Serializable]
public class MatchResultRpcResponse
{
    public bool accepted;
    public bool mismatch;
    public bool waiting;
    public string reason;
    public int coinsGranted;
    public int xpGranted;
    public int totalCoins;
    public int totalXp;
    public int leaguePlayed;
    public int leagueWins;
    public int leagueDraws;
    public int leagueLosses;
    public int leaguePoints;
    public bool walletUpdated;
    public bool leagueUpdated;
}

/// <summary>
/// Nakama RPC submit_match_result — skor tutarlılığı + authoritative wallet/lig ödülü.
/// </summary>
public static class MatchResultSubmitter
{
    public static async Task<MatchResultRpcResponse> SubmitAsync(MatchResultPayload payload)
    {
        NetworkBootstrap bootstrap = NetworkBootstrap.Instance;
        if (bootstrap == null || bootstrap.Auth == null)
        {
            return new MatchResultRpcResponse { accepted = false, reason = "no_bootstrap" };
        }

        if (!bootstrap.Auth.IsAuthenticated)
        {
            bool ok = await bootstrap.EnsureAuthenticatedAsync();
            if (!ok)
            {
                return new MatchResultRpcResponse { accepted = false, reason = "not_authenticated" };
            }
        }

        try
        {
            string json = JsonUtility.ToJson(payload);
            IApiRpc rpc = await bootstrap.Auth.Client.RpcAsync(
                bootstrap.Auth.Session,
                "submit_match_result",
                json);

            if (string.IsNullOrEmpty(rpc.Payload))
            {
                return new MatchResultRpcResponse { accepted = true, reason = "empty_payload" };
            }

            return JsonUtility.FromJson<MatchResultRpcResponse>(rpc.Payload)
                   ?? new MatchResultRpcResponse { accepted = true };
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[MatchResult] Submit failed: {ex.Message}");
            return new MatchResultRpcResponse { accepted = false, reason = ex.Message };
        }
    }

    /// <param name="applyWalletFromServer">true ise local AddReward atlandı; sunucu yanıtı uygulanır / fallback.</param>
    public static async void SubmitFromLocalMatch(
        MatchResultType result,
        int durationSeconds,
        string abandonReason = null,
        bool applyWalletFromServer = false,
        int levelBefore = 0)
    {
        if (!OnlineMatchSession.IsOnlineMatch && !MatchSessionContext.IsOnlineMatch)
        {
            return;
        }

        string resultKey = result switch
        {
            MatchResultType.Win => "win",
            MatchResultType.Draw => "draw",
            _ => "loss"
        };
        if (!string.IsNullOrEmpty(abandonReason))
        {
            resultKey = "forfeit";
        }

        var payload = new MatchResultPayload
        {
            matchId = string.IsNullOrEmpty(OnlineMatchSession.MatchId)
                ? $"local_{Guid.NewGuid():N}"
                : OnlineMatchSession.MatchId,
            localUserId = NetworkBootstrap.Instance?.Auth?.UserId,
            opponentUserId = OnlineMatchSession.OpponentUserId,
            localGoals = MatchSessionContext.PlayerGoalsAtEnd,
            opponentGoals = MatchSessionContext.OpponentGoalsAtEnd,
            durationSeconds = durationSeconds,
            result = resultKey,
            isHost = OnlineMatchSession.Channel != null && OnlineMatchSession.Channel.IsHost
        };

        MatchResultRpcResponse response = await SubmitAsync(payload);
        if (response.mismatch)
        {
            Debug.LogWarning($"[MatchResult] Score mismatch detected: {response.reason}");
        }

        if (applyWalletFromServer)
        {
            ApplyWalletResponse(response, result, abandonReason, levelBefore);
        }

        // Lig tablosu hâlâ local season (botlar dahil). Sunucu player_stats birikir;
        // absolute overwrite bot/local ilerlemeyi siler — SyncStandingsAsync ileride kullanılır.
        if (OnlineLeagueService.UseOnlineLeague && response.accepted && response.leagueUpdated)
        {
            Debug.Log(
                $"[MatchResult] Nakama league stats played={response.leaguePlayed} " +
                $"W/D/L={response.leagueWins}/{response.leagueDraws}/{response.leagueLosses} " +
                $"pts={response.leaguePoints}");
        }

        Debug.Log(
            $"[MatchResult] submit accepted={response.accepted} waiting={response.waiting} " +
            $"wallet={response.walletUpdated} coins+={response.coinsGranted} xp+={response.xpGranted} " +
            $"totals={response.totalCoins}/{response.totalXp} reason={response.reason}");
    }

    static void ApplyWalletResponse(
        MatchResultRpcResponse response,
        MatchResultType result,
        string abandonReason,
        int levelBefore)
    {
        if (response.accepted && response.walletUpdated)
        {
            WalletService.SetTotals(response.totalCoins, response.totalXp);
            MatchSessionContext.SetEarnedRewards(
                response.coinsGranted,
                response.xpGranted,
                levelBefore,
                WalletService.Level);
            MatchSessionContext.SetPendingBoosterUnlock(
                BoosterConfig.GetUnlockReachedOnLevelUp(levelBefore, WalletService.Level));
            return;
        }

        // Nakama yok / hata: local formülle fallback
        int coins = 0;
        int xp = 0;
        if (string.IsNullOrEmpty(abandonReason))
        {
            (coins, xp) = WalletService.GetReward(result);
        }

        if (coins > 0 || xp > 0)
        {
            WalletService.AddReward(coins, xp);
        }

        MatchSessionContext.SetEarnedRewards(coins, xp, levelBefore, WalletService.Level);
        MatchSessionContext.SetPendingBoosterUnlock(
            BoosterConfig.GetUnlockReachedOnLevelUp(levelBefore, WalletService.Level));
        Debug.LogWarning(
            $"[MatchResult] Wallet fallback local (+{coins}c +{xp}xp). reason={response.reason}");
    }
}

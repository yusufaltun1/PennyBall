using System;
using System.Threading.Tasks;
using Nakama;
using UnityEngine;

/// <summary>
/// Nakama lig istatistikleri — submit_match_result yazar; get_league_standings okur.
/// Tam standings tablosu henüz yok; oyuncu stats sync edilir.
/// </summary>
public static class OnlineLeagueService
{
    const string PrefUseOnline = "pb.online.league";

    /// <summary>Editor'da varsayılan açık. Online maç sonrası player stats sunucudan uygulanır.</summary>
    public static bool UseOnlineLeague
    {
        get
        {
            if (!PlayerPrefs.HasKey(PrefUseOnline))
            {
#if UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }

            return PlayerPrefs.GetInt(PrefUseOnline, 0) == 1;
        }
        set
        {
            PlayerPrefs.SetInt(PrefUseOnline, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    [Serializable]
    class LeaguePlayerStats
    {
        public int played;
        public int wins;
        public int draws;
        public int losses;
        public int points;
    }

    [Serializable]
    class LeagueStandingsPayload
    {
        public int version;
        public LeaguePlayerStats player;
    }

    public static async Task<bool> SyncStandingsAsync()
    {
        if (!UseOnlineLeague)
        {
            return false;
        }

        NetworkBootstrap bootstrap = NetworkBootstrap.Instance;
        if (bootstrap?.Auth == null || !bootstrap.Auth.IsAuthenticated)
        {
            return false;
        }

        try
        {
            IApiRpc rpc = await bootstrap.Auth.Client.RpcAsync(
                bootstrap.Auth.Session,
                "get_league_standings",
                "{}");

            if (string.IsNullOrEmpty(rpc.Payload))
            {
                return false;
            }

            var payload = JsonUtility.FromJson<LeagueStandingsPayload>(rpc.Payload);
            if (payload?.player == null)
            {
                return false;
            }

            // Bot ligiyle karışmasın: sadece online-only modda absolute sync.
            if (OnlineFeatureFlags.OnlineOnlyMatches)
            {
                LeagueService.Instance?.ApplyOnlinePlayerStats(
                    payload.player.played,
                    payload.player.wins,
                    payload.player.draws,
                    payload.player.points);
            }
            else
            {
                Debug.Log(
                    $"[OnlineLeague] Server stats played={payload.player.played} " +
                    $"W/D/L={payload.player.wins}/{payload.player.draws}/{payload.player.losses} " +
                    $"pts={payload.player.points} (local season korunuyor)");
            }

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[OnlineLeague] Sync failed: {ex.Message}");
            return false;
        }
    }
}

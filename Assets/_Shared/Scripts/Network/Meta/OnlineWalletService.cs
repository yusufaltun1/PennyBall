using System;
using System.Threading.Tasks;
using Nakama;
using UnityEngine;

/// <summary>
/// Nakama wallet — submit_match_result authoritative; get_wallet pull; set_wallet sadece seed.
/// </summary>
public static class OnlineWalletService
{
    const string PrefUseOnline = "pb.online.wallet";

    /// <summary>Editor'da varsayılan açık. Online maçta ödül sunucudan uygulanır.</summary>
    public static bool UseOnlineWallet
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
    class WalletState
    {
        public int coins;
        public int xp;
    }

    public static async Task<bool> PullAndApplyAsync()
    {
        if (!UseOnlineWallet)
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
                "get_wallet",
                "{}");

            var state = JsonUtility.FromJson<WalletState>(rpc.Payload);
            if (state == null)
            {
                return false;
            }

            WalletService.SetTotals(state.coins, state.xp);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[OnlineWallet] Pull failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>Sadece sunucuda wallet yoksa local'i seed eder (overwrite yok).</summary>
    public static async Task SeedIfEmptyAsync()
    {
        if (!UseOnlineWallet)
        {
            return;
        }

        NetworkBootstrap bootstrap = NetworkBootstrap.Instance;
        if (bootstrap?.Auth == null || !bootstrap.Auth.IsAuthenticated)
        {
            return;
        }

        var state = new WalletState
        {
            coins = WalletService.TotalCoins,
            xp = WalletService.TotalXp
        };

        try
        {
            await bootstrap.Auth.Client.RpcAsync(
                bootstrap.Auth.Session,
                "set_wallet",
                JsonUtility.ToJson(state));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[OnlineWallet] Seed failed: {ex.Message}");
        }
    }

    /// <summary>Seed (boşsa) sonra pull — auth sonrası tek giriş noktası.</summary>
    public static async Task SyncFromServerAsync()
    {
        if (!UseOnlineWallet)
        {
            return;
        }

        await SeedIfEmptyAsync();
        await PullAndApplyAsync();
    }

    [Obsolete("Use SeedIfEmptyAsync — set_wallet artık overwrite yapmaz.")]
    public static Task PushLocalAsync() => SeedIfEmptyAsync();
}

using UnityEngine;

/// <summary>
/// Online multiplayer feature flags. PlayerPrefs ile runtime toggle.
/// </summary>
public static class OnlineFeatureFlags
{
    const string PrefMatchmaking = "pb.online.matchmaking";
    const string PrefOnlineOnly = "pb.online.online_only";
    const string PrefUseFusion = "pb.online.use_fusion";

    /// <summary>Kapalıyken Matching tamamen bot path kullanır.</summary>
    public static bool OnlineMatchmakingEnabled
    {
        get
        {
            if (!PlayerPrefs.HasKey(PrefMatchmaking))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return false;
#endif
            }

            return PlayerPrefs.GetInt(PrefMatchmaking, 0) == 1;
        }
        set
        {
            PlayerPrefs.SetInt(PrefMatchmaking, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    /// <summary>Açıkken bot fallback yapılmaz; eşleşme bulunamazsa hata/geri dön.</summary>
    public static bool OnlineOnlyMatches
    {
        get => PlayerPrefs.GetInt(PrefOnlineOnly, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(PrefOnlineOnly, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// true ise Photon Fusion kanalı kullanılır (maç içi). Varsayılan: açık.
    /// </summary>
    public static bool PreferPhotonFusion
    {
        get => PlayerPrefs.GetInt(PrefUseFusion, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(PrefUseFusion, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}

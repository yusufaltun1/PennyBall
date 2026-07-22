using UnityEngine;
using Fusion.Photon.Realtime;

/// <summary>
/// Maç içi kanal fabrikası — gameplay için Photon Fusion (Shared).
/// AppId yoksa hata loglanır (Nakama realtime'a sessiz düşülmez).
/// </summary>
public static class MatchRealtimeFactory
{
    public static IMatchRealtimeChannel Create(NetworkBootstrap bootstrap)
    {
        if (bootstrap == null)
        {
            Debug.LogError("[Realtime] NetworkBootstrap missing");
            return null;
        }

        // Photon UserId her instance'ta benzersiz olmalı (ParrelSync PlayerPrefs paylaşır).
        // Nakama userId ile karıştırma — aynı id ile 2. peer join edemez (Error 32746).
        string localUserId = DeviceIdStore.GetPhotonUserId();
        Debug.Log($"[Realtime] Photon localUserId={localUserId}");

        string appId = ResolveFusionAppId(bootstrap.Config);
        if (string.IsNullOrEmpty(appId))
        {
            Debug.LogError(
                "[Realtime] Photon Fusion AppId yok. PhotonAppSettings / NetworkConfig doldur. " +
                "Maç içi sync Photon zorunlu.");
            return null;
        }

        // Config'e yaz ki channel ApplyPhotonAppSettings bulsun
        if (bootstrap.Config != null && string.IsNullOrEmpty(bootstrap.Config.photonFusionAppId))
        {
            bootstrap.Config.photonFusionAppId = appId;
        }

        OnlineFeatureFlags.PreferPhotonFusion = true;
        return new PhotonFusionMatchChannel(bootstrap.Config, localUserId);
    }

    static string ResolveFusionAppId(NetworkConfig config)
    {
        if (config != null && !string.IsNullOrEmpty(config.photonFusionAppId))
        {
            return config.photonFusionAppId;
        }

        return PhotonAppSettings.Global?.AppSettings?.AppIdFusion;
    }
}

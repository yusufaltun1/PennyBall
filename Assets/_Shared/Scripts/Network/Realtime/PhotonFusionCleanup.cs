using System.Threading.Tasks;
using Fusion;
using UnityEngine;

/// <summary>
/// Maçlar arası Fusion runner / relay kalıntılarını temizler.
/// Dispose fire-and-forget olduğunda ikinci maçta çift runner ve MatchStart takılması oluşabiliyor.
/// </summary>
public static class PhotonFusionCleanup
{
    public static async Task ForceShutdownAllAsync()
    {
        MatchShotNetworkRelay.ResetStaticState();

        NetworkRunner[] runners = Object.FindObjectsByType<NetworkRunner>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < runners.Length; i++)
        {
            NetworkRunner runner = runners[i];
            if (runner == null)
            {
                continue;
            }

            try
            {
                if (runner.IsRunning)
                {
                    await runner.Shutdown();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[PhotonFusion] Shutdown: {ex.Message}");
            }

            if (runner.gameObject != null)
            {
                Object.Destroy(runner.gameObject);
            }
        }

        GameObject legacy = GameObject.Find("PhotonFusionRunner");
        if (legacy != null)
        {
            Object.Destroy(legacy);
        }
    }

    public static void ForceShutdownAll()
    {
        MatchShotNetworkRelay.ResetStaticState();

        NetworkRunner[] runners = Object.FindObjectsByType<NetworkRunner>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < runners.Length; i++)
        {
            NetworkRunner runner = runners[i];
            if (runner == null)
            {
                continue;
            }

            try
            {
                if (runner.IsRunning)
                {
                    runner.Shutdown();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[PhotonFusion] Shutdown sync: {ex.Message}");
            }

            if (runner.gameObject != null)
            {
                Object.Destroy(runner.gameObject);
            }
        }

        GameObject legacy = GameObject.Find("PhotonFusionRunner");
        if (legacy != null)
        {
            Object.Destroy(legacy);
        }
    }
}

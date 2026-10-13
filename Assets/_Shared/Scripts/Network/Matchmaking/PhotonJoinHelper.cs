using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Photon kanalında 2. oyuncuyu bekleme döngüsü — quick-match ve oda kodu
/// (Create/Join) akışları arasında paylaşılır.
/// </summary>
public static class PhotonJoinHelper
{
    public static async Task<bool> WaitForRemotePlayerAsync(
        IMatchRealtimeChannel channel,
        float timeoutSeconds,
        System.IProgress<float> progress = null,
        string logPrefix = "[PhotonJoin]")
    {
        float elapsed = 0f;
        float nextLogAt = 1f;

        while (channel.RemotePlayerCount < 1 && elapsed < timeoutSeconds)
        {
            progress?.Report(Mathf.Clamp01(elapsed / Mathf.Max(0.01f, timeoutSeconds)));

            // Main thread frame — Task.Delay bazen ActivePlayers'ı stale bırakıyordu.
            await Task.Yield();
            elapsed += Time.unscaledDeltaTime;

            if (elapsed >= nextLogAt)
            {
                nextLogAt += 1f;
                Debug.Log(
                    $"{logPrefix} wait {elapsed:F0}s remotes={channel.RemotePlayerCount} " +
                    $"connected={channel.IsConnected} host={channel.IsHost}");
            }
        }

        return channel.RemotePlayerCount >= 1;
    }
}

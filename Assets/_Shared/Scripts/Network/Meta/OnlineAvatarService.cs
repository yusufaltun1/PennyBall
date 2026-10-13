using System;
using System.Threading.Tasks;
using Nakama;
using UnityEngine;

/// <summary>
/// Nakama profil (avatar + display_name) senkronu — her değişiklikte set_avatar/
/// set_display_name ile üzerine yazılır (wallet'in aksine seed değil, her zaman güncel
/// değeri gönderir). Create/Join oda akışı karşı tarafın bu bilgilerini buradan
/// (host_avatar_index/host_name, guest_avatar_index/guest_name) okur.
/// </summary>
public static class OnlineAvatarService
{
    [Serializable]
    class AvatarPayload
    {
        public int avatar_index;
    }

    [Serializable]
    class DisplayNamePayload
    {
        public string display_name;
    }

    public static async Task PushAsync(int avatarIndex)
    {
        NetworkBootstrap bootstrap = NetworkBootstrap.Instance;
        if (bootstrap == null || !await bootstrap.EnsureAuthenticatedAsync())
        {
            return;
        }

        try
        {
            string json = JsonUtility.ToJson(new AvatarPayload { avatar_index = avatarIndex });
            await bootstrap.Auth.Client.RpcAsync(bootstrap.Auth.Session, "set_avatar", json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[OnlineAvatar] Avatar push failed: {ex.Message}");
        }
    }

    public static async Task PushDisplayNameAsync(string displayName)
    {
        if (string.IsNullOrEmpty(displayName))
        {
            return;
        }

        NetworkBootstrap bootstrap = NetworkBootstrap.Instance;
        if (bootstrap == null || !await bootstrap.EnsureAuthenticatedAsync())
        {
            return;
        }

        try
        {
            string json = JsonUtility.ToJson(new DisplayNamePayload { display_name = displayName });
            await bootstrap.Auth.Client.RpcAsync(bootstrap.Auth.Session, "set_display_name", json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[OnlineAvatar] Display name push failed: {ex.Message}");
        }
    }
}

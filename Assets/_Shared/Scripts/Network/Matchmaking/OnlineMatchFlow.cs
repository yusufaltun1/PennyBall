using System;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Play matchmaking: Photon quick-match (oda + 2. oyuncu bekle).
/// Nakama matchmaker opsiyonel / kısa; maç içi her zaman Photon.
/// Create/Join ile aynı model: 2 kişi odada → sonra Game → 3-2-1.
/// </summary>
public static class OnlineMatchFlow
{
    /// <summary>Aynı 2 dk penceresindeki Play'ler aynı Photon odasına düşer.</summary>
    const int PhotonQuickMatchWindowSeconds = 120;

    /// <summary>Nakama kuyruk denemesi (0 = atla). Asıl eşleşme Photon QM.</summary>
    const float NakamaMatchmakerProbeSeconds = 0f;

    public static async Task<MatchmakingResult> ResolveOpponentAsync(
        float? timeoutOverride = null,
        IProgress<float> progress = null)
    {
        if (!OnlineFeatureFlags.OnlineMatchmakingEnabled)
        {
            return FallbackBot("flag_disabled");
        }

        NetworkBootstrap bootstrap = NetworkBootstrap.Instance;
        if (bootstrap == null)
        {
            var go = new GameObject("NetworkBootstrap");
            bootstrap = go.AddComponent<NetworkBootstrap>();
        }

        // Wallet/RPC için auth — matchmaking'i bloklamasın.
        _ = bootstrap.EnsureAuthenticatedAsync();

        // Opsiyonel Nakama probe (varsayılan kapalı — 45sn beklemeyi öldürür).
        if (NakamaMatchmakerProbeSeconds > 0f)
        {
            bool authed = bootstrap.Auth != null && bootstrap.Auth.IsAuthenticated;
            if (!authed)
            {
                authed = await bootstrap.EnsureAuthenticatedAsync();
            }

            if (authed)
            {
                var matchmaker = new NakamaMatchmaker(bootstrap.Auth, bootstrap.Config);
                MatchmakingResult nakamaResult = await matchmaker.FindMatchAsync(
                    NakamaMatchmakerProbeSeconds,
                    progress);

                if (nakamaResult.Success && nakamaResult.Source == MatchOpponentSource.Human)
                {
                    // Nakama match id = Photon room; Game sahnesinde join (Pending).
                    return CommitHumanMatchPendingJoin(nakamaResult);
                }
            }
        }

        float timeout = timeoutOverride ?? bootstrap.Config.matchmakingTimeoutSeconds;
        return await ResolvePhotonQuickMatchAsync(bootstrap, timeout, progress);
    }

    static async Task<MatchmakingResult> ResolvePhotonQuickMatchAsync(
        NetworkBootstrap bootstrap,
        float timeoutSeconds,
        IProgress<float> progress)
    {
        MatchmakingResult quick = BuildPhotonQuickMatch();
        IMatchRealtimeChannel channel = MatchRealtimeFactory.Create(bootstrap);
        if (channel == null)
        {
            return OnlineFeatureFlags.OnlineOnlyMatches
                ? MatchmakingResult.Fail("photon_no_appid")
                : FallbackBot("photon_no_appid");
        }

        try
        {
            await channel.JoinAsync(quick.PhotonRoomName);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MatchFlow] Photon QM join failed: {ex.Message}");
            channel.Dispose();
            return OnlineFeatureFlags.OnlineOnlyMatches
                ? MatchmakingResult.Fail(ex.Message)
                : FallbackBot("photon_join_failed");
        }

        Debug.Log(
            $"[MatchFlow] Photon QM joined room='{quick.PhotonRoomName}' — 2. oyuncu bekleniyor...");

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
                    $"[MatchFlow] QM wait {elapsed:F0}s remotes={channel.RemotePlayerCount} " +
                    $"connected={channel.IsConnected} host={channel.IsHost}");
            }
        }

        if (channel.RemotePlayerCount < 1)
        {
            Debug.LogWarning("[MatchFlow] Photon QM timeout — rakip gelmedi.");
            try
            {
                await channel.LeaveAsync();
            }
            catch
            {
                channel.Dispose();
            }

            return OnlineFeatureFlags.OnlineOnlyMatches
                ? MatchmakingResult.Fail("photon_qm_timeout", timedOut: true)
                : FallbackBot("photon_qm_timeout");
        }

        Debug.Log(
            $"[MatchFlow] Photon QM eşleşti remotes={channel.RemotePlayerCount} room='{quick.PhotonRoomName}'");

        // Authorize YOK — Game sahnesinde Ready → MatchStart ile 3-2-1 hizalanır.
        PendingPhotonSession.Clear();
        OnlineMatchSession.BeginHumanMatch(quick, channel);

        ApplyOpponentContext(quick);
        return quick;
    }

    static MatchmakingResult BuildPhotonQuickMatch()
    {
        int league = LeagueService.Instance != null ? LeagueService.Instance.PlayerLeague : 1;
        long window = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / PhotonQuickMatchWindowSeconds;
        string room = SanitizeRoom($"pb_qm_L{league}_{window}");

        Debug.Log($"[MatchFlow] Photon quick-match room='{room}'");

        return MatchmakingResult.Human(
            matchId: room,
            photonRoomName: room,
            opponentUserId: "photon_qm",
            displayName: "Opponent",
            avatarIndex: UnityEngine.Random.Range(0, 20));
    }

    /// <summary>Game sahnesinde join (Create/Join debug + Nakama room id).</summary>
    static MatchmakingResult CommitHumanMatchPendingJoin(MatchmakingResult result)
    {
        PendingPhotonSession.SetForHumanMatch(result);
        ApplyOpponentContext(result);
        return result;
    }

    static void ApplyOpponentContext(MatchmakingResult result)
    {
        var humanAsBot = new BotPlayerEntry
        {
            id = unchecked((int)HashString(result.OpponentUserId ?? result.MatchId)),
            displayName = string.IsNullOrEmpty(result.OpponentDisplayName)
                ? "Opponent"
                : result.OpponentDisplayName,
            avatarIndex = result.OpponentAvatarIndex,
            difficultyLevel = 12,
            homeLeague = LeagueService.Instance != null ? LeagueService.Instance.PlayerLeague : 1,
            countryCode = "XX"
        };
        MatchSessionContext.SetOpponent(humanAsBot);
        MatchSessionContext.SetOnlineMatch(true, result.OpponentUserId, result.MatchId);
    }

    static MatchmakingResult FallbackBot(string reason)
    {
        OnlineMatchSession.Clear();
        PendingPhotonSession.Clear();
        MatchSessionContext.SetOnlineMatch(false, null, null);

        BotPlayerEntry bot = LeagueService.Instance != null
            ? LeagueService.Instance.PickOpponentForNextMatch()
            : null;

        if (bot != null)
        {
            MatchSessionContext.SetOpponent(bot);
        }

        return MatchmakingResult.BotFallback(bot, reason);
    }

    static string SanitizeRoom(string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return "pb_qm";
        }

        char[] buffer = new char[Mathf.Min(raw.Length, 64)];
        int n = 0;
        for (int i = 0; i < raw.Length && n < buffer.Length; i++)
        {
            char c = raw[i];
            buffer[n++] = char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_';
        }

        return new string(buffer, 0, n);
    }

    static uint HashString(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return 0;
        }

        unchecked
        {
            uint hash = 2166136261;
            for (int i = 0; i < value.Length; i++)
            {
                hash = (hash ^ value[i]) * 16777619;
            }

            return hash;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nakama;
using UnityEngine;

/// <summary>
/// Nakama matchmaker — 1v1. Timeout'ta çağıran taraf bot fallback yapar.
/// </summary>
public sealed class NakamaMatchmaker : IMatchmaker
{
    readonly NakamaAuthService _auth;
    readonly NetworkConfig _config;
    CancellationTokenSource _cts;
    string _ticket;

    public NakamaMatchmaker(NakamaAuthService auth, NetworkConfig config)
    {
        _auth = auth;
        _config = config;
    }

    public void Cancel()
    {
        _cts?.Cancel();
        _ = RemoveTicketSafeAsync();
    }

    public async Task<MatchmakingResult> FindMatchAsync(float timeoutSeconds, IProgress<float> progress = null)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        CancellationToken token = _cts.Token;

        if (_auth == null || !await EnsureReadyAsync())
        {
            return MatchmakingResult.Fail("nakama_not_ready");
        }

        ISocket socket = _auth.Socket;
        var tcs = new TaskCompletionSource<MatchmakingResult>();

        void OnMatched(IMatchmakerMatched matched)
        {
            try
            {
                string opponentUserId = null;
                string displayName = "Opponent";
                foreach (IMatchmakerUser user in matched.Users)
                {
                    if (user.Presence.UserId == _auth.UserId)
                    {
                        continue;
                    }

                    opponentUserId = user.Presence.UserId;
                    displayName = string.IsNullOrEmpty(user.Presence.Username)
                        ? opponentUserId
                        : user.Presence.Username;
                    break;
                }

                string matchId = matched.MatchId;
                NakamaMatchmakerPending.LastMatched = matched;
                // Photon room adı olarak Nakama match id kullanılır (Fusion bağlanınca aynı id).
                tcs.TrySetResult(MatchmakingResult.Human(
                    matchId,
                    matchId,
                    opponentUserId,
                    displayName,
                    AvatarIndexFromUserId(opponentUserId)));
            }
            catch (Exception ex)
            {
                tcs.TrySetResult(MatchmakingResult.Fail(ex.Message));
            }
        }

        socket.ReceivedMatchmakerMatched += OnMatched;

        try
        {
            IMatchmakerTicket ticket = await socket.AddMatchmakerAsync(
                _config.matchmakerQuery,
                _config.matchmakerMinCount,
                _config.matchmakerMaxCount,
                new Dictionary<string, string>
                {
                    { "mode", "1v1" },
                    { "league", LeagueService.Instance != null
                        ? LeagueService.Instance.PlayerLeague.ToString()
                        : "1" }
                });

            _ticket = ticket.Ticket;

            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                token.ThrowIfCancellationRequested();

                if (tcs.Task.IsCompleted)
                {
                    return await tcs.Task;
                }

                progress?.Report(elapsed / timeoutSeconds);
                await Task.Delay(100, token);
                elapsed += 0.1f;
            }

            await RemoveTicketSafeAsync();
            return MatchmakingResult.Fail("timeout", timedOut: true);
        }
        catch (OperationCanceledException)
        {
            await RemoveTicketSafeAsync();
            return MatchmakingResult.Fail("cancelled");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Matchmaker] {ex.Message}");
            return MatchmakingResult.Fail(ex.Message);
        }
        finally
        {
            socket.ReceivedMatchmakerMatched -= OnMatched;
        }
    }

    async Task<bool> EnsureReadyAsync()
    {
        if (_auth.IsAuthenticated && _auth.Socket != null && _auth.Socket.IsConnected)
        {
            return true;
        }

        return await _auth.AuthenticateAsync();
    }

    async Task RemoveTicketSafeAsync()
    {
        if (string.IsNullOrEmpty(_ticket) || _auth?.Socket == null || !_auth.Socket.IsConnected)
        {
            _ticket = null;
            return;
        }

        try
        {
            await _auth.Socket.RemoveMatchmakerAsync(_ticket);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Matchmaker] Remove ticket failed: {ex.Message}");
        }

        _ticket = null;
    }

    static int AvatarIndexFromUserId(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return 0;
        }

        unchecked
        {
            int hash = userId.GetHashCode();
            return Math.Abs(hash) % 20;
        }
    }
}

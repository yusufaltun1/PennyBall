using System;
using System.Threading.Tasks;
using Nakama;
using UnityEngine;

[Serializable]
class CreateRoomRpcResponse
{
    public bool ok;
    public string code;
    public string photon_room_name;
    public long expires_at;
    public string reason;
}

[Serializable]
class JoinRoomRpcPayload
{
    public string code;
}

[Serializable]
class JoinRoomRpcResponse
{
    public bool ok;
    public string photon_room_name;
    public string host_user_id;
    public string host_name;
    public int host_avatar_index;
    public string reason;
}

[Serializable]
class RoomCodeGuestRpcResponse
{
    public bool ok;
    public string guest_user_id;
    public string guest_name;
    public int guest_avatar_index;
    public string reason;
}

[Serializable]
class CancelRoomRpcResponse
{
    public bool ok;
    public string reason;
}

public sealed class RoomCodeResult
{
    public bool Success;
    public string Reason;
    public string Code;
    public MatchmakingResult Match;
    public IMatchRealtimeChannel Channel;
}

/// <summary>
/// Arkadaşla oynama: 6 haneli oda kodu ile Create/Join.
/// Kod üretimi/doğrulaması/doluluk Nakama RPC'lerinde (server-authoritative);
/// gerçek maç bağlantısı quick-match ile aynı şekilde Photon üzerinden kurulur.
/// </summary>
public static class PartyRoomService
{
    const float OpponentWaitTimeoutSeconds = 120f;

    /// <summary>Sadece Nakama RPC — kod hemen döner, Photon bağlantısı burada YOK.
    /// UI kodu ekranda göstermeli, sonra <see cref="ConnectAndWaitForOpponentAsync"/> çağrılmalı.</summary>
    public static async Task<RoomCodeResult> RequestCreateCodeAsync()
    {
        NetworkBootstrap bootstrap = NetworkBootstrap.Instance;
        if (bootstrap == null || !await bootstrap.EnsureAuthenticatedAsync())
        {
            return Fail("not_authenticated");
        }

        CreateRoomRpcResponse rpcResult;
        try
        {
            IApiRpc rpc = await bootstrap.Auth.Client.RpcAsync(
                bootstrap.Auth.Session, "create_room_code", "{}");
            rpcResult = JsonUtility.FromJson<CreateRoomRpcResponse>(rpc.Payload);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[PartyRoom] create_room_code failed: {ex.Message}");
            return Fail(ex.Message);
        }

        if (rpcResult == null || !rpcResult.ok)
        {
            return Fail(rpcResult?.reason ?? "rpc_failed");
        }

        // Bu noktada misafir henüz bilinmiyor — gerçek kimliği/avatarı Photon'da
        // karşılaşınca get_room_code_guest ile JoinPhotonAndWaitAsync tamamlar.
        var match = MatchmakingResult.Human(
            matchId: rpcResult.photon_room_name,
            photonRoomName: rpcResult.photon_room_name,
            opponentUserId: null,
            displayName: null,
            avatarIndex: 0);

        return new RoomCodeResult { Success = true, Code = rpcResult.code, Match = match };
    }

    /// <summary>Sadece Nakama RPC — doğrulama hemen döner, Photon bağlantısı burada YOK.
    /// UI başarıyı göstermeli, sonra <see cref="ConnectAndWaitForOpponentAsync"/> çağrılmalı.</summary>
    public static async Task<RoomCodeResult> RequestJoinCodeAsync(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return Fail("invalid_payload");
        }

        NetworkBootstrap bootstrap = NetworkBootstrap.Instance;
        if (bootstrap == null || !await bootstrap.EnsureAuthenticatedAsync())
        {
            return Fail("not_authenticated");
        }

        JoinRoomRpcResponse rpcResult;
        try
        {
            string json = JsonUtility.ToJson(new JoinRoomRpcPayload { code = code });
            IApiRpc rpc = await bootstrap.Auth.Client.RpcAsync(
                bootstrap.Auth.Session, "join_room_code", json);
            rpcResult = JsonUtility.FromJson<JoinRoomRpcResponse>(rpc.Payload);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[PartyRoom] join_room_code failed: {ex.Message}");
            return Fail(ex.Message);
        }

        if (rpcResult == null || !rpcResult.ok)
        {
            return Fail(rpcResult?.reason ?? "rpc_failed");
        }

        var match = MatchmakingResult.Human(
            matchId: rpcResult.photon_room_name,
            photonRoomName: rpcResult.photon_room_name,
            opponentUserId: rpcResult.host_user_id,
            displayName: string.IsNullOrEmpty(rpcResult.host_name) ? "Opponent" : rpcResult.host_name,
            avatarIndex: rpcResult.host_avatar_index);

        return new RoomCodeResult { Success = true, Code = code, Match = match };
    }

    /// <summary>RPC adımından sonra çağrılır: Photon odasına katılır, 2. oyuncuyu bekler.</summary>
    public static async Task<RoomCodeResult> ConnectAndWaitForOpponentAsync(
        RoomCodeResult pending, IProgress<float> progress = null)
    {
        if (pending == null || !pending.Success || pending.Match == null)
        {
            return Fail("invalid_state", pending?.Code);
        }

        NetworkBootstrap bootstrap = NetworkBootstrap.Instance;
        if (bootstrap == null)
        {
            return Fail("no_bootstrap", pending.Code);
        }

        return await JoinPhotonAndWaitAsync(bootstrap, pending.Match, pending.Code, progress);
    }

    public static async Task CancelRoomAsync(string code)
    {
        NetworkBootstrap bootstrap = NetworkBootstrap.Instance;
        if (bootstrap?.Auth == null || !bootstrap.Auth.IsAuthenticated || string.IsNullOrEmpty(code))
        {
            return;
        }

        try
        {
            string json = JsonUtility.ToJson(new JoinRoomRpcPayload { code = code });
            await bootstrap.Auth.Client.RpcAsync(bootstrap.Auth.Session, "cancel_room_code", json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[PartyRoom] cancel_room_code failed: {ex.Message}");
        }
    }

    static async Task<RoomCodeResult> JoinPhotonAndWaitAsync(
        NetworkBootstrap bootstrap, MatchmakingResult match, string code, IProgress<float> progress)
    {
        IMatchRealtimeChannel channel = MatchRealtimeFactory.Create(bootstrap);
        if (channel == null)
        {
            return Fail("photon_no_appid", code);
        }

        try
        {
            await channel.JoinAsync(match.PhotonRoomName);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PartyRoom] Photon join failed: {ex.Message}");
            channel.Dispose();
            return Fail("photon_join_failed", code);
        }

        bool opponentFound = await PhotonJoinHelper.WaitForRemotePlayerAsync(
            channel, OpponentWaitTimeoutSeconds, progress, "[PartyRoom]");

        if (!opponentFound)
        {
            try
            {
                await channel.LeaveAsync();
            }
            catch
            {
                channel.Dispose();
            }

            return Fail("timeout", code);
        }

        // Host (Create) tarafı: misafirin kimliğini/avatarını join_room_code'dan öğrenemedi
        // (o yanıt sadece misafire gidiyor) — Photon'da karşılaştığımıza göre artık
        // get_room_code_guest ile çekebiliriz.
        if (string.IsNullOrEmpty(match.OpponentUserId))
        {
            match = await TryFetchGuestInfoAsync(bootstrap, match, code);
        }

        // Authorize YOK — Game sahnesinde Ready → MatchStart ile hizalanır (quick-match ile aynı).
        PendingPhotonSession.Clear();
        OnlineMatchSession.BeginHumanMatch(match, channel);
        ApplyOpponentContext(match);

        return new RoomCodeResult
        {
            Success = true,
            Code = code,
            Match = match,
            Channel = channel
        };
    }

    static async Task<MatchmakingResult> TryFetchGuestInfoAsync(
        NetworkBootstrap bootstrap, MatchmakingResult match, string code)
    {
        try
        {
            string json = JsonUtility.ToJson(new JoinRoomRpcPayload { code = code });
            IApiRpc rpc = await bootstrap.Auth.Client.RpcAsync(
                bootstrap.Auth.Session, "get_room_code_guest", json);
            RoomCodeGuestRpcResponse response =
                JsonUtility.FromJson<RoomCodeGuestRpcResponse>(rpc.Payload);

            if (response != null && response.ok)
            {
                match.OpponentUserId = response.guest_user_id;
                match.OpponentDisplayName =
                    string.IsNullOrEmpty(response.guest_name) ? "Opponent" : response.guest_name;
                match.OpponentAvatarIndex = response.guest_avatar_index;
            }
            else
            {
                Debug.LogWarning($"[PartyRoom] get_room_code_guest başarısız: {response?.reason}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[PartyRoom] get_room_code_guest failed: {ex.Message}");
        }

        return match;
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

    static RoomCodeResult Fail(string reason, string code = null)
    {
        return new RoomCodeResult { Success = false, Reason = reason, Code = code };
    }
}

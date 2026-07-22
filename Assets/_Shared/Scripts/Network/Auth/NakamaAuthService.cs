using System;
using System.Threading.Tasks;
using Nakama;
using UnityEngine;

/// <summary>
/// Nakama device auth + session/socket yaşam döngüsü.
/// </summary>
public class NakamaAuthService
{
    readonly NetworkConfig _config;
    IClient _client;
    ISession _session;
    ISocket _socket;

    public bool IsAuthenticated => _session != null && !_session.IsExpired;
    public IClient Client => _client;
    public ISession Session => _session;
    public ISocket Socket => _socket;
    public string UserId => _session?.UserId;
    public string Username => _session?.Username;

    public event Action Authenticated;
    public event Action<string> AuthFailed;

    public NakamaAuthService(NetworkConfig config)
    {
        _config = config;
    }

    public async Task<bool> AuthenticateAsync()
    {
        try
        {
            EnsureClient();
            string deviceId = DeviceIdStore.GetOrCreate();
            _session = await _client.AuthenticateDeviceAsync(deviceId, create: true);

            if (_config.logVerbose)
            {
                Debug.Log($"[Nakama] Authenticated userId={_session.UserId} username={_session.Username}");
            }

            await EnsureSocketAsync();
            Authenticated?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Nakama] Auth failed: {ex.Message}");
            AuthFailed?.Invoke(ex.Message);
            return false;
        }
    }

    public async Task<bool> EnsureSocketAsync()
    {
        if (_session == null)
        {
            return false;
        }

        EnsureClient();

        if (_socket == null)
        {
            _socket = _client.NewSocket(useMainThread: true);
        }

        if (_socket.IsConnected)
        {
            return true;
        }

        await _socket.ConnectAsync(_session, appearOnline: true);
        return _socket.IsConnected;
    }

    public async Task DisconnectAsync()
    {
        if (_socket != null && _socket.IsConnected)
        {
            await _socket.CloseAsync();
        }
    }

    void EnsureClient()
    {
        if (_client != null)
        {
            return;
        }

        _client = new Client(
            _config.scheme,
            _config.host,
            _config.port,
            _config.serverKey,
            UnityWebRequestAdapter.Instance);
    }
}

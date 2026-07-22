using System;
using UnityEngine;

/// <summary>
/// DontDestroyOnLoad ağ bootstrap: config, Nakama auth, realtime factory.
/// </summary>
public class NetworkBootstrap : MonoBehaviour
{
    public static NetworkBootstrap Instance { get; private set; }

    [SerializeField] NetworkConfig config;

    NakamaAuthService _auth;
    bool _bootStarted;

    public NetworkConfig Config => config != null ? config : NetworkConfig.LoadOrCreateDefaults();
    public NakamaAuthService Auth => _auth;
    public bool IsReady => _auth != null && _auth.IsAuthenticated;

    public event Action Ready;
    public event Action<string> Failed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (Instance != null)
        {
            return;
        }

        var go = new GameObject("NetworkBootstrap");
        Instance = go.AddComponent<NetworkBootstrap>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Application.runInBackground = true;

        if (config == null)
        {
            config = NetworkConfig.LoadOrCreateDefaults();
        }

        _auth = new NakamaAuthService(Config);
        _auth.Authenticated += () =>
        {
            Ready?.Invoke();
            if (OnlineWalletService.UseOnlineWallet)
            {
                _ = OnlineWalletService.SyncFromServerAsync();
            }
        };
        _auth.AuthFailed += message => Failed?.Invoke(message);
    }

    void Start()
    {
        if (Config.autoAuthenticateOnBoot)
        {
            BootAsync();
        }
    }

    public async void BootAsync()
    {
        if (_bootStarted)
        {
            return;
        }

        _bootStarted = true;
        bool ok = await _auth.AuthenticateAsync();
        if (!ok)
        {
            _bootStarted = false;
        }
    }

    public async System.Threading.Tasks.Task<bool> EnsureAuthenticatedAsync()
    {
        if (_auth.IsAuthenticated)
        {
            await _auth.EnsureSocketAsync();
            return true;
        }

        return await _auth.AuthenticateAsync();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}

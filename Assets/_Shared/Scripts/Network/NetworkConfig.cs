using UnityEngine;

[CreateAssetMenu(fileName = "NetworkConfig", menuName = "PennyBall/Network Config")]
public class NetworkConfig : ScriptableObject
{
    public const string ResourcesPath = "NetworkConfig";

    [Header("Nakama")]
    public string scheme = "http";
    public string host = "127.0.0.1";
    public int port = 7350;
    public string serverKey = "defaultkey";

    [Header("Matchmaking")]
    [Tooltip("Gerçek rakip bulunamazsa bot'a düşme süresi (saniye).")]
    public float matchmakingTimeoutSeconds = 45f;
    public int matchmakerMinCount = 2;
    public int matchmakerMaxCount = 2;
    public string matchmakerQuery = "*";

    [Header("Photon Fusion (maç içi — zorunlu)")]
    public string photonFusionAppId = "";
    [Tooltip("Boş = Photon otomatik region. Örn: eu, us, asia")]
    public string photonRegion = "";

    [Header("Dev")]
    public bool autoAuthenticateOnBoot = true;
    public bool logVerbose = true;

    public static NetworkConfig LoadOrCreateDefaults()
    {
        NetworkConfig config = Resources.Load<NetworkConfig>(ResourcesPath);
        if (config != null)
        {
            return config;
        }

        config = CreateInstance<NetworkConfig>();
        config.name = "NetworkConfig (Runtime Default)";
        return config;
    }
}

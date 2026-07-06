using UnityEngine;

/// <summary>
/// Uygulama ön plana dönünce Meta'ya activate app gönderir.
/// </summary>
public class MetaAppEventsBootstrap : MonoBehaviour
{
    public static MetaAppEventsBootstrap Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnApplicationPause(bool paused)
    {
        if (!paused)
        {
            MetaAppEventsService.ActivateApp();
        }
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            MetaAppEventsService.ActivateApp();
        }
    }
}

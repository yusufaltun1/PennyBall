using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Facebook.Unity;
using UnityEngine;

/// <summary>
/// Meta (Facebook) SDK init + ActivateApp + iOS ATT izni.
/// ATT diyaloğu otomatik çıkmaz; requestTrackingAuthorization gerekir.
/// </summary>
public class MetaAppEventsBootstrap : MonoBehaviour
{
    const float AttPromptDelaySeconds = 1.25f;

    static MetaAppEventsBootstrap _instance;

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    static extern void PB_RequestTrackingAuthorization(string gameObjectName);

    [DllImport("__Internal")]
    static extern int PB_GetTrackingAuthorizationStatus();
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureInstance()
    {
        if (_instance != null)
        {
            return;
        }

        var go = new GameObject("MetaAppEvents");
        _instance = go.AddComponent<MetaAppEventsBootstrap>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeSdk();
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus || !FB.IsInitialized)
        {
            return;
        }

        FB.ActivateApp();
    }

    void OnEnable()
    {
        GameAnalytics.EventRequested += OnGameAnalyticsEvent;
    }

    void OnDisable()
    {
        GameAnalytics.EventRequested -= OnGameAnalyticsEvent;
    }

    void InitializeSdk()
    {
        if (FB.IsInitialized)
        {
            ApplyMobileSettings();
            FB.ActivateApp();
            StartCoroutine(RequestAttWhenReady());
            return;
        }

        FB.Init(OnFbInitComplete, OnHideUnity);
    }

    void OnFbInitComplete()
    {
        if (!FB.IsInitialized)
        {
            Debug.LogWarning("[Meta] Facebook SDK init failed. FacebookSettings App ID kontrol et.");
            return;
        }

        ApplyMobileSettings();
        FB.ActivateApp();
        Debug.Log("[Meta] Facebook SDK initialized — App Events active.");
        StartCoroutine(RequestAttWhenReady());
    }

    void ApplyMobileSettings()
    {
#if !UNITY_WEBGL
        try
        {
            FB.Mobile.SetAutoLogAppEventsEnabled(true);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Meta] SetAutoLogAppEventsEnabled: {ex.Message}");
        }

        try
        {
            FB.Mobile.SetAdvertiserIDCollectionEnabled(true);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Meta] SetAdvertiserIDCollectionEnabled: {ex.Message}");
        }
#endif
    }

    IEnumerator RequestAttWhenReady()
    {
#if UNITY_IOS && !UNITY_EDITOR
        yield return new WaitForSecondsRealtime(AttPromptDelaySeconds);

        int status = PB_GetTrackingAuthorizationStatus();
        // 0 = notDetermined → diyalog göster
        if (status != 0)
        {
            ApplyAttResult(status);
            Debug.Log($"[Meta] ATT zaten yanıtlanmış (status={status}). Diyalog tekrar gösterilmez.");
            yield break;
        }

        PB_RequestTrackingAuthorization(gameObject.name);
#else
        yield break;
#endif
    }

    /// <summary>Native UnitySendMessage callback.</summary>
    public void OnAttAuthorizationResult(string statusParam)
    {
        if (!int.TryParse(statusParam, out int status))
        {
            return;
        }

        ApplyAttResult(status);
        Debug.Log($"[Meta] ATT sonucu: {status} (3=authorized, 2=denied, 1=restricted, 0=notDetermined)");
    }

    void ApplyAttResult(int status)
    {
        bool authorized = status == 3;
#if !UNITY_WEBGL && !UNITY_EDITOR
        if (!FB.IsInitialized)
        {
            return;
        }

        try
        {
            // iOS 14–16 için gerekli; iOS 17+ SDK ATT status'una bakar, yine de zararsız.
            FB.Mobile.SetAdvertiserTrackingEnabled(authorized);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Meta] SetAdvertiserTrackingEnabled: {ex.Message}");
        }

        try
        {
            FB.Mobile.SetAdvertiserIDCollectionEnabled(authorized);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Meta] SetAdvertiserIDCollectionEnabled: {ex.Message}");
        }
#endif
    }

    void OnHideUnity(bool isUnityShown)
    {
        Time.timeScale = isUnityShown ? 1f : 0f;
    }

    void OnGameAnalyticsEvent(string eventName, Dictionary<string, string> parameters)
    {
        if (!FB.IsInitialized || string.IsNullOrEmpty(eventName))
        {
            return;
        }

        Dictionary<string, object> fbParams = null;
        if (parameters != null && parameters.Count > 0)
        {
            fbParams = new Dictionary<string, object>(parameters.Count);
            foreach (KeyValuePair<string, string> pair in parameters)
            {
                fbParams[pair.Key] = pair.Value ?? string.Empty;
            }
        }

        try
        {
            FB.LogAppEvent(eventName, parameters: fbParams);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Meta] LogAppEvent '{eventName}' failed: {ex.Message}");
        }
    }
}

using System.Collections;
using UnityEngine;

/// <summary>
/// iOS 14+ App Tracking Transparency prompt (IDFA). Required for ads/analytics App Store review.
/// </summary>
public class AppTrackingTransparencyService : MonoBehaviour
{
    const float PromptDelaySeconds = 1.25f;
    const string RequestedOnceKey = "att_prompt_requested_v1";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureService()
    {
#if UNITY_IOS && !UNITY_EDITOR
        if (FindAnyObjectByType<AppTrackingTransparencyService>() != null)
        {
            return;
        }

        var serviceObject = new GameObject("AppTrackingTransparencyService");
        serviceObject.AddComponent<AppTrackingTransparencyService>();
        DontDestroyOnLoad(serviceObject);
#endif
    }

    IEnumerator Start()
    {
        yield return null;
        yield return new WaitForSecondsRealtime(PromptDelaySeconds);
        RequestTrackingAuthorization();
    }

    static void RequestTrackingAuthorization()
    {
#if UNITY_IOS && !UNITY_EDITOR
        IosAppTrackingTransparency.AuthorizationStatus status = IosAppTrackingTransparency.GetStatus();
        Debug.Log($"[ATT] Startup status={status}");

        if (status != IosAppTrackingTransparency.AuthorizationStatus.NotDetermined)
        {
            LogWhyPromptSkipped(status);
            OnTrackingFlowCompleted();
            return;
        }

        IosAppTrackingTransparency.RequestAuthorization(OnAuthorizationFinished);
#else
        OnTrackingFlowCompleted();
#endif
    }

    static void OnAuthorizationFinished(IosAppTrackingTransparency.AuthorizationStatus status)
    {
        PlayerPrefs.SetInt(RequestedOnceKey, 1);
        PlayerPrefs.Save();

        Debug.Log($"[ATT] User choice: {status}");
        OnTrackingFlowCompleted();
    }

    static void LogWhyPromptSkipped(IosAppTrackingTransparency.AuthorizationStatus status)
    {
        switch (status)
        {
            case IosAppTrackingTransparency.AuthorizationStatus.Authorized:
                Debug.Log("[ATT] Prompt skipped — already authorized.");
                break;
            case IosAppTrackingTransparency.AuthorizationStatus.Denied:
                Debug.Log("[ATT] Prompt skipped — previously denied. Reset: Settings > Privacy > Tracking, or reinstall app.");
                break;
            case IosAppTrackingTransparency.AuthorizationStatus.Restricted:
                Debug.Log("[ATT] Prompt skipped — restricted (Screen Time / MDM).");
                break;
        }
    }

    static void OnTrackingFlowCompleted()
    {
        ByteBrewAnalyticsBootstrap.InitializeAfterTrackingPrompt();
        MetaAppEventsService.RequestInitialize();
    }
}

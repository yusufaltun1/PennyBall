using System;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;

/// <summary>
/// iOS native ATTrackingManager wrapper.
/// </summary>
public static class IosAppTrackingTransparency
{
    public enum AuthorizationStatus
    {
        NotDetermined = 0,
        Restricted = 1,
        Denied = 2,
        Authorized = 3
    }

    delegate void NativeCallbackDelegate(int status);

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    static extern int PB_GetAppTrackingAuthorizationStatus();

    [DllImport("__Internal")]
    static extern void PB_RequestAppTrackingAuthorization(NativeCallbackDelegate callback);
#endif

    static Action<AuthorizationStatus> _pendingCompletion;
    static NativeCallbackDelegate _nativeCallback;

    public static AuthorizationStatus GetStatus()
    {
#if UNITY_IOS && !UNITY_EDITOR
        return (AuthorizationStatus)PB_GetAppTrackingAuthorizationStatus();
#else
        return AuthorizationStatus.Authorized;
#endif
    }

    public static void RequestAuthorization(Action<AuthorizationStatus> onComplete)
    {
#if UNITY_IOS && !UNITY_EDITOR
        AuthorizationStatus current = GetStatus();
        Debug.Log($"[ATT] Current status before request: {current}");

        if (current != AuthorizationStatus.NotDetermined)
        {
            onComplete?.Invoke(current);
            return;
        }

        _pendingCompletion = onComplete;
        _nativeCallback ??= OnNativeCallback;
        PB_RequestAppTrackingAuthorization(_nativeCallback);
#else
        onComplete?.Invoke(AuthorizationStatus.Authorized);
#endif
    }

    [MonoPInvokeCallback(typeof(NativeCallbackDelegate))]
    static void OnNativeCallback(int status)
    {
        var resolved = (AuthorizationStatus)status;
        Debug.Log($"[ATT] Authorization result: {resolved}");

        Action<AuthorizationStatus> completion = _pendingCompletion;
        _pendingCompletion = null;
        completion?.Invoke(resolved);
    }
}

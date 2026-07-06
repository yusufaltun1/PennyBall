using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Meta App Events — Facebook Login yok. SDK reflection ile yüklenir (import sonrası otomatik çalışır).
/// </summary>
public static class MetaAppEventsService
{
    const string FbTypeName = "Facebook.Unity.FB, Facebook.Unity";
    const string InitDelegateTypeName = "Facebook.Unity.InitDelegate, Facebook.Unity";

    static Type _fbType;
    static MethodInfo _initMethod;
    static MethodInfo _activateAppMethod;
    static MethodInfo _logAppEventMethod;
    static PropertyInfo _isInitializedProperty;
    static bool _initRequested;
    static bool _sdkAvailable;
    static bool _sdkMissingLogged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInit()
    {
#if UNITY_IOS && !UNITY_EDITOR
        // iOS: ATT prompt sonrası AppTrackingTransparencyService init eder.
        return;
#endif

        if (!MetaEventsConfig.IsConfigured)
        {
            return;
        }

        EnsureBootstrap();
        RequestInitialize();
    }

    static void EnsureBootstrap()
    {
        if (MetaAppEventsBootstrap.Instance != null)
        {
            return;
        }

        var bootstrapObject = new GameObject("MetaAppEventsBootstrap");
        bootstrapObject.AddComponent<MetaAppEventsBootstrap>();
        UnityEngine.Object.DontDestroyOnLoad(bootstrapObject);
    }

    public static void RequestInitialize()
    {
        if (_initRequested || !MetaEventsConfig.IsConfigured || !TryResolveSdk())
        {
            return;
        }

        if (IsSdkInitialized())
        {
            ActivateApp();
            return;
        }

        Type initDelegateType = Type.GetType(InitDelegateTypeName);
        if (initDelegateType == null || _initMethod == null)
        {
            return;
        }

        _initRequested = true;

        try
        {
            Delegate callback = Delegate.CreateDelegate(
                initDelegateType,
                typeof(MetaAppEventsService),
                nameof(OnInitCompleteInternal));
            _initMethod.Invoke(null, new object[] { callback, null, MetaEventsConfig.AppId });
        }
        catch (Exception ex)
        {
            _initRequested = false;
            Debug.LogWarning($"[Meta] FB.Init başarısız: {ex.Message}");
        }
    }

    static void OnInitCompleteInternal()
    {
        ActivateApp();
    }

    public static void ActivateApp()
    {
        if (!TryResolveSdk() || !IsSdkInitialized())
        {
            return;
        }

        try
        {
            _activateAppMethod?.Invoke(null, null);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Meta] ActivateApp başarısız: {ex.Message}");
        }
    }

    public static void TrackEvent(string eventName, Dictionary<string, string> parameters = null)
    {
        if (!MetaEventsConfig.IsConfigured || string.IsNullOrEmpty(eventName))
        {
            return;
        }

        if (!TryResolveSdk())
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Meta] (SDK yok) {eventName}");
#endif
            return;
        }

        if (!IsSdkInitialized())
        {
            RequestInitialize();
        }

        if (!IsSdkInitialized())
        {
            return;
        }

        Dictionary<string, object> metaParameters = null;
        if (parameters != null && parameters.Count > 0)
        {
            metaParameters = new Dictionary<string, object>(parameters.Count);
            foreach (KeyValuePair<string, string> pair in parameters)
            {
                metaParameters[pair.Key] = pair.Value;
            }
        }

        try
        {
            _logAppEventMethod?.Invoke(null, new object[] { eventName, null, metaParameters });
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Meta] Event '{eventName}' gönderilemedi: {ex.Message}");
        }
    }

    public static void TrackLevelAchieved(int level)
    {
        TrackEvent("fb_mobile_level_achieved", new Dictionary<string, string>
        {
            { "level", level.ToString() }
        });
    }

    public static void TrackCompleteRegistration()
    {
        TrackEvent("fb_mobile_complete_registration");
    }

    static bool TryResolveSdk()
    {
        if (_fbType != null)
        {
            return _sdkAvailable;
        }

        _fbType = Type.GetType(FbTypeName);
        if (_fbType == null)
        {
            if (!_sdkMissingLogged)
            {
                _sdkMissingLogged = true;
                Debug.Log(
                    "[Meta] Facebook SDK bulunamadı. " +
                    "Meta SDK for Unity import et → PennyBall → Meta → Apply App Events Config");
            }

            _sdkAvailable = false;
            return false;
        }

        _isInitializedProperty = _fbType.GetProperty(
            "IsInitialized",
            BindingFlags.Public | BindingFlags.Static);

        _activateAppMethod = _fbType.GetMethod(
            "ActivateApp",
            BindingFlags.Public | BindingFlags.Static,
            null,
            Type.EmptyTypes,
            null);

        _initMethod = FindMethod(_fbType, "Init", 3);
        _logAppEventMethod = FindMethod(_fbType, "LogAppEvent", 3);

        _sdkAvailable = _activateAppMethod != null && _logAppEventMethod != null;
        return _sdkAvailable;
    }

    static bool IsSdkInitialized()
    {
        if (_isInitializedProperty == null)
        {
            return false;
        }

        try
        {
            return (bool)_isInitializedProperty.GetValue(null);
        }
        catch
        {
            return false;
        }
    }

    static MethodInfo FindMethod(Type type, string name, int parameterCount)
    {
        MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);
        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo method = methods[i];
            if (method.Name == name && method.GetParameters().Length == parameterCount)
            {
                return method;
            }
        }

        return null;
    }
}

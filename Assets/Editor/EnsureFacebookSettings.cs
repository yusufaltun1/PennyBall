using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Penny Ball Meta App ID ile FacebookSettings asset'ini oluşturur / günceller.
/// Menü: PennyBall → Setup Meta (Facebook) App Events
/// </summary>
public static class EnsureFacebookSettings
{
    public const string PennyBallAppId = "1525809872892937";
    public const string PennyBallAppLabel = "Penny Ball";

    /// <summary>
    /// Developer → Settings → Advanced → Client Token (boş bırakılabilir; SDK 18+ için doldurman önerilir).
    /// </summary>
    public const string PennyBallClientToken = "d2ab6af73cad0dbbc4c3f41c2d7bebc5";

    const string SettingsAssetPath = "Assets/FacebookSDK/SDK/Resources/FacebookSettings.asset";

    [InitializeOnLoadMethod]
    static void AutoEnsureOnLoad()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            Ensure(silent: true);
        };
    }

    [MenuItem("PennyBall/Setup Meta (Facebook) App Events")]
    public static void SetupFromMenu()
    {
        if (Ensure(silent: false))
        {
            EditorUtility.DisplayDialog(
                "Meta App Events",
                "FacebookSettings hazır.\n\n" +
                $"App ID: {PennyBallAppId}\n\n" +
                "1) App Mode = Live olmalı\n" +
                "2) Client Token boşsa Developer → Settings → Advanced'den ekle\n" +
                "3) Yeni build alıp Events Manager'da Test Events kontrol et\n" +
                "4) Ads Manager'ı yenile",
                "OK");
        }
    }

    public static bool Ensure(bool silent)
    {
        Type settingsType = FindFacebookSettingsType();
        if (settingsType == null)
        {
            if (!silent)
            {
                Debug.LogError("[Meta] Facebook.Unity.Settings bulunamadı. FacebookSDK import edildi mi?");
            }

            return false;
        }

        EnsureFolder("Assets/FacebookSDK");
        EnsureFolder("Assets/FacebookSDK/SDK");
        EnsureFolder("Assets/FacebookSDK/SDK/Resources");

        UnityEngine.Object settings = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(SettingsAssetPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance(settingsType);
            AssetDatabase.CreateAsset(settings, SettingsAssetPath);
        }

        SetStringList(settingsType, "AppIds", settings, new List<string> { PennyBallAppId });
        SetStringList(settingsType, "AppLabels", settings, new List<string> { PennyBallAppLabel });
        SetStringList(settingsType, "ClientTokens", settings, new List<string> { PennyBallClientToken ?? string.Empty });

        SetInt(settingsType, "SelectedAppIndex", settings, 0);
        SetBool(settingsType, "AutoLogAppEventsEnabled", settings, true);
        SetBool(settingsType, "AdvertiserIDCollectionEnabled", settings, true);

        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (!silent)
        {
            Debug.Log($"[Meta] FacebookSettings güncellendi → {SettingsAssetPath} (App ID {PennyBallAppId})");
            Selection.activeObject = settings;
        }

        return true;
    }

    static Type FindFacebookSettingsType()
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType("Facebook.Unity.Settings.FacebookSettings")
                        ?? assembly.GetType("Facebook.Unity.FacebookSettings");
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = System.IO.Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, name);
    }

    static void SetStringList(Type type, string propertyName, object target, List<string> value)
    {
        PropertyInfo prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(prop.GetGetMethod().IsStatic ? null : target, value);
            return;
        }

        FieldInfo field = type.GetField(ToFieldName(propertyName), BindingFlags.NonPublic | BindingFlags.Instance)
                          ?? type.GetField(propertyName, BindingFlags.Public | BindingFlags.Instance);
        field?.SetValue(target, value);
    }

    static void SetInt(Type type, string propertyName, object target, int value)
    {
        PropertyInfo prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(prop.GetGetMethod().IsStatic ? null : target, value);
            return;
        }

        FieldInfo field = type.GetField(ToFieldName(propertyName), BindingFlags.NonPublic | BindingFlags.Instance);
        field?.SetValue(target, value);
    }

    static void SetBool(Type type, string propertyName, object target, bool value)
    {
        PropertyInfo prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(prop.GetGetMethod().IsStatic ? null : target, value);
            return;
        }

        FieldInfo field = type.GetField(ToFieldName(propertyName), BindingFlags.NonPublic | BindingFlags.Instance);
        field?.SetValue(target, value);
    }

    static string ToFieldName(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            return propertyName;
        }

        return char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1);
    }
}

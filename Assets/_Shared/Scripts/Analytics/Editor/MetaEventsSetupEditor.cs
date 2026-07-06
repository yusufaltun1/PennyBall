#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Meta SDK import edildikten sonra App ID + Client Token'ı FacebookSettings'e yazar.
/// </summary>
public static class MetaEventsSetupEditor
{
    const string FacebookSettingsAssetName = "FacebookSettings";

    [MenuItem("PennyBall/Meta/Apply App Events Config")]
    public static void ApplyFromMenu()
    {
        if (ApplyFacebookSettings())
        {
            Debug.Log("[Meta] FacebookSettings güncellendi.");
        }
        else
        {
            Debug.LogWarning(
                "[Meta] Facebook SDK bulunamadı. Önce Meta SDK for Unity import et, sonra tekrar dene.");
        }
    }

    [InitializeOnLoadMethod]
    static void AutoApplyOnLoad()
    {
        EditorApplication.delayCall += TryAutoApply;
    }

    static void TryAutoApply()
    {
        if (!MetaEventsConfig.IsConfigured)
        {
            return;
        }

        ApplyFacebookSettings(silent: true);
    }

    static bool ApplyFacebookSettings(bool silent = false)
    {
        string[] guids = AssetDatabase.FindAssets($"{FacebookSettingsAssetName} t:ScriptableObject");
        if (guids.Length == 0)
        {
            return false;
        }

        string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
        ScriptableObject settingsAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
        if (settingsAsset == null)
        {
            return false;
        }

        SerializedObject serialized = new SerializedObject(settingsAsset);
        SerializedProperty appIds = serialized.FindProperty("appIds");
        SerializedProperty clientTokens = serialized.FindProperty("clientTokens");
        SerializedProperty appLabels = serialized.FindProperty("appLabels");

        if (appIds == null || clientTokens == null)
        {
            if (!silent)
            {
                Debug.LogWarning("[Meta] FacebookSettings alanları bulunamadı.");
            }

            return false;
        }

        SetStringArrayFirst(appIds, MetaEventsConfig.AppId);
        SetStringArrayFirst(clientTokens, MetaEventsConfig.ClientToken);

        if (appLabels != null)
        {
            SetStringArrayFirst(appLabels, MetaEventsConfig.DisplayName);
        }

        SerializedProperty autoLog = serialized.FindProperty("autoLogAppEventsEnabled");
        if (autoLog != null)
        {
            autoLog.boolValue = true;
        }

        SerializedProperty advertiserId = serialized.FindProperty("advertiserIDCollectionEnabled");
        if (advertiserId != null)
        {
            advertiserId.boolValue = true;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settingsAsset);
        AssetDatabase.SaveAssets();

        TrySelectAppIdViaReflection(MetaEventsConfig.AppId);

        return true;
    }

    static void SetStringArrayFirst(SerializedProperty arrayProperty, string value)
    {
        if (arrayProperty.arraySize == 0)
        {
            arrayProperty.InsertArrayElementAtIndex(0);
        }

        arrayProperty.GetArrayElementAtIndex(0).stringValue = value;
    }

    static void TrySelectAppIdViaReflection(string appId)
    {
        Type settingsType = Type.GetType("Facebook.Unity.Settings.FacebookSettings, Facebook.Unity.Settings")
            ?? Type.GetType("Facebook.Unity.Settings.FacebookSettings, Facebook.Unity");

        if (settingsType == null)
        {
            return;
        }

        MethodInfo selectMethod = settingsType.GetMethod(
            "SelectApp",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(string) },
            null);

        selectMethod?.Invoke(null, new object[] { appId });
    }
}
#endif

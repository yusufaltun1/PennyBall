#if UNITY_EDITOR
using Fusion;
using Fusion.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// MatchShotRelay NetworkObject prefab'ını oluşturur + Fusion prefab table rebuild.
/// </summary>
public static class MatchShotRelayPrefabSetup
{
    public const string PrefabPath = "Assets/_Shared/Resources/MatchShotRelay.prefab";

    [InitializeOnLoadMethod]
    static void AutoEnsure()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying)
            {
                return;
            }

            try
            {
                EnsurePrefabExists(log: false);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ShotRelay] AutoEnsure atlandı: {ex.Message}");
            }
        };
    }

    [MenuItem("PennyBall/Online/Create MatchShotRelay Prefab")]
    public static void CreateFromMenu()
    {
        EnsurePrefabExists(log: true);
    }

    public static NetworkObject EnsurePrefabExists(bool log)
    {
        NetworkObject existing = LoadPrefab();
        if (existing != null)
        {
            try
            {
                NetworkProjectConfigUtilities.RebuildPrefabTable();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ShotRelay] PrefabTable rebuild: {ex.Message}");
            }

            if (log)
            {
                Debug.Log($"[ShotRelay] Prefab hazır + table rebuild: {PrefabPath}");
            }

            return existing;
        }

        EnsureFolders();

        var go = new GameObject("MatchShotRelay");
        try
        {
            go.AddComponent<NetworkObject>();
            go.AddComponent<MatchShotNetworkRelay>();

            bool saved = false;
            GameObject prefabRoot = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath, out saved);
            if (!saved || prefabRoot == null)
            {
                Debug.LogError($"[ShotRelay] Prefab kaydedilemedi: {PrefabPath}");
                return null;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            try
            {
                NetworkProjectConfigUtilities.RebuildPrefabTable();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ShotRelay] PrefabTable rebuild: {ex.Message}");
            }

            NetworkObject prefab = prefabRoot.GetComponent<NetworkObject>();
            if (prefab == null)
            {
                prefab = LoadPrefab();
            }

            if (log || prefab != null)
            {
                Debug.Log($"[ShotRelay] Prefab oluşturuldu: {PrefabPath}");
            }

            return prefab;
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    static NetworkObject LoadPrefab()
    {
        GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        return go != null ? go.GetComponent<NetworkObject>() : null;
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Shared"))
        {
            AssetDatabase.CreateFolder("Assets", "_Shared");
        }

        if (!AssetDatabase.IsValidFolder("Assets/_Shared/Resources"))
        {
            AssetDatabase.CreateFolder("Assets/_Shared", "Resources");
        }
    }
}
#endif

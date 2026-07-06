using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Onboarding sahnesinde rakip coinler ve oyuncu kalesi kaldırılır.
/// Yan coinler başlangıçta gizlenir; coin spawn pozisyonları ve rotasyonları kaydedilir.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-1000)]
public class OnboardingSceneBootstrap : MonoBehaviour
{
    public static Vector3 CenterCoinSpawnPosition { get; private set; }
    public static Quaternion CenterCoinSpawnRotation { get; private set; } = Quaternion.identity;
    public static Vector3 SideCoinLeftSpawnPosition { get; private set; }
    public static Quaternion SideCoinLeftSpawnRotation { get; private set; } = Quaternion.identity;
    public static Vector3 SideCoinRightSpawnPosition { get; private set; }
    public static Quaternion SideCoinRightSpawnRotation { get; private set; } = Quaternion.identity;

    static bool _prepared;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetPreparedFlag()
    {
        _prepared = false;
        CenterCoinSpawnPosition = default;
        CenterCoinSpawnRotation = Quaternion.identity;
        SideCoinLeftSpawnPosition = default;
        SideCoinLeftSpawnRotation = Quaternion.identity;
        SideCoinRightSpawnPosition = default;
        SideCoinRightSpawnRotation = Quaternion.identity;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureBootstrap()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || activeScene.name != OnboardingSceneNames.Onboarding)
        {
            return;
        }

        ApplyInitialSceneSetup(activeScene);

        if (FindFirstObjectByType<OnboardingSceneBootstrap>() != null)
        {
            return;
        }

        var bootstrapObject = new GameObject("OnboardingBootstrap");
        bootstrapObject.AddComponent<OnboardingSceneBootstrap>();
    }

    public static void ApplyInitialSceneSetup()
    {
        ApplyInitialSceneSetup(SceneManager.GetActiveScene());
    }

    public static void ApplyInitialSceneSetup(Scene scene)
    {
        if (_prepared || !scene.IsValid() || scene.name != OnboardingSceneNames.Onboarding)
        {
            return;
        }

        _prepared = true;
        CacheCoinSpawnPoses(scene);

        DestroyIfExists(scene, "Coin_E1");
        DestroyIfExists(scene, "Coin_E2");
        DestroyIfExists(scene, "Coin_E3");
        DestroyIfExists(scene, "Kale_P");
        DestroyIfExists(scene, "OpponentBot");

        DeactivateIfExists(scene, "Coin_P1");
        DeactivateIfExists(scene, "Coin_P3");
        DeactivateIfExists(scene, "InvalidMove");
    }

    void Awake()
    {
        if (SceneManager.GetActiveScene().name != OnboardingSceneNames.Onboarding)
        {
            Destroy(gameObject);
        }
    }

    static void CacheCoinSpawnPoses(Scene scene)
    {
        (CenterCoinSpawnPosition, CenterCoinSpawnRotation) = ReadCoinPose(scene, "Coin_P2");
        (SideCoinLeftSpawnPosition, SideCoinLeftSpawnRotation) = ReadCoinPose(scene, "Coin_P1");
        (SideCoinRightSpawnPosition, SideCoinRightSpawnRotation) = ReadCoinPose(scene, "Coin_P3");
    }

    static (Vector3 position, Quaternion rotation) ReadCoinPose(Scene scene, string objectName)
    {
        GameObject coinObject = FindInSceneByName(scene, objectName);
        if (coinObject == null)
        {
            return (default, Quaternion.identity);
        }

        Transform coinTransform = coinObject.transform;
        return (coinTransform.position, coinTransform.rotation);
    }

    public static GameObject FindSceneObject(string objectName)
    {
        return FindInSceneByName(SceneManager.GetActiveScene(), objectName);
    }

    static GameObject FindInSceneByName(Scene scene, string objectName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindDeepChild(roots[i].transform, objectName);
            if (found != null)
            {
                return found.gameObject;
            }
        }

        return null;
    }

    static Transform FindDeepChild(Transform parent, string objectName)
    {
        if (parent.name == objectName)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeepChild(parent.GetChild(i), objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    static void DestroyIfExists(Scene scene, string objectName)
    {
        GameObject target = FindInSceneByName(scene, objectName);
        if (target != null)
        {
            Object.Destroy(target);
        }
    }

    static void DeactivateIfExists(Scene scene, string objectName)
    {
        GameObject target = FindInSceneByName(scene, objectName);
        if (target != null)
        {
            target.SetActive(false);
        }
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Onboarding sahnesinde rakip coinler ve oyuncu kalesi kaldırılır.
/// Yan coinler başlangıçta gizlenir; coin spawn pozisyonları ve rotasyonları kaydedilir.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-200)]
public class OnboardingSceneBootstrap : MonoBehaviour
{
    public static Vector3 CenterCoinSpawnPosition { get; private set; }
    public static Quaternion CenterCoinSpawnRotation { get; private set; } = Quaternion.identity;
    public static Vector3 SideCoinLeftSpawnPosition { get; private set; }
    public static Quaternion SideCoinLeftSpawnRotation { get; private set; } = Quaternion.identity;
    public static Vector3 SideCoinRightSpawnPosition { get; private set; }
    public static Quaternion SideCoinRightSpawnRotation { get; private set; } = Quaternion.identity;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureBootstrap()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || activeScene.name != OnboardingSceneNames.Onboarding)
        {
            return;
        }

        if (FindFirstObjectByType<OnboardingSceneBootstrap>() != null)
        {
            return;
        }

        var bootstrapObject = new GameObject("OnboardingBootstrap");
        bootstrapObject.AddComponent<OnboardingSceneBootstrap>();
    }

    void Awake()
    {
        if (SceneManager.GetActiveScene().name != OnboardingSceneNames.Onboarding)
        {
            Destroy(gameObject);
            return;
        }

        DestroyIfExists("Coin_E1");
        DestroyIfExists("Coin_E2");
        DestroyIfExists("Coin_E3");
        DestroyIfExists("Kale_P");
        DestroyIfExists("OpponentBot");

        CacheCoinSpawnPoses();
        DeactivateIfExists("Coin_P1");
        DeactivateIfExists("Coin_P3");
        DeactivateIfExists("InvalidMove");
    }

    void CacheCoinSpawnPoses()
    {
        (CenterCoinSpawnPosition, CenterCoinSpawnRotation) = ReadCoinPose("Coin_P2");
        (SideCoinLeftSpawnPosition, SideCoinLeftSpawnRotation) = ReadCoinPose("Coin_P1");
        (SideCoinRightSpawnPosition, SideCoinRightSpawnRotation) = ReadCoinPose("Coin_P3");
    }

    static (Vector3 position, Quaternion rotation) ReadCoinPose(string objectName)
    {
        GameObject coinObject = GameObject.Find(objectName);
        if (coinObject == null)
        {
            return (default, Quaternion.identity);
        }

        Transform coinTransform = coinObject.transform;
        return (coinTransform.position, coinTransform.rotation);
    }

    static void DestroyIfExists(string objectName)
    {
        GameObject target = GameObject.Find(objectName);
        if (target != null)
        {
            Destroy(target);
        }
    }

    static void DeactivateIfExists(string objectName)
    {
        GameObject target = GameObject.Find(objectName);
        if (target != null)
        {
            target.SetActive(false);
        }
    }
}

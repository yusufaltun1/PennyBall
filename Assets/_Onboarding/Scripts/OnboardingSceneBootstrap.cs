using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Onboarding sahnesinde rakip coinler ve oyuncu kalesi kaldırılır.
/// Merkez coin (Coin_P2) spawn pozisyonu kaydedilir.
/// Yan coinler (Coin_P1 / Coin_P3) Aşama 8'e kadar pasif tutulur.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-1000)]
public class OnboardingSceneBootstrap : MonoBehaviour
{
    public static Vector3 CenterCoinSpawnPosition { get; private set; }
    public static Quaternion CenterCoinSpawnRotation { get; private set; } = Quaternion.identity;
    public static Transform CenterCoinTransform { get; private set; }

    public static Transform LeftCoinTransform { get; private set; }
    public static Transform RightCoinTransform { get; private set; }
    public static Vector3 LeftCoinGameUiSpawnPosition { get; private set; } = new(1.406f, 0.136f, 1.784f);
    public static Quaternion LeftCoinGameUiSpawnRotation { get; private set; } =
        new(-0.7071068f, 0f, 0f, 0.7071068f);
    public static Vector3 RightCoinGameUiSpawnPosition { get; private set; } = new(1.626f, 0.136f, 1.784f);
    public static Quaternion RightCoinGameUiSpawnRotation { get; private set; } =
        new(-0.7071068f, 0f, 0f, 0.7071068f);

    public static void CacheSideCoinReferences(Transform leftCoin, Transform rightCoin)
    {
        if (leftCoin != null)
        {
            LeftCoinTransform = leftCoin;
            LeftCoinGameUiSpawnRotation = leftCoin.rotation;
        }

        if (rightCoin != null)
        {
            RightCoinTransform = rightCoin;
            RightCoinGameUiSpawnRotation = rightCoin.rotation;
        }
    }

    static bool _sceneSetupApplied;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSceneSetupFlag()
    {
        _sceneSetupApplied = false;
        CenterCoinTransform = null;
        LeftCoinTransform = null;
        RightCoinTransform = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureBootstrap()
    {
        if (!IsOnboardingScene(SceneManager.GetActiveScene().name))
        {
            return;
        }

        EnsureSceneSetup();

        if (FindFirstObjectByType<OnboardingSceneBootstrap>() != null)
        {
            return;
        }

        var bootstrapObject = new GameObject("OnboardingBootstrap");
        bootstrapObject.AddComponent<OnboardingSceneBootstrap>();
    }

    void Awake()
    {
        if (!IsOnboardingScene(SceneManager.GetActiveScene().name))
        {
            Destroy(gameObject);
            return;
        }

        EnsureSceneSetup();
    }

    public static void EnsureSceneSetup()
    {
        if (_sceneSetupApplied || !IsOnboardingScene(SceneManager.GetActiveScene().name))
        {
            return;
        }

        _sceneSetupApplied = true;

        CacheCoinSpawnPoses();

        DestroyIfExists("Coin_E1");
        DestroyIfExists("Coin_E2");
        DestroyIfExists("Coin_E3");
        DestroyIfExists("Kale_P");
        DestroyIfExists("OpponentBot");

        // Yan coinler Aşama 8'e kadar kapalı; GameUI açılış pozisyonları saklanır.
        if (LeftCoinTransform != null)
        {
            LeftCoinTransform.gameObject.SetActive(false);
        }

        if (RightCoinTransform != null)
        {
            RightCoinTransform.gameObject.SetActive(false);
        }

        DeactivateIfExists("InvalidMove");
    }

    static bool IsOnboardingScene(string sceneName) =>
        sceneName == OnboardingSceneNames.Onboarding;

    static void CacheCoinSpawnPoses()
    {
        CenterCoinTransform = FindCoinTransform("Coin_P2");
        (CenterCoinSpawnPosition, CenterCoinSpawnRotation) = ReadCoinPose(CenterCoinTransform);

        LeftCoinTransform = FindCoinTransform("Coin_P1");
        RightCoinTransform = FindCoinTransform("Coin_P3");

        if (LeftCoinTransform != null)
        {
            LeftCoinGameUiSpawnRotation = LeftCoinTransform.rotation;
        }

        if (RightCoinTransform != null)
        {
            RightCoinGameUiSpawnRotation = RightCoinTransform.rotation;
        }
    }

    /// <summary>
    /// Coin_P1 / Coin_P3 sahnede kapalı başlayabilir; inactive dahil aranır.
    /// </summary>
    static Transform FindCoinTransform(string objectName)
    {
        GameObject active = GameObject.Find(objectName);
        if (active != null)
        {
            return active.transform;
        }

        Transform[] transforms = Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null || candidate.name != objectName)
            {
                continue;
            }

            if (!candidate.gameObject.scene.IsValid())
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    static (Vector3 position, Quaternion rotation) ReadCoinPose(Transform coinTransform)
    {
        if (coinTransform == null)
        {
            return (default, Quaternion.identity);
        }

        return (coinTransform.position, coinTransform.rotation);
    }

    static void DestroyIfExists(string objectName)
    {
        Transform target = FindCoinTransform(objectName);
        if (target != null)
        {
            Object.Destroy(target.gameObject);
        }
    }

    static void DeactivateIfExists(string objectName)
    {
        Transform target = FindCoinTransform(objectName);
        if (target != null)
        {
            target.gameObject.SetActive(false);
        }
    }
}

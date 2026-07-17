using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
    public static Transform CenterCoinTransform { get; private set; }
    public static Transform SideCoinLeftTransform { get; private set; }
    public static Transform SideCoinRightTransform { get; private set; }

    static bool _sceneSetupApplied;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSceneSetupFlag()
    {
        _sceneSetupApplied = false;
        CenterCoinTransform = null;
        SideCoinLeftTransform = null;
        SideCoinRightTransform = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureBootstrap()
    {
        if (!IsOnboardingScene(SceneManager.GetActiveScene().name))
        {
            return;
        }

        EnsureSceneSetup();
        WireSkipButton();

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
        WireSkipButton();
    }

    void Start()
    {
        WireSkipButton();
    }

    public static void SkipToExercise()
    {
        OnboardingProgress.MarkCompleted();
        MainMenuClickSound.Play();
        SceneManager.LoadScene(GameSceneNames.Exercise);
    }

    public static void WireSkipButton()
    {
        if (!IsOnboardingScene(SceneManager.GetActiveScene().name))
        {
            return;
        }

        GameObject skipObject = FindSkipButtonObject();
        if (skipObject == null)
        {
            return;
        }

        skipObject.transform.SetAsLastSibling();

        if (skipObject.GetComponent<OnboardingSkipButton>() == null)
        {
            skipObject.AddComponent<OnboardingSkipButton>();
        }

        Button button = skipObject.GetComponent<Button>();
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(SkipToExercise);
        button.onClick.AddListener(SkipToExercise);
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

        DeactivateIfExists("Coin_P1");
        DeactivateIfExists("Coin_P3");
        DeactivateIfExists("InvalidMove");
    }

    static bool IsOnboardingScene(string sceneName) =>
        sceneName == OnboardingSceneNames.Onboarding;

    static void CacheCoinSpawnPoses()
    {
        CenterCoinTransform = FindActiveCoinTransform("Coin_P2");
        SideCoinLeftTransform = FindActiveCoinTransform("Coin_P1");
        SideCoinRightTransform = FindActiveCoinTransform("Coin_P3");

        (CenterCoinSpawnPosition, CenterCoinSpawnRotation) = ReadCoinPose(CenterCoinTransform);
        (SideCoinLeftSpawnPosition, SideCoinLeftSpawnRotation) = ReadCoinPose(SideCoinLeftTransform);
        (SideCoinRightSpawnPosition, SideCoinRightSpawnRotation) = ReadCoinPose(SideCoinRightTransform);
    }

    static Transform FindActiveCoinTransform(string objectName)
    {
        GameObject coinObject = GameObject.Find(objectName);
        return coinObject != null ? coinObject.transform : null;
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
        GameObject target = GameObject.Find(objectName);
        if (target != null)
        {
            Object.Destroy(target);
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

    static GameObject FindSkipButtonObject()
    {
        GameObject direct = GameObject.Find("skip");
        if (direct != null)
        {
            return direct;
        }

        GameObject canvas = GameObject.Find("Canvas");
        if (canvas == null)
        {
            return null;
        }

        Transform[] children = canvas.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == "skip")
            {
                return children[i].gameObject;
            }
        }

        return null;
    }
}

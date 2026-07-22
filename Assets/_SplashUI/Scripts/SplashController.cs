using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SplashController : MonoBehaviour
{
    const string OnboardingScenePath = "Assets/_Onboarding/Scenes/Onboarding.unity";
    const string MainMenuScenePath = "Assets/_MainMenu/Scenes/MainMenu_Scene.unity";

    [SerializeField] Slider _loadingSlider;
    [SerializeField] string _nextSceneName = GameSceneNames.MainMenu;
    [SerializeField] float _minDisplaySeconds = 1.5f;
    [SerializeField] float _fillSpeed = 1.75f;

    void Awake()
    {
        if (_loadingSlider == null)
        {
            _loadingSlider = GetComponentInChildren<Slider>(true);
        }

        if (_loadingSlider != null)
        {
            _loadingSlider.interactable = false;
            _loadingSlider.value = 0f;

            if (_loadingSlider.handleRect != null)
            {
                _loadingSlider.handleRect.gameObject.SetActive(false);
            }
        }
    }

    void Start()
    {
        string nextScene = ResolveNextSceneName();
        StartCoroutine(LoadNextSceneRoutine(nextScene));
    }

    static string ResolveNextSceneName()
    {
        if (OnboardingProgress.IsCompleted)
        {
            return GameSceneNames.MainMenu;
        }

        if (TryResolveBuildScene(OnboardingSceneNames.Onboarding, OnboardingScenePath, out string onboardingScene))
        {
            return onboardingScene;
        }

        Debug.LogWarning(
            $"[Splash] '{OnboardingSceneNames.Onboarding}' build listesinde görünmedi. Main Menu'ye düşülüyor.");
        return GameSceneNames.MainMenu;
    }

    static bool TryResolveBuildScene(string sceneName, string scenePath, out string loadName)
    {
        loadName = null;

        if (string.IsNullOrEmpty(sceneName))
        {
            return false;
        }

        // 1) Build index by asset path (Unity 6 Build Profiles için daha güvenilir)
        if (!string.IsNullOrEmpty(scenePath))
        {
            int pathIndex = SceneUtility.GetBuildIndexByScenePath(scenePath);
            if (pathIndex >= 0)
            {
                loadName = sceneName;
                return true;
            }
        }

        // 2) Classic streamed-level check
        if (Application.CanStreamedLevelBeLoaded(sceneName))
        {
            loadName = sceneName;
            return true;
        }

        // 3) Scan all build scenes by file name
        int sceneCount = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < sceneCount; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (name == sceneName)
            {
                loadName = sceneName;
                return true;
            }
        }

#if UNITY_EDITOR
        // 4) Editor: diskteki EditorBuildSettings (Play Mode'da runtime API gecikebiliyor)
        foreach (UnityEditor.EditorBuildSettingsScene scene in UnityEditor.EditorBuildSettings.scenes)
        {
            if (!scene.enabled || string.IsNullOrEmpty(scene.path))
            {
                continue;
            }

            if (System.IO.Path.GetFileNameWithoutExtension(scene.path) == sceneName
                || scene.path == scenePath)
            {
                loadName = sceneName;
                return true;
            }
        }
#endif

        return false;
    }

    IEnumerator LoadNextSceneRoutine(string sceneName)
    {
        SetProgress(0f);

        string scenePath = sceneName == OnboardingSceneNames.Onboarding
            ? OnboardingScenePath
            : sceneName == GameSceneNames.MainMenu
                ? MainMenuScenePath
                : null;

        if (!TryResolveBuildScene(sceneName, scenePath, out string resolvedName))
        {
            Debug.LogError($"[Splash] Scene yüklenemedi: '{sceneName}'. Main Menu deneniyor.");
            sceneName = GameSceneNames.MainMenu;
        }
        else
        {
            sceneName = resolvedName;
        }

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName);
#if UNITY_EDITOR
        if (loadOperation == null)
        {
            string fallbackPath = sceneName == OnboardingSceneNames.Onboarding
                ? OnboardingScenePath
                : sceneName == GameSceneNames.MainMenu
                    ? MainMenuScenePath
                    : null;

            if (!string.IsNullOrEmpty(fallbackPath))
            {
                Debug.LogWarning(
                    $"[Splash] Runtime build listesi güncel değil; Editor Play Mode ile yükleniyor: {fallbackPath}");
                loadOperation = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                    fallbackPath,
                    new LoadSceneParameters(LoadSceneMode.Single));
            }
        }
#endif
        if (loadOperation == null)
        {
            Debug.LogError($"[Splash] LoadSceneAsync null döndü: '{sceneName}'");
            yield break;
        }

        loadOperation.allowSceneActivation = false;

        float elapsed = 0f;
        float displayedProgress = 0f;

        while (!loadOperation.isDone)
        {
            elapsed += Time.unscaledDeltaTime;

            float loadProgress = Mathf.Clamp01(loadOperation.progress / 0.9f);
            float timeProgress = _minDisplaySeconds > 0f
                ? Mathf.Clamp01(elapsed / _minDisplaySeconds)
                : 1f;
            float targetProgress = Mathf.Min(loadProgress, timeProgress);

            displayedProgress = Mathf.MoveTowards(
                displayedProgress,
                targetProgress,
                Time.unscaledDeltaTime * _fillSpeed);
            SetProgress(displayedProgress);

            bool loadReady = loadOperation.progress >= 0.9f;
            bool timeReady = elapsed >= _minDisplaySeconds;
            if (loadReady && timeReady && displayedProgress >= 0.99f)
            {
                SetProgress(1f);
                loadOperation.allowSceneActivation = true;
            }

            yield return null;
        }
    }

    void SetProgress(float value)
    {
        if (_loadingSlider == null)
        {
            return;
        }

        _loadingSlider.value = Mathf.Clamp01(value);
    }
}

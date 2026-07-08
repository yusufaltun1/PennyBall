using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SplashController : MonoBehaviour
{
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
        return OnboardingProgress.IsCompleted
            ? GameSceneNames.MainMenu
            : OnboardingSceneNames.Onboarding;
    }

    IEnumerator LoadNextSceneRoutine(string sceneName)
    {
        SetProgress(0f);

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName);
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

using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public class OnboardingFlowRouter : MonoBehaviour
{
    [SerializeField] bool _onlyOnMainMenuScene = true;

    void Awake()
    {
        if (_onlyOnMainMenuScene
            && SceneManager.GetActiveScene().name != OnboardingSceneNames.MainMenu)
        {
            return;
        }

        if (!OnboardingProgress.IsCompleted)
        {
            SceneManager.LoadScene(OnboardingSceneNames.Onboarding);
        }
    }
}

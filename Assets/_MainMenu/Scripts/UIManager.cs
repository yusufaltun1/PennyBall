using UnityEngine;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    public void OnPlayButtonPressed()
    {
        if (!OnboardingProgress.IsCompleted)
        {
            SceneManager.LoadScene(OnboardingSceneNames.Onboarding);
            return;
        }

        MatchLauncher.StartLeagueMatch();
    }

    public void OnAntremanButtonPressed()
    {
        Debug.Log("Antreman yapılacak!");
    }
}

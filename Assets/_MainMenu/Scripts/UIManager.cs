using UnityEngine;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{

    void Start()
    {
        // Eğer 3D Saha sahnesi halihazırda yüklü değilse, arka plana yükle
        if (!SceneManager.GetSceneByName("3d_Saha_Studio").isLoaded)
        {
            SceneManager.LoadScene("3d_Saha_Studio", LoadSceneMode.Additive);
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
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

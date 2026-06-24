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

        // Hide Matching_Panel at start of the game
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            Transform matchingPanelTrans = canvas.transform.Find("Matching_Panel");
            if (matchingPanelTrans != null)
            {
                matchingPanelTrans.gameObject.SetActive(false);
            }
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

        Canvas canvas = GetComponentInParent<Canvas>();
        Transform matchingPanelTrans = null;
        if (canvas != null)
        {
            matchingPanelTrans = canvas.transform.Find("Matching_Panel");
        }

        if (matchingPanelTrans != null)
        {
            matchingPanelTrans.gameObject.SetActive(true);
        }
        else
        {
            // Fallback in case Matching_Panel was deleted or not found
            MatchLauncher.StartLeagueMatch();
        }
    }

    public void OnAntremanButtonPressed()
    {
        Debug.Log("Antreman yapılacak!");
    }
}

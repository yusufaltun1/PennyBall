using UnityEngine;
using UnityEngine.SceneManagement;

public class ExerciseExitController : MonoBehaviour
{
    public void ExitToMainMenu()
    {
        MainMenuClickSound.Play();
        SceneManager.LoadScene(GameSceneNames.MainMenu);
    }
}

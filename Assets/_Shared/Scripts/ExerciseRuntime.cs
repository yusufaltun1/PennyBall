using UnityEngine.SceneManagement;

public static class ExerciseRuntime
{
    public static bool IsActive =>
        SceneManager.GetActiveScene().isLoaded
        && SceneManager.GetActiveScene().name == GameSceneNames.Exercise;
}

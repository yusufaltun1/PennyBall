using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Exercise sahnesinde rakip coinler ve oyuncu kalesi kaldırılır; antrenman modu hazırlanır.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-200)]
public class ExerciseSceneBootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureBootstrap()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || activeScene.name != GameSceneNames.Exercise)
        {
            return;
        }

        if (FindFirstObjectByType<ExerciseSceneBootstrap>() != null)
        {
            return;
        }

        var bootstrapObject = new GameObject("ExerciseBootstrap");
        bootstrapObject.AddComponent<ExerciseSceneBootstrap>();
    }

    void Awake()
    {
        if (SceneManager.GetActiveScene().name != GameSceneNames.Exercise)
        {
            Destroy(gameObject);
            return;
        }

        DestroyIfExists("Coin_E1");
        DestroyIfExists("Coin_E2");
        DestroyIfExists("Coin_E3");
        DestroyIfExists("Kale_P");
        DestroyIfExists("OpponentBot");
    }

    static void DestroyIfExists(string objectName)
    {
        GameObject target = GameObject.Find(objectName);
        if (target != null)
        {
            Destroy(target);
        }
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameRulesBootstrap
{
    static bool IsGameplayScene(string sceneName) =>
        sceneName == GameSceneNames.Game || sceneName == GameSceneNames.Exercise;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureGameRulesManager()
    {
        Scene activeScene = SceneManager.GetActiveScene();

        if (IsGameplayScene(activeScene.name) && GameRulesManager.Instance != null)
        {
            GameRulesManager.Instance.PrepareForNewMatch();
            return;
        }

        if (GameRulesManager.Instance != null)
        {
            return;
        }

        var rulesObject = new GameObject("GameRules");
        rulesObject.AddComponent<GameRulesManager>();
        rulesObject.AddComponent<GameFeedback>();
    }
}

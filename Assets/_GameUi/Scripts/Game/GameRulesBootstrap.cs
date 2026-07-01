using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameRulesBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureGameRulesManager()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.name == OnboardingSceneNames.Onboarding)
        {
            return;
        }

        if (activeScene.name == GameSceneNames.Game && GameRulesManager.Instance != null)
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

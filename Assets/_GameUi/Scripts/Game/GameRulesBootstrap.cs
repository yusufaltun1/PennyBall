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

        if (GameRulesManager.Instance != null)
        {
            return;
        }

        var rulesObject = new GameObject("GameRules");
        rulesObject.AddComponent<GameRulesManager>();
    }
}

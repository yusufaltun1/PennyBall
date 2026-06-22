using UnityEngine;

public static class GameRulesBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureGameRulesManager()
    {
        if (GameRulesManager.Instance != null)
        {
            return;
        }

        var rulesObject = new GameObject("GameRules");
        rulesObject.AddComponent<GameRulesManager>();
    }
}

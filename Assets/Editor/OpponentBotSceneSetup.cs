using UnityEditor;
using UnityEngine;

/// <summary>
/// Bot ve tur sistemini sahneye ekler.
/// PennyBall → Setup Opponent Bot menüsünü çalıştır.
/// </summary>
public static class OpponentBotSceneSetup
{
    [MenuItem("PennyBall/Setup Opponent Bot")]
    public static void SetupOpponentBot()
    {
        EnsureComponent<TurnController>("TurnController");
        EnsureComponent<MatchTurnCoordinator>("MatchTurnCoordinator");
        EnsureComponent<OpponentBotController>("OpponentBot");

        AttachGoalListenerToPlayerKale();

        Debug.Log("Opponent bot kuruldu. Kale_P GoalTrigger'a OpponentPlayerGoalListener eklendi.");
    }

    static void EnsureComponent<T>(string objectName) where T : Component
    {
        T existing = Object.FindFirstObjectByType<T>();
        if (existing != null)
        {
            return;
        }

        var gameObject = new GameObject(objectName);
        gameObject.AddComponent<T>();
        Undo.RegisterCreatedObjectUndo(gameObject, "Setup Opponent Bot");
    }

    static void AttachGoalListenerToPlayerKale()
    {
        GoalZone[] zones = Object.FindObjectsByType<GoalZone>(FindObjectsSortMode.None);
        for (int i = 0; i < zones.Length; i++)
        {
            GoalZone zone = zones[i];
            if (zone.transform.parent == null || !zone.transform.parent.name.Contains("Kale_P"))
            {
                continue;
            }

            if (zone.GetComponent<OpponentPlayerGoalListener>() == null)
            {
                Undo.AddComponent<OpponentPlayerGoalListener>(zone.gameObject);
            }
        }
    }
}

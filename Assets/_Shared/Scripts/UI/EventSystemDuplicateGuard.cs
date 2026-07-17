using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor'da birden fazla sahne açıkken veya sahne geçişlerinde
/// oluşan ikinci EventSystem uyarılarını önler.
/// </summary>
public static class EventSystemDuplicateGuard
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Deduplicate();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Deduplicate();
    }

    static void Deduplicate()
    {
        EventSystem[] systems = Object.FindObjectsByType<EventSystem>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        if (systems.Length <= 1)
        {
            return;
        }

        EventSystem keep = FindPreferred(systems);
        for (int i = 0; i < systems.Length; i++)
        {
            EventSystem candidate = systems[i];
            if (candidate == null || candidate == keep)
            {
                continue;
            }

            Debug.Log(
                $"[EventSystem] Duplicate kaldırıldı: {candidate.gameObject.scene.name}/{candidate.name}");
            Object.Destroy(candidate.gameObject);
        }
    }

    static EventSystem FindPreferred(EventSystem[] systems)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        for (int i = 0; i < systems.Length; i++)
        {
            EventSystem system = systems[i];
            if (system != null
                && system.isActiveAndEnabled
                && system.gameObject.scene == activeScene)
            {
                return system;
            }
        }

        if (EventSystem.current != null)
        {
            return EventSystem.current;
        }

        for (int i = 0; i < systems.Length; i++)
        {
            if (systems[i] != null)
            {
                return systems[i];
            }
        }

        return null;
    }
}

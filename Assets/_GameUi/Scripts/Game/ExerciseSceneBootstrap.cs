using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Egzersiz sahnesine özel kurallar <see cref="ExerciseRuntime"/> ve ilgili controller'larda uygulanır.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-200)]
public class ExerciseSceneBootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureBootstrap()
    {
        if (!ExerciseRuntime.IsActive)
        {
            return;
        }

        if (FindFirstObjectByType<ExerciseSceneBootstrap>() != null)
        {
            return;
        }

        var bootstrapObject = new GameObject("ExerciseBootstrap");
        bootstrapObject.AddComponent<ExerciseSceneBootstrap>();
        WireExitButton();
    }

    void Awake()
    {
        if (!ExerciseRuntime.IsActive)
        {
            Destroy(gameObject);
            return;
        }

        RemoveBoosterBar();
        WireExitButton();
    }

    void Start()
    {
        WireExitButton();
    }

    public static void WireExitButton()
    {
        if (!ExerciseRuntime.IsActive)
        {
            return;
        }

        GameObject exitObject = FindExitButtonObject();
        if (exitObject == null)
        {
            return;
        }

        exitObject.transform.SetAsLastSibling();

        ExerciseExitController controller = exitObject.GetComponent<ExerciseExitController>();
        if (controller == null)
        {
            controller = exitObject.AddComponent<ExerciseExitController>();
        }

        Button button = exitObject.GetComponent<Button>();
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(controller.ExitToMainMenu);
        button.onClick.AddListener(controller.ExitToMainMenu);
    }

    static GameObject FindExitButtonObject()
    {
        GameObject direct = GameObject.Find("exit");
        if (direct != null)
        {
            return direct;
        }

        GameObject canvas = GameObject.Find("Canvas");
        if (canvas == null)
        {
            return null;
        }

        Transform[] children = canvas.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == "exit")
            {
                return children[i].gameObject;
            }
        }

        return null;
    }

    static void RemoveBoosterBar()
    {
        BoostersMenuController menu = Object.FindFirstObjectByType<BoostersMenuController>();
        if (menu != null)
        {
            Object.Destroy(menu.gameObject);
        }
        else
        {
            DestroyIfExists("Boosters");
        }

        DestroyIfExists("BoostersUsed");
    }

    static void DestroyIfExists(string objectName)
    {
        GameObject target = GameObject.Find(objectName);
        if (target != null)
        {
            Object.Destroy(target);
        }
    }
}

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LeagueChangePanelSetup
{
    const string MainMenuScenePath = "Assets/_MainMenu/Scenes/MainMenu_Scene.unity";
    const string PrefabPath = "Assets/_MainMenu/Prefabs/LeagueChangePanel.prefab";

    [MenuItem("PennyBall/League/Setup LeagueChange Panel On MainMenu")]
    public static void SetupOnMainMenu()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "League Change Setup",
                "Bu işlem Play modunda çalışmaz.\n\nPlay'i durdur (Stop) ve Edit modunda tekrar çalıştır.",
                "Tamam");
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog(
                "League Change Setup",
                $"Prefab bulunamadı:\n{PrefabPath}\n\nGit geçmişinden geri yüklendiğinden emin ol.",
                "Tamam");
            return;
        }

        Scene mainMenu = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[League] MainMenu'de Canvas bulunamadı.");
            return;
        }

        RemoveWrongLeagueUpdatePanels(canvas.transform);

        LeagueChangePresenter existing =
            Object.FindFirstObjectByType<LeagueChangePresenter>(FindObjectsInactive.Include);
        if (existing == null)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
            instance.name = "LeagueChange";
            existing = instance.GetComponent<LeagueChangePresenter>();
        }

        existing.gameObject.SetActive(false);
        AssignToController(existing);

        EditorSceneManager.MarkSceneDirty(mainMenu);
        EditorSceneManager.SaveScene(mainMenu);
        Debug.Log("[League] LeagueChange paneli MainMenu'ye bağlandı.");
    }

    [MenuItem("PennyBall/League/Setup LeagueChange Panel On MainMenu", true)]
    static bool SetupOnMainMenuValidate()
    {
        return !Application.isPlaying;
    }

    static void RemoveWrongLeagueUpdatePanels(Transform canvas)
    {
        LeagueUpdateController[] wrongPanels =
            canvas.GetComponentsInChildren<LeagueUpdateController>(true);
        for (int i = 0; i < wrongPanels.Length; i++)
        {
            Object.DestroyImmediate(wrongPanels[i].gameObject);
        }
    }

    static void AssignToController(LeagueChangePresenter presenter)
    {
        LeagueSeasonResultController controller =
            Object.FindFirstObjectByType<LeagueSeasonResultController>(FindObjectsInactive.Include);
        if (controller == null)
        {
            GameObject uiManager = GameObject.Find("UIManager");
            if (uiManager != null)
            {
                controller = uiManager.GetComponent<LeagueSeasonResultController>();
                if (controller == null)
                {
                    controller = uiManager.AddComponent<LeagueSeasonResultController>();
                }
            }
        }

        if (controller == null)
        {
            Debug.LogWarning("[League] LeagueSeasonResultController bulunamadı.");
            return;
        }

        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("_leagueChangePanel").objectReferenceValue = presenter;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
    }
}

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LeagueChangePanelSetup
{
    const string GameUiScenePath = "Assets/_GameUi/Scenes/GameUI.unity";
    const string MainMenuScenePath = "Assets/_MainMenu/Scenes/MainMenu_Scene.unity";
    const string PrefabPath = "Assets/_MainMenu/Prefabs/LeagueChangePanel.prefab";

    [MenuItem("PennyBall/League/Setup LeagueChange Panel On MainMenu")]
    public static void SetupOnMainMenu()
    {
        EnsurePrefabExists();

        Scene mainMenu = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[League] MainMenu'de Canvas bulunamadı.");
            return;
        }

        LeagueChangePresenter existing = Object.FindFirstObjectByType<LeagueChangePresenter>(FindObjectsInactive.Include);
        if (existing != null)
        {
            existing.gameObject.SetActive(false);
            AssignToController(existing);
            EditorSceneManager.MarkSceneDirty(mainMenu);
            EditorSceneManager.SaveScene(mainMenu);
            Debug.Log("[League] LeagueChange zaten vardı, controller'a bağlandı ve kapatıldı.");
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[League] Prefab yok: {PrefabPath}");
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
        instance.name = "LeagueChange";
        instance.SetActive(false);

        LeagueChangePresenter presenter = instance.GetComponent<LeagueChangePresenter>();
        AssignToController(presenter);

        EditorSceneManager.MarkSceneDirty(mainMenu);
        EditorSceneManager.SaveScene(mainMenu);
        Debug.Log("[League] LeagueChange paneli MainMenu'ye eklendi.");
    }

    [MenuItem("PennyBall/League/Create LeagueChange Prefab From GameUI")]
    public static void EnsurePrefabExists()
    {
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existingPrefab != null)
        {
            return;
        }

        string folder = "Assets/_MainMenu/Prefabs";
        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder("Assets/_MainMenu", "Prefabs");
        }

        Scene gameUi = EditorSceneManager.OpenScene(GameUiScenePath, OpenSceneMode.Single);
        LeagueChangePresenter source = Object.FindFirstObjectByType<LeagueChangePresenter>(FindObjectsInactive.Include);
        if (source == null)
        {
            Debug.LogError("[League] GameUI içinde LeagueChange bulunamadı.");
            return;
        }

        source.gameObject.SetActive(true);
        GameObject prefabRoot = Object.Instantiate(source.gameObject);
        prefabRoot.name = "LeagueChangePanel";

        PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
        Object.DestroyImmediate(prefabRoot);

        source.gameObject.SetActive(false);
        EditorSceneManager.MarkSceneDirty(gameUi);
        EditorSceneManager.SaveScene(gameUi);

        Debug.Log($"[League] Prefab oluşturuldu: {PrefabPath}");
    }

    static void AssignToController(LeagueChangePresenter presenter)
    {
        LeagueSeasonResultController controller =
            Object.FindFirstObjectByType<LeagueSeasonResultController>(FindObjectsInactive.Include);
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

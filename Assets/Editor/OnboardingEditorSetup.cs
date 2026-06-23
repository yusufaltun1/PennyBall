using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class OnboardingEditorSetup
{
    const string GameUiScenePath = "Assets/_GameUi/Scenes/GameUI.unity";
    const string OnboardingScenePath = "Assets/_Onboarding/Scenes/Onboarding.unity";
    const string MainMenuScenePath = "Assets/_MainMenu/Scenes/MainMenu_Scene.unity";

    [MenuItem("PennyBall/Onboarding/Copy GameUI Scene To Onboarding")]
    public static void CopyGameUiSceneToOnboarding()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        if (!AssetDatabase.CopyAsset(GameUiScenePath, OnboardingScenePath))
        {
            System.IO.File.Copy(
                System.IO.Path.GetFullPath(GameUiScenePath),
                System.IO.Path.GetFullPath(OnboardingScenePath),
                true);
            AssetDatabase.ImportAsset(OnboardingScenePath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Onboarding] GameUI sahnesi Onboarding.unity olarak kopyalandı.");
        SetupOnboardingScene();
    }

    [MenuItem("PennyBall/Onboarding/Add Scenes To Build Settings")]
    public static void AddScenesToBuildSettings()
    {
        EditorBuildSettingsScene[] scenes =
        {
            new(MainMenuScenePath, true),
            new(OnboardingScenePath, true),
            new(GameUiScenePath, true)
        };

        EditorBuildSettings.scenes = scenes;
        Debug.Log("[Onboarding] Build Settings: MainMenu → Onboarding → GameUI");
    }

    [MenuItem("PennyBall/Onboarding/Setup Main Menu Router")]
    public static void SetupMainMenuRouter()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        EditorSceneManager.OpenScene(MainMenuScenePath);
        EnsureComponent<OnboardingFlowRouter>("OnboardingFlowRouter");

        OnboardingMainMenuPresenter presenter = EnsureComponent<OnboardingMainMenuPresenter>("OnboardingMainMenuPresenter");
        WireMainMenuPresenter(presenter);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[Onboarding] MainMenu router kuruldu.");
    }

    [MenuItem("PennyBall/Onboarding/Setup Onboarding Scene")]
    public static void SetupOnboardingScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        if (!System.IO.File.Exists(OnboardingScenePath))
        {
            CopyGameUiSceneToOnboarding();
            return;
        }

        EditorSceneManager.OpenScene(OnboardingScenePath);
        RemoveGameOnlyObjects();
        EnsureComponent<OnboardingSceneBootstrap>("OnboardingBootstrap");
        PrepareCoinsInEditor();
        OnboardingController controller = EnsureComponent<OnboardingController>("OnboardingController");
        OnboardingGuideView guideView = EnsureGuideUi();
        EnsureGoalDetector();
        WireOnboardingController(controller, guideView);
        EnsureOnboardingInputHandler(controller);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[Onboarding] Onboarding sahnesi GameUI ile aynı içerikte hazırlandı.");
    }

    [MenuItem("PennyBall/Onboarding/Reset Progress")]
    public static void ResetProgress()
    {
        OnboardingProgress.ResetAll();
        Debug.Log("[Onboarding] Progress sıfırlandı.");
    }

    public static void BatchSetupOnboardingScene()
    {
        CopyGameUiSceneToOnboarding();
        AddScenesToBuildSettings();
        EditorApplication.Exit(0);
    }

    static void RemoveGameOnlyObjects()
    {
        DestroyByName("OpponentBot");
        DestroyByName("MatchTurnCoordinator");
        DestroyByName("TurnController");
        DestroyByName("GameRules");
        DestroyByName("Coin_E1");
        DestroyByName("Coin_E2");
        DestroyByName("Coin_E3");
        DestroyByName("Kale_P");
    }

    static void DestroyByName(string objectName)
    {
        GameObject target = GameObject.Find(objectName);
        if (target == null)
        {
            return;
        }

        Undo.DestroyObjectImmediate(target);
    }

    static OnboardingGuideView EnsureGuideUi()
    {
        OnboardingGuideView existing = Object.FindFirstObjectByType<OnboardingGuideView>();
        if (existing != null)
        {
            return existing;
        }

        var canvasObject = new GameObject("OnboardingUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasObject, "Setup Onboarding UI");

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var textObject = new GameObject("InstructionText", typeof(RectTransform), typeof(Text));
        Undo.RegisterCreatedObjectUndo(textObject, "Setup Onboarding UI");
        textObject.transform.SetParent(canvasObject.transform, false);

        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 28;
        text.alignment = TextAnchor.UpperCenter;
        text.color = Color.white;
        text.text = "Onboarding";

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -40f);
        rect.sizeDelta = new Vector2(900f, 120f);

        OnboardingGuideView guideView = canvasObject.AddComponent<OnboardingGuideView>();
        SerializedObject guideSerialized = new SerializedObject(guideView);
        guideSerialized.FindProperty("_instructionText").objectReferenceValue = text;
        guideSerialized.ApplyModifiedPropertiesWithoutUndo();
        return guideView;
    }

    static void EnsureGoalDetector()
    {
        GameObject kaleE = GameObject.Find("Kale_E");
        if (kaleE == null)
        {
            Debug.LogWarning("[Onboarding] Kale_E bulunamadı.");
            return;
        }

        Transform goalTrigger = kaleE.transform.Find("GoalTrigger");
        if (goalTrigger == null)
        {
            Debug.LogWarning("[Onboarding] Kale_E/GoalTrigger bulunamadı.");
            return;
        }

        if (goalTrigger.GetComponent<OnboardingGoalDetector>() == null)
        {
            Undo.AddComponent<OnboardingGoalDetector>(goalTrigger.gameObject);
        }

        Collider collider = goalTrigger.GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = true;
        }
    }

    static void WireMainMenuPresenter(OnboardingMainMenuPresenter presenter)
    {
        SerializedObject serialized = new SerializedObject(presenter);
        GameObject playButton = GameObject.Find("Btn_Play");
        if (playButton != null)
        {
            serialized.FindProperty("_playButton").objectReferenceValue = playButton.GetComponent<Button>();
        }

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas != null)
        {
            CanvasGroup group = canvas.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = Undo.AddComponent<CanvasGroup>(canvas.gameObject);
            }

            serialized.FindProperty("_menuCanvasGroup").objectReferenceValue = group;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void PrepareCoinsInEditor()
    {
        string[] coinNames = { "Coin_P1", "Coin_P2", "Coin_P3" };
        for (int i = 0; i < coinNames.Length; i++)
        {
            GameObject coinObject = GameObject.Find(coinNames[i]);
            if (coinObject == null)
            {
                continue;
            }

            DisableComponent<CoinDragController>(coinObject);
            DisableComponent<CoinIdentity>(coinObject);
            DisableComponent<CoinVisualState>(coinObject);

            if (coinObject.GetComponent<OnboardingAimIndicator>() == null)
            {
                Undo.AddComponent<OnboardingAimIndicator>(coinObject);
            }

            if (coinObject.GetComponent<OnboardingCoinDragController>() == null)
            {
                Undo.AddComponent<OnboardingCoinDragController>(coinObject);
            }

            OnboardingCoin coin = coinObject.GetComponent<OnboardingCoin>();
            if (coin == null)
            {
                coin = Undo.AddComponent<OnboardingCoin>(coinObject);
            }

            coin.Configure(i);
        }
    }

    static void DisableComponent<T>(GameObject target) where T : Behaviour
    {
        T component = target.GetComponent<T>();
        if (component != null)
        {
            component.enabled = false;
        }
    }

    static void WireOnboardingController(OnboardingController controller, OnboardingGuideView guideView)
    {
        SerializedObject serialized = new SerializedObject(controller);

        OnboardingCoin[] coins = new OnboardingCoin[3];
        for (int i = 0; i < 3; i++)
        {
            GameObject coinObject = GameObject.Find($"Coin_P{i + 1}");
            if (coinObject == null)
            {
                continue;
            }

            coins[i] = coinObject.GetComponent<OnboardingCoin>();
        }

        serialized.FindProperty("_coins").arraySize = 3;
        for (int i = 0; i < 3; i++)
        {
            serialized.FindProperty("_coins").GetArrayElementAtIndex(i).objectReferenceValue = coins[i];
        }

        GameObject gateA = GameObject.Find("Coin_P1");
        GameObject gateB = GameObject.Find("Coin_P3");
        serialized.FindProperty("_gateCoinA").objectReferenceValue = gateA != null ? gateA.GetComponent<OnboardingCoin>() : null;
        serialized.FindProperty("_gateCoinB").objectReferenceValue = gateB != null ? gateB.GetComponent<OnboardingCoin>() : null;

        OnboardingGoalDetector goalDetector = Object.FindFirstObjectByType<OnboardingGoalDetector>();
        serialized.FindProperty("_goalDetector").objectReferenceValue = goalDetector;
        serialized.FindProperty("_guideView").objectReferenceValue = guideView;

        OnboardingStepDefinition[] defaultSteps = OnboardingDefaultSteps.Create();

        SerializedProperty stepsProperty = serialized.FindProperty("_steps");
        stepsProperty.arraySize = defaultSteps.Length;
        for (int i = 0; i < defaultSteps.Length; i++)
        {
            SerializedProperty element = stepsProperty.GetArrayElementAtIndex(i);
            OnboardingStepDefinition step = defaultSteps[i];
            element.FindPropertyRelative("stepType").enumValueIndex = (int)step.stepType;
            element.FindPropertyRelative("activeCoinIndex").intValue = step.activeCoinIndex;
            element.FindPropertyRelative("gateCoinAIndex").intValue = step.gateCoinAIndex;
            element.FindPropertyRelative("gateCoinBIndex").intValue = step.gateCoinBIndex;
            element.FindPropertyRelative("targetLaunchDirection").vector3Value = step.targetLaunchDirection;
            element.FindPropertyRelative("targetDirectionYawOffsetDegrees").floatValue = step.targetDirectionYawOffsetDegrees;
            element.FindPropertyRelative("targetPullDistance").floatValue = step.targetPullDistance;
            element.FindPropertyRelative("directionToleranceDegrees").floatValue = step.directionToleranceDegrees;
            element.FindPropertyRelative("pullTolerance").floatValue = step.pullTolerance;
            element.FindPropertyRelative("isFinalStep").boolValue = step.isFinalStep;
            element.FindPropertyRelative("instructionText").stringValue = step.instructionText;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void EnsureOnboardingInputHandler(OnboardingController controller)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            camera = Object.FindFirstObjectByType<Camera>();
        }

        if (camera == null)
        {
            Debug.LogWarning("[Onboarding] Kamera bulunamadı.");
            return;
        }

        OnboardingCoinInputHandler inputHandler = camera.GetComponent<OnboardingCoinInputHandler>();
        if (inputHandler == null)
        {
            inputHandler = Undo.AddComponent<OnboardingCoinInputHandler>(camera.gameObject);
        }

        SerializedObject serialized = new SerializedObject(inputHandler);
        serialized.FindProperty("_camera").objectReferenceValue = camera;
        serialized.FindProperty("_controller").objectReferenceValue = controller;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CoinInputHandler gameInput = camera.GetComponent<CoinInputHandler>();
        if (gameInput != null)
        {
            gameInput.enabled = false;
        }
    }

    static T EnsureComponent<T>(string objectName) where T : Component
    {
        T existing = Object.FindFirstObjectByType<T>();
        if (existing != null)
        {
            return existing;
        }

        var gameObject = new GameObject(objectName);
        Undo.RegisterCreatedObjectUndo(gameObject, "Setup Onboarding");
        return gameObject.AddComponent<T>();
    }
}

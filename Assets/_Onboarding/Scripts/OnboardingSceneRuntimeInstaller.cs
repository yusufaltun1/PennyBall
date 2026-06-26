using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Editor kurulumu yapılmamış olsa bile onboarding sahnesini çalışır hale getirir.
/// </summary>
public static class OnboardingSceneRuntimeInstaller
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void BootstrapAfterSceneLoad()
    {
        Scene activeScene = SceneManager.GetActiveScene();

        if (activeScene.name == OnboardingSceneNames.Onboarding)
        {
            EnsureEarlySetupRunner();
        }
    }

    static void EnsureEarlySetupRunner()
    {
        if (Object.FindFirstObjectByType<OnboardingEarlySetup>() != null)
        {
            return;
        }

        var runnerObject = new GameObject("OnboardingEarlySetup");
        runnerObject.AddComponent<OnboardingEarlySetup>();
    }

    public static void Install()
    {
        RemoveGameOnlyObjects();
        DisableAll<CoinInputHandler>();
        DestroyAll<GoalZone>();
        DestroyAllGameRulesManagers();
        DestroyAll<TurnController>();
        DestroyAll<MatchTurnCoordinator>();
        DestroyAll<OpponentBotController>();

        OnboardingCoin[] coins = PreparePlayerCoins();
        OnboardingGoalDetector goalDetector = EnsureGoalDetector();
        OnboardingGuideView guideView = EnsureGuideView();
        OnboardingController controller = EnsureController(coins, goalDetector, guideView);
        EnsureInputHandler(controller);
    }

    static void DestroyAllGameRulesManagers()
    {
        GameRulesManager[] managers = Object.FindObjectsByType<GameRulesManager>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < managers.Length; i++)
        {
            Object.Destroy(managers[i].gameObject);
        }
    }

    static void RemoveGameOnlyObjects()
    {
        DestroyIfExists("OpponentBot");
        DestroyIfExists("MatchTurnCoordinator");
        DestroyIfExists("TurnController");
        DestroyIfExists("GameRules");
        DestroyIfExists("Coin_E1");
        DestroyIfExists("Coin_E2");
        DestroyIfExists("Coin_E3");
        DestroyIfExists("Kale_P");
    }

    static void DestroyIfExists(string objectName)
    {
        GameObject target = GameObject.Find(objectName);
        if (target != null)
        {
            Object.Destroy(target);
        }
    }

    static void DisableAll<T>() where T : Behaviour
    {
        T[] components = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < components.Length; i++)
        {
            components[i].enabled = false;
        }
    }

    static void DestroyAll<T>() where T : Behaviour
    {
        T[] components = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < components.Length; i++)
        {
            Object.Destroy(components[i]);
        }
    }

    static OnboardingCoin[] PreparePlayerCoins()
    {
        string[] coinNames = { "Coin_P1", "Coin_P2", "Coin_P3" };
        OnboardingCoin[] coins = new OnboardingCoin[coinNames.Length];

        for (int i = 0; i < coinNames.Length; i++)
        {
            GameObject coinObject = GameObject.Find(coinNames[i]);
            if (coinObject == null)
            {
                continue;
            }

            DisableOnCoin<CoinDragController>(coinObject);
            DisableOnCoin<CoinIdentity>(coinObject);
            DisableOnCoin<CoinVisualState>(coinObject);
            ClearCoinVisualOverrides(coinObject);

            if (coinObject.GetComponent<OnboardingAimIndicator>() == null)
            {
                coinObject.AddComponent<OnboardingAimIndicator>();
            }

            if (coinObject.GetComponent<OnboardingCoinDragController>() == null)
            {
                coinObject.AddComponent<OnboardingCoinDragController>();
            }

            OnboardingCoin coin = coinObject.GetComponent<OnboardingCoin>();
            if (coin == null)
            {
                coin = coinObject.AddComponent<OnboardingCoin>();
            }

            coin.Configure(i);
            coins[i] = coin;
        }

        return coins;
    }

    static void DisableOnCoin<T>(GameObject coinObject) where T : Behaviour
    {
        T component = coinObject.GetComponent<T>();
        if (component != null)
        {
            component.enabled = false;
        }
    }

    static void ClearCoinVisualOverrides(GameObject coinObject)
    {
        Transform coinVisual = coinObject.transform.Find("Coin_Object");
        if (coinVisual == null)
        {
            return;
        }

        Renderer[] renderers = coinVisual.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            int materialCount = renderer.sharedMaterials.Length;
            for (int materialIndex = 0; materialIndex < materialCount; materialIndex++)
            {
                renderer.SetPropertyBlock(null, materialIndex);
            }
        }
    }

    static OnboardingGoalDetector EnsureGoalDetector()
    {
        OnboardingGoalDetector existing = Object.FindFirstObjectByType<OnboardingGoalDetector>();
        if (existing != null)
        {
            Collider existingCollider = existing.GetComponent<Collider>();
            if (existingCollider != null)
            {
                existingCollider.isTrigger = true;
            }

            return existing;
        }

        GameObject kaleE = GameObject.Find("Kale_E");
        if (kaleE == null)
        {
            return null;
        }

        Transform goalTrigger = kaleE.transform.Find("GoalTrigger");
        if (goalTrigger == null)
        {
            return null;
        }

        Collider collider = goalTrigger.GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = true;
        }

        return goalTrigger.gameObject.AddComponent<OnboardingGoalDetector>();
    }

    static OnboardingGuideView EnsureGuideView()
    {
        OnboardingGuideView existing = Object.FindFirstObjectByType<OnboardingGuideView>();
        if (existing != null)
        {
            return existing;
        }

        var canvasObject = new GameObject("OnboardingUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var textObject = new GameObject("InstructionText", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(canvasObject.transform, false);

        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 28;
        text.alignment = TextAnchor.UpperCenter;
        text.color = Color.white;

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -40f);
        rect.sizeDelta = new Vector2(900f, 120f);

        OnboardingGuideView guideView = canvasObject.AddComponent<OnboardingGuideView>();
        guideView.BindInstructionText(text);
        return guideView;
    }

    static OnboardingController EnsureController(
        OnboardingCoin[] coins,
        OnboardingGoalDetector goalDetector,
        OnboardingGuideView guideView)
    {
        OnboardingStepDefinition[] steps = OnboardingDefaultSteps.Create();

        OnboardingController[] existingControllers = Object.FindObjectsByType<OnboardingController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < existingControllers.Length; i++)
        {
            Object.Destroy(existingControllers[i].gameObject);
        }

        var controllerObject = new GameObject("OnboardingController");
        OnboardingController controller = controllerObject.AddComponent<OnboardingController>();
        controller.EnsureConfigured(coins, coins[0], coins[2], goalDetector, guideView, steps);
        return controller;
    }

    static void EnsureInputHandler(OnboardingController controller)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            camera = Object.FindFirstObjectByType<Camera>();
        }

        if (camera == null)
        {
            return;
        }

        OnboardingCoinInputHandler inputHandler = camera.GetComponent<OnboardingCoinInputHandler>();
        if (inputHandler == null)
        {
            inputHandler = camera.gameObject.AddComponent<OnboardingCoinInputHandler>();
        }

        inputHandler.Bind(camera, controller);

        OnboardingCoinInputHandler[] duplicateHandlers = Object.FindObjectsByType<OnboardingCoinInputHandler>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < duplicateHandlers.Length; i++)
        {
            if (duplicateHandlers[i] != inputHandler)
            {
                Object.Destroy(duplicateHandlers[i]);
            }
        }

        CoinInputHandler gameInput = camera.GetComponent<CoinInputHandler>();
        if (gameInput != null)
        {
            gameInput.enabled = false;
            Object.Destroy(gameInput);
        }
    }
}

[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
sealed class OnboardingEarlySetup : MonoBehaviour
{
    void Awake()
    {
        OnboardingSceneRuntimeInstaller.Install();
        Destroy(gameObject);
    }
}

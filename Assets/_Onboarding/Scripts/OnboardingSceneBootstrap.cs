using UnityEngine;

/// <summary>
/// Onboarding sahnesinde oyun scriptlerini devre dışı bırakır, coinlere onboarding bileşenlerini ekler.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-200)]
public class OnboardingSceneBootstrap : MonoBehaviour
{
    [SerializeField] string[] _playerCoinObjectNames = { "Coin_P1", "Coin_P2", "Coin_P3" };

    void Awake()
    {
        OnboardingSceneRuntimeInstaller.Install();
        DestroyGameObjectIfExists("OpponentBot");
        DestroyGameObjectIfExists("MatchTurnCoordinator");
        DestroyGameObjectIfExists("TurnController");
        DestroyGameObjectIfExists("GameRules");
        DestroyGameObjectIfExists("Coin_E1");
        DestroyGameObjectIfExists("Coin_E2");
        DestroyGameObjectIfExists("Coin_E3");
        DestroyGameObjectIfExists("Kale_P");

        DisableGameComponent<CoinInputHandler>();

        GameRulesManager rulesManager = FindFirstObjectByType<GameRulesManager>();
        if (rulesManager != null)
        {
            Destroy(rulesManager.gameObject);
        }

        DisableGameComponent<OpponentBotController>();
        DisableGameComponent<MatchTurnCoordinator>();
        DisableGameComponent<TurnController>();

        for (int i = 0; i < _playerCoinObjectNames.Length; i++)
        {
            GameObject coinObject = GameObject.Find(_playerCoinObjectNames[i]);
            if (coinObject == null)
            {
                continue;
            }

            PrepareCoin(coinObject, i);
        }
    }

    static void PrepareCoin(GameObject coinObject, int coinIndex)
    {
        DisableGameComponent<CoinDragController>(coinObject);
        DisableGameComponent<CoinIdentity>(coinObject);
        DisableGameComponent<CoinVisualState>(coinObject);

        if (coinObject.GetComponent<OnboardingAimIndicator>() == null)
        {
            coinObject.AddComponent<OnboardingAimIndicator>();
        }

        if (coinObject.GetComponent<OnboardingCoinDragController>() == null)
        {
            coinObject.AddComponent<OnboardingCoinDragController>();
        }

        OnboardingCoin onboardingCoin = coinObject.GetComponent<OnboardingCoin>();
        if (onboardingCoin == null)
        {
            onboardingCoin = coinObject.AddComponent<OnboardingCoin>();
        }

        onboardingCoin.Configure(coinIndex);
    }

    static void DestroyGameObjectIfExists(string objectName)
    {
        GameObject target = GameObject.Find(objectName);
        if (target != null)
        {
            Destroy(target);
        }
    }

    static void DisableGameComponent<T>() where T : Behaviour
    {
        T[] components = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < components.Length; i++)
        {
            components[i].enabled = false;
        }
    }

    static void DisableGameComponent<T>(GameObject target) where T : Behaviour
    {
        T component = target.GetComponent<T>();
        if (component != null)
        {
            component.enabled = false;
        }
    }
}

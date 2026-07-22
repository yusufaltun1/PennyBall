using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Oyuncu ve bot/remote round reset'lerini bağlar.
/// Online maçta bot kapatılır, RemoteOpponentController kullanılır.
/// </summary>
public class MatchTurnCoordinator : MonoBehaviour
{
    [SerializeField] bool _enableOpponentBot = true;

    void Start()
    {
        Subscribe();

        if (SceneManager.GetActiveScene().name == OnboardingSceneNames.Onboarding)
        {
            OnboardingSceneBootstrap.EnsureSceneSetup();
            _enableOpponentBot = false;
        }

        if (OnlineMatchSession.IsOnlineMatch
            || MatchSessionContext.IsOnlineMatch
            || PendingPhotonSession.HasPending)
        {
            _enableOpponentBot = false;
            EnsureRemoteOpponent();
            OpponentBotController.Instance?.DisableForOnlineMatch();
        }

        GameRulesManager.Instance?.PrepareForNewMatch();
        StartCoroutine(StartOpponentWhenReady());
    }

    static void EnsureRemoteOpponent()
    {
        if (RemoteOpponentController.Instance != null)
        {
            RemoteOpponentController.Instance.OnMatchStarted();
            return;
        }

        var go = new GameObject("RemoteOpponentController");
        go.AddComponent<RemoteOpponentController>().OnMatchStarted();
    }

    IEnumerator StartOpponentWhenReady()
    {
        while (MatchBeginningCountdownController.IsActive)
        {
            yield return null;
        }

        if (_enableOpponentBot && OpponentBotController.Instance != null)
        {
            OpponentBotController.Instance.ResetRoundState(isMatchOpening: true);
        }
    }

    void OnDestroy()
    {
        Unsubscribe();
    }

    void Subscribe()
    {
        if (GameRulesManager.Instance != null)
        {
            GameRulesManager.Instance.RoundReset += OnRoundReset;
        }
    }

    void Unsubscribe()
    {
        if (GameRulesManager.Instance != null)
        {
            GameRulesManager.Instance.RoundReset -= OnRoundReset;
        }
    }

    void OnRoundReset()
    {
        if (!_enableOpponentBot || OpponentBotController.Instance == null)
        {
            return;
        }

        OpponentBotController.Instance.ResetRoundState();
    }
}

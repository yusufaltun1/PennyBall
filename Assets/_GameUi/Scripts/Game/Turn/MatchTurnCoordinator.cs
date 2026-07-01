using System.Collections;
using UnityEngine;

/// <summary>
/// Oyuncu ve bot round reset'lerini bağlar. Sıra tabanlı geçiş yok — her iki taraf bağımsız oynar.
/// </summary>
public class MatchTurnCoordinator : MonoBehaviour
{
    [SerializeField] bool _enableOpponentBot = true;

    void Start()
    {
        Subscribe();
        GameRulesManager.Instance?.PrepareForNewMatch();
        StartCoroutine(StartOpponentWhenReady());
    }

    IEnumerator StartOpponentWhenReady()
    {
        while (MatchBeginningCountdownController.IsActive)
        {
            yield return null;
        }

        if (_enableOpponentBot && OpponentBotController.Instance != null)
        {
            OpponentBotController.Instance.ResetRoundState();
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

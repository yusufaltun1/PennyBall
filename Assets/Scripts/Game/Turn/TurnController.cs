using System;
using UnityEngine;

/// <summary>
/// Kimin sırası olduğunu yönetir. Input ve bot bu sınıfa bakar.
/// </summary>
public class TurnController : MonoBehaviour
{
    public static TurnController Instance { get; private set; }

    [SerializeField] CoinTeam _startingTeam = CoinTeam.Player;

    CoinTeam _activeTeam;

    public CoinTeam ActiveTeam => _activeTeam;
    public bool IsPlayerTurn => _activeTeam == CoinTeam.Player;
    public bool IsOpponentTurn => _activeTeam == CoinTeam.Opponent;

    public event Action<CoinTeam> TurnChanged;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        _activeTeam = _startingTeam;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void BeginMatch()
    {
        _activeTeam = _startingTeam;
        TurnChanged?.Invoke(_activeTeam);
    }

    public void SwitchTo(CoinTeam team)
    {
        if (_activeTeam == team)
        {
            return;
        }

        _activeTeam = team;
        TurnChanged?.Invoke(_activeTeam);
    }

    public void SwitchToOpponent() => SwitchTo(CoinTeam.Opponent);
    public void SwitchToPlayer() => SwitchTo(CoinTeam.Player);
}

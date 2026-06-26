using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class GoalZone : MonoBehaviour
{
    [SerializeField] CoinTeam _goalOwner;
    [SerializeField] bool _autoDetectOwnerFromName = true;

    readonly HashSet<int> _coinsInside = new();

    void Awake()
    {
        if (_autoDetectOwnerFromName && transform.parent != null)
        {
            DetectGoalOwnerFromName(transform.parent.name);
        }
    }

    void DetectGoalOwnerFromName(string kaleName)
    {
        if (kaleName.Contains("_E"))
        {
            _goalOwner = CoinTeam.Opponent;
            return;
        }

        if (kaleName.Contains("_P"))
        {
            _goalOwner = CoinTeam.Player;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        CoinIdentity coin = other.GetComponentInParent<CoinIdentity>();
        if (coin == null)
        {
            return;
        }

        int coinId = coin.GetInstanceID();
        if (_coinsInside.Contains(coinId))
        {
            return;
        }

        _coinsInside.Add(coinId);

        if (_goalOwner == CoinTeam.Opponent && coin.Team == CoinTeam.Player)
        {
            GameRulesManager.Instance?.NotifyCoinEnteredGoal(coin);
        }
        else if (_goalOwner == CoinTeam.Player && coin.Team == CoinTeam.Opponent)
        {
            OpponentBotController.Instance?.NotifyGoalEntered(coin);
        }
    }

    public bool IsOpponentGoal => _goalOwner == CoinTeam.Opponent;

    public bool IsCoinInside(CoinIdentity coin)
    {
        return coin != null && _coinsInside.Contains(coin.GetInstanceID());
    }

    public bool ContainsWorldPosition(Vector3 worldPosition)
    {
        BoxCollider boxCollider = GetComponent<BoxCollider>();
        if (boxCollider == null)
        {
            return false;
        }

        return boxCollider.bounds.Contains(worldPosition);
    }

    void OnTriggerExit(Collider other)
    {
        CoinIdentity coin = other.GetComponentInParent<CoinIdentity>();
        if (coin == null)
        {
            return;
        }

        _coinsInside.Remove(coin.GetInstanceID());
    }
}

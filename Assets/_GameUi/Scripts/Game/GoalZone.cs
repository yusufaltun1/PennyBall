using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// EnemyGoalArea / PlayerGoalArea — kale içi trigger hacmi.
/// Overlap yalnızca fizik collider kesişimine bakar (ComputePenetration).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class GoalZone : MonoBehaviour
{
    public const string EnemyGoalAreaName = "EnemyGoalArea";
    public const string PlayerGoalAreaName = "PlayerGoalArea";

    [SerializeField] CoinTeam _goalOwner;
    [SerializeField] bool _autoDetectOwnerFromName = true;

    readonly HashSet<int> _coinsInside = new();

    void Awake()
    {
        if (_autoDetectOwnerFromName)
        {
            DetectGoalOwnerFromName();
        }
    }

    void DetectGoalOwnerFromName()
    {
        string objectName = gameObject.name;
        if (objectName == EnemyGoalAreaName || objectName.Contains("EnemyGoal"))
        {
            _goalOwner = CoinTeam.Opponent;
            return;
        }

        if (objectName == PlayerGoalAreaName || objectName.Contains("PlayerGoal"))
        {
            _goalOwner = CoinTeam.Player;
            return;
        }

        Transform current = transform;
        while (current != null)
        {
            string kaleName = current.name;
            if (kaleName.Contains("_E"))
            {
                _goalOwner = CoinTeam.Opponent;
                return;
            }

            if (kaleName.Contains("_P"))
            {
                _goalOwner = CoinTeam.Player;
                return;
            }

            current = current.parent;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        CoinIdentity coin = other.GetComponentInParent<CoinIdentity>();
        if (coin == null)
        {
            return;
        }

        if (!_coinsInside.Add(coin.GetInstanceID()))
        {
            return;
        }

        // Spekülatif / yanlış trigger'ı ele: gerçekten penetre etmiyorsa sayma.
        if (!OverlapsCoin(coin))
        {
            _coinsInside.Remove(coin.GetInstanceID());
            return;
        }

        NotifyEntered(coin);
    }

    void OnTriggerStay(Collider other)
    {
        CoinIdentity coin = other.GetComponentInParent<CoinIdentity>();
        if (coin == null)
        {
            return;
        }

        if (!OverlapsCoin(coin))
        {
            _coinsInside.Remove(coin.GetInstanceID());
            return;
        }

        _coinsInside.Add(coin.GetInstanceID());
        NotifyEntered(coin);
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

    void NotifyEntered(CoinIdentity coin)
    {
        if (_goalOwner == CoinTeam.Opponent && coin.Team == CoinTeam.Player)
        {
            GameRulesManager.Instance?.NotifyCoinEnteredGoal(coin);
        }
        else if (_goalOwner == CoinTeam.Player && coin.Team == CoinTeam.Opponent)
        {
            // Online'da rakip golü Photon RPC ile gelir; lokal bot path kapalı.
            if (OnlineMatchSession.IsOnlineMatch || MatchSessionContext.IsOnlineMatch)
            {
                return;
            }

            OpponentBotController.Instance?.NotifyGoalEntered(coin);
        }
    }

    public bool IsOpponentGoal => _goalOwner == CoinTeam.Opponent;

    public void ClearTrackingForCoin(CoinIdentity coin)
    {
        if (coin != null)
        {
            _coinsInside.Remove(coin.GetInstanceID());
        }
    }

    /// <summary>
    /// Coin fizik collider'ı ile GoalArea gerçekten kesişiyor mu?
    /// (useVisualMesh parametresi geriye dönük uyumluluk için durur; skorlama fiziğe bakar.)
    /// </summary>
    public bool OverlapsCoin(CoinIdentity coin, bool useVisualMesh = true)
    {
        if (coin == null)
        {
            return false;
        }

        Physics.SyncTransforms();

        BoxCollider goalCollider = GetComponent<BoxCollider>();
        Collider coinCollider = coin.GetComponentInChildren<Collider>();
        if (goalCollider == null || coinCollider == null)
        {
            return false;
        }

        // Trigger + non-trigger arasında en güvenilir kesişim testi.
        if (Physics.ComputePenetration(
                coinCollider,
                coinCollider.transform.position,
                coinCollider.transform.rotation,
                goalCollider,
                goalCollider.transform.position,
                goalCollider.transform.rotation,
                out _,
                out float separationDistance))
        {
            return separationDistance > 0.0001f;
        }

        return false;
    }

    public static GoalZone FindOpponentGoalArea()
    {
        GoalZone[] zones = FindObjectsByType<GoalZone>(FindObjectsSortMode.None);
        for (int i = 0; i < zones.Length; i++)
        {
            if (zones[i].IsOpponentGoal)
            {
                return zones[i];
            }
        }

        return null;
    }

    public static GoalZone FindPlayerGoalArea()
    {
        GoalZone[] zones = FindObjectsByType<GoalZone>(FindObjectsSortMode.None);
        for (int i = 0; i < zones.Length; i++)
        {
            if (!zones[i].IsOpponentGoal)
            {
                return zones[i];
            }
        }

        return null;
    }
}

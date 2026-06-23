using UnityEngine;

/// <summary>
/// Kale_P trigger'ında rakip coin golünü algılar.
/// GoalZone.cs'e dokunmadan rakip golünü bot tarafına iletir.
/// Kale prefab'ındaki GoalTrigger'a eklenir (Kale_P instance).
/// </summary>
[DisallowMultipleComponent]
public class OpponentPlayerGoalListener : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (transform.parent == null || !transform.parent.name.Contains("_P"))
        {
            return;
        }

        CoinIdentity coin = other.GetComponentInParent<CoinIdentity>();
        if (coin == null || coin.Team != CoinTeam.Opponent)
        {
            return;
        }

        if (OpponentBotController.Instance != null)
        {
            OpponentBotController.Instance.NotifyGoalEntered(coin);
        }
    }
}

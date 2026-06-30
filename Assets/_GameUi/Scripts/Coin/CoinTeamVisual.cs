using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CoinIdentity))]
public class CoinTeamVisual : MonoBehaviour
{
    [SerializeField] Material _playerOuter;
    [SerializeField] Material _playerInner;
    [SerializeField] Material _enemyOuter;
    [SerializeField] Material _enemyInner;

    public void ApplyTeamMaterials()
    {
        CoinIdentity identity = GetComponent<CoinIdentity>();
        bool enemy = identity != null && identity.Team == CoinTeam.Opponent;

        Transform coinObject = transform.Find("Coin_Object");
        if (coinObject == null)
        {
            return;
        }

        Renderer renderer = coinObject.GetComponent<Renderer>();
        if (renderer == null)
        {
            return;
        }

        Material outer = enemy ? _enemyOuter : _playerOuter;
        Material inner = enemy ? _enemyInner : _playerInner;
        if (outer == null || inner == null)
        {
            return;
        }

        renderer.sharedMaterials = new[] { outer, inner };
    }
}

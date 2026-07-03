using System.Collections.Generic;
using UnityEngine;

public enum CameraEdgeZoneSide
{
    Left,
    Right
}

[DisallowMultipleComponent]
public class CameraEdgeZoneTracker : MonoBehaviour
{
    static readonly HashSet<CoinIdentity> LeftCoins = new();
    static readonly HashSet<CoinIdentity> RightCoins = new();

    [SerializeField] CameraEdgeZoneSide _side;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState()
    {
        LeftCoins.Clear();
        RightCoins.Clear();
    }

    public static bool TryGetPlayerCoinZone(CoinIdentity coin, out CameraEdgeZoneSide side)
    {
        side = default;

        if (coin == null || coin.Team != CoinTeam.Player)
        {
            return false;
        }

        bool inLeft = LeftCoins.Contains(coin);
        bool inRight = RightCoins.Contains(coin);
        if (!inLeft && !inRight)
        {
            return false;
        }

        if (inLeft)
        {
            side = CameraEdgeZoneSide.Left;
            return true;
        }

        if (inRight)
        {
            side = CameraEdgeZoneSide.Right;
            return true;
        }

        return false;
    }

    void OnTriggerStay(Collider other)
    {
        SetMembership(other, true);
    }

    void OnTriggerEnter(Collider other)
    {
        SetMembership(other, true);
    }

    void OnTriggerExit(Collider other)
    {
        SetMembership(other, false);
    }

    void SetMembership(Collider other, bool enter)
    {
        CoinIdentity identity = other.GetComponentInParent<CoinIdentity>();
        if (identity == null || identity.Team != CoinTeam.Player)
        {
            return;
        }

        HashSet<CoinIdentity> set = _side == CameraEdgeZoneSide.Left ? LeftCoins : RightCoins;
        if (enter)
        {
            set.Add(identity);
        }
        else
        {
            set.Remove(identity);
        }
    }
}

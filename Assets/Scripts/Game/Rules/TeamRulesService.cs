using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Takım kuralları — saf C#, MonoBehaviour değil.
/// GameRulesManager (oyuncu) ve OpponentBotController (rakip) burayı kullanır.
/// </summary>
public static class TeamRulesService
{
    public static void DiscoverCoins(TeamRoundState state, string coinNameSuffix)
    {
        state.Coins.Clear();

        CoinIdentity[] coins = UnityEngine.Object.FindObjectsByType<CoinIdentity>(FindObjectsSortMode.None);
        for (int i = 0; i < coins.Length; i++)
        {
            CoinIdentity coin = coins[i];
            if (coin != null && coin.gameObject.name.Contains(coinNameSuffix))
            {
                state.Coins.Add(coin);
            }
        }

        ResolveOpeningCoin(state);
    }

    public static void BeginNewRound(TeamRoundState state)
    {
        state.IsFirstMove = true;
        state.OpeningCoin = null;
        state.WaitingForOthers.Clear();
    }

    public static void ResolveOpeningCoin(TeamRoundState state)
    {
        state.OpeningCoin = null;

        if (state.Coins.Count == 0)
        {
            return;
        }

        if (state.Coins.Count == 1)
        {
            state.OpeningCoin = state.Coins[0];
            return;
        }

        state.Coins.Sort(CompareCoinHorizontalPosition);
        state.OpeningCoin = state.Coins[state.Coins.Count / 2];
    }

    public static bool CanSelectCoin(
        TeamRoundState state,
        CoinIdentity coin,
        bool isResolvingMove,
        bool isPassive)
    {
        if (isResolvingMove || coin == null || isPassive)
        {
            return false;
        }

        if (coin.DragController.IsSliding || coin.DragController.IsAiming)
        {
            return false;
        }

        if (state.IsFirstMove && state.OpeningCoin != null && coin != state.OpeningCoin)
        {
            return false;
        }

        return true;
    }

    public static void ApplyOpeningRestrictions(TeamRoundState state, Action<CoinIdentity, bool> setPassive)
    {
        if (state.OpeningCoin == null)
        {
            return;
        }

        setPassive(state.OpeningCoin, false);

        for (int i = 0; i < state.Coins.Count; i++)
        {
            CoinIdentity coin = state.Coins[i];
            if (coin == state.OpeningCoin)
            {
                continue;
            }

            setPassive(coin, true);
        }
    }

    public static void UnlockOpeningSideCoins(TeamRoundState state, Action<CoinIdentity, bool> setPassive)
    {
        if (state.OpeningCoin == null)
        {
            return;
        }

        for (int i = 0; i < state.Coins.Count; i++)
        {
            CoinIdentity coin = state.Coins[i];
            if (coin == state.OpeningCoin || state.WaitingForOthers.ContainsKey(coin))
            {
                continue;
            }

            setPassive(coin, false);
        }
    }

    public static void LockCoinUntilOthersMoved(TeamRoundState state, CoinIdentity coin, Action<CoinIdentity, bool> setPassive)
    {
        state.WaitingForOthers[coin] = new HashSet<CoinIdentity>();
        setPassive(coin, true);
    }

    public static void UnlockCoin(TeamRoundState state, CoinIdentity coin, Action<CoinIdentity, bool> setPassive)
    {
        state.WaitingForOthers.Remove(coin);
        setPassive(coin, false);
    }

    public static void RegisterSuccessfulShot(
        TeamRoundState state,
        CoinIdentity movedCoin,
        Action<CoinIdentity, bool> setPassive)
    {
        for (int i = 0; i < state.Coins.Count; i++)
        {
            CoinIdentity waitingCoin = state.Coins[i];
            if (waitingCoin == movedCoin || waitingCoin.IsPassive)
            {
                continue;
            }

            if (!state.WaitingForOthers.TryGetValue(waitingCoin, out HashSet<CoinIdentity> movedOthers))
            {
                continue;
            }

            movedOthers.Add(movedCoin);
            if (movedOthers.Count >= 2)
            {
                state.WaitingForOthers.Remove(waitingCoin);
                setPassive(waitingCoin, false);
            }
        }
    }

    public static bool ValidatePassBetween(
        TeamRoundState state,
        CoinIdentity movingCoin,
        IReadOnlyList<Vector3> pathSamples,
        float gateMargin)
    {
        if (!TryGetGateCoins(state, movingCoin, out CoinIdentity gateA, out CoinIdentity gateB))
        {
            return false;
        }

        return PassBetweenValidator.DidPassBetweenAlongPath(
            pathSamples,
            gateA.transform.position,
            gateB.transform.position,
            gateMargin);
    }

    public static bool TryGetGateCoins(
        TeamRoundState state,
        CoinIdentity movingCoin,
        out CoinIdentity gateA,
        out CoinIdentity gateB)
    {
        gateA = null;
        gateB = null;

        int found = 0;
        for (int i = 0; i < state.Coins.Count; i++)
        {
            CoinIdentity coin = state.Coins[i];
            if (coin == movingCoin)
            {
                continue;
            }

            if (found == 0)
            {
                gateA = coin;
                found++;
            }
            else
            {
                gateB = coin;
                return true;
            }
        }

        return false;
    }

    static int CompareCoinHorizontalPosition(CoinIdentity a, CoinIdentity b)
    {
        float delta = a.transform.position.x - b.transform.position.x;
        if (Mathf.Abs(delta) > 0.001f)
        {
            return delta < 0f ? -1 : 1;
        }

        delta = a.transform.position.z - b.transform.position.z;
        return delta < 0f ? -1 : delta > 0f ? 1 : 0;
    }
}

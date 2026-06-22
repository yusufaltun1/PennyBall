using System.Collections.Generic;

/// <summary>
/// Bir takımın tur içi durumu. Oyuncu ve bot aynı modeli kullanır.
/// </summary>
public sealed class TeamRoundState
{
    public readonly List<CoinIdentity> Coins = new(3);
    public readonly Dictionary<CoinIdentity, HashSet<CoinIdentity>> WaitingForOthers = new();

    public CoinIdentity OpeningCoin;
    public bool IsFirstMove = true;
}

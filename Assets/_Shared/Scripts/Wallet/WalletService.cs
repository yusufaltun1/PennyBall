using System;

public static class WalletService
{
    // Maç başına sabit ödüller
    public const int CoinsWin  = 40;
    public const int CoinsDraw = 20;
    public const int CoinsLoss = 0;
    public const int XpPerMatch = 10;

    static WalletData _data;

    public static event Action Changed;
    public static event Action<int, int> LevelChanged;

    public static WalletData Data
    {
        get
        {
            if (_data == null) _data = WalletRepository.Load();
            return _data;
        }
    }

    public static int TotalCoins => Data.totalCoins;
    public static int TotalXp    => Data.totalXp;
    public static int Level      => PlayerLevelProgression.GetLevelFromTotalXp(TotalXp);
    public static int XpInCurrentLevel => PlayerLevelProgression.GetXpInCurrentLevel(TotalXp);
    public static int XpToNextLevel    => PlayerLevelProgression.GetXpRequiredForNextLevel(Level);
    public static float LevelProgress  => PlayerLevelProgression.GetProgressInCurrentLevel(TotalXp);
    public static bool IsMaxLevel      => Level >= PlayerLevelProgression.MaxLevel;

    public static (int coins, int xp) GetReward(MatchResultType result)
    {
        int coins = result switch
        {
            MatchResultType.Win  => CoinsWin,
            MatchResultType.Draw => CoinsDraw,
            _                   => CoinsLoss
        };
        return (coins, XpPerMatch);
    }

    public static void AddReward(int coins, int xp)
    {
        int levelBefore = Level;

        Data.totalCoins += coins;
        Data.totalXp    += xp;
        WalletRepository.Save(Data);
        Changed?.Invoke();

        int levelAfter = Level;
        if (levelAfter > levelBefore)
            LevelChanged?.Invoke(levelBefore, levelAfter);
    }
}

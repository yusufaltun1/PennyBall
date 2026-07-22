using System;
using UnityEngine;

public static class WalletService
{
    // Maç başına sabit ödüller
    public const int CoinsWin  = 40;
    public const int CoinsDraw = 20;
    public const int CoinsLoss = 10;
    public const int XpPerMatch = 10;
    public const int LeaguePromotionCoins = 100;
    public const int LeaguePromotionXp = 50;

    static WalletData _data;

    public static event Action Changed;
    public static event Action<int, int> LevelChanged;

    // Domain Reload kapalıyken bile Play'e her girişte diskteki kaydı yeniden yükle.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState()
    {
        _data = null;
        Changed = null;
        LevelChanged = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ReloadBeforePlay()
    {
        _data = WalletRepository.Load();
    }

    public static void InvalidateCache()
    {
        _data = null;
    }

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

    public static bool TrySpendCoins(int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        if (Data.totalCoins < amount)
        {
            return false;
        }

        Data.totalCoins -= amount;
        WalletRepository.Save(Data);
        Changed?.Invoke();
        return true;
    }

    public static bool HasEnoughCoins(int amount)
    {
        return Data.totalCoins >= amount;
    }

    public static void SetTotals(int totalCoins, int totalXp)
    {
        int levelBefore = Level;

        Data.totalCoins = UnityEngine.Mathf.Max(0, totalCoins);
        Data.totalXp = UnityEngine.Mathf.Max(0, totalXp);
        WalletRepository.Save(Data);
        Changed?.Invoke();

        int levelAfter = Level;
        if (levelAfter != levelBefore)
        {
            LevelChanged?.Invoke(levelBefore, levelAfter);
        }
    }
}

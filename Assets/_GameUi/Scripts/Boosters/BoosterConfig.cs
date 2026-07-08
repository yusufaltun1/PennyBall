public static class BoosterConfig
{
    public const int UseCostCoins = 200;

    public const int FreezeUnlockLevel = 4;
    public const int TimeUnlockLevel = 7;
    public const int GoalKeeperUnlockLevel = 14;
    public const int LastCoinUnlockLevel = 24;

    public static int GetUnlockLevel(BoosterType type)
    {
        return type switch
        {
            BoosterType.Freeze => FreezeUnlockLevel,
            BoosterType.Time => TimeUnlockLevel,
            BoosterType.GoalKeeper => GoalKeeperUnlockLevel,
            BoosterType.LastCoin => LastCoinUnlockLevel,
            _ => int.MaxValue,
        };
    }

    public static bool IsUnlockedAtLevel(BoosterType type, int playerLevel)
    {
        return playerLevel >= GetUnlockLevel(type);
    }

    public static BoosterType? GetUnlockReachedOnLevelUp(int levelBefore, int levelAfter)
    {
        if (levelAfter <= levelBefore)
        {
            return null;
        }

        if (levelBefore < FreezeUnlockLevel && levelAfter >= FreezeUnlockLevel)
        {
            return BoosterType.Freeze;
        }

        if (levelBefore < TimeUnlockLevel && levelAfter >= TimeUnlockLevel)
        {
            return BoosterType.Time;
        }

        if (levelBefore < GoalKeeperUnlockLevel && levelAfter >= GoalKeeperUnlockLevel)
        {
            return BoosterType.GoalKeeper;
        }

        if (levelBefore < LastCoinUnlockLevel && levelAfter >= LastCoinUnlockLevel)
        {
            return BoosterType.LastCoin;
        }

        return null;
    }
}

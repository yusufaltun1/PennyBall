public static class MatchSessionContext
{
    static BotPlayerEntry _currentOpponent;

    public static BotPlayerEntry CurrentOpponent => _currentOpponent;
    public static bool HasOpponent => _currentOpponent != null;

    public static int RankBefore  { get; private set; } = -1;
    public static int RankAfter   { get; private set; } = -1;
    public static int EarnedCoins { get; private set; } = 0;
    public static int EarnedXp    { get; private set; } = 0;
    public static int LevelBefore { get; private set; } = 1;
    public static int LevelAfter  { get; private set; } = 1;
    public static bool LeveledUp  => LevelAfter > LevelBefore;

    public static void SetOpponent(BotPlayerEntry opponent)
    {
        _currentOpponent = opponent;
    }

    public static void SetRankBefore(int rank)  => RankBefore  = rank;
    public static void SetRankAfter(int rank)   => RankAfter   = rank;
    public static void SetEarnedRewards(int coins, int xp, int levelBefore, int levelAfter)
    {
        EarnedCoins = coins;
        EarnedXp = xp;
        LevelBefore = levelBefore;
        LevelAfter = levelAfter;
    }

    public static void Clear()
    {
        _currentOpponent = null;
        RankBefore  = -1;
        RankAfter   = -1;
        EarnedCoins = 0;
        EarnedXp    = 0;
        LevelBefore = 1;
        LevelAfter  = 1;
    }
}

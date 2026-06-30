public static class MatchSessionContext
{
    static BotPlayerEntry _currentOpponent;

    public static BotPlayerEntry CurrentOpponent => _currentOpponent;
    public static bool HasOpponent => _currentOpponent != null;

    public static int RankBefore { get; private set; } = -1;
    public static int RankAfter  { get; private set; } = -1;

    public static void SetOpponent(BotPlayerEntry opponent)
    {
        _currentOpponent = opponent;
    }

    public static void SetRankBefore(int rank) => RankBefore = rank;
    public static void SetRankAfter(int rank)  => RankAfter  = rank;

    public static void Clear()
    {
        _currentOpponent = null;
        RankBefore = -1;
        RankAfter  = -1;
    }
}

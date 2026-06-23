public static class MatchSessionContext
{
    static BotPlayerEntry _currentOpponent;

    public static BotPlayerEntry CurrentOpponent => _currentOpponent;
    public static bool HasOpponent => _currentOpponent != null;

    public static void SetOpponent(BotPlayerEntry opponent)
    {
        _currentOpponent = opponent;
    }

    public static void Clear()
    {
        _currentOpponent = null;
    }
}

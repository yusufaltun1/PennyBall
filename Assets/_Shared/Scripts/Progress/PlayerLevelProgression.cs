public static class PlayerLevelProgression
{
    public const int MaxLevel = 100;

    // Seviye 1→2, 2→3, 3→4, 4→5 için gereken XP
    static readonly int[] EarlyLevelXp = { 30, 35, 40, 45 };

    public static int GetXpRequiredForNextLevel(int currentLevel)
    {
        if (currentLevel >= MaxLevel)
            return 0;

        if (currentLevel <= EarlyLevelXp.Length)
            return EarlyLevelXp[currentLevel - 1];

        if (currentLevel < 15)
            return 50 + (currentLevel - 5) * 5;

        if (currentLevel < 35)
            return 100 + (currentLevel - 15) * 8;

        if (currentLevel < 60)
            return 250 + (currentLevel - 35) * 12;

        if (currentLevel < 85)
            return 520 + (currentLevel - 60) * 18;

        return 950 + (currentLevel - 85) * 35;
    }

    public static int GetLevelFromTotalXp(int totalXp)
    {
        int level = 1;
        int remaining = totalXp;

        while (level < MaxLevel)
        {
            int required = GetXpRequiredForNextLevel(level);
            if (remaining < required)
                break;

            remaining -= required;
            level++;
        }

        return level;
    }

    public static int GetXpInCurrentLevel(int totalXp)
    {
        int remaining = totalXp;

        for (int level = 1; level < MaxLevel; level++)
        {
            int required = GetXpRequiredForNextLevel(level);
            if (remaining < required)
                return remaining;

            remaining -= required;
        }

        return 0;
    }

    public static float GetProgressInCurrentLevel(int totalXp)
    {
        int level = GetLevelFromTotalXp(totalXp);
        if (level >= MaxLevel)
            return 1f;

        int required = GetXpRequiredForNextLevel(level);
        if (required <= 0)
            return 1f;

        return (float)GetXpInCurrentLevel(totalXp) / required;
    }

    public static int GetTotalXpForLevel(int level)
    {
        if (level <= 1)
            return 0;

        level = UnityEngine.Mathf.Clamp(level, 1, MaxLevel);
        int total = 0;

        for (int l = 1; l < level; l++)
            total += GetXpRequiredForNextLevel(l);

        return total;
    }
}

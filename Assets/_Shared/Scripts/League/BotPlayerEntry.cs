using System;

[Serializable]
public class BotPlayerEntry
{
    public int id;
    public string displayName;
    public string countryCode;
    public int homeLeague;
    public int avatarIndex;
    public int difficultyLevel;
}

[Serializable]
public class BotPlayersDatabase
{
    public int version;
    public int totalBots;
    public BotPlayerEntry[] bots;
}

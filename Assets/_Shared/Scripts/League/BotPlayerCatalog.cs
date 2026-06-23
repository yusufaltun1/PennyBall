using System.Collections.Generic;
using UnityEngine;

public static class BotPlayerCatalog
{
    static BotPlayersDatabase _database;
    static readonly Dictionary<int, BotPlayerEntry> _byId = new();
    static readonly Dictionary<int, List<BotPlayerEntry>> _byLeague = new();

    public static bool IsLoaded => _database != null && _database.bots != null;

    public static void Load()
    {
        if (IsLoaded)
        {
            return;
        }

        TextAsset json = Resources.Load<TextAsset>(LeagueConfig.BotDatabaseResourcePath);
        if (json == null)
        {
            Debug.LogError($"[League] Bot database not found at Resources/{LeagueConfig.BotDatabaseResourcePath}");
            return;
        }

        _database = JsonUtility.FromJson<BotPlayersDatabase>(json.text);
        _byId.Clear();
        _byLeague.Clear();

        for (int i = 0; i < _database.bots.Length; i++)
        {
            BotPlayerEntry bot = _database.bots[i];
            _byId[bot.id] = bot;

            if (!_byLeague.TryGetValue(bot.homeLeague, out List<BotPlayerEntry> list))
            {
                list = new List<BotPlayerEntry>();
                _byLeague[bot.homeLeague] = list;
            }

            list.Add(bot);
        }
    }

    public static bool TryGetById(int botId, out BotPlayerEntry bot)
    {
        Load();
        return _byId.TryGetValue(botId, out bot);
    }

    public static IReadOnlyList<BotPlayerEntry> GetBotsForLeague(int league)
    {
        Load();
        if (_byLeague.TryGetValue(league, out List<BotPlayerEntry> list))
        {
            return list;
        }

        return System.Array.Empty<BotPlayerEntry>();
    }
}

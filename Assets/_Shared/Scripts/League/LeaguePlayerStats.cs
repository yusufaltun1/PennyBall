public static class LeaguePlayerStats
{
    public static int GetPlayerPoints()
    {
        LeagueSaveData save = LeagueService.Instance != null
            ? LeagueService.Instance.Save
            : LeagueRepository.Load();

        return GetPlayerPoints(save);
    }

    public static int GetPlayerPoints(LeagueSaveData save)
    {
        if (save?.standings == null)
        {
            return 0;
        }

        for (int i = 0; i < save.standings.Length; i++)
        {
            if (save.standings[i].isPlayer)
            {
                return save.standings[i].points;
            }
        }

        return 0;
    }
}

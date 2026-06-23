using UnityEditor;
using UnityEngine;

public static class LeagueDebugMenu
{
    [MenuItem("PennyBall/League/Reset League Save")]
    public static void ResetLeagueSave()
    {
        LeagueRepository.Delete();
        Debug.Log("[League] Save silindi. Oyunu yeniden başlat.");
    }

    [MenuItem("PennyBall/League/Log Current Standings")]
    public static void LogStandings()
    {
        LeagueSaveData save = LeagueRepository.Load();
        if (save == null)
        {
            Debug.Log("[League] Kayıt yok.");
            return;
        }

        LeagueStandingsLogger.LogLeagueStandings(save);
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;

public static class MatchLauncher
{
    public static void StartLeagueMatch()
    {
        if (LeagueService.Instance == null)
        {
            Debug.LogError("[Match] LeagueService bulunamadı.");
            return;
        }

        BotPlayerEntry opponent = LeagueService.Instance.PickOpponentForNextMatch();
        if (opponent == null)
        {
            Debug.LogError("[Match] Lig havuzundan rakip seçilemedi.");
            return;
        }

        MatchSessionContext.SetOpponent(opponent);
        MatchSessionLogger.LogMatchedOpponent(opponent, LeagueService.Instance.PlayerLeague);
        SceneManager.LoadScene(GameSceneNames.Game);
    }
}

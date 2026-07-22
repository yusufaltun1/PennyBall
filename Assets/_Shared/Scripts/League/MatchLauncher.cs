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

        if (MatchSessionContext.HasOpponent)
        {
            MatchSessionLogger.LogMatchedOpponent(
                MatchSessionContext.CurrentOpponent,
                LeagueService.Instance.PlayerLeague);
            SceneManager.LoadScene(GameSceneNames.Game);
            return;
        }

        BotPlayerEntry opponent = LeagueService.Instance.PickOpponentForNextMatch();
        if (opponent == null)
        {
            Debug.LogError("[Match] Lig havuzundan rakip seçilemedi.");
            return;
        }

        MatchSessionContext.SetOpponent(opponent);
        MatchSessionContext.SetOnlineMatch(false, null, null);
        MatchSessionLogger.LogMatchedOpponent(opponent, LeagueService.Instance.PlayerLeague);
        SceneManager.LoadScene(GameSceneNames.Game);
    }
}

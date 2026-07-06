using UnityEngine;

public static class GameProgressReset
{
    /// <param name="keepFeedbackSettings">
    /// true ise müzik/ses/titreşim ayarları korunur (Settings version debug gate için).
    /// </param>
    public static void ResetAll(bool keepFeedbackSettings = true)
    {
        OnboardingProgress.ResetAll();

        WalletRepository.Delete();
        WalletService.ReloadAfterProgressReset();

        LeagueRepository.Delete();
        if (LeagueService.Instance != null)
        {
            LeagueService.Instance.Initialize();
        }

        MatchSessionContext.Clear();
        MatchSessionTracker.ClearPersistedActiveMatch();
        MatchAdTracker.Reset();

        if (!keepFeedbackSettings)
        {
            GameFeedbackSettingsRepository.Delete();
            GameFeedbackSettingsService.Reload();
        }

        PlayerPrefs.Save();
        Debug.Log("[Progress] Tüm oyun kaydı sıfırlandı.");
    }
}

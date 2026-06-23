using UnityEngine;

public static class OnboardingProgress
{
    const string CompletedKey = "pennyball.onboarding.completed";
    const string PlayHighlightPendingKey = "pennyball.onboarding.play_highlight_pending";

    public static bool IsCompleted => PlayerPrefs.GetInt(CompletedKey, 0) == 1;

    public static bool IsPlayHighlightPending => PlayerPrefs.GetInt(PlayHighlightPendingKey, 0) == 1;

    public static void MarkCompleted()
    {
        PlayerPrefs.SetInt(CompletedKey, 1);
        PlayerPrefs.SetInt(PlayHighlightPendingKey, 1);
        PlayerPrefs.Save();
    }

    public static void MarkPlayHighlightShown()
    {
        PlayerPrefs.SetInt(PlayHighlightPendingKey, 0);
        PlayerPrefs.Save();
    }

    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(CompletedKey);
        PlayerPrefs.DeleteKey(PlayHighlightPendingKey);
        PlayerPrefs.Save();
    }
}

using UnityEngine;

/// <summary>
/// İlk açılış sırası: Splash → HowToPlay → Onboarding → MainMenu.
/// Sahne kararları tek yerden verilir.
/// </summary>
public static class FirstRunFlow
{
    public static string GetSceneAfterSplash()
    {
        return GetPendingFirstRunScene() ?? GameSceneNames.MainMenu;
    }

    public static string GetSceneAfterHowToPlay()
    {
        return OnboardingProgress.IsCompleted
            ? GameSceneNames.MainMenu
            : OnboardingSceneNames.Onboarding;
    }

    /// <summary>İlk açılışta tamamlanmamış bir adım yoksa null döner.</summary>
    public static string GetPendingFirstRunScene()
    {
        // HowToPlay sahnesi Build Settings'e eklenene kadar bu adım atlanır.
        if (!HowToPlayProgress.IsSeen && Application.CanStreamedLevelBeLoaded(GameSceneNames.HowToPlay))
        {
            return GameSceneNames.HowToPlay;
        }

        if (!OnboardingProgress.IsCompleted)
        {
            return OnboardingSceneNames.Onboarding;
        }

        return null;
    }
}

using UnityEngine.SceneManagement;

public static class ExerciseRuntime
{
    public const int OnboardingBotStrength = 7;
    public const float OnboardingBotThinkDelaySeconds = 2.5f;

    static bool _launchedFromOnboarding;

    public static bool IsActive =>
        SceneManager.GetActiveScene().isLoaded
        && SceneManager.GetActiveScene().name == GameSceneNames.Exercise;

    public static void MarkLaunchedFromOnboarding()
    {
        _launchedFromOnboarding = true;
    }

    public static bool ConsumeOnboardingBotProfile()
    {
        if (!_launchedFromOnboarding)
        {
            return false;
        }

        _launchedFromOnboarding = false;
        return true;
    }
}

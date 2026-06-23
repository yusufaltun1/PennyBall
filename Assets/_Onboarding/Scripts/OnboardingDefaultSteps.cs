public static class OnboardingDefaultSteps
{
    public const int Count = 4;

    public static OnboardingStepDefinition[] Create()
    {
        return new[]
        {
            new OnboardingStepDefinition
            {
                stepType = OnboardingStepType.GuidedShot,
                activeCoinIndex = 1,
                targetDirectionYawOffsetDegrees = -22f,
                targetPullDistance = 0.14f,
                directionToleranceDegrees = 22f,
                pullTolerance = 0.06f,
                instructionText = "Ortadaki coini hedefe doğru çek ve bırak."
            },
            new OnboardingStepDefinition
            {
                stepType = OnboardingStepType.PassBetween,
                activeCoinIndex = 0,
                gateCoinAIndex = 1,
                gateCoinBIndex = 2,
                targetDirectionYawOffsetDegrees = 20f,
                targetPullDistance = 0.22f,
                directionToleranceDegrees = 18f,
                pullTolerance = 0.06f,
                instructionText = "Sol coin ile diğer iki coin arasından geç."
            },
            new OnboardingStepDefinition
            {
                stepType = OnboardingStepType.ScoreGoal,
                activeCoinIndex = 2,
                targetDirectionYawOffsetDegrees = -14f,
                targetPullDistance = 0.31f,
                directionToleranceDegrees = 20f,
                pullTolerance = 0.05f,
                isFinalStep = false,
                instructionText = "Üçüncü coin ile kaleye gol at."
            },
            new OnboardingStepDefinition
            {
                stepType = OnboardingStepType.ScoreGoal,
                activeCoinIndex = 0,
                targetPullDistance = 0.35f,
                directionToleranceDegrees = 18f,
                pullTolerance = 0.04f,
                isFinalStep = true,
                instructionText = "Birinci coin ile tam güçte kaleye gol at."
            }
        };
    }
}

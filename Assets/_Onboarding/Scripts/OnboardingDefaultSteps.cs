public static class OnboardingDefaultSteps
{
    public const int Count = 4;
    public const int FirstCoinIndex = 0;
    public const int SecondCoinIndex = 1;
    public const int ThirdCoinIndex = 2;

    public static OnboardingStepDefinition[] Create()
    {
        return new[]
        {
            new OnboardingStepDefinition
            {
                stepType = OnboardingStepType.GuidedShot,
                activeCoinIndex = SecondCoinIndex,
                targetDirectionYawOffsetDegrees = -22f,
                targetPullDistance = 0.18f,
                directionToleranceDegrees = 8f,
                pullTolerance = 0.018f,
                instructionText = "Ortadaki coini hedefe doğru çek ve bırak."
            },
            new OnboardingStepDefinition
            {
                stepType = OnboardingStepType.PassBetween,
                activeCoinIndex = FirstCoinIndex,
                gateCoinAIndex = SecondCoinIndex,
                gateCoinBIndex = ThirdCoinIndex,
                targetDirectionYawOffsetDegrees = 20f,
                targetPullDistance = 0.22f,
                directionToleranceDegrees = 18f,
                pullTolerance = 0.06f,
                instructionText = "Sol coin ile diğer iki coin arasından geç."
            },
            new OnboardingStepDefinition
            {
                stepType = OnboardingStepType.ScoreGoal,
                activeCoinIndex = ThirdCoinIndex,
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
                activeCoinIndex = SecondCoinIndex,
                targetDirectionYawOffsetDegrees = -35f,
                targetPullDistance = 0.35f,
                directionToleranceDegrees = 18f,
                pullTolerance = 0.04f,
                isFinalStep = true,
                instructionText = "Ortadaki coin ile tam güçte kaleye gol at."
            }
        };
    }
}

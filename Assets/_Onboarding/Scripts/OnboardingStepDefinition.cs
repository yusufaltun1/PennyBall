using System;
using UnityEngine;

public enum OnboardingStepType
{
    GuidedShot,
    PassBetween,
    ScoreGoal
}

[Serializable]
public class OnboardingStepDefinition
{
    public OnboardingStepType stepType = OnboardingStepType.GuidedShot;
    [Tooltip("0 = P1, 1 = P2, 2 = P3")]
    public int activeCoinIndex;
    [Tooltip("PassBetween adımı için kapı coin A")]
    public int gateCoinAIndex = 0;
    [Tooltip("PassBetween adımı için kapı coin B")]
    public int gateCoinBIndex = 2;
    public Vector3 targetLaunchDirection = Vector3.forward;
    [Tooltip("Kale yönüne göre sola (-) / sağa (+) derece kaydırma")]
    public float targetDirectionYawOffsetDegrees;
    public float targetPullDistance = 0.2f;
    public float directionToleranceDegrees = 18f;
    public float pullTolerance = 0.06f;
    [Tooltip("Bu adım tamamlanınca onboarding biter")]
    public bool isFinalStep;
    [TextArea] public string instructionText;
}

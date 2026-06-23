using UnityEngine;

public struct OnboardingAimFeedback
{
    public bool HasAim;
    public bool DirectionValid;
    public bool PowerValid;

    public bool IsFullyValid => HasAim && DirectionValid && PowerValid;
}

public static class OnboardingAimValidator
{
    public static OnboardingAimFeedback Evaluate(
        Vector3 launchDirection,
        float pullDistance,
        Vector3 targetDirection,
        float targetPullDistance,
        float directionToleranceDegrees,
        float pullTolerance)
    {
        OnboardingAimFeedback feedback = new()
        {
            HasAim = true
        };

        launchDirection.y = 0f;
        targetDirection.y = 0f;

        if (launchDirection.sqrMagnitude < 0.0001f || targetDirection.sqrMagnitude < 0.0001f)
        {
            return feedback;
        }

        launchDirection.Normalize();
        targetDirection.Normalize();

        float angle = Vector3.Angle(launchDirection, targetDirection);
        feedback.DirectionValid = angle <= directionToleranceDegrees;
        feedback.PowerValid = Mathf.Abs(pullDistance - targetPullDistance) <= pullTolerance;
        return feedback;
    }

    public static bool IsAimValid(
        Vector3 launchDirection,
        float pullDistance,
        Vector3 targetDirection,
        float targetPullDistance,
        float directionToleranceDegrees,
        float pullTolerance)
    {
        return Evaluate(
            launchDirection,
            pullDistance,
            targetDirection,
            targetPullDistance,
            directionToleranceDegrees,
            pullTolerance).IsFullyValid;
    }
}

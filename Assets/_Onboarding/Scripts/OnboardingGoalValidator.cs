using System.Collections.Generic;
using UnityEngine;

public static class OnboardingGoalValidator
{
    public static bool DidPathEnterGoal(IReadOnlyList<Vector3> pathPoints, Collider goalCollider, float margin = 0.22f)
    {
        if (goalCollider == null || pathPoints == null || pathPoints.Count == 0)
        {
            return false;
        }

        Bounds bounds = goalCollider.bounds;
        bounds.Expand(margin);

        for (int i = 0; i < pathPoints.Count; i++)
        {
            if (IsPointInsideGoal(pathPoints[i], goalCollider, bounds))
            {
                return true;
            }
        }

        for (int i = 1; i < pathPoints.Count; i++)
        {
            Vector3 start = pathPoints[i - 1];
            Vector3 end = pathPoints[i];
            Vector3 mid = (start + end) * 0.5f;

            if (IsPointInsideGoal(start, goalCollider, bounds)
                || IsPointInsideGoal(end, goalCollider, bounds)
                || IsPointInsideGoal(mid, goalCollider, bounds))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsCoinInsideGoal(Vector3 worldPosition, Collider goalCollider, float margin = 0.22f)
    {
        if (goalCollider == null)
        {
            return false;
        }

        Bounds bounds = goalCollider.bounds;
        bounds.Expand(margin);
        return IsPointInsideGoal(worldPosition, goalCollider, bounds);
    }

    static bool IsPointInsideGoal(Vector3 worldPosition, Collider goalCollider, Bounds expandedBounds)
    {
        if (expandedBounds.Contains(worldPosition))
        {
            return true;
        }

        Vector3 closestPoint = goalCollider.ClosestPoint(worldPosition);
        closestPoint.y = worldPosition.y;
        return (worldPosition - closestPoint).sqrMagnitude <= 0.035f;
    }
}

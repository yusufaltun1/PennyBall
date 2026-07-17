using UnityEngine;

public static class OnboardingGoalCenter
{
    public static bool TryGetEnemyGoalCenter(out Vector3 center)
    {
        GoalZone zone = GoalZone.FindOpponentGoalArea();
        if (zone != null)
        {
            center = zone.transform.position;
            return true;
        }

        center = default;
        return false;
    }

    /// <summary>
    /// Kale ağzının içindeki gol bölgesi merkezi (EnemyGoalArea collider ortası).
    /// </summary>
    public static bool TryGetEnemyGoalInteriorPoint(
        Transform kaleFallback,
        Vector3 fromPosition,
        float extraInset,
        out Vector3 interiorPoint)
    {
        interiorPoint = default;

        GoalZone zone = GoalZone.FindOpponentGoalArea();
        if (zone != null)
        {
            BoxCollider goalBox = zone.GetComponent<BoxCollider>();
            if (goalBox != null)
            {
                interiorPoint = goalBox.bounds.center;
            }
            else
            {
                interiorPoint = zone.transform.position;
            }

            if (extraInset > 0f)
            {
                Vector3 shotDirection = GetPlanarDirection(fromPosition, interiorPoint);
                interiorPoint += shotDirection * extraInset;
            }

            interiorPoint.y = fromPosition.y;
            return true;
        }

        if (kaleFallback == null)
        {
            return false;
        }

        interiorPoint = kaleFallback.position;
        interiorPoint.y = fromPosition.y;
        return true;
    }

    public static Vector3 ResolveEnemyGoalCenter(Transform kaleFallback)
    {
        if (TryGetEnemyGoalCenter(out Vector3 goalCenter))
        {
            return goalCenter;
        }

        if (kaleFallback == null)
        {
            return Vector3.zero;
        }

        Renderer[] renderers = kaleFallback.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds.center;
        }

        return kaleFallback.position;
    }

    static Vector3 GetPlanarDirection(Vector3 fromPosition, Vector3 toPosition)
    {
        Vector3 direction = toPosition - fromPosition;
        direction.y = 0f;
        return direction.sqrMagnitude < 0.0001f ? Vector3.forward : direction.normalized;
    }
}

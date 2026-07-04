using UnityEngine;

public static class OnboardingGoalCenter
{
    public static bool TryGetEnemyGoalCenter(out Vector3 center)
    {
        GoalZone[] zones = Object.FindObjectsByType<GoalZone>(FindObjectsSortMode.None);
        for (int i = 0; i < zones.Length; i++)
        {
            Transform parent = zones[i].transform.parent;
            if (parent != null && parent.name.Contains("_E"))
            {
                center = zones[i].transform.position;
                return true;
            }
        }

        center = default;
        return false;
    }

    /// <summary>
    /// Kale ağzının içindeki gol bölgesi merkezi (GoalTrigger collider ortası).
    /// </summary>
    public static bool TryGetEnemyGoalInteriorPoint(
        Transform kaleFallback,
        Vector3 fromPosition,
        float extraInset,
        out Vector3 interiorPoint)
    {
        interiorPoint = default;
        if (kaleFallback == null)
        {
            return false;
        }

        Transform goalTrigger = kaleFallback.Find("GoalTrigger");
        if (goalTrigger == null)
        {
            return false;
        }

        BoxCollider goalBox = goalTrigger.GetComponent<BoxCollider>();
        if (goalBox == null)
        {
            interiorPoint = goalTrigger.position;
            interiorPoint.y = fromPosition.y;
            return true;
        }

        Bounds bounds = goalBox.bounds;
        interiorPoint = bounds.center;

        if (extraInset > 0f)
        {
            Vector3 shotDirection = GetPlanarDirection(fromPosition, interiorPoint);
            interiorPoint += shotDirection * extraInset;
        }

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

        Transform goalTrigger = kaleFallback.Find("GoalTrigger");
        if (goalTrigger != null)
        {
            BoxCollider goalBox = goalTrigger.GetComponent<BoxCollider>();
            if (goalBox != null)
            {
                return goalBox.bounds.center;
            }

            return goalTrigger.position;
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

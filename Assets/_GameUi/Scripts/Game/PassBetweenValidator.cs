using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kapı kuralı: atılan para, diğer iki parayı birleştiren çizgi segmentinin
/// bir yüzünden diğer yüzüne geçmek zorundadır.
/// </summary>
public static class PassBetweenValidator
{
    public static bool DidPassBetweenAlongPath(
        IReadOnlyList<Vector3> pathPoints,
        Vector3 gateA,
        Vector3 gateB,
        float gateMargin)
    {
        if (pathPoints == null || pathPoints.Count < 2)
        {
            return false;
        }

        for (int i = 1; i < pathPoints.Count; i++)
        {
            if (DidPassBetween(pathPoints[i - 1], pathPoints[i], gateA, gateB, gateMargin))
            {
                return true;
            }
        }

        return false;
    }

    public static bool DidPassBetween(
        Vector3 pathStart,
        Vector3 pathEnd,
        Vector3 gateA,
        Vector3 gateB,
        float gateMargin)
    {
        Vector2 start = ToXZ(pathStart);
        Vector2 end = ToXZ(pathEnd);
        Vector2 a = ToXZ(gateA);
        Vector2 b = ToXZ(gateB);

        Vector2 ab = b - a;
        float abLength = ab.magnitude;
        if (abLength < 0.001f)
        {
            return false;
        }

        Vector2 path = end - start;
        if (path.sqrMagnitude < 1e-8f)
        {
            return false;
        }

        Vector2 abDirection = ab / abLength;
        Vector2 perpendicular = new Vector2(-abDirection.y, abDirection.x);

        float startDistance = Vector2.Dot(start - a, perpendicular);
        float endDistance = Vector2.Dot(end - a, perpendicular);

        // Zorunlu: çizginin bir tarafından diğer tarafına geçiş (işaret değişimi).
        // Aynı tarafta kalıp margin içine girmek geçiş sayılmaz.
        if (startDistance * endDistance >= 0f)
        {
            return false;
        }

        float denominator = Vector2.Dot(path, perpendicular);
        if (Mathf.Abs(denominator) < 1e-6f)
        {
            return false;
        }

        float t = Vector2.Dot(a - start, perpendicular) / denominator;
        if (t < 0f || t > 1f)
        {
            return false;
        }

        Vector2 intersection = start + path * t;
        float pad = Mathf.Max(0f, gateMargin);
        float projection = Vector2.Dot(intersection - a, abDirection);
        return projection >= -pad && projection <= abLength + pad;
    }

    static Vector2 ToXZ(Vector3 position)
    {
        return new Vector2(position.x, position.z);
    }
}

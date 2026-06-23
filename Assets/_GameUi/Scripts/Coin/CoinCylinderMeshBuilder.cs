using System.Collections.Generic;
using UnityEngine;

public static class CoinCylinderMeshBuilder
{
    public static Mesh Create(float radius, float height, int segments)
    {
        int sides = Mathf.Max(8, segments);
        float halfHeight = height * 0.5f;

        var vertices = new List<Vector3>(sides * 2 + 2);
        var triangles = new List<int>(sides * 12);

        vertices.Add(new Vector3(0f, -halfHeight, 0f));
        vertices.Add(new Vector3(0f, halfHeight, 0f));

        for (int i = 0; i < sides; i++)
        {
            float angle = i * Mathf.PI * 2f / sides;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            vertices.Add(new Vector3(x, -halfHeight, z));
            vertices.Add(new Vector3(x, halfHeight, z));
        }

        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            int bottomCurrent = 2 + i * 2;
            int bottomNext = 2 + next * 2;
            int topCurrent = bottomCurrent + 1;
            int topNext = bottomNext + 1;

            triangles.Add(0);
            triangles.Add(bottomNext);
            triangles.Add(bottomCurrent);

            triangles.Add(1);
            triangles.Add(topCurrent);
            triangles.Add(topNext);

            triangles.Add(bottomCurrent);
            triangles.Add(topCurrent);
            triangles.Add(bottomNext);

            triangles.Add(bottomNext);
            triangles.Add(topCurrent);
            triangles.Add(topNext);
        }

        var mesh = new Mesh { name = "CoinCylinderCollision" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}

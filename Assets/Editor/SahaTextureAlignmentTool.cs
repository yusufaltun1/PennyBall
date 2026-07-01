using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class SahaTextureAlignmentTool
{
    const string SahaPrefabPath = "Assets/Prefab/Saha.prefab";
    const string MaterialPath = "Assets/Materials/Saha_Texture.mat";

    [MenuItem("PennyBall/Align Saha Texture")]
    public static void AlignFromMenu()
    {
        AlignSahaTexture(logOnly: false);
    }

    public static void AlignSahaTexture(bool logOnly)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(SahaPrefabPath);
        if (prefabRoot == null)
        {
            Debug.LogError($"Saha prefab bulunamadi: {SahaPrefabPath}");
            return;
        }

        Transform geometry = prefabRoot.transform.Find("SahaGeometry");
        if (geometry == null)
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
            Debug.LogError("SahaGeometry bulunamadi.");
            return;
        }

        MeshFilter meshFilter = geometry.GetComponent<MeshFilter>();
        MeshRenderer meshRenderer = geometry.GetComponent<MeshRenderer>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
            Debug.LogError("SahaGeometry mesh bulunamadi.");
            return;
        }

        Mesh mesh = meshFilter.sharedMesh;
        Material targetMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (targetMaterial == null)
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
            Debug.LogError($"Material bulunamadi: {MaterialPath}");
            return;
        }

        int subMesh = FindTextureSubMesh(meshRenderer, targetMaterial);
        if (subMesh < 0)
        {
            subMesh = mesh.subMeshCount > 1 ? 1 : 0;
        }

        if (!TryGetSubMeshUvBounds(mesh, subMesh, out float minU, out float maxU, out float minV, out float maxV))
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
            Debug.LogError("Saha_Texture submesh UV verisi bulunamadi.");
            return;
        }

        float spanU = Mathf.Max(maxU - minU, 0.0001f);
        float spanV = Mathf.Max(maxV - minV, 0.0001f);
        Vector2 scale = new(1f / spanU, 1f / spanV);
        Vector2 offset = new(-minU / spanU, -minV / spanV);

        Texture2D texture = targetMaterial.GetTexture("_BaseMap") as Texture2D;
        float textureAspect = texture != null
            ? texture.width / (float)texture.height
            : 0f;

        Debug.Log(
            $"[SahaTexture] submesh={subMesh} UV span=({spanU:F4},{spanV:F4}) aspect={spanU / spanV:F4}");
        Debug.Log(
            $"[SahaTexture] texture={(texture != null ? texture.name : "null")} aspect={textureAspect:F4}");
        Debug.Log($"[SahaTexture] scale={scale} offset={offset}");

        if (logOnly)
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
            return;
        }

        targetMaterial.SetTextureScale("_BaseMap", scale);
        targetMaterial.SetTextureOffset("_BaseMap", offset);
        targetMaterial.SetTextureScale("_MainTex", scale);
        targetMaterial.SetTextureOffset("_MainTex", offset);

        EditorUtility.SetDirty(targetMaterial);
        AssetDatabase.SaveAssets();
        PrefabUtility.UnloadPrefabContents(prefabRoot);

        Debug.Log("[SahaTexture] Saha_Texture material hizalandi.");
    }

    static int FindTextureSubMesh(MeshRenderer renderer, Material targetMaterial)
    {
        Material[] materials = renderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] == targetMaterial)
            {
                return i;
            }
        }

        return -1;
    }

    static bool TryGetSubMeshUvBounds(
        Mesh mesh,
        int subMeshIndex,
        out float minU,
        out float maxU,
        out float minV,
        out float maxV)
    {
        minU = minV = float.MaxValue;
        maxU = maxV = float.MinValue;

        Vector2[] uvs = mesh.uv;
        if (uvs == null || uvs.Length == 0 || subMeshIndex < 0 || subMeshIndex >= mesh.subMeshCount)
        {
            return false;
        }

        SubMeshDescriptor descriptor = mesh.GetSubMesh(subMeshIndex);
        int end = descriptor.indexStart + descriptor.indexCount;
        int[] triangles = mesh.triangles;
        bool found = false;

        for (int i = descriptor.indexStart; i < end; i++)
        {
            int vertex = triangles[i];
            if (vertex < 0 || vertex >= uvs.Length)
            {
                continue;
            }

            Vector2 uv = uvs[vertex];
            minU = Mathf.Min(minU, uv.x);
            maxU = Mathf.Max(maxU, uv.x);
            minV = Mathf.Min(minV, uv.y);
            maxV = Mathf.Max(maxV, uv.y);
            found = true;
        }

        return found;
    }
}

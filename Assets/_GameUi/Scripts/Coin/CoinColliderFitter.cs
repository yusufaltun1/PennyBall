using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
[ExecuteAlways]
public class CoinColliderFitter : MonoBehaviour
{
    const string PhysicsChildName = "Coin_Physics";

    [SerializeField] float _radiusScale = 0.98f;
    [SerializeField] float _heightScale = 1f;
    [SerializeField] int _cylinderSegments = 24;

    Mesh _generatedMesh;

    void Awake()
    {
        RebuildCollider();
    }

    void OnValidate()
    {
        RebuildCollider();
    }

    void OnDestroy()
    {
        CleanupGeneratedMesh();
    }

    public void RebuildCollider()
    {
        Renderer visualRenderer = GetComponentInChildren<Renderer>();
        if (visualRenderer == null || visualRenderer.gameObject.name == PhysicsChildName)
        {
            visualRenderer = FindVisualRenderer();
        }

        if (visualRenderer == null)
        {
            return;
        }

        RemoveCollidersFromVisual(visualRenderer.transform);

        Transform physicsTransform = GetOrCreatePhysicsChild(visualRenderer.transform);
        FitPhysicsChild(visualRenderer, physicsTransform);
    }

    Renderer FindVisualRenderer()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].gameObject.name != PhysicsChildName)
            {
                return renderers[i];
            }
        }

        return null;
    }

    void RemoveCollidersFromVisual(Transform visualTransform)
    {
        Collider[] colliders = visualTransform.GetComponents<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            DestroyObject(colliders[i]);
        }
    }

    Transform GetOrCreatePhysicsChild(Transform visualTransform)
    {
        Transform existing = visualTransform.parent.Find(PhysicsChildName);
        if (existing != null)
        {
            return existing;
        }

        var physicsObject = new GameObject(PhysicsChildName);
        physicsObject.transform.SetParent(visualTransform.parent, false);
        return physicsObject.transform;
    }

    void FitPhysicsChild(Renderer visualRenderer, Transform physicsTransform)
    {
        Transform coinRoot = visualRenderer.transform.parent;
        Bounds worldBounds = visualRenderer.bounds;
        Vector3 worldSize = worldBounds.size;
        int thinAxis = GetThinAxisIndex(worldSize);

        float worldRadius = GetRadius(worldSize, thinAxis) * _radiusScale;
        float worldHeight = worldSize[thinAxis] * _heightScale;
        float parentScale = Mathf.Max(Mathf.Abs(coinRoot.lossyScale.x), 0.0001f);
        float localRadius = worldRadius / parentScale;
        float localHeight = worldHeight / parentScale;

        physicsTransform.SetPositionAndRotation(worldBounds.center, GetWorldCylinderRotation(thinAxis));
        physicsTransform.localScale = Vector3.one;

        CleanupGeneratedMesh();

        MeshFilter meshFilter = GetOrAddComponent<MeshFilter>(physicsTransform.gameObject);
        MeshCollider meshCollider = GetOrAddComponent<MeshCollider>(physicsTransform.gameObject);

        _generatedMesh = CoinCylinderMeshBuilder.Create(localRadius, localHeight, _cylinderSegments);
        meshFilter.sharedMesh = _generatedMesh;

        meshCollider.sharedMesh = _generatedMesh;
        meshCollider.convex = true;

        PhysicsMaterial coinMaterial = Resources.Load<PhysicsMaterial>("Physics/Coin");
        if (coinMaterial != null)
        {
            meshCollider.material = coinMaterial;
        }
    }

    static float GetRadius(Vector3 size, int thinAxis)
    {
        if (thinAxis == 0)
        {
            return Mathf.Max(size.y, size.z) * 0.5f;
        }

        if (thinAxis == 1)
        {
            return Mathf.Max(size.x, size.z) * 0.5f;
        }

        return Mathf.Max(size.x, size.y) * 0.5f;
    }

    static int GetThinAxisIndex(Vector3 size)
    {
        int thinAxis = 0;
        if (size.y < size[thinAxis])
        {
            thinAxis = 1;
        }

        if (size.z < size[thinAxis])
        {
            thinAxis = 2;
        }

        return thinAxis;
    }

    static Quaternion GetWorldCylinderRotation(int thinWorldAxis)
    {
        Vector3 cylinderUp = Vector3.up;
        if (thinWorldAxis == 0)
        {
            cylinderUp = Vector3.right;
        }
        else if (thinWorldAxis == 2)
        {
            cylinderUp = Vector3.forward;
        }

        return Quaternion.FromToRotation(Vector3.up, cylinderUp);
    }

    static T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component == null)
        {
            component = target.AddComponent<T>();
        }

        return component;
    }

    void CleanupGeneratedMesh()
    {
        if (_generatedMesh == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(_generatedMesh);
        }
        else
        {
            DestroyImmediate(_generatedMesh);
        }

        _generatedMesh = null;
    }

    static void DestroyObject(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}

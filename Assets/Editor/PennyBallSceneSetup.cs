using UnityEditor;
using UnityEngine;

public static class PennyBallSceneSetup
{
    const string CoinPrefabPath = "Assets/Prefab/Coin.prefab";
    const string SahaPrefabPath = "Assets/Prefab/Saha.prefab";

    [MenuItem("PennyBall/Setup Coin Physics")]
    static void SetupCoinPhysics()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(CoinPrefabPath);
        if (prefabRoot == null)
        {
            Debug.LogError($"Coin prefab bulunamadi: {CoinPrefabPath}");
            return;
        }

        Rigidbody rigidbody = GetOrAddComponent<Rigidbody>(prefabRoot);
        rigidbody.mass = 0.08f;
        rigidbody.linearDamping = 1.2f;
        rigidbody.angularDamping = 2f;
        rigidbody.useGravity = false;
        rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rigidbody.constraints = RigidbodyConstraints.FreezePositionY
                                | RigidbodyConstraints.FreezeRotationX
                                | RigidbodyConstraints.FreezeRotationZ;

        Transform coinMesh = prefabRoot.transform.Find("Coin_Object");
        if (coinMesh != null)
        {
            Object.DestroyImmediate(coinMesh.GetComponent<BoxCollider>());
            Object.DestroyImmediate(coinMesh.GetComponent<SphereCollider>());
            Object.DestroyImmediate(coinMesh.GetComponent<CapsuleCollider>());
            Object.DestroyImmediate(coinMesh.GetComponent<MeshCollider>());
        }

        GetOrAddComponent<CoinDragController>(prefabRoot);
        GetOrAddComponent<CoinAimIndicator>(prefabRoot);
        GetOrAddComponent<CoinAimIndicatorSettings>(prefabRoot);
        GetOrAddComponent<CoinIdentity>(prefabRoot);
        GetOrAddComponent<CoinVisualState>(prefabRoot);

        PrefabUtility.SaveAsPrefabAsset(prefabRoot, CoinPrefabPath);
        PrefabUtility.UnloadPrefabContents(prefabRoot);

        Debug.Log("Coin prefab fizik ve surukleme bilesenleriyle guncellendi.");
    }

    [MenuItem("PennyBall/Setup Play Surface Collider")]
    static void SetupPlaySurfaceCollider()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(SahaPrefabPath);
        if (prefabRoot == null)
        {
            Debug.LogError($"Saha prefab bulunamadi: {SahaPrefabPath}");
            return;
        }

        Transform sahaGeometry = prefabRoot.transform.Find("SahaGeometry");
        if (sahaGeometry != null)
        {
            MeshCollider meshCollider = GetOrAddComponent<MeshCollider>(sahaGeometry.gameObject);
            MeshFilter meshFilter = sahaGeometry.GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                meshCollider.sharedMesh = meshFilter.sharedMesh;
            }

            meshCollider.convex = false;

            PhysicsMaterial tableMaterial = Resources.Load<PhysicsMaterial>("Physics/TableSurface");
            if (tableMaterial != null)
            {
                meshCollider.material = tableMaterial;
            }
        }

        GetOrAddComponent<PlaySurfacePhysics>(prefabRoot);

        PrefabUtility.SaveAsPrefabAsset(prefabRoot, SahaPrefabPath);
        PrefabUtility.UnloadPrefabContents(prefabRoot);

        Debug.Log("Saha prefab'ina MeshCollider eklendi.");
    }

    [MenuItem("PennyBall/Setup Scene Input")]
    static void SetupSceneInput()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            camera = Object.FindFirstObjectByType<Camera>();
        }

        if (camera == null)
        {
            Debug.LogError("Sahnede kamera bulunamadi.");
            return;
        }

        GetOrAddComponent<CoinInputHandler>(camera.gameObject);
        EditorUtility.SetDirty(camera.gameObject);

        Debug.Log("CoinInputHandler kameraya eklendi.");
    }

    [MenuItem("PennyBall/Setup Game Rules")]
    static void SetupGameRules()
    {
        GameRulesManager existing = Object.FindFirstObjectByType<GameRulesManager>();
        if (existing != null)
        {
            Debug.Log("GameRulesManager zaten sahnede var.");
            return;
        }

        var rulesObject = new GameObject("GameRules");
        rulesObject.AddComponent<GameRulesManager>();
        Undo.RegisterCreatedObjectUndo(rulesObject, "Setup Game Rules");
        Debug.Log("GameRulesManager sahneye eklendi.");
    }

    [MenuItem("PennyBall/Setup Kale Prefab Goal Zones")]
    static void SetupKalePrefabGoalZones()
    {
        const string kalePrefabPath = "Assets/Prefab/Kale.prefab";
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(kalePrefabPath);
        if (prefabRoot == null)
        {
            Debug.LogError($"Kale prefab bulunamadi: {kalePrefabPath}");
            return;
        }

        Transform trigger = prefabRoot.transform.Find("GoalTrigger");
        if (trigger == null)
        {
            var triggerObject = new GameObject("GoalTrigger");
            trigger = triggerObject.transform;
            trigger.SetParent(prefabRoot.transform, false);
            trigger.localPosition = new Vector3(0f, 0.14f, 0f);
        }

        BoxCollider boxCollider = GetOrAddComponent<BoxCollider>(trigger.gameObject);
        boxCollider.isTrigger = true;
        boxCollider.center = Vector3.zero;
        boxCollider.size = new Vector3(0.48f, 0.1f, 0.18f);

        GetOrAddComponent<GoalZone>(trigger.gameObject);

        PrefabUtility.SaveAsPrefabAsset(prefabRoot, kalePrefabPath);
        PrefabUtility.UnloadPrefabContents(prefabRoot);

        Debug.Log("Kale prefab GoalTrigger guncellendi.");
    }

    [MenuItem("PennyBall/Setup Boundary Physics")]
    static void SetupBoundaryPhysics()
    {
        GameObject boundaries = GameObject.Find("Boundries");
        if (boundaries == null)
        {
            Debug.LogError("Sahnede 'Boundries' objesi bulunamadi.");
            return;
        }

        GetOrAddComponent<BoundaryPhysics>(boundaries);
        EditorUtility.SetDirty(boundaries);
        Debug.Log("BoundaryPhysics Boundries objesine eklendi.");
    }

    [MenuItem("PennyBall/Setup All")]
    static void SetupAll()
    {
        SetupCoinPhysics();
        SetupPlaySurfaceCollider();
        SetupBoundaryPhysics();
        SetupGameRules();
        SetupSceneInput();
    }

    public static void ExecuteSetupAll()
    {
        SetupCoinPhysics();
        SetupPlaySurfaceCollider();
        SetupBoundaryPhysics();
        SetupGameRules();
        SetupSceneInput();
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
}

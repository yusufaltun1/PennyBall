using UnityEngine;

[DisallowMultipleComponent]
public class BoundaryPhysics : MonoBehaviour
{
    static PhysicsMaterial _wallMaterial;

    void Awake()
    {
        if (_wallMaterial == null)
        {
            _wallMaterial = Resources.Load<PhysicsMaterial>("Physics/Wall");
        }

        if (_wallMaterial == null)
        {
            return;
        }

        Collider[] colliders = GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].material = _wallMaterial;
        }
    }
}

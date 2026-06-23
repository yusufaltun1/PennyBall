using UnityEngine;

[DisallowMultipleComponent]
public class PlaySurfacePhysics : MonoBehaviour
{
    static PhysicsMaterial _tableMaterial;

    void Awake()
    {
        if (_tableMaterial == null)
        {
            _tableMaterial = Resources.Load<PhysicsMaterial>("Physics/TableSurface");
        }

        if (_tableMaterial == null)
        {
            return;
        }

        Collider[] colliders = GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].material = _tableMaterial;
        }
    }
}

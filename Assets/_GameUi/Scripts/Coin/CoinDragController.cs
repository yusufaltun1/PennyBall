using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CoinAimIndicator))]
[RequireComponent(typeof(CoinIdentity))]
[RequireComponent(typeof(CoinVisualState))]
public class CoinDragController : MonoBehaviour
{
    [SerializeField] float _launchForceMultiplier = 18f;
    [SerializeField] float _maxLaunchSpeed = 10f;
    [SerializeField] float _maxPullDistance = 0.40f;
    [SerializeField] float _minPullDistance = 0.025f;
    [SerializeField] float _stopSpeedThreshold = 0.05f;
    [SerializeField] float _linearDamping = 2.5f;
    [SerializeField] float _angularDamping = 5f;
    [SerializeField] float _mass = 0.1f;
    [SerializeField] float _wallBounceRetention = 0.9f;
    [SerializeField] string _boundariesRootName = "Boundries";

    Rigidbody _rigidbody;
    CoinAimIndicator _aimIndicator;
    Transform _boundariesRoot;

    Vector3 _anchorPosition;
    Vector3 _pullPosition;
    float _tableHeight;
    bool _isAiming;

    public bool IsAiming => _isAiming;
    public bool IsSliding =>
        !_isAiming
        && !_rigidbody.isKinematic
        && _rigidbody.linearVelocity.sqrMagnitude > _stopSpeedThreshold * _stopSpeedThreshold;

    public float LaunchForceMultiplier => _launchForceMultiplier;
    public float MaxLaunchSpeed => _maxLaunchSpeed;
    public float MaxPullDistance => _maxPullDistance;
    public float MinPullDistance => _minPullDistance;

    void Awake()
    {
        EnsureGameplayComponents();

        _rigidbody = GetComponent<Rigidbody>();
        _aimIndicator = GetComponent<CoinAimIndicator>();
        _tableHeight = transform.position.y;
        CacheBoundariesRoot();
        ConfigureRigidbody();
        ApplyCoinPhysicsMaterial();
    }

    void EnsureGameplayComponents()
    {
        if (GetComponent<CoinIdentity>() == null)
        {
            gameObject.AddComponent<CoinIdentity>();
        }

        if (GetComponent<CoinVisualState>() == null)
        {
            gameObject.AddComponent<CoinVisualState>();
        }
    }

    void CacheBoundariesRoot()
    {
        GameObject boundaries = GameObject.Find(_boundariesRootName);
        if (boundaries != null)
        {
            _boundariesRoot = boundaries.transform;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (_rigidbody.isKinematic || _isAiming)
        {
            return;
        }

        if (!IsBoundaryCollider(collision.collider))
        {
            if (!IsWallLikeSurface(collision))
            {
                return;
            }
        }

        ApplyBilliardBounce(collision);
    }

    bool IsWallLikeSurface(Collision collision)
    {
        if (collision.collider.GetComponentInParent<CoinDragController>() != null)
        {
            return false;
        }

        if (collision.contactCount == 0)
        {
            return false;
        }

        Vector3 normal = collision.GetContact(0).normal;
        return Mathf.Abs(normal.y) < 0.65f;
    }

    bool IsBoundaryCollider(Collider collider)
    {
        if (_boundariesRoot == null)
        {
            CacheBoundariesRoot();
        }

        if (_boundariesRoot != null && collider.transform.IsChildOf(_boundariesRoot))
        {
            return true;
        }

        return false;
    }

    void ApplyBilliardBounce(Collision collision)
    {
        if (collision.contactCount == 0)
        {
            return;
        }

        Vector3 normal = collision.GetContact(0).normal;
        normal.y = 0f;
        if (normal.sqrMagnitude < 0.0001f)
        {
            return;
        }

        normal.Normalize();

        Vector3 velocity = _rigidbody.linearVelocity;
        velocity.y = 0f;
        if (velocity.sqrMagnitude < 0.01f)
        {
            return;
        }

        // Only bounce if moving INTO the wall; if already moving away, skip.
        if (Vector3.Dot(velocity, normal) >= 0f)
        {
            return;
        }

        _rigidbody.linearVelocity = Vector3.Reflect(velocity, normal) * _wallBounceRetention;
    }

    void FixedUpdate()
    {
        if (_rigidbody.isKinematic || _isAiming)
        {
            return;
        }

        LockToTablePlane();
    }

    void LockToTablePlane()
    {
        Vector3 velocity = _rigidbody.linearVelocity;
        velocity.y = 0f;
        _rigidbody.linearVelocity = velocity;

        Vector3 angularVelocity = _rigidbody.angularVelocity;
        angularVelocity.x = 0f;
        angularVelocity.z = 0f;
        _rigidbody.angularVelocity = angularVelocity;

        Vector3 position = _rigidbody.position;
        position.y = _tableHeight;
        _rigidbody.position = position;
    }

    void ApplyCoinPhysicsMaterial()
    {
        PhysicsMaterial coinMaterial = Resources.Load<PhysicsMaterial>("Physics/Coin");
        if (coinMaterial == null)
        {
            return;
        }

        Collider[] colliders = GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].material = coinMaterial;
        }
    }

    void ConfigureRigidbody()
    {
        _rigidbody.mass = _mass;
        _rigidbody.linearDamping = _linearDamping;
        _rigidbody.angularDamping = _angularDamping;
        _rigidbody.useGravity = false;
        _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _rigidbody.constraints = RigidbodyConstraints.FreezePositionY
                                 | RigidbodyConstraints.FreezeRotation;
    }

    public void BeginAim()
    {
        _isAiming = true;
        _anchorPosition = transform.position;
        _pullPosition = _anchorPosition;

        _rigidbody.isKinematic = true;
        _aimIndicator.Hide();
    }

    public void UpdateAim(Vector3 pullWorldPosition)
    {
        if (!_isAiming)
        {
            return;
        }

        _pullPosition = FlattenToTable(pullWorldPosition, _anchorPosition.y);

        Vector3 launchDirection;
        float pullDistance;
        if (!TryGetLaunchData(out launchDirection, out pullDistance))
        {
            _aimIndicator.Hide();
            return;
        }

        _aimIndicator.UpdateVisual(_anchorPosition, _pullPosition, launchDirection, pullDistance);
    }

    public void ReleaseAim()
    {
        if (!_isAiming)
        {
            return;
        }

        _isAiming = false;
        _aimIndicator.Hide();

        Vector3 launchVelocity = CalculateLaunchVelocity();
        ApplyLaunchVelocity(launchVelocity);
    }

    public bool LaunchInDirection(Vector3 direction, float pullDistance)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        direction.Normalize();
        pullDistance = Mathf.Clamp(pullDistance, _minPullDistance, _maxPullDistance);
        if (pullDistance < _minPullDistance)
        {
            return false;
        }

        if (_isAiming)
        {
            CancelAim();
        }

        Vector3 launchVelocity = direction * (pullDistance * _launchForceMultiplier);
        return ApplyLaunchVelocity(launchVelocity);
    }

    bool ApplyLaunchVelocity(Vector3 launchVelocity)
    {
        _rigidbody.isKinematic = false;

        if (launchVelocity.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        launchVelocity.y = 0f;
        _rigidbody.linearVelocity = Vector3.ClampMagnitude(launchVelocity, _maxLaunchSpeed);
        return _rigidbody.linearVelocity.sqrMagnitude > 0.0001f;
    }

    public void CancelAim()
    {
        if (!_isAiming)
        {
            return;
        }

        _isAiming = false;
        _aimIndicator.Hide();
        _rigidbody.isKinematic = false;
    }

    Vector3 CalculateLaunchVelocity()
    {
        Vector3 launchDirection;
        float pullDistance;
        if (!TryGetLaunchData(out launchDirection, out pullDistance))
        {
            return Vector3.zero;
        }

        return launchDirection * (pullDistance * _launchForceMultiplier);
    }

    bool TryGetLaunchData(out Vector3 launchDirection, out float pullDistance)
    {
        Vector3 pullVector = _anchorPosition - _pullPosition;
        pullVector.y = 0f;

        pullDistance = Mathf.Clamp(pullVector.magnitude, 0f, _maxPullDistance);
        if (pullDistance < _minPullDistance)
        {
            launchDirection = Vector3.zero;
            return false;
        }

        launchDirection = pullVector / pullVector.magnitude;
        return true;
    }

    static Vector3 FlattenToTable(Vector3 position, float height)
    {
        return new Vector3(position.x, height, position.z);
    }
}

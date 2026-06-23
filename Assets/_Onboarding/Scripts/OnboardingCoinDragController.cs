using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(OnboardingAimIndicator))]
public class OnboardingCoinDragController : MonoBehaviour
{
    [SerializeField] float _launchForceMultiplier = 4f;
    [SerializeField] float _maxLaunchSpeed = 8f;
    [SerializeField] float _maxPullDistance = 0.35f;
    [SerializeField] float _minPullDistance = 0.025f;
    [SerializeField] float _stopSpeedThreshold = 0.05f;
    [SerializeField] float _linearDamping = 2f;
    [SerializeField] float _angularDamping = 5f;
    [SerializeField] float _mass = 0.1f;
    [SerializeField] float _wallBounceRetention = 0.9f;
    [SerializeField] string _boundariesRootName = "Boundries";

    Rigidbody _rigidbody;
    OnboardingAimIndicator _aimIndicator;
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

    public event Action<OnboardingCoin, OnboardingCoinDragController> LaunchCommitted;

    void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _aimIndicator = GetComponent<OnboardingAimIndicator>();
        _tableHeight = transform.position.y;
        CacheBoundariesRoot();
        ConfigureRigidbody();
        ApplyCoinPhysicsMaterial();
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

        if (!IsBoundaryCollider(collision.collider) && !IsWallLikeSurface(collision))
        {
            return;
        }

        ApplyBilliardBounce(collision);
    }

    bool IsWallLikeSurface(Collision collision)
    {
        if (collision.collider.GetComponentInParent<OnboardingCoinDragController>() != null)
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

        return _boundariesRoot != null && collider.transform.IsChildOf(_boundariesRoot);
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

        if (Vector3.Dot(velocity, normal) > 0f)
        {
            normal = -normal;
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
        _rigidbody.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
    }

    public void BeginAim()
    {
        _isAiming = true;
        _anchorPosition = transform.position;
        _pullPosition = _anchorPosition;
        _rigidbody.isKinematic = true;
        _aimIndicator.BeginAim();
    }

    public void UpdateAim(Vector3 pullWorldPosition, OnboardingAimFeedback feedback)
    {
        if (!_isAiming)
        {
            return;
        }

        _pullPosition = FlattenToTable(pullWorldPosition, _anchorPosition.y);

        if (!TryGetLaunchData(out Vector3 launchDirection, out float pullDistance))
        {
            _aimIndicator.BeginAim();
            return;
        }

        _aimIndicator.UpdateVisual(_anchorPosition, _pullPosition, launchDirection, pullDistance, feedback);
    }

    public bool TryReleaseAim()
    {
        return TryReleaseAimAt(_pullPosition);
    }

    public bool TryReleaseAimAt(Vector3 pullWorldPosition)
    {
        if (!_isAiming)
        {
            return false;
        }

        _pullPosition = FlattenToTable(pullWorldPosition, _anchorPosition.y);

        if (!TryGetLaunchData(out _, out _))
        {
            return false;
        }

        _isAiming = false;
        _aimIndicator.EndAim();

        Vector3 launchVelocity = CalculateLaunchVelocity();
        bool launched = ApplyLaunchVelocity(launchVelocity);
        if (launched)
        {
            OnboardingCoin coin = GetComponent<OnboardingCoin>();
            LaunchCommitted?.Invoke(coin, this);
        }

        return launched;
    }

    public void CancelAim()
    {
        if (!_isAiming)
        {
            return;
        }

        _isAiming = false;
        _aimIndicator.EndAim();
        _rigidbody.isKinematic = false;
    }

    public bool TryGetLaunchData(out Vector3 launchDirection, out float pullDistance)
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

    public void ShowGuide(
        Vector3 launchDirection,
        float pullDistance,
        float pullTolerance,
        float directionToleranceDegrees)
    {
        _aimIndicator.ConfigureGuide(
            transform,
            launchDirection,
            pullDistance,
            pullTolerance,
            directionToleranceDegrees);
    }

    public void HideGuide()
    {
        _aimIndicator.HideGuide();
    }

    public void ResetToPosition(Vector3 position)
    {
        CancelAim();
        _rigidbody.isKinematic = true;
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
        transform.position = position;
        _rigidbody.isKinematic = false;
    }

    Vector3 CalculateLaunchVelocity()
    {
        if (!TryGetLaunchData(out Vector3 launchDirection, out float pullDistance))
        {
            return Vector3.zero;
        }

        return launchDirection * (pullDistance * _launchForceMultiplier);
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

    static Vector3 FlattenToTable(Vector3 position, float height)
    {
        return new Vector3(position.x, height, position.z);
    }
}

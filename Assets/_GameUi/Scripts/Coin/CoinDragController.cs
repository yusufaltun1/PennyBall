using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CoinAimIndicator))]
[RequireComponent(typeof(CoinIdentity))]
[RequireComponent(typeof(CoinVisualState))]
[RequireComponent(typeof(CoinGateIndicatorSettings))]
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
    [SerializeField] [Range(0.5f, 1.2f)] float _travelDistanceScale = 0.9f;

    [Header("Collision Spin")]
    [SerializeField] float _collisionSpinStrength = 1.5f;
    [SerializeField] float _maxSpinSpeed = 1080f;
    [SerializeField] float _spinDecay = 2f;
    [SerializeField] float _minCollisionSpeedForSpin = 0.15f;

    Rigidbody _rigidbody;
    CoinAimIndicator _aimIndicator;
    readonly RaycastHit[] _wallHits = new RaycastHit[8];

    Transform _spinVisual;
    Quaternion _spinVisualBaseLocalRotation;
    float _spinAngle;
    float _spinSpeedDegrees;
    float _coinCastRadius = 0.024f;

    Vector3 _anchorPosition;
    Vector3 _pullPosition;
    float _tableHeight;
    bool _isAiming;
    bool _isAimPullLocked;
    bool _isAimDirectionLocked;
    Vector3 _lockedLaunchDirection;
    readonly List<Vector3> _slidePath = new(128);
    bool _recordSlidePath;

    public bool IsAiming => _isAiming;
    public bool IsAimPullLocked => _isAimPullLocked;
    public bool IsSliding =>
        !_isAiming
        && !_rigidbody.isKinematic
        && _rigidbody.linearVelocity.sqrMagnitude > _stopSpeedThreshold * _stopSpeedThreshold;

    /// <summary>Son atışın FixedUpdate path’i — kapı doğrulaması için.</summary>
    public IReadOnlyList<Vector3> SlidePath => _slidePath;

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
        CacheCoinCastRadius();
        CacheSpinVisual();
        ConfigureRigidbody();
        ApplyCoinPhysicsMaterial();
    }

    void CacheSpinVisual()
    {
        _spinVisual = transform.Find("Coin_Object");
        if (_spinVisual == null)
        {
            MeshRenderer meshRenderer = GetComponentInChildren<MeshRenderer>();
            if (meshRenderer != null)
            {
                _spinVisual = meshRenderer.transform;
            }
        }

        if (_spinVisual != null)
        {
            _spinVisualBaseLocalRotation = _spinVisual.localRotation;
        }
    }

    void CacheCoinCastRadius()
    {
        SphereCollider sphereCollider = GetComponentInChildren<SphereCollider>();
        if (sphereCollider == null)
        {
            return;
        }

        float scale = sphereCollider.transform.lossyScale.x;
        _coinCastRadius = Mathf.Max(sphereCollider.radius * scale, 0.005f);
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

        if (GetComponent<CoinGateIndicatorSettings>() == null)
        {
            gameObject.AddComponent<CoinGateIndicatorSettings>();
        }

        if (GetComponent<CoinAimIndicatorSettings>() == null)
        {
            gameObject.AddComponent<CoinAimIndicatorSettings>();
        }
    }

    void FixedUpdate()
    {
        if (_rigidbody.isKinematic || _isAiming)
        {
            return;
        }

        LockToTablePlane();
        UpdateVisualSpin();

        // IsSliding'e bağlama: tek frame düşük hız kaydı kesmesin.
        if (_recordSlidePath)
        {
            _slidePath.Add(_rigidbody != null ? _rigidbody.position : transform.position);
        }
    }

    void UpdateVisualSpin()
    {
        if (_spinVisual == null)
        {
            return;
        }

        if (Mathf.Abs(_spinSpeedDegrees) > 0.01f)
        {
            _spinAngle += _spinSpeedDegrees * Time.fixedDeltaTime;
            float decay = Mathf.Exp(-_spinDecay * Time.fixedDeltaTime);
            _spinSpeedDegrees *= decay;
        }

        ApplyVisualSpinRotation();
    }

    void ApplyVisualSpinRotation()
    {
        if (_spinVisual == null)
        {
            return;
        }

        _spinVisual.localRotation = _spinVisualBaseLocalRotation * Quaternion.AngleAxis(_spinAngle, Vector3.forward);
    }

    void LockToTablePlane()
    {
        Vector3 velocity = _rigidbody.linearVelocity;
        velocity.y = 0f;
        _rigidbody.linearVelocity = velocity;
    }

    void OnCollisionEnter(Collision collision)
    {
        PlayCollisionFeedback(collision);

        if (_rigidbody.isKinematic || _isAiming || _collisionSpinStrength <= 0f || _spinVisual == null)
        {
            return;
        }

        Vector3 relativeVelocity = collision.relativeVelocity;
        relativeVelocity.y = 0f;
        if (relativeVelocity.sqrMagnitude < _minCollisionSpeedForSpin * _minCollisionSpeedForSpin)
        {
            return;
        }

        float spinSpeed = CalculateCollisionSpinSpeed(collision, relativeVelocity);
        ApplyCollisionSpin(spinSpeed);
    }

    void PlayCollisionFeedback(Collision collision)
    {
        if (_rigidbody.isKinematic || _isAiming)
        {
            return;
        }

        CoinDragController otherCoin = collision.collider.GetComponentInParent<CoinDragController>();
        if (otherCoin != null && otherCoin != this)
        {
            if (GetInstanceID() > otherCoin.GetInstanceID())
            {
                return;
            }

            Vector3 relativeVelocity = collision.relativeVelocity;
            relativeVelocity.y = 0f;
            float speed = relativeVelocity.magnitude;
            if (speed < 0.001f)
            {
                return;
            }

            Vector3 contactPoint = collision.contactCount > 0
                ? collision.GetContact(0).point
                : (transform.position + otherCoin.transform.position) * 0.5f;

            GameFeedback.EnsureInstance()?.TryPlayCoinHit(
                GetInstanceID(),
                otherCoin.GetInstanceID(),
                speed,
                contactPoint);
            return;
        }

        if (collision.collider.GetComponentInParent<BoundaryPhysics>() == null)
        {
            return;
        }

        Vector3 wallRelativeVelocity = collision.relativeVelocity;
        wallRelativeVelocity.y = 0f;
        float wallSpeed = wallRelativeVelocity.sqrMagnitude > 0.0001f
            ? wallRelativeVelocity.magnitude
            : _rigidbody.linearVelocity.magnitude;
        if (wallSpeed < _minCollisionSpeedForSpin * 0.5f)
        {
            return;
        }

        Vector3 wallPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : transform.position;
        float intensity = Mathf.Clamp01(wallSpeed / _maxLaunchSpeed);
        GameFeedback.EnsureInstance()?.PlayWallHit(intensity, wallPoint);
    }

    float CalculateCollisionSpinSpeed(Collision collision, Vector3 relativeVelocity)
    {
        float spinContribution = 0f;
        int contactCount = collision.contactCount;
        if (contactCount <= 0)
        {
            return 0f;
        }

        Vector3 impulse = collision.impulse;
        impulse.y = 0f;

        for (int i = 0; i < contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            Vector3 offset = contact.point - transform.position;
            offset.y = 0f;
            float radiusSq = offset.sqrMagnitude;
            if (radiusSq < 0.000001f)
            {
                continue;
            }

            Vector3 selfPointVelocity = _rigidbody.GetPointVelocity(contact.point);
            Vector3 otherPointVelocity = collision.rigidbody != null
                ? collision.rigidbody.GetPointVelocity(contact.point)
                : Vector3.zero;

            Vector3 slipVelocity = selfPointVelocity - otherPointVelocity;
            slipVelocity.y = 0f;

            float impulseTorque = Vector3.Cross(offset, impulse).y / radiusSq;
            float slipTorque = Vector3.Cross(offset, slipVelocity).y;
            float glancingTorque = Vector3.Cross(offset.normalized, relativeVelocity).y;

            spinContribution += impulseTorque + slipTorque + glancingTorque;
        }

        spinContribution /= contactCount;
        float spinSpeedDegrees = spinContribution * _collisionSpinStrength / _coinCastRadius * Mathf.Rad2Deg;
        return spinSpeedDegrees;
    }

    void ApplyCollisionSpin(float spinSpeedDegrees)
    {
        if (Mathf.Abs(spinSpeedDegrees) <= 0.01f)
        {
            return;
        }

        _spinSpeedDegrees = Mathf.Clamp(_spinSpeedDegrees + spinSpeedDegrees, -_maxSpinSpeed, _maxSpinSpeed);
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
        _isAimPullLocked = false;
        _isAimDirectionLocked = false;
        _anchorPosition = transform.position;
        _pullPosition = _anchorPosition;

        _rigidbody.isKinematic = true;
        ResetVisualRotation();
        _aimIndicator.Hide();
    }

    public void ResetVisualRotation()
    {
        _spinAngle = 0f;
        _spinSpeedDegrees = 0f;
        ApplyVisualSpinRotation();
    }

    public void LockAimPull()
    {
        if (!_isAiming)
        {
            return;
        }

        _isAimPullLocked = true;
        ApplyAimVisual();
    }

    public void LockAimTowardWorldTarget(Vector3 targetWorldPosition)
    {
        if (!_isAiming)
        {
            return;
        }

        Vector3 launchDirection = targetWorldPosition - _anchorPosition;
        launchDirection.y = 0f;
        if (launchDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        launchDirection.Normalize();
        float pullDistance = Mathf.Max(_minPullDistance, GetPullDistanceForPower01(0.5f));
        _pullPosition = _anchorPosition - launchDirection * pullDistance;
        _isAimPullLocked = true;
        ApplyAimVisual();
    }

    public void LockAimDirection(Vector3 launchDirection)
    {
        if (!_isAiming)
        {
            return;
        }

        launchDirection.y = 0f;
        if (launchDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        _lockedLaunchDirection = launchDirection.normalized;
        _isAimDirectionLocked = true;
        _isAimPullLocked = false;
        ConstrainPullToLockedDirection(_pullPosition);
        ApplyAimVisual();
    }

    public void SetAimPullForPower01(float power01)
    {
        if (!_isAiming || !_isAimDirectionLocked)
        {
            return;
        }

        float pullDistance = GetPullDistanceForPower01(Mathf.Clamp01(power01));
        _pullPosition = _anchorPosition - _lockedLaunchDirection * pullDistance;
        ApplyAimVisual();
    }

    public bool TryGetPower01ForWorldTarget(
        Vector3 anchor,
        Vector3 direction,
        Vector3 targetWorldPosition,
        out float power01,
        float distanceTolerance = 0.02f)
    {
        power01 = 0f;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        direction.Normalize();
        Vector3 flatAnchor = FlattenToTable(anchor, anchor.y);
        Vector3 flatTarget = FlattenToTable(targetWorldPosition, anchor.y);
        Vector3 toTarget = flatTarget - flatAnchor;
        toTarget.y = 0f;
        float desiredDistance = toTarget.magnitude;
        if (desiredDistance < 0.0001f)
        {
            return false;
        }

        if (Vector3.Dot(direction, toTarget.normalized) < 0.25f)
        {
            return false;
        }

        float lo = 0f;
        float hi = 1f;
        float bestPower = 0.5f;
        float bestError = float.MaxValue;

        for (int i = 0; i < 24; i++)
        {
            float mid = (lo + hi) * 0.5f;
            Vector3 pathEnd = GetPathEndForDirectionAndPower(anchor, direction, mid);
            float pathDistance = Vector3.Distance(
                new Vector3(flatAnchor.x, 0f, flatAnchor.z),
                new Vector3(pathEnd.x, 0f, pathEnd.z));
            float error = Mathf.Abs(pathDistance - desiredDistance);
            if (error < bestError)
            {
                bestError = error;
                bestPower = mid;
            }

            if (pathDistance < desiredDistance)
            {
                lo = mid;
            }
            else
            {
                hi = mid;
            }
        }

        power01 = bestPower;
        return bestError <= distanceTolerance || bestError <= desiredDistance * 0.08f;
    }

    void ConstrainPullToLockedDirection(Vector3 pullWorldPosition)
    {
        pullWorldPosition = FlattenToTable(pullWorldPosition, _anchorPosition.y);
        Vector3 fromAnchorToPull = pullWorldPosition - _anchorPosition;
        fromAnchorToPull.y = 0f;
        float pullDistance = Vector3.Dot(fromAnchorToPull, -_lockedLaunchDirection);
        pullDistance = Mathf.Clamp(pullDistance, _minPullDistance, _maxPullDistance);
        _pullPosition = _anchorPosition - _lockedLaunchDirection * pullDistance;
    }

    public void UpdateAim(Vector3 pullWorldPosition)
    {
        if (!_isAiming)
        {
            return;
        }

        if (_isAimPullLocked)
        {
            ApplyAimVisual();
            return;
        }

        if (_isAimDirectionLocked)
        {
            ConstrainPullToLockedDirection(pullWorldPosition);
        }
        else
        {
            _pullPosition = FlattenToTable(pullWorldPosition, _anchorPosition.y);
        }

        ApplyAimVisual();
    }

    void ApplyAimVisual()
    {
        Vector3 launchDirection;
        float pullDistance;
        if (!TryGetLaunchData(out launchDirection, out pullDistance))
        {
            _aimIndicator.Hide();
            return;
        }

        float power01 = ResolveAimPower01(pullDistance);
        float launchSpeed = GetLaunchSpeedForPower01(power01);
        float travelDistance = EstimateTravelDistance(launchSpeed);
        CoinAimIndicator.PathVisual path = BuildAimPath(_anchorPosition, launchDirection, travelDistance);
        _aimIndicator.UpdateVisual(path, power01);
    }

    public bool TryGetActiveAimTarget(out Vector3 targetWorldPosition, out float power01)
    {
        targetWorldPosition = default;
        power01 = 0f;

        if (!_isAiming)
        {
            return false;
        }

        if (!TryGetLaunchData(out Vector3 launchDirection, out float pullDistance))
        {
            return false;
        }

        power01 = ResolveAimPower01(pullDistance);
        targetWorldPosition = GetPathEndForDirectionAndPower(_anchorPosition, launchDirection, power01);
        return true;
    }

    public bool TryGetActiveLaunchDirection(out Vector3 launchDirection)
    {
        launchDirection = default;
        if (!_isAiming)
        {
            return false;
        }

        return TryGetLaunchData(out launchDirection, out _);
    }

    public Vector3 GetPathEndForDirectionAndPower(Vector3 anchor, Vector3 direction, float power01)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return anchor;
        }

        direction.Normalize();
        float pullDistance = Mathf.Lerp(_minPullDistance, _maxPullDistance, Mathf.Clamp01(power01));
        float launchSpeed = Mathf.Min(pullDistance * _launchForceMultiplier, _maxLaunchSpeed);
        float travelDistance = EstimateTravelDistance(launchSpeed);
        return BuildAimPath(anchor, direction, travelDistance).PrimaryEnd;
    }

    public CoinAimIndicator.PathVisual BuildPathForDirectionAndPower(Vector3 anchor, Vector3 direction, float power01)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return new CoinAimIndicator.PathVisual(anchor, anchor, false, anchor);
        }

        direction.Normalize();
        float pullDistance = Mathf.Lerp(_minPullDistance, _maxPullDistance, Mathf.Clamp01(power01));
        float launchSpeed = Mathf.Min(pullDistance * _launchForceMultiplier, _maxLaunchSpeed);
        float travelDistance = EstimateTravelDistance(launchSpeed);
        return BuildAimPath(anchor, direction, travelDistance);
    }

    public void ReleaseAim()
    {
        if (!_isAiming)
        {
            return;
        }

        _isAiming = false;
        _isAimPullLocked = false;
        _isAimDirectionLocked = false;
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
        if (_rigidbody.linearVelocity.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        _slidePath.Clear();
        _slidePath.Add(_rigidbody.position);
        _recordSlidePath = true;

        float power01 = Mathf.Clamp01(_rigidbody.linearVelocity.magnitude / _maxLaunchSpeed);
        GameFeedback.EnsureInstance()?.PlayShot(power01);
        return true;
    }

    public void ForceStopSliding()
    {
        if (_rigidbody == null)
        {
            return;
        }

        if (_recordSlidePath)
        {
            _slidePath.Add(_rigidbody.position);
            _recordSlidePath = false;
        }

        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
    }

    public void CopySlidePathTo(List<Vector3> destination)
    {
        if (destination == null)
        {
            return;
        }

        destination.Clear();
        for (int i = 0; i < _slidePath.Count; i++)
        {
            destination.Add(_slidePath[i]);
        }
    }

    public void FreezeForGoal()
    {
        if (_isAiming)
        {
            _isAiming = false;
            _aimIndicator.Hide();
        }

        ForceStopSliding();
        _spinSpeedDegrees = 0f;

        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = true;
        }
    }

    public void UnfreezeAfterGoal()
    {
        if (_rigidbody == null || _isAiming)
        {
            return;
        }

        _rigidbody.isKinematic = false;
    }

    public void CancelAim()
    {
        if (!_isAiming)
        {
            return;
        }

        _isAiming = false;
        _isAimPullLocked = false;
        _isAimDirectionLocked = false;
        _aimIndicator.Hide();
        _rigidbody.isKinematic = false;
    }

    public bool TryGetAimLaunchDirection(out Vector3 launchDirection)
    {
        return TryGetAimLaunchData(out launchDirection, out _);
    }

    public bool TryGetAimLaunchData(out Vector3 launchDirection, out float pullDistance)
    {
        launchDirection = Vector3.zero;
        pullDistance = 0f;
        if (!_isAiming)
        {
            return false;
        }

        return TryGetLaunchData(out launchDirection, out pullDistance);
    }

    Vector3 CalculateLaunchVelocity()
    {
        Vector3 launchDirection;
        float pullDistance;
        if (!TryGetLaunchData(out launchDirection, out pullDistance))
        {
            return Vector3.zero;
        }

        float power01 = ResolveAimPower01(pullDistance);
        float effectivePullDistance = GetPullDistanceForPower01(power01);
        return launchDirection * (effectivePullDistance * _launchForceMultiplier);
    }

    float ResolveAimPower01(float pullDistance)
    {
        if (TryGetOnboardingFixedAimPower(out float fixedPower))
        {
            return fixedPower;
        }

        return Mathf.InverseLerp(_minPullDistance, _maxPullDistance, pullDistance);
    }

    static bool TryGetOnboardingFixedAimPower(out float power01)
    {
        power01 = 0f;
        OnboardingGuideController guide = OnboardingGuideController.Instance;
        if (guide == null || !guide.UsesFixedTutorialAimPower)
        {
            return false;
        }

        power01 = Mathf.Clamp01(guide.FixedTutorialAimPower01);
        return true;
    }

    float GetPullDistanceForPower01(float power01)
    {
        return Mathf.Lerp(_minPullDistance, _maxPullDistance, Mathf.Clamp01(power01));
    }

    float GetLaunchSpeedForPower01(float power01)
    {
        float pullDistance = GetPullDistanceForPower01(power01);
        return Mathf.Min(pullDistance * _launchForceMultiplier, _maxLaunchSpeed);
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

    float EstimateTravelDistance(float launchSpeed)
    {
        launchSpeed = Mathf.Clamp(launchSpeed, 0f, _maxLaunchSpeed);
        if (launchSpeed <= _stopSpeedThreshold)
        {
            return 0f;
        }

        float distance = 0f;
        float speed = launchSpeed;
        float dt = Time.fixedDeltaTime > 0.0001f ? Time.fixedDeltaTime : 0.02f;
        float dampingFactor = Mathf.Clamp01(1f - _linearDamping * dt);

        for (int i = 0; i < 4000 && speed > _stopSpeedThreshold; i++)
        {
            distance += speed * dt;
            speed *= dampingFactor;
        }

        return distance * _travelDistanceScale;
    }

    CoinAimIndicator.PathVisual BuildAimPath(Vector3 anchor, Vector3 direction, float totalDistance)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f || totalDistance <= 0.0001f)
        {
            return new CoinAimIndicator.PathVisual(anchor, anchor, false, anchor);
        }

        direction.Normalize();

        if (!TryFindWallHit(anchor, direction, totalDistance, out RaycastHit wallHit, out float distanceToWall))
        {
            Vector3 freeEnd = anchor + direction * totalDistance;
            return new CoinAimIndicator.PathVisual(anchor, freeEnd, false, freeEnd);
        }

        Vector3 hitPoint = wallHit.point;
        hitPoint.y = anchor.y;
        float remainingDistance = totalDistance - distanceToWall;
        if (remainingDistance <= 0.01f)
        {
            return new CoinAimIndicator.PathVisual(anchor, hitPoint, false, hitPoint);
        }

        if (!TryGetReflectionDirection(direction, wallHit.normal, out Vector3 reflectionDirection))
        {
            return new CoinAimIndicator.PathVisual(anchor, hitPoint, false, hitPoint);
        }

        Vector3 bounceEnd = hitPoint + reflectionDirection * remainingDistance;
        return new CoinAimIndicator.PathVisual(anchor, hitPoint, true, bounceEnd);
    }

    bool TryFindWallHit(Vector3 anchor, Vector3 direction, float maxDistance, out RaycastHit closestHit, out float distanceFromAnchor)
    {
        closestHit = default;
        distanceFromAnchor = 0f;

        float castDistance = Mathf.Max(maxDistance - _coinCastRadius, 0f);
        if (castDistance <= 0.0001f)
        {
            return false;
        }

        Vector3 castOrigin = anchor + direction * _coinCastRadius;
        castOrigin.y = anchor.y;

        int hitCount = Physics.RaycastNonAlloc(
            castOrigin,
            direction,
            _wallHits,
            castDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        float closestDistance = float.MaxValue;
        bool found = false;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = _wallHits[i].collider;
            if (hitCollider == null || hitCollider.transform.IsChildOf(transform))
            {
                continue;
            }

            if (hitCollider.GetComponentInParent<BoundaryPhysics>() == null)
            {
                continue;
            }

            if (_wallHits[i].distance >= closestDistance)
            {
                continue;
            }

            closestDistance = _wallHits[i].distance;
            closestHit = _wallHits[i];
            found = true;
        }

        if (!found)
        {
            return false;
        }

        distanceFromAnchor = _coinCastRadius + closestDistance;
        Vector3 hitPoint = anchor + direction * distanceFromAnchor;
        hitPoint.y = anchor.y;
        closestHit.point = hitPoint;
        return true;
    }

    static bool TryGetReflectionDirection(Vector3 incomingDirection, Vector3 surfaceNormal, out Vector3 reflectionDirection)
    {
        Vector3 normal = surfaceNormal;
        normal.y = 0f;
        if (normal.sqrMagnitude < 0.0001f)
        {
            reflectionDirection = Vector3.zero;
            return false;
        }

        normal.Normalize();
        reflectionDirection = Vector3.Reflect(incomingDirection, normal);
        reflectionDirection.y = 0f;
        if (reflectionDirection.sqrMagnitude < 0.0001f)
        {
            reflectionDirection = Vector3.zero;
            return false;
        }

        reflectionDirection.Normalize();
        return true;
    }
}

using System.Collections;
using UnityEngine;

public class CameraAimZoom : MonoBehaviour
{
    enum EdgeCameraMode
    {
        None,
        DynamicZoom,
        DragSide
    }

    [Header("Edge Camera Mode")]
    [SerializeField] bool _dynamicZoom;
    [SerializeField] bool _dragSide = true;

    [Header("Pull Zoom")]
    [Tooltip("Max çekilmede kameranın geri çekileceği mesafe (world units)")]
    [SerializeField, Range(0.05f, 5f)] float _maxPullBack = 0.6f;

    [Tooltip("Sağa/sola çekilince ek zoom katsayısı (dikey ekranda dar yön için)")]
    [SerializeField, Range(1f, 4f)] float _sideZoomBoost = 2f;

    [Tooltip("Zoom in/out geçiş hızı")]
    [SerializeField, Range(1f, 20f)] float _lerpSpeed = 7f;

    [Tooltip("Düşük çekmede az, max çekmeye yaklaşınca hızlı artan zoom eğrisi (1 = doğrusal, 2+ = gecikmeli)")]
    [SerializeField, Range(1f, 5f)] float _zoomCurveExponent = 2.75f;

    [Header("Dynamic Zoom")]
    [SerializeField] float _edgeOrbitHorizontal = 0.85f;
    [SerializeField] float _focusHeight = 0.02f;
    [Tooltip("Kenar bakışında kameranın global Y değerinden düşülecek miktar (sahaya yaklaşır)")]
    [SerializeField, Range(0f, 1.5f)] float _edgeHeightLowering = 0.28f;
    [Tooltip("Kenar bakışında home pitch'ten düşülecek X rotasyonu (derece, daha yatay bakış)")]
    [SerializeField, Range(0f, 25f)] float _edgePitchReduceDegrees = 8f;
    [SerializeField, Range(0f, 1f)] float _maxEdgeBlendAtFullPull = 1f;
    [SerializeField, Range(1f, 3f)] float _edgeBlendCurveExponent = 1.6f;
    [Tooltip("Çekim başlayınca kameranın paranın arkasına dönme hızı (pull ratio aralığı)")]
    [SerializeField, Range(0f, 0.2f)] float _edgeAimAlignStartPull = 0.02f;
    [SerializeField, Range(0.05f, 0.35f)] float _edgeAimAlignEndPull = 0.12f;
    [Tooltip("Dynamic Zoom modunda güç çekimiyle geri zoom")]
    [SerializeField, Range(0.05f, 5f)] float _dynamicZoomMaxPullBack = 0.8f;

    [Header("Drag Side")]
    [Tooltip("Tam çekimde kameranın X pozisyonundan kayacağı max mesafe")]
    [SerializeField, Range(0f, 1f)] float _dragSideMaxPositionX = 0.3f;
    [Tooltip("Drag Side modunda zoom out çarpanı")]
    [SerializeField, Range(1f, 3f)] float _dragSideZoomMultiplier = 1.65f;
    [Tooltip("Drag Side: tam çekimde ulaşılacak Field of View (başlangıç = kameranın home FOV'u)")]
    [SerializeField, Range(12f, 90f)] float _dragSideMaxFieldOfView = 20f;
    [Tooltip("Drag Side: yatay (sağ/sol) çekimin kamera etkisi ağırlığı")]
    [SerializeField, Range(0f, 1f)] float _dragSideHorizontalInfluence = 0.8f;
    [Tooltip("Drag Side: dikey (ileri/geri) çekimin kamera etkisi ağırlığı")]
    [SerializeField, Range(0f, 1f)] float _dragSideVerticalInfluence = 0.2f;

    [Header("Edge Camera Shared")]
    [SerializeField] float _edgeRestoreDuration = 0.32f;

    Camera _camera;
    Vector3 _homePosition;
    Quaternion _homeRotation;
    float _homePitchRad;
    float _homeFieldOfView;
    float _currentOffset;
    bool _wasOffset;
    bool _edgeAssistActive;
    EdgeCameraMode _activeEdgeMode;
    CoinDragController _edgeCoin;
    Vector3 _edgeCoinAnchor;
    Vector3 _edgeShotDirection;
    Vector3 _dragSidePullDirection;
    CameraEdgeZoneSide _dragSideZone;
    float _edgePullRatio;
    Coroutine _edgeRestoreRoutine;

    public bool IsEdgeAssistActive => _edgeAssistActive;

    void Awake()
    {
        _camera = GetComponent<Camera>();
    }

    void OnEnable()
    {
        MatchIntroCameraFlythrough.Finished += CaptureHome;
    }

    void OnDisable()
    {
        MatchIntroCameraFlythrough.Finished -= CaptureHome;
        StopEdgeRestore();
        _edgeAssistActive = false;
        _activeEdgeMode = EdgeCameraMode.None;
        _edgeCoin = null;
    }

    void Start()
    {
        if (!MatchIntroCameraFlythrough.IsActive)
        {
            CaptureHome();
        }
    }

    void CaptureHome()
    {
        _homePosition = transform.position;
        _homeRotation = transform.rotation;
        _homePitchRad = GetPitchRadians(_homeRotation);
        _homeFieldOfView = _camera != null ? _camera.fieldOfView : 60f;
        _currentOffset = 0f;
        _wasOffset = false;
    }

    void LateUpdate()
    {
        if (_edgeAssistActive)
        {
            if (_activeEdgeMode == EdgeCameraMode.DynamicZoom)
            {
                ApplyDynamicZoomFollow();
            }
            else if (_activeEdgeMode == EdgeCameraMode.DragSide)
            {
                ApplyDragSideFollow();
            }

            return;
        }

        if (_currentOffset < 0.001f)
        {
            _currentOffset = 0f;
        }

        if (_currentOffset == 0f)
        {
            if (_wasOffset)
            {
                transform.position = _homePosition;
                _wasOffset = false;
            }
        }
        else
        {
            _wasOffset = true;
            transform.position = _homePosition - transform.forward * _currentOffset;
        }
    }

    public void SetDragState(float pullRatio, float sideRatio = 0f)
    {
        if (_edgeAssistActive && _activeEdgeMode == EdgeCameraMode.DynamicZoom)
        {
            return;
        }

        float maxPullBack = _maxPullBack;
        if (_edgeAssistActive && _activeEdgeMode == EdgeCameraMode.DragSide)
        {
            pullRatio = ApplyDragSideDirectionalPullRatio(pullRatio, _dragSidePullDirection);
            maxPullBack *= _dragSideZoomMultiplier;
        }

        ApplyPullZoom(pullRatio, sideRatio, maxPullBack);
    }

    void ApplyPullZoom(float pullRatio, float sideRatio, float maxPullBack)
    {
        float easedPull = Mathf.Pow(Mathf.Clamp01(pullRatio), _zoomCurveExponent);
        float boost = Mathf.Lerp(1f, _sideZoomBoost, sideRatio);
        float target = maxPullBack * easedPull * boost;
        _currentOffset = Mathf.Lerp(_currentOffset, target, _lerpSpeed * Time.deltaTime);
    }

    public void TryBeginEdgeAssist(CoinDragController coin)
    {
        EdgeCameraMode mode = ResolveEdgeCameraMode();
        if (mode == EdgeCameraMode.None || coin == null || _camera == null)
        {
            return;
        }

        CoinIdentity identity = coin.GetComponent<CoinIdentity>();
        if (!CameraEdgeZoneTracker.TryGetPlayerCoinZone(identity, out CameraEdgeZoneSide side))
        {
            return;
        }

        if (_edgeAssistActive && _edgeCoin == coin && _activeEdgeMode == mode)
        {
            return;
        }

        StopEdgeRestore();
        CaptureHome();

        _edgeCoin = coin;
        _edgeCoinAnchor = coin.transform.position;
        _edgePullRatio = 0f;
        _activeEdgeMode = mode;
        _edgeAssistActive = true;
        _currentOffset = 0f;
        _wasOffset = false;

        if (mode == EdgeCameraMode.DynamicZoom)
        {
            _edgeShotDirection = Vector3.zero;
        }
        else
        {
            _dragSideZone = side;
            _dragSidePullDirection = Vector3.zero;
        }
    }

    public void UpdateEdgeAssist(CoinDragController coin, Vector3 shotDirection, float pullRatio)
    {
        if (!_edgeAssistActive || coin == null || coin != _edgeCoin)
        {
            return;
        }

        _edgePullRatio = Mathf.Clamp01(pullRatio);

        if (_activeEdgeMode == EdgeCameraMode.DragSide)
        {
            shotDirection.y = 0f;
            if (shotDirection.sqrMagnitude > 0.0001f)
            {
                _dragSidePullDirection = shotDirection.normalized;
            }

            _edgePullRatio = ApplyDragSideDirectionalPullRatio(_edgePullRatio, _dragSidePullDirection);
            return;
        }

        if (_activeEdgeMode != EdgeCameraMode.DynamicZoom)
        {
            return;
        }

        _edgeCoinAnchor = coin.transform.position;

        shotDirection.y = 0f;
        if (shotDirection.sqrMagnitude > 0.0001f)
        {
            Vector3 newDirection = shotDirection.normalized;
            if (_edgeShotDirection.sqrMagnitude < 0.0001f)
            {
                _edgeShotDirection = newDirection;
            }
            else
            {
                float smooth = 1f - Mathf.Exp(-12f * Time.deltaTime);
                _edgeShotDirection = Vector3.Slerp(_edgeShotDirection, newDirection, smooth).normalized;
            }
        }
    }

    public void EndEdgeAssist()
    {
        if (!_edgeAssistActive)
        {
            return;
        }

        _edgeAssistActive = false;
        _activeEdgeMode = EdgeCameraMode.None;
        _edgeCoin = null;
        _edgeShotDirection = Vector3.zero;
        _dragSidePullDirection = Vector3.zero;
        _edgePullRatio = 0f;
        _currentOffset = 0f;
        _wasOffset = false;
        _edgeRestoreRoutine = StartCoroutine(RestoreHomeRoutine());
    }

    EdgeCameraMode ResolveEdgeCameraMode()
    {
        if (_dynamicZoom && _dragSide)
        {
            return EdgeCameraMode.None;
        }

        if (_dynamicZoom)
        {
            return EdgeCameraMode.DynamicZoom;
        }

        if (_dragSide)
        {
            return EdgeCameraMode.DragSide;
        }

        return EdgeCameraMode.None;
    }

    void ApplyDynamicZoomFollow()
    {
        if (_edgeCoin == null)
        {
            return;
        }

        if (_edgePullRatio < 0.0001f || _edgeShotDirection.sqrMagnitude < 0.0001f)
        {
            transform.SetPositionAndRotation(_homePosition, _homeRotation);
            return;
        }

        float aimBlend = EvaluateAimBlend(_edgePullRatio);
        float powerBlend = EvaluatePowerBlend(_edgePullRatio);

        Vector3 shotDirection = _edgeShotDirection;
        Vector3 focus = _edgeCoinAnchor + Vector3.up * _focusHeight;

        ComputeEdgeTargetPose(_edgeCoinAnchor, shotDirection, 0f, out Vector3 baseAimPosition, out Quaternion baseAimRotation);
        ComputeEdgeTargetPose(_edgeCoinAnchor, shotDirection, 1f, out Vector3 fullPowerPosition, out Quaternion fullPowerRotation);

        Vector3 homeOffset = _homePosition - focus;
        Vector3 baseOffset = baseAimPosition - focus;
        Vector3 powerOffset = fullPowerPosition - focus;
        Vector3 targetOffset = Vector3.Lerp(baseOffset, powerOffset, powerBlend);

        Vector3 blendedOffset = BlendOrbitOffset(homeOffset, targetOffset, aimBlend, aimBlend);
        Vector3 position = focus + blendedOffset;

        Quaternion targetRotation = Quaternion.Slerp(baseAimRotation, fullPowerRotation, powerBlend);
        Quaternion rotation = Quaternion.Slerp(_homeRotation, targetRotation, aimBlend);

        float zoom = _dynamicZoomMaxPullBack * Mathf.Pow(powerBlend, _zoomCurveExponent);
        position -= rotation * Vector3.forward * zoom;
        transform.SetPositionAndRotation(position, rotation);
    }

    float EvaluateAimBlend(float pullRatio)
    {
        return SmoothStep01(Mathf.InverseLerp(_edgeAimAlignStartPull, _edgeAimAlignEndPull, pullRatio));
    }

    float EvaluatePowerBlend(float pullRatio)
    {
        return SmoothStep01(Mathf.Pow(pullRatio, _edgeBlendCurveExponent) * _maxEdgeBlendAtFullPull);
    }

    static Vector3 BlendOrbitOffset(Vector3 from, Vector3 to, float aimBlend, float pullBlend)
    {
        if (from.sqrMagnitude < 0.0001f || to.sqrMagnitude < 0.0001f)
        {
            return Vector3.Lerp(from, to, pullBlend);
        }

        float fromMagnitude = from.magnitude;
        float toMagnitude = to.magnitude;
        Vector3 direction = Vector3.Slerp(from / fromMagnitude, to / toMagnitude, aimBlend);
        return direction * Mathf.Lerp(fromMagnitude, toMagnitude, pullBlend);
    }

    float ApplyDragSideDirectionalPullRatio(float pullRatio, Vector3 pullDirection)
    {
        pullDirection.y = 0f;
        if (pullDirection.sqrMagnitude < 0.0001f || _dragSideHorizontalInfluence < 0.0001f)
        {
            return Mathf.Clamp01(pullRatio);
        }

        pullDirection.Normalize();
        float horizontalPart = Mathf.Abs(pullDirection.x);
        float verticalPart = Mathf.Abs(pullDirection.z);
        float directionWeight = _dragSideHorizontalInfluence * horizontalPart
                                + _dragSideVerticalInfluence * verticalPart;
        directionWeight /= _dragSideHorizontalInfluence;

        return Mathf.Clamp01(pullRatio * directionWeight);
    }

    void ApplyDragSideFollow()
    {
        float pullRatio = Mathf.Clamp01(_edgePullRatio);
        float xShift = pullRatio * _dragSideMaxPositionX;
        if (_dragSideZone == CameraEdgeZoneSide.Left)
        {
            xShift = -xShift;
        }

        Vector3 position = _homePosition;
        position.x += xShift;
        position -= transform.forward * _currentOffset;
        transform.SetPositionAndRotation(position, _homeRotation);

        if (_camera != null)
        {
            _camera.fieldOfView = Mathf.Lerp(_homeFieldOfView, _dragSideMaxFieldOfView, pullRatio);
        }
    }

    void ComputeEdgeTargetPose(
        Vector3 coinPosition,
        Vector3 shotDirection,
        float powerBlend,
        out Vector3 position,
        out Quaternion rotation)
    {
        shotDirection.y = 0f;
        if (shotDirection.sqrMagnitude < 0.0001f)
        {
            shotDirection = Vector3.forward;
        }
        else
        {
            shotDirection.Normalize();
        }

        powerBlend = Mathf.Clamp01(powerBlend);

        Vector3 focus = coinPosition + Vector3.up * _focusHeight;
        float pitchReduceRad = _edgePitchReduceDegrees * powerBlend * Mathf.Deg2Rad;
        float pitchRad = Mathf.Max(10f * Mathf.Deg2Rad, _homePitchRad - pitchReduceRad);
        Vector3 behindShot = -shotDirection;
        float verticalRise = _edgeOrbitHorizontal * Mathf.Tan(pitchRad);
        float heightLowering = _edgeHeightLowering * powerBlend;

        position = focus + behindShot * _edgeOrbitHorizontal;
        position.y = focus.y + verticalRise - heightLowering;

        Vector3 lookDirection = focus - position;
        if (lookDirection.sqrMagnitude < 0.0001f)
        {
            lookDirection = Vector3.down;
        }

        rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
    }

    static Quaternion ApplyPitchOffset(Quaternion rotation, float pitchDeltaDegrees)
    {
        if (Mathf.Abs(pitchDeltaDegrees) < 0.001f)
        {
            return rotation;
        }

        Vector3 euler = rotation.eulerAngles;
        float pitch = euler.x > 180f ? euler.x - 360f : euler.x;
        return Quaternion.Euler(pitch + pitchDeltaDegrees, euler.y, euler.z);
    }

    static Vector3 GetDefaultShotDirection(CoinIdentity identity)
    {
        if (identity != null
            && CameraEdgeZoneTracker.TryGetPlayerCoinZone(identity, out CameraEdgeZoneSide side))
        {
            return side == CameraEdgeZoneSide.Left ? Vector3.right : Vector3.left;
        }

        return Vector3.forward;
    }

    static float GetPitchRadians(Quaternion rotation)
    {
        Vector3 forward = rotation * Vector3.forward;
        float horizontal = new Vector2(forward.x, forward.z).magnitude;
        if (horizontal < 0.0001f)
        {
            return 58f * Mathf.Deg2Rad;
        }

        return Mathf.Atan2(-forward.y, horizontal);
    }

    IEnumerator RestoreHomeRoutine()
    {
        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        float startFieldOfView = _camera != null ? _camera.fieldOfView : _homeFieldOfView;
        float duration = Mathf.Max(0.01f, _edgeRestoreDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = SmoothStep01(elapsed / duration);
            transform.position = Vector3.Lerp(startPosition, _homePosition, t);
            transform.rotation = Quaternion.Slerp(startRotation, _homeRotation, t);
            if (_camera != null)
            {
                _camera.fieldOfView = Mathf.Lerp(startFieldOfView, _homeFieldOfView, t);
            }
            yield return null;
        }

        transform.SetPositionAndRotation(_homePosition, _homeRotation);
        if (_camera != null)
        {
            _camera.fieldOfView = _homeFieldOfView;
        }
        _edgeRestoreRoutine = null;
    }

    void StopEdgeRestore()
    {
        if (_edgeRestoreRoutine == null)
        {
            return;
        }

        StopCoroutine(_edgeRestoreRoutine);
        _edgeRestoreRoutine = null;
    }

    static float SmoothStep01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}

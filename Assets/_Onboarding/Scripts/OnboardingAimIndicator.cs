using UnityEngine;

[DisallowMultipleComponent]
public class OnboardingAimIndicator : MonoBehaviour
{
    [SerializeField] float _lineHeightOffset = 0.006f;
    [SerializeField] float _trackLength = 0.34f;
    [SerializeField] float _trackWidth = 0.0045f;
    [SerializeField] float _sweetZoneWidth = 0.014f;
    [SerializeField] float _ghostWidth = 0.006f;
    [SerializeField] float _launchWidth = 0.011f;
    [SerializeField] float _pullWidth = 0.007f;
    [SerializeField] float _arrowHeadLength = 0.035f;
    [SerializeField] float _arrowHeadWidth = 0.028f;
    [SerializeField] int _angleArcSegments = 10;

    [SerializeField] Color _trackColor = new(1f, 1f, 1f, 0.22f);
    [SerializeField] Color _sweetZoneIdleColor = new(0.35f, 0.78f, 1f, 0.72f);
    [SerializeField] Color _sweetZonePowerColor = new(0.95f, 0.88f, 0.2f, 0.95f);
    [SerializeField] Color _sweetZoneReadyColor = new(0.1f, 1f, 0.35f, 1f);
    [SerializeField] Color _ghostColor = new(1f, 1f, 1f, 0.42f);
    [SerializeField] Color _pullColor = new(1f, 1f, 1f, 0.82f);
    [SerializeField] Color _launchBadDirectionColor = new(1f, 0.38f, 0.32f, 0.98f);
    [SerializeField] Color _launchBadPowerColor = new(1f, 0.82f, 0.18f, 0.98f);
    [SerializeField] Color _launchReadyColor = new(0.08f, 1f, 0.38f, 1f);
    [SerializeField] Color _angleArcColor = new(1f, 0.55f, 0.35f, 0.75f);

    LineRenderer _trackLine;
    LineRenderer _sweetZoneLine;
    LineRenderer _ghostShaftLine;
    LineRenderer _ghostHeadLeft;
    LineRenderer _ghostHeadRight;
    LineRenderer _pullLine;
    LineRenderer _launchShaftLine;
    LineRenderer _launchHeadLeft;
    LineRenderer _launchHeadRight;
    LineRenderer _angleArcLine;

    Material _lineMaterial;

    Transform _anchorTransform;
    Vector3 _guideAnchor;
    Vector3 _guideDirection;
    float _targetPullDistance;
    float _pullTolerance;
    float _directionToleranceDegrees;
    bool _guideActive;
    bool _isAiming;

    void Awake()
    {
        _lineMaterial = CreateVertexColorMaterial();
        _trackLine = CreateLine("PowerTrack", _trackWidth, _trackWidth);
        _sweetZoneLine = CreateLine("SweetZone", _sweetZoneWidth, _sweetZoneWidth);
        _ghostShaftLine = CreateLine("GhostShaft", _ghostWidth, _ghostWidth * 0.7f);
        _ghostHeadLeft = CreateLine("GhostHeadLeft", _ghostWidth, _ghostWidth * 0.45f);
        _ghostHeadRight = CreateLine("GhostHeadRight", _ghostWidth, _ghostWidth * 0.45f);
        _pullLine = CreateLine("PullElastic", _pullWidth, _pullWidth * 0.75f);
        _launchShaftLine = CreateLine("LaunchShaft", _launchWidth, _launchWidth * 0.65f);
        _launchHeadLeft = CreateLine("LaunchHeadLeft", _launchWidth, _launchWidth * 0.5f);
        _launchHeadRight = CreateLine("LaunchHeadRight", _launchWidth, _launchWidth * 0.5f);
        _angleArcLine = CreateLine("AngleArc", _trackWidth * 1.4f, _trackWidth * 1.4f, 32);

        SetGuideVisible(false);
        SetAimVisible(false);
    }

    void LateUpdate()
    {
        if (!_guideActive || _isAiming || _anchorTransform == null)
        {
            return;
        }

        _guideAnchor = _anchorTransform.position;
        RefreshIdleGuide();
    }

    public void BeginAim()
    {
        _isAiming = true;
        SetAimVisible(false);
        RefreshIdleGuide();
    }

    public void UpdateVisual(
        Vector3 anchor,
        Vector3 pullPoint,
        Vector3 launchDirection,
        float pullDistance,
        OnboardingAimFeedback feedback)
    {
        _guideAnchor = anchor;
        Vector3 elevatedAnchor = Elevate(anchor);
        Vector3 elevatedPull = Elevate(pullPoint);

        Vector3 targetDirection = NormalizeFlat(_guideDirection);
        Vector3 currentDirection = NormalizeFlat(launchDirection);

        RefreshTargetOverlay(elevatedAnchor, targetDirection);

        ApplyLineColor(_pullLine, _pullColor);
        _pullLine.SetPosition(0, elevatedAnchor);
        _pullLine.SetPosition(1, elevatedPull);

        Color launchColor = GetLaunchColor(feedback);
        ApplyLineColor(_launchShaftLine, launchColor);
        ApplyLineColor(_launchHeadLeft, launchColor);
        ApplyLineColor(_launchHeadRight, launchColor);

        Vector3 launchEnd = elevatedAnchor + currentDirection * pullDistance;
        _launchShaftLine.SetPosition(0, elevatedAnchor);
        _launchShaftLine.SetPosition(1, launchEnd);
        SetArrowHead(_launchHeadLeft, _launchHeadRight, elevatedAnchor, launchEnd, currentDirection, _arrowHeadLength, _arrowHeadWidth);

        UpdateSweetZone(feedback);
        UpdateAngleArc(elevatedAnchor, targetDirection, currentDirection, feedback);

        SetGuideVisible(true);
        SetAimVisible(true);
    }

    public void ConfigureGuide(
        Transform anchor,
        Vector3 launchDirection,
        float targetPullDistance,
        float pullTolerance,
        float directionToleranceDegrees)
    {
        _anchorTransform = anchor;
        _guideAnchor = anchor != null ? anchor.position : Vector3.zero;
        _guideDirection = launchDirection;
        _targetPullDistance = targetPullDistance;
        _pullTolerance = pullTolerance;
        _directionToleranceDegrees = directionToleranceDegrees;
        _guideActive = true;
        _isAiming = false;
        RefreshIdleGuide();
    }

    public void HideGuide()
    {
        _guideActive = false;
        _anchorTransform = null;
        SetGuideVisible(false);
        SetAimVisible(false);
    }

    public void Hide()
    {
        _isAiming = false;
        SetAimVisible(false);
        HideGuide();
    }

    public void EndAim()
    {
        _isAiming = false;
        SetAimVisible(false);

        if (_guideActive)
        {
            RefreshIdleGuide();
        }
    }

    void RefreshIdleGuide()
    {
        if (!_guideActive)
        {
            return;
        }

        Vector3 anchor = Elevate(_guideAnchor);
        Vector3 direction = NormalizeFlat(_guideDirection);
        RefreshTargetOverlay(anchor, direction);
        UpdateSweetZone(default);
        _angleArcLine.enabled = false;
        SetGuideVisible(true);
    }

    void RefreshTargetOverlay(Vector3 anchor, Vector3 direction)
    {
        Vector3 trackEnd = anchor + direction * _trackLength;
        Vector3 ghostEnd = anchor + direction * _targetPullDistance;

        ApplyLineColor(_trackLine, _trackColor);
        _trackLine.SetPosition(0, anchor);
        _trackLine.SetPosition(1, trackEnd);

        ApplyLineColor(_ghostShaftLine, _ghostColor);
        ApplyLineColor(_ghostHeadLeft, _ghostColor);
        ApplyLineColor(_ghostHeadRight, _ghostColor);
        _ghostShaftLine.SetPosition(0, anchor);
        _ghostShaftLine.SetPosition(1, ghostEnd);
        SetArrowHead(_ghostHeadLeft, _ghostHeadRight, anchor, ghostEnd, direction, _arrowHeadLength, _arrowHeadWidth);
    }

    void UpdateSweetZone(OnboardingAimFeedback feedback)
    {
        Vector3 anchor = Elevate(_guideAnchor);
        Vector3 direction = NormalizeFlat(_guideDirection);
        float zoneMin = Mathf.Max(0.02f, _targetPullDistance - _pullTolerance);
        float zoneMax = Mathf.Min(_trackLength, _targetPullDistance + _pullTolerance);

        Color zoneColor = _sweetZoneIdleColor;
        if (feedback.IsFullyValid)
        {
            zoneColor = _sweetZoneReadyColor;
        }
        else if (feedback.HasAim && feedback.DirectionValid && !feedback.PowerValid)
        {
            zoneColor = _sweetZonePowerColor;
        }

        ApplyLineColor(_sweetZoneLine, zoneColor);
        _sweetZoneLine.SetPosition(0, anchor + direction * zoneMin);
        _sweetZoneLine.SetPosition(1, anchor + direction * zoneMax);
    }

    void UpdateAngleArc(Vector3 anchor, Vector3 targetDirection, Vector3 currentDirection, OnboardingAimFeedback feedback)
    {
        if (!feedback.HasAim || feedback.DirectionValid)
        {
            _angleArcLine.enabled = false;
            return;
        }

        float angle = Vector3.SignedAngle(targetDirection, currentDirection, Vector3.up);
        if (Mathf.Abs(angle) < 2f)
        {
            _angleArcLine.enabled = false;
            return;
        }

        float radius = 0.08f;
        int pointCount = _angleArcSegments + 1;
        EnsureLinePointCount(_angleArcLine, pointCount);

        for (int i = 0; i < pointCount; i++)
        {
            float t = i / (float)_angleArcSegments;
            float stepAngle = Mathf.Lerp(0f, angle, t);
            Vector3 dir = Quaternion.AngleAxis(stepAngle, Vector3.up) * targetDirection;
            _angleArcLine.SetPosition(i, anchor + dir * radius);
        }

        ApplyLineColor(_angleArcLine, _angleArcColor);
        _angleArcLine.enabled = true;
    }

    Color GetLaunchColor(OnboardingAimFeedback feedback)
    {
        if (feedback.IsFullyValid)
        {
            return _launchReadyColor;
        }

        if (feedback.HasAim && feedback.DirectionValid)
        {
            return _launchBadPowerColor;
        }

        return _launchBadDirectionColor;
    }

    static void SetArrowHead(
        LineRenderer leftWing,
        LineRenderer rightWing,
        Vector3 anchor,
        Vector3 tip,
        Vector3 direction,
        float headLength,
        float headWidth)
    {
        Vector3 forward = NormalizeFlat(direction);
        Vector3 back = tip - forward * headLength;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized * headWidth * 0.5f;

        leftWing.SetPosition(0, tip);
        leftWing.SetPosition(1, back + right);
        rightWing.SetPosition(0, tip);
        rightWing.SetPosition(1, back - right);
    }

    void SetGuideVisible(bool visible)
    {
        _trackLine.enabled = visible;
        _sweetZoneLine.enabled = visible;
        _ghostShaftLine.enabled = visible;
        _ghostHeadLeft.enabled = visible;
        _ghostHeadRight.enabled = visible;
    }

    void SetAimVisible(bool visible)
    {
        _pullLine.enabled = visible;
        _launchShaftLine.enabled = visible;
        _launchHeadLeft.enabled = visible;
        _launchHeadRight.enabled = visible;

        if (!visible)
        {
            _angleArcLine.enabled = false;
        }
    }

    static void ApplyLineColor(LineRenderer lineRenderer, Color color)
    {
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
    }

    static void EnsureLinePointCount(LineRenderer lineRenderer, int pointCount)
    {
        if (lineRenderer.positionCount != pointCount)
        {
            lineRenderer.positionCount = pointCount;
        }
    }

    Vector3 Elevate(Vector3 position)
    {
        return new Vector3(position.x, position.y + _lineHeightOffset, position.z);
    }

    static Vector3 NormalizeFlat(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return Vector3.forward;
        }

        return direction.normalized;
    }

    LineRenderer CreateLine(string objectName, float startWidth, float endWidth, int pointCount = 2)
    {
        var lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);

        var lineRenderer = lineObject.AddComponent<LineRenderer>();
        lineRenderer.positionCount = pointCount;
        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = startWidth;
        lineRenderer.endWidth = endWidth;
        lineRenderer.numCapVertices = 5;
        lineRenderer.numCornerVertices = 3;
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.material = _lineMaterial;
        lineRenderer.enabled = false;
        return lineRenderer;
    }

    static Material CreateVertexColorMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        Material material = new Material(shader);
        material.color = Color.white;
        material.renderQueue = 3100;
        return material;
    }
}

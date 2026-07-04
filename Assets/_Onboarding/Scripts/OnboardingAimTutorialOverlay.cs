using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class OnboardingAimTutorialOverlay : MonoBehaviour
{
    [SerializeField] float _lineHeightOffset = 0.004f;
    [SerializeField] float _angleGuideLineLength = 1.35f;
    [SerializeField] float _angleGuideLineWidth = 0.012f;
    [SerializeField] float _dashLength = 0.08f;
    [SerializeField] float _dashGap = 0.05f;
    [SerializeField] Color _angleGuideColor = new(1f, 1f, 1f, 0.85f);
    [SerializeField] Color _angleGuideAlignedColor = new(0.15f, 0.88f, 0.28f, 0.92f);
    [SerializeField] float _ghostLineWidth = 0.01f;
    [SerializeField] Color _ghostLineColor = new(0.35f, 0.75f, 1f, 0.55f);
    [SerializeField] Color _ghostGlowColor = new(0.35f, 0.75f, 1f, 0.28f);

    LineRenderer _leftAngleLine;
    LineRenderer _rightAngleLine;
    LineRenderer _ghostGlowLine;
    LineRenderer _ghostCoreLine;
    LineRenderer _ghostTargetRing;

    Material _solidMaterial;
    Material _glowMaterial;
    bool _isInitialized;

    void Awake()
    {
        EnsureInitialized();
        HideAll();
    }

    public void EnsureInitialized()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        _solidMaterial = CreateLineMaterial(additiveGlow: false);
        _glowMaterial = CreateLineMaterial(additiveGlow: true);

        _leftAngleLine = CreateGuideLine("AngleGuideLeft");
        _rightAngleLine = CreateGuideLine("AngleGuideRight");
        _ghostGlowLine = CreateGuideLine("GhostPowerGlow");
        _ghostCoreLine = CreateGuideLine("GhostPowerCore");
        _ghostTargetRing = CreateGuideLine("GhostPowerTarget");
        _ghostTargetRing.loop = true;
    }

    void OnDestroy()
    {
        if (_solidMaterial != null)
        {
            Destroy(_solidMaterial);
        }

        if (_glowMaterial != null)
        {
            Destroy(_glowMaterial);
        }
    }

    public float AngleGuideLineLength => _angleGuideLineLength;

    public static Vector3 GetAngleGuideArrowAnchor(
        Vector3 coinPosition,
        Vector3 goalPosition,
        float halfAngleDegrees,
        float distanceFromCoin)
    {
        Vector3 centerDirection = GetMidAngleDirection(coinPosition, goalPosition);
        Vector3 leftDirection = Quaternion.Euler(0f, -halfAngleDegrees, 0f) * centerDirection;
        Vector3 rightDirection = Quaternion.Euler(0f, halfAngleDegrees, 0f) * centerDirection;

        Vector3 leftPoint = coinPosition + leftDirection * distanceFromCoin;
        Vector3 rightPoint = coinPosition + rightDirection * distanceFromCoin;
        return (leftPoint + rightPoint) * 0.5f;
    }

    public void ShowAngleGuides(Vector3 coinPosition, Vector3 goalPosition, float halfAngleDegrees)
    {
        ShowAngleGuides(coinPosition, goalPosition, halfAngleDegrees, _angleGuideColor);
    }

    public void ShowAngleGuides(
        Vector3 coinPosition,
        Vector3 goalPosition,
        float halfAngleDegrees,
        Color lineColor)
    {
        EnsureInitialized();

        Vector3 centerDirection = Flatten(goalPosition - coinPosition);
        if (centerDirection.sqrMagnitude < 0.0001f)
        {
            centerDirection = Vector3.forward;
        }
        else
        {
            centerDirection.Normalize();
        }

        Vector3 leftDirection = Quaternion.Euler(0f, -halfAngleDegrees, 0f) * centerDirection;
        Vector3 rightDirection = Quaternion.Euler(0f, halfAngleDegrees, 0f) * centerDirection;

        ApplyDashedLine(_leftAngleLine, coinPosition, leftDirection, _angleGuideLineLength, lineColor);
        ApplyDashedLine(_rightAngleLine, coinPosition, rightDirection, _angleGuideLineLength, lineColor);
    }

    public void HideAngleGuides()
    {
        if (_leftAngleLine != null)
        {
            _leftAngleLine.enabled = false;
        }

        if (_rightAngleLine != null)
        {
            _rightAngleLine.enabled = false;
        }
    }

    public void ShowPowerGuide(CoinAimIndicator.PathVisual path, float targetRingRadius)
    {
        EnsureInitialized();

        Vector3 start = Elevate(path.PrimaryStart);
        Vector3 end = Elevate(path.PrimaryEnd);

        ApplySolidSegment(_ghostGlowLine, start, end, _ghostLineWidth * 2.4f, _ghostGlowColor, _glowMaterial);
        ApplySolidSegment(_ghostCoreLine, start, end, _ghostLineWidth, _ghostLineColor, _solidMaterial);
        ApplyRing(_ghostTargetRing, end, targetRingRadius, _ghostLineWidth * 1.2f, _ghostLineColor);
    }

    public void HidePowerGuide()
    {
        if (_ghostGlowLine != null)
        {
            _ghostGlowLine.enabled = false;
        }

        if (_ghostCoreLine != null)
        {
            _ghostCoreLine.enabled = false;
        }

        if (_ghostTargetRing != null)
        {
            _ghostTargetRing.enabled = false;
        }
    }

    public void HideAll()
    {
        HideAngleGuides();
        HidePowerGuide();
    }

    public static bool IsDirectionWithinAngleRange(Vector3 aimDirection, Vector3 centerDirection, float halfAngleDegrees)
    {
        aimDirection = Flatten(aimDirection);
        centerDirection = Flatten(centerDirection);
        if (aimDirection.sqrMagnitude < 0.0001f || centerDirection.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        float signedAngle = Vector3.SignedAngle(centerDirection, aimDirection, Vector3.up);
        return Mathf.Abs(signedAngle) <= halfAngleDegrees;
    }

    public static Vector3 GetMidAngleDirection(Vector3 coinPosition, Vector3 goalPosition)
    {
        Vector3 direction = Flatten(goalPosition - coinPosition);
        return direction.sqrMagnitude < 0.0001f ? Vector3.forward : direction.normalized;
    }

    public static float ComparePowerAlongDirection(
        Vector3 coinPosition,
        Vector3 aimDirection,
        Vector3 playerTarget,
        Vector3 guideTarget)
    {
        aimDirection = Flatten(aimDirection);
        if (aimDirection.sqrMagnitude < 0.0001f)
        {
            return 0f;
        }

        aimDirection.Normalize();
        float playerDistance = Vector3.Dot(Flatten(playerTarget - coinPosition), aimDirection);
        float guideDistance = Vector3.Dot(Flatten(guideTarget - coinPosition), aimDirection);
        return playerDistance - guideDistance;
    }

    public static bool IsEndpointNearTarget(Vector3 playerEndpoint, Vector3 targetEndpoint, float matchRadius)
    {
        if (matchRadius <= 0f)
        {
            return false;
        }

        playerEndpoint = Flatten(playerEndpoint);
        targetEndpoint = Flatten(targetEndpoint);
        return (playerEndpoint - targetEndpoint).sqrMagnitude <= matchRadius * matchRadius;
    }

    void ApplyDashedLine(LineRenderer lineRenderer, Vector3 origin, Vector3 direction, float length, Color color)
    {
        if (length <= 0.001f)
        {
            lineRenderer.enabled = false;
            return;
        }

        direction.Normalize();
        int positionCount = Mathf.Max(2, Mathf.CeilToInt(length / (_dashLength + _dashGap)) * 2);
        if (lineRenderer.positionCount != positionCount)
        {
            lineRenderer.positionCount = positionCount;
        }

        float traveled = 0f;
        int index = 0;
        while (traveled < length && index < positionCount - 1)
        {
            float dashEnd = Mathf.Min(traveled + _dashLength, length);
            lineRenderer.SetPosition(index++, Elevate(origin + direction * traveled));
            lineRenderer.SetPosition(index++, Elevate(origin + direction * dashEnd));
            traveled += _dashLength + _dashGap;
        }

        if (index < positionCount)
        {
            lineRenderer.positionCount = index;
        }

        ApplyLineColor(lineRenderer, color);
        lineRenderer.startWidth = _angleGuideLineWidth;
        lineRenderer.endWidth = _angleGuideLineWidth;
        lineRenderer.enabled = positionCount >= 2;
    }

    void ApplySolidSegment(
        LineRenderer lineRenderer,
        Vector3 start,
        Vector3 end,
        float width,
        Color color,
        Material material)
    {
        if (Vector3.Distance(start, end) <= 0.001f)
        {
            lineRenderer.enabled = false;
            return;
        }

        lineRenderer.material = material;
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);
        ApplyLineColor(lineRenderer, color);
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width * 0.95f;
        lineRenderer.enabled = true;
    }

    void ApplyRing(LineRenderer lineRenderer, Vector3 center, float radius, float width, Color color)
    {
        if (radius <= 0.001f)
        {
            lineRenderer.enabled = false;
            return;
        }

        const int segments = 32;
        if (lineRenderer.positionCount != segments)
        {
            lineRenderer.positionCount = segments;
        }

        float y = center.y;
        for (int i = 0; i < segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            lineRenderer.SetPosition(
                i,
                new Vector3(center.x + Mathf.Cos(angle) * radius, y, center.z + Mathf.Sin(angle) * radius));
        }

        ApplyLineColor(lineRenderer, color);
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;
        lineRenderer.enabled = true;
    }

    LineRenderer CreateGuideLine(string objectName)
    {
        var lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);

        var lineRenderer = lineObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.numCapVertices = 4;
        lineRenderer.numCornerVertices = 0;
        lineRenderer.material = _solidMaterial;
        lineRenderer.enabled = false;
        return lineRenderer;
    }

    static void ApplyLineColor(LineRenderer lineRenderer, Color color)
    {
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
    }

    Vector3 Elevate(Vector3 position)
    {
        return new Vector3(position.x, position.y + _lineHeightOffset, position.z);
    }

    static Vector3 Flatten(Vector3 vector)
    {
        vector.y = 0f;
        return vector;
    }

    static Material CreateLineMaterial(bool additiveGlow)
    {
        Shader shader = Shader.Find("Hidden/Internal-Colored");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        Material material = new Material(shader);
        material.hideFlags = HideFlags.HideAndDontSave;
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt(
            "_DstBlend",
            (int)(additiveGlow ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
        material.SetInt("_Cull", (int)CullMode.Off);
        material.SetInt("_ZWrite", 0);
        material.renderQueue = (int)RenderQueue.Transparent;
        return material;
    }
}

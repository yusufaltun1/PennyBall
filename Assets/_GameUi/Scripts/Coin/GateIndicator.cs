using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class GateIndicator : MonoBehaviour
{
    const int MaxDashSegments = 32;

    readonly List<LineRenderer> _dashLines = new(MaxDashSegments);

    LineRenderer _glowLine;
    Material _dashMaterial;
    Material _glowMaterial;
    CoinGateIndicatorSettings _activeSettings;

    CoinIdentity _gateCoinA;
    CoinIdentity _gateCoinB;
    Vector3 _gateStart;
    Vector3 _gateEnd;
    Color _lineColor;
    float _dashOffset;
    float _glowPulseTime;
    bool _isVisible;
    bool _animate;

    public static GateIndicator Instance { get; private set; }
    public bool IsVisible => _isVisible;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        _dashMaterial = CreateLineMaterial(false);
        _glowMaterial = CreateLineMaterial(true);
        _glowLine = CreateLineRenderer("GateGlow", _glowMaterial, GetGlowWidth());
        BuildDashPool();
        SetVisible(false);
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (_dashMaterial != null)
        {
            Destroy(_dashMaterial);
        }

        if (_glowMaterial != null)
        {
            Destroy(_glowMaterial);
        }
    }

    void OnEnable()
    {
        SubscribeRulesEvents();
    }

    void Start()
    {
        SubscribeRulesEvents();
    }

    void OnDisable()
    {
        UnsubscribeRulesEvents();
    }

    void SubscribeRulesEvents()
    {
        if (GameRulesManager.Instance == null)
        {
            return;
        }

        GameRulesManager.Instance.PlayerShotResolved -= OnPlayerShotResolved;
        GameRulesManager.Instance.RoundReset -= Hide;
        GameRulesManager.Instance.PlayerShotResolved += OnPlayerShotResolved;
        GameRulesManager.Instance.RoundReset += Hide;
    }

    void UnsubscribeRulesEvents()
    {
        if (GameRulesManager.Instance == null)
        {
            return;
        }

        GameRulesManager.Instance.PlayerShotResolved -= OnPlayerShotResolved;
        GameRulesManager.Instance.RoundReset -= Hide;
    }

    void Update()
    {
        if (!_isVisible)
        {
            return;
        }

        if (!TrySyncGatePositions())
        {
            Hide();
            return;
        }

        if (_animate)
        {
            _dashOffset += GetAnimationSpeed() * Time.deltaTime;
            _glowPulseTime += GetGlowPulseSpeed() * Time.deltaTime;
        }

        RebuildVisual();
    }

    void OnPlayerShotResolved(CoinIdentity coin, bool valid)
    {
        Hide();
    }

    public void Show(
        CoinIdentity gateCoinA,
        CoinIdentity gateCoinB,
        Color lineColor,
        CoinGateIndicatorSettings settings,
        bool animate)
    {
        if (gateCoinA == null || gateCoinB == null)
        {
            Hide();
            return;
        }

        ApplySettings(settings);
        _gateCoinA = gateCoinA;
        _gateCoinB = gateCoinB;
        _lineColor = lineColor;
        _dashOffset = 0f;
        _glowPulseTime = 0f;
        _animate = animate;
        _isVisible = true;
        TrySyncGatePositions();
        RebuildVisual();
        SetRenderersEnabled(true);
    }

    bool TrySyncGatePositions()
    {
        if (_gateCoinA == null || _gateCoinB == null)
        {
            return false;
        }

        _gateStart = _gateCoinA.transform.position;
        _gateEnd = _gateCoinB.transform.position;
        return true;
    }

    public void PauseAnimation()
    {
        _animate = false;
    }

    public void Hide()
    {
        _isVisible = false;
        _animate = false;
        _gateCoinA = null;
        _gateCoinB = null;
        SetRenderersEnabled(false);
    }

    void ApplySettings(CoinGateIndicatorSettings settings)
    {
        _activeSettings = settings;
        ApplyLineWidths();
    }

    void ApplyLineWidths()
    {
        float glowWidth = GetGlowWidth();
        _glowLine.startWidth = glowWidth;
        _glowLine.endWidth = glowWidth * 0.85f;

        float dashWidth = GetLineWidth();
        for (int i = 0; i < _dashLines.Count; i++)
        {
            _dashLines[i].startWidth = dashWidth;
            _dashLines[i].endWidth = dashWidth * 0.85f;
        }
    }

    void RebuildVisual()
    {
        Vector3 start = Elevate(_gateStart);
        Vector3 end = Elevate(_gateEnd);

        float glowAlpha = GetGlowAlpha();
        if (_animate)
        {
            float pulse = (Mathf.Sin(_glowPulseTime) + 1f) * 0.5f;
            glowAlpha += pulse * GetGlowPulseAmount();
        }

        Color glowColor = _lineColor;
        glowColor.a = glowAlpha;
        ApplyLineColors(_glowLine, glowColor, glowColor.a);
        _glowLine.SetPosition(0, start);
        _glowLine.SetPosition(1, end);

        RebuildDashes(start, end);
    }

    void RebuildDashes(Vector3 start, Vector3 end)
    {
        Vector3 delta = end - start;
        float totalLength = delta.magnitude;
        if (totalLength <= 0.001f)
        {
            DisableUnusedDashes(0);
            return;
        }

        Vector3 direction = delta / totalLength;
        float dashLength = GetDashLength();
        float dashGap = GetDashGap();
        float patternLength = dashLength + dashGap;
        float normalizedOffset = _dashOffset % patternLength;
        if (normalizedOffset < 0f)
        {
            normalizedOffset += patternLength;
        }

        float cursor = -normalizedOffset;
        int dashIndex = 0;

        while (cursor < totalLength && dashIndex < MaxDashSegments)
        {
            float dashStartDistance = Mathf.Max(0f, cursor);
            float dashEndDistance = Mathf.Min(totalLength, cursor + dashLength);
            if (dashEndDistance - dashStartDistance > 0.001f)
            {
                LineRenderer dashLine = _dashLines[dashIndex];
                dashLine.enabled = true;
                ApplyLineColors(dashLine, _lineColor, _lineColor.a * 0.75f);
                dashLine.SetPosition(0, start + direction * dashStartDistance);
                dashLine.SetPosition(1, start + direction * dashEndDistance);
                dashIndex++;
            }

            cursor += patternLength;
        }

        DisableUnusedDashes(dashIndex);
    }

    void DisableUnusedDashes(int startIndex)
    {
        for (int i = startIndex; i < _dashLines.Count; i++)
        {
            _dashLines[i].enabled = false;
        }
    }

    void SetRenderersEnabled(bool enabled)
    {
        _glowLine.enabled = enabled;
        for (int i = 0; i < _dashLines.Count; i++)
        {
            _dashLines[i].enabled = false;
        }
    }

    void SetVisible(bool visible)
    {
        _isVisible = visible;
        SetRenderersEnabled(visible);
    }

    Vector3 Elevate(Vector3 position)
    {
        return new Vector3(position.x, position.y + GetLineHeightOffset(), position.z);
    }

    void BuildDashPool()
    {
        for (int i = 0; i < MaxDashSegments; i++)
        {
            _dashLines.Add(CreateLineRenderer($"GateDash_{i}", _dashMaterial, GetLineWidth()));
        }
    }

    float GetLineHeightOffset() => _activeSettings != null ? _activeSettings.LineHeightOffset : 0.004f;
    float GetLineWidth() => _activeSettings != null ? _activeSettings.LineWidth : 0.05f;
    float GetDashLength() => _activeSettings != null ? _activeSettings.DashLength : 0.055f;
    float GetDashGap() => _activeSettings != null ? _activeSettings.DashGap : 0.04f;
    float GetAnimationSpeed() => _activeSettings != null ? _activeSettings.AnimationSpeed : 1.2f;
    float GetGlowWidthMultiplier() => _activeSettings != null ? _activeSettings.GlowWidthMultiplier : 3.2f;
    float GetGlowAlpha() => _activeSettings != null ? _activeSettings.GlowAlpha : 0.28f;
    float GetGlowPulseSpeed() => _activeSettings != null ? _activeSettings.GlowPulseSpeed : 2.5f;
    float GetGlowPulseAmount() => _activeSettings != null ? _activeSettings.GlowPulseAmount : 0.12f;
    float GetGlowWidth() => GetLineWidth() * GetGlowWidthMultiplier();

    static void ApplyLineColors(LineRenderer lineRenderer, Color startColor, float endAlpha)
    {
        Color endColor = startColor;
        endColor.a = endAlpha;
        lineRenderer.startColor = startColor;
        lineRenderer.endColor = endColor;
    }

    LineRenderer CreateLineRenderer(string objectName, Material material, float width)
    {
        var lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);

        var lineRenderer = lineObject.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 2;
        lineRenderer.useWorldSpace = true;
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width * 0.85f;
        lineRenderer.numCapVertices = 4;
        lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.material = material;
        lineRenderer.enabled = false;

        return lineRenderer;
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

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", Color.white);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", Color.white);
        }

        return material;
    }
}

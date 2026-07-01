using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class GateIndicator : MonoBehaviour
{
    const int DashTextureResolution = 64;

    LineRenderer _glowLine;
    LineRenderer _dashLine;
    Material _glowMaterial;
    Material _dashMaterial;
    Material _dashLineMaterialInstance;
    Texture2D _dashTexture;
    bool[] _dashPattern;
    CoinGateIndicatorSettings _activeSettings;

    CoinIdentity _gateCoinA;
    CoinIdentity _gateCoinB;
    Vector3 _gateStart;
    Vector3 _gateEnd;
    Color _lineColor;
    Color _glowColor;
    float _dashOffset;
    float _glowPulseTime;
    float _lastDashLength = -1f;
    float _lastDashGap = -1f;
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
        _glowMaterial = CreateLineMaterial(additiveGlow: true);
        _dashMaterial = CreateDashMaterial();
        _glowLine = CreateLineRenderer("GateGlow", _glowMaterial);
        _dashLine = CreateLineRenderer("GateDash", _dashMaterial);
        _dashLine.textureMode = LineTextureMode.Tile;
        _dashLineMaterialInstance = _dashLine.material;
        SetVisible(false);
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (_glowMaterial != null)
        {
            Destroy(_glowMaterial);
        }

        if (_dashMaterial != null)
        {
            Destroy(_dashMaterial);
        }

        if (_dashLineMaterialInstance != null)
        {
            Destroy(_dashLineMaterialInstance);
        }

        if (_dashTexture != null)
        {
            Destroy(_dashTexture);
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
        _lineColor = settings != null ? settings.LineColor : new Color(0.506f, 0.325f, 0.796f, 1f);
        _glowColor = settings != null ? settings.GlowColor : _lineColor;
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
        EnsureDashTexture();
        ApplyLineWidths();
    }

    void ApplyLineWidths()
    {
        float glowWidth = GetGlowWidth();
        _glowLine.startWidth = glowWidth;
        _glowLine.endWidth = glowWidth;

        float dashWidth = GetLineWidth();
        _dashLine.startWidth = dashWidth;
        _dashLine.endWidth = dashWidth;
    }

    void RebuildVisual()
    {
        Vector3 start = Elevate(_gateStart);
        Vector3 end = Elevate(_gateEnd);
        float totalLength = Vector3.Distance(start, end);
        if (totalLength <= 0.001f)
        {
            _dashLine.enabled = false;
            _glowLine.enabled = false;
            return;
        }

        EnsureDashTexture();

        float glowAlpha = GetGlowAlpha();
        if (_animate)
        {
            float pulse = (Mathf.Sin(_glowPulseTime) + 1f) * 0.5f;
            glowAlpha += pulse * GetGlowPulseAmount();
        }

        Color glowColor = _glowColor;
        glowColor.a = glowAlpha;
        ApplyLineColors(_glowLine, glowColor);
        _glowLine.SetPosition(0, start);
        _glowLine.SetPosition(1, end);
        _glowLine.enabled = true;

        float dashLength = GetDashLength();
        float dashGap = GetDashGap();
        float patternLength = Mathf.Max(dashLength + dashGap, 0.001f);

        ApplyLineColors(_dashLine, _lineColor);
        _dashLine.SetPosition(0, start);
        _dashLine.SetPosition(1, end);
        _dashLine.textureScale = new Vector2(totalLength / patternLength, 1f);

        float patternOffset = _dashOffset / patternLength;
        ApplyDashTextureScroll(patternOffset - Mathf.Floor(patternOffset));

        _dashLine.enabled = true;
    }

    void ApplyDashTextureScroll(float scrollU)
    {
        if (_dashTexture == null || _dashPattern == null)
        {
            return;
        }

        int shift = Mathf.FloorToInt(scrollU * DashTextureResolution) % DashTextureResolution;
        if (shift < 0)
        {
            shift += DashTextureResolution;
        }

        for (int x = 0; x < DashTextureResolution; x++)
        {
            int source = (x + shift) % DashTextureResolution;
            bool isDash = _dashPattern[source];
            _dashTexture.SetPixel(x, 0, isDash ? Color.white : new Color(1f, 1f, 1f, 0f));
        }

        _dashTexture.Apply(false, false);
    }

    void EnsureDashTexture()
    {
        float dashLength = GetDashLength();
        float dashGap = GetDashGap();
        if (_dashTexture != null
            && Mathf.Approximately(dashLength, _lastDashLength)
            && Mathf.Approximately(dashGap, _lastDashGap))
        {
            return;
        }

        if (_dashTexture != null)
        {
            Destroy(_dashTexture);
        }

        float pattern = Mathf.Max(dashLength + dashGap, 0.001f);
        int dashPixels = Mathf.Clamp(Mathf.RoundToInt(DashTextureResolution * (dashLength / pattern)), 1, DashTextureResolution - 1);

        _dashTexture = new Texture2D(DashTextureResolution, 1, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        _dashPattern = new bool[DashTextureResolution];
        for (int x = 0; x < DashTextureResolution; x++)
        {
            bool isDash = x < dashPixels;
            _dashPattern[x] = isDash;
            _dashTexture.SetPixel(x, 0, isDash ? Color.white : new Color(1f, 1f, 1f, 0f));
        }

        _dashTexture.Apply(false, false);
        if (_dashLineMaterialInstance != null)
        {
            if (_dashLineMaterialInstance.HasProperty("_MainTex"))
            {
                _dashLineMaterialInstance.SetTexture("_MainTex", _dashTexture);
            }

            if (_dashLineMaterialInstance.HasProperty("_BaseMap"))
            {
                _dashLineMaterialInstance.SetTexture("_BaseMap", _dashTexture);
            }
        }

        _lastDashLength = dashLength;
        _lastDashGap = dashGap;
    }

    void SetRenderersEnabled(bool enabled)
    {
        _glowLine.enabled = enabled;
        _dashLine.enabled = enabled;
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

    static void ApplyLineColors(LineRenderer lineRenderer, Color color)
    {
        lineRenderer.applyActiveColorSpace = false;
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        lineRenderer.colorGradient = CreateSolidGradient(color);
    }

    static Gradient CreateSolidGradient(Color color)
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(color, 0f),
                new GradientColorKey(color, 1f)
            },
            new[]
            {
                new GradientAlphaKey(color.a, 0f),
                new GradientAlphaKey(color.a, 1f)
            });
        return gradient;
    }

    LineRenderer CreateLineRenderer(string objectName, Material material)
    {
        var lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);

        var lineRenderer = lineObject.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 2;
        lineRenderer.useWorldSpace = true;
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.widthMultiplier = 1f;
        lineRenderer.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
        lineRenderer.numCapVertices = 0;
        lineRenderer.numCornerVertices = 0;
        lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.material = material;
        lineRenderer.enabled = false;
        ApplyLineColors(lineRenderer, Color.white);

        return lineRenderer;
    }

    static Material CreateDashMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Hidden/Internal-Colored");
        }

        Material material = new Material(shader);
        material.hideFlags = HideFlags.HideAndDontSave;
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_Cull", (int)CullMode.Off);
        material.SetInt("_ZWrite", 0);
        material.renderQueue = (int)RenderQueue.Transparent;

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", Color.white);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", Color.white);
        }

        return material;
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

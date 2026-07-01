using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[RequireComponent(typeof(CoinAimIndicatorSettings))]
public class CoinAimIndicator : MonoBehaviour
{
    public readonly struct PathVisual
    {
        public PathVisual(Vector3 primaryStart, Vector3 primaryEnd, bool hasBounce, Vector3 bounceEnd)
        {
            PrimaryStart = primaryStart;
            PrimaryEnd = primaryEnd;
            HasBounce = hasBounce;
            BounceEnd = bounceEnd;
        }

        public Vector3 PrimaryStart { get; }
        public Vector3 PrimaryEnd { get; }
        public bool HasBounce { get; }
        public Vector3 BounceEnd { get; }
    }

    sealed class ArrowStrip
    {
        public Mesh Mesh;
        public MeshRenderer Renderer;
        public readonly List<Vector3> Vertices = new(128);
        public readonly List<Vector2> Uvs = new(128);
        public readonly List<Color32> Colors = new(128);
        public readonly List<int> Triangles = new(256);
    }

    CoinAimIndicatorSettings _settings;

    LineRenderer _primaryGlowLine;
    LineRenderer _bounceGlowLine;
    LineRenderer _originGlowRing;
    LineRenderer _targetFillRing;
    LineRenderer _targetRingOuter;
    LineRenderer _targetRingInner;

    ArrowStrip _primaryArrows;
    ArrowStrip _bounceArrows;

    Material _glowMaterial;
    Material _solidMaterial;
    Material _arrowMaterial;

    Transform _arrowRoot;

    float _scrollOffset;
    float _power01;
    PathVisual _path;
    bool _isVisible;
    bool _hasArrowTexture;

    Color _arrowStartColor = Color.white;
    Color _arrowEndColor = Color.white;
    float _bounceArrowAlpha = 1f;

    public Color StartColor => _settings != null ? _settings.WeakPowerColor : Color.cyan;

    void Awake()
    {
        _settings = GetComponent<CoinAimIndicatorSettings>();
        _glowMaterial = CreateLineMaterial(additiveGlow: true);
        _solidMaterial = CreateLineMaterial(additiveGlow: false);
        _arrowMaterial = CreateArrowMaterial();

        _arrowRoot = new GameObject("AimArrowsRoot").transform;
        _arrowRoot.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        _arrowRoot.localScale = Vector3.one;

        _primaryGlowLine = CreateGlowLine("AimGlowPrimary");
        _bounceGlowLine = CreateGlowLine("AimGlowBounce");
        _primaryArrows = CreateArrowStrip("AimArrowsPrimary");
        _bounceArrows = CreateArrowStrip("AimArrowsBounce");

        int ringSegments = Mathf.Max(_settings.RingSegments, 8);
        _originGlowRing = CreateRingLine("AimOriginGlow", _glowMaterial, ringSegments);
        _targetFillRing = CreateRingLine("AimTargetFill", _solidMaterial, ringSegments);
        _targetRingInner = CreateRingLine("AimTargetRingInner", _solidMaterial, ringSegments);
        _targetRingOuter = CreateRingLine("AimTargetRingOuter", _solidMaterial, ringSegments);

        RefreshArrowTextureBinding();
        SetVisible(false);
    }

    void OnDestroy()
    {
        if (_glowMaterial != null)
        {
            Destroy(_glowMaterial);
        }

        if (_solidMaterial != null)
        {
            Destroy(_solidMaterial);
        }

        if (_arrowMaterial != null)
        {
            Destroy(_arrowMaterial);
        }

        DestroyArrowStrip(_primaryArrows);
        DestroyArrowStrip(_bounceArrows);

        if (_arrowRoot != null)
        {
            Destroy(_arrowRoot.gameObject);
        }
    }

    void Update()
    {
        if (!_isVisible || !_hasArrowTexture)
        {
            return;
        }

        float direction = _settings.ReverseArrowAnimation ? -1f : 1f;
        _scrollOffset += _settings.AnimationSpeed * direction * Time.deltaTime;
        RebuildArrowMeshes();
    }

    public void UpdateVisual(PathVisual path, float power01)
    {
        _path = path;
        _power01 = Mathf.Clamp01(power01);
        _isVisible = true;

        RefreshArrowTextureBinding();
        RebuildVisual();
    }

    public void Hide()
    {
        _isVisible = false;
        SetVisible(false);
    }

    void RebuildVisual()
    {
        Color startColor = _settings.WeakPowerColor;
        Color endColor = Color.Lerp(_settings.WeakPowerColor, _settings.MaxPowerColor, _power01);
        endColor.a *= _settings.EndAlphaFactor;

        Color glowStart = startColor;
        glowStart.a *= _settings.GlowAlpha;
        Color glowEnd = endColor;
        glowEnd.a *= _settings.GlowAlpha;

        _arrowStartColor = Color.white;
        _arrowEndColor = Color.white;
        _bounceArrowAlpha = 1f;

        Vector3 primaryStart = Elevate(_path.PrimaryStart);
        Vector3 primaryEnd = Elevate(_path.PrimaryEnd);
        float primaryLength = Vector3.Distance(primaryStart, primaryEnd);

        ApplyGlowSegment(_primaryGlowLine, primaryStart, primaryEnd, primaryLength, glowStart, glowEnd);

        Color targetColor = endColor;
        targetColor.a = _settings.TargetAlpha;
        ApplyTargetMarkers(primaryEnd, targetColor);

        Color originColor = startColor;
        originColor.a = _settings.OriginGlowAlpha;
        ApplyOriginGlow(primaryStart, originColor);

        if (_path.HasBounce)
        {
            Color bounceGlowStart = glowStart;
            bounceGlowStart.a *= _settings.BounceAlphaFactor;
            Color bounceGlowEnd = glowEnd;
            bounceGlowEnd.a *= _settings.BounceAlphaFactor;

            Vector3 bounceStartPos = primaryEnd;
            Vector3 bounceEndPos = Elevate(_path.BounceEnd);
            float bounceLength = Vector3.Distance(bounceStartPos, bounceEndPos);

            ApplyGlowSegment(_bounceGlowLine, bounceStartPos, bounceEndPos, bounceLength, bounceGlowStart, bounceGlowEnd);
            _bounceArrowAlpha = _settings.BounceAlphaFactor;
        }
        else
        {
            _bounceGlowLine.enabled = false;
            ClearArrowStrip(_bounceArrows);
        }

        RebuildArrowMeshes();
    }

    void RebuildArrowMeshes()
    {
        if (!_hasArrowTexture)
        {
            ClearArrowStrip(_primaryArrows);
            ClearArrowStrip(_bounceArrows);
            return;
        }

        Vector3 primaryStart = Elevate(_path.PrimaryStart);
        Vector3 primaryEnd = Elevate(_path.PrimaryEnd);
        float primaryLength = Vector3.Distance(primaryStart, primaryEnd);
        BuildArrowStrip(_primaryArrows, primaryStart, primaryEnd, primaryLength, _arrowStartColor, _arrowEndColor, 1f);

        if (_path.HasBounce)
        {
            Vector3 bounceStartPos = primaryEnd;
            Vector3 bounceEndPos = Elevate(_path.BounceEnd);
            float bounceLength = Vector3.Distance(bounceStartPos, bounceEndPos);
            BuildArrowStrip(
                _bounceArrows,
                bounceStartPos,
                bounceEndPos,
                bounceLength,
                _arrowStartColor,
                _arrowEndColor,
                _bounceArrowAlpha);
        }
        else
        {
            ClearArrowStrip(_bounceArrows);
        }
    }

    void BuildArrowStrip(
        ArrowStrip strip,
        Vector3 start,
        Vector3 end,
        float length,
        Color startColor,
        Color endColor,
        float alphaMultiplier)
    {
        strip.Vertices.Clear();
        strip.Uvs.Clear();
        strip.Colors.Clear();
        strip.Triangles.Clear();

        if (length <= 0.001f)
        {
            strip.Renderer.enabled = false;
            return;
        }

        Vector3 direction = (end - start) / length;
        Vector3 across = GetPathAcross(direction);
        float arrowLength = _settings.GetArrowLength();
        float step = _settings.GetArrowStep();
        float scroll = Mathf.Repeat(_scrollOffset, step);

        for (float distance = scroll; distance < length + arrowLength; distance += step)
        {
            float centerDistance = distance + arrowLength * 0.5f;
            if (centerDistance + arrowLength * 0.5f < 0f || centerDistance - arrowLength * 0.5f > length)
            {
                continue;
            }

            Vector3 center = start + direction * centerDistance;
            float t = Mathf.Clamp01(centerDistance / length);
            Color color = Color.Lerp(startColor, endColor, t);
            color.a *= alphaMultiplier;
            AddArrowQuad(
                strip,
                center,
                direction,
                across,
                arrowLength,
                _settings.ArrowWidth,
                color,
                _settings.FlipArrowDirection);
        }

        if (strip.Vertices.Count == 0)
        {
            strip.Renderer.enabled = false;
            return;
        }

        strip.Mesh.Clear();
        strip.Mesh.SetVertices(strip.Vertices);
        strip.Mesh.SetUVs(0, strip.Uvs);
        strip.Mesh.SetColors(strip.Colors);
        strip.Mesh.SetTriangles(strip.Triangles, 0);
        strip.Mesh.RecalculateBounds();
        strip.Mesh.RecalculateNormals();
        strip.Renderer.enabled = _isVisible;
    }

    static void AddArrowQuad(
        ArrowStrip strip,
        Vector3 center,
        Vector3 along,
        Vector3 across,
        float length,
        float width,
        Color color,
        bool flipAlong)
    {
        Vector3 halfAlong = along * (length * 0.5f);
        Vector3 halfAcross = across * (width * 0.5f);
        int baseIndex = strip.Vertices.Count;
        Color32 color32 = color;

        strip.Vertices.Add(center - halfAlong - halfAcross);
        strip.Vertices.Add(center - halfAlong + halfAcross);
        strip.Vertices.Add(center + halfAlong + halfAcross);
        strip.Vertices.Add(center + halfAlong - halfAcross);

        if (flipAlong)
        {
            strip.Uvs.Add(new Vector2(1f, 0f));
            strip.Uvs.Add(new Vector2(1f, 1f));
            strip.Uvs.Add(new Vector2(0f, 1f));
            strip.Uvs.Add(new Vector2(0f, 0f));
        }
        else
        {
            strip.Uvs.Add(new Vector2(0f, 0f));
            strip.Uvs.Add(new Vector2(0f, 1f));
            strip.Uvs.Add(new Vector2(1f, 1f));
            strip.Uvs.Add(new Vector2(1f, 0f));
        }

        strip.Colors.Add(color32);
        strip.Colors.Add(color32);
        strip.Colors.Add(color32);
        strip.Colors.Add(color32);

        strip.Triangles.Add(baseIndex);
        strip.Triangles.Add(baseIndex + 1);
        strip.Triangles.Add(baseIndex + 2);
        strip.Triangles.Add(baseIndex);
        strip.Triangles.Add(baseIndex + 2);
        strip.Triangles.Add(baseIndex + 3);
    }

    static Vector3 GetPathAcross(Vector3 pathDirection)
    {
        Camera camera = Camera.main;
        if (camera != null)
        {
            Vector3 across = Vector3.Cross(camera.transform.forward, pathDirection);
            if (across.sqrMagnitude > 1e-6f)
            {
                return across.normalized;
            }
        }

        Vector3 fallback = Vector3.Cross(Vector3.up, pathDirection);
        if (fallback.sqrMagnitude > 1e-6f)
        {
            return fallback.normalized;
        }

        return Vector3.right;
    }

    void ApplyGlowSegment(
        LineRenderer glowLine,
        Vector3 start,
        Vector3 end,
        float length,
        Color glowStart,
        Color glowEnd)
    {
        if (length <= 0.001f)
        {
            glowLine.enabled = false;
            return;
        }

        ApplyLineGradient(glowLine, glowStart, glowEnd);
        float glowWidth = _settings.GlowLineWidth * _settings.GlowWidthMultiplier;
        glowLine.startWidth = glowWidth;
        glowLine.endWidth = glowWidth * 0.95f;
        glowLine.SetPosition(0, start);
        glowLine.SetPosition(1, end);
        glowLine.enabled = true;
    }

    void ApplyOriginGlow(Vector3 center, Color color)
    {
        ApplyRing(_originGlowRing, center, _settings.OriginGlowRadius, _settings.OriginGlowWidth, color);
    }

    void ApplyTargetMarkers(Vector3 center, Color color)
    {
        ApplyRing(_targetFillRing, center, _settings.TargetFillRadius, _settings.ArrowWidth * 0.55f, color);
        ApplyRing(_targetRingInner, center, _settings.TargetRing1Radius, _settings.TargetRingWidth, color);
        ApplyRing(_targetRingOuter, center, _settings.TargetRing2Radius, _settings.TargetRingWidth, color);
    }

    void ApplyRing(LineRenderer lineRenderer, Vector3 center, float radius, float width, Color color)
    {
        if (radius <= 0.001f || width <= 0.001f)
        {
            lineRenderer.enabled = false;
            return;
        }

        ApplyLineGradient(lineRenderer, color, color);
        SetCirclePositions(lineRenderer, center, radius);
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;
        lineRenderer.enabled = true;
    }

    void SetCirclePositions(LineRenderer lineRenderer, Vector3 center, float radius)
    {
        int segments = Mathf.Max(_settings.RingSegments, 8);
        if (lineRenderer.positionCount != segments)
        {
            lineRenderer.positionCount = segments;
        }

        float y = center.y;
        for (int i = 0; i < segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            float x = center.x + Mathf.Cos(angle) * radius;
            float z = center.z + Mathf.Sin(angle) * radius;
            lineRenderer.SetPosition(i, new Vector3(x, y, z));
        }
    }

    void RefreshArrowTextureBinding()
    {
        Texture2D texture = _settings != null ? _settings.ArrowTexture : null;
        _hasArrowTexture = texture != null;

        if (_arrowMaterial == null)
        {
            return;
        }

        if (_arrowMaterial.HasProperty("_MainTex"))
        {
            _arrowMaterial.SetTexture("_MainTex", texture != null ? texture : Texture2D.whiteTexture);
        }

        if (_arrowMaterial.HasProperty("_BaseMap"))
        {
            _arrowMaterial.SetTexture("_BaseMap", texture != null ? texture : Texture2D.whiteTexture);
        }

        if (_primaryArrows?.Renderer != null)
        {
            ApplyTextureToRenderer(_primaryArrows.Renderer, texture);
        }

        if (_bounceArrows?.Renderer != null)
        {
            ApplyTextureToRenderer(_bounceArrows.Renderer, texture);
        }
    }

    static void ApplyTextureToRenderer(MeshRenderer renderer, Texture2D texture)
    {
        Material material = renderer.material;
        Texture2D resolved = texture != null ? texture : Texture2D.whiteTexture;

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", resolved);
        }

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", resolved);
        }
    }

    Vector3 Elevate(Vector3 position)
    {
        float offset = _settings != null ? _settings.LineHeightOffset : 0.003f;
        return new Vector3(position.x, position.y + offset, position.z);
    }

    void SetVisible(bool visible)
    {
        _primaryGlowLine.enabled = visible;
        _bounceGlowLine.enabled = false;
        _primaryArrows.Renderer.enabled = visible && _hasArrowTexture;
        _bounceArrows.Renderer.enabled = visible && _path.HasBounce && _hasArrowTexture;
        _originGlowRing.enabled = visible;
        _targetFillRing.enabled = visible;
        _targetRingInner.enabled = visible;
        _targetRingOuter.enabled = visible;
    }

    static void ClearArrowStrip(ArrowStrip strip)
    {
        if (strip == null)
        {
            return;
        }

        strip.Vertices.Clear();
        strip.Uvs.Clear();
        strip.Colors.Clear();
        strip.Triangles.Clear();
        strip.Mesh.Clear();
        strip.Renderer.enabled = false;
    }

    static void DestroyArrowStrip(ArrowStrip strip)
    {
        if (strip == null)
        {
            return;
        }

        if (strip.Mesh != null)
        {
            Destroy(strip.Mesh);
        }

        if (strip.Renderer != null)
        {
            Destroy(strip.Renderer.gameObject);
        }
    }

    ArrowStrip CreateArrowStrip(string objectName)
    {
        var stripObject = new GameObject(objectName);
        stripObject.transform.SetParent(_arrowRoot, false);
        stripObject.transform.localPosition = Vector3.zero;
        stripObject.transform.localRotation = Quaternion.identity;
        stripObject.transform.localScale = Vector3.one;

        var meshFilter = stripObject.AddComponent<MeshFilter>();
        var meshRenderer = stripObject.AddComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.enabled = false;
        meshRenderer.material = new Material(_arrowMaterial);

        var mesh = new Mesh
        {
            name = objectName + "Mesh",
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
        };
        mesh.MarkDynamic();
        meshFilter.sharedMesh = mesh;

        return new ArrowStrip
        {
            Mesh = mesh,
            Renderer = meshRenderer
        };
    }

    static void ApplyLineGradient(LineRenderer lineRenderer, Color startColor, Color endColor)
    {
        lineRenderer.applyActiveColorSpace = false;
        lineRenderer.startColor = startColor;
        lineRenderer.endColor = endColor;
        lineRenderer.colorGradient = CreateGradient(startColor, endColor);
    }

    static Gradient CreateGradient(Color startColor, Color endColor)
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(startColor, 0f),
                new GradientColorKey(endColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(startColor.a, 0f),
                new GradientAlphaKey(endColor.a, 1f)
            });
        return gradient;
    }

    LineRenderer CreateGlowLine(string objectName)
    {
        return CreateLineRenderer(objectName, _glowMaterial);
    }

    LineRenderer CreateRingLine(string objectName, Material material, int segments)
    {
        LineRenderer lineRenderer = CreateLineRenderer(objectName, material);
        lineRenderer.loop = true;
        lineRenderer.positionCount = segments;
        return lineRenderer;
    }

    LineRenderer CreateLineRenderer(string objectName, Material material)
    {
        var lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);

        var lineRenderer = lineObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.widthMultiplier = 1f;
        lineRenderer.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
        lineRenderer.numCapVertices = 4;
        lineRenderer.numCornerVertices = 0;
        lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.material = material;
        lineRenderer.enabled = false;
        ApplyLineGradient(lineRenderer, Color.white, Color.white);

        return lineRenderer;
    }

    static Material CreateArrowMaterial()
    {
        Shader shader = Shader.Find("PennyBall/AimArrow");
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

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", 0f);
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

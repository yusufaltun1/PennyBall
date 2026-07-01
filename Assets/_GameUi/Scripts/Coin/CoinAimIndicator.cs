using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
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

    [SerializeField] float _lineHeightOffset = 0.003f;
    [SerializeField] float _lineWidth = 0.006f;
    [SerializeField] Color _weakPowerColor = new(0.2f, 0.95f, 0.45f, 0.95f);
    [SerializeField] Color _maxPowerColor = new(0.55f, 0.04f, 0.04f, 0.95f);
    [SerializeField] [Range(0f, 1f)] float _endAlphaFactor = 0.5f;
    [SerializeField] [Range(0f, 1f)] float _bounceAlphaFactor = 0.75f;

    public Color StartColor => _weakPowerColor;

    LineRenderer _aimLine;
    LineRenderer _bounceLine;
    Material _lineMaterial;

    void Awake()
    {
        _lineMaterial = CreateLineMaterial();
        _aimLine = CreateLineRenderer("AimLine");
        _bounceLine = CreateLineRenderer("BounceLine", 0.8f);
        SetVisible(false);
    }

    void OnDestroy()
    {
        if (_lineMaterial != null)
        {
            Destroy(_lineMaterial);
        }
    }

    public void UpdateVisual(PathVisual path, float power01)
    {
        Color lineColor = Color.Lerp(_weakPowerColor, _maxPowerColor, Mathf.Clamp01(power01));
        ApplyLineColors(_aimLine, lineColor, _endAlphaFactor);

        Vector3 primaryStart = Elevate(path.PrimaryStart);
        Vector3 primaryEnd = Elevate(path.PrimaryEnd);
        _aimLine.SetPosition(0, primaryStart);
        _aimLine.SetPosition(1, primaryEnd);

        if (path.HasBounce)
        {
            Color bounceColor = lineColor;
            bounceColor.a *= _bounceAlphaFactor;
            ApplyLineColors(_bounceLine, bounceColor, _endAlphaFactor);

            Vector3 bounceStart = primaryEnd;
            Vector3 bounceEnd = Elevate(path.BounceEnd);
            _bounceLine.SetPosition(0, bounceStart);
            _bounceLine.SetPosition(1, bounceEnd);
            _bounceLine.enabled = true;
        }
        else
        {
            _bounceLine.enabled = false;
        }

        _aimLine.enabled = true;
    }

    public void Hide()
    {
        SetVisible(false);
    }

    void ApplyLineColors(LineRenderer lineRenderer, Color color, float endAlphaFactor)
    {
        Color endColor = color;
        endColor.a = color.a * endAlphaFactor;
        lineRenderer.startColor = color;
        lineRenderer.endColor = endColor;
    }

    Vector3 Elevate(Vector3 position)
    {
        return new Vector3(position.x, position.y + _lineHeightOffset, position.z);
    }

    void SetVisible(bool visible)
    {
        _aimLine.enabled = visible;
        _bounceLine.enabled = false;
    }

    LineRenderer CreateLineRenderer(string objectName, float widthScale = 1f)
    {
        var lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);

        var lineRenderer = lineObject.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 2;
        lineRenderer.useWorldSpace = true;
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.startWidth = _lineWidth * widthScale;
        lineRenderer.endWidth = _lineWidth * widthScale * 0.65f;
        lineRenderer.numCapVertices = 4;
        lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.material = _lineMaterial;
        lineRenderer.enabled = false;

        return lineRenderer;
    }

    static Material CreateLineMaterial()
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
        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
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

using UnityEngine;

[DisallowMultipleComponent]
public class CoinAimIndicator : MonoBehaviour
{
    [SerializeField] float _lineHeightOffset = 0.003f;
    [SerializeField] float _lineWidth = 0.006f;
    [SerializeField] Color _pullLineColor = new(1f, 0.45f, 0.2f, 0.9f);
    [SerializeField] Color _aimLineColor = new(0.2f, 0.95f, 0.45f, 0.95f);

    LineRenderer _pullLine;
    LineRenderer _aimLine;

    void Awake()
    {
        _pullLine = CreateLineRenderer("PullLine", _pullLineColor);
        _aimLine = CreateLineRenderer("AimLine", _aimLineColor);
        SetVisible(false);
    }

    public void UpdateVisual(Vector3 anchor, Vector3 pullPoint, Vector3 launchDirection, float pullDistance)
    {
        Vector3 elevatedAnchor = Elevate(anchor);
        Vector3 elevatedPull = Elevate(pullPoint);
        Vector3 aimEnd = elevatedAnchor + launchDirection * pullDistance;

        _pullLine.SetPosition(0, elevatedAnchor);
        _pullLine.SetPosition(1, elevatedPull);

        _aimLine.SetPosition(0, elevatedAnchor);
        _aimLine.SetPosition(1, aimEnd);

        SetVisible(true);
    }

    public void Hide()
    {
        SetVisible(false);
    }

    Vector3 Elevate(Vector3 position)
    {
        return new Vector3(position.x, position.y + _lineHeightOffset, position.z);
    }

    void SetVisible(bool visible)
    {
        _pullLine.enabled = visible;
        _aimLine.enabled = visible;
    }

    LineRenderer CreateLineRenderer(string objectName, Color color)
    {
        var lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);

        var lineRenderer = lineObject.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 2;
        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = _lineWidth;
        lineRenderer.endWidth = _lineWidth * 0.65f;
        lineRenderer.numCapVertices = 4;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        lineRenderer.enabled = false;

        return lineRenderer;
    }
}

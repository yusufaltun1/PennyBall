using TMPro;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class GuideExplanationAutoWidth : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI _text;
    [SerializeField] float _horizontalPadding = 32f;
    [SerializeField] float _verticalPadding = 20f;
    [SerializeField] float _minWidth = 80f;
    [SerializeField] float _maxWidth = 900f;

    RectTransform _rectTransform;
    string _lastAppliedText;

    void Awake()
    {
        ResolveReferences();
        Refresh();
    }

    void OnEnable()
    {
        ResolveReferences();
        Refresh();
    }

    void LateUpdate()
    {
        if (_text == null)
        {
            return;
        }

        if (_text.text != _lastAppliedText)
        {
            Refresh();
        }
    }

    public void Refresh()
    {
        ResolveReferences();
        if (_text == null || _rectTransform == null)
        {
            return;
        }

        _text.textWrappingMode = TextWrappingModes.NoWrap;
        _text.overflowMode = TextOverflowModes.Overflow;

        string message = _text.text ?? string.Empty;
        _lastAppliedText = message;

        _text.ForceMeshUpdate();
        Vector2 textSize = _text.GetPreferredValues(message, float.PositiveInfinity, 0f);

        float width = Mathf.Clamp(textSize.x + _horizontalPadding * 2f, _minWidth, _maxWidth);
        float height = textSize.y + _verticalPadding * 2f;

        _rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        _rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
    }

    void ResolveReferences()
    {
        if (_rectTransform == null)
        {
            _rectTransform = transform as RectTransform;
        }

        if (_text == null)
        {
            _text = GetComponentInChildren<TextMeshProUGUI>(true);
        }
    }
}

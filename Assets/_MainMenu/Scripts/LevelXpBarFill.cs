using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public class LevelXpBarFill : MonoBehaviour
{
    [Header("100% dolu iken LevelStatus RectTransform değerleri")]
    [SerializeField] Vector2 _fullAnchorMin = new(0f, 0f);
    [SerializeField] Vector2 _fullAnchorMax = new(0.8571429f, 1f);
    [SerializeField] Vector2 _fillAnchoredPosition = new(18.4f, 7.5f);
    [SerializeField] Vector2 _fillPivot = new(0f, 0.5f);

    [SerializeField] RectTransform _trackRect;
    [SerializeField] RectTransform _fillRect;
    [SerializeField] float _minVisibleProgress = 0.02f;

    Image _fillImage;

    void Awake()
    {
        ResolveReferences();
    }

    void OnEnable()
    {
        WalletService.Changed += Refresh;
        WalletService.LevelChanged += OnLevelChanged;
        Refresh();
    }

    void Start()
    {
        Refresh();
    }

    void OnDisable()
    {
        WalletService.Changed -= Refresh;
        WalletService.LevelChanged -= OnLevelChanged;
    }

    void OnLevelChanged(int levelBefore, int levelAfter)
    {
        Refresh();
    }

    void ResolveReferences()
    {
        if (_fillRect == null)
        {
            _fillRect = transform as RectTransform;
        }

        if (_fillImage == null)
        {
            _fillImage = GetComponent<Image>();
        }

        if (_trackRect == null && _fillRect != null && _fillRect.parent is RectTransform parentRect)
        {
            _trackRect = parentRect;
        }

        if (_fillImage != null)
        {
            _fillImage.type = Image.Type.Simple;
            _fillImage.preserveAspect = false;
        }
    }

    public void Refresh()
    {
        ResolveReferences();

        if (_fillRect == null)
        {
            return;
        }

        float progress = Mathf.Clamp01(WalletService.LevelProgress);
        if (progress > 0f)
        {
            progress = Mathf.Max(progress, _minVisibleProgress);
        }

        ApplyProgress(progress);
    }

    void ApplyProgress(float progress)
    {
        _fillRect.localScale = Vector3.one;
        _fillRect.pivot = _fillPivot;
        _fillRect.anchorMin = _fullAnchorMin;
        _fillRect.anchorMax = new Vector2(
            Mathf.Lerp(_fullAnchorMin.x, _fullAnchorMax.x, progress),
            _fullAnchorMax.y);
        _fillRect.anchoredPosition = _fillAnchoredPosition;
        _fillRect.sizeDelta = Vector2.zero;
    }

#if UNITY_EDITOR
    [ContextMenu("Preview 100% Fill")]
    void PreviewFullFill()
    {
        ResolveReferences();
        ApplyProgress(1f);
    }

    [ContextMenu("Capture Current Rect As 100% Layout")]
    void CaptureCurrentLayoutAsFull()
    {
        ResolveReferences();
        if (_fillRect == null)
        {
            return;
        }

        _fullAnchorMin = _fillRect.anchorMin;
        _fullAnchorMax = _fillRect.anchorMax;
        _fillAnchoredPosition = _fillRect.anchoredPosition;
        _fillPivot = _fillRect.pivot;
    }

    void OnValidate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        ResolveReferences();
        Refresh();
    }
#endif
}

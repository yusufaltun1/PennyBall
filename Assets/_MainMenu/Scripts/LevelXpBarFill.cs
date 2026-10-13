using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// LevelStatus'un sahnedeki dikey yerleşimini ve sol başlangıcını korur;
/// yalnızca sağ kenarı XP ilerlemesine göre FillTrack içinde uzatır.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public class LevelXpBarFill : MonoBehaviour
{
    [Tooltip("%100 dolulukta sağ kenarın FillTrack genişliğine oranı (1 = FillTrack'in sağ kenarı).")]
    [SerializeField, Range(0f, 1f)] float _fullAnchorMaxX = 1f;

    [SerializeField] RectTransform _fillRect;
    [SerializeField] float _minVisibleProgress = 0.02f;

    Image _fillImage;
    bool _hasDesignLayout;
    float _designAnchorMinY;
    float _designAnchorMaxY;
    Vector2 _designPivot;
    float _designLeft;
    float _designOffsetMinY;
    float _designOffsetMaxY;

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

        if (_fillImage != null)
        {
            _fillImage.type = Image.Type.Simple;
            _fillImage.preserveAspect = false;
        }

        CaptureDesignLayout();
    }

    void CaptureDesignLayout()
    {
        if (_hasDesignLayout || _fillRect == null)
        {
            return;
        }

        _designAnchorMinY = _fillRect.anchorMin.y;
        _designAnchorMaxY = _fillRect.anchorMax.y;
        _designPivot = _fillRect.pivot;
        _designLeft = _fillRect.offsetMin.x;
        _designOffsetMinY = _fillRect.offsetMin.y;
        _designOffsetMaxY = _fillRect.offsetMax.y;
        _hasDesignLayout = true;
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
        _fillRect.pivot = _designPivot;
        _fillRect.anchorMin = new Vector2(0f, _designAnchorMinY);
        _fillRect.anchorMax = new Vector2(Mathf.Lerp(0f, _fullAnchorMaxX, progress), _designAnchorMaxY);
        _fillRect.offsetMin = new Vector2(_designLeft, _designOffsetMinY);
        _fillRect.offsetMax = new Vector2(0f, _designOffsetMaxY);
    }

#if UNITY_EDITOR
    [ContextMenu("Preview 100% Fill")]
    void PreviewFullFill()
    {
        ResolveReferences();
        ApplyProgress(1f);
    }

    [ContextMenu("Preview 50% Fill")]
    void PreviewHalfFill()
    {
        ResolveReferences();
        ApplyProgress(0.5f);
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

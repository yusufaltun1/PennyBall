using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ChangeAvatarPanelController : MonoBehaviour
{
    [Header("Navigation")]
    [SerializeField] Button _openButton;
    [SerializeField] Button _closeButton;

    [Header("Grid")]
    [SerializeField] Transform               _content;     // Avatars/Container/Viewport/Content
    [SerializeField] ChangeAvatarElementView _slotPrefab;  // ChangeAvatarElement prefab

    [Header("Selection Visuals")]
    [SerializeField] Color _selectedTint   = Color.white;
    [SerializeField] Color _unselectedTint = new Color(0.45f, 0.45f, 0.45f, 1f);

    [Header("Animation")]
    [SerializeField] float _animDuration = 0.35f;

    RectTransform              _rect;
    AvatarSpriteLibrary        _library;
    ChangeAvatarElementView[]  _slots;
    int                        _currentIndex;
    Coroutine                  _anim;
    float                      _openY, _closedY;
    bool                       _isOpen;

    void Awake()
    {
        _rect = GetComponent<RectTransform>();
        CachePositions();
        SetToClosedPosition();
        if (!_isOpen)
            gameObject.SetActive(false);

        if (_openButton  != null) _openButton.onClick.AddListener(Open);
        if (_closeButton != null) _closeButton.onClick.AddListener(Close);
    }

    void OnDestroy()
    {
        if (_openButton  != null) _openButton.onClick.RemoveListener(Open);
        if (_closeButton != null) _closeButton.onClick.RemoveListener(Close);
    }

    public void Open()
    {
        if (_isOpen) return;
        _isOpen = true;

        _library      = AvatarSpriteLibrary.Load();
        _currentIndex = LeagueService.Instance?.PlayerAvatarIndex ?? 0;

        gameObject.SetActive(true);
        BuildGrid();
        SetToClosedPosition();
        Animate(_openY, deactivateOnDone: false);
    }

    public void Close()
    {
        if (!_isOpen) return;
        MainMenuClickSound.Play();
        _isOpen = false;
        Animate(_closedY, deactivateOnDone: true);
    }

    // ── Grid ─────────────────────────────────────────────────────────────────

    void BuildGrid()
    {
        ClearContent();

        int count = _library != null ? _library.Count : 0;
        _slots = new ChangeAvatarElementView[count];

        for (int i = 0; i < count; i++)
        {
            var slot = Instantiate(_slotPrefab, _content);
            var layout = slot.GetComponent<LayoutElement>() ?? slot.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = layout.minHeight = 300f;
            layout.preferredWidth = layout.preferredHeight = 300f;
            slot.Setup(i, _library.Get(i), i == _currentIndex, _selectedTint, _unselectedTint);
            slot.Bind(SelectAvatar);
            _slots[i] = slot;
        }

        if (_content is RectTransform contentRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
    }

    void ClearContent()
    {
        if (_content == null)
        {
            return;
        }

        for (int i = _content.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(_content.GetChild(i).gameObject);
        }
    }

    void SelectAvatar(int index)
    {
        _currentIndex = index;
        LeagueService.Instance?.SetPlayerAvatar(index);
        RefreshAll();
    }

    void RefreshAll()
    {
        if (_slots == null || _library == null) return;
        for (int i = 0; i < _slots.Length; i++)
            _slots[i].Setup(i, _library.Get(i), i == _currentIndex, _selectedTint, _unselectedTint);
    }

    // ── Animation ────────────────────────────────────────────────────────────

    void Animate(float targetY, bool deactivateOnDone)
    {
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(DoAnimate(targetY, deactivateOnDone));
    }

    IEnumerator DoAnimate(float targetY, bool deactivateOnDone)
    {
        Vector2 start   = _rect.anchoredPosition;
        Vector2 end     = new Vector2(start.x, targetY);
        float   elapsed = 0f;

        while (elapsed < _animDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _animDuration);
            t = t * t * (3f - 2f * t);
            _rect.anchoredPosition = Vector2.Lerp(start, end, t);
            yield return null;
        }

        _rect.anchoredPosition = end;
        _anim = null;

        if (deactivateOnDone) gameObject.SetActive(false);
    }

    void CachePositions()
    {
        float halfHeight = GetCanvasHalfHeight();
        _closedY = halfHeight;
        _openY   = -halfHeight;
    }

    void SetToClosedPosition()
    {
        var pos = _rect.anchoredPosition;
        pos.y = _closedY;
        _rect.anchoredPosition = pos;
    }

    float GetCanvasHalfHeight()
    {
        const float fallback = 1170f;
        Canvas c = GetComponentInParent<Canvas>();
        if (c == null) return fallback;
        RectTransform cr = c.GetComponent<RectTransform>();
        return cr != null && cr.rect.height > 0f ? cr.rect.height * 0.5f : fallback;
    }
}

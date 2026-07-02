using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChangeAvatarElementView : MonoBehaviour
{
    [SerializeField] Image           _avatarImage;
    [SerializeField] TextMeshProUGUI _activeLabel;

    Button     _hitButton;
    GameObject _activeBadge;
    int        _avatarIndex = -1;

    public int AvatarIndex => _avatarIndex;

    void Awake()
    {
        if (_avatarImage == null)
        {
            Transform t = transform.Find("AvatarMask/AvatarImage");
            if (t != null) _avatarImage = t.GetComponent<Image>();
        }
        if (_activeLabel == null)
            _activeLabel = GetComponentInChildren<TextMeshProUGUI>(true);
        if (_activeBadge == null && _activeLabel != null)
        {
            Transform badgeRoot = _activeLabel.transform.parent;
            _activeBadge = badgeRoot != null ? badgeRoot.gameObject : _activeLabel.gameObject;
        }

        EnsureHitButton();
        DisableOverlappingRaycasts();
    }

    void EnsureHitButton()
    {
        Transform hitTransform = transform.Find("HitArea");
        if (hitTransform == null)
        {
            var go = new GameObject("HitArea", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            hitTransform = go.transform;
        }

        var hitImage = hitTransform.GetComponent<Image>();
        if (hitImage == null)
            hitImage = hitTransform.gameObject.AddComponent<Image>();
        hitImage.color = new Color(1f, 1f, 1f, 0f);
        hitImage.raycastTarget = true;

        _hitButton = hitTransform.GetComponent<Button>();
        if (_hitButton == null)
            _hitButton = hitTransform.gameObject.AddComponent<Button>();
        _hitButton.transition = Selectable.Transition.None;
        _hitButton.targetGraphic = hitImage;

        hitTransform.SetAsLastSibling();
    }

    void DisableOverlappingRaycasts()
    {
        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        Image hitImage = _hitButton != null ? _hitButton.targetGraphic as Image : null;
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = graphics[i] == hitImage;
    }

    public void Setup(int avatarIndex, Sprite sprite, bool selected, Color selectedTint, Color normalTint)
    {
        _avatarIndex = avatarIndex;

        if (_avatarImage != null)
        {
            _avatarImage.sprite = sprite;
            _avatarImage.color  = selected ? selectedTint : normalTint;
        }
        if (_activeBadge != null)
            _activeBadge.SetActive(selected);
    }

    public void Bind(System.Action<int> onSelected)
    {
        if (_hitButton == null)
            return;

        _hitButton.onClick.RemoveAllListeners();
        int index = _avatarIndex;
        _hitButton.onClick.AddListener(() => onSelected?.Invoke(index));
    }
}

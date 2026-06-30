using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChangeAvatarElementView : MonoBehaviour
{
    [SerializeField] Image            _avatarImage;  // AvatarMask/AvatarImage
    [SerializeField] TextMeshProUGUI  _activeLabel;  // Image/Text (TMP)

    Button _button;

    public Button Button => _button;

    void Awake()
    {
        _button = GetComponent<Button>();
        if (_button == null)
        {
            _button = gameObject.AddComponent<Button>();
            _button.transition = Selectable.Transition.None;
        }
        if (_avatarImage != null)
            _button.targetGraphic = _avatarImage;

        // Auto-find fallbacks
        if (_avatarImage == null)
        {
            Transform t = transform.Find("AvatarMask/AvatarImage");
            if (t != null) _avatarImage = t.GetComponent<Image>();
        }
        if (_activeLabel == null)
            _activeLabel = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    public void Setup(Sprite sprite, bool selected, Color selectedTint, Color normalTint)
    {
        if (_avatarImage != null)
        {
            _avatarImage.sprite = sprite;
            _avatarImage.color  = selected ? selectedTint : normalTint;
        }
        if (_activeLabel != null)
            _activeLabel.gameObject.SetActive(selected);
    }
}

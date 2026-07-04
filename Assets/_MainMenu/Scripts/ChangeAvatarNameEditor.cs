using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ChangeAvatarNameEditor : MonoBehaviour
{
    const int DefaultMaxLength = 20;

    [SerializeField] Image _nameBar;
    [SerializeField] RectTransform _textViewport;
    [SerializeField] TextMeshProUGUI _nameText;
    [SerializeField] Button _editButton;
    [SerializeField] int _maxLength = DefaultMaxLength;

    TMP_InputField _nameInput;

    void Awake()
    {
        EnsureInputField();

        if (_nameInput != null)
        {
            _nameInput.onEndEdit.AddListener(OnEndEdit);
            _nameInput.onSubmit.AddListener(OnEndEdit);
        }

        if (_editButton != null)
        {
            _editButton.onClick.AddListener(OnEditClicked);
        }
    }

    void OnEnable()
    {
        if (LeagueService.Instance != null)
        {
            LeagueService.Instance.DisplayNameChanged += Refresh;
        }

        Refresh();
    }

    void OnDisable()
    {
        if (LeagueService.Instance != null)
        {
            LeagueService.Instance.DisplayNameChanged -= Refresh;
        }
    }

    void OnDestroy()
    {
        if (_nameInput != null)
        {
            _nameInput.onEndEdit.RemoveListener(OnEndEdit);
            _nameInput.onSubmit.RemoveListener(OnEndEdit);
        }

        if (_editButton != null)
        {
            _editButton.onClick.RemoveListener(OnEditClicked);
        }
    }

    public void OnEditClicked()
    {
        if (_nameInput == null)
        {
            return;
        }

        _nameInput.Select();
        _nameInput.ActivateInputField();
    }

    void OnEndEdit(string value)
    {
        string trimmed = value == null ? string.Empty : value.Trim();
        if (trimmed.Length == 0)
        {
            Refresh();
            return;
        }

        int maxLength = Mathf.Max(1, _maxLength);
        if (trimmed.Length > maxLength)
        {
            trimmed = trimmed.Substring(0, maxLength);
        }

        if (LeagueService.Instance != null)
        {
            LeagueService.Instance.SetPlayerDisplayName(trimmed);
        }

        Refresh();
    }

    void Refresh()
    {
        if (_nameInput == null || _nameInput.isFocused)
        {
            return;
        }

        string displayName = LeagueService.Instance?.Save?.playerDisplayName;
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = "Player";
        }

        _nameInput.SetTextWithoutNotify(displayName);
    }

    void EnsureInputField()
    {
        if (_nameBar == null || _textViewport == null || _nameText == null)
        {
            Debug.LogError("[ChangeAvatarNameEditor] Name bar references are missing.", this);
            return;
        }

        _nameInput = _nameBar.GetComponent<TMP_InputField>();
        if (_nameInput == null)
        {
            _nameInput = _nameBar.gameObject.AddComponent<TMP_InputField>();
        }

        _nameText.raycastTarget = false;
        _nameText.textWrappingMode = TextWrappingModes.NoWrap;
        _nameText.overflowMode = TextOverflowModes.Ellipsis;

        _nameInput.targetGraphic = _nameBar;
        _nameInput.textViewport = _textViewport;
        _nameInput.textComponent = _nameText;
        _nameInput.lineType = TMP_InputField.LineType.SingleLine;
        _nameInput.characterLimit = Mathf.Max(1, _maxLength);
        _nameInput.richText = false;
        _nameInput.onFocusSelectAll = true;
        _nameInput.shouldHideMobileInput = false;
        _nameInput.transition = Selectable.Transition.None;
        _nameInput.customCaretColor = true;
        _nameInput.caretColor = _nameText.color;
        _nameInput.caretWidth = 2;
        _nameInput.selectionColor = new Color(_nameText.color.r, _nameText.color.g, _nameText.color.b, 0.35f);
        _nameInput.interactable = true;
    }
}

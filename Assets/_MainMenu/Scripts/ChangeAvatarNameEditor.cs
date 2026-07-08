using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ChangeAvatarNameEditor : MonoBehaviour
{
    const int DefaultMaxLength = 20;

    [SerializeField] Image _nameBar;
    [SerializeField] TextMeshProUGUI _nameText;
    [SerializeField] Button _editButton;
    [SerializeField] int _maxLength = DefaultMaxLength;

    TMP_InputField _nameInput;
    RectTransform _textViewport;

    void Awake()
    {
        ResolveReferences();
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
        Subscribe();
        Refresh();
        StartCoroutine(SubscribeWhenReady());
    }

    void OnDisable()
    {
        Unsubscribe();
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

    IEnumerator SubscribeWhenReady()
    {
        while (LeagueService.Instance == null)
        {
            yield return null;
        }

        Subscribe();
        Refresh();
    }

    void Subscribe()
    {
        if (LeagueService.Instance == null)
        {
            return;
        }

        LeagueService.Instance.DisplayNameChanged -= Refresh;
        LeagueService.Instance.DisplayNameChanged += Refresh;
    }

    void Unsubscribe()
    {
        if (LeagueService.Instance == null)
        {
            return;
        }

        LeagueService.Instance.DisplayNameChanged -= Refresh;
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

        LeagueService.Instance?.SetPlayerDisplayName(trimmed);
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

    void ResolveReferences()
    {
        if (_nameBar == null)
        {
            Transform imageTransform = transform.Find("Image");
            if (imageTransform != null)
            {
                _nameBar = imageTransform.GetComponent<Image>();
            }
        }

        if (_nameText == null && _nameBar != null)
        {
            Transform textTransform = _nameBar.transform.Find("Text Area/PlayerName");
            if (textTransform == null)
            {
                textTransform = _nameBar.transform.Find("PlayerName");
            }

            if (textTransform != null)
            {
                _nameText = textTransform.GetComponent<TextMeshProUGUI>();
            }
        }

        if (_editButton == null)
        {
            Transform buttonTransform = transform.Find("Button");
            if (buttonTransform != null)
            {
                _editButton = buttonTransform.GetComponent<Button>();
            }
        }
    }

    void EnsureInputField()
    {
        if (_nameBar == null || _nameText == null)
        {
            Debug.LogError("[ChangeAvatarNameEditor] Name bar references are missing.", this);
            return;
        }

        EnsureTextViewport();

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

    void EnsureTextViewport()
    {
        if (_textViewport != null)
        {
            return;
        }

        Transform existing = _nameBar.transform.Find("Text Area");
        if (existing != null)
        {
            _textViewport = existing as RectTransform;
            return;
        }

        var viewportObject = new GameObject("Text Area", typeof(RectTransform));
        _textViewport = viewportObject.GetComponent<RectTransform>();
        _textViewport.SetParent(_nameBar.transform, false);
        StretchToParent(_textViewport);

        RectTransform textRect = _nameText.rectTransform;
        textRect.SetParent(_textViewport, false);
        StretchToParent(textRect);
    }

    static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }
}

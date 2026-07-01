using UnityEngine;
using UnityEngine.UI;

public class SettingsToggleControl : MonoBehaviour
{
    public enum SettingKind
    {
        Music,
        SoundEffects,
        Vibration,
    }

    [SerializeField] SettingKind _kind;
    [SerializeField] Button _button;
    [SerializeField] Image _image;

    Sprite _onSprite;
    Sprite _offSprite;
    bool _initialized;
    float _lastClickTime;

    public void Initialize(SettingKind kind)
    {
        _kind = kind;
        EnsureInitialized();
        RefreshFromService();
    }

    void OnEnable()
    {
        GameFeedbackSettingsService.Changed -= RefreshFromService;
        GameFeedbackSettingsService.Changed += RefreshFromService;
        RefreshFromService();
    }

    void OnDisable()
    {
        GameFeedbackSettingsService.Changed -= RefreshFromService;
    }

    void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(OnToggleClicked);
        }
    }

    public void RefreshFromService()
    {
        if (!_initialized || _image == null)
        {
            return;
        }

        _image.sprite = IsEnabled() ? _onSprite : _offSprite;
    }

    void EnsureInitialized()
    {
        if (_button == null)
        {
            _button = GetComponent<Button>();
        }

        if (_image == null)
        {
            _image = GetComponent<Image>();
        }

        if (_button == null || _image == null)
        {
            Debug.LogWarning($"[SettingsToggle] {name} için Button/Image bulunamadı.");
            return;
        }

        if (!_initialized)
        {
            _onSprite = _image.sprite;
            _offSprite = _button.spriteState.pressedSprite != null
                ? _button.spriteState.pressedSprite
                : _onSprite;

            _button.transition = Selectable.Transition.None;
            _initialized = true;
        }

        _button.onClick.RemoveListener(OnToggleClicked);
        _button.onClick.AddListener(OnToggleClicked);
    }

    void OnToggleClicked()
    {
        if (!_initialized)
        {
            return;
        }

        float now = Time.unscaledTime;
        if (now - _lastClickTime < 0.15f)
        {
            RefreshFromService();
            return;
        }

        _lastClickTime = now;

        bool nextValue = !IsEnabled();
        switch (_kind)
        {
            case SettingKind.Music:
                GameFeedbackSettingsService.MusicEnabled = nextValue;
                break;
            case SettingKind.SoundEffects:
                GameFeedbackSettingsService.SoundEffectsEnabled = nextValue;
                break;
            case SettingKind.Vibration:
                GameFeedbackSettingsService.VibrationEnabled = nextValue;
                break;
        }

        RefreshFromService();
    }

    bool IsEnabled()
    {
        switch (_kind)
        {
            case SettingKind.Music:
                return GameFeedbackSettingsService.MusicEnabled;
            case SettingKind.SoundEffects:
                return GameFeedbackSettingsService.SoundEffectsEnabled;
            case SettingKind.Vibration:
                return GameFeedbackSettingsService.VibrationEnabled;
            default:
                return true;
        }
    }
}

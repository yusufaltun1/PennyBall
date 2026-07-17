using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class LevelGatedBoosterSlot : MonoBehaviour
{
    [SerializeField] GameObject _levelLabelRoot;
    [SerializeField] TextMeshProUGUI _levelLabel;
    [SerializeField] GameObject _unlockedContent;
    [SerializeField] Image _iconImage;
    [SerializeField] Image _backgroundImage;
    [SerializeField] Sprite _lockedBackgroundSprite;
    [SerializeField] Sprite _unlockedBackgroundSprite;
    [SerializeField] Sprite _noCoinsBackgroundSprite;
    [SerializeField] Sprite _lockedIconSprite;
    [SerializeField] Sprite _unlockedIconSprite;
    [SerializeField] GameObject _costRoot;
    [SerializeField] TextMeshProUGUI _costLabel;
    [SerializeField] Button _button;

    Selectable.Transition _defaultButtonTransition = Selectable.Transition.ColorTint;
    bool _cachedDefaultTransition;

    public void Refresh(int playerLevel, BoosterType boosterType, bool requiresCoins = true)
    {
        ResolveReferences();
        EnsureNoCoinsSprite();

        int unlockLevel = BoosterConfig.GetUnlockLevel(boosterType);
        bool unlocked = playerLevel >= unlockLevel;
        bool hasCoins = !requiresCoins || WalletService.HasEnoughCoins(BoosterConfig.UseCostCoins);

        if (_levelLabelRoot != null)
        {
            _levelLabelRoot.SetActive(!unlocked);
        }

        if (_levelLabel != null)
        {
            _levelLabel.text = $"Lvl. {unlockLevel}";
        }

        if (_unlockedContent != null)
        {
            _unlockedContent.SetActive(true);
        }

        if (!unlocked)
        {
            ApplyLevelLockedVisuals();
            return;
        }

        // Coming soon: coin şartı yok ama Cost görseli unlock iken açık kalır.
        ApplyCostVisible(true);

        if (!hasCoins)
        {
            ApplyNoCoinsVisuals();
            return;
        }

        ApplyActiveVisuals();
    }

    public Button Button
    {
        get
        {
            ResolveReferences();
            return _button;
        }
    }

    void EnsureNoCoinsSprite()
    {
        if (_noCoinsBackgroundSprite != null)
        {
            return;
        }

        // Inspector referansı kaybolursa Ice ile aynı NoCoins sprite'ını kullan.
        Transform root = transform.parent != null ? transform.parent : transform;
        IceBoosterController ice = root.GetComponentInChildren<IceBoosterController>(true);
        if (ice != null && ice.NoCoinsBackgroundSprite != null)
        {
            _noCoinsBackgroundSprite = ice.NoCoinsBackgroundSprite;
        }
    }

    void ApplyLevelLockedVisuals()
    {
        SetBackgroundSprite(_lockedBackgroundSprite);
        SetIconSprite(_lockedIconSprite);
        ApplyCostVisible(false);

        // Ice_Unlocked / Time_Unlocked ile aynı: ColorTint + DisabledColor alpha ~50.
        SetButtonInteractable(false, useColorTintDisabled: true);
    }

    void ApplyNoCoinsVisuals()
    {
        if (_button != null)
        {
            CacheDefaultTransition();
            _button.transition = Selectable.Transition.None;
            _button.interactable = false;
        }

        ResetGraphicColors();
        SetBackgroundSprite(_noCoinsBackgroundSprite);
        SetIconSprite(_lockedIconSprite);
    }

    void ApplyActiveVisuals()
    {
        ResetGraphicColors();
        SetBackgroundSprite(_unlockedBackgroundSprite);
        SetIconSprite(_unlockedIconSprite);
        SetButtonInteractable(true, useColorTintDisabled: true);
    }

    void ApplyCostVisible(bool visible)
    {
        if (_costRoot != null)
        {
            _costRoot.SetActive(visible);
        }

        if (!visible || _costLabel == null)
        {
            return;
        }

        _costLabel.text = BoosterConfig.UseCostCoins.ToString();
    }

    void SetBackgroundSprite(Sprite sprite)
    {
        if (_backgroundImage == null || sprite == null)
        {
            return;
        }

        _backgroundImage.overrideSprite = null;
        _backgroundImage.sprite = sprite;
    }

    void SetIconSprite(Sprite sprite)
    {
        if (_iconImage == null || sprite == null)
        {
            return;
        }

        _iconImage.overrideSprite = null;
        _iconImage.sprite = sprite;
    }

    void CacheDefaultTransition()
    {
        if (_cachedDefaultTransition || _button == null)
        {
            return;
        }

        if (_button.transition != Selectable.Transition.None)
        {
            _defaultButtonTransition = _button.transition;
            _cachedDefaultTransition = true;
        }
    }

    void ResetGraphicColors()
    {
        if (_backgroundImage != null)
        {
            _backgroundImage.color = Color.white;
        }

        if (_iconImage != null)
        {
            _iconImage.color = Color.white;
        }
    }

    void SetButtonInteractable(bool interactable, bool useColorTintDisabled)
    {
        if (_button == null)
        {
            return;
        }

        CacheDefaultTransition();

        // Seviye kilidi / aktif: ColorTint ki DisabledColor alpha uygulansın.
        // NoCoins: Transition.None (ayrı path).
        if (useColorTintDisabled)
        {
            _button.transition = _defaultButtonTransition == Selectable.Transition.None
                ? Selectable.Transition.ColorTint
                : _defaultButtonTransition;
        }
        else
        {
            _button.transition = Selectable.Transition.None;
        }

        _button.interactable = interactable;
    }

    void ResolveReferences()
    {
        if (_backgroundImage == null)
        {
            _backgroundImage = GetComponent<Image>();
        }

        if (_button == null)
        {
            _button = GetComponent<Button>();
        }

        CacheDefaultTransition();

        if (_unlockedContent == null)
        {
            Transform icon = transform.Find("Icon");
            if (icon != null)
            {
                _unlockedContent = icon.gameObject;
            }
        }

        if (_iconImage == null && _unlockedContent != null)
        {
            _iconImage = _unlockedContent.GetComponent<Image>();
        }

        if (_levelLabelRoot == null)
        {
            Transform label = transform.Find("Text (TMP)");
            if (label != null)
            {
                _levelLabelRoot = label.gameObject;
                _levelLabel = label.GetComponent<TextMeshProUGUI>();
            }
        }

        if (_costRoot == null)
        {
            Transform cost = transform.Find("Cost");
            if (cost != null)
            {
                _costRoot = cost.gameObject;
                _costLabel = cost.GetComponent<TextMeshProUGUI>();
            }
        }
        else if (_costLabel == null && _costRoot != null)
        {
            _costLabel = _costRoot.GetComponent<TextMeshProUGUI>();
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        ResolveReferences();
    }
#endif
}

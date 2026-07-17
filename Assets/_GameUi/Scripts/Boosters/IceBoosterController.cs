using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class IceBoosterController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] Button _iceButton;
    [SerializeField] GameObject _icon;
    [SerializeField] GameObject _costText;
    [SerializeField] TextMeshProUGUI _remainingText;
    [SerializeField] Image _backgroundImage;
    [SerializeField] Image _iconImage;

    [Header("Coins")]
    [SerializeField] Sprite _activeBackgroundSprite;
    [SerializeField] Sprite _lockedBackgroundSprite;
    [SerializeField] Sprite _activeIconSprite;
    [SerializeField] Sprite _lockedIconSprite;

    bool _hasCoins = true;

    public Sprite NoCoinsBackgroundSprite => _lockedBackgroundSprite;

    [Header("Booster Süreleri")]
    [SerializeField] IceBoosterTimingSettings _boosterTiming = new();

    [Header("IceUsed Görsel Süreleri")]
    [SerializeField] IceUsedFeedbackTimingSettings _usedFeedbackTiming = new();

    [Header("Frost Visuals")]
    [SerializeField] IceCoinFrostVisual.Settings _frostSettings = new();

    [Header("Used Feedback")]
    [SerializeField] IceUsedBoosterFeedback _iceUsedFeedback;

    Coroutine _activationRoutine;
    Selectable.Transition _defaultButtonTransition;
    bool _cooldownActive;

    void Awake()
    {
        ResolveReferences();
        CacheDefaultSprites();
        ApplyCoinsAvailability();
    }

    void OnEnable()
    {
        WalletService.Changed += RefreshWalletState;

        if (_iceButton != null)
        {
            _iceButton.onClick.AddListener(OnIceButtonClicked);
        }

        if (!_cooldownActive && _activationRoutine == null)
        {
            RefreshWalletState();
        }
    }

    void OnDisable()
    {
        WalletService.Changed -= RefreshWalletState;

        if (_iceButton != null)
        {
            _iceButton.onClick.RemoveListener(OnIceButtonClicked);
        }
    }

    public void RefreshWalletState()
    {
        _hasCoins = WalletService.HasEnoughCoins(BoosterConfig.UseCostCoins);

        // Sayaç / kullanım sırasında coin yetersizliği InUse görünümünü ezmesin.
        if (_cooldownActive || _activationRoutine != null)
        {
            return;
        }

        ApplyCoinsAvailability();
    }

    void OnDestroy()
    {
        if (_activationRoutine != null)
        {
            StopCoroutine(_activationRoutine);
            _activationRoutine = null;
            _cooldownActive = false;
            IceOpponentFreezeUtility.UnfreezeAllOpponentCoins();
            IceOpponentFreezeUtility.ResumeOpponentBotAfterIceBooster();
        }
    }

    void ResolveReferences()
    {
        if (_iceButton == null)
        {
            _iceButton = GetComponent<Button>();
        }

        if (_backgroundImage == null)
        {
            _backgroundImage = GetComponent<Image>();
        }

        if (_icon == null)
        {
            Transform iconTransform = transform.Find("Icon");
            if (iconTransform != null)
            {
                _icon = iconTransform.gameObject;
            }
        }

        if (_iconImage == null && _icon != null)
        {
            _iconImage = _icon.GetComponent<Image>();
        }

        if (_remainingText == null)
        {
            Transform remainingTransform = transform.Find("Remaining");
            if (remainingTransform != null)
            {
                _remainingText = remainingTransform.GetComponent<TextMeshProUGUI>();
            }
        }

        if (_costText == null)
        {
            Transform costTransform = transform.Find("Cost");
            if (costTransform != null)
            {
                _costText = costTransform.gameObject;
            }
        }

        if (_iceUsedFeedback == null)
        {
            GameObject boostersUsed = GameObject.Find("BoostersUsed");
            if (boostersUsed != null)
            {
                _iceUsedFeedback = boostersUsed.GetComponent<IceUsedBoosterFeedback>();
            }
        }
    }

    void CacheDefaultSprites()
    {
        if (_iceButton != null)
        {
            _defaultButtonTransition = _iceButton.transition;
        }

        if (_activeBackgroundSprite == null && _backgroundImage != null)
        {
            _activeBackgroundSprite = _backgroundImage.sprite;
        }

        if (_activeIconSprite == null && _iconImage != null)
        {
            _activeIconSprite = _iconImage.sprite;
        }
    }

    void OnIceButtonClicked()
    {
        RefreshWalletState();

        if (!_hasCoins || _activationRoutine != null || _cooldownActive)
        {
            return;
        }

        // Coin harcaması Changed tetikler; sayaç başlamadan önce kilitle ki NoCoins uygulanmasın.
        _cooldownActive = true;

        if (!WalletService.TrySpendCoins(BoosterConfig.UseCostCoins))
        {
            _cooldownActive = false;
            RefreshWalletState();
            return;
        }

        _activationRoutine = StartCoroutine(ActivateIceBoosterRoutine());
    }

    IEnumerator ActivateIceBoosterRoutine()
    {
        // Rakip hazır olmayı beklemeden InUse + Cost gizle; aksi halde Cost kısa süre görünür kalır.
        BeginCooldownVisuals(_boosterTiming.CooldownSeconds);

        try
        {
            yield return IceOpponentFreezeUtility.WaitUntilOpponentReadyForIceBooster();

            IceOpponentFreezeUtility.PauseOpponentBotForIceBooster();

            IceOpponentFreezeUtility.FreezeAllOpponentCoins(
                _boosterTiming.FreezeDurationSeconds,
                _frostSettings);
            _iceUsedFeedback?.Play(_usedFeedbackTiming);

            float startTime = Time.time;
            bool coinsUnfrozen = false;

            while (true)
            {
                float elapsed = Time.time - startTime;
                int remainingSeconds = _boosterTiming.CooldownSeconds - Mathf.FloorToInt(elapsed);
                UpdateRemainingText(Mathf.Max(0, remainingSeconds));

                if (!coinsUnfrozen && elapsed >= _boosterTiming.FreezeDurationSeconds)
                {
                    IceOpponentFreezeUtility.UnfreezeAllOpponentCoins();
                    IceOpponentFreezeUtility.ResumeOpponentBotAfterIceBooster();
                    coinsUnfrozen = true;
                }

                if (elapsed >= _boosterTiming.CooldownSeconds)
                {
                    break;
                }

                yield return null;
            }

            UpdateRemainingText(0);
            IceOpponentFreezeUtility.UnfreezeAllOpponentCoins();
        }
        finally
        {
            IceOpponentFreezeUtility.UnfreezeAllOpponentCoins();
            IceOpponentFreezeUtility.ResumeOpponentBotAfterIceBooster();
            _activationRoutine = null;
            _cooldownActive = false;
            RefreshWalletState();
        }
    }

    void BeginCooldownVisuals(int remainingSeconds)
    {
        // Sprite Swap DisabledSprite = Booster_InUse (overrideSprite üzerinden)
        if (_iceButton != null)
        {
            _iceButton.transition = _defaultButtonTransition;
        }

        SetButtonInteractable(false);

        if (_icon != null)
        {
            _icon.SetActive(false);
        }

        if (_costText != null)
        {
            _costText.SetActive(false);
        }

        if (_remainingText != null)
        {
            _remainingText.gameObject.SetActive(true);
            UpdateRemainingText(remainingSeconds);
        }
    }

    void ApplyCoinsAvailability()
    {
        if (_cooldownActive || _activationRoutine != null)
        {
            return;
        }

        EndCooldownChrome();
        ShowCostLabel();

        if (!_hasCoins)
        {
            ApplyLockedVisuals();
            return;
        }

        ApplyActiveVisuals();
        SetButtonInteractable(true);
    }

    void EndCooldownChrome()
    {
        if (_remainingText != null)
        {
            _remainingText.gameObject.SetActive(false);
        }

        if (_icon != null)
        {
            _icon.SetActive(true);
        }
    }

    void ShowCostLabel()
    {
        if (_costText == null)
        {
            return;
        }

        _costText.SetActive(true);

        TextMeshProUGUI label = _costText.GetComponent<TextMeshProUGUI>();
        if (label != null)
        {
            label.text = BoosterConfig.UseCostCoins.ToString();
        }
    }

    void ApplyLockedVisuals()
    {
        ShowCostLabel();

        // Önce Sprite Swap'ı kapat; aksi halde Button'ın InUse overrideSprite'ı kalır.
        if (_iceButton != null)
        {
            _iceButton.transition = Selectable.Transition.None;
            _iceButton.interactable = false;
        }

        SetBackgroundSprite(_lockedBackgroundSprite);

        if (_iconImage != null && _lockedIconSprite != null)
        {
            _iconImage.overrideSprite = null;
            _iconImage.sprite = _lockedIconSprite;
        }

        if (_icon != null)
        {
            _icon.SetActive(true);
        }
    }

    void ApplyActiveVisuals()
    {
        SetBackgroundSprite(_activeBackgroundSprite);

        if (_iconImage != null && _activeIconSprite != null)
        {
            _iconImage.overrideSprite = null;
            _iconImage.sprite = _activeIconSprite;
        }

        if (_iceButton != null)
        {
            _iceButton.transition = _defaultButtonTransition;
        }
    }

    void SetBackgroundSprite(Sprite sprite)
    {
        if (_backgroundImage == null || sprite == null)
        {
            return;
        }

        // Button Sprite Swap, sprite yerine overrideSprite kullanır.
        // Sayaç bitince InUse override'ı temizlenmezse NoCoins görünmez.
        _backgroundImage.overrideSprite = null;
        _backgroundImage.sprite = sprite;
    }

    void UpdateRemainingText(int remainingSeconds)
    {
        if (_remainingText != null)
        {
            _remainingText.text = Mathf.Max(0, remainingSeconds).ToString();
        }
    }

    void SetButtonInteractable(bool interactable)
    {
        if (_iceButton != null)
        {
            _iceButton.interactable = interactable;
        }
    }
}

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TimeBoosterController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] Button _timeButton;
    [SerializeField] Image _backgroundImage;
    [SerializeField] Image _iconImage;
    [SerializeField] GameObject _costText;
    [SerializeField] Sprite _activeBackgroundSprite;
    [SerializeField] Sprite _lockedBackgroundSprite;
    [SerializeField] Sprite _activeIconSprite;
    [SerializeField] Sprite _usedIconSprite;
    [SerializeField] Sprite _lockedIconSprite;

    [Header("Booster")]
    [SerializeField] float _bonusSeconds = 15f;

    [Header("TimeUsed Görsel Süreleri")]
    [SerializeField] IceUsedFeedbackTimingSettings _usedFeedbackTiming = new();

    [Header("Used Feedback")]
    [SerializeField] IceUsedBoosterFeedback _timeUsedFeedback;

    bool _hasCoins = true;
    bool _usedThisMatch;
    bool _subscribedToMatchEvents;
    Coroutine _subscribeRoutine;
    Selectable.Transition _defaultButtonTransition;

    void Awake()
    {
        ResolveReferences();
        CacheDefaultSprites();
    }

    void OnEnable()
    {
        WalletService.Changed += RefreshWalletState;
        SubscribeMatchEvents();

        if (_timeButton != null)
        {
            _timeButton.onClick.AddListener(OnTimeButtonClicked);
        }

        ResetForNewMatch();
        _subscribeRoutine = StartCoroutine(SubscribeWhenReady());
    }

    void OnDisable()
    {
        WalletService.Changed -= RefreshWalletState;

        if (_subscribeRoutine != null)
        {
            StopCoroutine(_subscribeRoutine);
            _subscribeRoutine = null;
        }

        UnsubscribeMatchEvents();

        if (_timeButton != null)
        {
            _timeButton.onClick.RemoveListener(OnTimeButtonClicked);
        }
    }

    public void RefreshWalletState()
    {
        _hasCoins = WalletService.HasEnoughCoins(BoosterConfig.UseCostCoins);

        // Maçta 1 kez kullanıldıysa coin yeterli olsa bile tekrar açılmaz.
        if (_usedThisMatch)
        {
            SetUsedVisuals();
            return;
        }

        if (!_hasCoins)
        {
            ApplyLockedVisuals();
            return;
        }

        ShowCostLabel();
        ApplyActiveVisuals();
        SetButtonInteractable(true);
    }

    IEnumerator SubscribeWhenReady()
    {
        while (LeagueMatchController.Instance == null)
        {
            yield return null;
        }

        SubscribeMatchEvents();
        _subscribeRoutine = null;
    }

    void SubscribeMatchEvents()
    {
        if (_subscribedToMatchEvents || LeagueMatchController.Instance == null)
        {
            return;
        }

        LeagueMatchController.Instance.MatchStarted += OnMatchStarted;
        _subscribedToMatchEvents = true;
    }

    void UnsubscribeMatchEvents()
    {
        if (!_subscribedToMatchEvents || LeagueMatchController.Instance == null)
        {
            return;
        }

        LeagueMatchController.Instance.MatchStarted -= OnMatchStarted;
        _subscribedToMatchEvents = false;
    }

    void OnMatchStarted()
    {
        ResetForNewMatch();
    }

    void OnTimeButtonClicked()
    {
        if (_usedThisMatch)
        {
            return;
        }

        _hasCoins = WalletService.HasEnoughCoins(BoosterConfig.UseCostCoins);
        if (!_hasCoins)
        {
            ApplyLockedVisuals();
            return;
        }

        if (LeagueMatchController.Instance == null || !LeagueMatchController.Instance.IsMatchActive)
        {
            return;
        }

        // Coin harcaması Changed tetikler; used flag önce set edilmeli ki NoCoins ezmesin.
        _usedThisMatch = true;

        if (!WalletService.TrySpendCoins(BoosterConfig.UseCostCoins))
        {
            _usedThisMatch = false;
            RefreshWalletState();
            return;
        }

        if (!LeagueMatchController.Instance.AddMatchTime(_bonusSeconds))
        {
            _usedThisMatch = false;
            WalletService.AddReward(BoosterConfig.UseCostCoins, 0);
            RefreshWalletState();
            return;
        }

        _timeUsedFeedback?.PlayTime(_usedFeedbackTiming);
        SetUsedVisuals();
    }

    void ResetForNewMatch()
    {
        _usedThisMatch = false;
        RefreshWalletState();
    }

    void SetUsedVisuals()
    {
        // Kullanıldı: Disabled Sprite (Booster_InUse) + Time_BW, Cost gizli.
        if (_timeButton != null)
        {
            _timeButton.transition = _defaultButtonTransition;
            _timeButton.interactable = false;
        }

        if (_iconImage != null && _usedIconSprite != null)
        {
            _iconImage.overrideSprite = null;
            _iconImage.sprite = _usedIconSprite;
        }

        if (_costText != null)
        {
            _costText.SetActive(false);
        }
    }

    void ApplyLockedVisuals()
    {
        ShowCostLabel();

        if (_timeButton != null)
        {
            _timeButton.transition = Selectable.Transition.None;
            _timeButton.interactable = false;
        }

        SetBackgroundSprite(_lockedBackgroundSprite);

        if (_iconImage != null && _lockedIconSprite != null)
        {
            _iconImage.overrideSprite = null;
            _iconImage.sprite = _lockedIconSprite;
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

    void ApplyActiveVisuals()
    {
        SetBackgroundSprite(_activeBackgroundSprite);

        if (_iconImage != null && _activeIconSprite != null)
        {
            _iconImage.overrideSprite = null;
            _iconImage.sprite = _activeIconSprite;
        }

        if (_timeButton != null)
        {
            _timeButton.transition = _defaultButtonTransition;
        }
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

    void SetButtonInteractable(bool interactable)
    {
        if (_timeButton != null)
        {
            _timeButton.interactable = interactable;
        }
    }

    void ResolveReferences()
    {
        if (_timeButton == null)
        {
            _timeButton = GetComponent<Button>();
        }

        if (_backgroundImage == null)
        {
            _backgroundImage = GetComponent<Image>();
        }

        if (_iconImage == null)
        {
            Transform iconTransform = transform.Find("Icon");
            if (iconTransform != null)
            {
                _iconImage = iconTransform.GetComponent<Image>();
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

        if (_timeUsedFeedback == null)
        {
            GameObject boostersUsed = GameObject.Find("BoostersUsed");
            if (boostersUsed != null)
            {
                _timeUsedFeedback = boostersUsed.GetComponent<IceUsedBoosterFeedback>();
            }
        }
    }

    void CacheDefaultSprites()
    {
        if (_timeButton != null)
        {
            _defaultButtonTransition = _timeButton.transition;
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
}

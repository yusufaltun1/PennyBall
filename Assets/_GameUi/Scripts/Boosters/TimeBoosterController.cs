using System.Collections;
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

    [Header("Coins")]
    [SerializeField] bool _hasCoins = true;

    [Header("Booster")]
    [SerializeField] float _bonusSeconds = 15f;

    [Header("TimeUsed Görsel Süreleri")]
    [SerializeField] IceUsedFeedbackTimingSettings _usedFeedbackTiming = new();

    [Header("Used Feedback")]
    [SerializeField] IceUsedBoosterFeedback _timeUsedFeedback;

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
        if (!_hasCoins || _usedThisMatch)
        {
            return;
        }

        if (LeagueMatchController.Instance == null || !LeagueMatchController.Instance.IsMatchActive)
        {
            return;
        }

        if (!LeagueMatchController.Instance.AddMatchTime(_bonusSeconds))
        {
            return;
        }

        _usedThisMatch = true;
        _timeUsedFeedback?.PlayTime(_usedFeedbackTiming);
        SetUsedVisuals();
    }

    void ResetForNewMatch()
    {
        _usedThisMatch = false;

        if (!_hasCoins)
        {
            ApplyLockedVisuals();
            return;
        }

        if (_costText != null)
        {
            _costText.SetActive(true);
        }

        ApplyActiveVisuals();

        if (_timeButton != null)
        {
            _timeButton.interactable = true;
        }
    }

    void SetUsedVisuals()
    {
        if (_timeButton != null)
        {
            _timeButton.interactable = false;
        }

        if (_iconImage != null && _usedIconSprite != null)
        {
            _iconImage.sprite = _usedIconSprite;
        }

        if (_costText != null)
        {
            _costText.SetActive(false);
        }
    }

    void ApplyLockedVisuals()
    {
        if (_backgroundImage != null && _lockedBackgroundSprite != null)
        {
            _backgroundImage.sprite = _lockedBackgroundSprite;
        }

        if (_iconImage != null && _lockedIconSprite != null)
        {
            _iconImage.sprite = _lockedIconSprite;
        }

        if (_timeButton != null)
        {
            _timeButton.transition = Selectable.Transition.None;
            _timeButton.interactable = false;
        }
    }

    void ApplyActiveVisuals()
    {
        if (_backgroundImage != null && _activeBackgroundSprite != null)
        {
            _backgroundImage.sprite = _activeBackgroundSprite;
        }

        if (_iconImage != null && _activeIconSprite != null)
        {
            _iconImage.sprite = _activeIconSprite;
        }

        if (_timeButton != null)
        {
            _timeButton.transition = _defaultButtonTransition;
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

using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UnlockedFeaturesController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] RectTransform _particleBurst;
    [SerializeField] RectTransform _anons;
    [SerializeField] RectTransform _poster;
    [SerializeField] RectTransform _desc;
    [SerializeField] RectTransform _continueButton;

    [Header("Feature Selection")]
    [SerializeField] bool _freeze;
    [SerializeField] bool _time;
    [SerializeField] bool _goalKeeper;
    [SerializeField] bool _lastCoinEnable;

    BoosterType _activeFeature = BoosterType.Freeze;
    Action _onContinue;
    bool _playOnEnable;
    Button _continueButtonComponent;

    [Header("Freeze Content")]
    [SerializeField] Sprite _freezePosterSprite;
    [SerializeField] string _freezeDescription = "Freeze opponent for 5s";

    [Header("Time Content")]
    [SerializeField] Sprite _timePosterSprite;
    [SerializeField] string _timeDescription = "Extend the match for 15 seconds";

    [Header("Goal Keeper Content")]
    [SerializeField] Sprite _goalKeeperPosterSprite;
    [SerializeField] string _goalKeeperDescription = "Use a Goal Keeper for 5 seconds";

    [Header("Last Coin Content")]
    [SerializeField] Sprite _lastCoinPosterSprite;
    [SerializeField] string _lastCoinDescription = "Activate latest coin";

    [Header("Particle Burst")]
    [SerializeField] UIParticleBurstSettings _burstSettings = new()
    {
        particleCount = 24,
        burstCoverage = 1f,
        expandDuration = 0.75f,
        fallDuration = 1f,
        gravity = 1450f,
        horizontalDrift = 240f,
    };

    [Header("Anons")]
    [SerializeField] float _anonsIntroDuration = 0.4f;
    [SerializeField] float _anonsHoldDuration = 0.25f;
    [SerializeField] float _anonsExitDuration = 0.4f;
    [SerializeField] float _anonsStartScale = 0.1f;
    [SerializeField] float _anonsEndScale = 0.1f;

    [Header("Poster / Desc")]
    [SerializeField] float _slideOffsetY = 100f;
    [SerializeField] float _slideDuration = 0.4f;

    [Header("Btn Continue")]
    [SerializeField] float _continueDelayAfterDesc = 1f;
    [SerializeField] float _continueDuration = 0.5f;
    [SerializeField] float _continueOffscreenPadding = 120f;

    RectTransform _canvasRect;
    Vector2 _posterRestPosition;
    Vector2 _descRestPosition;
    Vector2 _continueRestPosition;
    Coroutine _sequenceRoutine;
    readonly UIParticleBurstPlayer _burstPlayer = new();
    Image _posterImage;
    TextMeshProUGUI _descText;

    void Awake()
    {
        ResolveReferences();
        WireContinueButton();
    }

    public void Configure(BoosterType feature, Action onContinue)
    {
        _activeFeature = feature;
        _onContinue = onContinue;
        _playOnEnable = true;

        _freeze = feature == BoosterType.Freeze;
        _time = feature == BoosterType.Time;
        _goalKeeper = feature == BoosterType.GoalKeeper;
        _lastCoinEnable = feature == BoosterType.LastCoin;
    }

    void WireContinueButton()
    {
        if (_continueButton == null)
        {
            return;
        }

        _continueButtonComponent = _continueButton.GetComponent<Button>();
        if (_continueButtonComponent != null)
        {
            _continueButtonComponent.onClick.RemoveListener(OnContinueClicked);
            _continueButtonComponent.onClick.AddListener(OnContinueClicked);
        }
    }

    void OnContinueClicked()
    {
        Action callback = _onContinue;
        _onContinue = null;
        _playOnEnable = false;

        gameObject.SetActive(false);

        BoostersMenuController menu = FindAnyObjectByType<BoostersMenuController>(FindObjectsInactive.Include);
        menu?.Refresh();

        callback?.Invoke();
    }

    void OnEnable()
    {
        if (!_playOnEnable)
        {
            return;
        }

        if (_sequenceRoutine != null)
        {
            StopCoroutine(_sequenceRoutine);
        }

        _sequenceRoutine = StartCoroutine(PlaySequence());
    }

    void OnDisable()
    {
        if (_sequenceRoutine != null)
        {
            StopCoroutine(_sequenceRoutine);
            _sequenceRoutine = null;
        }

        _burstPlayer.Clear();
    }

    void ResolveReferences()
    {
        if (_particleBurst == null)
        {
            _particleBurst = transform.Find("ParticleBurst") as RectTransform;
        }

        if (_anons == null)
        {
            _anons = transform.Find("Anons") as RectTransform;
        }

        if (_poster == null)
        {
            _poster = transform.Find("Poster") as RectTransform;
        }

        if (_desc == null)
        {
            _desc = transform.Find("Desc") as RectTransform;
        }

        if (_continueButton == null)
        {
            _continueButton = transform.Find("Btn_Continue") as RectTransform;
        }

        if (_posterImage == null && _poster != null)
        {
            _posterImage = _poster.GetComponent<Image>();
        }

        if (_descText == null && _desc != null)
        {
            _descText = _desc.GetComponent<TextMeshProUGUI>();
        }
    }

    void ApplyFeatureContent()
    {
        switch (_activeFeature)
        {
            case BoosterType.Time:
                ApplyPoster(_timePosterSprite);
                ApplyDescription(_timeDescription);
                return;
            case BoosterType.GoalKeeper:
                ApplyPoster(_goalKeeperPosterSprite);
                ApplyDescription(_goalKeeperDescription);
                return;
            case BoosterType.LastCoin:
                ApplyPoster(_lastCoinPosterSprite);
                ApplyDescription(_lastCoinDescription);
                return;
            default:
                ApplyPoster(_freezePosterSprite);
                ApplyDescription(_freezeDescription);
                return;
        }
    }

    void ApplyPoster(Sprite sprite)
    {
        if (_posterImage != null && sprite != null)
        {
            _posterImage.sprite = sprite;
        }
    }

    void ApplyDescription(string description)
    {
        if (_descText != null && !string.IsNullOrEmpty(description))
        {
            _descText.text = description;
        }
    }

    IEnumerator PlaySequence()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();

        _canvasRect = GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
        PrepareInitialState();

        LevelUpSound.Play();

        if (_particleBurst != null)
        {
            EnsureBurstMask();
            _burstPlayer.Prepare(_particleBurst, _burstSettings);
            StartCoroutine(_burstPlayer.PlayRoutine());
        }

        if (_anons != null)
        {
            _anons.gameObject.SetActive(true);
            _anons.localScale = Vector3.one * _anonsStartScale;
            SetAlpha(_anons, 1f);

            yield return AnimateScale(_anons, Vector3.one * _anonsStartScale, Vector3.one, _anonsIntroDuration, EaseOutBack);

            if (_anonsHoldDuration > 0f)
            {
                yield return new WaitForSeconds(_anonsHoldDuration);
            }

            Coroutine posterRoutine = null;
            if (_poster != null)
            {
                posterRoutine = StartCoroutine(PlaySlideIn(_poster, _posterRestPosition));
            }

            yield return AnimateAnonsExit();

            if (posterRoutine != null)
            {
                yield return posterRoutine;
            }

            if (_desc != null)
            {
                yield return PlaySlideIn(_desc, _descRestPosition);
            }

            if (_continueDelayAfterDesc > 0f)
            {
                yield return new WaitForSeconds(_continueDelayAfterDesc);
            }

            if (_continueButton != null)
            {
                _continueButton.gameObject.SetActive(true);
                Vector2 continueStart = GetContinueStartPosition();
                _continueButton.anchoredPosition = continueStart;
                yield return AnimatePosition(
                    _continueButton,
                    continueStart,
                    _continueRestPosition,
                    _continueDuration,
                    EaseOutBack);
            }
        }

        _sequenceRoutine = null;
    }

    void EnsureBurstMask()
    {
        if (GetComponent<RectMask2D>() == null)
        {
            gameObject.AddComponent<RectMask2D>();
        }
    }

    IEnumerator AnimateAnonsExit()
    {
        if (_anons == null)
        {
            yield break;
        }

        CanvasGroup canvasGroup = EnsureCanvasGroup(_anons.gameObject);
        Vector3 fromScale = _anons.localScale;
        Vector3 toScale = Vector3.one * _anonsEndScale;
        float fromAlpha = canvasGroup.alpha;
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, _anonsExitDuration);

        while (elapsed < safeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            _anons.localScale = Vector3.LerpUnclamped(fromScale, toScale, t);
            canvasGroup.alpha = Mathf.LerpUnclamped(fromAlpha, 0f, t);
            yield return null;
        }

        _anons.localScale = toScale;
        canvasGroup.alpha = 0f;
        _anons.gameObject.SetActive(false);
        canvasGroup.alpha = 1f;
        _anons.localScale = Vector3.one * _anonsStartScale;
    }

    IEnumerator PlaySlideIn(RectTransform rect, Vector2 restPosition)
    {
        if (rect == null)
        {
            yield break;
        }

        rect.gameObject.SetActive(true);
        CanvasGroup canvasGroup = EnsureCanvasGroup(rect.gameObject);
        Vector2 startPosition = restPosition + new Vector2(0f, -_slideOffsetY);
        rect.anchoredPosition = startPosition;
        canvasGroup.alpha = 0f;

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, _slideDuration);

        while (elapsed < safeDuration)
        {
            elapsed += Time.deltaTime;
            float t = EaseInOut(Mathf.Clamp01(elapsed / safeDuration));
            rect.anchoredPosition = Vector2.LerpUnclamped(startPosition, restPosition, t);
            canvasGroup.alpha = t;
            yield return null;
        }

        rect.anchoredPosition = restPosition;
        canvasGroup.alpha = 1f;
    }

    void PrepareInitialState()
    {
        ApplyFeatureContent();

        if (_anons != null)
        {
            _anons.gameObject.SetActive(false);
            _anons.localScale = Vector3.one * _anonsStartScale;
            SetAlpha(_anons, 1f);
        }

        if (_poster != null)
        {
            _posterRestPosition = _poster.anchoredPosition;
            _poster.gameObject.SetActive(false);
            SetAlpha(_poster, 0f);
        }

        if (_desc != null)
        {
            _descRestPosition = _desc.anchoredPosition;
            _desc.gameObject.SetActive(false);
            SetAlpha(_desc, 0f);
        }

        if (_continueButton != null)
        {
            _continueRestPosition = _continueButton.anchoredPosition;
            _continueButton.gameObject.SetActive(false);
            _continueButton.anchoredPosition = GetContinueStartPosition();
        }
    }

    Vector2 GetContinueStartPosition()
    {
        float canvasHalfHeight = GetCanvasHalfHeight();
        float height = _continueButton != null ? _continueButton.rect.height : 0f;

        return new Vector2(
            _continueRestPosition.x,
            _continueRestPosition.y - canvasHalfHeight - height - _continueOffscreenPadding);
    }

    float GetCanvasHalfHeight()
    {
        const float defaultHalfHeight = 1170f;

        if (_canvasRect == null || _canvasRect.rect.height <= 0f)
        {
            return defaultHalfHeight;
        }

        return _canvasRect.rect.height * 0.5f;
    }

    static void SetAlpha(RectTransform rect, float alpha)
    {
        if (rect == null)
        {
            return;
        }

        EnsureCanvasGroup(rect.gameObject).alpha = alpha;
    }

    static CanvasGroup EnsureCanvasGroup(GameObject target)
    {
        CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = target.AddComponent<CanvasGroup>();
        }

        return canvasGroup;
    }

    static IEnumerator AnimateScale(
        RectTransform rect,
        Vector3 fromScale,
        Vector3 toScale,
        float duration,
        System.Func<float, float> ease)
    {
        if (rect == null)
        {
            yield break;
        }

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);

        while (elapsed < safeDuration)
        {
            elapsed += Time.deltaTime;
            float t = ease(Mathf.Clamp01(elapsed / safeDuration));
            rect.localScale = Vector3.LerpUnclamped(fromScale, toScale, t);
            yield return null;
        }

        rect.localScale = toScale;
    }

    static IEnumerator AnimatePosition(
        RectTransform rect,
        Vector2 fromPosition,
        Vector2 toPosition,
        float duration,
        System.Func<float, float> ease)
    {
        if (rect == null)
        {
            yield break;
        }

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);

        while (elapsed < safeDuration)
        {
            elapsed += Time.deltaTime;
            float t = ease(Mathf.Clamp01(elapsed / safeDuration));
            rect.anchoredPosition = Vector2.LerpUnclamped(fromPosition, toPosition, t);
            yield return null;
        }

        rect.anchoredPosition = toPosition;
    }

    static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    static float EaseInOut(float t)
    {
        return t * t * (3f - 2f * t);
    }
}

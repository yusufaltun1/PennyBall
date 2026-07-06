using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class OnboardingCompleteController : MonoBehaviour
{
    public const int OnboardingBonusCoins = 40;

    [SerializeField] RectTransform _particleBurst;
    [SerializeField] RectTransform _imageContainer;
    [SerializeField] RectTransform _wellDoneContent;
    [SerializeField] RectTransform _coins;
    [SerializeField] RectTransform _claimButton;

    [SerializeField] UIParticleBurstSettings _burstSettings = new()
    {
        particleCount = 24,
        burstCoverage = 1f,
        particleSizeRange = new Vector2(18f, 42f),
        expandDuration = 0.75f,
        fallDuration = 1f,
        gravity = 1450f,
        horizontalDrift = 240f,
        particleColors = new[]
        {
            new Color(1f, 0.84f, 0.1f, 1f),
            new Color(0.2f, 0.95f, 0.45f, 1f),
            new Color(1f, 0.45f, 0.1f, 1f),
            new Color(0.35f, 0.75f, 1f, 1f),
            new Color(1f, 0.3f, 0.55f, 1f),
        },
    };

    [SerializeField] float _introScaleDuration = 0.45f;
    [SerializeField] float _imageHoldDuration = 1.5f;
    [SerializeField] float _claimPhaseDelay = 1f;
    [SerializeField] float _coinsEnterDuration = 0.45f;
    [SerializeField] float _imageExitDuration = 0.35f;
    [SerializeField] float _claimDuration = 0.5f;
    [SerializeField] float _coinsEnterOffsetY = 50f;
    [SerializeField] float _offscreenPadding = 120f;

    const float StartScale = 0.1f;

    RectTransform _canvasRect;
    Vector2 _imageRestPosition;
    Vector2 _coinsRestPosition;
    Vector2 _claimFinalPosition;
    Coroutine _sequenceRoutine;
    readonly UIParticleBurstPlayer _burstPlayer = new();
    bool _claimed;

    void Awake()
    {
        ResolveReferences();
        WireClaimButton();
    }

    void OnEnable()
    {
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

    public void ClaimToExercise()
    {
        if (_claimed)
        {
            return;
        }

        _claimed = true;
        MainMenuClickSound.Play();
        WalletService.AddReward(OnboardingBonusCoins, 0);
        SceneManager.LoadScene(GameSceneNames.Exercise);
    }

    void WireClaimButton()
    {
        if (_claimButton == null)
        {
            return;
        }

        Button button = _claimButton.GetComponent<Button>();
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(ClaimToExercise);
        button.onClick.AddListener(ClaimToExercise);
    }

    void ResolveReferences()
    {
        if (_particleBurst == null)
        {
            _particleBurst = transform.Find("ParticleBurst") as RectTransform;
        }

        Transform image = transform.Find("Image");
        if (_imageContainer == null && image != null)
        {
            _imageContainer = image as RectTransform;
        }

        if (_wellDoneContent == null && image != null)
        {
            _wellDoneContent = image.Find("Update") as RectTransform;
        }

        if (_coins == null)
        {
            _coins = transform.Find("Coins") as RectTransform;
        }

        if (_claimButton == null)
        {
            _claimButton = transform.Find("Btn_Claim") as RectTransform;
        }
    }

    IEnumerator PlaySequence()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();

        _canvasRect = GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
        PrepareInitialState();

        WellDoneSound.Play();

        Coroutine burstRoutine = null;
        Coroutine introRoutine = null;

        if (_particleBurst != null)
        {
            EnsureBurstMask();
            _burstPlayer.Prepare(_particleBurst, _burstSettings);
            burstRoutine = StartCoroutine(_burstPlayer.PlayRoutine());
        }

        if (_wellDoneContent != null)
        {
            _wellDoneContent.gameObject.SetActive(true);
            _wellDoneContent.localScale = Vector3.one * StartScale;
            introRoutine = StartCoroutine(
                AnimateScale(_wellDoneContent, Vector3.one * StartScale, Vector3.one, _introScaleDuration));
        }

        yield return WaitForAll(introRoutine);

        if (_imageHoldDuration > 0f)
        {
            yield return new WaitForSeconds(_imageHoldDuration);
        }

        Coroutine coinsRoutine = null;
        Coroutine imageExitRoutine = null;

        if (_coins != null)
        {
            _coins.gameObject.SetActive(true);
            Vector2 coinsStart = _coinsRestPosition + new Vector2(0f, -_coinsEnterOffsetY);
            _coins.anchoredPosition = coinsStart;
            _coins.localScale = Vector3.one * StartScale;
            coinsRoutine = StartCoroutine(AnimateCoinsEnter(coinsStart, _coinsRestPosition));
        }

        if (_imageContainer != null)
        {
            Vector2 exitTarget = GetImageExitTarget();
            imageExitRoutine = StartCoroutine(
                AnimatePosition(_imageContainer, _imageRestPosition, exitTarget, _imageExitDuration, EaseInQuad));
        }

        if (_claimPhaseDelay > 0f)
        {
            yield return new WaitForSeconds(_claimPhaseDelay);
        }

        Coroutine claimRoutine = null;
        if (_claimButton != null)
        {
            _claimButton.gameObject.SetActive(true);
            Vector2 claimStart = GetClaimStartPosition();
            _claimButton.anchoredPosition = claimStart;
            claimRoutine = StartCoroutine(
                AnimatePosition(_claimButton, claimStart, _claimFinalPosition, _claimDuration, EaseOutBack));
        }

        yield return WaitForAll(burstRoutine, introRoutine, coinsRoutine, imageExitRoutine, claimRoutine);
        _sequenceRoutine = null;
    }

    IEnumerator AnimateCoinsEnter(Vector2 fromPosition, Vector2 toPosition)
    {
        if (_coins == null)
        {
            yield break;
        }

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, _coinsEnterDuration);
        Vector3 fromScale = Vector3.one * StartScale;
        Vector3 toScale = Vector3.one;

        while (elapsed < safeDuration)
        {
            elapsed += Time.deltaTime;
            float t = EaseOutBack(Mathf.Clamp01(elapsed / safeDuration));
            _coins.anchoredPosition = Vector2.LerpUnclamped(fromPosition, toPosition, t);
            _coins.localScale = Vector3.LerpUnclamped(fromScale, toScale, t);
            yield return null;
        }

        _coins.anchoredPosition = toPosition;
        _coins.localScale = toScale;
    }

    IEnumerator WaitForAll(params Coroutine[] routines)
    {
        if (routines == null || routines.Length == 0)
        {
            yield break;
        }

        int remaining = 0;
        for (int i = 0; i < routines.Length; i++)
        {
            if (routines[i] != null)
            {
                remaining++;
            }
        }

        if (remaining == 0)
        {
            yield break;
        }

        int completed = 0;
        for (int i = 0; i < routines.Length; i++)
        {
            Coroutine routine = routines[i];
            if (routine == null)
            {
                continue;
            }

            StartCoroutine(TrackRoutine(routine, () => completed++));
        }

        while (completed < remaining)
        {
            yield return null;
        }
    }

    IEnumerator TrackRoutine(Coroutine routine, Action onComplete)
    {
        yield return routine;
        onComplete?.Invoke();
    }

    void EnsureBurstMask()
    {
        if (GetComponent<RectMask2D>() == null)
        {
            gameObject.AddComponent<RectMask2D>();
        }
    }

    void PrepareInitialState()
    {
        if (_imageContainer != null)
        {
            _imageRestPosition = _imageContainer.anchoredPosition;
        }

        if (_wellDoneContent != null)
        {
            _wellDoneContent.gameObject.SetActive(false);
            _wellDoneContent.localScale = Vector3.one * StartScale;
        }

        if (_coins != null)
        {
            _coinsRestPosition = _coins.anchoredPosition;
            _coins.gameObject.SetActive(false);
            _coins.localScale = Vector3.one * StartScale;
        }

        if (_claimButton != null)
        {
            _claimFinalPosition = _claimButton.anchoredPosition;
            _claimButton.gameObject.SetActive(false);
            _claimButton.anchoredPosition = GetClaimStartPosition();
        }
    }

    Vector2 GetImageExitTarget()
    {
        float canvasHalfHeight = GetCanvasHalfHeight();
        float height = _imageContainer != null ? _imageContainer.rect.height : 0f;

        return new Vector2(
            _imageRestPosition.x,
            _imageRestPosition.y + canvasHalfHeight + height + _offscreenPadding);
    }

    Vector2 GetClaimStartPosition()
    {
        float canvasHalfHeight = GetCanvasHalfHeight();
        float height = _claimButton != null ? _claimButton.rect.height : 0f;

        return new Vector2(
            _claimFinalPosition.x,
            _claimFinalPosition.y - canvasHalfHeight - height - _offscreenPadding);
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

    IEnumerator AnimateScale(RectTransform rect, Vector3 fromScale, Vector3 toScale, float duration)
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
            float t = EaseOutBack(Mathf.Clamp01(elapsed / safeDuration));
            rect.localScale = Vector3.LerpUnclamped(fromScale, toScale, t);
            yield return null;
        }

        rect.localScale = toScale;
    }

    IEnumerator AnimatePosition(
        RectTransform rect,
        Vector2 fromPosition,
        Vector2 toPosition,
        float duration,
        Func<float, float> ease)
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

    static float EaseInQuad(float t)
    {
        return t * t;
    }
}

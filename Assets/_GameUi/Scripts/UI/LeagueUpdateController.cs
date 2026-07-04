using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LeagueUpdateController : MonoBehaviour
{
    [SerializeField] RectTransform _particleBurst;
    [SerializeField] RectTransform _update;
    [SerializeField] RectTransform _flare;
    [SerializeField] RectTransform _coins;
    [SerializeField] RectTransform _claimButton;

    [SerializeField] UIParticleBurstSettings _burstSettings = new()
    {
        particleCount = 12,
    };

    [SerializeField] float _scaleDuration = 0.45f;
    [SerializeField] float _postIntroDelay = 0.5f;
    [SerializeField] float _updateMoveDuration = 0.45f;
    [SerializeField] float _coinsClaimDelay = 0.25f;
    [SerializeField] float _claimDuration = 0.5f;
    [SerializeField] float _flareHalfDuration = 0.5f;
    [SerializeField] float _claimOffscreenPadding = 120f;

    const float StartScale = 0.1f;

    RectTransform _canvasRect;
    Vector2 _updateRestPosition;
    Vector2 _claimFinalPosition;
    Coroutine _sequenceRoutine;
    Coroutine _flareRoutine;
    readonly UIParticleBurstPlayer _burstPlayer = new();

    void Awake()
    {
        ResolveReferences();
    }

    void OnEnable()
    {
        if (_sequenceRoutine != null)
        {
            StopCoroutine(_sequenceRoutine);
        }

        if (_flareRoutine != null)
        {
            StopCoroutine(_flareRoutine);
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

        if (_flareRoutine != null)
        {
            StopCoroutine(_flareRoutine);
            _flareRoutine = null;
        }

        _burstPlayer.Clear();
    }

    public void ClaimToMainMenu()
    {
        MainMenuClickSound.Play();
        SceneManager.LoadScene(GameSceneNames.MainMenu);
    }

    void ResolveReferences()
    {
        if (_particleBurst == null)
        {
            _particleBurst = transform.Find("ParticleBurst") as RectTransform;
        }

        Transform image = transform.Find("Image");

        if (_update == null && image != null)
        {
            _update = image.Find("Update") as RectTransform;
        }

        if (_flare == null && _update != null)
        {
            _flare = _update.Find("Flare") as RectTransform;
        }

        if (_coins == null && image != null)
        {
            _coins = image.Find("Coins") as RectTransform;
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

        bool startsIntro = _particleBurst != null || _update != null;
        if (startsIntro)
        {
            LevelUpSound.Play();
        }

        Coroutine burstRoutine = null;
        Coroutine updateRoutine = null;

        if (_particleBurst != null)
        {
            EnsureBurstMask();
            _burstPlayer.Prepare(_particleBurst, _burstSettings);
            burstRoutine = StartCoroutine(_burstPlayer.PlayRoutine());
        }

        if (_update != null)
        {
            _update.gameObject.SetActive(true);
            _update.localScale = Vector3.one * StartScale;

            if (_flare != null)
            {
                _flare.localScale = Vector3.one;
                _flareRoutine = StartCoroutine(FlarePulseLoop());
            }

            updateRoutine = StartCoroutine(
                AnimateScale(_update, Vector3.one * StartScale, Vector3.one, _scaleDuration));
        }

        yield return WaitForAll(burstRoutine, updateRoutine);

        if (_postIntroDelay > 0f)
        {
            yield return new WaitForSeconds(_postIntroDelay);
        }

        Coroutine updateMoveRoutine = null;
        if (_update != null)
        {
            Vector2 moveTarget = GetUpdateMoveUpTarget();
            updateMoveRoutine = StartCoroutine(
                AnimatePosition(_update, _updateRestPosition, moveTarget, _updateMoveDuration));
        }

        if (_coinsClaimDelay > 0f)
        {
            yield return new WaitForSeconds(_coinsClaimDelay);
        }

        Coroutine coinsRoutine = null;
        Coroutine claimRoutine = null;

        if (_coins != null)
        {
            _coins.gameObject.SetActive(true);
            _coins.localScale = Vector3.one * StartScale;
            coinsRoutine = StartCoroutine(
                AnimateScale(_coins, Vector3.one * StartScale, Vector3.one, _scaleDuration));
        }

        if (_claimButton != null)
        {
            _claimButton.gameObject.SetActive(true);
            Vector2 claimStart = GetClaimStartPosition();
            _claimButton.anchoredPosition = claimStart;
            claimRoutine = StartCoroutine(
                AnimatePosition(_claimButton, claimStart, _claimFinalPosition, _claimDuration));
        }

        yield return WaitForAll(updateMoveRoutine, coinsRoutine, claimRoutine);

        _sequenceRoutine = null;
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
        if (_update != null)
        {
            _update.gameObject.SetActive(false);
            _update.localScale = Vector3.one * StartScale;
            _updateRestPosition = _update.anchoredPosition;
        }

        if (_coins != null)
        {
            _coins.gameObject.SetActive(false);
            _coins.localScale = Vector3.one * StartScale;
        }

        if (_flare != null)
        {
            _flare.localScale = Vector3.one;
        }

        if (_claimButton != null)
        {
            _claimFinalPosition = _claimButton.anchoredPosition;
            _claimButton.gameObject.SetActive(false);
            _claimButton.anchoredPosition = GetClaimStartPosition();
        }
    }

    Vector2 GetUpdateMoveUpTarget()
    {
        if (_update == null || _canvasRect == null)
        {
            return _updateRestPosition;
        }

        Vector2 updateCenterCanvas = GetLocalPointInCanvas(_update, _canvasRect);
        float canvasTop = _canvasRect.rect.yMax;
        float moveUp = (canvasTop - updateCenterCanvas.y) * 0.5f;

        return _updateRestPosition + new Vector2(0f, moveUp);
    }

    static Vector2 GetLocalPointInCanvas(RectTransform rect, RectTransform canvas)
    {
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, rect.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screenPoint, null, out Vector2 localPoint);
        return localPoint;
    }

    Vector2 GetClaimStartPosition()
    {
        float canvasHalfHeight = GetCanvasHalfHeight();
        float height = _claimButton != null ? _claimButton.rect.height : 0f;

        return new Vector2(
            _claimFinalPosition.x,
            _claimFinalPosition.y - canvasHalfHeight - height - _claimOffscreenPadding);
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

    IEnumerator FlarePulseLoop()
    {
        Vector3 minScale = Vector3.one;
        Vector3 maxScale = Vector3.one * 1.1f;
        float duration = Mathf.Max(0.01f, _flareHalfDuration * 2f);

        while (true)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = (Mathf.Sin((elapsed / duration) * Mathf.PI * 2f - Mathf.PI * 0.5f) + 1f) * 0.5f;
                _flare.localScale = Vector3.LerpUnclamped(minScale, maxScale, t);
                yield return null;
            }
        }
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

    IEnumerator AnimatePosition(RectTransform rect, Vector2 fromPosition, Vector2 toPosition, float duration)
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
}

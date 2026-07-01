using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyGoalEffectController : MonoBehaviour
{
    public static EnemyGoalEffectController Instance { get; private set; }

    [Header("References")]
    [SerializeField] RectTransform _aaahhTarget;

    [Header("Grow In")]
    [SerializeField] float _scaleFactor = 1.44f;
    [SerializeField] float _animationDuration = 0.8f;
    [SerializeField] float _screenHoldDuration = 1.2f;

    Coroutine _playRoutine;
    MonoBehaviour _routineHost;
    Vector3 _aaahhBaseScale = Vector3.one;

    public static EnemyGoalEffectController EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        return FindFirstObjectByType<EnemyGoalEffectController>(FindObjectsInactive.Include);
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        ResolveAaahhTarget();
        CacheAaahhBaseScale();
    }

    void OnDestroy()
    {
        StopActiveRoutine();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    void ResolveAaahhTarget()
    {
        if (_aaahhTarget != null)
        {
            return;
        }

        Transform aaahh = transform.Find("Aaahh");
        if (aaahh is RectTransform rect)
        {
            _aaahhTarget = rect;
        }
    }

    void CacheAaahhBaseScale()
    {
        if (_aaahhTarget != null && _aaahhTarget.localScale.sqrMagnitude > 0.0001f)
        {
            _aaahhBaseScale = _aaahhTarget.localScale;
        }
        else
        {
            _aaahhBaseScale = Vector3.one;
        }
    }

    float GetStartScaleMultiplier()
    {
        return 1f / Mathf.Max(_scaleFactor, 0.01f);
    }

    MonoBehaviour ResolveRoutineHost()
    {
        if (GameRulesManager.Instance != null)
        {
            return GameRulesManager.Instance;
        }

        return this;
    }

    void StopActiveRoutine()
    {
        if (_playRoutine == null)
        {
            return;
        }

        MonoBehaviour host = _routineHost != null ? _routineHost : ResolveRoutineHost();
        if (host != null)
        {
            host.StopCoroutine(_playRoutine);
        }

        _playRoutine = null;
        _routineHost = null;
    }

    public bool CanPlay()
    {
        ResolveAaahhTarget();
        return _aaahhTarget != null;
    }

    public void Play(Action onComplete)
    {
        ResolveAaahhTarget();
        if (_aaahhTarget == null)
        {
            onComplete?.Invoke();
            return;
        }

        StopActiveRoutine();
        CacheAaahhBaseScale();

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        _aaahhTarget.localScale = _aaahhBaseScale * GetStartScaleMultiplier();

        _routineHost = ResolveRoutineHost();
        if (_routineHost == null)
        {
            gameObject.SetActive(false);
            onComplete?.Invoke();
            return;
        }

        _playRoutine = _routineHost.StartCoroutine(PlayRoutine(onComplete));
    }

    IEnumerator PlayRoutine(Action onComplete)
    {
        yield return GrowAaahhRoutine();

        if (_screenHoldDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(_screenHoldDuration);
        }

        gameObject.SetActive(false);
        _playRoutine = null;
        _routineHost = null;
        onComplete?.Invoke();
    }

    IEnumerator GrowAaahhRoutine()
    {
        if (_aaahhTarget == null)
        {
            yield break;
        }

        float duration = Mathf.Max(0.01f, _animationDuration);
        float startMultiplier = GetStartScaleMultiplier();
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float eased = EaseOutBack(normalized);
            float scaleMultiplier = Mathf.Lerp(startMultiplier, 1f, eased);
            _aaahhTarget.localScale = _aaahhBaseScale * scaleMultiplier;
            yield return null;
        }

        _aaahhTarget.localScale = _aaahhBaseScale;
    }

    static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class IceUsedBoosterFeedback : MonoBehaviour
{
    [SerializeField] RectTransform _iceUsed;
    [SerializeField] Image _iceUsedImage;

    [Header("Varsayılan Süreler")]
    [SerializeField] IceUsedFeedbackTimingSettings _defaultTiming = new();

    IceUsedFeedbackTimingSettings _activeTiming;
    Coroutine _routine;
    Color _baseColor = Color.white;

    void Awake()
    {
        ResolveReferences();
        HideImmediate();
    }

    public void Play(IceUsedFeedbackTimingSettings timing = null)
    {
        if (_iceUsed == null)
        {
            return;
        }

        _activeTiming = timing ?? _defaultTiming;
        if (_activeTiming == null)
        {
            _activeTiming = new IceUsedFeedbackTimingSettings();
        }

        if (_routine != null)
        {
            StopCoroutine(_routine);
        }

        _routine = StartCoroutine(PlayRoutine());
    }

    void ResolveReferences()
    {
        if (_iceUsed == null)
        {
            Transform iceUsedTransform = transform.Find("IceUsed");
            if (iceUsedTransform != null)
            {
                _iceUsed = iceUsedTransform as RectTransform;
            }
        }

        if (_iceUsedImage == null && _iceUsed != null)
        {
            _iceUsedImage = _iceUsed.GetComponent<Image>();
        }

        if (_iceUsedImage != null)
        {
            _baseColor = _iceUsedImage.color;
        }
    }

    void HideImmediate()
    {
        if (_iceUsed == null)
        {
            return;
        }

        IceUsedFeedbackTimingSettings timing = _activeTiming ?? _defaultTiming ?? new IceUsedFeedbackTimingSettings();
        _iceUsed.localScale = Vector3.one * timing.BounceEndScale;
        SetAlpha(1f);
        _iceUsed.gameObject.SetActive(false);
    }

    IEnumerator PlayRoutine()
    {
        IceUsedFeedbackTimingSettings timing = _activeTiming ?? _defaultTiming ?? new IceUsedFeedbackTimingSettings();

        _iceUsed.gameObject.SetActive(true);
        _iceUsed.localScale = Vector3.one * timing.BounceStartScale;
        SetAlpha(1f);

        float elapsed = 0f;
        while (elapsed < timing.BounceInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = EaseOutBack(Mathf.Clamp01(elapsed / timing.BounceInDuration));
            float scale = Mathf.LerpUnclamped(timing.BounceStartScale, timing.BounceEndScale, t);
            _iceUsed.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }

        _iceUsed.localScale = Vector3.one * timing.BounceEndScale;

        elapsed = 0f;
        Vector3 startScale = Vector3.one * timing.BounceEndScale;
        Vector3 endScale = Vector3.one * timing.ExitScale;
        while (elapsed < timing.ExpandFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / timing.ExpandFadeDuration);
            _iceUsed.localScale = Vector3.Lerp(startScale, endScale, t);
            SetAlpha(Mathf.Lerp(1f, 0f, t));
            yield return null;
        }

        HideImmediate();
        _routine = null;
    }

    void SetAlpha(float alpha)
    {
        if (_iceUsedImage == null)
        {
            return;
        }

        Color color = _baseColor;
        color.a = alpha;
        _iceUsedImage.color = color;
    }

    static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}

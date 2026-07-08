using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class IceUsedBoosterFeedback : MonoBehaviour
{
    [SerializeField] RectTransform _iceUsed;
    [SerializeField] Image _iceUsedImage;
    [SerializeField] RectTransform _timeUsed;
    [SerializeField] Image _timeUsedImage;

    [Header("Varsayılan Süreler")]
    [SerializeField] IceUsedFeedbackTimingSettings _defaultTiming = new();

    [Header("Used Sounds")]
    [SerializeField] AudioClip _iceUsedSound;
    [SerializeField] [Range(0f, 1f)] float _iceUsedSoundVolume = 1f;
    [SerializeField] AudioClip _timeUsedSound;
    [SerializeField] [Range(0f, 1f)] float _timeUsedSoundVolume = 1f;

    IceUsedFeedbackTimingSettings _activeIceTiming;
    IceUsedFeedbackTimingSettings _activeTimeTiming;
    Coroutine _iceRoutine;
    Coroutine _timeRoutine;
    Color _iceBaseColor = Color.white;
    Color _timeBaseColor = Color.white;
    AudioSource _audioSource;

    void Awake()
    {
        if (ExerciseRuntime.IsActive)
        {
            Destroy(gameObject);
            return;
        }

        ResolveReferences();
        HideImmediate(_iceUsed, _iceUsedImage, _defaultTiming);
        HideImmediate(_timeUsed, _timeUsedImage, _defaultTiming);
    }

    public void Play(IceUsedFeedbackTimingSettings timing = null)
    {
        if (_iceUsed == null)
        {
            return;
        }

        _activeIceTiming = timing ?? _defaultTiming ?? new IceUsedFeedbackTimingSettings();

        if (_iceRoutine != null)
        {
            StopCoroutine(_iceRoutine);
        }

        _iceRoutine = StartCoroutine(PlayIceRoutine());
    }

    public void PlayTime(IceUsedFeedbackTimingSettings timing = null)
    {
        if (_timeUsed == null)
        {
            return;
        }

        _activeTimeTiming = timing ?? _defaultTiming ?? new IceUsedFeedbackTimingSettings();

        if (_timeRoutine != null)
        {
            StopCoroutine(_timeRoutine);
        }

        _timeRoutine = StartCoroutine(PlayTimeRoutine());
    }

    IEnumerator PlayIceRoutine()
    {
        PlayUsedSound(_iceUsedSound, _iceUsedSoundVolume);
        yield return PlayRoutine(_iceUsed, _iceUsedImage, _activeIceTiming, _iceBaseColor);
        _iceRoutine = null;
    }

    IEnumerator PlayTimeRoutine()
    {
        PlayUsedSound(_timeUsedSound, _timeUsedSoundVolume);
        yield return PlayRoutine(_timeUsed, _timeUsedImage, _activeTimeTiming, _timeBaseColor);
        _timeRoutine = null;
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
            _iceBaseColor = _iceUsedImage.color;
        }

        if (_timeUsed == null)
        {
            Transform timeUsedTransform = transform.Find("TimeUsed");
            if (timeUsedTransform != null)
            {
                _timeUsed = timeUsedTransform as RectTransform;
            }
        }

        if (_timeUsedImage == null && _timeUsed != null)
        {
            _timeUsedImage = _timeUsed.GetComponent<Image>();
        }

        if (_timeUsedImage != null)
        {
            _timeBaseColor = _timeUsedImage.color;
        }
    }

    static void HideImmediate(
        RectTransform target,
        Image targetImage,
        IceUsedFeedbackTimingSettings timing)
    {
        if (target == null)
        {
            return;
        }

        IceUsedFeedbackTimingSettings safeTiming = timing ?? new IceUsedFeedbackTimingSettings();
        target.localScale = Vector3.one * safeTiming.BounceEndScale;
        SetAlpha(targetImage, 1f);
        target.gameObject.SetActive(false);
    }

    IEnumerator PlayRoutine(
        RectTransform target,
        Image targetImage,
        IceUsedFeedbackTimingSettings timing,
        Color baseColor)
    {
        target.gameObject.SetActive(true);
        target.localScale = Vector3.one * timing.BounceStartScale;
        SetAlpha(targetImage, baseColor, 1f);

        float elapsed = 0f;
        while (elapsed < timing.BounceInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = EaseOutBack(Mathf.Clamp01(elapsed / timing.BounceInDuration));
            float scale = Mathf.LerpUnclamped(timing.BounceStartScale, timing.BounceEndScale, t);
            target.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }

        target.localScale = Vector3.one * timing.BounceEndScale;

        elapsed = 0f;
        Vector3 startScale = Vector3.one * timing.BounceEndScale;
        Vector3 endScale = Vector3.one * timing.ExitScale;
        while (elapsed < timing.ExpandFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / timing.ExpandFadeDuration);
            target.localScale = Vector3.Lerp(startScale, endScale, t);
            SetAlpha(targetImage, baseColor, Mathf.Lerp(1f, 0f, t));
            yield return null;
        }

        HideImmediate(target, targetImage, timing);
    }

    void PlayUsedSound(AudioClip clip, float volume)
    {
        if (clip == null)
        {
            return;
        }

        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;
        }

        _audioSource.PlayOneShot(clip, volume);
    }

    static void SetAlpha(Image targetImage, float alpha)
    {
        if (targetImage == null)
        {
            return;
        }

        Color color = targetImage.color;
        color.a = alpha;
        targetImage.color = color;
    }

    static void SetAlpha(Image targetImage, Color baseColor, float alpha)
    {
        if (targetImage == null)
        {
            return;
        }

        Color color = baseColor;
        color.a = alpha;
        targetImage.color = color;
    }

    static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}

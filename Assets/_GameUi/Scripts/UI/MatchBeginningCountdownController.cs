using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class MatchBeginningCountdownController : MonoBehaviour
{
    public static bool IsActive { get; private set; }
    public static event Action Finished;

    [SerializeField] GameObject _beginningRoot;
    [SerializeField] Image _counterImage;
    [SerializeField] Sprite _sprite3;
    [SerializeField] Sprite _sprite2;
    [SerializeField] Sprite _sprite1;
    [SerializeField] Sprite _spriteWhistle;
    [SerializeField] GameFeedbackAudioLibrary _audioLibrary;

    [SerializeField] float _stepDuration = 1f;
    [SerializeField] float _bounceDuration = 0.45f;
    [SerializeField] float _startScale = 0.75f;
    [SerializeField] float _endScale = 1f;

    AudioSource _audioSource;
    Coroutine _routine;
    Vector3 _counterBaseScale = Vector3.one;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState()
    {
        IsActive = false;
        Finished = null;
    }

    void Awake()
    {
        IsActive = true;
        ResolveReferences();

        if (_counterImage != null)
        {
            _counterBaseScale = _counterImage.rectTransform.localScale;
        }

        if (_beginningRoot != null)
        {
            _beginningRoot.SetActive(true);
        }
    }

    void Start()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }

        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0f;

        _routine = StartCoroutine(CountdownRoutine());
    }

    void OnDestroy()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
        }

        if (IsActive)
        {
            IsActive = false;
            Finished?.Invoke();
        }
    }

    void ResolveReferences()
    {
        if (_beginningRoot == null)
        {
            _beginningRoot = gameObject;
        }

        if (_counterImage == null && _beginningRoot != null)
        {
            Transform sayac = _beginningRoot.transform.Find("Sayac");
            if (sayac != null)
            {
                _counterImage = sayac.GetComponent<Image>();
            }
        }

        ResolveAudioLibrary();
    }

    void ResolveAudioLibrary()
    {
        if (_audioLibrary != null)
        {
            return;
        }

        GameFeedbackAudioLibrary[] libraries = Resources.FindObjectsOfTypeAll<GameFeedbackAudioLibrary>();
        if (libraries.Length > 0)
        {
            _audioLibrary = libraries[0];
        }
    }

    IEnumerator CountdownRoutine()
    {
        Sprite[] sprites = { _sprite3, _sprite2, _sprite1 };

        for (int i = 0; i < sprites.Length; i++)
        {
            if (_counterImage != null && sprites[i] != null)
            {
                _counterImage.sprite = sprites[i];
            }

            PlayCountSound(3 - i);

            yield return AnimateBounce();

            float remaining = _stepDuration - _bounceDuration;
            if (remaining > 0f)
            {
                yield return new WaitForSecondsRealtime(remaining);
            }
        }

        if (_counterImage != null && _spriteWhistle != null)
        {
            _counterImage.sprite = _spriteWhistle;
        }

        PlayWhistle();

        yield return AnimateBounce();

        float whistleRemaining = _stepDuration - _bounceDuration;
        if (whistleRemaining > 0f)
        {
            yield return new WaitForSecondsRealtime(whistleRemaining);
        }

        HideBeginning();

        IsActive = false;
        Finished?.Invoke();
        _routine = null;
    }

    IEnumerator AnimateBounce()
    {
        if (_counterImage == null)
        {
            yield return new WaitForSecondsRealtime(_bounceDuration);
            yield break;
        }

        RectTransform counterTransform = _counterImage.rectTransform;
        float duration = Mathf.Max(0.01f, _bounceDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float eased = EaseOutBack(normalized);
            float scale = Mathf.Lerp(_startScale, _endScale, eased);
            counterTransform.localScale = _counterBaseScale * scale;
            yield return null;
        }

        counterTransform.localScale = _counterBaseScale * _endScale;
    }

    void PlayCountSound(int count)
    {
        GameFeedbackSettingsService.EnsureLoaded();
        if (!GameFeedbackSettingsService.SoundEffectsEnabled)
        {
            return;
        }

        ResolveAudioLibrary();
        if (_audioLibrary == null || _audioSource == null)
        {
            return;
        }

        AudioClip clip = count switch
        {
            3 => _audioLibrary.Count3,
            2 => _audioLibrary.Count2,
            1 => _audioLibrary.Count1,
            _ => null
        };

        if (clip != null)
        {
            _audioSource.PlayOneShot(clip, 1f);
        }
    }

    void PlayWhistle()
    {
        if (GameFeedback.Instance != null)
        {
            GameFeedback.Instance.PlayWhistle();
            return;
        }

        ResolveAudioLibrary();
        if (_audioLibrary == null || _audioLibrary.whistle == null || _audioSource == null
            || !GameFeedbackSettingsService.SoundEffectsEnabled)
        {
            return;
        }

        _audioSource.PlayOneShot(_audioLibrary.whistle, 1f);
    }

    void HideBeginning()
    {
        if (_beginningRoot != null)
        {
            _beginningRoot.SetActive(false);
        }
    }

    static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}

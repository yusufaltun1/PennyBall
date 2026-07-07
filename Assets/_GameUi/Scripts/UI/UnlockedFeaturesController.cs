using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class UnlockedFeaturesController : MonoBehaviour
{
    [SerializeField] RectTransform _particleBurst;
    [SerializeField] RectTransform _iconName;

    [Header("IconName Intro")]
    [SerializeField] float _iconNameIntroDelay = 0.15f;
    [SerializeField] float _iconNameMoveDuration = 0.15f;
    [SerializeField] float _iconNameStartOffsetY = -25f;
    [SerializeField] float _iconNameStartScale = 0.1f;

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

    Coroutine _introRoutine;
    Vector2 _iconNameRestPosition;
    Vector3 _iconNameRestScale;
    readonly UIParticleBurstPlayer _burstPlayer = new();

    void Awake()
    {
        ResolveReferences();
        CacheIconNameRestState();
    }

    void OnEnable()
    {
        if (_introRoutine != null)
        {
            StopCoroutine(_introRoutine);
        }

        CacheIconNameRestState();
        PrepareIconNameForIntro();
        _introRoutine = StartCoroutine(PlayIntroRoutine());
    }

    void OnDisable()
    {
        if (_introRoutine != null)
        {
            StopCoroutine(_introRoutine);
            _introRoutine = null;
        }

        _burstPlayer.Clear();
        ResetIconNameTransform();
    }

    void ResolveReferences()
    {
        if (_particleBurst == null)
        {
            _particleBurst = transform.Find("ParticleBurst") as RectTransform;
        }

        if (_iconName == null)
        {
            Transform ice = transform.Find("ICE");
            if (ice != null)
            {
                _iconName = ice.Find("IconName") as RectTransform;
            }
        }
    }

    void CacheIconNameRestState()
    {
        if (_iconName == null)
        {
            return;
        }

        _iconNameRestPosition = _iconName.anchoredPosition;
        _iconNameRestScale = _iconName.localScale;
    }

    void PrepareIconNameForIntro()
    {
        if (_iconName == null)
        {
            return;
        }

        _iconName.anchoredPosition = _iconNameRestPosition + new Vector2(0f, _iconNameStartOffsetY);
        _iconName.localScale = _iconNameRestScale * _iconNameStartScale;
    }

    void ResetIconNameTransform()
    {
        if (_iconName == null)
        {
            return;
        }

        _iconName.anchoredPosition = _iconNameRestPosition;
        _iconName.localScale = _iconNameRestScale;
    }

    IEnumerator PlayIntroRoutine()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();

        Coroutine burstRoutine = null;
        Coroutine iconRoutine = null;

        if (_particleBurst != null)
        {
            LevelUpSound.Play();
            EnsureBurstMask();
            _burstPlayer.Prepare(_particleBurst, _burstSettings);
            burstRoutine = StartCoroutine(_burstPlayer.PlayRoutine());
        }

        if (_iconName != null)
        {
            iconRoutine = StartCoroutine(PlayIconNameIntroRoutine());
        }

        if (burstRoutine != null)
        {
            yield return burstRoutine;
        }

        if (iconRoutine != null)
        {
            yield return iconRoutine;
        }

        _introRoutine = null;
    }

    IEnumerator PlayIconNameIntroRoutine()
    {
        if (_iconNameIntroDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(_iconNameIntroDelay);
        }

        Vector2 startPosition = _iconNameRestPosition + new Vector2(0f, _iconNameStartOffsetY);
        Vector2 endPosition = _iconNameRestPosition;
        Vector3 startScale = _iconNameRestScale * _iconNameStartScale;
        Vector3 endScale = _iconNameRestScale;
        float duration = Mathf.Max(0.01f, _iconNameMoveDuration);
        float elapsed = 0f;

        _iconName.anchoredPosition = startPosition;
        _iconName.localScale = startScale;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EaseInOut(t);
            _iconName.anchoredPosition = Vector2.LerpUnclamped(startPosition, endPosition, eased);
            _iconName.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);
            yield return null;
        }

        _iconName.anchoredPosition = endPosition;
        _iconName.localScale = endScale;
    }

    void EnsureBurstMask()
    {
        if (GetComponent<RectMask2D>() == null)
        {
            gameObject.AddComponent<RectMask2D>();
        }
    }

    static float EaseInOut(float t)
    {
        return t * t * (3f - 2f * t);
    }
}

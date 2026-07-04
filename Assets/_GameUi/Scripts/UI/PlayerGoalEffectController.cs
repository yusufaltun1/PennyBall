using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PlayerGoalEffectController : MonoBehaviour
{
    public static PlayerGoalEffectController Instance { get; private set; }

    static readonly string[] DefaultLetterOrder =
    {
        "Letter_G",
        "Letter_O1",
        "Letter_O2",
        "Letter_A1",
        "Letter_A2",
        "Letter_L",
        "Letter_Unlem",
    };

    [Header("References")]
    [SerializeField] RectTransform _lettersRoot;
    [SerializeField] RectTransform[] _waveLetters;
    [SerializeField] RectTransform _particleBurstRoot;

    [Header("Letter Wave")]
    [SerializeField] float _letterWaveTotalDuration = 2f;
    [SerializeField, Range(0.1f, 0.95f)] float _nextLetterStartAtPeakProgress = 0.66f;
    [SerializeField] float _letterPeakScale = 1.44f;
    [SerializeField] int _lettersGroupPulseCount = 2;

    [Header("Burst Particles")]
    [SerializeField] UIParticleBurstSettings _burstSettings = new();

    readonly UIParticleBurstPlayer _burstPlayer = new();
    Coroutine _playRoutine;
    MonoBehaviour _routineHost;
    Vector3[] _letterBaseScales = Array.Empty<Vector3>();
    Vector3 _lettersRootBaseScale = Vector3.one;

    public static PlayerGoalEffectController EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        return FindFirstObjectByType<PlayerGoalEffectController>(FindObjectsInactive.Include);
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        ResolveWaveLetters();
        CacheLetterBaseScales();
        CacheLettersRootBaseScale();
    }

    void OnDestroy()
    {
        StopActiveRoutine();

        if (Instance == this)
        {
            Instance = null;
        }
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
        _burstPlayer.Clear();
    }

    void ResolveWaveLetters()
    {
        if (_waveLetters != null && _waveLetters.Length > 0)
        {
            return;
        }

        if (_lettersRoot == null)
        {
            Transform letters = transform.Find("Letters");
            if (letters != null)
            {
                _lettersRoot = letters as RectTransform;
            }
        }

        if (_lettersRoot == null)
        {
            return;
        }

        var resolved = new List<RectTransform>(DefaultLetterOrder.Length);
        for (int i = 0; i < DefaultLetterOrder.Length; i++)
        {
            Transform letter = _lettersRoot.Find(DefaultLetterOrder[i]);
            if (letter is RectTransform rect)
            {
                resolved.Add(rect);
            }
        }

        if (resolved.Count > 0)
        {
            _waveLetters = resolved.ToArray();
        }
    }

    void CacheLetterBaseScales()
    {
        if (_waveLetters == null || _waveLetters.Length == 0)
        {
            _letterBaseScales = Array.Empty<Vector3>();
            return;
        }

        _letterBaseScales = new Vector3[_waveLetters.Length];
        for (int i = 0; i < _waveLetters.Length; i++)
        {
            RectTransform letter = _waveLetters[i];
            _letterBaseScales[i] = letter != null && letter.localScale.sqrMagnitude > 0.0001f
                ? letter.localScale
                : Vector3.one;
        }
    }

    void CacheLettersRootBaseScale()
    {
        if (_lettersRoot == null)
        {
            Transform letters = transform.Find("Letters");
            if (letters is RectTransform rect)
            {
                _lettersRoot = rect;
            }
        }

        if (_lettersRoot != null && _lettersRoot.localScale.sqrMagnitude > 0.0001f)
        {
            _lettersRootBaseScale = _lettersRoot.localScale;
        }
        else
        {
            _lettersRootBaseScale = Vector3.one;
        }
    }

    void ResetLettersRootScale()
    {
        if (_lettersRoot != null)
        {
            _lettersRoot.localScale = _lettersRootBaseScale;
        }
    }

    public bool CanPlay()
    {
        ResolveWaveLetters();
        return _waveLetters != null && _waveLetters.Length > 0;
    }

    public void Play(Action onComplete)
    {
        ResolveWaveLetters();
        if (_waveLetters == null || _waveLetters.Length == 0)
        {
            onComplete?.Invoke();
            return;
        }

        StopActiveRoutine();
        CacheLetterBaseScales();
        CacheLettersRootBaseScale();
        _burstPlayer.Clear();
        ResetLetterScales();
        ResetLettersRootScale();

        if (_particleBurstRoot == null)
        {
            _particleBurstRoot = transform as RectTransform;
        }

        EnsureParticleArea();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

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
        UIParticleBurstSettings burstSettings = GetBurstSettings();
        _burstPlayer.Prepare(_particleBurstRoot, burstSettings);
        _burstPlayer.Spawn();

        float waveDuration = Mathf.Max(0.01f, _letterWaveTotalDuration);
        float elapsed = 0f;
        while (elapsed < waveDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            ApplyLetterWave(elapsed);
            _burstPlayer.UpdateExpand(Mathf.Clamp01(elapsed / waveDuration));
            yield return null;
        }

        ApplyLetterWave(waveDuration);
        ResetLetterScales();
        _burstPlayer.UpdateExpand(1f);

        yield return LettersGroupPulseRoutine();
        yield return _burstPlayer.FallAndClear();

        gameObject.SetActive(false);
        _playRoutine = null;
        _routineHost = null;
        onComplete?.Invoke();
    }

    UIParticleBurstSettings GetBurstSettings()
    {
        if (_burstSettings == null)
        {
            _burstSettings = new UIParticleBurstSettings();
        }

        _burstSettings.expandDuration = _letterWaveTotalDuration;
        return _burstSettings;
    }

    float ComputeLetterCycleDuration(int letterCount)
    {
        if (letterCount <= 1)
        {
            return _letterWaveTotalDuration;
        }

        float overlap = Mathf.Clamp(_nextLetterStartAtPeakProgress, 0.1f, 0.95f);
        float denominator = 1f + (letterCount - 1) * overlap * 0.5f;
        return _letterWaveTotalDuration / denominator;
    }

    float ComputeLetterStagger(float letterCycleDuration)
    {
        return letterCycleDuration * 0.5f * Mathf.Clamp(_nextLetterStartAtPeakProgress, 0.1f, 0.95f);
    }

    void ApplyLetterWave(float elapsed)
    {
        int count = _waveLetters.Length;
        float cycle = ComputeLetterCycleDuration(count);
        float stagger = ComputeLetterStagger(cycle);

        for (int i = 0; i < count; i++)
        {
            RectTransform letter = _waveLetters[i];
            if (letter == null)
            {
                continue;
            }

            float localTime = elapsed - i * stagger;
            float scaleMultiplier = EvaluateLetterScale(localTime, cycle, _letterPeakScale);
            letter.localScale = _letterBaseScales[i] * scaleMultiplier;
        }
    }

    static float EvaluateLetterScale(float localTime, float cycle, float peakScale)
    {
        if (localTime <= 0f || localTime >= cycle)
        {
            return 1f;
        }

        float normalized = localTime / cycle;
        if (normalized < 0.5f)
        {
            float upProgress = normalized / 0.5f;
            return Mathf.Lerp(1f, peakScale, SmoothStep(upProgress));
        }

        float downProgress = (normalized - 0.5f) / 0.5f;
        return Mathf.Lerp(peakScale, 1f, SmoothStep(downProgress));
    }

    static float EvaluateInvertedLetterScale(float localTime, float cycle, float peakScale)
    {
        if (localTime <= 0f || localTime >= cycle)
        {
            return 1f;
        }

        float minScale = 1f / peakScale;
        float normalized = localTime / cycle;
        if (normalized < 0.5f)
        {
            float shrinkProgress = normalized / 0.5f;
            return Mathf.Lerp(1f, minScale, SmoothStep(shrinkProgress));
        }

        float growProgress = (normalized - 0.5f) / 0.5f;
        return Mathf.Lerp(minScale, 1f, SmoothStep(growProgress));
    }

    void ResetLetterScales()
    {
        if (_waveLetters == null)
        {
            return;
        }

        for (int i = 0; i < _waveLetters.Length; i++)
        {
            RectTransform letter = _waveLetters[i];
            if (letter == null)
            {
                continue;
            }

            Vector3 baseScale = i < _letterBaseScales.Length ? _letterBaseScales[i] : Vector3.one;
            letter.localScale = baseScale;
        }
    }

    IEnumerator LettersGroupPulseRoutine()
    {
        if (_lettersRoot == null || _lettersGroupPulseCount <= 0)
        {
            yield break;
        }

        float pulseDuration = ComputeLetterCycleDuration(_waveLetters.Length);

        for (int pulse = 0; pulse < _lettersGroupPulseCount; pulse++)
        {
            float elapsed = 0f;
            while (elapsed < pulseDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float scaleMultiplier = EvaluateInvertedLetterScale(elapsed, pulseDuration, _letterPeakScale);
                _lettersRoot.localScale = _lettersRootBaseScale * scaleMultiplier;
                yield return null;
            }
        }

        ResetLettersRootScale();
    }

    void EnsureParticleArea()
    {
        RectTransform container = transform as RectTransform;
        if (container == null)
        {
            return;
        }

        if (_particleBurstRoot == null)
        {
            _particleBurstRoot = container;
        }

        if (GetComponent<RectMask2D>() == null)
        {
            gameObject.AddComponent<RectMask2D>();
        }

        _burstPlayer.Prepare(_particleBurstRoot, GetBurstSettings());
    }

    static float SmoothStep(float t)
    {
        return t * t * (3f - 2f * t);
    }
}

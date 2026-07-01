using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class GameFeedback : MonoBehaviour
{
    public static GameFeedback Instance { get; private set; }

    [SerializeField] GameFeedbackAudioLibrary _audioLibrary;
    [SerializeField] [Range(0f, 1f)] float _masterVolume = 0.85f;
    [SerializeField] [Range(0f, 1f)] float _kickVolume = 0.9f;
    [SerializeField] [Range(0f, 1f)] float _wallHitVolume = 0.85f;
    [SerializeField] [Range(0f, 1f)] float _coinHitVolume = 0.85f;
    [SerializeField] [Range(0f, 1f)] float _goalVolume = 0.5f;
    [SerializeField] [Range(0f, 1f)] float _musicVolume = 0.3f;
    [SerializeField] float _coinHitMinSpeed = 0.35f;

    [Header("Coin Hit Dust")]
    [SerializeField] CoinHitDustSettings _coinHitDust = new();

    AudioSource _sfxSource;
    AudioSource _goalSource;
    AudioSource _musicSource;

    AudioClip _kickClip;
    AudioClip _wallClip;
    AudioClip _coinClip;
    AudioClip _goalClip;
    AudioClip _musicClip;

    float _lastShotSfx;
    float _lastWallSfx;
    float _lastCoinSfx;
    float _lastShotVibe;
    float _lastHitVibe;

    Transform _dustAnchor;
    ParticleSystem _dustBurst;
    Material _dustMaterial;

    readonly Dictionary<long, float> _coinPairCooldown = new();

    public static GameFeedback EnsureInstance()
    {
        if (Instance != null)
        {
            Instance.StartBackgroundMusic();
            return Instance;
        }

        GameFeedback existing = FindFirstObjectByType<GameFeedback>();
        if (existing != null)
        {
            existing.StartBackgroundMusic();
            return existing;
        }

        return null;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SeedDefaultSettingsIfNeeded();
        EnsureAudioListener();
        SetupAudio();
        SetupDust();
        ApplySettings();
    }

    void OnDestroy()
    {
        UnsubscribeEvents();

        if (_dustMaterial != null && _coinHitDust.CustomMaterial == null)
        {
            Destroy(_dustMaterial);
            _dustMaterial = null;
        }

        if (_dustAnchor != null)
        {
            Destroy(_dustAnchor.gameObject);
            _dustAnchor = null;
            _dustBurst = null;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    void OnValidate()
    {
        if (_dustBurst != null)
        {
            ApplyDustSettings();
        }
    }

    void OnEnable()
    {
        GameFeedbackSettingsService.Changed += ApplySettings;
        SubscribeEvents();
        ApplySettings();
    }

    void OnDisable()
    {
        GameFeedbackSettingsService.Changed -= ApplySettings;
        UnsubscribeEvents();
    }

    void Start()
    {
        SubscribeEvents();
        ApplySettings();
        StartBackgroundMusic();
        PlayMatchStartBell();
    }

    void SeedDefaultSettingsIfNeeded()
    {
        GameFeedbackSettingsService.EnsureLoaded();
    }

    public void ApplySettings()
    {
        bool soundEnabled = GameFeedbackSettingsService.SoundEffectsEnabled;
        bool musicEnabled = GameFeedbackSettingsService.MusicEnabled;

        if (_sfxSource != null)
        {
            _sfxSource.mute = !soundEnabled;
        }

        if (_goalSource != null)
        {
            _goalSource.mute = !soundEnabled;
        }

        if (_musicSource != null)
        {
            if (!musicEnabled)
            {
                _musicSource.Stop();
            }
            else
            {
                StartBackgroundMusic();
            }
        }
    }

    void SubscribeEvents()
    {
        if (GameRulesManager.Instance != null)
        {
            GameRulesManager.Instance.RoundReset -= OnRoundReset;
            GameRulesManager.Instance.RoundReset += OnRoundReset;
        }
    }

    void UnsubscribeEvents()
    {
        if (GameRulesManager.Instance != null)
        {
            GameRulesManager.Instance.RoundReset -= OnRoundReset;
        }
    }

    void OnRoundReset()
    {
        PlayMatchStartBell();
    }

    public void RefreshEventSubscriptions()
    {
        SubscribeEvents();
    }

    public void PlayShot(float power01)
    {
        power01 = Mathf.Clamp01(power01);
        if (power01 < 0.04f || _kickClip == null || !GameFeedbackSettingsService.SoundEffectsEnabled)
        {
            return;
        }

        float now = Time.unscaledTime;
        if (now - _lastShotSfx > 0.04f)
        {
            _lastShotSfx = now;
            PlayOneShot(_kickClip, _kickVolume * (0.75f + power01 * 0.25f), 0.92f + power01 * 0.16f);
        }

        Vibrate(now, 0.12f, ref _lastShotVibe);
    }

    public void PlayWallHit(float intensity01, Vector3 position)
    {
        if (_wallClip == null || !GameFeedbackSettingsService.SoundEffectsEnabled)
        {
            return;
        }

        intensity01 = Mathf.Clamp01(intensity01);
        if (intensity01 < 0.02f)
        {
            return;
        }

        float now = Time.unscaledTime;
        if (now - _lastWallSfx > 0.05f)
        {
            _lastWallSfx = now;
            PlayOneShot(_wallClip, _wallHitVolume * (0.7f + intensity01 * 0.3f), 0.94f + intensity01 * 0.12f);
        }

        Vibrate(now, 0.06f, ref _lastHitVibe);
    }

    public bool TryPlayCoinHit(int selfId, int otherId, float impactSpeed, Vector3 position)
    {
        if (impactSpeed < _coinHitMinSpeed || _coinClip == null || !GameFeedbackSettingsService.SoundEffectsEnabled)
        {
            return false;
        }

        float now = Time.unscaledTime;
        long pairKey = PairKey(selfId, otherId);
        if (_coinPairCooldown.TryGetValue(pairKey, out float until) && now < until)
        {
            return false;
        }

        _coinPairCooldown[pairKey] = now + 0.08f;

        float intensity = Mathf.Clamp01(impactSpeed / 4.45f);
        if (now - _lastCoinSfx > 0.04f)
        {
            _lastCoinSfx = now;
            PlayOneShot(_coinClip, _coinHitVolume * (0.65f + intensity * 0.35f), 0.92f + intensity * 0.18f);
        }

        PlayDust(position, intensity);
        Vibrate(now, 0.06f, ref _lastHitVibe);
        return true;
    }

    public void PlayGoal()
    {
        if (_goalSource == null || _goalClip == null || !GameFeedbackSettingsService.SoundEffectsEnabled)
        {
            return;
        }

        if (_goalSource.isPlaying)
        {
            _goalSource.Stop();
        }

        _goalSource.clip = _goalClip;
        _goalSource.volume = _masterVolume * _goalVolume;
        _goalSource.pitch = 1f;
        _goalSource.Play();
        Vibrate(Time.unscaledTime, 0.12f, ref _lastShotVibe);
    }

    public void PlayMatchStartBell()
    {
        if (_wallClip == null || !GameFeedbackSettingsService.SoundEffectsEnabled)
        {
            return;
        }

        PlayOneShot(_wallClip, _wallHitVolume * 0.95f, 1.05f);
    }

    public void StartBackgroundMusic()
    {
        if (_musicSource == null || _musicClip == null || !GameFeedbackSettingsService.MusicEnabled)
        {
            return;
        }

        if (_musicSource.isPlaying)
        {
            return;
        }

        _musicSource.clip = _musicClip;
        _musicSource.volume = _masterVolume * _musicVolume;
        _musicSource.Play();
    }

    void SetupAudio()
    {
        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.playOnAwake = false;
        _sfxSource.spatialBlend = 0f;
        _sfxSource.loop = false;
        _sfxSource.volume = _masterVolume;

        _goalSource = gameObject.AddComponent<AudioSource>();
        _goalSource.playOnAwake = false;
        _goalSource.spatialBlend = 0f;
        _goalSource.loop = false;

        _musicSource = gameObject.AddComponent<AudioSource>();
        _musicSource.playOnAwake = false;
        _musicSource.spatialBlend = 0f;
        _musicSource.loop = true;

        if (_audioLibrary != null)
        {
            _kickClip = _audioLibrary.kickClip;
            _wallClip = _audioLibrary.wallHitClip;
            _coinClip = _audioLibrary.coinHitClip;
            _goalClip = _audioLibrary.goalCelebrationClip;
            _musicClip = _audioLibrary.backgroundMusicClip;
        }

        if (!HasAllClips())
        {
            GameFeedbackAudioLibrary[] libraries = Resources.FindObjectsOfTypeAll<GameFeedbackAudioLibrary>();
            if (libraries.Length > 0)
            {
                _audioLibrary = libraries[0];
                _kickClip = _audioLibrary.kickClip;
                _wallClip = _audioLibrary.wallHitClip;
                _coinClip = _audioLibrary.coinHitClip;
                _goalClip = _audioLibrary.goalCelebrationClip;
                _musicClip = _audioLibrary.backgroundMusicClip;
            }
        }

        if (!HasAllClips())
        {
            Debug.LogWarning("[GameFeedback] Ses klipleri yüklenemedi. GameFeedbackAudioLibrary atamasını kontrol et.");
        }
    }

    bool HasAllClips()
    {
        return _kickClip != null
               && _wallClip != null
               && _coinClip != null
               && _goalClip != null
               && _musicClip != null;
    }

    static void EnsureAudioListener()
    {
        if (FindFirstObjectByType<AudioListener>() != null)
        {
            return;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            camera = FindFirstObjectByType<Camera>();
        }

        if (camera != null && camera.GetComponent<AudioListener>() == null)
        {
            camera.gameObject.AddComponent<AudioListener>();
        }
    }

    void PlayOneShot(AudioClip clip, float volumeScale, float pitch)
    {
        if (clip == null || _sfxSource == null)
        {
            return;
        }

        _sfxSource.pitch = pitch;
        _sfxSource.volume = _masterVolume * volumeScale;
        _sfxSource.PlayOneShot(clip);
    }

    void Vibrate(float now, float cooldown, ref float lastTime)
    {
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
        if (!GameFeedbackSettingsService.VibrationEnabled || now - lastTime < cooldown)
        {
            return;
        }

        lastTime = now;
        Handheld.Vibrate();
#endif
    }

    static long PairKey(int a, int b)
    {
        if (a > b)
        {
            (a, b) = (b, a);
        }

        return ((long)a << 32) | (uint)b;
    }

    void SetupDust()
    {
        if (!_coinHitDust.Enabled)
        {
            return;
        }

        var rig = new GameObject("CoinDustFx");
        rig.transform.SetParent(null);
        _dustAnchor = rig.transform;

        _dustBurst = rig.AddComponent<ParticleSystem>();
        _dustBurst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ApplyDustSettings();
    }

    void ApplyDustSettings()
    {
        if (_dustBurst == null)
        {
            return;
        }

        ParticleSystem.MainModule main = _dustBurst.main;
        main.duration = _coinHitDust.Duration;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(
            _coinHitDust.LifetimeRange.x,
            _coinHitDust.LifetimeRange.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(
            _coinHitDust.SpeedRange.x,
            _coinHitDust.SpeedRange.y);
        main.startSize = new ParticleSystem.MinMaxCurve(
            _coinHitDust.SizeRange.x,
            _coinHitDust.SizeRange.y);
        main.startRotation = new ParticleSystem.MinMaxCurve(
            _coinHitDust.RotationRange.x,
            _coinHitDust.RotationRange.y);
        main.gravityModifier = _coinHitDust.GravityModifier;
        main.maxParticles = _coinHitDust.MaxParticles;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.playOnAwake = false;
        main.startColor = new ParticleSystem.MinMaxGradient(_coinHitDust.StartColor);

        ParticleSystem.EmissionModule emission = _dustBurst.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = _dustBurst.shape;
        shape.enabled = true;
        shape.position = Vector3.zero;
        shape.shapeType = _coinHitDust.ShapeType;
        shape.rotation = _coinHitDust.ShapeRotation;
        ApplyShapeDimensions(shape);

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = _dustBurst.sizeOverLifetime;
        sizeOverLifetime.enabled = _coinHitDust.SizeOverLifetime != null;
        if (sizeOverLifetime.enabled)
        {
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, _coinHitDust.SizeOverLifetime);
        }

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = _dustBurst.colorOverLifetime;
        colorOverLifetime.enabled = _coinHitDust.ColorOverLifetime != null;
        if (colorOverLifetime.enabled)
        {
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(_coinHitDust.ColorOverLifetime);
        }

        ParticleSystemRenderer renderer = _dustBurst.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = _coinHitDust.RenderMode;
        renderer.material = ResolveDustMaterial();
    }

    void ApplyShapeDimensions(ParticleSystem.ShapeModule shape)
    {
        CoinHitDustSettings settings = _coinHitDust;
        shape.position = Vector3.zero;

        switch (settings.ShapeType)
        {
            case ParticleSystemShapeType.Circle:
            case ParticleSystemShapeType.CircleEdge:
            case ParticleSystemShapeType.SingleSidedEdge:
                shape.radius = settings.ShapeRadius;
                shape.scale = Vector3.one;
                break;

            case ParticleSystemShapeType.Sphere:
            case ParticleSystemShapeType.Hemisphere:
                shape.radius = settings.ShapeRadius;
                shape.scale = Vector3.one;
                break;

            case ParticleSystemShapeType.Box:
            case ParticleSystemShapeType.BoxShell:
            case ParticleSystemShapeType.BoxEdge:
            case ParticleSystemShapeType.Rectangle:
                shape.scale = settings.ShapeScale;
                break;

            default:
                shape.radius = settings.ShapeRadius;
                shape.scale = settings.ShapeScale;
                break;
        }
    }

    Material ResolveDustMaterial()
    {
        if (_coinHitDust.CustomMaterial != null)
        {
            return _coinHitDust.CustomMaterial;
        }

        if (_dustMaterial != null)
        {
            Destroy(_dustMaterial);
        }

        _dustMaterial = CreateParticleMaterial(_coinHitDust.AdditiveBlend);
        return _dustMaterial;
    }

    void PlayDust(Vector3 position, float intensity)
    {
        if (!_coinHitDust.Enabled || _dustBurst == null || _dustAnchor == null)
        {
            return;
        }

        Vector3 emitPosition = position + Vector3.up * _coinHitDust.HeightOffset;
        _dustAnchor.SetPositionAndRotation(emitPosition, Quaternion.identity);

        int burstCount = Mathf.RoundToInt(Mathf.Lerp(
            _coinHitDust.BurstCountMin,
            _coinHitDust.BurstCountMax,
            Mathf.Clamp01(intensity)));

        ParticleSystem.EmissionModule emission = _dustBurst.emission;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)Mathf.Max(1, burstCount))
        });

        _dustBurst.Clear(true);
        _dustBurst.Play(true);
    }

    static Material CreateParticleMaterial(bool additive)
    {
        string[] shaderNames =
        {
            "Universal Render Pipeline/Particles/Unlit",
            "Particles/Standard Unlit",
            "Sprites/Default"
        };

        foreach (string shaderName in shaderNames)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                continue;
            }

            Material material = new Material(shader);
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.white);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", Color.white);
            }

            if (additive)
            {
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)BlendMode.One);
            }

            return material;
        }

        return new Material(Shader.Find("Sprites/Default"));
    }
}

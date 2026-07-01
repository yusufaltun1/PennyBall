using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Gol anında particle patlaması + kısa ışık flaşı.
/// GoalZone konumlarında otomatik oynatılır (Inspector'dan da atanabilir).
/// </summary>
[DisallowMultipleComponent]
public class GoalCelebration : MonoBehaviour
{
    public static GoalCelebration Instance { get; private set; }

    [Header("Konum")]
    [Tooltip("Boşsa GoalZone'lardan otomatik bulunur")]
    [SerializeField] Transform _playerScoresAtGoal;
    [SerializeField] Transform _enemyScoresAtGoal;

    [Header("Renkler — Oyuncu Golü")]
    [SerializeField] Color _playerPrimary = new(1f, 0.85f, 0.1f);
    [SerializeField] Color _playerSecondary = new(0.2f, 0.95f, 0.45f);
    [SerializeField] Color _playerAccent = new(1f, 0.45f, 0.1f);

    [Header("Renkler — Rakip Golü")]
    [SerializeField] Color _enemyPrimary = new(1f, 0.25f, 0.2f);
    [SerializeField] Color _enemySecondary = new(0.55f, 0.1f, 0.15f);
    [SerializeField] Color _enemyAccent = new(1f, 0.55f, 0.15f);

    [Header("Işık")]
    [SerializeField] float _flashPeakIntensity = 6f;
    [SerializeField] float _flashDuration = 0.55f;

    [Header("Gol FX — Genel")]
    [Tooltip("Efektin kale üstündeki yüksekliği")]
    [SerializeField] float _fxHeightOffset = 0.35f;
    [Tooltip("Tüm parçacık boyutlarını çarpar")]
    [SerializeField] [Range(0.25f, 3f)] float _fxGlobalSizeScale = 1f;
    [Tooltip("Hız ve animasyon oynatma hızını çarpar")]
    [SerializeField] [Range(0.25f, 3f)] float _fxGlobalSpeedScale = 1f;

    [Header("Gol FX — Confetti")]
    [SerializeField] GoalCelebrationBurstSettings _confettiFx = new();

    [Header("Gol FX — Sparks")]
    [SerializeField] GoalCelebrationBurstSettings _sparksFx = new();

    [Header("Gol FX — Ring Wave")]
    [SerializeField] GoalCelebrationRingSettings _ringFx = new();

    [Header("Gol FX — Star Fountain")]
    [SerializeField] GoalCelebrationBurstSettings _starFountainFx = new();

    [Header("Gol FX — Smoke")]
    [SerializeField] GoalCelebrationBurstSettings _smokeFx = new();

    [Header("Eski UI (isteğe bağlı — atanmışsa gizlenir)")]
    [SerializeField] GameObject _legacyTextRoot;

    Transform _fxAnchor;
    readonly List<ParticleSystem> _burstSystems = new(5);
    readonly List<bool> _burstEnabled = new(5);
    Light _flashLight;
    Coroutine _flashRoutine;

    void Reset()
    {
        ApplyPennyBall3dFxDefaults();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        ResolveGoalAnchors();
        BuildFxRig();
        HideLegacyText();
    }

    void Start()
    {
        ResolveGoalAnchors();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying && _fxAnchor != null && _burstSystems.Count > 0)
        {
            ReapplyFxSettings();
        }
    }
#endif

    void OnDestroy()
    {
        if (_fxAnchor != null)
        {
            Destroy(_fxAnchor.gameObject);
            _fxAnchor = null;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    [ContextMenu("Reset FX To PennyBall3d Defaults")]
    void ResetFxToDefaults()
    {
        ApplyPennyBall3dFxDefaults();
        if (_fxAnchor != null)
        {
            ReapplyFxSettings();
        }
    }

    void ApplyPennyBall3dFxDefaults()
    {
        _fxHeightOffset = 0.35f;
        _fxGlobalSizeScale = 1f;
        _fxGlobalSpeedScale = 1f;
        _confettiFx.ApplyConfettiDefaults();
        _sparksFx.ApplySparksDefaults();
        _ringFx.ApplyDefaults();
        _starFountainFx.ApplyStarFountainDefaults();
        _smokeFx.ApplySmokeDefaults();
    }

    void HideLegacyText()
    {
        if (_legacyTextRoot != null)
        {
            _legacyTextRoot.SetActive(false);
        }
    }

    void ResolveGoalAnchors()
    {
        if (_playerScoresAtGoal != null && _enemyScoresAtGoal != null)
        {
            return;
        }

        GoalZone[] zones = FindObjectsByType<GoalZone>(FindObjectsSortMode.None);
        for (int i = 0; i < zones.Length; i++)
        {
            GoalZone zone = zones[i];
            if (zone.IsOpponentGoal && _playerScoresAtGoal == null)
            {
                _playerScoresAtGoal = zone.transform;
            }
            else if (!zone.IsOpponentGoal && _enemyScoresAtGoal == null)
            {
                _enemyScoresAtGoal = zone.transform;
            }
        }

        if (_playerScoresAtGoal == null)
        {
            GameObject kaleE = GameObject.Find("Kale_E");
            if (kaleE != null)
            {
                Transform trigger = kaleE.transform.Find("GoalTrigger");
                _playerScoresAtGoal = trigger != null ? trigger : kaleE.transform;
            }
        }

        if (_enemyScoresAtGoal == null)
        {
            GameObject kaleP = GameObject.Find("Kale_P");
            if (kaleP != null)
            {
                Transform trigger = kaleP.transform.Find("GoalTrigger");
                _enemyScoresAtGoal = trigger != null ? trigger : kaleP.transform;
            }
        }
    }

    void BuildFxRig()
    {
        if (_fxAnchor != null)
        {
            Destroy(_fxAnchor.gameObject);
        }

        _burstSystems.Clear();
        _burstEnabled.Clear();

        var rig = new GameObject("GoalFxRig");
        rig.transform.SetParent(null);
        _fxAnchor = rig.transform;

        _burstSystems.Add(CreateConfettiBurst(_fxAnchor, "Confetti", _confettiFx));
        _burstEnabled.Add(_confettiFx.Enabled);
        _burstSystems.Add(CreateSparkBurst(_fxAnchor, "Sparks", _sparksFx));
        _burstEnabled.Add(_sparksFx.Enabled);
        _burstSystems.Add(CreateRingWave(_fxAnchor, "RingWave", _ringFx));
        _burstEnabled.Add(_ringFx.Enabled);
        _burstSystems.Add(CreateStarFountain(_fxAnchor, "StarFountain", _starFountainFx));
        _burstEnabled.Add(_starFountainFx.Enabled);
        _burstSystems.Add(CreateSmokePuff(_fxAnchor, "SmokePuff", _smokeFx));
        _burstEnabled.Add(_smokeFx.Enabled);

        var lightGo = new GameObject("GoalFlashLight");
        lightGo.transform.SetParent(_fxAnchor, false);
        _flashLight = lightGo.AddComponent<Light>();
        _flashLight.type = LightType.Point;
        _flashLight.range = 14f;
        _flashLight.shadows = LightShadows.None;
        _flashLight.enabled = false;
    }

    void ReapplyFxSettings()
    {
        if (_burstSystems.Count < 5)
        {
            return;
        }

        ApplyBurstSettings(_burstSystems[0], _confettiFx, isConfetti: true);
        _burstEnabled[0] = _confettiFx.Enabled;
        ApplyBurstSettings(_burstSystems[1], _sparksFx, isConfetti: false);
        _burstEnabled[1] = _sparksFx.Enabled;
        ApplyRingSettings(_burstSystems[2], _ringFx);
        _burstEnabled[2] = _ringFx.Enabled;
        ApplyBurstSettings(_burstSystems[3], _starFountainFx, isConfetti: false, isStar: true);
        _burstEnabled[3] = _starFountainFx.Enabled;
        ApplyBurstSettings(_burstSystems[4], _smokeFx, isConfetti: false, isSmoke: true);
        _burstEnabled[4] = _smokeFx.Enabled;
    }

    float SizeScale => _fxGlobalSizeScale;
    float SpeedScale => _fxGlobalSpeedScale;

    void ApplyBurstSettings(
        ParticleSystem ps,
        GoalCelebrationBurstSettings settings,
        bool isConfetti = false,
        bool isStar = false,
        bool isSmoke = false)
    {
        if (ps == null || settings == null)
        {
            return;
        }

        ParticleSystem.MainModule main = ps.main;
        main.duration = settings.Duration;
        main.simulationSpeed = settings.SimulationSpeed * SpeedScale;
        main.startLifetime = ScaledRange(settings.LifetimeRange, 1f);
        main.startSpeed = ScaledRange(settings.SpeedRange, SpeedScale);
        main.startSize = ScaledRange(settings.SizeRange, SizeScale);
        main.gravityModifier = settings.GravityModifier;
        main.maxParticles = settings.MaxParticles;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(
                settings.BurstDelay,
                (short)settings.BurstMin,
                (short)settings.BurstMax,
                1,
                settings.BurstInterval)
        });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.radius = settings.ShapeRadius * SizeScale;
        if (shape.shapeType == ParticleSystemShapeType.Cone)
        {
            shape.angle = settings.ConeAngle;
        }

        SetupRenderer(ps, settings.Additive);

        if (isConfetti)
        {
            ConfigureConfettiModules(ps);
        }
        else if (isStar)
        {
            ConfigureStarModules(ps);
        }
        else if (isSmoke)
        {
            ConfigureSmokeModules(ps);
        }
    }

    void ApplyRingSettings(ParticleSystem ps, GoalCelebrationRingSettings settings)
    {
        if (ps == null || settings == null)
        {
            return;
        }

        ParticleSystem.MainModule main = ps.main;
        main.duration = settings.Duration;
        main.simulationSpeed = settings.SimulationSpeed * SpeedScale;
        main.startLifetime = settings.Lifetime;
        main.startSize = settings.StartSize * SizeScale;
        main.maxParticles = 1;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.radius = settings.ShapeRadius * SizeScale;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, settings.SizeOverLifetime);

        SetupRenderer(ps, additive: true);
        ConfigureRingColorOverLifetime(ps);
    }

    static ParticleSystem.MinMaxCurve ScaledRange(Vector2 range, float scale)
    {
        return new ParticleSystem.MinMaxCurve(range.x * scale, range.y * scale);
    }

    public void ShowGoal(bool playerScored)
    {
        if (_fxAnchor == null || _burstSystems.Count == 0)
        {
            BuildFxRig();
        }
        else
        {
            ReapplyFxSettings();
        }

        ResolveGoalAnchors();

        Transform anchor = playerScored ? _playerScoresAtGoal : _enemyScoresAtGoal;
        if (anchor == null)
        {
            Debug.LogWarning($"[GoalCelebration] Gol efekti için kale bulunamadı. playerScored={playerScored}");
            GameFeedback.EnsureInstance()?.PlayGoal();
            return;
        }

        Color primary = playerScored ? _playerPrimary : _enemyPrimary;
        Color secondary = playerScored ? _playerSecondary : _enemySecondary;
        Color accent = playerScored ? _playerAccent : _enemyAccent;

        Vector3 origin = anchor.position + Vector3.up * _fxHeightOffset;
        _fxAnchor.SetPositionAndRotation(origin, Quaternion.identity);

        for (int i = 0; i < _burstSystems.Count; i++)
        {
            if (i < _burstEnabled.Count && !_burstEnabled[i])
            {
                continue;
            }

            ParticleSystem ps = _burstSystems[i];
            if (ps == null)
            {
                continue;
            }

            ApplyPalette(ps, primary, secondary, accent);
            ps.Clear(true);
            ps.Play(true);
        }

        if (_flashLight != null)
        {
            if (_flashRoutine != null)
            {
                StopCoroutine(_flashRoutine);
            }

            _flashRoutine = StartCoroutine(FlashLightRoutine(primary, accent));
        }

        GameFeedback.EnsureInstance()?.PlayGoal();
    }

    IEnumerator FlashLightRoutine(Color core, Color rim)
    {
        _flashLight.enabled = true;
        _flashLight.color = Color.Lerp(core, rim, 0.35f);

        float elapsed = 0f;
        while (elapsed < _flashDuration)
        {
            elapsed += Time.deltaTime;
            float normalized = elapsed / _flashDuration;
            float pulse = Mathf.Sin(normalized * Mathf.PI);
            _flashLight.intensity = _flashPeakIntensity * pulse;
            yield return null;
        }

        _flashLight.intensity = 0f;
        _flashLight.enabled = false;
        _flashRoutine = null;
    }

    ParticleSystem CreateConfettiBurst(Transform parent, string name, GoalCelebrationBurstSettings settings)
    {
        ParticleSystem ps = CreateBase(parent, name);
        ParticleSystem.MainModule main = ps.main;
        main.loop = false;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;
        main.stopAction = ParticleSystemStopAction.None;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        ConfigureConfettiModules(ps);
        ApplyBurstSettings(ps, settings, isConfetti: true);
        return ps;
    }

    static void ConfigureConfettiModules(ParticleSystem ps)
    {
        ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(2f, 5f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        ParticleSystem.RotationOverLifetimeModule rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        rotation.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        rotation.z = new ParticleSystem.MinMaxCurve(-180f, 180f);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.85f, 0.65f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.7f, 0.85f),
            new Keyframe(1f, 0.2f)));
    }

    ParticleSystem CreateSparkBurst(Transform parent, string name, GoalCelebrationBurstSettings settings)
    {
        ParticleSystem ps = CreateBase(parent, name);
        ParticleSystem.MainModule main = ps.main;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;

        ParticleSystem.LimitVelocityOverLifetimeModule limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.drag = 2.5f;

        ApplyBurstSettings(ps, settings, isConfetti: false);
        return ps;
    }

    ParticleSystem CreateRingWave(Transform parent, string name, GoalCelebrationRingSettings settings)
    {
        ParticleSystem ps = CreateBase(parent, name);
        ParticleSystem.MainModule main = ps.main;
        main.loop = false;
        main.startSpeed = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.rotation = new Vector3(90f, 0f, 0f);

        ApplyRingSettings(ps, settings);
        return ps;
    }

    static void ConfigureRingColorOverLifetime(ParticleSystem ps)
    {
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.75f, 0f),
                new GradientAlphaKey(0.35f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
    }

    ParticleSystem CreateStarFountain(Transform parent, string name, GoalCelebrationBurstSettings settings)
    {
        ParticleSystem ps = CreateBase(parent, name);
        ParticleSystem.MainModule main = ps.main;
        main.loop = false;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        ConfigureStarModules(ps);
        ApplyBurstSettings(ps, settings, isConfetti: false, isStar: true);
        return ps;
    }

    static void ConfigureStarModules(ParticleSystem ps)
    {
        ParticleSystem.RotationOverLifetimeModule rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        rotation.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        rotation.z = new ParticleSystem.MinMaxCurve(120f, 240f);
    }

    ParticleSystem CreateSmokePuff(Transform parent, string name, GoalCelebrationBurstSettings settings)
    {
        ParticleSystem ps = CreateBase(parent, name);
        ParticleSystem.MainModule main = ps.main;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;

        ConfigureSmokeModules(ps);
        ApplyBurstSettings(ps, settings, isConfetti: false, isSmoke: true);
        return ps;
    }

    static void ConfigureSmokeModules(ParticleSystem ps)
    {
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.45f, 0f),
                new GradientAlphaKey(0.2f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.5f),
            new Keyframe(1f, 1.4f)));
    }

    static ParticleSystem CreateBase(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;

        return ps;
    }

    static void SetupRenderer(ParticleSystem ps, bool additive)
    {
        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = CreateParticleMaterial(additive);
    }

    static Material CreateParticleMaterial(bool additive)
    {
        string[] shaderNames =
        {
            "Universal Render Pipeline/Particles/Unlit",
            "Particles/Universal Forward",
            "Particles/Standard Unlit",
            "Mobile/Particles/Additive",
            "Sprites/Default"
        };

        for (int i = 0; i < shaderNames.Length; i++)
        {
            Shader shader = Shader.Find(shaderNames[i]);
            if (shader == null)
            {
                continue;
            }

            Material material = new Material(shader);
            material.hideFlags = HideFlags.HideAndDontSave;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.white);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", Color.white);
            }

            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt(
                "_DstBlend",
                (int)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            material.SetInt("_Cull", (int)CullMode.Off);
            material.SetInt("_ZWrite", 0);
            material.renderQueue = (int)RenderQueue.Transparent;

            return material;
        }

        return new Material(Shader.Find("Sprites/Default"));
    }

    static void ApplyPalette(ParticleSystem ps, Color primary, Color secondary, Color accent)
    {
        ParticleSystem.MainModule main = ps.main;
        var startGradient = new Gradient();
        startGradient.SetKeys(
            new[]
            {
                new GradientColorKey(primary, 0f),
                new GradientColorKey(secondary, 0.35f),
                new GradientColorKey(accent, 0.7f),
                new GradientColorKey(Color.Lerp(primary, Color.white, 0.35f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            });
        main.startColor = new ParticleSystem.MinMaxGradient(startGradient);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ps.colorOverLifetime;
        if (!colorOverLifetime.enabled)
        {
            return;
        }

        var lifetimeGradient = new Gradient();
        lifetimeGradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.Lerp(primary, secondary, 0f), 0f),
                new GradientColorKey(Color.Lerp(primary, secondary, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.85f, 0.65f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(lifetimeGradient);
    }
}
